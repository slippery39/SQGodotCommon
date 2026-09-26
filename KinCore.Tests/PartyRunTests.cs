using System.Collections.Immutable;
using ImmutableGameObjects;
using KinCore;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **THE RUN — every rule FIRES** (KinJam.md "THE MAP", "CATCHING"): towns, areas with their own
/// pools, trails, the deeper path, gyms, gold and the shop; one starter and the rest caught, the
/// bench, HP carried, knockouts revived. Inline monsters, foes, regions and rewards only.
/// </summary>
public class PartyRunTests
{
	/// <summary>Hits their whole line for everything: dropped on any monster still standing, it ends a fight.</summary>
	private static KinCard Wipe(string name) =>
		new()
		{
			Name = name,
			Cost = 0,
			Effects =
			[
				new KinEffect
				{
					Template = new StrikeAction { Amount = 999, Aim = Aim.Sweep },
					Text = name,
				},
			],
		};

	private static PartyCompanion Mon(string name, int hp = 40) =>
		new(name, hp, 0, [new Intent { Name = "Idle", Kind = IntentType.Block }]);

	private static Foe Foe(string name, int position = 0, int hit = 0) =>
		new()
		{
			Name = name,
			Hp = 50,
			MaxHp = 50,
			Position = position,
			Pattern =
			[
				hit > 0
					? new Intent
					{
						Name = "Hit",
						Kind = IntentType.Attack,
						Amount = hit,
					}
					: new Intent { Name = "Idle", Kind = IntentType.Block },
			],
		};

	private static readonly PartyCompanion A = Mon("A");
	private static readonly PartyCompanion B = Mon("B");
	private static readonly PartyCompanion C = Mon("C");

	private static Area Area(string name) =>
		new(name, "", [Foe($"{name}1"), Foe($"{name}2")], Foe($"{name} Rare"));

	private static Region Region(string name) =>
		new(name, [Area($"{name}North"), Area($"{name}South")], Fight(Foe($"{name} Gym")), 1, 2);

	private static Encounter Fight(params Foe[] foes) => new("Fight", [.. foes]);

	private static PartyRun Run() =>
		PartyRun.Start(
			A,
			seed: 1,
			regions: [Region("R1"), Region("R2")],
			deck: [Wipe("Wipe")],
			rewards: [Wipe("Prize1"), Wipe("Prize2"), Wipe("Prize3")]
		);

	/// <summary>A run on a trail of the given fights — for the rules that need a foe that hits.</summary>
	private static PartyRun OnTrail(PartyRun run, params Encounter[] fights) =>
		run with
		{
			Phase = RunPhase.Trail,
			Area = Area("Test"),
			Trail = [.. fights.Select(f => new Stop(StopKind.Battle, f))],
			StopIndex = 0,
		};

	private static PartyRun WithTeam(PartyRun run, params PartyCompanion[] team) =>
		run with
		{
			Team = [.. team.Select(c => new RunCompanion(c, c.Hp))],
		};

	/// <summary>
	/// Plays an action — pressing FIGHT first if the battle is still deploying (R2): a run's battle
	/// opens deploying, and these tests are about what happens once it has begun.
	/// </summary>
	private static GameState Do(GameState s, GameAction a) =>
		(
			s.GetParty().Deploying && a is not (DeployMoveAction or BeginFightAction)
				? s.AddAction(new BeginFightAction()).ProcessAllActions().State
				: s
		)
			.AddAction(a)
			.ProcessAllActions()
			.State;

	/// <summary>Wins the battle: the Wipe, dropped on the first monster still standing.</summary>
	private static GameState Win(GameState s)
	{
		var wipe = s.CardsIn(ZoneType.Hand).First(c => c.Name == "Wipe");
		return Do(
			s,
			new PlayPartyCardAction { CardId = wipe.Id, Space = s.LivingAllies().First().Position }
		);
	}

	private static (PartyRun, RunReport) WinNext(PartyRun run) =>
		run.AfterBattle(Win(run.StartBattle()));

	/// <summary>Weakens the foe at that place in their line to `hp` and throws a Snare at it.</summary>
	private static GameState Catch(GameState s, int position, int hp)
	{
		var foe = s.LivingFoes().Single(f => f.Position == position);
		s = s.UpdateObject(foe.Id, foe with { Hp = hp });
		return Do(s, new UseSnareAction { FoeId = foe.Id });
	}

	private static int Hp(PartyRun run, string name) =>
		run.Team.Single(m => m.Companion.Name == name).Hp;

	// ===== The route

