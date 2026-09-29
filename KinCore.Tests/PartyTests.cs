using System.Collections.Immutable;
using ImmutableGameObjects;
using KinCore;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **THE RELAY — every rule FIRES** (`KinRelayPlan.md`). Exploring, not tuning: these read a number
/// that moved, never a field that was set, and every monster, card and foe is inline.
/// </summary>
public partial class PartyTests
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

	private static Intent Hit(int amount, Aim aim = Aim.Front) =>
		new()
		{
			Name = "Hit",
			Kind = IntentType.Attack,
			Amount = amount,
			Target = aim,
		};

	private static PartyCompanion Mon(
		string name,
		int hp = 20,
		int power = 0,
		params Intent[] moves
	) => new(name, hp, power, moves.Length > 0 ? [.. moves] : [Idle]);

	private static Foe Foe(int position, int hp = 50, params Intent[] pattern) =>
		new()
		{
			Name = "Foe",
			Hp = hp,
			MaxHp = hp,
			Position = position,
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

	private static GameState Do(GameState s, GameAction action) =>
		s.AddAction(action).ProcessAllActions().State;

	private static GameState Play(GameState s, string name, int position, bool foeRow = false) =>
		Do(
			s,
			new PlayPartyCardAction
			{
				CardId = InHand(s, name).Id,
				Space = position,
				FoeRow = foeRow,
			}
		);

	private static bool CanPlay(GameState s, string name, int position, bool foeRow = false) =>
		new PlayPartyCardAction
		{
			CardId = InHand(s, name).Id,
			Space = position,
			FoeRow = foeRow,
		}
			.ValidateAdd(s)
			.IsValid;

	private static GameState EndTurn(GameState s) => Do(s, new EndPartyTurnAction());

	private static KinCard InHand(GameState s, string name) =>
		s.CardsIn(ZoneType.Hand).First(c => c.Name == name);

	private static Ally Named(GameState s, string name) => s.Allies().Single(a => a.Name == name);

	/// <summary>The foe standing at that place in their line.</summary>
	private static Foe FoeIn(GameState s, int position) =>
		s.LivingFoes().Single(f => f.Position == position);

	private static string Line(GameState s) =>
		string.Join(",", s.LivingAllies().Select(a => a.Name));

	// ===== THE RELAY: two lines, the front holds, steps back to front

	[Test]
	public void TheFrontTakesTheBlowAndEveryMonsterActs()
	{
		var s = Battle(
			[new(Mon("Bramble"), 0), new(Mon("Pike", moves: Hit(2)), 1)],
			[],
			Foe(0, pattern: Hit(5)),
			Foe(1)
		);

		s = EndTurn(s);

		Assert.That(Named(s, "Bramble").Hp, Is.EqualTo(20 - 5), "the front took it");
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20));
		Assert.That(
			FoeIn(s, 0).Hp,
			Is.EqualTo(50 - 2),
			"Pike, second in line, still hit their front"
		);
	}

	[Test]
	public void TheLinesActInStepsFromTheBackBothSidesAtOnce()
	{
		var s = Battle([new(Mon("A"), 0), new(Mon("B"), 1), new(Mon("C"), 2)], [], Foe(0), Foe(1));

		var steps = s.ActingSteps()
			.Select(step =>
				string.Join(" ", step.Select(c => (c is Ally ? "A" : "F") + c.Position))
			);

		Assert.That(string.Join(" | ", steps), Is.EqualTo("A2 | A1 F1 | A0 F0"));
	}

	[Test]
	public void AStepIsSimultaneousSoTheFrontsTrade()
	{
		var s = Battle(
			[new(Mon("Pike", hp: 5, moves: Hit(99)), 0), new(Mon("Bramble"), 1)],
			[],
			Foe(0, hp: 5, pattern: Hit(99)),
			Foe(1)
		);

		s = EndTurn(s);

		Assert.That(Named(s, "Pike").IsKnockedOut, Is.True);
		Assert.That(s.LivingFoes().Count(), Is.EqualTo(1), "both fronts fell in the same step");
	}

	[Test]
	public void ABackLinerActsFirstAndCanDropYourFrontBeforeItSwings()
	{
		var s = Battle(
			[new(Mon("Pike", hp: 5, moves: Hit(10)), 0), new(Mon("Bramble", hp: 30), 1)],
			[],
			Foe(0),
			Foe(1, pattern: Hit(99))
		);

		s = EndTurn(s);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50), "Pike never swung");
		Assert.That(Named(s, "Bramble").Position, Is.EqualTo(0), "and the line closed up");
	}

	[Test]
	public void ALaterBlowFindsTheNewFront()
	{
		var s = Battle(
			[new(Mon("Pike", hp: 5), 0), new(Mon("Bramble", hp: 30), 1)],
			[],
			Foe(0, pattern: Hit(4)),
			Foe(1, pattern: Hit(99))
		);

		s = EndTurn(s);

		Assert.That(Named(s, "Bramble").Hp, Is.EqualTo(30 - 4));
	}

	[Test]
	public void EachAimLandsWhereItSays()
	{
		GameState Aimed(Aim aim) =>
			EndTurn(
				Battle(
					[new(Mon("A"), 0), new(Mon("B"), 1), new(Mon("C", hp: 10), 2)],
					[],
					Foe(0, pattern: Hit(3, aim))
				)
			);
		string Hurt(GameState s) =>
			string.Join(",", s.Allies().Where(a => a.Hp < a.MaxHp).Select(a => a.Name));

		Assert.That(Hurt(Aimed(Aim.Front)), Is.EqualTo("A"));
		Assert.That(Hurt(Aimed(Aim.Back)), Is.EqualTo("C"));
		Assert.That(Hurt(Aimed(Aim.Pierce)), Is.EqualTo("A,B"));
		Assert.That(Hurt(Aimed(Aim.Sweep)), Is.EqualTo("A,B,C"));
		Assert.That(Hurt(Aimed(Aim.Hunt)), Is.EqualTo("C"), "the lowest HP");
	}

	[Test]
	public void TheFinisherCashesEveryoneWhoActedBeforeIt()
	{
		var pike = Mon("Pike", moves: Hit(2)) with { FinisherPerAlly = 2 };
		var s = Battle([new(pike, 0), new(Mon("B"), 1), new(Mon("C"), 2)], [], Foe(0));

		s = EndTurn(s);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - (2 + 2 * 2)));
	}

	[Test]
	public void AnAheadBlockShieldsTheOneAhead()
	{
		var shield = new Intent
		{
			Name = "Cover",
			Kind = IntentType.Block,
			Amount = 5,
			Target = Aim.Ahead,
		};
		var s = Battle(
			[new(Mon("Front"), 0), new(Mon("Back", moves: shield), 1)],
			[],
			Foe(0, pattern: Hit(5))
		);

		s = EndTurn(s);

		Assert.That(Named(s, "Front").Hp, Is.EqualTo(20));
	}

	[Test]
	public void BracesLandBeforeBlowsInTheSameStepOnBothSides()
	{
		var brace = new Intent
		{
			Name = "Brace",
			Kind = IntentType.Block,
			Amount = 6,
		};
		var s = Battle([new(Mon("Pike", moves: Hit(4)), 0)], [], Foe(0, pattern: [brace, Idle]));

		s = EndTurn(s);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50), "its brace met Pike's blow");
		Assert.That(FoeIn(s, 0).Block, Is.EqualTo(2), "and held into your turn");

		s = EndTurn(s);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(46), "it dropped when the foe next acted");
	}

	[Test]
	public void SwapRallyAndRetreatReorderYourLine()
	{
		var s = Battle(
			[new(Mon("A"), 0), new(Mon("B"), 1), new(Mon("C"), 2)],
			[
				Card("Swap", 0, new SwapAction()),
				Card("Rally", 0, new RallyAction()),
				Card("Retreat", 0, new RetreatAction()),
			],
			Foe(0)
		);
		Assert.That(CanPlay(s, "Swap", 0), Is.False, "nobody is ahead of the front");

		s = Play(s, "Swap", 2);
		Assert.That(Line(s), Is.EqualTo("A,C,B"));
		s = Play(s, "Rally", 2);
		Assert.That(Line(s), Is.EqualTo("B,A,C"));
		s = Play(s, "Retreat", 0);
		Assert.That(Line(s), Is.EqualTo("A,C,B"));
	}

	[Test]
	public void GustSwapsTheirFrontTwoAndGaleLeavesThemOffBalance()
	{
		var gale = Mon("Gale") with { Unbalances = 2 };
		var s = Battle(
			[new(Mon("Pike", moves: Hit(3)), 0), new(gale, 1)],
			[Card("Gust", 1, new GustAction())],
			Foe(0) with
			{
				Name = "Ahead",
			},
			Foe(1) with
			{
				Name = "Behind",
			}
		);
		Assert.That(CanPlay(s, "Gust", 0), Is.False, "their line, not yours");

		s = EndTurn(Play(s, "Gust", 0, foeRow: true));

		Assert.That(FoeIn(s, 0).Name, Is.EqualTo("Behind"));
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - (3 + 2)));
	}

	[Test]
	public void AFoesShoveSwapsYourFrontTwo()
	{
		var shove = new Intent { Name = "Shove", Kind = IntentType.Shove };
		var s = Battle([new(Mon("A"), 0), new(Mon("B"), 1)], [], Foe(0, pattern: shove));

		s = EndTurn(s);

		Assert.That(Line(s), Is.EqualTo("B,A"));
	}

	[Test]
	public void AMoveIntentMovesItThroughItsLine()
	{
		var drift = new Intent
		{
			Name = "Drift",
			Kind = IntentType.Move,
			Amount = 1,
		};
		var s = Battle(
			[new(Mon("Pike"), 0)],
			[],
			Foe(0, pattern: drift) with
			{
				Name = "Drifter",
			},
			Foe(1)
		);

		s = EndTurn(s);

		Assert.That(FoeIn(s, 1).Name, Is.EqualTo("Drifter"));
	}

	[Test]
	public void ATokenArrivingMidRoundDoesNotMakeAnyoneSkipTheirStep()
	{
		var grub = new TokenTemplate(Mon("Grub", moves: Hit(1)), 2);
		var brood = new Intent
		{
			Name = "Brood",
			Kind = IntentType.Summon,
			Summons = grub,
		};
		var s = Battle(
			[new(Mon("Pike", moves: Hit(2)), 0), new(Mon("Vine", moves: brood), 1)],
			[],
			Foe(0)
		);

		s = EndTurn(s);

		Assert.That(Line(s), Is.EqualTo("Grub,Pike,Vine"), "the grub arrived at the front");
		Assert.That(
			FoeIn(s, 0).Hp,
			Is.EqualTo(50 - 2),
			"Pike was pushed back and still acted; the grub waits"
		);
	}

	[Test]
	public void TheForecastMatchesTheRound()
	{
		var s = Battle([new(Mon("Pike", moves: Hit(4)), 0)], [], Foe(0, pattern: Hit(6)));

		var forecast = s.ForecastIfTurnEndsNow();

		Assert.That(forecast.Hp[Named(s, "Pike").Id], Is.EqualTo(6));
		Assert.That(forecast.Hp[FoeIn(s, 0).Id], Is.EqualTo(4));
	}

	// ===== Deploy (R2): order your line before the fight

	[Test]
	public void ADeployingBattleWaitsForFightAndOnlyThenKeepsItsOrder()
	{
		var s = PartyBattleFactory.Create(
			new PartyScenario(
				"Test",
				"",
				[new(Mon("A"), 0), new(Mon("B"), 1), new(Mon("C"), 2)],
				[Foe(0, pattern: Hit(4))],
				[Card("Guard", 1, new GuardAction())],
				[],
				Deploy: true
			)
		);
		Assert.That(CanPlay(s, "Guard", 0), Is.False, "cards wait");
		Assert.That(new EndPartyTurnAction().ValidateAdd(s).IsValid, Is.False, "so does the turn");

		s = Do(s, new DeployMoveAction { AllyId = Named(s, "C").Id, To = 0 });
		s = Do(s, new DeployMoveAction { AllyId = Named(s, "A").Id, To = 2 });
		Assert.That(Line(s), Is.EqualTo("C,B,A"), "any monster to any place, as often as you like");

		Assert.That(
			s.ForecastIfTurnEndsNow().Hp[Named(s, "C").Id],
			Is.EqualTo(4),
			"the forecast plays the round in the order being set: C is the front now"
		);

		s = Do(s, new BeginFightAction());
		Assert.That(CanPlay(s, "Guard", 0), Is.True);
		Assert.That(
			new DeployMoveAction { AllyId = Named(s, "A").Id, To = 0 }
				.ValidateAdd(s)
				.IsValid,
			Is.False,
			"after FIGHT only cards reorder"
		);
		Assert.That(
			s.GetParty().DeployedOrder,
			Is.EqualTo(new[] { Named(s, "C").Slot, Named(s, "B").Slot, Named(s, "A").Slot })
		);
	}

	// ===== Cycles and passives

	[Test]
	public void TheCycleAdvancesEveryTurn()
	{
		var s = Battle([new(Mon("Pike", moves: [Hit(1), Hit(10)]), 0)], [], Foe(0));

		s = EndTurn(s);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(49));
		Assert.That(
			Named(s, "Pike").Current.Amount,
			Is.EqualTo(10),
			"the next move is telegraphed"
		);

		s = EndTurn(s);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(39));
	}

	[Test]
	public void ThornsHurtWhateverAttacksHerEvenThroughBlock()
	{
		var bramble = Mon("Bramble") with { Thorns = 2 };
		var s = Battle(
			[new(bramble, 0)],
			[Card("Guard", 1, new GuardAction { Amount = 10 })],
			Foe(0, pattern: Hit(5))
		);

		s = EndTurn(Play(s, "Guard", 0));

		Assert.That(Named(s, "Bramble").Hp, Is.EqualTo(20));
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(48));
	}

	[Test]
	public void ThornsCanWinTheBattleOnTheirOwn()
	{
		var bramble = Mon("Bramble") with { Thorns = 5 };
		var s = Battle(
			[new(bramble, 0)],
			[],
			Foe(0, hp: 5, pattern: Hit(1)),
			Foe(1, hp: 5, pattern: Hit(1))
		);

		s = EndTurn(s);

		Assert.That(s.GetParty().IsOver && s.GetParty().Won, Is.True);
	}

	// ===== Trainer cards — played ON something, owned by nobody

	[Test]
	public void GuardBlocksForTheMonsterItIsDroppedOnAndIsGoneNextTurn()
	{
		var s = Battle(
			[new(Mon("Pike"), 0), new(Mon("Bramble"), 1)],
			[Card("Guard", 1, new GuardAction { Amount = 6 })],
			Foe(0, pattern: Hit(9))
		);

		s = Play(s, "Guard", 0);
		Assert.That(Named(s, "Bramble").Block, Is.EqualTo(0));

		s = EndTurn(s);
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(17));
		Assert.That(Named(s, "Pike").Block, Is.EqualTo(0));
	}

	[Test]
	public void ACardForYourMonstersMustBeDroppedOnOne()
	{
		var s = Battle(
			[new(Mon("Pike"), 0)],
			[Card("Guard", 1, new GuardAction { Amount = 6 })],
			Foe(0)
		);

		Assert.That(CanPlay(s, "Guard", 0), Is.True);
		Assert.That(CanPlay(s, "Guard", 1), Is.False, "nobody stands there");
		Assert.That(CanPlay(s, "Guard", 0, foeRow: true), Is.False, "a foe");
	}

	[Test]
	public void RallyAddsPowerToThisTurnsAttackOnly()
	{
		var s = Battle(
			[new(Mon("Pike", moves: Hit(2)), 0)],
			[Card("Rally", 1, new PowerAction { Amount = 3 })],
			Foe(0)
		);

		s = EndTurn(Play(s, "Rally", 0));
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(45));

		s = EndTurn(s);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(43));
	}

	[Test]
	public void HastenPlaysTheMoveNowAndCountsForTheRelay()
	{
		var pike = Mon("Pike", moves: Hit(2)) with { FinisherPerAlly = 2 };
		var s = Battle(
			[new(pike, 0), new(Mon("Wisp", moves: [Hit(1), Hit(9)]), 1)],
			[Card("Hasten", 1, new HastenAction())],
			Foe(0)
		);

		s = Play(s, "Hasten", 1);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(49), "at once");

		s = EndTurn(s);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(49 - (2 + 2)), "not again — and Pike counted it");
	}

	[Test]
	public void StrikeAttacksTheirFrontNowOnTopOfTheMove()
	{
		var s = Battle(
			[new(Mon("Pike", power: 2, moves: Hit(1)), 0)],
			[Card("Strike", 1, new StrikeAction { Amount = 3 })],
			Foe(0)
		);

		s = Play(s, "Strike", 0);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(45));

		s = EndTurn(s);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(42));
	}

	[Test]
	public void StaggerCostsAFoeItsNextMove()
	{
		var s = Battle(
			[new(Mon("Pike"), 0)],
			[Card("Stagger", 1, new StaggerAction())],
			Foe(0, pattern: [Hit(9), Hit(1)])
		);

		Assert.That(CanPlay(s, "Stagger", 0), Is.False, "it is dropped on a foe");
		s = EndTurn(Play(s, "Stagger", 0, foeRow: true));
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20));

		s = EndTurn(s);
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(19), "the cycle moved on");
	}

	[Test]
	public void ACardThatDrawsCannotDrawItself()
	{
		var s = Battle([new(Mon("Pike"), 0)], [Card("Peek", 0, new DrawAction())], Foe(0));

		s = Play(s, "Peek", 0);

		Assert.That(s.CardsIn(ZoneType.Hand), Is.Empty);
	}

	// ===== The bench and monster decks

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
		var s = Deal(
			[
				new(Mon("Inkling") with { Abilities = [energyOnDraw] }, 0),
				new(Mon("Boar") with { Abilities = [energyOnDraw] }, -1),
			],
			[peek, peek],
			Foe(0)
		);
		Assert.That(s.GetParty().Energy, Is.EqualTo(3), "the opening hand is not a draw");

		s = Play(s, "Peek", 0);
		Assert.That(
			s.GetParty().Energy,
			Is.EqualTo(4),
			"one from Inkling; the benched Boar is not active"
		);

		s = Play(s, "Peek", 0);
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
		var s = Deal(
			[new(Mon("Magpie") with { Abilities = [energyOnDiscard] }, 0)],
			[Card("Sift", 0, PartyDiscard.DrawThenDiscard(2, 1))],
			Foe(0)
		);

		s = Play(s, "Sift", 0);

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

	[Test]
	public void RummageDrawsAsManyAsYouDiscard()
	{
		var s = Deal(
			[new(Mon("Pike"), 0)],
			[Card("Rummage", 0, PartyDiscard.DiscardThenDraw())],
			Foe(0)
		);
		s = Play(s, "Rummage", 0);

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

		var kept = EndTurn(Deal([new(Mon("Pike"), 0)], [toss], Foe(0)));
		Assert.That(FoeIn(kept, 0).Hp, Is.EqualTo(50), "the end-of-turn discard is not discarding");

		var s = Deal([new(Mon("Pike"), 0)], [sift, toss], Foe(0));
		s = Play(s, "Sift", 0);
		s = s.ResolveChoice([InHand(s, "Flare").Id]).State;
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 5));
	}

	[Test]
	public void ACostReductionReadsTheDiscardsThisTurn()
	{
		var hammer = Card("Hammer", 3, new StrikeAction { Amount = 6 }) with
		{
			Components = [new CostReduction { PerDiscardThisTurn = 1 }],
		};
		var s = Deal(
			[new(Mon("Pike"), 0)],
			[Card("Sift", 0, PartyDiscard.DrawThenDiscard(1, 1)), hammer],
			Foe(0)
		);
		Assert.That(s.CostOf(InHand(s, "Hammer")), Is.EqualTo(3));

		s = Play(s, "Sift", 0);
		s = s.ResolveChoice([InHand(s, "Guard").Id]).State;

		Assert.That(s.CostOf(InHand(s, "Hammer")), Is.EqualTo(2));
		s = Play(s, "Hammer", 0);
		Assert.That(s.GetParty().Energy, Is.EqualTo(3 - 2), "and it is paid at that cost");
	}

	[Test]
	public void PageStormHitsForPowerPlusTheCardsLeftInHand()
	{
		var s = Deal(
			[new(Mon("Pike", power: 2), 0)],
			[Card("Storm", 0, new StrikeAction { Amount = 0, PlusCardsInHand = true })],
			Foe(0)
		);

		s = Play(s, "Storm", 0);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - (2 + 4)), "Power 2, four cards left in hand");
	}

	[Test]
	public void AThiefStealsTheTopCardAndGivesItBackWhenBeaten()
	{
		var thief = Foe(0, hp: 10, pattern: Hit(1) with { Steals = true });
		var s = Deal([new(Mon("Pike", hp: 30), 0)], [], thief);
		var top = s.CardsIn(ZoneType.Draw).First().Id;

		s = EndTurn(s);
		Assert.That(s.GetParent(top), Is.EqualTo(FoeIn(s, 0).Id), "held by the thief");

		s = Do(s, new StrikeAction { Amount = 99, Space = 0 });
		Assert.That(s.GetParent(top), Is.EqualTo(s.ZoneId(ZoneType.Discard)));
	}

	[Test]
	public void AWildTraitFiresForTheFoeWhileItStands()
	{
		var hoard = Foe(0) with
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
		var s = Deal([new(Mon("Pike"), 0)], [Card("Peek", 0, new DrawAction())], hoard);

		s = Play(s, "Peek", 0);

		Assert.That(FoeIn(s, 0).Block, Is.EqualTo(2));
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
		};

		var mine = PartyRun.FromFoe(wild);

		Assert.That(mine.Passive, Is.EqualTo(wild.CaughtPassive));
		Assert.That(mine.Abilities, Is.EqualTo(wild.CaughtAbilities));
		Assert.That(mine.Moves.Any(m => m.Steals), Is.False);
	}

	// ===== Spellcraft

	private static KinCard Zap(int amount = 4) =>
		Card("Zap", 1, new SpellDamageAction { Amount = amount });

	[Test]
	public void ASpellReachesAnyFoeButMustBeDroppedOnOne()
	{
		var s = Deal([new(Mon("Pike"), 0)], [Zap()], Foe(0), Foe(1));

		Assert.That(CanPlay(s, "Zap", 0), Is.False, "not on your own line");
		s = Play(s, "Zap", 1, foeRow: true);

		Assert.That(FoeIn(s, 1).Hp, Is.EqualTo(50 - 4), "past their front");
	}

	[Test]
	public void SpellPowerCountsOnlyWhileTheMonsterIsInTheLine()
	{
		var ember = Mon("Ember") with { Abilities = [new SpellPower { Amount = 2 }] };
		var benched = Deal([new(Mon("Pike"), 0), new(ember, -1)], [Zap()], Foe(0));
		var fighting = Deal([new(Mon("Pike"), 0), new(ember, 1)], [Zap()], Foe(0));

		Assert.That(FoeIn(Play(benched, "Zap", 0, true), 0).Hp, Is.EqualTo(50 - 4));
		Assert.That(FoeIn(Play(fighting, "Zap", 0, true), 0).Hp, Is.EqualTo(50 - 6));
	}

	[Test]
	public void AWardHalvesTheSpellAfterTheBonus()
	{
		var ember = Mon("Ember") with { Abilities = [new SpellPower { Amount = 2 }] };
		var s = Deal([new(ember, 0)], [Zap()], Foe(0) with { Components = [new SpellWard()] });

		s = Play(s, "Zap", 0, foeRow: true);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - (4 + 2) / 2));
	}

	[Test]
	public void OverloadDealsTheSpellDamageAlreadyDealtThisTurn()
	{
		var overload = Card(
			"Overload",
			0,
			new SpellDamageAction { FromSpellDamageThisTurn = true }
		);
		var s = Deal([new(Mon("Pike"), 0)], [Zap(), Zap(), overload], Foe(0), Foe(1));

		s = Play(s, "Zap", 0, foeRow: true);
		s = Play(s, "Zap", 0, foeRow: true);
		s = Play(s, "Overload", 1, foeRow: true);

		// Zap 4 (Kindle 0 → 1), Zap 4 + 1 (Kindle → 2): 9 spell damage this turn; Overload deals that + 2 Kindle.
		Assert.That(FoeIn(s, 1).Hp, Is.EqualTo(50 - (4 + 5 + 2)));
	}

	[Test]
	public void FocusMakesThisTurnsSpellsHitTheOneBehindToo()
	{
		var s = Deal(
			[new(Mon("Pike"), 0)],
			[Card("Focus", 0, new SplashSpellsAction()), Zap()],
			Foe(0),
			Foe(1),
			Foe(2),
			Foe(3)
		);

		s = Play(s, "Focus", 0);
		s = Play(s, "Zap", 1, foeRow: true);

		Assert.That(s.LivingFoes().Select(f => f.Hp), Is.EqualTo(new[] { 50, 46, 46, 50 }));
	}

	[Test]
	public void AnEchoRepeatsTheLastSpellWhereItWasDroppedAndAWildOneDoesNothing()
	{
		var echo = new Intent { Name = "Echo", Kind = IntentType.Echo };
		var s = Deal([new(Mon("Owl", moves: echo), 0)], [Zap()], Foe(0, pattern: echo), Foe(1));

		s = Play(s, "Zap", 1, foeRow: true);
		s = EndTurn(s);

		Assert.That(
			FoeIn(s, 1).Hp,
			Is.EqualTo(50 - 4 - (4 + 1)),
			"cast, then echoed with 1 Kindle"
		);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50));
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
			Foe(0)
		);

		s = Play(s, "Rally", 0);
		Assert.That(Named(s, "Warden").Block, Is.EqualTo(0), "not a spell");
		s = Play(s, "Zap", 0, foeRow: true);
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
		var s = Deal([new(Mon("Pike"), 0)], [surge], Foe(0));

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
			Foe(0)
		);

		s = Play(s, "Quicken", 0);
		Assert.That(s.CostOf(InHand(s, "Zap")), Is.EqualTo(0));
		s = Play(s, "Zap", 0, foeRow: true);

		Assert.That(s.CostOf(InHand(s, "Zap")), Is.EqualTo(1));
		Assert.That(s.GetParty().Energy, Is.EqualTo(3 - 1));
	}

	[Test]
	public void TheFirstCardEachTurnPaysTaxesAfterDiscountsFlooredAtZero()
	{
		var hush = Foe(0) with { Components = [new FirstCardCost { Amount = 1 }] };
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

		var now = Deal([new(storm, 0)], [Zap(99), battleCry], Foe(0, hp: 5), Foe(1));
		now = Play(now, "Zap", 0, foeRow: true);
		Assert.That(now.GetParty().Energy, Is.EqualTo(3 - 1 + 1), "the Stormbuck");
		now = Play(now, "Cry", 0);
		Assert.That(
			now.GetParty().Energy,
			Is.EqualTo(3 - 1 + 1 + 2),
			"and Battle Cry sees the kill"
		);

		var later = EndTurn(Deal([new(storm, 0)], [], Foe(0, hp: 5), Foe(1)));
		Assert.That(
			later.LivingFoes().Count(),
			Is.EqualTo(1),
			"the front fell at the end of the turn"
		);
		Assert.That(later.GetParty().Energy, Is.EqualTo(3), "no energy from the end of the turn");
	}

	[Test]
	public void AnUnhitGlowmothBringsEnergyNextTurn()
	{
		var moth = Mon("Moth") with { Abilities = [new EnergyIfUnhit { Amount = 1 }] };

		var safe = EndTurn(Deal([new(Mon("Pike"), 0), new(moth, 1)], [], Foe(0, pattern: Hit(1))));
		Assert.That(safe.GetParty().Energy, Is.EqualTo(3 + 1), "behind the front, unhit");

		var struck = EndTurn(Deal([new(moth, 0)], [], Foe(0, pattern: Hit(1))));
		Assert.That(struck.GetParty().Energy, Is.EqualTo(3));
	}

	[Test]
	public void AnXCardSpendsAllYourEnergyAndCountsIt()
	{
		var unleash = Card("Unleash", 0, new StrikeAction { PerX = 4 }) with
		{
			Components = [new SpendsAllEnergy()],
		};
		var s = Deal([new(Mon("Pike"), 0)], [unleash], Foe(0));

		s = Play(s, "Unleash", 0);

		Assert.That(s.GetParty().Energy, Is.EqualTo(0));
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 4 * 3));
	}

	// ===== Summon — tokens enter at the FRONT

	private static TokenTemplate Token(int hp = 3, int fades = 2, params Intent[] moves) =>
		new(Mon("Tok", hp: hp, moves: moves), fades);

	private static KinCard Summon(TokenTemplate token, int count = 1) =>
		Card("Summon", 0, new SummonTokenAction { Token = token, Count = count });

	[Test]
	public void ATokenEntersAtTheFrontActsAndFades()
	{
		var s = Deal(
			[new(Mon("Pike"), 0), new(Mon("Boar"), -1)],
			[Summon(Token(fades: 1, moves: Hit(2)))],
			Foe(0)
		);

		Assert.That(CanPlay(s, "Summon", 0, foeRow: true), Is.False, "your line");
		Assert.That(CanPlay(s, "Summon", 1), Is.False, "your FRONT — where it arrives");
		s = Play(s, "Summon", 0);
		Assert.That(Line(s), Is.EqualTo("Tok,Pike"));

		s = EndTurn(s);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 2), "it acted");
		Assert.That(
			Line(s),
			Is.EqualTo("Pike"),
			"and faded at the start of the next turn — bringing no one off the bench"
		);
	}

	[Test]
	public void TokensNeverKeepABattleAlive()
	{
		var s = Deal(
			[new(Mon("Pike", hp: 5), 0)],
			[Summon(Token(hp: 30))],
			Foe(0, pattern: Hit(9, Aim.Back))
		);
		s = Play(s, "Summon", 0);

		s = EndTurn(s);

		Assert.That(s.GetParty().IsOver && !s.GetParty().Won, Is.True);
	}

	[Test]
	public void AFallenTokenBringsNoBenchButShieldsTheOneBesideIt()
	{
		var sprout = new TokenTemplate(
			Mon("Sprout", hp: 3) with
			{
				Abilities = [new FaintShield { Amount = 3 }],
			},
			2
		);
		var s = Deal(
			[new(Mon("Pike"), 0), new(Mon("Boar"), -1)],
			[Summon(sprout)],
			Foe(0, pattern: Hit(3)),
			Foe(1, pattern: Hit(9))
		);
		s = Play(s, "Summon", 0);

		// Their back fells the sprout first; then their front's blow finds Pike behind the shield.
		s = EndTurn(s);

		Assert.That(Line(s), Is.EqualTo("Pike"), "the sprout fell, and no bench came for a token");
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20), "the later blow hit the shield");
	}

	[Test]
	public void ADecoyDrawsBackAndHuntingAttacks()
	{
		var decoy = new TokenTemplate(Mon("Decoy", hp: 30) with { Abilities = [new Lure()] }, 1);
		var s = Deal(
			[new(Mon("Pike", hp: 5), 0)],
			[Summon(decoy)],
			Foe(0, pattern: Hit(4, Aim.Hunt))
		);
		s = Play(s, "Summon", 0);

		s = EndTurn(s);

		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(5), "the weakest was spared");
	}

	[Test]
	public void TrampleCarriesTheRestIntoTheOneBehind()
	{
		var horn = Foe(0, pattern: Hit(8)) with { Components = [new Trample()] };
		var s = Deal([new(Mon("Pike"), 0)], [Summon(Token(hp: 3))], horn);
		s = Play(s, "Summon", 0);

		s = EndTurn(s);

		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20 - (8 - 3)));
	}

	[Test]
	public void AWildBroodPutsAGrubAtTheirFront()
	{
		var brood = new Intent
		{
			Name = "Brood",
			Kind = IntentType.Summon,
			Summons = Token(hp: 2, fades: 2),
		};
		var s = EndTurn(Battle([new(Mon("Pike", hp: 30), 0)], [], Foe(0, pattern: brood)));

		var front = FoeIn(s, 0);
		Assert.That(front.FadesIn, Is.GreaterThan(0), "the grub is in front");
	}

	[Test]
	public void SwarmAttacksWithEveryTokenAndABoostArrivesWithThem()
	{
		var howler = Mon("Howler") with { Abilities = [new TokenBoost { Hp = 2, Power = 1 }] };
		var s = Deal(
			[new(howler, 0)],
			[Summon(Token(), count: 2), Card("Swarm", 0, new TokensAttackAction { Amount = 2 })],
			Foe(0)
		);

		s = Play(s, "Summon", 0);
		Assert.That(Line(s), Is.EqualTo("Tok,Tok,Howler"));
		Assert.That(s.AllyAt(0)!.Hp, Is.EqualTo(3 + 2));
		s = Play(s, "Swarm", 0);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 2 * (2 + 1)));
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
		var s = Deal([new(Mon("Pike"), 0)], [Summon(Token()), offering], Foe(0));
		s = Play(s, "Summon", 0);

		Assert.That(CanPlay(s, "Offering", 1), Is.False, "only on a token");
		s = Play(s, "Offering", 0);

		Assert.That(Line(s), Is.EqualTo("Pike"));
		Assert.That(s.GetParty().Energy, Is.EqualTo(3 + 1));
		Assert.That(s.CardsIn(ZoneType.Hand).Count(), Is.EqualTo(3 + 2));
	}

	// ===== How a battle ends

	[Test]
	public void KillingTheLastFoeWins()
	{
		var s = EndTurn(Battle([new(Mon("Pike", moves: Hit(99)), 0)], [], Foe(0)));

		Assert.That(s.GetParty().IsOver && s.GetParty().Won, Is.True);
	}

	[Test]
	public void EveryMonsterDownLoses()
	{
		var s = EndTurn(Battle([new(Mon("Pike", hp: 5), 0)], [], Foe(0, pattern: Hit(9))));

		Assert.That(s.GetParty().IsOver && !s.GetParty().Won, Is.True);
	}

	[Test]
	public void OneMonsterDownIsNotALoss()
	{
		var s = EndTurn(
			Battle(
				[new(Mon("Pike", hp: 5), 0), new(Mon("Bramble"), 1)],
				[],
				Foe(0, pattern: Hit(9))
			)
		);

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
			Assert.That(
				s.LivingAllies().Select(a => a.Position),
				Is.EqualTo(Enumerable.Range(0, s.LivingAllies().Count())),
				"the line is contiguous from the front"
			);
		}
	}

	// ===== The swap cards' SOLO MODE (Shayne, 2026-09-27: dead cards with one monster)

	[Test]
	public void HoldTheLineAtTheFrontGainsBlockInsteadOfBeingDead()
	{
		var hold = PartyCards.HoldTheLine;
		var block = ((SwapAction)hold.Effects[0].Template).AloneBlock;
		var s = Deal([new PlacedCompanion(Mon("Solo"), 0)], [hold], Foe(0, 50, Idle));

		Assert.That(block, Is.GreaterThan(0));
		Assert.That(CanPlay(s, "Hold the Line", 0), Is.True, "alone, it is no longer dead");
		s = Play(s, "Hold the Line", 0);

		Assert.That(Named(s, "Solo").Block, Is.EqualTo(block));
	}

	[Test]
	public void GustAgainstALoneFoeHitsItInsteadOfBeingDead()
	{
		var damage = ((GustAction)PartyCards.Gust.Effects[0].Template).AloneDamage;
		var s = Deal([new PlacedCompanion(Mon("Solo"), 0)], [PartyCards.Gust], Foe(0, 50, Idle));

		Assert.That(damage, Is.GreaterThan(0));
		Assert.That(CanPlay(s, "Gust", 0, foeRow: true), Is.True);
		s = Play(s, "Gust", 0, foeRow: true);

		Assert.That(s.LivingFoes().Single().Hp, Is.EqualTo(50 - damage));
	}
}
