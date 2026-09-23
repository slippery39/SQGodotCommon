using System.Collections.Immutable;
using ImmutableGameObjects;
using KinCore;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **THE COMPANION GAME's first slice — every mechanic FIRES.** Exploring, not tuning: these read a
/// number that moved, never a field that was set, and every companion, card and foe is inline.
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

	private static PartyCompanion Mon(
		string name,
		int hp = 20,
		int power = 0,
		int speed = 3,
		params KinCard[] cards
	) => new(name, hp, power, speed, [.. cards]);

	private static Foe Foe(int space, int hp = 50, params Intent[] pattern) =>
		new()
		{
			Name = "Foe",
			Hp = hp,
			MaxHp = hp,
			Space = space,
			Pattern = pattern.Length > 0 ? [.. pattern] : [Idle],
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

	/// <summary>Decks here are five cards or fewer, so every card is in the opening hand.</summary>
	private static GameState Battle(PlacedCompanion[] companions, params Foe[] foes) =>
		PartyBattleFactory.Create(new PartyScenario("Test", "", [.. companions], [.. foes], []));

	private static GameState Do(GameState s, GameAction action) =>
		s.AddAction(action).ProcessAllActions().State;

	private static GameState Play(GameState s, string name, int space = -1) =>
		Do(s, new PlayPartyCardAction { CardId = InHand(s, name).Id, Space = space });

	private static KinCard InHand(GameState s, string name) =>
		s.CardsIn(ZoneType.Hand).First(c => c.Name == name);

	private static Ally Named(GameState s, string name) => s.Allies().Single(a => a.Name == name);

	private static Foe FoeIn(GameState s, int space) =>
		s.GetChildren(s.GetWellKnownId(PartyState.BattleKey))
			.OfType<Foe>()
			.Single(f => f.Space == space);

	// ===== Cards act through their owner

	[Test]
	public void AnAttackFiresStraightAheadAndAddsPower()
	{
		var jab = Card("Jab", 1, new StrikeAction { Amount = 2 });
		var s = Battle([new(Mon("Pike", power: 3, cards: jab), 2)], Foe(2), Foe(3));

		s = Play(s, "Jab");

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50 - (2 + 3)));
		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(50), "only the column ahead is hit");
	}

	[Test]
	public void AnAttackIntoAnEmptyColumnMisses()
	{
		var jab = Card("Jab", 1, new StrikeAction { Amount = 2 });
		var s = Battle([new(Mon("Pike", cards: jab), 0)], Foe(2));

		s = Play(s, "Jab");

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50));
		Assert.That(s.GetParty().Energy, Is.EqualTo(2), "the card was still played");
	}

	[Test]
	public void ASweepHitsThreeColumns()
	{
		var sweep = Card(
			"Sweep",
			2,
			new StrikeAction
			{
				Amount = 4,
				AddPower = false,
				Offsets = [-1, 0, 1],
			}
		);
		var s = Battle([new(Mon("Pike", cards: sweep), 2)], Foe(1), Foe(2), Foe(3), Foe(4));

		s = Play(s, "Sweep");

		Assert.That(new[] { 1, 2, 3 }.Select(c => FoeIn(s, c).Hp), Is.All.EqualTo(46));
		Assert.That(FoeIn(s, 4).Hp, Is.EqualTo(50));
	}

	[Test]
	public void LungeStepsThenStrikesFromWhereItLands()
	{
		var lunge = Card(
			"Lunge",
			1,
			new StepAction(),
			new StrikeAction { Amount = 5, AddPower = false }
		);
		var s = Battle([new(Mon("Pike", cards: lunge), 2)], Foe(2), Foe(3));

		s = Play(s, "Lunge", space: 3);

		Assert.That(Named(s, "Pike").Space, Is.EqualTo(3));
		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(45));
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50));
	}

	[Test]
	public void AMoveCardMustBeDroppedOnAnEmptySpaceNextToItsOwner()
	{
		var lunge = Card("Lunge", 1, new StepAction(), new StrikeAction { Amount = 5 });
		var s = Battle([new(Mon("Pike", cards: lunge), 2), new(Mon("Bramble"), 3)], Foe(2));
		var id = InHand(s, "Lunge").Id;

		foreach (var bad in new[] { -1, 2, 3, 4 })
			Assert.That(
				new PlayPartyCardAction { CardId = id, Space = bad }
					.ValidateAdd(s)
					.IsValid,
				Is.False,
				$"space {bad}"
			);
		Assert.That(
			new PlayPartyCardAction { CardId = id, Space = 1 }
				.ValidateAdd(s)
				.IsValid,
			Is.True
		);
	}

	[Test]
	public void AKnockedOutCompanionsCardsAreDead()
	{
		var jab = Card("Jab", 0, new StrikeAction { Amount = 1 });
		var s = Battle(
			[new(Mon("Pike", hp: 4, cards: jab), 2), new(Mon("Bramble", hp: 30), 0)],
			Foe(2, 50, Hit(9))
		);

		s = Do(s, new EndPartyTurnAction());

		Assert.That(Named(s, "Pike").IsKnockedOut, Is.True);
		var refusal = new PlayPartyCardAction { CardId = InHand(s, "Jab").Id }.ValidateAdd(s);
		Assert.That(refusal.Reason, Does.Contain("knocked out"));
	}

	// ===== Block

	[Test]
	public void BlockSoaksAHitAndIsGoneNextTurn()
	{
		var bark = Card("Bark Skin", 1, new GuardAction { Amount = 6 });
		var s = Battle([new(Mon("Bramble", hp: 30, cards: bark), 1)], Foe(1, 50, Hit(9)));

		s = Play(s, "Bark Skin");
		s = Do(s, new EndPartyTurnAction());

		Assert.That(Named(s, "Bramble").Hp, Is.EqualTo(30 - (9 - 6)));
		Assert.That(Named(s, "Bramble").Block, Is.EqualTo(0));
	}

	[Test]
	public void RootWallGuardsTheCompanionsBesideIt()
	{
		var wall = Card("Root Wall", 2, new GuardAction { Amount = 5, AndBeside = true });
		var s = Battle(
			[new(Mon("Bramble", cards: wall), 1), new(Mon("Near"), 2), new(Mon("Far"), 4)],
			Foe(0)
		);

		s = Play(s, "Root Wall");

		Assert.That(Named(s, "Bramble").Block, Is.EqualTo(5));
		Assert.That(Named(s, "Near").Block, Is.EqualTo(5));
		Assert.That(Named(s, "Far").Block, Is.EqualTo(0));
	}

	[Test]
	public void AFoesBlockHoldsThroughYourTurn()
	{
		var jab = Card("Jab", 1, new StrikeAction { Amount = 10, AddPower = false });
		var s = Battle(
			[new(Mon("Pike", cards: jab), 2)],
			Foe(
				2,
				50,
				new Intent
				{
					Name = "Preen",
					Kind = IntentType.Block,
					Amount = 6,
				}
			)
		);

		s = Do(s, new EndPartyTurnAction());
		s = Play(s, "Jab");

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50 - (10 - 6)));
	}

	// ===== Where you stand

	[Test]
	public void SteppingOutOfAShapeDodgesIt()
	{
		var s = Battle([new(Mon("Pike", hp: 20), 2)], Foe(2, 50, Hit(9)));

		s = Do(s, new MoveAllyAction { AllyId = Named(s, "Pike").Id, Space = 1 });
		s = Do(s, new EndPartyTurnAction());

		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20));
	}

	[Test]
	public void TheMiddleOfAThreeWideAttackCannotStepOut()
	{
		var s = Battle([new(Mon("Pike", hp: 20), 2)], Foe(2, 50, Hit(5, -1, 0, 1)));

		s = Do(s, new MoveAllyAction { AllyId = Named(s, "Pike").Id, Space = 3 });
		s = Do(s, new EndPartyTurnAction());

		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(15));
	}

	[Test]
	public void AHomingAttackFindsTheLowestHpCompanionWhereverItStands()
	{
		var zap = new Intent
		{
			Name = "Zap",
			Kind = IntentType.Attack,
			Amount = 4,
			Homing = true,
		};
		var s = Battle([new(Mon("Big", hp: 30), 0), new(Mon("Small", hp: 10), 4)], Foe(2, 50, zap));

		s = Do(s, new EndPartyTurnAction());

		Assert.That(Named(s, "Small").Hp, Is.EqualTo(6));
		Assert.That(Named(s, "Big").Hp, Is.EqualTo(30));
	}

	[Test]
	public void DrawFirePullsASingleTargetHitOffTheCompanionBesideIt()
	{
		var decoy = Card("Draw Fire", 1, new DrawFireAction());
		var s = Battle(
			[new(Mon("Bramble", hp: 30, cards: decoy), 1), new(Mon("Pike", hp: 20), 2)],
			Foe(2, 50, Hit(9))
		);

		s = Play(s, "Draw Fire");
		s = Do(s, new EndPartyTurnAction());

		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(20));
		Assert.That(Named(s, "Bramble").Hp, Is.EqualTo(21));
	}

	[Test]
	public void DrawFireDoesNotPullAWideAttack()
	{
		var decoy = Card("Draw Fire", 1, new DrawFireAction());
		var s = Battle(
			[new(Mon("Bramble", hp: 30, cards: decoy), 0), new(Mon("Pike", hp: 20), 2)],
			Foe(2, 50, Hit(5, 0, 1))
		);

		s = Play(s, "Draw Fire");
		s = Do(s, new EndPartyTurnAction());

		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(15));
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
		var s = Battle([new(Mon("Pike"), 0)], Foe(3, 50, drift));

		s = Do(s, new EndPartyTurnAction());

		Assert.That(s.LivingFoes().Single().Space, Is.EqualTo(2));
	}

	// ===== The free move and its cooldown

	[TestCase(3, 1)]
	[TestCase(2, 2)]
	[TestCase(1, 3)]
	public void SpeedSetsHowManyTurnsUntilTheNextFreeMove(int speed, int turnsToWait)
	{
		var s = Battle([new(Mon("Mon", speed: speed), 2)], Foe(0));
		var id = Named(s, "Mon").Id;

		s = Do(s, new MoveAllyAction { AllyId = id, Space = 3 });

		for (var turn = 1; turn < turnsToWait; turn++)
		{
			s = Do(s, new EndPartyTurnAction());
			Assert.That(
				new MoveAllyAction { AllyId = id, Space = 2 }
					.ValidateAdd(s)
					.IsValid,
				Is.False,
				$"turn +{turn}"
			);
		}

		s = Do(s, new EndPartyTurnAction());
		Assert.That(
			new MoveAllyAction { AllyId = id, Space = 2 }
				.ValidateAdd(s)
				.IsValid,
			Is.True
		);
	}

	/// <summary>
	/// **A card is discarded AFTER it resolves, so it can never draw itself.** Found in play: Feint
	/// (step, draw 1) went to Discard first, the draw reshuffled Discard into an empty Draw pile, and
	/// Feint came straight back — a free step with no cooldown, as often as it came up.
	/// </summary>
	[Test]
	public void ACardThatDrawsCannotDrawItself()
	{
		var feint = Card("Feint", 0, new StepAction(), new DrawAction { Count = 1 });
		var s = Battle([new(Mon("Pike", cards: feint), 2)], Foe(0));

		s = Play(s, "Feint", space: 3);

		Assert.That(s.CardsIn(ZoneType.Hand), Is.Empty);
		Assert.That(s.CardsIn(ZoneType.Discard).Select(c => c.Name), Is.EqualTo(new[] { "Feint" }));
	}

	[Test]
	public void AMoveCardIgnoresTheCooldown()
	{
		var feint = Card("Feint", 0, new StepAction());
		var s = Battle([new(Mon("Pike", speed: 1, cards: feint), 2)], Foe(0));

		s = Do(s, new MoveAllyAction { AllyId = Named(s, "Pike").Id, Space = 3 });
		s = Play(s, "Feint", space: 4);

		Assert.That(Named(s, "Pike").Space, Is.EqualTo(4));
	}

	// ===== KITS v2 — passives and signature mechanics

	private static PartyCompanion Thorny(int thorns, params KinCard[] cards) =>
		new("Bramble", 30, 0, 1, [.. cards], Thorns: thorns);

	private static PartyCompanion Nimble(int perStep, params KinCard[] cards) =>
		new("Pike", 20, 0, 3, [.. cards], MomentumPerStep: perStep);

	[Test]
	public void ThornsHurtWhateverAttacksHerEvenThroughBlock()
	{
		var bark = Card("Bark Skin", 0, new GuardAction { Amount = 50 });
		var s = Battle([new(Thorny(2, bark), 1)], Foe(1, 50, Hit(9)));

		s = Play(s, "Bark Skin");
		s = Do(s, new EndPartyTurnAction());

		Assert.That(FoeIn(s, 1).Hp, Is.EqualTo(48));
		Assert.That(Named(s, "Bramble").Hp, Is.EqualTo(30), "the hit was fully blocked");
	}

	[Test]
	public void ThornhideAddsThornsForThisTurnOnly()
	{
		var hide = Card("Thornhide", 0, new ThornsAction { Amount = 3 });
		var s = Battle([new(Thorny(2, hide), 1)], Foe(1, 50, Hit(1)));

		s = Play(s, "Thornhide");
		s = Do(s, new EndPartyTurnAction());
		Assert.That(FoeIn(s, 1).Hp, Is.EqualTo(50 - 5));

		s = Do(s, new EndPartyTurnAction());
		Assert.That(FoeIn(s, 1).Hp, Is.EqualTo(45 - 2), "back to the passive alone");
	}

	[Test]
	public void ThornsCanWinTheBattleBeforeTheRestOfTheFoesAct()
	{
		var s = Battle(
			[new(Thorny(5), 1), new(Mon("Pike", hp: 20), 3)],
			Foe(1, 5, Hit(1)),
			Foe(3, 5, Hit(9))
		);
		// Only the first foe attacks Bramble; the second would hit Pike if it ever acted.
		s = Do(s, new EndPartyTurnAction());

		Assert.That(FoeIn(s, 1).IsDead, Is.True);
		Assert.That(s.GetParty().IsOver, Is.False, "one foe still stands");
		Assert.That(Named(s, "Pike").Hp, Is.EqualTo(11));

		var last = Battle([new(Thorny(5), 1), new(Mon("Pike", hp: 20), 3)], Foe(1, 5, Hit(1)));
		last = Do(last, new EndPartyTurnAction());
		Assert.That(last.GetParty().IsOver && last.GetParty().Won, Is.True);
	}

	[Test]
	public void RetaliateDealsHerBlock()
	{
		var bark = Card("Bark Skin", 0, new GuardAction { Amount = 7 });
		var hit = Card("Retaliate", 0, new StrikeAction { AddPower = false, AddBlock = true });
		var s = Battle([new(Thorny(0, bark, hit), 1)], Foe(1));

		s = Play(s, "Bark Skin");
		s = Play(s, "Retaliate");

		Assert.That(FoeIn(s, 1).Hp, Is.EqualTo(43));
	}

	[Test]
	public void EveryStepBuildsMomentumAndTheNextAttackSpendsIt()
	{
		var feint = Card("Feint", 0, new StepAction());
		var jab = Card("Jab", 0, new StrikeAction { Amount = 1, AddPower = false });
		var jab2 = Card("Jab Two", 0, new StrikeAction { Amount = 1, AddPower = false });
		var s = Battle([new(Nimble(2, feint, jab, jab2), 1)], Foe(3));

		s = Do(s, new MoveAllyAction { AllyId = Named(s, "Pike").Id, Space = 2 }); // free move
		s = Play(s, "Feint", space: 3); // card step
		s = Play(s, "Jab");
		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(50 - (1 + 2 + 2)));

		s = Play(s, "Jab Two");
		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(45 - 1), "spent by the first attack");
	}

	[Test]
	public void MomentumIsGoneNextTurn()
	{
		var jab = Card("Jab", 0, new StrikeAction { Amount = 1, AddPower = false });
		var s = Battle([new(Nimble(2, jab), 1)], Foe(2));

		s = Do(s, new MoveAllyAction { AllyId = Named(s, "Pike").Id, Space = 2 });
		s = Do(s, new EndPartyTurnAction());
		s = Play(s, "Jab");

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(49));
	}

	[Test]
	public void HitAndRunStrikesFromWhereItStandsThenSteps()
	{
		var run = Card(
			"Hit and Run",
			0,
			new StrikeAction { Amount = 5, AddPower = false },
			new StepAction()
		);
		var s = Battle([new(Nimble(2, run), 2)], Foe(2), Foe(3));

		s = Play(s, "Hit and Run", space: 3);

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(45));
		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(50));
		Assert.That(Named(s, "Pike").Space, Is.EqualTo(3));
	}

	[Test]
	public void FlankDoublesOnlyAgainstALoneFoe()
	{
		var flank = Card(
			"Flank",
			0,
			new StrikeAction
			{
				Amount = 4,
				AddPower = false,
				DoubleIfAlone = true,
			}
		);
		var alone = Battle([new(Nimble(0, flank), 2)], Foe(2), Foe(4));
		var crowded = Battle([new(Nimble(0, flank), 2)], Foe(2), Foe(3));

		alone = Play(alone, "Flank");
		crowded = Play(crowded, "Flank");

		Assert.That(FoeIn(alone, 2).Hp, Is.EqualTo(42));
		Assert.That(FoeIn(crowded, 2).Hp, Is.EqualTo(46));
	}

	// ===== Gale, the Controller — moving foes

	private static PartyCompanion Windy(int unbalances, params KinCard[] cards) =>
		new("Gale", 22, 0, 2, [.. cards], Unbalances: unbalances);

	private static readonly KinCard Gust = Card("Gust", 0, new PushAction());

	[Test]
	public void AGustReAimsTheFoesAttack()
	{
		var s = Battle([new(Windy(0, Gust), 2)], Foe(2, 50, Hit(9)));

		s = Play(s, "Gust", space: 3);
		s = Do(s, new EndPartyTurnAction());

		Assert.That(s.LivingFoes().Single().Space, Is.EqualTo(3));
		Assert.That(Named(s, "Gale").Hp, Is.EqualTo(22), "the Charge now lands on an empty space");
	}

	[Test]
	public void AGustNeedsAFoeAheadAndAnOpenColumnBesideIt()
	{
		var s = Battle([new(Windy(0, Gust), 2)], Foe(2), Foe(3));
		var id = InHand(s, "Gust").Id;

		Assert.That(
			new PlayPartyCardAction { CardId = id, Space = 3 }
				.ValidateAdd(s)
				.Reason,
			Does.Contain("in the way")
		);
		Assert.That(
			new PlayPartyCardAction { CardId = id, Space = 0 }
				.ValidateAdd(s)
				.IsValid,
			Is.False
		);
		Assert.That(
			new PlayPartyCardAction { CardId = id, Space = 1 }
				.ValidateAdd(s)
				.IsValid,
			Is.True
		);

		var empty = Battle([new(Windy(0, Gust), 2)], Foe(4));
		Assert.That(
			new PlayPartyCardAction { CardId = InHand(empty, "Gust").Id, Space = 3 }
				.ValidateAdd(empty)
				.Reason,
			Does.Contain("no foe ahead")
		);
	}

	[Test]
	public void ASlamIntoAFoeHurtsBothAndMovesNeither()
	{
		var slam = Card("Slam", 0, new PushAction { Collision = 5 });
		var s = Battle([new(Windy(0, slam), 2)], Foe(2), Foe(3));

		s = Play(s, "Slam", space: 3);

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(45));
		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(45));
	}

	[Test]
	public void AWhirlwindSwapsTwoFoes()
	{
		var whirl = Card("Whirlwind", 0, new SwapAction());
		var s = Battle([new(Windy(0, whirl), 2)], Foe(2, hp: 10), Foe(1, hp: 20));

		s = Play(s, "Whirlwind", space: 1);

		Assert.That(FoeIn(s, 1).Hp, Is.EqualTo(10));
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(20));
	}

	[Test]
	public void AFoeGaleMovesIsOffBalanceUntilYourNextTurn()
	{
		var jab = Card("Jab", 0, new StrikeAction { Amount = 5, AddPower = false });
		var later = Card("Jab Later", 0, new StrikeAction { Amount = 5, AddPower = false });
		var s = Battle([new(Windy(2, Gust), 2), new(Nimble(0, jab, later), 3)], Foe(2));

		s = Play(s, "Gust", space: 3); // pushed in front of Pike
		s = Play(s, "Jab");
		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(50 - (5 + 2)));

		s = Do(s, new EndPartyTurnAction());
		s = Play(s, "Jab Later");
		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(43 - 5), "Off-Balance is gone next turn");
	}

	// ===== Reward cards — the primitives they added

	[Test]
	public void BriarBurstDealsHerThornsAcrossThreeColumns()
	{
		var hide = Card("Thornhide", 0, new ThornsAction { Amount = 3 });
		var burst = Card(
			"Briar Burst",
			0,
			new StrikeAction
			{
				AddPower = false,
				AddThorns = true,
				Offsets = [-1, 0, 1],
			}
		);
		var s = Battle([new(Thorny(2, hide, burst), 2)], Foe(1), Foe(2), Foe(3));

		s = Play(s, "Thornhide");
		s = Play(s, "Briar Burst");

		Assert.That(new[] { 1, 2, 3 }.Select(c => FoeIn(s, c).Hp), Is.All.EqualTo(45));
	}

	[Test]
	public void PierceIgnoresBlock()
	{
		var pierce = Card(
			"Pierce",
			0,
			new StrikeAction
			{
				Amount = 5,
				AddPower = false,
				IgnoreBlock = true,
			}
		);
		var preen = new Intent
		{
			Name = "Preen",
			Kind = IntentType.Block,
			Amount = 10,
		};
		var s = Battle([new(Nimble(0, pierce), 2)], Foe(2, 50, preen));

		s = Do(s, new EndPartyTurnAction());
		s = Play(s, "Pierce");

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(45));
	}

	[Test]
	public void QuickstepBuildsMomentumTwice()
	{
		var quick = Card("Quickstep", 0, new StepAction(), new MomentumAction { Amount = 2 });
		var jab = Card("Jab", 0, new StrikeAction { Amount = 1, AddPower = false });
		var s = Battle([new(Nimble(2, quick, jab), 1)], Foe(2));

		s = Play(s, "Quickstep", space: 2);
		s = Play(s, "Jab");

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(50 - (1 + 2 + 2)));
	}

	[Test]
	public void DowndraftUnbalancesWithoutMoving()
	{
		var down = Card("Downdraft", 0, new UnbalanceAction());
		var jab = Card("Jab", 0, new StrikeAction { Amount = 5, AddPower = false });
		var s = Battle([new(Windy(2, down, jab), 2)], Foe(2));

		s = Play(s, "Downdraft");
		s = Play(s, "Jab");

		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(43));
	}

	[Test]
	public void CycloneSwapsAndHurtsBoth()
	{
		var cyclone = Card("Cyclone", 0, new SwapAction { Damage = 3 });
		var s = Battle([new(Windy(0, cyclone), 2)], Foe(2, hp: 10), Foe(3, hp: 20));

		s = Play(s, "Cyclone", space: 3);

		Assert.That(FoeIn(s, 3).Hp, Is.EqualTo(7));
		Assert.That(FoeIn(s, 2).Hp, Is.EqualTo(17));
	}

	// ===== The telegraph is the truth

	[Test]
	public void TheForecastIsWhatEndingTheTurnCosts()
	{
		var s = Battle(
			[new(Mon("A", hp: 30), 1), new(Mon("B", hp: 30), 2)],
			Foe(1, 50, Hit(7, 0, 1)),
			Foe(2, 50, Hit(3))
		);
		var forecast = s.HpLostIfTurnEndsNow();

		var after = Do(s, new EndPartyTurnAction());

		foreach (var ally in s.Allies())
			Assert.That(
				ally.Hp - after.Allies().Single(a => a.Id == ally.Id).Hp,
				Is.EqualTo(forecast[ally.Id])
			);
		Assert.That(forecast.Values.Sum(), Is.GreaterThan(0));
	}

	// ===== How a battle ends

	[Test]
	public void KillingTheLastFoeWins()
	{
		var jab = Card("Jab", 1, new StrikeAction { Amount = 5, AddPower = false });
		var s = Battle([new(Mon("Pike", cards: jab), 2)], Foe(2, hp: 5));

		s = Play(s, "Jab");

		Assert.That(s.GetParty().IsOver && s.GetParty().Won, Is.True);
	}

	[Test]
	public void EveryCompanionKnockedOutLoses()
	{
		var s = Battle(
			[new(Mon("A", hp: 5), 1), new(Mon("B", hp: 5), 2)],
			Foe(1, 50, Hit(9, 0, 1))
		);

		s = Do(s, new EndPartyTurnAction());

		Assert.That(s.GetParty().IsOver, Is.True);
		Assert.That(s.GetParty().Won, Is.False);
	}

	[Test]
	public void OneCompanionDownIsNotALoss()
	{
		var s = Battle([new(Mon("A", hp: 5), 1), new(Mon("B", hp: 30), 3)], Foe(1, 50, Hit(9)));

		s = Do(s, new EndPartyTurnAction());

		Assert.That(s.GetParty().IsOver, Is.False);
		Assert.That(s.AllyAt(1), Is.Null, "a knocked-out companion leaves the row");
	}

	// ===== The authored scenarios build and deal what they say

	[Test]
	public void EveryScenarioBuildsAndDealsItsOpeningHand()
	{
		foreach (var scenario in PartyContent.Scenarios)
		{
			var s = PartyBattleFactory.Create(scenario);
			var hand = s.CardsIn(ZoneType.Hand).Select(c => c.Name).ToList();

			Assert.That(hand, Has.Count.EqualTo(StartPartyTurnAction.HandSize), scenario.Name);
			Assert.That(hand, Is.SupersetOf(scenario.OpeningHand), scenario.Name);
			Assert.That(s.Allies().Count(), Is.EqualTo(scenario.Companions.Count));
			Assert.That(s.LivingFoes().Count(), Is.EqualTo(scenario.Foes.Count));
		}
	}
}