	[Test]
	public void ARunStartsInTheFirstTownWithOneMonsterSnaresAndGold()
	{
		var run = Run();

		Assert.That(run.Phase, Is.EqualTo(RunPhase.Town));
		Assert.That(run.Region.Name, Is.EqualTo("R1"));
		Assert.That(run.Team.Select(m => m.Companion.Name), Is.EqualTo(new[] { "A" }));
		Assert.That(run.Snares, Is.EqualTo(PartyRun.StartingSnares));
		Assert.That(run.Gold, Is.EqualTo(PartyRun.StartingGold));
	}

	[Test]
	public void AnAreasTrailIsTwoFightsAFindAndTheDeeperPathFromItsOwnPool()
	{
		for (var seed = 0; seed < 20; seed++)
		{
			var run = (Run() with { Seed = seed }).LeaveTown().ChooseArea(1);

			Assert.That(
				run.Trail.Select(s => s.Kind),
				Is.EqualTo(new[] { StopKind.Battle, StopKind.Battle, StopKind.Find, StopKind.Deep })
			);
			var wild = run.Trail.Take(2).SelectMany(s => s.Encounter!.Foes).Select(f => f.Name);
			Assert.That(wild, Is.All.AnyOf("R1South1", "R1South2"), $"seed {seed}");
			Assert.That(
				run.Trail[3].Encounter!.Foes.Select(f => f.Name),
				Does.Contain("R1South Rare"),
				"the rare lives down the deeper path"
			);
		}
	}

	[Test]
	public void WinningAFightPaysGoldAndWalksOn()
	{
		var run = Run().LeaveTown().ChooseArea(0);

		RunReport report;
		(run, report) = WinNext(run);

		Assert.That(report.Gold, Is.EqualTo(PartyRun.WildGold));
		Assert.That(run.Gold, Is.EqualTo(PartyRun.StartingGold + PartyRun.WildGold));
		Assert.That(run.StopIndex, Is.EqualTo(1));
	}

	[TestCase(FindKind.Snare)]
	[TestCase(FindKind.Gold)]
	[TestCase(FindKind.Rest)]
	public void AFindIsPickedUp(FindKind kind)
	{
		var run = Run() with
		{
			Phase = RunPhase.Trail,
			Team = [new RunCompanion(A, 10)],
			Trail = [new Stop(StopKind.Find, Find: kind)],
		};

		var after = run.TakeFind();

		Assert.That(after.Snares - run.Snares, Is.EqualTo(kind == FindKind.Snare ? 1 : 0));
		Assert.That(
			after.Gold - run.Gold,
			Is.EqualTo(kind == FindKind.Gold ? PartyRun.FoundGold : 0)
		);
		Assert.That(Hp(after, "A"), Is.EqualTo(kind == FindKind.Rest ? 10 + 12 : 10), "30% of 40");
		Assert.That(after.Phase, Is.EqualTo(RunPhase.Gym), "the trail's end is the gym");
	}

	[Test]
	public void TurningBackFromTheDeeperPathGoesToTheGym()
	{
		var run = Run().LeaveTown().ChooseArea(0);
		run = WinNext(WinNext(run).Item1).Item1.TakeFind();
		Assert.That(run.CurrentStop.Kind, Is.EqualTo(StopKind.Deep));

		run = run.SkipDeep();

		Assert.That(run.Phase, Is.EqualTo(RunPhase.Gym));
		Assert.That(run.NextFight.Foes.Single().Name, Is.EqualTo("R1 Gym"));
	}

	[Test]
	public void WinningAGymHealsEveryoneInTheNextTown()
	{
		var run = WithTeam(Run(), A, B) with { Phase = RunPhase.Gym };
		run = run with { Team = [.. run.Team.Select(m => m with { Hp = 5 })] };

		(run, _) = WinNext(run);

		Assert.That(run.Phase, Is.EqualTo(RunPhase.Town));
		Assert.That(run.Region.Name, Is.EqualTo("R2"));
		Assert.That(run.Team.Select(m => m.Hp), Is.All.EqualTo(40));
	}

	[Test]
	public void WinningTheLastGymWinsTheRun()
	{
		var run = Run() with { RegionIndex = 1, Phase = RunPhase.Gym };

		(run, _) = WinNext(run);

		Assert.That(run.IsWon && run.IsOver, Is.True);
	}

	[Test]
	public void LosingABattleEndsTheRun()
	{
		var run = OnTrail(Run(), Fight(Foe("Brute", hit: 99)));

		(run, _) = run.AfterBattle(Do(run.StartBattle(), new EndPartyTurnAction()));

		Assert.That(run.Phase, Is.EqualTo(RunPhase.Lost));
		Assert.That(run.IsOver && !run.IsWon, Is.True);
	}

