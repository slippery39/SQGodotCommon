using DoomCore;
using ImmutableGameObjects;

namespace DoomCore.Tests;

/// <summary>
/// A run is all three acts, in a fixed order. These assert the CONSEQUENCES of that — the theme
/// changing under you, life coming back at the break, gold surviving it — rather than the shape of
/// the table that describes it.
/// </summary>
public class ActChainTests
{
	[Test]
	public void ARunWalksEveryActInOrder()
	{
		Assert.That(
			ActMap.Order,
			Has.Length.GreaterThan(1),
			"a chained run needs more than one act"
		);
		Assert.That(
			ActMap.Order.Distinct().Count(),
			Is.EqualTo(ActMap.Order.Length),
			"an act repeated in the order would replay its whole doom schedule"
		);

		foreach (var (theme, index) in ActMap.Order.Select((t, i) => (t, i)))
		{
			var firstFloor = index * ActMap.ActLength + 1;
			var lastFloor = (index + 1) * ActMap.ActLength;

			Assert.That(ActMap.ThemeFor(firstFloor), Is.EqualTo(theme));
			Assert.That(ActMap.ThemeFor(lastFloor), Is.EqualTo(theme));
			Assert.That(ActMap.FloorInAct(firstFloor), Is.EqualTo(1));
			Assert.That(ActMap.FloorInAct(lastFloor), Is.EqualTo(ActMap.ActLength));
		}
	}

	/// <summary>
	/// **Each act runs its OWN schedule.** The apocalypse on act 2's first floor must be act 2's
	/// opener, not a continuation of act 1 — if the schedule read the run-wide floor, every act
	/// after the first would sit permanently on its final band.
	/// </summary>
	[Test]
	public void EachActStartsItsOwnDoomSchedule()
	{
		foreach (var (theme, index) in ActMap.Order.Select((t, i) => (t, i)))
		{
			var firstFloor = index * ActMap.ActLength + 1;
			var bossFloor = (index + 1) * ActMap.ActLength;
			var definition = ThemeLibrary.Of(theme);

			Assert.That(
				StarterContent.ScenarioFor(theme, firstFloor),
				Is.EqualTo(definition.Bands[0]),
				$"act {index + 1} should open on its own first band"
			);
			Assert.That(
				StarterContent.ScenarioFor(theme, bossFloor),
				Is.EqualTo(definition.FinalDoom),
				$"act {index + 1} should end on its own final doom"
			);
		}
	}

	/// <summary>
	/// **Later acts are harder, and they have to be**: content is chosen by the floor's position
	/// within its act, so without scaling act 2 floor 1 would field act 1 floor 1 and the run would
	/// get easier every time you cleared an act.
	/// </summary>
	[Test]
	public void LaterActsFieldTougherVersionsOfTheSameRoster()
	{
		var act1 = StarterContent.OpponentHealthFor(1);
		var act2 = StarterContent.OpponentHealthFor(ActMap.ActLength + 1);
		var act3 = StarterContent.OpponentHealthFor(ActMap.ActLength * 2 + 1);

		Assert.That(act2, Is.GreaterThan(act1), "act 2 must not be a repeat of act 1");
		Assert.That(act3, Is.GreaterThan(act2), "and act 3 harder again");
	}

	/// <summary>
	/// Clearing the boss of a non-final act heals you and walks you into the next one.
	///
	/// **Partial, not full** — a full restore made the run three independent acts, because nothing
	/// spent in act 1 could cost you in act 2. The amount is READ from `ActBreakHealFor` rather than
	/// restated, so retuning the curve does not break a test about the rule.
	/// </summary>
	[Test]
	public void AnActBreakRestoresLifeAndGoldSurvivesIt()
	{
		var bossFloor = ActMap.ActLength;
		Assert.That(ActMap.IsActBreak(bossFloor), Is.True, "sanity: that is an act break");

		var run = new Run
		{
			Floor = bossFloor,
			Life = 12,
			MaxLife = 120,
			Gold = 55,
		}.WithCards([Unit()]);

		var before = run.Life;
		var after = Clear(run);
		var heal = StarterContent.ActBreakHealFor(run.MaxLife);

		Assert.That(after.Floor, Is.EqualTo(bossFloor + 1));
		Assert.That(after.Life, Is.GreaterThan(before), "the break heals you");
		Assert.That(
			after.Life,
			Is.LessThanOrEqualTo(before + heal),
			"but not by more than the act break is authored to give"
		);
		Assert.That(
			heal,
			Is.LessThan(run.MaxLife),
			"a FULL restore would make each act independent of the last — damage must carry a debt "
				+ "forward or a long run is not one run"
		);
		Assert.That(after.IsOver, Is.False, "and the run carries on into the next act");
		Assert.That(
			after.Gold,
			Is.GreaterThanOrEqualTo(55),
			"gold survives the break — banking for the next act's shop is a decision"
		);
	}

	/// <summary>An ordinary floor does NOT heal you. Only the act break does.</summary>
	[Test]
	public void AnOrdinaryFloorDoesNotHeal()
	{
		var run = new Run
		{
			Floor = 1,
			Life = 12,
			MaxLife = 120,
		}.WithCards([Unit()]);

		Assert.That(ActMap.IsActBreak(1), Is.False, "sanity");
		Assert.That(Clear(run).Life, Is.LessThan(120));
	}

	/// <summary>Clearing a battle pays gold. Nothing else in the game produces any.</summary>
	[Test]
	public void ClearingABattlePaysGold()
	{
		var run = new Run
		{
			Floor = 1,
			Life = 100,
			MaxLife = 120,
		}.WithCards([Unit()]);

		Assert.That(Clear(run).Gold, Is.GreaterThan(0), "a cleared floor paid nothing");
	}

	private static RunCard Unit() =>
		new()
		{
			Name = "Survivor",
			Cost = 0,
			IsUnit = true,
			Power = 400,
			Toughness = 400,
		};

	/// <summary>Plays the floor's battle out to a win and hands back the run that follows.</summary>
	private static Run Clear(Run run)
	{
		var (state, _) = run.StartBattle(DoomScenario.Flood, countdown: 99, [], opponentHealth: 20);

		for (var turn = 0; turn < 40 && !state.GetBattle().IsOver; turn++)
		{
			foreach (var card in state.CardsIn(ZoneType.Hand).ToList())
			{
				var (next, ok) = state.TryAddAction(
					new PlayCardAction { CardId = card.Id, Lane = 0 }
				);
				if (ok)
				{
					(state, _) = next.ProcessAllActions();
					break;
				}
			}

			(state, _) = state.AddAction(new EndTurnAction()).ProcessAllActions();
		}

		Assert.That(
			state.GetBattle().OpponentDefeated,
			Is.True,
			"the helper failed to win the battle, so the assertion below means nothing"
		);
		return run.AfterBattle(state);
	}
}
