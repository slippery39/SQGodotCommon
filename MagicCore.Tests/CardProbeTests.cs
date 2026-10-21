using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Sets.Fra;

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
		var extras = FraCards
			.Scripted.Select(FraCards.Get)
			.Where(c => !c.Data.TypeLine.Contains("Land"))
			.OrderBy(_ => rng.Next())
			.Take(10);
		return
		[
			.. batch.SelectMany(n => Enumerable.Repeat(FraCards.Get(n), 3)),
			.. extras,
			.. new[] { "Plains", "Island", "Swamp", "Mountain", "Forest" }.SelectMany(l =>
				Enumerable.Repeat(TestGames.Card(l), 5)
			),
		];
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
		var casts = batch.ToDictionary(n => n, _ => 0);
		var activations = batch.ToDictionary(n => n, _ => 0);
		var onStack = batch.ToDictionary(n => n, _ => new HashSet<int>());
		var unfinished = 0;
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
			for (var i = 0; i < 20_000 && !s.IsOver(); i++)
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
					CastSpellAction c => s.GetCard(c.CardId).Name,
					ActivateAbilityAction a => s.GetCard(a.CardId).Name,
					ActivateLoyaltyAction l => s.GetCard(l.CardId).Name,
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
					if (onStack.TryGetValue(ability.Source.Name, out var seen))
						seen.Add(ability.Id);
			}
			unfinished += s.IsOver() ? 0 : 1;
		}
		var lines = batch.Select(n =>
			$"{n}: cast {casts[n]}, activated {activations[n]}, abilities on the stack {onStack[n].Count}"
			+ (casts[n] + activations[n] + onStack[n].Count == 0 ? "   <-- NEVER USED" : "")
		);
		TestContext.Progress.WriteLine(
			$"{games} games, {unfinished} unfinished\n" + string.Join("\n", lines)
		);
		Assert.That(
			unfinished,
			Is.LessThanOrEqualTo(games / 10),
			"most random games should finish"
		);
	}
}