	[Test]
	public void EveryRealAreaAndGymBuildsABattle()
	{
		foreach (var region in PartyWorld.Regions)
		{
			Assert.That(region.Gym.Foes.All(f => !f.Catchable), Is.True, $"{region.Name}'s gym");
			for (var a = 0; a < region.Areas.Count; a++)
			{
				var run = PartyRun.Start(PartyContent.Pike, seed: 5) with
				{
					RegionIndex = PartyWorld.Regions.IndexOf(region),
				};
				run = run.LeaveTown().ChooseArea(a);
				foreach (var stop in run.Trail.Where(s => s.Encounter is not null))
					Assert.That(
						(run with { StopIndex = run.Trail.IndexOf(stop) })
							.StartBattle()
							.LivingFoes(),
						Is.Not.Empty
					);
			}
			var gym = PartyRun.Start(PartyContent.Pike, 5) with
			{
				RegionIndex = PartyWorld.Regions.IndexOf(region),
				Phase = RunPhase.Gym,
			};
			Assert.That(gym.StartBattle().LivingFoes(), Is.Not.Empty);
		}
	}

	[Test]
	public void ATierScalesAFoesHpBlockAndAttacks()
	{
		var foe = Foe("Brute", hit: 10) with
		{
			Pattern =
			[
				new Intent
				{
					Name = "Hit",
					Kind = IntentType.Attack,
					Amount = 10,
				},
				new Intent
				{
					Name = "Brace",
					Kind = IntentType.Block,
					Amount = 4,
				},
			],
		};

		var scaled = PartyWorld.Scale(foe, new PartyWorld.Tier(1, 1, Hp: 2.0, Damage: 1.5));

		Assert.That(scaled.MaxHp, Is.EqualTo(foe.MaxHp * 2));
		Assert.That(scaled.Pattern[0].Amount, Is.EqualTo(15));
		Assert.That(scaled.Pattern[1].Amount, Is.EqualTo(8), "Block scales with HP");
	}

	[Test]
	public void LaterRegionsFieldMoreAndTougherFoes()
	{
		var first = PartyWorld.Regions[0];
		var last = PartyWorld.Regions[^1];

		Assert.That(last.MaxFoes, Is.GreaterThan(first.MaxFoes));
		Assert.That(
			last.Areas.SelectMany(a => a.Pool).Max(f => f.MaxHp),
			Is.GreaterThan(first.Areas.SelectMany(a => a.Pool).Max(f => f.MaxHp))
		);
		Assert.That(
			last.Gym.Foes.Sum(f => f.MaxHp),
			Is.GreaterThan(first.Gym.Foes.Sum(f => f.MaxHp)),
			"a gym is a tougher line"
		);
	}

	// ===== HP across fights

	[Test]
	public void HpCarriesIntoTheNextFight()
	{
		var run = OnTrail(Run(), Fight(Foe("Brute", hit: 7)), Fight(Foe("Idle")));

		(run, _) = run.AfterBattle(Win(Do(run.StartBattle(), new EndPartyTurnAction())));

		Assert.That(Hp(run, "A"), Is.EqualTo(33));
		Assert.That(run.StartBattle().Allies().Single().Hp, Is.EqualTo(33));
	}

	[Test]
	public void AKnockedOutMonsterRevivesAtAQuarterOfItsMax()
	{
		var run = OnTrail(WithTeam(Run(), A, B), Fight(Foe("Brute", 1, 99)), Fight(Foe("Idle")));

		var battle = Do(run.StartBattle(), new EndPartyTurnAction()); // A, in front, is knocked out
		Assert.That(battle.Allies().Single(a => a.Name == "A").IsKnockedOut, Is.True);

		RunReport report;
		(run, report) = run.AfterBattle(Win(battle)); // B wins it
		Assert.That(report.Revived, Is.EqualTo(new[] { "A" }));
		Assert.That(Hp(run, "A"), Is.EqualTo(10));
	}

	// ===== Deploy — the order is yours, and it is kept

	[Test]
	public void TheOrderYouDeployIsKeptForTheNextFight()
	{
		var run = OnTrail(WithTeam(Run(), A, B), Fight(Foe("Idle")), Fight(Foe("Idle")));

		var battle = run.StartBattle();
		Assert.That(battle.GetParty().Deploying, Is.True, "a run's battle opens deploying");
		battle = Do(
			battle,
			new DeployMoveAction { AllyId = battle.Allies().Single(a => a.Name == "B").Id, To = 0 }
		);
		battle = Do(battle, new BeginFightAction());
		(run, _) = run.AfterBattle(Win(battle));

		Assert.That(run.Team.Select(m => m.Companion.Name), Is.EqualTo(new[] { "B", "A" }));
		Assert.That(
			run.StartBattle().LivingAllies().Select(a => a.Name),
			Is.EqualTo(new[] { "B", "A" })
		);
	}

