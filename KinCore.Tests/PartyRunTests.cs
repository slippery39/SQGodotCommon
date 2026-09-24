using System.Collections.Immutable;
using ImmutableGameObjects;
using KinCore;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **THE RUN, v1 — every rule FIRES** (KinJam.md "THE RUN"): one starter, two joins, HP carried,
/// knockouts revived at a quarter, one rest before the last battle, rewards into the trainer's deck.
/// Inline companions, foes, encounters and rewards only.
/// </summary>
public class PartyRunTests
{
	/// <summary>Hits every column for everything: dropped on any monster still standing, it ends a fight.</summary>
	private static KinCard Wipe(string name) =>
		new()
		{
			Name = name,
			Cost = 0,
			Effects =
			[
				new KinEffect
				{
					Template = new StrikeAction
					{
						Amount = 999,
						Offsets = [-4, -3, -2, -1, 0, 1, 2, 3, 4],
					},
					Text = name,
				},
			],
		};

	private static PartyCompanion Mon(string name, int hp = 40) =>
		new(name, hp, 0, 1, [new Intent { Name = "Idle", Kind = IntentType.Block }]);

	private static Foe Foe(int space, int hit = 0) =>
		new()
		{
			Name = "Foe",
			Hp = 50,
			MaxHp = 50,
			Speed = 3,
			Space = space,
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

	private static readonly KinCard Prize = Wipe("Prize");

	private static PartyRun Run(params Encounter[] encounters) =>
		PartyRun.Start(
			A,
			seed: 1,
			roster: [A, B, C],
			encounters: [.. encounters],
			deck: [Wipe("Wipe")],
			rewards: [Prize]
		);

	private static Encounter Fight(params Foe[] foes) => new("Fight", [.. foes]);

	private static GameState Do(GameState s, GameAction a) =>
		s.AddAction(a).ProcessAllActions().State;

	/// <summary>Wins the battle: the Wipe, dropped on the first monster still standing.</summary>
	private static GameState Win(GameState s)
	{
		var wipe = s.CardsIn(ZoneType.Hand).First(c => c.Name == "Wipe");
		return Do(
			s,
			new PlayPartyCardAction { CardId = wipe.Id, Space = s.LivingAllies().First().Space }
		);
	}

	private static int Hp(PartyRun run, string name) =>
		run.Team.Single(m => m.Companion.Name == name).Hp;

	[Test]
	public void ARunStartsWithOneAndTheOthersJoinAfterTheFirstTwoWins()
	{
		var run = Run(Fight(Foe(2)), Fight(Foe(2)), Fight(Foe(2)), Fight(Foe(2)));
		Assert.That(run.Team.Select(m => m.Companion.Name), Is.EqualTo(new[] { "A" }));

		RunReport report;
		(run, report) = run.AfterBattle(Win(run.StartBattle()));
		Assert.That(report.Joined, Is.EqualTo("B"));

		(run, report) = run.AfterBattle(Win(run.StartBattle()));
		Assert.That(report.Joined, Is.EqualTo("C"));

		(run, report) = run.AfterBattle(Win(run.StartBattle()));
		Assert.That(report.Joined, Is.Null);
		Assert.That(run.Team, Has.Count.EqualTo(3));
	}

	[Test]
	public void HpCarriesIntoTheNextBattle()
	{
		var run = Run(Fight(Foe(2, hit: 7)), Fight(Foe(1)), Fight(Foe(1)));

		var battle = Do(run.StartBattle(), new EndPartyTurnAction()); // A takes 7
		(run, _) = run.AfterBattle(Win(battle));

		Assert.That(Hp(run, "A"), Is.EqualTo(33));
		Assert.That(run.StartBattle().Allies().Single(a => a.Name == "A").Hp, Is.EqualTo(33));
	}

	[Test]
	public void AKnockedOutCompanionRevivesAtAQuarterOfItsMax()
	{
		// Four fights, so the rest (before the last) does not land on the same step as the revive.
		var run = Run(Fight(Foe(2)), Fight(Foe(1, hit: 99)), Fight(Foe(1)), Fight(Foe(1)));
		(run, _) = run.AfterBattle(Win(run.StartBattle())); // B joins: A at 1, B at 3

		var battle = Do(run.StartBattle(), new EndPartyTurnAction()); // A is knocked out
		Assert.That(battle.Allies().Single(a => a.Name == "A").IsKnockedOut, Is.True);

		RunReport report;
		(run, report) = run.AfterBattle(Win(battle)); // B wins it
		Assert.That(report.Revived, Is.EqualTo(new[] { "A" }));
		Assert.That(Hp(run, "A"), Is.EqualTo(10));
	}

	[Test]
	public void LosingABattleEndsTheRun()
	{
		var run = Run(Fight(Foe(2, hit: 99)), Fight(Foe(2)));

		(run, _) = run.AfterBattle(Do(run.StartBattle(), new EndPartyTurnAction()));

		Assert.That(run.Lost && run.IsOver, Is.True);
		Assert.That(run.IsWon, Is.False);
	}

	[Test]
	public void TheTeamRestsOnceBeforeTheLastBattle()
	{
		var run = Run(Fight(Foe(2, hit: 20)), Fight(Foe(1)));

		RunReport report;
		(run, report) = run.AfterBattle(Win(Do(run.StartBattle(), new EndPartyTurnAction())));

		Assert.That(report.Rested, Is.True);
		Assert.That(Hp(run, "A"), Is.EqualTo(20 + 12), "30% of 40 back");
		Assert.That(Hp(run, "B"), Is.EqualTo(40), "a companion never rests past its max");
	}

	[Test]
	public void WinningTheLastBattleWinsTheRun()
	{
		var run = Run(Fight(Foe(2)));

		(run, _) = run.AfterBattle(Win(run.StartBattle()));

		Assert.That(run.IsWon && run.IsOver, Is.True);
	}

	[Test]
	public void ATakenRewardJoinsTheDeck()
	{
		var run = Run(Fight(Foe(2)), Fight(Foe(2)));

		run = run.Take(run.RewardOffer().Single());
		var deck = run.StartBattle().CardsIn(ZoneType.Hand).Select(c => c.Name);

		Assert.That(deck, Is.EquivalentTo(new[] { "Wipe", "Prize" }));
	}
}
