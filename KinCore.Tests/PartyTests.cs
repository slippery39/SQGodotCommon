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

		var forecast = s.ForecastIfTurnEndsNow();

		Assert.That(forecast.Hp[Named(s, "Pike").Id], Is.EqualTo(6));
		Assert.That(forecast.Hp[FoeIn(s, 2).Id], Is.EqualTo(4));
	}

	// ===== The trainer's health — dodging is not free

	private static int TrainerHp(GameState s) => s.GetParty().TrainerHp;

	[Test]
	public void AnAttackThatLandsOnNoMonsterHitsTheTrainer()
	{
		var s = Battle([new(Mon("Pike"), 2)], [], Foe(2, pattern: Hit(9)));
		Assert.That(s.AimsAtTrainer(FoeIn(s, 2)), Is.False, "Pike is in the way");

		s = Step(s, "Pike", 3);
		Assert.That(s.AimsAtTrainer(FoeIn(s, 2)), Is.True, "the telegraph says so");
		Assert.That(s.ForecastIfTurnEndsNow().Trainer, Is.EqualTo(9));

		s = EndTurn(s);
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20));
		Assert.That(TrainerHp(s), Is.EqualTo(PartyScenario.DefaultTrainerHp - 9));
	}

	[Test]
	public void AWideAttackThatCatchesAMonsterDoesNotAlsoHitTheTrainer()
	{
		var s = Battle([new(Mon("Pike"), 1)], [], Foe(2, pattern: Hit(4, -1, 0, 1)));

		s = EndTurn(s);

		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(16));
		Assert.That(
			TrainerHp(s),
			Is.EqualTo(PartyScenario.DefaultTrainerHp),
			"once, and it landed"
		);
	}

	[Test]
	public void TheTrainerAtZeroLosesTheBattle()
	{
		var s = Battle(
			[new(Mon("Pike"), 0)],
			[],
			Foe(2, pattern: Hit(PartyScenario.DefaultTrainerHp))
		);

		s = EndTurn(s);

		Assert.That(s.GetParty().IsOver && !s.GetParty().Won, Is.True);
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20), "every monster still standing");
	}

	// ===== The gym leader — the mirror

	private static GameState Gym(GameState s, int leaderHp) =>
		s.UpdateObject(s.GetParty().Id, s.GetParty() with { LeaderHp = leaderHp });

	[Test]
	public void ASwingIntoAnEmptyColumnHitsTheLeaderInAGym()
	{
		var s = Gym(Battle([new(Mon("Pike", power: 1, moves: Hit(4)), 0)], [], Foe(2)), 30);
		Assert.That(s.AimsAtLeader(Named(s, "Pike")), Is.True);

		s = EndTurn(s);

		Assert.That(s.GetParty().LeaderHp, Is.EqualTo(30 - 5));
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50));
	}

	[Test]
	public void InTheWildASwingIntoAnEmptyColumnIsWasted()
	{
		var s = Battle([new(Mon("Pike", moves: Hit(4)), 0)], [], Foe(2));
		Assert.That(s.AimsAtLeader(Named(s, "Pike")), Is.False);

		s = EndTurn(s);

		Assert.That(s.GetParty().LeaderHp, Is.EqualTo(0));
		Assert.That(s.GetParty().IsOver, Is.False);
	}

	[Test]
	public void TheLeaderAtZeroWinsTheGym()
	{
		var s = Gym(Battle([new(Mon("Pike", moves: Hit(10)), 0)], [], Foe(2)), 10);

		s = EndTurn(s);

		Assert.That(s.GetParty().IsOver && s.GetParty().Won, Is.True);
		Assert.That(s.LivingFoes(), Is.Not.Empty, "its creatures still stand");
	}

	// ===== The bench, in battle

	[Test]
	public void ABenchedMonsterIsOffTheBoardUntilOneFaints()
	{
		var s = Battle(
			[new(Mon("Pike", hp: 5), 2), new(Mon("Boar", moves: Hit(3)), -1)],
			[],
			Foe(2, pattern: [Hit(9), Hit(1)])
		);
		Assert.That(s.LivingAllies().Select(a => a.Name), Is.EqualTo(new[] { "Pike" }));
		Assert.That(s.ActingOrder().Any(c => c.Name == "Boar"), Is.False, "the bench does not act");

		s = EndTurn(s);

		Assert.That(Named(s, "Pike").IsKnockedOut, Is.True);
		Assert.That(Named(s, "Boar").Space, Is.EqualTo(2), "it stepped into Pike's space");
		Assert.That(s.GetParty().IsOver, Is.False, "so the battle goes on");
		Assert.That(TrainerHp(s), Is.EqualTo(PartyScenario.DefaultTrainerHp));

		s = EndTurn(s);
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50 - 3), "and it fights from the next turn");
	}

	// ===== Monster decks

	private static IEnumerable<string> InPlay(GameState s) =>
		new[] { ZoneType.Draw, ZoneType.Hand, ZoneType.Discard }.SelectMany(z =>
			s.CardsIn(z).Select(c => c.Name)
		);

	[Test]
	public void AMonsterOnTheBoardBringsItsDeck()
	{
		var s = Battle(
			[new(Mon("Pike") with { Cards = [Card("Feint", 0, new DashAction())] }, 2)],
			[Card("Guard", 1, new GuardAction { Amount = 5 })],
			Foe(2)
		);

		Assert.That(InPlay(s), Is.EquivalentTo(new[] { "Guard", "Feint" }));
	}

	[Test]
	public void ABenchedMonstersDeckJoinsWhenItStepsIn()
	{
		var s = Battle(
			[
				new(Mon("Pike", hp: 5), 2),
				new(Mon("Boar") with { Cards = [Card("Tusk", 0, new DashAction())] }, -1),
			],
			[],
			Foe(2, pattern: Hit(9))
		);
		Assert.That(InPlay(s), Does.Not.Contain("Tusk"), "the bench's deck waits");

		s = EndTurn(s);

		Assert.That(Named(s, "Boar").Benched, Is.False);
		Assert.That(s.CardsIn(ZoneType.Hand).Select(c => c.Name), Does.Contain("Tusk"));
	}

	[Test]
	public void AFaintedMonstersCardsLeaveEveryZone()
	{
		var s = Battle(
			[
				new(Mon("Pike", hp: 5) with { Cards = [Card("Feint", 0, new DashAction())] }, 2),
				new(Mon("Bramble"), 0),
			],
			[Card("Guard", 1, new GuardAction { Amount = 5 })],
			Foe(2, pattern: Hit(9))
		);
		Assert.That(InPlay(s), Does.Contain("Feint"));

		s = EndTurn(s);

		Assert.That(Named(s, "Pike").IsKnockedOut, Is.True);
		Assert.That(InPlay(s), Is.EquivalentTo(new[] { "Guard" }), "no dead draws");
	}

	// ===== Monsters that change how you play: triggers

	[Test]
	public void AMonstersDrawTriggerFiresOncePerTurnAndNotFromTheBench()
	{
		var energyOnDraw = new Trigger
		{
			When = new OnCardsDrawn(),
			Effects = [new GainEnergyAction()],
			MaxPerTurn = 1,
		};
		var peek = Card("Peek", 0, new DrawAction());
		var s = PartyBattleFactory.Create(
			new PartyScenario(
				"Test",
				"",
				[
					new(Mon("Inkling") with { Abilities = [energyOnDraw] }, 2),
					new(Mon("Boar") with { Abilities = [energyOnDraw] }, -1),
				],
				[Foe(2)],
				[peek, peek, .. Enumerable.Repeat(Card("Guard", 1, new GuardAction()), 6)],
				["Peek", "Peek"]
			)
		);
		Assert.That(s.GetParty().Energy, Is.EqualTo(3), "the opening hand is not a draw");

		s = Play(s, "Peek", 2);
		Assert.That(
			s.GetParty().Energy,
			Is.EqualTo(4),
			"one from Inkling; the benched Boar is not active"
		);

		s = Play(s, "Peek", 2);
		Assert.That(s.GetParty().Energy, Is.EqualTo(4), "once a turn");
	}

	[Test]
	public void SiftDrawsThenDiscardsTheCardYouPickAndADiscardTriggerFires()
	{
		var energyOnDiscard = new Trigger
		{
			When = new OnCardDiscarded(),
			Effects = [new GainEnergyAction { Amount = 2 }],
		};
		var s = PartyBattleFactory.Create(
			new PartyScenario(
				"Test",
				"",
				[new(Mon("Magpie") with { Abilities = [energyOnDiscard] }, 2)],
				[Foe(2)],
				[
					Card("Sift", 0, PartyDiscard.DrawThenDiscard(2, 1)),
					.. Enumerable.Repeat(Card("Guard", 1, new GuardAction()), 6),
				],
				["Sift"]
			)
		);

		s = Play(s, "Sift", 2);

		var choice = s.GetPendingChoice();
		Assert.That(choice, Is.Not.Null, "it waits for the player");
		Assert.That(choice!.Options.Select(o => o.DisplayText), Does.Not.Contain("Sift"));
		Assert.That(
			choice.Options,
			Has.Count.EqualTo(4 + 2),
			"the rest of the hand, plus the two drawn"
		);

		var discarded = choice.Options[0].Id;
		s = s.ResolveChoice([discarded]).State;

		Assert.That(s.GetParent(discarded), Is.EqualTo(s.ZoneId(ZoneType.Discard)));
		Assert.That(s.CardsIn(ZoneType.Hand).Count(), Is.EqualTo(5));
		Assert.That(s.CardsIn(ZoneType.Discard).Select(c => c.Name), Does.Contain("Sift"));
		Assert.That(s.GetParty().Energy, Is.EqualTo(3 + 2), "the discard fired Magpie");
	}

	/// <summary>A battle whose deck is bigger than a hand, with the named cards dealt first.</summary>
	private static GameState Deal(PlacedCompanion[] companions, KinCard[] top, params Foe[] foes) =>
		PartyBattleFactory.Create(
			new PartyScenario(
				"Test",
				"",
				[.. companions],
				[.. foes],
				[.. top, .. Enumerable.Repeat(Card("Guard", 1, new GuardAction()), 6)],
				[.. top.Select(c => c.Name)]
			)
		);

	[Test]
	public void RummageDrawsAsManyAsYouDiscard()
	{
		var s = Deal(
			[new(Mon("Pike"), 2)],
			[Card("Rummage", 0, PartyDiscard.DiscardThenDraw())],
			Foe(2)
		);
		s = Play(s, "Rummage", 2);

		var two = s.GetPendingChoice()!.Options.Take(2).Select(o => o.Id).ToList();
		s = s.ResolveChoice([.. two]).State;

		Assert.That(two.All(id => s.GetParent(id) == s.ZoneId(ZoneType.Discard)), Is.True);
		Assert.That(
			s.CardsIn(ZoneType.Hand).Count(),
			Is.EqualTo(4),
			"four left, two gone, two drawn"
		);
		Assert.That(s.GetParty().DiscardedThisTurn, Is.EqualTo(2));
	}

	[Test]
	public void ATossFiresWhenACardDiscardsItButNotAtTheEndOfTheTurn()
	{
		var toss = Card("Flare", 5, new GuardAction()) with
		{
			Components =
			[
				new Trigger
				{
					When = new OnSelfDiscarded(),
					Effects = [new DamageRandomFoeAction { Amount = 5 }],
				},
			],
		};
		var sift = Card("Sift", 0, PartyDiscard.DrawThenDiscard(1, 1));

		var kept = Deal([new(Mon("Pike"), 2)], [toss], Foe(2));
		kept = EndTurn(kept);
		Assert.That(FoeIn(kept, 2).Hp, Is.EqualTo(50), "the end-of-turn discard is not discarding");

		var s = Deal([new(Mon("Pike"), 2)], [sift, toss], Foe(2));
		s = Play(s, "Sift", 2);
		s = s.ResolveChoice([InHand(s, "Flare").Id]).State;
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50 - 5));
	}

	[Test]
	public void ACostReductionReadsTheDiscardsThisTurn()
	{
		var hammer = Card("Hammer", 3, new StrikeAction { Amount = 6 }) with
		{
			Components = [new CostReduction { PerDiscardThisTurn = 1 }],
		};
		var s = Deal(
			[new(Mon("Pike"), 2)],
			[Card("Sift", 0, PartyDiscard.DrawThenDiscard(1, 1)), hammer],
			Foe(2)
		);
		Assert.That(s.CostOf(InHand(s, "Hammer")), Is.EqualTo(3));

		s = Play(s, "Sift", 2);
		s = s.ResolveChoice([InHand(s, "Guard").Id]).State;

		Assert.That(s.CostOf(InHand(s, "Hammer")), Is.EqualTo(2));
		s = Play(s, "Hammer", 2);
		Assert.That(s.GetParty().Energy, Is.EqualTo(3 - 2), "and it is paid at that cost");
	}

	[Test]
	public void PageStormHitsForPowerPlusTheCardsLeftInHand()
	{
		var s = Deal(
			[new(Mon("Pike", power: 2), 2)],
			[Card("Storm", 0, new StrikeAction { Amount = 0, PlusCardsInHand = true })],
			Foe(2)
		);

		s = Play(s, "Storm", 2);

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50 - (2 + 4)), "Power 2, four cards left in hand");
	}

	[Test]
	public void AThiefStealsTheTopCardAndGivesItBackWhenBeaten()
	{
		var thief = Foe(2, hp: 10, pattern: Hit(1) with { Steals = true });
		var s = Deal([new(Mon("Pike", hp: 30), 2)], [], thief);
		var top = s.CardsIn(ZoneType.Draw).First().Id;

		s = EndTurn(s);
		Assert.That(s.GetParent(top), Is.EqualTo(FoeIn(s, 2).Id), "held by the thief");

		s = Do(s, new StrikeAction { Amount = 99, Space = 2 });
		Assert.That(s.GetParent(top), Is.EqualTo(s.ZoneId(ZoneType.Discard)));
	}

	[Test]
	public void AWildTraitFiresForTheFoeWhileItStands()
	{
		var hoard = Foe(2) with
		{
			Components =
			[
				new Trigger
				{
					When = new OnDrawOrDiscard(),
					Effects = [new GainBlockAction { Amount = 2 }],
				},
			],
		};
		var s = Deal([new(Mon("Pike"), 2)], [Card("Peek", 0, new DrawAction())], hoard);

		s = Play(s, "Peek", 2);

		Assert.That(FoeIn(s, 2).Block, Is.EqualTo(2));
	}

	[Test]
	public void ACaughtCreatureWakesItsAbilityAndLeavesItsThieveryWild()
	{
		var discardEnergy = new Trigger
		{
			When = new OnCardDiscarded(),
			Effects = [new GainEnergyAction()],
		};
		var wild = Foe(0, pattern: Hit(3) with { Steals = true }) with
		{
			CaughtPassive = "TEST",
			CaughtAbilities = [discardEnergy],
			CaughtCards = [Card("Gift", 0, new DashAction())],
		};

		var mine = PartyRun.FromFoe(wild);

		Assert.That(mine.Passive, Is.EqualTo(wild.CaughtPassive));
		Assert.That(mine.Abilities, Is.EqualTo(wild.CaughtAbilities));
		Assert.That(mine.Cards, Is.EqualTo(wild.CaughtCards));
		Assert.That(mine.Moves.Any(m => m.Steals), Is.False);
	}

	// ===== Spellcraft

	private static KinCard Zap(int amount = 4) =>
		Card("Zap", 1, new SpellDamageAction { Amount = amount });

	[Test]
	public void ASpellNeedsNoAimButMustBeDroppedOnAFoe()
	{
		var s = Deal([new(Mon("Pike"), 0)], [Zap()], Foe(4));

		Assert.That(CanPlay(s, "Zap", 0), Is.False, "not on your own row");
		s = Play(s, "Zap", 4, foeRow: true);

		Assert.That(FoeIn(s, 4).Hp, Is.EqualTo(50 - 4), "nobody stands in front of it");
	}

	[Test]
	public void SpellPowerCountsOnlyWhileTheMonsterIsOnTheBoard()
	{
		var ember = Mon("Ember") with { Abilities = [new SpellPower { Amount = 2 }] };
		var benched = Deal([new(Mon("Pike"), 0), new(ember, -1)], [Zap()], Foe(4));
		var fighting = Deal([new(Mon("Pike"), 0), new(ember, 2)], [Zap()], Foe(4));

		Assert.That(FoeIn(Play(benched, "Zap", 4, true), 4).Hp, Is.EqualTo(50 - 4));
		Assert.That(FoeIn(Play(fighting, "Zap", 4, true), 4).Hp, Is.EqualTo(50 - 6));
	}

	[Test]
	public void AWardHalvesTheSpellAfterTheBonus()
	{
		var ember = Mon("Ember") with { Abilities = [new SpellPower { Amount = 2 }] };
		var s = Deal([new(ember, 0)], [Zap()], Foe(4) with { Components = [new SpellWard()] });

		s = Play(s, "Zap", 4, foeRow: true);

		Assert.That(FoeIn(s, 4).Hp, Is.EqualTo(50 - (4 + 2) / 2));
	}

	[Test]
	public void OverloadDealsTheSpellDamageAlreadyDealtThisTurn()
	{
		var overload = Card(
			"Overload",
			0,
			new SpellDamageAction { FromSpellDamageThisTurn = true }
		);
		var s = Deal([new(Mon("Pike"), 0)], [Zap(), Zap(), overload], Foe(3), Foe(4));

		s = Play(s, "Zap", 3, foeRow: true);
		s = Play(s, "Zap", 3, foeRow: true);
		s = Play(s, "Overload", 4, foeRow: true);

		Assert.That(FoeIn(s, 4).Hp, Is.EqualTo(50 - 8));
	}

	[Test]
	public void FocusMakesThisTurnsSpellsHitBesideTheTarget()
	{
		var focus = Card("Focus", 0, new SplashSpellsAction());
		var s = Deal([new(Mon("Pike"), 0)], [focus, Zap()], Foe(1), Foe(2), Foe(3), Foe(4));

		s = Play(s, "Focus", 0);
		s = Play(s, "Zap", 2, foeRow: true);

		Assert.That(new[] { 1, 2, 3 }.Select(c => FoeIn(s, c).Hp), Is.All.EqualTo(46));
		Assert.That(FoeIn(s, 4).Hp, Is.EqualTo(50));
	}

	[Test]
	public void AnEchoRepeatsTheLastSpellWhereItWasDroppedAndAWildOneDoesNothing()
	{
		var echo = new Intent { Name = "Echo", Kind = IntentType.Echo };
		var s = Deal([new(Mon("Owl", moves: echo), 0)], [Zap()], Foe(3), Foe(4, pattern: echo));

		s = Play(s, "Zap", 3, foeRow: true);
		s = EndTurn(s);

		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(50 - 4 - 4), "cast, then echoed");
		Assert.That(FoeIn(s, 4).Hp, Is.EqualTo(50));
		Assert.That(s.GetParty().LastSpell, Is.Null, "the next turn has cast nothing");
	}

	[Test]
	public void PlayingASpellFiresASpellTrigger()
	{
		var guard = Mon("Warden") with
		{
			Abilities =
			[
				new Trigger
				{
					When = new OnSpellPlayed(),
					Effects = [new GainBlockAction { Amount = 3 }],
				},
			],
		};
		var s = Deal(
			[new(guard, 0)],
			[Zap(), Card("Rally", 0, new PowerAction { Amount = 1 })],
			Foe(4)
		);

		s = Play(s, "Rally", 0);
		Assert.That(Named(s, "Warden").Block, Is.EqualTo(0), "not a spell");
		s = Play(s, "Zap", 4, foeRow: true);
		Assert.That(Named(s, "Warden").Block, Is.EqualTo(3));
	}

	// ===== Surge

	[Test]
	public void BorrowedEnergyComesOffNextTurn()
	{
		var surge = Card(
			"Surge",
			0,
			new GainEnergyAction { Amount = 2 },
			new BorrowEnergyAction { Amount = 1 }
		);
		var s = Deal([new(Mon("Pike"), 0)], [surge], Foe(4));

		s = Play(s, "Surge", 0);
		Assert.That(s.GetParty().Energy, Is.EqualTo(3 + 2));

		s = EndTurn(s);
		Assert.That(s.GetParty().Energy, Is.EqualTo(3 - 1));
		Assert.That(EndTurn(s).GetParty().Energy, Is.EqualTo(3), "paid back once");
	}

	[Test]
	public void QuickenMakesOnlyTheNextCardFree()
	{
		var s = Deal(
			[new(Mon("Pike"), 0)],
			[Card("Quicken", 1, new NextCardFreeAction()), Zap(), Zap()],
			Foe(4)
		);

		s = Play(s, "Quicken", 0);
		Assert.That(s.CostOf(InHand(s, "Zap")), Is.EqualTo(0));
		s = Play(s, "Zap", 4, foeRow: true);

		Assert.That(s.CostOf(InHand(s, "Zap")), Is.EqualTo(1));
		Assert.That(s.GetParty().Energy, Is.EqualTo(3 - 1));
	}

	[Test]
	public void TheFirstCardEachTurnPaysTaxesAfterDiscountsFlooredAtZero()
	{
		var hush = Foe(4) with { Components = [new FirstCardCost { Amount = 1 }] };
		var free = Card("Free", 0, new GuardAction());
		var s = Deal([new(Mon("Pike"), 0)], [free, free], hush);

		Assert.That(s.CostOf(InHand(s, "Free")), Is.EqualTo(1), "taxed");
		s = Play(s, "Free", 0);
		Assert.That(s.CostOf(InHand(s, "Free")), Is.EqualTo(0), "only the first card");

		var discount = Mon("Moth") with { Abilities = [new FirstCardCost { Amount = -1 }] };
		var both = Deal([new(discount, 0)], [free], hush);
		Assert.That(both.CostOf(InHand(both, "Free")), Is.EqualTo(1), "0 − 1 floors at 0, then +1");
	}

	[Test]
	public void AKillDuringYourTurnPaysTheStormbuckAndAKillAtTheEndDoesNot()
	{
		var storm = Mon("Buck", moves: Hit(99)) with
		{
			Abilities =
			[
				new Trigger
				{
					When = new OnFoeDefeatedDuringYourTurn(),
					Effects = [new GainEnergyAction()],
				},
			],
		};
		var battleCry = Card("Cry", 0, new GainEnergyIfFoeDiedAction { Amount = 2 });

		var now = Deal([new(storm, 0)], [Zap(99), battleCry], Foe(4, hp: 5), Foe(3));
		now = Play(now, "Zap", 4, foeRow: true);
		Assert.That(now.GetParty().Energy, Is.EqualTo(3 - 1 + 1), "the Stormbuck");
		now = Play(now, "Cry", 0);
		Assert.That(
			now.GetParty().Energy,
			Is.EqualTo(3 - 1 + 1 + 2),
			"and Battle Cry sees the kill"
		);

		var later = Deal([new(storm, 0)], [], Foe(0, hp: 5), Foe(3));
		later = EndTurn(later);
		Assert.That(FoeIn(later, 0).IsDead, Is.True);
		Assert.That(later.GetParty().Energy, Is.EqualTo(3), "no energy from the end of the turn");
	}

	[Test]
	public void AnUnhitGlowmothBringsEnergyNextTurn()
	{
		var moth = Mon("Moth") with { Abilities = [new EnergyIfUnhit { Amount = 1 }] };

		var safe = EndTurn(Deal([new(moth, 0)], [], Foe(4, pattern: Hit(1))));
		Assert.That(safe.GetParty().Energy, Is.EqualTo(3 + 1));

		var struck = EndTurn(Deal([new(moth, 4)], [], Foe(4, pattern: Hit(1))));
		Assert.That(struck.GetParty().Energy, Is.EqualTo(3));
	}

	[Test]
	public void AnXCardSpendsAllYourEnergyAndCountsIt()
	{
		var unleash = Card("Unleash", 0, new StrikeAction { PerX = 4 }) with
		{
			Components = [new SpendsAllEnergy()],
		};
		var s = Deal([new(Mon("Pike"), 2)], [unleash], Foe(2));

		s = Play(s, "Unleash", 2);

		Assert.That(s.GetParty().Energy, Is.EqualTo(0));
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50 - 4 * 3));
	}

	// ===== Summon — tokens

	private static TokenTemplate Token(int hp = 3, int fades = 2, params Intent[] moves) =>
		new(Mon("Tok", hp: hp, moves: moves), fades);

	private static KinCard Summon(TokenTemplate token, int count = 1) =>
		Card("Summon", 0, new SummonTokenAction { Token = token, Count = count });

	private static Ally TokenAt(GameState s, int space) => s.AllyAt(space)!;

	[Test]
	public void ATokenLandsWhereDroppedActsAtTheEndOfTheTurnAndFades()
	{
		var s = Deal([new(Mon("Pike"), 0)], [Summon(Token(fades: 1, moves: Hit(2)))], Foe(3));

		Assert.That(CanPlay(s, "Summon", 0), Is.False, "not on a monster");
		s = Play(s, "Summon", 3);
		Assert.That(TokenAt(s, 3).FadesIn, Is.EqualTo(1));

		s = EndTurn(s);
		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(50 - 2), "it acted");
		Assert.That(s.AllyAt(3), Is.Null, "and faded at the start of the next turn");
	}

	[Test]
	public void TokensNeverKeepABattleAlive()
	{
		var s = Deal(
			[new(Mon("Pike", hp: 5), 0)],
			[Summon(Token(hp: 30))],
			Foe(0, pattern: Hit(9))
		);
		s = Play(s, "Summon", 3);

		s = EndTurn(s);

		Assert.That(s.GetParty().IsOver && !s.GetParty().Won, Is.True);
	}

	[Test]
	public void AFaintedTokenBringsNoBenchButShieldsItsNeighbours()
	{
		var sprout = new TokenTemplate(
			Mon("Sprout", hp: 3) with
			{
				Abilities = [new FaintShield { Amount = 3 }],
			},
			2
		);
		// A fast foe fells the sprout; a slow one then swings at Pike, into the shield.
		var s = Deal(
			[new(Mon("Pike"), 0), new(Mon("Boar"), -1)],
			[Summon(sprout)],
			Foe(1, speed: 3, pattern: Hit(9)),
			Foe(0, speed: 1, pattern: Hit(3))
		);
		s = Play(s, "Summon", 1);

		s = EndTurn(s);

		Assert.That(s.AllyAt(1), Is.Null, "the sprout fell");
		Assert.That(Named(s, "Boar").Benched, Is.True, "a token is not replaced from the bench");
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20), "the slower blow hit the shield");
	}

	[Test]
	public void ADecoyDrawsEveryHomingAttack()
	{
		var decoy = new TokenTemplate(Mon("Decoy", hp: 30) with { Abilities = [new Lure()] }, 1);
		var homing = new Intent
		{
			Name = "Zap",
			Kind = IntentType.Attack,
			Amount = 4,
			Homing = true,
		};
		var s = Deal([new(Mon("Pike", hp: 5), 0)], [Summon(decoy)], Foe(4, pattern: homing));
		s = Play(s, "Summon", 3);

		s = EndTurn(s);

		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(5), "the weakest was spared");
	}

	[Test]
	public void TrampleCarriesTheRestOfTheBlowToYou()
	{
		var horn = Foe(3, pattern: Hit(8)) with { Components = [new Trample()] };
		var s = Deal([new(Mon("Pike"), 0)], [Summon(Token(hp: 3))], horn);
		s = Play(s, "Summon", 3);

		s = EndTurn(s);

		Assert.That(TrainerHp(s), Is.EqualTo(PartyScenario.DefaultTrainerHp - (8 - 3)));
	}

	[Test]
	public void AWildBroodFillsTheFoeRowAndCannotBeCaught()
	{
		var brood = new Intent
		{
			Name = "Brood",
			Kind = IntentType.Summon,
			Summons = Token(hp: 2, fades: 2),
		};
		var s = Deal([new(Mon("Pike", hp: 30), 0)], [], Foe(2, pattern: brood));

		s = EndTurn(s);

		var grub = s.LivingFoes().Single(f => f.FadesIn > 0);
		Assert.That(Math.Abs(grub.Space - 2), Is.EqualTo(1), "beside it");
		Assert.That(grub.Catchable, Is.False);
	}

	[Test]
	public void SwarmAttacksWithEveryTokenAndABoostArrivesWithThem()
	{
		var howler = Mon("Howler") with { Abilities = [new TokenBoost { Hp = 2, Power = 1 }] };
		var s = Deal(
			[new(howler, 0)],
			[Summon(Token(), count: 2), Card("Swarm", 0, new TokensAttackAction { Amount = 2 })],
			Foe(2),
			Foe(3)
		);

		s = Play(s, "Summon", 3);
		Assert.That(TokenAt(s, 3).Hp, Is.EqualTo(3 + 2));
		s = Play(s, "Swarm", 0);

		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(50 - (2 + 1)));
		Assert.That(
			FoeIn(s, 2).Hp,
			Is.EqualTo(50 - (2 + 1)),
			"the second: nearest empty, left on a tie"
		);
	}

	[Test]
	public void AnOfferingSpendsATokenForCardsAndEnergy()
	{
		var offering = Card(
			"Offering",
			0,
			new SacrificeTokenAction(),
			new DrawAction { Count = 2 },
			new GainEnergyAction()
		);
		var s = Deal([new(Mon("Pike"), 0)], [Summon(Token()), offering], Foe(4));
		s = Play(s, "Summon", 3);

		Assert.That(CanPlay(s, "Offering", 0), Is.False, "only on a token");
		s = Play(s, "Offering", 3);

		Assert.That(s.AllyAt(3), Is.Null);
		Assert.That(s.GetParty().Energy, Is.EqualTo(3 + 1));
		Assert.That(s.CardsIn(ZoneType.Hand).Count(), Is.EqualTo(3 + 2));
	}

	[Test]
	public void TheBattleIsLostOnlyWhenTheBenchIsGoneToo()
	{
		var s = Battle(
			[new(Mon("Pike", hp: 5), 2), new(Mon("Boar", hp: 5), -1)],
			[],
			Foe(2, pattern: Hit(9))
		);

		s = EndTurn(s);
		Assert.That(s.GetParty().IsOver, Is.False);

		s = EndTurn(s);
		Assert.That(s.GetParty().IsOver && !s.GetParty().Won, Is.True);
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
