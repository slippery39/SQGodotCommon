using System.Diagnostics;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds draft knowledge by brute force: run N drafts, play the resulting decks against
/// each other, and record which cards — and which card pairs — were in hand for wins.
///
/// Drafts run first and sequentially (they are cheap and must stay deterministic), then
/// every game across every draft is played in one parallel batch, then results are folded
/// in sequentially so the output does not depend on thread scheduling.
/// </summary>
public class DraftTrainer
{
	private readonly DraftFormat _format;
	private readonly int _draftCount;
	private readonly int _seatCount;
	private readonly int _seed;
	private readonly int _aiDepth;
	private readonly int _gamesPerPair;
	private readonly DraftTrainingData? _bootstrap;

	/// <param name="bootstrap">
	/// When supplied, seats draft with the trained picker instead of Curve/Random. This is
	/// how a second training run sharpens the model toward decks the AI actually builds —
	/// re-run training with the existing file and merge.
	/// </param>
	public DraftTrainer(
		DraftFormat format,
		int draftCount,
		int seatCount,
		int seed,
		int aiDepth = 2,
		int gamesPerPair = 1,
		DraftTrainingData? bootstrap = null
	)
	{
		if (seatCount < 2)
			throw new ArgumentOutOfRangeException(nameof(seatCount), "Need at least two seats.");
		if (draftCount < 1)
			throw new ArgumentOutOfRangeException(nameof(draftCount), "Need at least one draft.");
		_format = format;
		_draftCount = draftCount;
		_seatCount = seatCount;
		_seed = seed;
		_aiDepth = aiDepth;
		_gamesPerPair = gamesPerPair;
		_bootstrap = bootstrap;
	}

	private readonly record struct ScheduledGame(int Draft, int Seat1, int Seat2, int GameSeed);

	/// Returns the counts from THIS run only. Merging with any existing file is the
	/// caller's decision (see DraftTrainingStore.SaveMerged).
	public DraftTrainingData Run()
	{
		var drafterLabel = _bootstrap is null ? "Curve/Random" : "Trained (all seats)";
		Console.WriteLine(
			$"Training: {_draftCount} drafts x {_seatCount} seats ({_format}), "
				+ $"drafters: {drafterLabel}, AI depth {_aiDepth}, seed {_seed}"
		);

		// --- Phase 1: drafts (sequential, deterministic) ---
		var draftTimer = Stopwatch.StartNew();
		var pools = new List<IReadOnlyList<Card>>[_draftCount];
		for (var d = 0; d < _draftCount; d++)
		{
			var draftSeed = _seed + d * 1000;
			var final = Draft.RunToCompletion(
				Draft.Create(_format, CardLibrary.All, draftSeed, _seatCount),
				BuildPickers(draftSeed)
			);
			pools[d] = final.Seats.Select(s => (IReadOnlyList<Card>)s.Pool).ToList();
		}
		draftTimer.Stop();

		var schedule = BuildSchedule();
		Console.WriteLine(
			$"  {_draftCount} drafts in {draftTimer.ElapsedMilliseconds}ms. "
				+ $"Playing {schedule.Count} games..."
		);

		// --- Phase 2: games (parallel into a pre-allocated array, so order is fixed) ---
		var results = new GameResult[schedule.Count];
		var completed = 0;
		var gameTimer = Stopwatch.StartNew();
		// Measured ~1.8x over sequential and flat past 4 threads — the engine's immutable
		// collections make this memory-bound, not CPU-bound, so there is no DOP to tune.
		Parallel.For(
			0,
			schedule.Count,
			i =>
			{
				var g = schedule[i];
				var (state, ids, cardNames) = DraftGameSetup.Build(
					pools[g.Draft][g.Seat1],
					pools[g.Draft][g.Seat2]
				);
				var aiRng = new Random(g.GameSeed + 4);
				var runner = new GameRunner(
					new MultiTurnBeamSearchAiStrategy(ids, _aiDepth, rng: aiRng),
					new MultiTurnBeamSearchAiStrategy(ids, _aiDepth, rng: aiRng)
				);
				var (result, _) = runner.Run(
					state,
					ids,
					cardNames,
					shuffleSeed: g.GameSeed + 2,
					gameRngSeed: g.GameSeed + 3
				);
				results[i] = result;

				var done = Interlocked.Increment(ref completed);
				if (done % 200 == 0)
					Console.WriteLine($"    {done} / {schedule.Count} games...");
			}
		);
		gameTimer.Stop();

		// --- Phase 3: fold in results (sequential — independent of thread order) ---
		var accumulator = new Accumulator();
		for (var i = 0; i < schedule.Count; i++)
		{
			var g = schedule[i];
			var result = results[i];
			// The deck, not the whole pool: BuildDeck plays only the first maxSpells picks,
			// and the padded Plains must stay out of the counts entirely.
			accumulator.Add(
				result.Player1DrawnCards,
				DeckSpellsOf(pools[g.Draft][g.Seat1]),
				result.IsPlayer1Win
			);
			accumulator.Add(
				result.Player2DrawnCards,
				DeckSpellsOf(pools[g.Draft][g.Seat2]),
				result.IsPlayer2Win
			);
		}

		var data = accumulator.ToData();
		PrintSummary(data, results, gameTimer.ElapsedMilliseconds);
		return data;
	}

