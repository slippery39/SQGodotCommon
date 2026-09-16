using DoomCore;

namespace DoomCore.Tests;

/// <summary>
/// The doom SCHEDULE. A theme decides which apocalypse waits on which floor, and these are the
/// rules a themed set has to obey to be playable at all.
/// </summary>
public class ThemeTests
{
	/// <summary>
	/// **The final doom must be BATTLE scope.** `Run.AfterBattle` applies a permanent transform and
	/// then the act ends, so a permanent doom on the last floor rewrites a deck nobody will ever
	/// draw again — a no-op with an animation, and indistinguishable from one that worked.
	/// </summary>
	[Test]
	public void EveryThemeEndsOnABattleScopeDoom()
	{
		foreach (var theme in ThemeLibrary.All)
			Assert.That(
				StarterContent.ScopeOf(theme.FinalDoom),
				Is.EqualTo(DoomScope.Battle),
				$"{theme.Name}'s final doom is permanent, so it would rewrite a deck the run never "
					+ "draws again"
			);
	}

	[Test]
	public void EveryScheduledDoomIsImplementedAndReal()
	{
		foreach (var theme in ThemeLibrary.All)
		{
			Assert.That(theme.Bands, Is.Not.Empty, $"{theme.Name} schedules no dooms at all");

			foreach (var scenario in theme.Bands.Append(theme.FinalDoom))
			{
				Assert.That(
					scenario,
					Is.Not.EqualTo(DoomScenario.None),
					$"{theme.Name} schedules None, which fires and does nothing"
				);
				Assert.That(
					ScenarioLibrary.Of(scenario).Implemented,
					Is.True,
					$"{theme.Name} schedules {scenario}, which is flagged unimplemented"
				);
			}
		}
	}

	/// <summary>
	/// Bands are six floors each: 1-6, 7-12, 13-18, and the boss floor on its own. Floor 19 is a
	/// rest and holds no battle, so it is not asserted here.
	/// </summary>
	[Test]
	public void TheScheduleSplitsTheActIntoSixFloorBands()
	{
		var theme = ThemeLibrary.LongEmergency;

		foreach (var floor in Enumerable.Range(1, 6))
			Assert.That(
				StarterContent.ScenarioFor(theme.Theme, floor),
				Is.EqualTo(theme.Bands[0]),
				$"floor {floor} should be in the first band"
			);

		foreach (var floor in Enumerable.Range(7, 6))
			Assert.That(StarterContent.ScenarioFor(theme.Theme, floor), Is.EqualTo(theme.Bands[1]));

		foreach (var floor in Enumerable.Range(13, 6))
			Assert.That(StarterContent.ScenarioFor(theme.Theme, floor), Is.EqualTo(theme.Bands[2]));

		Assert.That(
			StarterContent.ScenarioFor(theme.Theme, Run.ActLength),
			Is.EqualTo(theme.FinalDoom),
			"the boss floor gets the final doom and nothing else does"
		);
	}

	/// <summary>
	/// The schedule is the same every run of a theme. That is the point — it is a story, not a
	/// shuffle — and it is what lets the whole act be shown at run start.
	/// </summary>
	[Test]
	public void TheScheduleDoesNotVaryWithTheSeed()
	{
		foreach (var floor in Enumerable.Range(1, Run.ActLength))
		{
			var scenario = StarterContent.ScenarioFor(DoomTheme.LongEmergency, floor);

			Assert.That(
				StarterContent.ScenarioFor(DoomTheme.LongEmergency, floor),
				Is.EqualTo(scenario),
				$"floor {floor} gave two different answers"
			);
		}
	}

	/// <summary>
	/// Whatever a theme schedules must be legal where it lands. `MinFloor` no longer SELECTS
	/// anything, but it still records where a doom was designed to be seen, and a schedule that
	/// ignores it is putting an apocalypse somewhere it was never balanced for.
	/// </summary>
	[Test]
	public void NoThemeSchedulesADoomBelowItsMinFloor()
	{
		foreach (var theme in ThemeLibrary.All)
		{
			foreach (var floor in Enumerable.Range(1, Run.ActLength))
			{
				if (StarterContent.FloorKindFor(floor) == FloorKind.Rest)
					continue;

				var scenario = StarterContent.ScenarioFor(theme.Theme, floor);

				Assert.That(
					ScenarioLibrary.Of(scenario).MinFloor,
					Is.LessThanOrEqualTo(floor),
					$"{theme.Name} puts {scenario} on floor {floor}, below where it was designed for"
				);
			}
		}
	}
}
