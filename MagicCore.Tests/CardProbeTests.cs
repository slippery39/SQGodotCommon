using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Sets;

namespace MagicCore.Tests;

/// <summary>
/// The fuzz probe for a batch of cards — explicit, so the normal suite never runs it:
///
///   MAGIC_PROBE_CARDS="Gardenize;Omnipresence" dotnet test MagicCore.Tests --filter "FullyQualifiedName~CardProbeTests"
///
/// Decks of the named cards (3 of each) plus random scripted cards and basics play random games
/// that ALSO pick "yes" and up to MaxChoices — the paths a Take(MinChoices) player never takes.
/// Crashes fail the run. Then a report: for each named card, how often it was cast, activated, and
/// how often an ability of it went on the stack — "passing only proves games end" (HANDOFF), so a
/// card that never fired is listed as such. MAGIC_PROBE_GAMES sets the game count (default 150).
/// </summary>
[Explicit("A probe: run it by name, with MAGIC_PROBE_CARDS set")]
public class CardProbeTests
{
	static IReadOnlyList<string> Batch =>
		(Environment.GetEnvironmentVariable("MAGIC_PROBE_CARDS") ?? "").Split(
			';',
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
		);

	static List<CardDefinition> Deck(IReadOnlyList<string> batch, int seed)
	{
		var rng = new Random(seed);
		var extras = CardRegistry
			.Scripted.Select(CardRegistry.Get)
			.Where(RandomGameTests.Dealt)
			.OrderBy(_ => rng.Next())
			.Take(10);
		return
		[
			.. batch.SelectMany(n => Enumerable.Repeat(CardRegistry.Get(n), 3)),
			.. extras,
			.. new[] { "Plains", "Island", "Swamp", "Mountain", "Forest" }.SelectMany(l =>
				Enumerable.Repeat(TestGames.Card(l), 5)
			),
		];
	}

	/// <summary>
	/// A random game nobody wins can grow without end — Troubling Tribbles doubled every upkeep, 936 tokens
	/// by turn 26 (batch 6b; it is dealt no more, MC-4.5), and each step then scans them all. Past 300 permanents it counts as unfinished.
	/// </summary>
	static bool Overrun(GameState s)
	{
		var first = s.GetGame().StartingPlayerId;
		return s.CardsIn(first, ZoneType.Battlefield).Count()
				+ s.CardsIn(s.OpponentOf(first), ZoneType.Battlefield).Count()
			> 300;
	}

	[Test]
	public void Probe()
	{
		var batch = Batch;
		Assume.That(
			batch,
			Is.Not.Empty,
			"set MAGIC_PROBE_CARDS to a ;-separated list of card names"
		);
		var games = int.TryParse(Environment.GetEnvironmentVariable("MAGIC_PROBE_GAMES"), out var g)
			? g
			: 150;
		// a transformed card is named for its back face: counted under the card's name (Reflection of Kiki-Jiki)
		static string NameOf(global::MagicCore.Cards.Card c) =>
			c.GetComponent<global::MagicCore.Cards.Transformed>()?.Original.Name ?? c.Name;
		var casts = batch.ToDictionary(n => n, _ => 0);
		var activations = batch.ToDictionary(n => n, _ => 0);
		var onStack = batch.ToDictionary(n => n, _ => new HashSet<int>());
		var unfinished = 0;
		var overrun = 0;
		var overruns = new List<string>(); // which game, and what filled the battlefield — open it before blaming random play
		for (var seed = 1; seed <= games; seed++)
		{
			var rng = new Random(seed);
			// a sideboard too, so a card that reaches outside the game (Extrapolate) has something there
			var s = MagicRules.NewGame(
				Deck(batch, seed),
				Deck(batch, seed + 10_000),
				seed,
				sideboard1: Deck(batch, seed + 20_000).Take(5),
				sideboard2: Deck(batch, seed + 30_000).Take(5)
			);
			for (var i = 0; i < 20_000 && !s.IsOver() && !Overrun(s); i++)
			{
				if (s.GetPendingChoice() is { } choice)
				{
					var options = choice.Options.Where(o => o.IsEnabled).ToList();
					var n = rng.Next(
						choice.MinChoices,
						Math.Min(choice.MaxChoices, options.Count) + 1
					);
					s = s.Choose(options.OrderBy(_ => rng.Next()).Take(n).Select(o => o.Id));
					continue;
				}
				var pool = s.LegalActions()
					.Where(a => a is not MulliganAction || rng.Next(10) == 0)
					.ToList();
				var act = pool[rng.Next(pool.Count)];
				var name = act switch
				{
					CastSpellAction c => NameOf(s.GetCard(c.CardId)),
					ActivateAbilityAction a => NameOf(s.GetCard(a.CardId)),
					ActivateLoyaltyAction l => NameOf(s.GetCard(l.CardId)),
					_ => null,
				};
				if (name is not null && casts.ContainsKey(name))
				{
					if (act is CastSpellAction)
						casts[name]++;
					else
						activations[name]++;
				}
				s = s.Act(act);
				foreach (
					var ability in s.GetChildren(s.GetWellKnownId(MagicKeys.Stack))
						.OfType<StackAbility>()
				)
					if (onStack.TryGetValue(NameOf(ability.Source), out var seen))
						seen.Add(ability.Id);
			}
			unfinished += s.IsOver() ? 0 : 1;
			if (!Overrun(s))
				continue;
			overrun++;
			var first = s.GetGame().StartingPlayerId;
			overruns.Add(
				$"seed {seed}, turn {s.GetGame().TurnNumber}: "
					+ string.Join(
						", ",
						s.CardsIn(first, ZoneType.Battlefield)
							.Concat(s.CardsIn(s.OpponentOf(first), ZoneType.Battlefield))
							.GroupBy(c => c.Name)
							.OrderByDescending(n => n.Count())
							.Take(6)
							.Select(n => $"{n.Count()} {n.Key}")
					)
			);
		}
		var lines = batch.Select(n =>
			$"{n}: cast {casts[n]}, activated {activations[n]}, abilities on the stack {onStack[n].Count}"
			+ (casts[n] + activations[n] + onStack[n].Count == 0 ? "   <-- NEVER USED" : "")
		);
		TestContext.Progress.WriteLine(
			$"{games} games, {unfinished} unfinished ({overrun} past 300 permanents)\n"
				+ string.Join("\n", lines.Concat(overruns))
		);
		Assert.That(
			unfinished,
			Is.LessThanOrEqualTo(games / 10),
			"most random games should finish"
		);
	}
}
