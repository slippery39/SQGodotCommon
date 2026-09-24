using System.Collections.Immutable;
using ImmutableGameObjects;
using KinCore;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **AUTO-BATTLE v1 — every mechanic FIRES** (KinJam.md). Exploring, not tuning: these read a
/// number that moved, never a field that was set, and every monster, card and foe is inline.
/// </summary>
public class PartyTests
{
	private static KinCard Card(string name, int cost, params GameAction[] steps) =>
		new()
		{
			Name = name,
			Cost = cost,
			Effects = [.. steps.Select(t => new KinEffect { Template = t, Text = name })],
		};

	private static readonly Intent Idle =
		new()
		{
			Name = "Idle",
			Kind = IntentType.Block,
			Amount = 0,
		};

	private static Intent Hit(int amount, params int[] offsets) =>
		new()
		{
			Name = "Hit",
			Kind = IntentType.Attack,
			Amount = amount,
			Offsets = offsets.Length > 0 ? [.. offsets] : [0],
		};

	private static PartyCompanion Mon(
		string name,
		int hp = 20,
		int power = 0,
		int speed = 2,
		params Intent[] moves
	) => new(name, hp, power, speed, moves.Length > 0 ? [.. moves] : [Idle]);

	private static Foe Foe(int space, int hp = 50, int speed = 2, params Intent[] pattern) =>
		new()
		{
			Name = "Foe",
			Hp = hp,
			MaxHp = hp,
			Speed = speed,
			Space = space,
			Pattern = pattern.Length > 0 ? [.. pattern] : [Idle],
		};

	/// <summary>Decks here are five cards or fewer, so every card is in the opening hand.</summary>
	private static GameState Battle(
		PlacedCompanion[] companions,
		KinCard[] deck,
		params Foe[] foes
	) =>
		PartyBattleFactory.Create(
			new PartyScenario("Test", "", [.. companions], [.. foes], [.. deck], [])
		);

	private static GameState Do(GameState s, GameAction action) =>
		s.AddAction(action).ProcessAllActions().State;

	private static GameState Play(GameState s, string name, int space, bool foeRow = false) =>
		Do(
			s,
			new PlayPartyCardAction
			{
				CardId = InHand(s, name).Id,
				Space = space,
				FoeRow = foeRow,
			}
		);

	private static bool CanPlay(GameState s, string name, int space, bool foeRow = false) =>
		new PlayPartyCardAction
		{
			CardId = InHand(s, name).Id,
			Space = space,
			FoeRow = foeRow,
		}
			.ValidateAdd(s)
			.IsValid;

	private static GameState Step(GameState s, string who, int space) =>
		Do(s, new MoveAllyAction { AllyId = Named(s, who).Id, Space = space });

	private static GameState EndTurn(GameState s) => Do(s, new EndPartyTurnAction());

	private static KinCard InHand(GameState s, string name) =>
		s.CardsIn(ZoneType.Hand).First(c => c.Name == name);

	private static Ally Named(GameState s, string name) => s.Allies().Single(a => a.Name == name);

	private static Foe FoeIn(GameState s, int space) =>
		s.GetChildren(s.GetWellKnownId(PartyState.BattleKey))
			.OfType<Foe>()
			.Single(f => f.Space == space);

	// ===== Monsters fight on their own

