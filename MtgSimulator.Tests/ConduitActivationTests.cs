using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Does the pilot actually tap Wirewood Conduit, and does it do so in BOTH fixtures?**
///
/// The card reads *"Exhaust: add mana equal to the number of Elves you control"*, and it is the one
/// card where three instruments disagree: games-in-hand rates it **+1.4** inside the hand-built elf
/// deck, `OutputProbe` puts it slightly BELOW its slot average, and the builder has never once
/// proposed it. At most one of those is right about the card.
///
/// `AbilityActivatedEvent` exists precisely because activating an ability used to name the card in
/// no event at all, which made any activated-ability engine invisible to anything reading a log.
/// Counting it separates two explanations that need opposite fixes:
///
/// - **Zero activations** — the pilot never explores the tap, because activating scores ~0 against
///   an evaluator that excludes temporary mana. Then the probe is correctly measuring "unusable as
///   piloted", and no amount of valuing mana in the PROBE changes anything.
/// - **Many activations** — the mana is being produced, and whatever it buys is either not reaching
///   the scoreboard or not being counted by the output vector.
/// </summary>
[TestFixture]
public class ConduitActivationTests
{
	private const string Conduit = "Wirewood Conduit";

	private static (int Activations, int Turns, int Damage, int Entered, int Drawn) CountIn(
		Decklist deck,
		IReadOnlyDictionary<string, Card> pool,
		Func<int, IReadOnlyList<Card>> opponent,
		int seed,
		int maxTurns,
		int selfActions = 1,
		int selfGreedyTurns = int.MaxValue
	)
	{
		const bool deckIsPlayer1 = true;
		var (state, ids, cardNames) = GameSetup.FromDecks(
			owner => deck.Materialize(owner, pool),
			opponent
		);

		var rng = new Random(seed + 4);
		var runner = new GameRunner(
			new MultiTurnBeamSearchAiStrategy(
				ids,
				2,
				rng: rng,
				cardValues: AiCardValues.Current,
				selfActionsPerTurn: selfActions,
				selfGreedyTurns: selfGreedyTurns
			),
			new MultiTurnBeamSearchAiStrategy(ids, 2, rng: rng, cardValues: AiCardValues.Current),
			maxTurns: maxTurns
		);

		var (result, final) = runner.Run(state, ids, cardNames, seed + 2, seed + 3);

		var conduitIds = cardNames
			.Where(kv => string.Equals(kv.Value, Conduit, StringComparison.Ordinal))
			.Select(kv => kv.Key)
			.ToHashSet();

		var activations = result.AllEvents.Count(e =>
			e is AbilityActivatedEvent a && conduitIds.Contains(a.CardId)
		);

		// **Whether the card ever REACHED play, which the activation count alone cannot say.** A
		// zero that means "never drawn it" and a zero that means "drawn and never used" are opposite
		// findings, and only one of them is about the AI.
		var entered = result.AllEvents.Count(e =>
			e is CreatureEnteredBattlefieldEvent c && conduitIds.Contains(c.CardId)
		);
		var drawn = (deckIsPlayer1 ? result.Player1DrawnCards : result.Player2DrawnCards).Count(n =>
			string.Equals(n, Conduit, StringComparison.Ordinal)
		);

		var them = final.GetPlayer(ids.Player2Id);
		return (activations, result.TurnCount, them.StartingLife - them.Life, entered, drawn);
	}

