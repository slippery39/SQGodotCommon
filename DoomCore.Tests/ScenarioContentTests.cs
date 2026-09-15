using DoomCore;
using ImmutableGameObjects;

namespace DoomCore.Tests;

/// <summary>
/// Apocalypses as content. The claim being tested is that a BATTLE-scope scenario is now data
/// alone — so Ashfall, which has no case in any hook, must fire correctly anyway.
/// </summary>
public class ScenarioContentTests
{
	private static (GameState, int) AddUnit(GameState state, int toughness, int lane)
	{
		var card = new DoomCard
		{
			Name = "Stray",
			Cost = 0,
			RunCardId = 0,
		};
		card = (DoomCard)
			card.WithComponent(
				new UnitComponent
				{
					Power = 1,
					Toughness = toughness,
					Lane = lane,
				}
			);

		var (s, added) = state.AddObject(card, state.ZoneId(ZoneType.Hand));
		return (s, added.Id);
	}

	/// <summary>
	/// The whole slice in one test. Ashfall exists only in ScenarioLibrary — no hook case, no scope
	/// row, no countdown row — and it has to do exactly what it says when the clock runs out.
	/// </summary>
	[Test]
	public void AnApocalypseAddedAsDataAloneStillFires()
	{
		var state = DoomBattleFactory.Create(
			DoomScenario.Ashfall,
			countdown: 1,
			life: 40,
			opponentHealth: 500
		);
		(state, var unitId) = AddUnit(state, toughness: 6, lane: 0);

		(state, _) = state.BeginBattle();
		(state, _) = state
			.AddAction(new PlayCardAction { CardId = unitId, Lane = 0 })
			.ProcessAllActions();

		(state, _) = state.AddAction(new EndTurnAction()).ProcessAllActions();

		Assert.That(state.GetBattle().DoomsFired, Is.EqualTo(1), "it should have landed");
		Assert.That(
			state.GetPlayer().Life,
			Is.EqualTo(37),
			"3 to you, straight from the library entry"
		);
		Assert.That(
			state.UnitInLane(0)!.Unit().Damage,
			Is.EqualTo(2),
			"and 2 to the unit — chipped, not swept: a different question from Flood"
		);
	}

	/// <summary>
	/// Flood still sweeps, now that it runs as data rather than as a hook case.
	///
	/// Built through a RUN rather than the battle factory, because the companion is added by
	/// `Run.StartBattle` — the factory alone produces a board with no companion on it, and the first
	/// version of this test "failed" for exactly that reason.
	/// </summary>
	[Test]
	public void FloodStillWashesTheBoardAndSparesTheCompanion()
	{
		var run = new Run
		{
			Life = 40,
			MaxLife = 40,
			Companion = StarterContent.StarterCompanion,
		}.WithCards(
			[
				new RunCard
				{
					Name = "Stray",
					Cost = 0,
					IsUnit = true,
					Power = 1,
					Toughness = 4,
				},
			]
		);

		var (state, _) = run.StartBattle(DoomScenario.Flood, countdown: 1, [], opponentHealth: 500);

		var stray = state.CardsIn(ZoneType.Hand).First(c => c.Name == "Stray");
		(state, _) = state
			.AddAction(new PlayCardAction { CardId = stray.Id, Lane = 0 })
			.ProcessAllActions();

		Assert.That(state.UnitInLane(0), Is.Not.Null, "it should be standing before the flood");

		(state, _) = state.AddAction(new EndTurnAction()).ProcessAllActions();

		Assert.That(state.UnitInLane(0), Is.Null, "the unit should have been washed away");
		Assert.That(
			state.Units().Any(u => u.HasComponent<CompanionComponent>()),
			Is.True,
			"and the companion rides it out, as it rides out everything"
		);
	}

	/// <summary>
	/// A battle scenario declaring no effects would fire and change nothing, which is exactly the
	/// silent no-op this codebase keeps rediscovering. It must be loud instead.
	/// </summary>
	[Test]
	public void ABattleScenarioWithNoEffectsIsRefusedRatherThanIgnored()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 9, opponentHealth: 30);

		Assert.Throws<InvalidOperationException>(
			() => DoomBattleEffects.Apply(state, DoomScenario.Zombie),
			"a permanent scenario in the battle hook must throw, not no-op"
		);
	}

	[Test]
	public void AnUnimplementedScenarioIsNeverOffered()
	{
		foreach (var floor in new[] { 1, 5, 12, 20 })
			Assert.That(
				StarterContent.PlayableOn(floor),
				Does.Not.Contain(DoomScenario.Rapture),
				"Rapture has no transform; offering it would throw mid-run"
			);
	}

	[Test]
	public void ScopeIsTheDifficultyCurve()
	{
		Assert.That(
			StarterContent.PlayableOn(1).All(s => StarterContent.ScopeOf(s) == DoomScope.Battle),
			Is.True,
			"floor 1 may only roll apocalypses that leave no marks on the run"
		);

		Assert.That(
			StarterContent.PlayableOn(5).Any(s => StarterContent.ScopeOf(s) == DoomScope.Permanent),
			Is.True,
			"and later floors must be able to rewrite the deck, or the power curve never starts"
		);
	}
}