	// ===== The bench in battle

	[Test]
	public void TheBenchFightsAndComesBackWithTheHpItHasLeft()
	{
		var run = OnTrail(
			WithTeam(Run(), Mon("A", hp: 5)) with
			{
				Bench = [new RunCompanion(B, 40)],
			},
			Fight(Foe("Brute", hit: 9)),
			Fight(Foe("Idle"))
		);

		var battle = EndTurnTwice(run.StartBattle()); // A faints, B steps in and takes a hit
		RunReport report;
		(run, report) = run.AfterBattle(Win(battle));

		Assert.That(report.Revived, Is.EqualTo(new[] { "A" }));
		Assert.That(run.Bench.Single().Hp, Is.EqualTo(40 - 9), "B stays on the bench, hurt");
		Assert.That(run.Team.Single().Companion.Name, Is.EqualTo("A"), "the lineup is unchanged");
	}

	private static GameState EndTurnTwice(GameState s) =>
		Do(Do(s, new EndPartyTurnAction()), new EndPartyTurnAction());

	// ===== Catching

	[Test]
	public void ACaughtFoeJoinsWithItsCycleAndTheHpItWasCaughtAt()
	{
		var run = OnTrail(Run(), Fight(Foe("Brute", hit: 6)), Fight(Foe("Idle")));

		RunReport report;
		(run, report) = run.AfterBattle(Catch(run.StartBattle(), 0, hp: 10));

		Assert.That(report.Caught, Is.EqualTo(new[] { "Brute" }));
		var caught = run.Team[1];
		Assert.That(caught.Hp, Is.EqualTo(10));
		Assert.That(caught.Companion.Moves.Single().Amount, Is.EqualTo(6), "its own move");
		Assert.That(run.Snares, Is.EqualTo(PartyRun.StartingSnares - 1), "the Snare is spent");
		Assert.That(run.StartBattle().Allies().Count(), Is.EqualTo(2), "and it fights");
	}

	[Test]
	public void ACatchBeyondThreeGoesToTheBenchAndCanBeSwappedIn()
	{
		var run = OnTrail(
			WithTeam(Run(), A, B, C),
			Fight(Foe("Runt", 0), Foe("Brute", 1)),
			Fight(Foe("Idle"))
		);

		RunReport report;
		(run, report) = run.AfterBattle(Win(Catch(run.StartBattle(), 0, hp: 5)));

		Assert.That(report.ToBench, Is.EqualTo(new[] { "Runt" }));
		Assert.That(run.Team, Has.Count.EqualTo(PartyRun.TeamSize));

		run = run.Swap(teamIndex: 1, benchIndex: 0);
		Assert.That(run.Team.Select(m => m.Companion.Name), Is.EqualTo(new[] { "A", "Runt", "C" }));
		Assert.That(run.Bench.Single().Companion.Name, Is.EqualTo("B"));
	}

	// ===== Cards and the shop

	[Test]
	public void ATakenRewardJoinsTheDeck()
	{
		var run = Run();

		run = run.Take(run.RewardOffer()[0]);

		Assert.That(run.Deck, Has.Count.EqualTo(2));
	}

	[Test]
	public void TheShopSellsSnaresForGold()
	{
		var run = Run().BuySnare();

		Assert.That(run.Snares, Is.EqualTo(PartyRun.StartingSnares + 1));
		Assert.That(run.Gold, Is.EqualTo(PartyRun.StartingGold - PartyRun.SnarePrice));

		var broke = run with { Gold = PartyRun.SnarePrice - 1 };
		Assert.That(broke.CanBuySnare, Is.False);
		Assert.That(broke.BuySnare(), Is.EqualTo(broke), "nothing happens");
	}

	[Test]
	public void AShopCardCanBeBoughtOnce()
	{
		var run = Run() with { Gold = 999 };

		run = run.BuyCard(0);
		Assert.That(run.Deck.Last(), Is.EqualTo(run.ShopCards()[0]));
		Assert.That(run.CanBuyCard(0), Is.False);
		Assert.That(run.CanBuyCard(1), Is.True);
	}

	[Test]
	public void TheShopRemovesACardForGold()
	{
		var run = (Run() with { Gold = 999 }).Take(Wipe("Extra"));

		run = run.Remove(0);

		Assert.That(run.Deck.Select(c => c.Name), Is.EqualTo(new[] { "Extra" }));
		Assert.That(run.Gold, Is.EqualTo(999 - PartyRun.RemovePrice));
		Assert.That(run.CanRemove, Is.False, "never the last card");
	}
}