	/// <summary>
	/// **Three rollout arms against the same games.** The claim under test is that a one-action
	/// simulated turn cannot represent a mana engine's payoff (*activate, cast, cast, cast*), so the
	/// pilot prices the Conduit as a 1/1 for 2 and leaves it in hand. If widening the budget does not
	/// move the CAST count, the theory is wrong and nothing further is worth building on it.
	/// </summary>
	[Test]
	[Explicit("Diagnostic — rollout width against Conduit casts.")]
	public void DoesAWiderRolloutMakeThePilotPlayIt()
	{
		Directory.SetCurrentDirectory(
			Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."))
		);

		var pool = SetRegistry
			.Designed.Cards.Where(c => !c.HasSubtype("Land"))
			.ToDictionary(c => c.Name, c => c, StringComparer.Ordinal);

		var built = DeckRegistry.Build(DesignedGauntletDecks.Elves, 1);
		var elves = new Decklist(
			"CMB Elves",
			built
				.Where(c => !c.HasSubtype("Land"))
				.GroupBy(c => c.Name, StringComparer.Ordinal)
				.ToImmutableSortedDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
			built.Count(c => c.HasSubtype("Land"))
		);

		var field = DecklistStore
			.Load(Directory.GetFiles("sim_results", "metagame_des_*.json").OrderBy(f => f).Last())!
			.Decks;

		foreach (
			var (label, actions, turns) in new[]
			{
				("off (1 action)", 1, int.MaxValue),
				("first turn only (3)", 3, 1),
				("all turns (3)", 3, int.MaxValue),
			}
		)
		{
			var drawn = 0;
			var entered = 0;
			var acts = 0;
			var elapsed = System.Diagnostics.Stopwatch.StartNew();

			for (var i = 0; i < 8; i++)
			{
				var opp = field[i % field.Count];
				var r = CountIn(
					elves,
					pool,
					owner => opp.Materialize(owner, pool),
					90_000 + i * 31,
					100,
					actions,
					turns
				);
				drawn += r.Drawn;
				entered += r.Entered;
				acts += r.Activations;
			}

			elapsed.Stop();
			TestContext.Out.WriteLine(
				$"  {label, -22} drawn {drawn, 3}  entered {entered, 3}  activations {acts, 3}  "
					+ $"{elapsed.ElapsedMilliseconds, 6} ms"
			);
		}
	}

	[Test]
	[Explicit("Diagnostic — is the tap ability ever used, and in which fixture.")]
	public void IsTheConduitEverTapped()
	{
		Directory.SetCurrentDirectory(
			Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."))
		);

		var pool = SetRegistry
			.Designed.Cards.Where(c => !c.HasSubtype("Land"))
			.ToDictionary(c => c.Name, c => c, StringComparer.Ordinal);

		// Rebuilt as a Decklist from the registry's card list — the gauntlet decks are raw card
		// lists and everything downstream here takes a Decklist.
		var built = DeckRegistry.Build(DesignedGauntletDecks.Elves, 1);
		var elves = new Decklist(
			"CMB Elves",
			built
				.Where(c => !c.HasSubtype("Land"))
				.GroupBy(c => c.Name, StringComparer.Ordinal)
				.ToImmutableSortedDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
			built.Count(c => c.HasSubtype("Land"))
		);

		var field = DecklistStore
			.Load(Directory.GetFiles("sim_results", "metagame_des_*.json").OrderBy(f => f).Last())!
			.Decks;

		TestContext.Out.WriteLine("  REAL GAMES (vs the evolved field)");
		for (var i = 0; i < 4; i++)
		{
			var opp = field[i % field.Count];
			var r = CountIn(
				elves,
				pool,
				owner => opp.Materialize(owner, pool),
				90_000 + i * 31,
				100
			);
			TestContext.Out.WriteLine(
				$"    vs {opp.Name, -28} drawn {r.Drawn}  entered {r.Entered}  activations {r.Activations, 3}  turns {r.Turns, 3}"
			);
		}

		TestContext.Out.WriteLine("\n  SOLITAIRE (the OutputProbe fixture, inert unkillable seat)");
		var inert = Decklist.Empty("Inert") with { Lands = Decklist.DeckSize };
		for (var i = 0; i < 4; i++)
		{
			var r = CountIn(
				elves,
				pool,
				owner => inert.Materialize(owner, pool),
				60_000 + i * 101,
				OutputProbe.DefaultTurns
			);
			TestContext.Out.WriteLine(
				$"    seed {i}                          drawn {r.Drawn}  entered {r.Entered}  activations {r.Activations, 3}  turns {r.Turns, 3}"
			);
		}
	}
}
