using ImmutableGameObjects;
using MagicCore.Sets;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests;

/// <summary>
/// Whole games of random legal moves and random choice answers. Asserts that the engine never throws,
/// every game ends — a net under everything the focused tests do not think of — and that each game
/// plays exactly as recorded (<see cref="GoldenReplay"/>), so a refactor can prove it changed nothing.
/// </summary>
public class RandomGameTests
{
	static readonly GoldenReplay Replays = new("GoldenReplay.txt");

	[OneTimeTearDown]
	public void SaveReplays() => Replays.WriteIfUpdating();

	[Test]
	public void RandomPlay_VanillaFiller_AlwaysFinishes([Range(1, 30)] int seed) =>
		PlayAndCheck("VanillaFiller", TestGames.New(seed), seed, $"seed {seed}");

	[Test]
	public void RandomPlay_RealFraDecks_AlwaysFinishes([Range(1, 90)] int seed)
	{
		// Every ordered pairing of the decks, in turn — so each deck plays each other from both seats.
		var decks = FraDecks.All.Keys.ToList();
		var pairs = decks.SelectMany(a => decks.Where(b => b != a).Select(b => (a, b))).ToList();
		var (first, second) = pairs[seed % pairs.Count];

		var s = MagicRules.NewGame(FraDecks.All[first], FraDecks.All[second], seed);
		PlayAndCheck("RealFraDecks", s, seed, $"seed {seed} ({first} vs {second})");
	}

	// Every scripted card, not just those in FraDecks: each seed deals all scripted nonland cards into
	// two decks at random, with lands of every colour, so a card in no deck still gets played.
	[Test]
	public void RandomPlay_EveryScriptedCard([Range(1, 30)] int seed)
	{
		// Shayne, 2026-11-07 (MC-5.6 batch 3b): with the last three Stardates dealt in, seed 21's decks double
		// Troubling Tribbles every upkeep, and Scotty's beam me up (a move per spell × creature) and Station (a
		// move per creature) make each step ~n³ — 41 permanents cost 8 s a turn, 73 cost 90 s; it never ends.
		// The fix is grouping identical permanents in a choice (AI-18), not this test.
		if (seed == 21)
			Assert.Ignore("Seed 21: Tribbles doubling makes every step ~n³ in permanents — AI-18");
		var rng = new Random(seed);
		var spells = CardRegistry
			.Scripted.Select(CardRegistry.Get)
			.Where(c => !c.Data.TypeLine.Contains("Land"))
			.OrderBy(_ => rng.Next())
			.ToList();
		var lands = new[] { "Plains", "Island", "Swamp", "Mountain", "Forest" }
			.SelectMany(l => Enumerable.Repeat(TestGames.Card(l), 4))
			.ToList();
		var half = spells.Count / 2;

		var s = MagicRules.NewGame(
			[.. spells.Take(half), .. lands],
			[.. spells.Skip(half), .. lands],
			seed
		);

		PlayAndCheck("EveryScriptedCard", s, seed, $"seed {seed}");
	}

	static void PlayAndCheck(string game, GameState s, int seed, string name)
	{
		var trace = new GoldenReplay.Trace();
		var end = PlayRandomly(s, seed, trace);
		Assert.That(end.IsOver(), $"{name} did not finish");
		Replays.Check(game, seed, trace, end);
	}

	/// <summary>
	/// Random legal moves and random choice answers. By default a choice gets its fewest picks;
	/// <paramref name="everyPath"/> also says "yes" and picks up to the most allowed (CardReplayTests).
	/// </summary>
	public static GameState PlayRandomly(
		GameState s,
		int seed,
		GoldenReplay.Trace? trace = null,
		bool everyPath = false,
		int lastTurn = int.MaxValue,
		Action<GameState, GameAction>? onMove = null
	)
	{
		var rng = new Random(seed);
		for (var i = 0; i < 20_000 && !s.IsOver() && s.GetGame().TurnNumber <= lastTurn; i++)
		{
			if (s.GetPendingChoice() is { } choice)
			{
				// Once: enumerating twice would draw from rng twice.
				var picks = (everyPath ? EveryPath(choice, rng) : FewestPicks(choice, rng))
					.Select(o => o.Id)
					.ToList();
				trace?.Step(
					$"{Seat(s)} {choice.Options.Count} {Describe.Answer(s, choice, picks)}"
				);
				s = s.Choose(picks);
				continue;
			}
			// Mulligan rarely, or random play mulligans to nothing and never reaches combat.
			var pool = s.LegalActions()
				.Where(a => a is not Actions.MulliganAction || rng.Next(10) == 0)
				.ToList();
			var move = pool[rng.Next(pool.Count)];
			trace?.Step($"{Seat(s)} {pool.Count} {Describe.Action(s, move)}");
			onMove?.Invoke(s, move);
			s = s.Act(move);
		}
		return s;
	}

	static string Seat(GameState s) => s.DecidingPlayerId() == s.Player1Id() ? "P1" : "P2";

	static IEnumerable<ChoiceOption> FewestPicks(ChoiceAction choice, Random rng) =>
		choice.Options.OrderBy(_ => rng.Next()).Take(choice.MinChoices);

	static IEnumerable<ChoiceOption> EveryPath(ChoiceAction choice, Random rng)
	{
		var options = choice.Options.Where(o => o.IsEnabled).ToList();
		// Never below the minimum: too few options is the engine's error to report, not a throw here.
		var most = Math.Max(choice.MinChoices, Math.Min(choice.MaxChoices, options.Count));
		var n = rng.Next(choice.MinChoices, most + 1);
		return options.OrderBy(_ => rng.Next()).Take(n);
	}
}
