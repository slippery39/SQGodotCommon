using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **THE TOWN — every rule FIRES** (`KinMapPlan.md` §3, §7): its buildings, and the hospital that
/// now sells healing. Uses the run fixtures in `PartyRunTests.cs`.
/// </summary>
public partial class PartyRunTests
{
	private static BuildingKind[] Buildings(PartyRun run) =>
		[.. run.Town.Buildings.Select(b => b.Kind)];

	[Test]
	public void EveryTownHasItsBuildingsAndNoLeadersHall()
	{
		foreach (var region in new[] { 0, 1 })
			Assert.That(
				Buildings(Run() with { RegionIndex = region }),
				Is.EquivalentTo(
					new[] { BuildingKind.Hospital, BuildingKind.Shop, BuildingKind.Gate }
				)
			);
	}

	[Test]
	public void EveryBuildingIsPlacedOnTheMap()
	{
		var town = (Run() with { RegionIndex = 1 }).Town;

		Assert.That(
			town.Buildings,
			Has.All.Matches<Building>(b => b.X is > 0 and < 1 && b.Y is > 0 and < 1)
		);
	}

	[Test]
	public void TheHospitalHealsTheTeamForGold()
	{
		var run = WithTeam(Run(), A, B) with
		{
			Team = [new RunCompanion(A, 5), new RunCompanion(B, 1)],
		};

		var after = run.HealAtHospital();

		Assert.That(Hp(after, "A"), Is.EqualTo(40));
		Assert.That(Hp(after, "B"), Is.EqualTo(40));
		Assert.That(after.Gold, Is.EqualTo(run.Gold - PartyRun.HospitalPrice));
	}

	[Test]
	public void TheHospitalRefusesTheWellAndThePoor()
	{
		var well = Run();
		Assert.That(well.CannotHeal, Is.EqualTo("Everyone is already well"));
		Assert.That(well.HealAtHospital(), Is.EqualTo(well), "nothing is charged");

		var poor = Run() with
		{
			Team = [new RunCompanion(A, 5)],
			Gold = PartyRun.HospitalPrice - 1,
		};
		Assert.That(poor.CannotHeal, Is.Not.Null);
		Assert.That(poor.HealAtHospital(), Is.EqualTo(poor));
	}

	[Test]
	public void TheHospitalHealsOnlyInTown()
	{
		var run = OnRoute(Run() with { Team = [new RunCompanion(A, 5)] });

		Assert.That(run.CannotHeal, Is.EqualTo("Not in a town"));
	}
}
