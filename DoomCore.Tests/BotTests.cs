using DoomCore;

namespace DoomCore.Tests;

/// <summary>
/// The bot is a MEASURING INSTRUMENT, so what needs guarding is that it actually plays — a bot that
/// silently passes every turn produces a full results file and a completely wrong difficulty curve,
/// which is the same silent no-op this codebase has lost four bugs to.
///
/// Nothing here asserts a balance number. Those live in `doom_sim_results/` and change with the
/// content; a test that pinned one would fail on every card tweak and teach nobody anything.
/// </summary>
public class BotTests
{
	[Test]
	public void BotPlaysCardsRatherThanPassing()
	{
		var run = StarterContent.NewRun(seed: 7);
		var (state, _) = run.StartBattle(
			DoomScenario.Flood,
			StarterContent.CountdownFor(DoomScenario.Flood),
			StarterContent.EnemiesFor(1, 7),
			opponent: StarterContent.OpponentFor(1, 7)
		);

		var played = DoomBot.PlayTurn(state, run);

		// The companion is already on the field, so "the bot played something" is strictly more
		// than one unit standing.
		Assert.That(
			played.Units().Count(),
			Is.GreaterThan(1),
			"the bot ended its first turn without playing a card"
		);
		Assert.That(played.GetPlayer().Energy, Is.LessThan(state.GetPlayer().Energy));
	}

	[Test]
	public void BotClearsTheFirstFloor()
	{
		var result = RunSimulator.Play(seed: 7);

		Assert.That(result.Floors, Is.Not.Empty);
		Assert.That(
			result.Floors[0].Outcome,
			Is.EqualTo("Cleared"),
			"floor 1 is unwinnable for the bot — either the content moved a long way or the bot broke"
		);
		Assert.That(result.Floors[0].Turns, Is.GreaterThan(0));
	}

	/// <summary>
	/// A rest floor must be a REST for everyone. The simulator and `DoomBoard` both ask
	/// `FloorKindFor`, and every measured number assumes they agree — if the front end fought on a
	/// floor the sim rested on, the balance tables would describe a game nobody plays.
	/// </summary>
	[Test]
	public void RestFloorsHealAndAreNeverFought()
	{
		var rests = Enumerable
			.Range(1, Run.ActLength)
			.Where(f => StarterContent.FloorKindFor(f) == FloorKind.Rest)
			.ToList();

		Assert.That(rests, Is.Not.Empty, "an act with no rests is 20 unhealed battles");
		Assert.That(
			StarterContent.FloorKindFor(Run.ActLength),
			Is.EqualTo(FloorKind.Battle),
			"the act must end on the thing you came for"
		);

		var hurt = StarterContent.NewRun(seed: 3) with { Life = 10, Floor = rests[0] };
		var rested = hurt.Rest(StarterContent.RestHealFor(hurt.MaxLife));

		Assert.That(rested.Life, Is.GreaterThan(hurt.Life), "a rest that heals nothing is a no-op");
		Assert.That(rested.Floor, Is.EqualTo(hurt.Floor + 1), "and it still costs a floor");

		var full = StarterContent.NewRun(seed: 3);
		Assert.That(
			full.Rest(StarterContent.RestHealFor(full.MaxLife)).Life,
			Is.EqualTo(full.MaxLife),
			"healing past max"
		);

		// No run may record a battle on a rest floor.
		foreach (var result in RunSimulator.PlayMany(3))
			Assert.That(
				result.Floors.Select(f => f.Floor).Intersect(rests),
				Is.Empty,
				$"seed {result.Seed} fought on a rest floor"
			);
	}

	[Test]
	public void EverySimulatedRunEnds()
	{
		foreach (var result in RunSimulator.PlayMany(5))
		{
			Assert.That(result.EndReason, Is.Not.Empty);
			Assert.That(result.FloorReached, Is.InRange(1, Run.RunLength));

			// **Was "one reward taken per floor cleared", and that stopped being true when the bot
			// learned to decline.** Skipping is a legal move — the Godot reward screen has always
			// offered it — so the count is now bounded above by the clears rather than equal to
			// them. What still has to hold: it cannot take MORE than it cleared, and a run of any
			// length must take something, or the reward path is not running at all.
			var cleared = result.Floors.Count(f => f.Outcome == "Cleared");
			Assert.That(
				result.TakenRewards.Count,
				Is.InRange(0, cleared),
				$"seed {result.Seed} took more rewards than it cleared floors"
			);

			if (cleared >= 5)
				Assert.That(
					result.TakenRewards,
					Is.Not.Empty,
					$"seed {result.Seed} cleared {cleared} floors and was never offered a reward"
				);
		}
	}
}
