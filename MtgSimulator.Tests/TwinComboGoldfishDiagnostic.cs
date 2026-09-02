using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// **What actually happens in a real goldfish game with the Twin deck?**
///
/// Everything upstream checks out: the engine executes the loop, `LoopDetector` finds it, and
/// `TwinComboPilotTests` shows the production AI turning it over six times unprompted when both
/// halves are on the battlefield. Yet mode 7 reads `depth 1.0` and `kill 99` — never wins.
///
/// So this replays the real thing and prints the timeline: when each half arrives, how many times
/// the ability fires, and what the board looks like. `Goldfish.Play` discards the event log
/// deliberately (it is the expensive thing to hold), so the game is rebuilt here the same way it
/// builds one.
/// </summary>
[TestFixture]
public class TwinComboGoldfishDiagnostic
{
	private static readonly string[] Copiers = ["Twinflame Artisan", "Kilnmother Vess"];
	private static readonly string[] Untappers = ["Mirevale Deceiver", "Tidebinder Sprite"];

	[Test]
	[Explicit("Diagnostic — prints a turn-by-turn timeline of the Twin deck goldfishing.")]
	public void DumpWhatHappensInARealGame()
	{
		var pool = SetRegistry.Designed.Cards.ToDictionary(
			c => c.Name,
			c => c,
			StringComparer.Ordinal
		);

		// A PURE combo deck — no good stuff at all. That removes the "it wins with its flex slots"
		// confound in one direction and the "flex slots crowded the combo out" confound in the
		// other: 16 combo cards, 24 lands, 20 copies of a vanilla body to make a legal 60.
		var deck = Decklist.Empty("Twin-Pure") with
		{
			Lands = 24,
		};
		foreach (var name in Copiers.Concat(Untappers))
			deck = deck with { Spells = deck.Spells.SetItem(name, 4) };

		// 60 cards needs 36 spells; 16 are the combo, so 20 more at a max of 4 each means five
		// distinct fillers. Cheap vanilla-ish bodies, chosen by cost so they cannot out-compete the
		// combo for mana and cannot themselves be a win condition worth measuring.
		var combo = Copiers.Concat(Untappers).ToHashSet(StringComparer.Ordinal);
		foreach (
			var f in SetRegistry
				.Designed.Cards.Where(c =>
					!combo.Contains(c.Name)
					&& c.HasComponent<CreatureComponent>()
					&& !c.HasSubtype("Land")
					&& c.ManaCost == 1
				)
				.OrderBy(c => c.Name, StringComparer.Ordinal)
				.Take(5)
		)
			deck = deck with { Spells = deck.Spells.SetItem(f.Name, 4) };

		TestContext.Out.WriteLine(
			$"deck: {deck.Lands} lands + {deck.Spells.Sum(kv => kv.Value)} spells "
				+ $"= {deck.Lands + deck.Spells.Sum(kv => kv.Value)}   valid={deck.IsValid}"
		);
		foreach (var (k, v) in deck.Spells.OrderBy(kv => kv.Key, StringComparer.Ordinal))
			TestContext.Out.WriteLine($"   {v}x {k}");

		for (var game = 0; game < 5; game++)
			ReportOneGame(deck, pool, 50_000 + game * 11);
	}

	private static void ReportOneGame(
		Decklist deck,
		IReadOnlyDictionary<string, Card> pool,
		int seed
	)
	{
		var inert = Decklist.Empty("Inert") with { Lands = Decklist.DeckSize };
		var (state, ids, cardNames) = GameSetup.FromDecks(
			owner => deck.Materialize(owner, pool),
			owner => inert.Materialize(owner, pool)
		);

		var runner = new GameRunner(
			new MultiTurnBeamSearchAiStrategy(ids, 2, rng: new Random(seed + 1)),
			new InertSeat()
		);

		var (result, _) = runner.Run(state, ids, cardNames, seed + 2, seed + 3);

		var turn = 1;
		var activations = 0;
		var copierArrived = 0;
		var untapperArrived = 0;
		var log = new List<string>();

		foreach (var e in result.AllEvents)
		{
			if (e is TurnStartedEvent)
			{
				turn++;
				continue;
			}

			var id = IdOf(e);
			if (id == 0 || !cardNames.TryGetValue(id, out var name))
				continue;

			if (e is AbilityActivatedEvent && Copiers.Contains(name))
			{
				activations++;
				log.Add($"    t{turn / 2}: ACTIVATE {name}");
			}
			else if (e is CreatureEnteredBattlefieldEvent)
			{
				if (Copiers.Contains(name) && copierArrived == 0)
				{
					copierArrived = turn / 2;
					log.Add($"    t{turn / 2}: copier {name} entered");
				}
				else if (Untappers.Contains(name) && untapperArrived == 0)
				{
					untapperArrived = turn / 2;
					log.Add($"    t{turn / 2}: untapper {name} entered");
				}
			}
		}

		TestContext.Out.WriteLine(
			$"\n--- seed {seed}: {result.EndReason}, {result.TurnCount} turns, "
				+ $"P1 win={result.IsPlayer1Win}\n"
				+ $"    first copier t{copierArrived}, first untapper t{untapperArrived}, "
				+ $"activations={activations}"
		);
		if (result.ExceptionMessage is { Length: > 0 })
			TestContext.Out.WriteLine(
				$"    THREW: {result.ExceptionMessage}\n"
					+ $"    {(result.ExceptionStackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()}"
			);
		foreach (var line in log.Take(14))
			TestContext.Out.WriteLine(line);
	}

	private static int IdOf(GameEvent e) =>
		e.GetType().GetProperty("CardId")?.GetValue(e) as int?
		?? e.GetType().GetProperty("CreatureId")?.GetValue(e) as int?
		?? 0;

	private sealed class InertSeat : IAiStrategy
	{
		// Taken FROM the generator, never constructed by hand: a bare `new EndTurnAction()` is
		// missing the ids the action needs and throws "key '0' was not present in the dictionary"
		// on the first turn of every game. Mirrors Goldfish.InertStrategy exactly.
		public GameAction SelectAction(GameState state, MtgGameIds ids, int playerId)
		{
			var legal = MtgActionGenerator.GetLegalActions(state, ids, playerId);
			return legal.FirstOrDefault(a => a is EndTurnAction) ?? legal[0];
		}

		public ImmutableList<int> ResolveChoice(
			GameState state,
			ChoiceAction choice,
			int playerId
		) =>
			choice
				.GetOptions(state, null)
				.Where(o => o.IsEnabled)
				.Take(Math.Max(0, choice.MinChoices))
				.Select(o => o.Id)
				.ToImmutableList();
	}
}
