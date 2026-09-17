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

	/// <summary>What Ashfall is authored to do to one target, read off the library entry.</summary>
	private static int Amount(DoomTarget target) =>
		(
			(DealDamageAction)
				ScenarioLibrary.Ashfall.BattleEffects.Single(e => e.Target == target).Template
		).Amount;

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

		// Both amounts come from the library entry itself. The test is that a scenario added as
		// DATA ALONE fires and hits both targets — not what this month's numbers happen to be.
		var burn = Amount(DoomTarget.Player);
		var chip = Amount(DoomTarget.YourUnits);

		Assert.That(state.GetBattle().DoomsFired, Is.EqualTo(1), "it should have landed");
		Assert.That(burn, Is.Not.Zero, "Ashfall is authored to do nothing to you");
		Assert.That(
			state.GetPlayer().Life,
			Is.EqualTo(40 - burn),
			"straight from the library entry"
		);
		// **Read off the CARD, not off the lane.** Combat v3 withdraws units at the end of the
		// turn, so by the time we look the unit is in Discard — but the damage the apocalypse
		// marked on it is still on the component, and that is what says Ashfall chipped rather
		// than swept. (It clears when the card is next played; see `PlayCardAction`.)
		Assert.That(
			((DoomCard)state.GetObject(unitId)).Unit().Damage,
			Is.EqualTo(chip),
			"and the unit is chipped, not swept: a different question from Flood"
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

	/// <summary>
	/// Scope is still the curve, but a THEME owns it now rather than a floor gate.
	///
	/// This used to assert that floor 1 could only roll battle-scope dooms, back when the doom was
	/// drawn at random from everything legal. The schedule replaced the roll, and a theme may open
	/// on a gentle permanent doom — The Rising opens on the dead coming back, which only ever ADDS
	/// to your deck.
	///
	/// **Scope is not severity**, which is why nothing here asserts an escalating scope. An earlier
	/// version of this test did, and it failed The Rising for putting a battle-scope doom after a
	/// permanent one. The content was right and the rule was invented. What has to hold is only
	/// this: the band before the boss rewrites the deck, so the run walks into the last fight
	/// carrying what the act did to it.
	/// </summary>
	[Test]
	public void EveryThemeRewritesTheDeckInTheBandBeforeTheBoss()
	{
		foreach (var theme in ThemeLibrary.All)
			Assert.That(
				StarterContent.ScopeOf(theme.Bands[^1]),
				Is.EqualTo(DoomScope.Permanent),
				$"{theme.Name}'s last band before the boss is battle scope, so nothing it does "
					+ "carries into the fight the run was built for"
			);
	}
}