	[Test]
	public void AMonsterAttacksAheadAtTheEndOfTheTurnWithItsPower()
	{
		var s = Battle([new(Mon("Pike", power: 3, moves: Hit(2)), 2)], [], Foe(2), Foe(3));

		s = EndTurn(s);

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50 - (2 + 3)));
		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(50), "only the column ahead is hit");
	}

	[Test]
	public void AMonsterInTheWrongColumnMisses()
	{
		var s = Battle([new(Mon("Pike", moves: Hit(5)), 0)], [], Foe(2));

		s = EndTurn(s);

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50));
	}

	[Test]
	public void AThreeWideMoveHitsThreeColumns()
	{
		var s = Battle(
			[new(Mon("Gale", moves: Hit(4, -1, 0, 1)), 2)],
			[],
			Foe(1),
			Foe(2),
			Foe(3),
			Foe(4)
		);

		s = EndTurn(s);

		Assert.That(new[] { 1, 2, 3 }.Select(c => FoeIn(s, c).Hp), Is.All.EqualTo(46));
		Assert.That(FoeIn(s, 4).Hp, Is.EqualTo(50));
	}

	[Test]
	public void TheCycleAdvancesEveryTurn()
	{
		var s = Battle([new(Mon("Pike", moves: [Hit(1), Hit(10)]), 2)], [], Foe(2));

		s = EndTurn(s);
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(49));
		Assert.That(
			Named(s, "Pike").Current.Amount,
			Is.EqualTo(10),
			"the next move is telegraphed"
		);

		s = EndTurn(s);
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(39));
	}

	[Test]
	public void AFasterMonsterKillsAFoeBeforeItsBlowLands()
	{
		var s = Battle(
			[new(Mon("Pike", speed: 3, moves: Hit(10)), 2)],
			[],
			Foe(2, hp: 10, speed: 2, pattern: Hit(7)),
			Foe(4)
		);

		s = EndTurn(s);

		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20), "the foe died before its turn came");
	}

	[Test]
	public void ASlowerMonsterIsHitFirst()
	{
		var s = Battle(
			[new(Mon("Bramble", speed: 1, moves: Hit(10)), 2)],
			[],
			Foe(2, hp: 10, speed: 2, pattern: Hit(7)),
			Foe(4)
		);

		s = EndTurn(s);

		Assert.That(Named(s, "Bramble").Hp, Is.EqualTo(13));
		Assert.That(FoeIn(s, 2).IsDead, Is.True, "and still lands its own blow after");
	}

	[Test]
	public void OnATieYoursActFirst()
	{
		var s = Battle([new(Mon("Pike", speed: 2), 0)], [], Foe(1, speed: 2), Foe(3, speed: 3));

		var order = s.ActingOrder().Select(c => c.Speed + (c is Ally ? "A" : "F"));

		Assert.That(order, Is.EqualTo(new[] { "3F", "2A", "2F" }));
	}

	[Test]
	public void AKnockedOutMonsterDoesNotAct()
	{
		var s = Battle(
			[new(Mon("Pike", hp: 5, speed: 1, moves: Hit(10)), 2), new(Mon("Bramble"), 0)],
			[],
			Foe(2, speed: 2, pattern: Hit(9))
		);

		s = EndTurn(s);

		Assert.That(Named(s, "Pike").IsKnockedOut, Is.True);
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50));
	}

	// ===== The free step

	[Test]
	public void EveryMonsterStepsOncePerTurn()
	{
		var s = Battle([new(Mon("Pike"), 2), new(Mon("Gale"), 4)], [], Foe(0));

		s = Step(s, "Pike", 1);
		s = Step(s, "Gale", 3);

		Assert.That(Named(s, "Pike").Space, Is.EqualTo(1));
		Assert.That(Named(s, "Gale").Space, Is.EqualTo(3));
		Assert.That(
			new MoveAllyAction { AllyId = Named(s, "Pike").Id, Space = 0 }
				.ValidateAdd(s)
				.IsValid,
			Is.False,
			"one step a turn"
		);

		s = EndTurn(s);
		Assert.That(Step(s, "Pike", 0).Allies().First().Space, Is.EqualTo(0), "back next turn");
	}

	[Test]
	public void AStepIntoAnAllySwapsThem()
	{
		var s = Battle(
			[new(Mon("Bramble"), 0), new(Mon("Gale"), 1), new(Mon("Pike"), 2)],
			[],
			Foe(0)
		);

		s = Step(s, "Gale", 2);

		Assert.That(Named(s, "Gale").Space, Is.EqualTo(2));
		Assert.That(Named(s, "Pike").Space, Is.EqualTo(1));
		Assert.That(Named(s, "Pike").StepsLeft, Is.EqualTo(1), "being swapped is not a step");
	}

	[Test]
	public void SteppingOutOfAShapeDodgesIt()
	{
		var s = Battle([new(Mon("Pike"), 2)], [], Foe(2, pattern: Hit(9)));

		s = EndTurn(Step(s, "Pike", 3));

		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20));
	}

	[Test]
	public void TheMiddleOfAThreeWideAttackCannotStepOut()
	{
		var s = Battle([new(Mon("Pike"), 2)], [], Foe(2, pattern: Hit(5, -1, 0, 1)));

		s = EndTurn(Step(s, "Pike", 3));

		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(15));
	}

	[Test]
	public void AHomingAttackFindsTheLowestHpMonsterWhereverItStands()
	{
		var zap = new Intent
		{
			Name = "Zap",
			Kind = IntentType.Attack,
			Amount = 4,
			Homing = true,
		};
		var s = Battle(
			[new(Mon("Bramble", hp: 30), 0), new(Mon("Pike", hp: 12), 4)],
			[],
			Foe(2, pattern: zap)
		);

		s = EndTurn(s);

		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(8));
		Assert.That(Named(s, "Bramble").Hp, Is.EqualTo(30));
	}

	[Test]
	public void AFoeMovesByIntent()
	{
		var drift = new Intent
		{
			Name = "Drift",
			Kind = IntentType.Move,
			Amount = -1,
		};
		var s = Battle([new(Mon("Pike"), 0)], [], Foe(3, pattern: drift));

		s = EndTurn(s);

		Assert.That(s.LivingFoes().Single().Space, Is.EqualTo(2));
	}

	[Test]
	public void AFoesBlockHoldsThroughYourTurnAndDropsWhenItActs()
	{
		var brace = new Intent
		{
			Name = "Brace",
			Kind = IntentType.Block,
			Amount = 6,
		};
		var s = Battle(
			[new(Mon("Pike", speed: 1, moves: Hit(4)), 2)],
			[],
			Foe(2, speed: 3, pattern: [brace, Idle])
		);

		s = EndTurn(s); // it braces first, then Pike hits the Block
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50));
		Assert.That(FoeIn(s, 2).Block, Is.EqualTo(2), "held into your turn");

		s = EndTurn(s); // it acts first again, and its Block drops
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(46));
	}

	// ===== Passives

	[Test]
	public void ThornsHurtWhateverAttacksHerEvenThroughBlock()
	{
		var guard = Card("Guard", 1, new GuardAction { Amount = 10 });
		var bramble = Mon("Bramble") with { Thorns = 2 };
		var s = Battle([new(bramble, 2)], [guard], Foe(2, pattern: Hit(5)));

		s = EndTurn(Play(s, "Guard", 2));

		Assert.That(Named(s, "Bramble").Hp, Is.EqualTo(20));
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(48));
	}

	[Test]
	public void ThornsCanWinTheBattleBeforeTheRestAct()
	{
		var bramble = Mon("Bramble") with { Thorns = 5 };
		var s = Battle(
			[new(bramble, 2)],
			[],
			Foe(2, hp: 5, speed: 3, pattern: Hit(1)),
			Foe(3, hp: 5, speed: 3, pattern: Hit(1, -1))
		);

		s = EndTurn(s);

		Assert.That(s.GetParty().IsOver && s.GetParty().Won, Is.True);
	}

	[Test]
	public void EveryStepBuildsMomentumAndTheNextAttackSpendsIt()
	{
		var dash = Card("Dash", 0, new DashAction());
		var pike = Mon("Pike", moves: Hit(1)) with { MomentumPerStep = 2 };
		var s = Battle([new(pike, 2)], [dash], Foe(2));

		s = Play(s, "Dash", 2);
		s = Step(Step(s, "Pike", 3), "Pike", 2);
		s = EndTurn(s);

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50 - (1 + 4)));
		Assert.That(Named(s, "Pike").Momentum, Is.EqualTo(0));
	}

	[Test]
	public void AFoeMovedWhileGaleStandsIsOffBalance()
	{
		var gust = Card("Gust", 1, new PushAction());
		var gale = Mon("Gale") with { Unbalances = 2 };
		var s = Battle([new(gale, 0), new(Mon("Pike", moves: Hit(3)), 3)], [gust], Foe(2));

		s = EndTurn(Play(s, "Gust", 3, foeRow: true));

		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(50 - (3 + 2)));
	}

	[Test]
	public void GalesGustPushesTheFoeAhead()
	{
		var gust = new Intent
		{
			Name = "Gust",
			Kind = IntentType.Push,
			Amount = 1,
		};
		var s = Battle([new(Mon("Gale", moves: gust), 2)], [], Foe(2));

		s = EndTurn(s);

		Assert.That(s.LivingFoes().Single().Space, Is.EqualTo(3));
	}

	// ===== Trainer cards — played ON something, owned by nobody

	[Test]
	public void GuardBlocksForTheMonsterItIsDroppedOnAndIsGoneNextTurn()
	{
		var guard = Card("Guard", 1, new GuardAction { Amount = 6 });
		var s = Battle(
			[new(Mon("Bramble"), 1), new(Mon("Pike"), 2)],
			[guard],
			Foe(2, pattern: Hit(9))
		);

		s = Play(s, "Guard", 2);
		Assert.That(Named(s, "Bramble").Block, Is.EqualTo(0));

		s = EndTurn(s);
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(17));
		Assert.That(Named(s, "Pike").Block, Is.EqualTo(0));
	}

	[Test]
	public void ACardForYourMonstersMustBeDroppedOnOne()
	{
		var guard = Card("Guard", 1, new GuardAction { Amount = 6 });
		var s = Battle([new(Mon("Pike"), 2)], [guard], Foe(2));

		Assert.That(CanPlay(s, "Guard", 2), Is.True);
		Assert.That(CanPlay(s, "Guard", 3), Is.False, "an empty space");
		Assert.That(CanPlay(s, "Guard", 2, foeRow: true), Is.False, "a foe");
	}

	[Test]
	public void RallyAddsPowerToThisTurnsAttackOnly()
	{
		var rally = Card("Rally", 1, new PowerAction { Amount = 3 });
		var s = Battle([new(Mon("Pike", moves: Hit(2)), 2)], [rally], Foe(2));

		s = EndTurn(Play(s, "Rally", 2));
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(45));

		s = EndTurn(s);
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(43));
	}

	[Test]
	public void HastenPlaysTheMoveNowAndNotAgainAtTheEnd()
	{
		var hasten = Card("Hasten", 1, new HastenAction());
		var s = Battle([new(Mon("Pike", moves: [Hit(5), Hit(1)]), 2)], [hasten], Foe(2));

		s = Play(s, "Hasten", 2);
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(45), "at once");

		s = EndTurn(s);
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(45), "and not again");
	}

	[Test]
	public void HastenThenStepHitsAndDodges()
	{
		var hasten = Card("Hasten", 1, new HastenAction());
		var s = Battle([new(Mon("Pike", moves: Hit(5)), 2)], [hasten], Foe(2, pattern: Hit(9)));

		s = EndTurn(Step(Play(s, "Hasten", 2), "Pike", 3));

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(45));
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20));
	}

	[Test]
	public void StrikeAttacksNowOnTopOfTheMove()
	{
		var strike = Card("Strike", 1, new StrikeAction { Amount = 3 });
		var s = Battle([new(Mon("Pike", power: 2, moves: Hit(1)), 2)], [strike], Foe(2));

		s = Play(s, "Strike", 2);
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(45));

		s = EndTurn(s);
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(42));
	}

	[Test]
	public void StaggerCostsAFoeItsNextMove()
	{
		var stagger = Card("Stagger", 1, new StaggerAction());
		var s = Battle([new(Mon("Pike"), 2)], [stagger], Foe(2, pattern: [Hit(9), Hit(1)]));

		Assert.That(CanPlay(s, "Stagger", 2), Is.False, "it is dropped on a foe");
		s = EndTurn(Play(s, "Stagger", 2, foeRow: true));
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20));

		s = EndTurn(s);
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(19), "the cycle moved on");
	}

	[Test]
	public void GustPullsTheFoeBesideAnEmptySpaceIntoItAndReAimsIt()
	{
		var gust = Card("Gust", 1, new PushAction());
		var s = Battle([new(Mon("Pike"), 2)], [gust], Foe(2, pattern: Hit(9)));

		s = EndTurn(Play(s, "Gust", 1, foeRow: true));

		Assert.That(s.LivingFoes().Single().Space, Is.EqualTo(1));
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20));
	}

	[Test]
	public void GustNeedsExactlyOneFoeBesideAnEmptySpace()
	{
		var gust = Card("Gust", 1, new PushAction());
		var s = Battle([new(Mon("Pike"), 2)], [gust], Foe(1), Foe(3));

		Assert.That(CanPlay(s, "Gust", 0, foeRow: true), Is.True);
		Assert.That(CanPlay(s, "Gust", 2, foeRow: true), Is.False, "two foes beside it");
		Assert.That(CanPlay(s, "Gust", 1, foeRow: true), Is.False, "a foe is there");
		Assert.That(CanPlay(s, "Gust", 2), Is.False, "your own row");
	}

	[Test]
	public void ACardThatDrawsCannotDrawItself()
	{
		var dash = Card("Dash", 0, new DashAction(), new DrawAction());
		var s = Battle([new(Mon("Pike"), 2)], [dash], Foe(2));

		s = Play(s, "Dash", 2);

		Assert.That(s.CardsIn(ZoneType.Hand), Is.Empty);
	}

	// ===== Catching — a Snare is an item, thrown at a weakened foe

	private static GameState WithSnares(GameState s, int snares) =>
		s.UpdateObject(s.GetParty().Id, s.GetParty() with { Snares = snares });

	private static bool CanSnare(GameState s, int space) =>
		new UseSnareAction { FoeId = FoeIn(s, space).Id }
			.ValidateAdd(s)
			.IsValid;

	private static GameState Snare(GameState s, int space) =>
		Do(s, new UseSnareAction { FoeId = FoeIn(s, space).Id });

	[Test]
	public void AFoeCanBeCaughtOnlyAtAThirdOfItsHpOrLess()
	{
		var s = WithSnares(Battle([new(Mon("Pike"), 0)], [], Foe(2, hp: 30), Foe(3)), 1);
		Assert.That(CanSnare(s, 2), Is.False, "30 of 30");

		s = s.UpdateObject(FoeIn(s, 2).Id, FoeIn(s, 2) with { Hp = 10 });
		Assert.That(CanSnare(s, 2), Is.True, "10 of 30");
	}

	[Test]
	public void ACaughtFoeLeavesTheBoardAndCostsASnareAndEnergy()
	{
		var s = WithSnares(Battle([new(Mon("Pike"), 2)], [], Foe(2, hp: 3), Foe(3)), 2);
		s = s.UpdateObject(FoeIn(s, 2).Id, FoeIn(s, 2) with { MaxHp = 30 });

		s = Snare(s, 2);

		Assert.That(s.LivingFoes().Select(f => f.Space), Is.EqualTo(new[] { 3 }));
		Assert.That(s.CaughtFoes().Single().Hp, Is.EqualTo(3), "caught at the HP it had");
		Assert.That(s.GetParty().Snares, Is.EqualTo(1));
		Assert.That(s.GetParty().Energy, Is.EqualTo(3 - UseSnareAction.Cost));
	}

	[Test]
	public void CatchingTheLastFoeWinsTheBattle()
	{
		var s = WithSnares(Battle([new(Mon("Pike"), 2)], [], Foe(2, hp: 3)), 1);
		s = s.UpdateObject(FoeIn(s, 2).Id, FoeIn(s, 2) with { MaxHp = 30 });

		s = Snare(s, 2);

		Assert.That(s.GetParty().IsOver && s.GetParty().Won, Is.True);
	}

	[Test]
	public void NoSnaresOrABossCannotBeCaught()
	{
		var s = Battle([new(Mon("Pike"), 2)], [], Foe(2, hp: 3), Foe(3, hp: 3));
		s = s.UpdateObject(FoeIn(s, 2).Id, FoeIn(s, 2) with { MaxHp = 30 });
		s = s.UpdateObject(FoeIn(s, 3).Id, FoeIn(s, 3) with { MaxHp = 30, Catchable = false });
		Assert.That(CanSnare(s, 2), Is.False, "no Snares");

		s = WithSnares(s, 1);
		Assert.That(CanSnare(s, 2), Is.True);
		Assert.That(CanSnare(s, 3), Is.False, "a boss");
	}

	[Test]
	public void ACaughtWispDriftsOnYourRow()
	{
		var drift = new Intent
		{
			Name = "Drift",
			Kind = IntentType.Move,
			Amount = -1,
		};
		var s = Battle([new(Mon("Wisp", moves: drift), 3), new(Mon("Pike"), 1)], [], Foe(0));

		s = EndTurn(s);
		Assert.That(Named(s, "Wisp").Space, Is.EqualTo(2));

		s = EndTurn(s);
		Assert.That(Named(s, "Wisp").Space, Is.EqualTo(2), "Pike is in the way");
	}

	// ===== The battle's end, and what the board reads

	[Test]
	public void TheForecastCountsBothSides()
	{
		var s = Battle([new(Mon("Pike", moves: Hit(4)), 2)], [], Foe(2, pattern: Hit(6)));

		var forecast = s.HpLostIfTurnEndsNow();

		Assert.That(forecast[Named(s, "Pike").Id], Is.EqualTo(6));
		Assert.That(forecast[FoeIn(s, 2).Id], Is.EqualTo(4));
	}

	[Test]
	public void KillingTheLastFoeWins()
	{
		var s = Battle([new(Mon("Pike", moves: Hit(99)), 2)], [], Foe(2));

		s = EndTurn(s);

		Assert.That(s.GetParty().IsOver && s.GetParty().Won, Is.True);
	}

	[Test]
	public void EveryMonsterKnockedOutLoses()
	{
		var s = Battle([new(Mon("Pike", hp: 5), 2)], [], Foe(2, pattern: Hit(9)));

		s = EndTurn(s);

		Assert.That(s.GetParty().IsOver, Is.True);
		Assert.That(s.GetParty().Won, Is.False);
	}

	[Test]
	public void OneMonsterDownIsNotALoss()
	{
		var s = Battle(
			[new(Mon("Pike", hp: 5), 2), new(Mon("Bramble"), 0)],
			[],
			Foe(2, pattern: Hit(9))
		);

		s = EndTurn(s);

		Assert.That(s.GetParty().IsOver, Is.False);
	}

	[Test]
	public void EveryScenarioBuildsAndDealsItsOpeningHand()
	{
		foreach (var scenario in PartyContent.Scenarios)
		{
			var s = PartyBattleFactory.Create(scenario, seed: 3);
			var hand = s.CardsIn(ZoneType.Hand).Select(c => c.Name).ToList();

			Assert.That(hand, Has.Count.EqualTo(StartPartyTurnAction.HandSize), scenario.Name);
			Assert.That(hand.Take(scenario.OpeningHand.Count), Is.EqualTo(scenario.OpeningHand));
			Assert.That(
				s.Allies().All(a => a.Pattern.Count > 0),
				Is.True,
				"every monster has moves"
			);
		}
	}
}
