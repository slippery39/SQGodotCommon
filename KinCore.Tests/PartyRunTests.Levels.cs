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
	public void AtTheBaseLevelNothingChanges()
	{
		var foe = Hitter(10, 4);

		var same = PartyLevels.Scale(foe, PartyLevels.Base);

		Assert.That(same.MaxHp, Is.EqualTo(foe.MaxHp));
		Assert.That(
			same.Pattern.Select(i => i.Amount),
			Is.EqualTo(foe.Pattern.Select(i => i.Amount))
		);
		Assert.That(PartyLevels.Scale(A, PartyLevels.Base).Hp, Is.EqualTo(A.Hp));
	}

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

	[Test]
	public void AWinGivesTheTeamXpAndEnoughOfItALevel()
	{
		var foe = Foe("W") with { Level = 20 };
		var run = OnFights(WithTeam(Run(), A, B), Fight(foe));
		var xp = PartyLevels.XpFor([foe], leader: false);
		Assert.That(
			xp,
			Is.GreaterThanOrEqualTo(PartyLevels.XpToNext(PartyLevels.Base)),
			"enough for a level"
		);

		(run, var report) = WinNext(run);

		Assert.That(report.Xp, Is.EqualTo(xp));
		Assert.That(run.Team.Select(m => m.Level), Is.All.GreaterThan(PartyLevels.Base));
		Assert.That(report.LevelUps, Has.Count.EqualTo(2));
	}

	[Test]
	public void ALevelGainedRaisesMaxHpAndCurrentHpAlike()
	{
		var hurt = new RunCompanion(A, 10);

		var grown = PartyLevels.Gain(hurt, PartyLevels.XpToNext(PartyLevels.Base));

		Assert.That(grown.Level, Is.EqualTo(PartyLevels.Base + 1));
		Assert.That(grown.MaxHp, Is.GreaterThan(hurt.MaxHp));
		Assert.That(grown.Hp - hurt.Hp, Is.EqualTo(grown.MaxHp - hurt.MaxHp));
		Assert.That(grown.Xp, Is.Zero);
	}

	[Test]
	public void ALeaderIsWorthDoubleXp()
	{
		var foes = new[] { Foe("L") with { Level = 8 } };

		Assert.That(
			PartyLevels.XpFor(foes, leader: true),
			Is.EqualTo(2 * PartyLevels.XpFor(foes, leader: false))
		);
	}

	[Test]
	public void AMonsterFightsAtItsLevel()
	{
		var run = OnFights(
			Run() with
			{
				Team = [new RunCompanion(A, 30, Level: 10)],
			},
			Fight(Foe("W"))
		);

		var ally = run.StartBattle().Allies().Single();

		Assert.That(ally.Level, Is.EqualTo(10));
		Assert.That(ally.MaxHp, Is.EqualTo(PartyLevels.At(A.Hp, 10)));
	}

	[Test]
	public void ACatchJoinsAtItsLevelFromItsSpeciesBase()
	{
		var boar = PartyWorld.Species("Boar")!;
		var wild = PartyLevels.Scale(boar, 3) with { Position = 0 };
		var run = OnFights(Run(), Fight(wild), Fight(Foe("Idle")));

		(run, _) = run.AfterBattle(Catch(run.StartBattle(), 0, hp: 5));

		var caught = run.Team.Single(m => m.Companion.Name == "Boar");
		Assert.That(caught.Level, Is.EqualTo(3));
		Assert.That(caught.Companion.Hp, Is.EqualTo(boar.MaxHp), "stored at its species' base");
		Assert.That(caught.MaxHp, Is.EqualTo(wild.MaxHp), "and fights at its level");
	}
}