	/// <summary>
	/// Every seat at a table must draft with a picker of the SAME STRENGTH, or card rates
	/// measure the drafter instead of the card.
	///
	/// Mixing a strong picker with weak ones was tried and is actively harmful: a card the
	/// model likes gets taken by the Trained seats, so it lands in decks winning ~84% while
	/// cards it dislikes land in decks winning ~34%. Its "win rate" then reflects *who
	/// drafted it*. Feeding that back compounds it — measured over 10 generations, the
	/// correlation between a card's starting rate and how much its rate moved was +0.63,
	/// the top 20 cards gained 7.8pp and the bottom 20 lost 9.5pp, and the rate range blew
	/// out from 44-67% to 31-79% while actual play strength got slightly *worse*.
	///
	/// So: no model means Curve/Random (equally weak); a model means all seats Trained
	/// (equally strong). Deck diversity then comes from the picker's softmax temperature.
	/// The opposite failure mode to watch for is diversity collapse — if every deck becomes
	/// the same, card rates decay toward the base rate. Diagnose by the spread of card win
	/// rates across generations: it should stay roughly flat, not explode and not collapse.
	/// </summary>
	private IReadOnlyList<DraftPicker> BuildPickers(int draftSeed) =>
		Enumerable
			.Range(0, _seatCount)
			.Select(i =>
				_bootstrap is not null
					? DraftPickers.Trained(_bootstrap, new Random(draftSeed + 100 + i))
				: i % 2 == 0 ? (DraftPicker)DraftPickers.Curve
				: DraftPickers.Random(new Random(draftSeed + 100 + i))
			)
			.ToList();

	private List<ScheduledGame> BuildSchedule()
	{
		var schedule = new List<ScheduledGame>();
		var gameIndex = 0;
		for (var d = 0; d < _draftCount; d++)
		{
			for (var a = 0; a < _seatCount; a++)
			{
				for (var b = a + 1; b < _seatCount; b++)
				{
					for (var k = 0; k < _gamesPerPair; k++)
					{
						// Alternate who is on the play so the play/draw advantage does not
						// systematically bias every card in the lower-indexed seat's pool.
						var (p1, p2) = k % 2 == 0 ? (a, b) : (b, a);
						schedule.Add(
							new ScheduledGame(d, p1, p2, _seed + 1_000_000 + gameIndex++ * 5)
						);
					}
				}
			}
		}
		return schedule;
	}

	/// The cards that actually make the deck — must mirror Draft.BuildDeck's selection.
	private static IReadOnlyList<string> DeckSpellsOf(IReadOnlyList<Card> pool) =>
		pool.Where(c => !c.HasSubtype("Land"))
			.Take(Draft.DefaultMaxSpells)
			.Select(c => c.Name)
			.ToList();

