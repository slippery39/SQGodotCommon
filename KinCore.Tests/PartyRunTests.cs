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
public partial class PartyRunTests
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
		new(
			name,
			[Area($"{name}North"), Area($"{name}South")],
			[Fight(Foe($"{name} Boss"))],
			[Fight(Foe($"{name} Elite"))],
			1,
			2
		);

	private static Encounter Fight(params Foe[] foes) => new("Fight", [.. foes]);

	private static PartyRun Run() =>
		PartyRun.Start(
			A,
			seed: 1,
			regions: [Region("R1"), Region("R2")],
			deck: [Wipe("Wipe")],
			rewards: [Wipe("Prize1"), Wipe("Prize2"), Wipe("Prize3")]
		);

	private static PartyRun WithTeam(PartyRun run, params PartyCompanion[] team) =>
		run with
		{
			Team = [.. team.Select(c => new RunCompanion(c, c.Hp))],
		};

	private static GameState Do(GameState s, GameAction a) =>
		s.AddAction(a).ProcessAllActions().State;

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

	private static int Hp(PartyRun run, string name) =>
		run.Team.Single(m => m.Companion.Name == name).Hp;

	// ===== The regions

	[Test]
	public void ARunStartsInTheFirstTownWithOneMonsterAndGold()
	{
		var run = Run();

		Assert.That(run.Phase, Is.EqualTo(RunPhase.Town));
		Assert.That(run.Region.Name, Is.EqualTo("R1"));
		Assert.That(run.Team.Select(m => m.Companion.Name), Is.EqualTo(new[] { "A" }));
		Assert.That(run.Gold, Is.EqualTo(PartyRun.StartingGold));
	}

	[Test]
	public void BeatingTheLastBossWinsTheRun()
	{
		var run = OnRoute(Run() with { RegionIndex = 1 }).MoveTo(1);
		Assert.That(run.AtBoss);

		(run, _) = WinNext(run);

		Assert.That(run.IsWon && run.IsOver, Is.True);
	}

	[Test]
	public void LosingABattleEndsTheRun()
	{
		var run = OnFights(Run(), Fight(Foe("Brute", hit: 99)));

		(run, _) = run.AfterBattle(Do(run.StartBattle(), new EndPartyTurnAction()));

		Assert.That(run.Phase, Is.EqualTo(RunPhase.Lost));
		Assert.That(run.IsOver && !run.IsWon, Is.True);
	}

	[Test]
	public void EveryRealRouteFightEliteAndBossBuildsABattle()
	{
		foreach (var region in PartyWorld.Regions)
		{
			var index = PartyWorld.Regions.IndexOf(region);
			var route = (
				PartyRun.Start(PartyContent.Pike, seed: 5) with
				{
					RegionIndex = index,
				}
			).EnterRoute();
			Assert.That(route.Route!.End.IsFight, "the boss stands at the end");
			foreach (var node in route.Route.Nodes.Where(n => n.IsFight))
				Assert.That(
					(route with { NodeId = node.Id }).StartBattle().LivingFoes(),
					Is.Not.Empty,
					$"{region.Name}, {node.Kind}"
				);
		}
	}

	// ===== HP across fights

	[Test]
	public void HpCarriesIntoTheNextFight()
	{
		var run = OnFights(Run(), Fight(Foe("Brute", hit: 7)), Fight(Foe("Idle")));

		(run, _) = run.AfterBattle(Win(Do(run.StartBattle(), new EndPartyTurnAction())));

		Assert.That(Hp(run, "A"), Is.EqualTo(33));
		Assert.That(run.StartBattle().Allies().Single().Hp, Is.EqualTo(33));
	}

	[Test]
	public void AKnockedOutMonsterIsBackAtOneHp()
	{
		var run = OnFights(WithTeam(Run(), A, B), Fight(Foe("Brute", 1, 99)), Fight(Foe("Idle")));

		var battle = Do(run.StartBattle(), new EndPartyTurnAction()); // A, in front, is knocked out
		Assert.That(battle.Allies().Single(a => a.Name == "A").IsKnockedOut, Is.True);

		RunReport report;
		(run, report) = run.AfterBattle(Win(battle)); // B wins it
		Assert.That(report.Revived, Is.EqualTo(new[] { "A" }));
		Assert.That(Hp(run, "A"), Is.EqualTo(1), "round 4: back at 1 HP");
	}

	// ===== Monsters come from BOSSES (round 4)

	[Test]
	public void TheFirstTwoBossesEachOfferThreeMonstersOfYourFamily()
	{
		var run = Run() with
		{
			Family = Family.Grove,
			Regions = [Region("R1"), Region("R2"), Region("R3")],
		};

		foreach (var region in new[] { 0, 1 })
		{
			var (town, _) = WinNext(OnRoute(run with { RegionIndex = region }).MoveTo(1));

			Assert.That(town.MonsterChoice, Has.Count.EqualTo(3), $"boss {region + 1}");
			Assert.That(town.MonsterChoice.Select(m => m.Family), Is.All.EqualTo(Family.Grove));

			var pick = town.MonsterChoice[0];
			run = town.ChooseMonster(pick);
			Assert.That(run.Team.Select(m => m.Companion.Name), Does.Contain(pick.Name));
			Assert.That(
				run.Team.Single(m => m.Companion.Name == pick.Name).Hp,
				Is.EqualTo(pick.Hp)
			);
			Assert.That(run.MonsterChoice, Is.Empty);
		}

		Assert.That(run.Team, Has.Count.EqualTo(PartyRun.TeamSize));
		var (third, _) = WinNext(OnRoute(run with { RegionIndex = 2 }).MoveTo(1));
		Assert.That(third.MonsterChoice, Is.Empty, "a full team is offered no more");
	}

	// ===== The team's order — set in town, and only there (deploy cut, 2026-10-02)

	[Test]
	public void TheOrderIsSetInTownAndIsTheLine()
	{
		var town = WithTeam(Run(), A, B) with { Phase = RunPhase.Town };

		var moved = town.MoveToFront(1);
		Assert.That(moved.Team.Select(m => m.Companion.Name), Is.EqualTo(new[] { "B", "A" }));

		var run = OnFights(moved, Fight(Foe("Idle")));
		Assert.That(
			run.StartBattle().LivingAllies().Select(a => a.Name),
			Is.EqualTo(new[] { "B", "A" }),
			"the town's order is the fight's line, and the fight opens ready to play"
		);
		Assert.That(run.MoveToFront(1).Team, Is.EqualTo(run.Team), "not on the route");
	}

	// ===== The bench in battle

	private static GameState EndTurnTwice(GameState s) =>
		Do(Do(s, new EndPartyTurnAction()), new EndPartyTurnAction());

	// ===== Catching

	// ===== Cards and the shop

	[Test]
	public void ATakenRewardJoinsTheDeck()
	{
		var run = Run();

		run = run.Take(run.RewardOffer()[0]);

		Assert.That(run.Deck, Has.Count.EqualTo(2));
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
