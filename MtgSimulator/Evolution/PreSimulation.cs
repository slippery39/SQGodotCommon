using System.Diagnostics;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Plays random constructed decks to measure the card pool BEFORE evolution starts.
///
/// **This exists to break a self-reinforcing loop.** A card only accumulates constructed data
/// by being in a deck, and it only gets into a deck by having good data — so a card that starts
/// unmeasured tends to stay unmeasured. Measured on a 100-generation run: the cards in final
/// decks had a median of 11 310 games while the rest had 998, and only 343 of 785 cards were
/// ever tried at all. Whatever was picked up early got entrenched, and the valuations for
/// everything else stayed at the draft prior forever.
///
/// Random decks sample the pool uniformly, so every card gets games on equal terms before any
/// selection pressure exists. Same idea as DraftTrainer's Curve/Random drafters and for the
/// same reason recorded there: **a model must be seeded by something card-agnostic, or it
/// measures the picker rather than the card.**
///
/// **Card coverage is excellent and pair coverage is not**, and that is arithmetic rather than
/// a tuning problem: a 60-card deck holds ~15 distinct cards, which is 105 pairs out of the
/// ~83 000 a 408-card pool can form. Cards get hundreds of games each in minutes; pairs need
/// orders of magnitude more games to reach the same density. Treat the presim as a CARD-value
/// bootstrap that also starts the pair table, not as a synergy oracle — evolution densifies
/// pairs later by playing the same cards together repeatedly.
/// </summary>
public static class PreSimulation
{
	private readonly record struct ScheduledGame(int Deck1, int Deck2, int GameSeed);

	/// <summary>
	/// Builds a decklist by uniform sampling — no card values, no curve target, no synergy.
	/// Deliberately knows nothing: this is the control that measures the pool rather than
	/// measuring the sampler.
	/// </summary>
	public static Decklist RandomDeck(string name, IReadOnlyList<Card> spells, Random rng)
	{
		var lands = Decklist.MinLands + rng.Next(Decklist.MaxLands - Decklist.MinLands + 1);
		var deck = Decklist.Empty(name) with { Lands = lands };

		var guard = 0;
		while (deck.SpellCount < Decklist.DeckSize - lands)
		{
			if (guard++ > Decklist.DeckSize * 4)
				break;

			var card = spells[rng.Next(spells.Count)];
			var room = Decklist.MaxCopies - deck.CopiesOf(card.Name);
			if (room <= 0)
				continue;

			var need = Decklist.DeckSize - lands - deck.SpellCount;
			var add = Math.Min(Math.Min(1 + rng.Next(Decklist.MaxCopies), room), need);
			deck = deck.WithCopies(card.Name, deck.CopiesOf(card.Name) + add);
		}

		return deck;
	}

	/// <summary>
	/// Plays <paramref name="deckCount"/> random decks against each other and returns the
	/// counts. Same three-phase shape as everything else here: build sequentially, play in one
	/// parallel batch into a pre-allocated array, fold in sequentially.
	/// </summary>
	/// <param name="opponentsPerDeck">
	/// How many opponents each deck faces. Games scale as deckCount * opponentsPerDeck / 2, so
	/// this is the main cost knob.
	/// </param>
	public static DraftTrainingData Run(
		IReadOnlyList<Card> pool,
		int deckCount,
		int opponentsPerDeck,
		int seed,
		int aiDepth
	)
	{
		var spells = pool.Where(c => !c.HasSubtype("Land")).ToList();
		var index = ConstructedGameSetup.PoolIndex(spells);

		var rng = new Random(seed);
		var decks = Enumerable
			.Range(0, deckCount)
			.Select(i => RandomDeck($"R{i}", spells, rng))
			.Where(d => d.IsValid)
			.ToList();

		var schedule = new List<ScheduledGame>();
		var gameIndex = 0;
		for (var i = 0; i < decks.Count; i++)
		{
			for (var k = 0; k < opponentsPerDeck; k++)
			{
				var j = rng.Next(decks.Count);
				if (j == i)
					continue;
				// Alternate the seat so the play/draw advantage does not attach to low indices.
				var (a, b) = k % 2 == 0 ? (i, j) : (j, i);
				schedule.Add(new ScheduledGame(a, b, seed + 500_000 + gameIndex++ * 5));
			}
		}

		Console.WriteLine(
			$"  Pre-simulation: {decks.Count} random decks, {schedule.Count} games "
				+ $"(uniform sampling — measures the pool, not a picker)..."
		);

		var results = new GameResult[schedule.Count];
		var completed = 0;
		var timer = Stopwatch.StartNew();

		Parallel.For(
			0,
			schedule.Count,
			s =>
			{
				var g = schedule[s];
				var (state, ids, cardNames) = ConstructedGameSetup.Build(
					decks[g.Deck1],
					decks[g.Deck2],
					index
				);
				var aiRng = new Random(g.GameSeed + 4);
				var runner = new GameRunner(
					new MultiTurnBeamSearchAiStrategy(
						ids,
						aiDepth,
						rng: aiRng,
						cardValues: AiCardValues.Current
					),
					new MultiTurnBeamSearchAiStrategy(
						ids,
						aiDepth,
						rng: aiRng,
						cardValues: AiCardValues.Current
					)
				);
				var (result, _) = runner.Run(
					state,
					ids,
					cardNames,
					shuffleSeed: g.GameSeed + 2,
					gameRngSeed: g.GameSeed + 3
				);
				results[s] = result with { AllEvents = [] };

				var done = Interlocked.Increment(ref completed);
				if (done % 1000 == 0)
					Console.WriteLine($"    presim {done} / {schedule.Count}...");
			}
		);
		timer.Stop();

		var accumulator = new CardStatAccumulator();
		var excluded = 0;
		foreach (var (g, result) in schedule.Zip(results))
		{
			if (
				result.EndReason
				is GameEndReason.TimeLimitReached
					or GameEndReason.UnhandledException
			)
			{
				excluded++;
				continue;
			}

			accumulator.Add(
				result.Player1DrawnCards,
				decks[g.Deck1].Spells.Keys.ToList(),
				result.IsPlayer1Win
			);
			accumulator.Add(
				result.Player2DrawnCards,
				decks[g.Deck2].Spells.Keys.ToList(),
				result.IsPlayer2Win
			);
		}

		var data = accumulator.ToData();
		var cardGames = data.Cards.Select(c => c.Games).OrderBy(g => g).ToList();
		var pairGames = data.Pairs.Select(p => p.Games).OrderBy(g => g).ToList();

		Console.WriteLine(
			$"    {schedule.Count - excluded} games in {timer.Elapsed.TotalMinutes:F1}m, "
				+ $"{excluded} excluded. Base rate {data.Prior:P1}."
		);
		Console.WriteLine(
			$"    {data.Cards.Count}/{spells.Count} cards measured, median "
				+ $"{(cardGames.Count > 0 ? cardGames[cardGames.Count / 2] : 0)} games/card; "
				+ $"{data.Pairs.Count} pairs, median "
				+ $"{(pairGames.Count > 0 ? pairGames[pairGames.Count / 2] : 0)} games/pair."
		);

		var usablePairs = data.Pairs.Count(p => p.Games >= ConstructedValues.MinPairGames);
		Console.WriteLine(
			$"    {usablePairs} pairs clear the {ConstructedValues.MinPairGames}-game gate"
				+ (
					usablePairs == 0
						? " — pair coverage needs far more games; card values are the usable output here."
						: "."
				)
		);

		return data;
	}
}