	private static void PrintSummary(
		DraftTrainingData data,
		IReadOnlyList<GameResult> results,
		long elapsedMs
	)
	{
		var draws = results.Count(r => r.IsDraw);
		var flagged = results.Count(r => r.IsFlagged);

		Console.WriteLine();
		Console.WriteLine("  --- Training Summary ---");
		Console.WriteLine();
		Console.WriteLine($"  Games played:     {results.Count}  ({elapsedMs / 1000.0:F1}s)");
		Console.WriteLine($"  Draws:            {draws}");
		Console.WriteLine($"  Flagged games:    {flagged}");
		Console.WriteLine($"  Deck-games:       {data.Perspectives}");
		Console.WriteLine($"  Base win rate:    {data.Prior:P1}");
		Console.WriteLine($"  Cards learned:    {data.Cards.Count}");
		Console.WriteLine($"  Pairs learned:    {data.Pairs.Count}");

		if (data.Cards.Count > 0)
		{
			var medianCardGames = data.Cards.Select(c => c.Games).OrderBy(g => g).ToList()[
				data.Cards.Count / 2
			];
			Console.WriteLine($"  Median games/card: {medianCardGames}");
		}
		if (data.Pairs.Count > 0)
		{
			var medianPairGames = data.Pairs.Select(p => p.Games).OrderBy(g => g).ToList()[
				data.Pairs.Count / 2
			];
			Console.WriteLine($"  Median games/pair: {medianPairGames}");
			if (medianPairGames < 20)
				Console.WriteLine(
					"  NOTE: pair data is thin — synergy scores will be mostly shrunk to the "
						+ "base rate. Run more drafts and merge."
				);
		}
		Console.WriteLine();
	}

	/// <summary>
	/// Accumulates games-in-hand counts. A card is credited once per game in which it was
	/// drawn regardless of how many copies were drawn, and a pair only when both halves
	/// were drawn in that same game.
	/// </summary>
	private sealed class Accumulator
	{
		private readonly Dictionary<string, (int Games, int Wins, int DeckGames)> _cards =
			new(StringComparer.Ordinal);
		private readonly Dictionary<
			(string A, string B),
			(int Games, int Wins, int DeckGames)
		> _pairs = [];
		private int _perspectives;
		private int _wins;

		/// <param name="deckSpells">
		/// The non-land cards actually in this deck. Recorded whether drawn or not — the
		/// drawn/in-deck ratio is what lets the picker weigh a pair term (needs both cards
		/// drawn) against a card term (needs only one) on a common per-game scale.
		/// </param>
		public void Add(
			IReadOnlyList<string> drawnCards,
			IReadOnlyList<string> deckSpells,
			bool won
		)
		{
			_perspectives++;
			if (won)
				_wins++;

			// Sorted so every pair key is (A <= B).
			var deck = deckSpells
				.Distinct(StringComparer.Ordinal)
				.OrderBy(n => n, StringComparer.Ordinal)
				.ToList();
			var inDeck = deck.ToHashSet(StringComparer.Ordinal);
			var drawn = drawnCards
				.Where(inDeck.Contains)
				.Distinct(StringComparer.Ordinal)
				.OrderBy(n => n, StringComparer.Ordinal)
				.ToList();
			var wasDrawn = drawn.ToHashSet(StringComparer.Ordinal);

			foreach (var name in deck)
			{
				var c = _cards.GetValueOrDefault(name);
				var hit = wasDrawn.Contains(name);
				_cards[name] = (
					c.Games + (hit ? 1 : 0),
					c.Wins + (hit && won ? 1 : 0),
					c.DeckGames + 1
				);
			}

			for (var i = 0; i < deck.Count; i++)
			{
				for (var j = i + 1; j < deck.Count; j++)
				{
					var key = (deck[i], deck[j]);
					var p = _pairs.GetValueOrDefault(key);
					var hit = wasDrawn.Contains(deck[i]) && wasDrawn.Contains(deck[j]);
					_pairs[key] = (
						p.Games + (hit ? 1 : 0),
						p.Wins + (hit && won ? 1 : 0),
						p.DeckGames + 1
					);
				}
			}
		}

		public DraftTrainingData ToData() =>
			new(
				_perspectives,
				_wins,
				_cards
					.OrderBy(kv => kv.Key, StringComparer.Ordinal)
					.Select(kv => new CardStat(
						kv.Key,
						kv.Value.Games,
						kv.Value.Wins,
						kv.Value.DeckGames
					))
					.ToList(),
				_pairs
					.OrderBy(kv => kv.Key.A, StringComparer.Ordinal)
					.ThenBy(kv => kv.Key.B, StringComparer.Ordinal)
					.Select(kv => new PairStat(
						kv.Key.A,
						kv.Key.B,
						kv.Value.Games,
						kv.Value.Wins,
						kv.Value.DeckGames
					))
					.ToList()
			);
	}
}
