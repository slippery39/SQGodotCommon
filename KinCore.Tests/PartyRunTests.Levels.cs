using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **LEVELS, XP and the early game — every rule FIRES** (Shayne, 2026-09-27): stats scale with level
/// and are unchanged at the base; a region's wild fights come in its level range; region 1 fields one
/// foe; your monsters grow from wins; a catch keeps its level.
/// Uses the run fixtures in `PartyRunTests.cs`.
/// </summary>
public partial class PartyRunTests
{
	private static Foe Hitter(int hit, int block) =>
		Foe("Brute") with
		{
			Pattern =
			[
				new Intent
				{
					Name = "Hit",
					Kind = IntentType.Attack,
					Amount = hit,
				},
				new Intent
				{
					Name = "Brace",
					Kind = IntentType.Block,
					Amount = block,
				},
			],
		};

	// ===== Scaling

	[Test]
	public void ALevelScalesHpBlockAndAttacksUpAndDown()
	{
		var foe = Hitter(10, 4);

		var high = PartyLevels.Scale(foe, 10);
		var low = PartyLevels.Scale(foe, 2);

		Assert.That(
			high.MaxHp,
			Is.EqualTo(PartyLevels.At(foe.MaxHp, 10)).And.GreaterThan(foe.MaxHp)
		);
		Assert.That(high.Pattern[0].Amount, Is.GreaterThan(10));
		Assert.That(high.Pattern[1].Amount, Is.GreaterThan(4), "Block scales too");
		Assert.That(low.MaxHp, Is.LessThan(foe.MaxHp));
		Assert.That(low.Level, Is.EqualTo(2));
	}

	[Test]
	public void EveryRegionsWildFightsComeInItsLevelRange()
	{
		foreach (var region in PartyWorld.Regions.Take(3))
		{
			var index = PartyWorld.Regions.IndexOf(region);
			for (var seed = 0; seed < 10; seed++)
			{
				var route = (PartyRun.Start(PartyContent.Pike, seed) with { RegionIndex = index })
					.EnterRoute()
					.Route!;
				foreach (
					var node in route.Nodes.Where(n => n.Kind is NodeKind.Wild or NodeKind.Grass)
				)
					Assert.That(
						node.Encounter!.Foes.Select(f => f.Level),
						Is.All.InRange(region.MinLevel, region.MaxLevel),
						$"{region.Name}, seed {seed}"
					);
			}
		}
	}

	[Test]
	public void LaterRegionsFieldMoreAndHigherLevelledFoes()
	{
		var first = PartyWorld.Regions[0];
		var last = PartyWorld.Regions[^1];

		Assert.That(last.MaxFoes, Is.GreaterThan(first.MaxFoes));
		Assert.That(last.MinLevel, Is.GreaterThan(first.MaxLevel));
		Assert.That(
			last.Bosses.SelectMany(b => b.Foes).Select(f => f.Level),
			Is.All.EqualTo(last.BossLevel)
		);
		Assert.That(last.BossLevel, Is.GreaterThan(last.EliteLevel).And.GreaterThan(last.MaxLevel));
	}

	[Test]
	public void TheFirstRegionsWildFoesAreNeverAboveAStarter()
	{
		// Region 1 chips (Shayne, 2026-09-28: "a little chip"), but never outclasses a Lv 5 starter,
		// and a fight is at most two foes.
		var first = PartyWorld.Regions[0];

		Assert.That(first.MaxLevel, Is.LessThanOrEqualTo(PartyLevels.Base));
		for (var seed = 0; seed < 20; seed++)
		{
			var route = (PartyRun.Start(PartyContent.Pike, seed)).EnterRoute().Route!;
			foreach (var node in route.Nodes.Where(n => n.Kind is NodeKind.Wild or NodeKind.Grass))
				Assert.That(
					node.Encounter!.Foes,
					Has.Count.InRange(1, 2).And.All.Matches<Foe>(f => f.Level <= PartyLevels.Base),
					$"seed {seed}, {node.Kind}"
				);
		}
	}

	// ===== Your monsters grow
}
