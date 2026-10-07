using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Sets.Fra;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Every modal FRA card: each mode FIRES, through real casting and the real stack.</summary>
public class FraModalTests : FraBoardTest
{
	[Test]
	public void SurgicalPrecision_DestroysOnlyToughnessFourUp_OrDrawsAndGains()
	{
		var small = Field(TestGames.Creature("Small", "3/3"), them);
		var big = Field(TestGames.Creature("Big", "2/4"), them);
		var precision = Hand("Surgical Precision", me);
		Assert.That(
			Casts(precision).Where(a => a.Modes[0] == 0).Select(a => a.Targets[0]),
			Does.Contain(big).And.Not.Contain(small)
		);

		var life = Life(me);
		Cast(precision, [0], big);
		Assert.That((Alive(big), Life(me)), Is.EqualTo((false, life + 1)));

		var hand = HandCount(me);
		var second = Hand("Surgical Precision", me);
		Cast(second, [1]);
		Assert.That(
			(HandCount(me), Life(me)),
			Is.EqualTo((hand + 1, life + 1 + 2)),
			"put in hand +1, cast -1, drew +1"
		);
	}

	[Test]
	public void DiviningDuelist_EntersAndTaps_OrLoots()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var duelist = Hand("Divining Duelist", me);
		CastWithTrigger(duelist, [0], bear);
		Assert.That(s.GetCard(bear).IsTapped);

		var loot = Hand("Divining Duelist", me);
		var hand = HandCount(me);
		CastWithTrigger(loot, [2]);
		Assert.That(HandCount(me), Is.EqualTo(hand - 1), "cast it, drew one, discarded one");
		Assert.That(s.CardsIn(me, ZoneType.Graveyard).Count(), Is.EqualTo(1));
	}

	[Test]
	public void PerfectedTheory_SetsBaseUnderCountersAndPumps()
	{
		var bear = Field(TestGames.Creature("Bear", "3/3"), me);
		s = s.UpdateObject(bear, s.GetCard(bear) with { PlusOneCounters = 1 }); // TEST SETUP

		Cast(Hand("Perfected Theory", me), [0], bear);
		Assert.That(
			(s.Power(s.GetCard(bear)), s.Toughness(s.GetCard(bear))),
			Is.EqualTo((2, 2)),
			"base 1/1, then the counter"
		);

		Cast(Hand("Perfected Theory", me), [1], bear);
		Assert.That(
			(s.Power(s.GetCard(bear)), s.Toughness(s.GetCard(bear))),
			Is.EqualTo((5, 6)),
			"the later base wins"
		);
	}

	[Test]
	public void RiseOfTheDeathbringer_DrawsByGreatestPower_OrShrinksEveryCreature()
	{
		Field(TestGames.Creature("Three", "3/1"), me);
		var (hand, life) = (HandCount(me), Life(me));
		Cast(Hand("Rise of the Deathbringer", me), [0]);
		Assert.That((HandCount(me), Life(me)), Is.EqualTo((hand + 3, life - 3)));

		var theirs = Field(TestGames.Creature("Theirs", "3/4"), them);
		Cast(Hand("Rise of the Deathbringer", me), [1]);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Any(c => c.Name == "Three"),
			Is.False,
			"mine too"
		);
		Assert.That(s.Toughness(s.GetCard(theirs)), Is.EqualTo(1));
	}

	[Test]
	public void VraskasFinalMercy_DestroysOrEmpowers_AlwaysTwoLife()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var life = Life(me);
		Cast(Hand("Vraska's Final Mercy", me), [0], bear);
		Assert.That((Alive(bear), Life(me)), Is.EqualTo((false, life - 2)));

		Cast(Hand("Vraska's Final Mercy", me), [1]);
		Assert.That((Jace(me)!.Loyalty, Life(me)), Is.EqualTo((6, life - 4)));
	}

	[Test]
	public void FulminousForte_PingsOnlyTheirSide_OrFiveToOne()
	{
		var mine = Field(TestGames.Creature("Mine", "1/1"), me);
		var theirs = Field(TestGames.Creature("Theirs", "1/1"), them);
		Cast(Hand("Fulminous Forte", me), [0]);
		Assert.That((Alive(mine), Alive(theirs)), Is.EqualTo((true, false)));

		var big = Field(TestGames.Creature("Big", "4/5"), them);
		Cast(Hand("Fulminous Forte", me), [1], big);
		Assert.That(Alive(big), Is.False);
	}

	[Test]
	public void CompelBrutality_YourCreatureBites_OrYourWalkerByLoyalty()
	{
		var mine = Field(TestGames.Creature("Mine", "3/3"), me);
		var theirs = Field(TestGames.Creature("Theirs", "3/3"), them);
		Cast(Hand("Compel Brutality", me), [0], mine, theirs);
		Assert.That((Alive(theirs), s.GetCard(mine).Damage), Is.EqualTo((false, 0)), "one-sided");

		Cast(Hand("Vraska's Final Mercy", me), [1]); // Jace at 6
		var big = Field(TestGames.Creature("Big", "5/6"), them);
		Cast(Hand("Compel Brutality", me), [1], Jace(me)!.Id, big);
		Assert.That(Alive(big), Is.False, "6 loyalty, 6 damage");
	}

	[Test]
	public void ChargeTheSanctum_TeamPlusTwo_OrOneFirstStrikerWithACounter()
	{
		var a = Field(TestGames.Creature("A", "1/1"), me);
		var b = Field(TestGames.Creature("B", "1/1"), me);
		Cast(Hand("Charge the Sanctum", me), [0]);
		Assert.That((s.Power(s.GetCard(a)), s.Power(s.GetCard(b))), Is.EqualTo((3, 3)));

		Cast(Hand("Charge the Sanctum", me), [1], a);
		var card = s.GetCard(a);
		Assert.That(
			(s.Power(card), card.PlusOneCounters, s.Has(card, "First strike")),
			Is.EqualTo((6, 1, true))
		);
	}

	[Test]
	public void CraftworkCrusher_ChoosesTwo_DamageAndACadet()
	{
		var victim = Field(TestGames.Creature("Victim", "4/4"), them);
		CastWithTrigger(Hand("Craftwork Crusher", me), [0, 1], victim);

		Assert.That(Alive(victim), Is.False);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Cadet"),
			Is.EqualTo(1)
		);
	}

	[Test]
	public void FateholdCharm_BouncesASpellOnTheStack()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var charm = Hand("Fatehold Charm", me);
		var gasp = Hand("Last Gasp", them);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = gasp,
				Targets = [bear],
			}
		);
		s = s.Act(new PassPriorityAction { PlayerId = them });

		Cast(charm, [1], gasp);

		Assert.That(s.IsIn(gasp, them, ZoneType.Hand), "back in its owner's hand");
		Assert.That(s.Toughness(s.GetCard(bear)), Is.EqualTo(2), "it never resolved");
	}

	[Test]
	public void FateholdCharm_DrawAndEmpower_OrTeamPlusOnePlusTwo()
	{
		var hand = HandCount(me);
		Cast(Hand("Fatehold Charm", me), [0]);
		Assert.That((HandCount(me), Jace(me)!.Loyalty), Is.EqualTo((hand + 1, 2)));

		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(Hand("Fatehold Charm", me), [2]);
		Assert.That((s.Power(s.GetCard(bear)), s.Toughness(s.GetCard(bear))), Is.EqualTo((3, 4)));
	}

	[Test]
	public void KonstrariCharm_SixToAFlierOnly_CountersAndTrample_OrThreeColourless()
	{
		var bird = Field(TestGames.Creature("Bird", "2/6", "Flying"), them);
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var charm = Hand("Konstrari Charm", me);
		Assert.That(
			Casts(charm).Where(a => a.Modes[0] == 0).Select(a => a.Targets[0]),
			Does.Contain(bird).And.Not.Contain(bear)
		);
		Cast(charm, [0], bird);
		Assert.That(Alive(bird), Is.False);

		var mine = Field(TestGames.Creature("Mine", "1/1"), me);
		Cast(Hand("Konstrari Charm", me), [1], mine);
		Assert.That(
			(s.GetCard(mine).PlusOneCounters, s.Has(s.GetCard(mine), "Trample")),
			Is.EqualTo((2, true))
		);

		Cast(Hand("Konstrari Charm", me), [2]);
		Assert.That(s.GetPlayer(me).ManaPool.Colorless, Is.EqualTo(3));
	}

	[Test]
	public void StingerquillCharm_ThreeAnywhere_FirstStrikeDeathtouch_OrAHastyCadet()
	{
		var theirs = Life(them);
		Cast(Hand("Stingerquill Charm", me), [0], them);
		Assert.That(Life(them), Is.EqualTo(theirs - 3));

		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(Hand("Stingerquill Charm", me), [1], bear);
		Assert.That(s.Has(s.GetCard(bear), "First strike") && s.Has(s.GetCard(bear), "Deathtouch"));

		Cast(Hand("Stingerquill Charm", me), [2]);
		var cadet = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Cadet");
		Assert.That(Combat.IsSummoningSick(s, cadet), "made this turn");
		Assert.That(Combat.CanAttack(s, cadet), "haste: it can attack anyway");
	}

	[Test]
	public void ArchiveArbiter_DestroysANoncreatureNonland_OrGainsFour()
	{
		var way = Field(FraCards.Get("Way of the Pyromancer"), them);
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		CastWithTrigger(Hand("Archive Arbiter", me), [0], way);
		Assert.That(Alive(way), Is.False);
		Assert.That(Alive(bear), "creatures are not targets");

		var life = Life(me);
		CastWithTrigger(Hand("Archive Arbiter", me), [1]);
		Assert.That(Life(me), Is.EqualTo(life + 4));
	}

	[Test]
	public void Yuriko_MinusXByGraveyard_OrSurveilTwo()
	{
		for (var i = 0; i < 3; i++)
			(s, _) = TestGames.Put(
				s,
				me,
				TestGames.Creature($"Dead{i}", "1/1"),
				ZoneType.Graveyard
			);
		var bear = Field(TestGames.Creature("Bear", "4/4"), them);
		CastWithTrigger(Hand("Yuriko, Hope from the Shadows", me), [0], bear);
		Assert.That(s.Power(s.GetCard(bear)), Is.EqualTo(4 - 3));

		var top2 = s.CardsIn(me, ZoneType.Library).Take(2).Select(c => c.Id).ToList();
		var yuriko = Hand("Yuriko, Hope from the Shadows", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = yuriko });
		Resolve(choice =>
			choice switch
			{
				ChooseModesChoice => [1],
				ChooseFromLibraryTopChoice => top2,
				_ => [],
			}
		);
		// The first Yuriko went to the graveyard too — the legend rule, as the second one entered.
		Assert.That(top2.All(id => s.IsIn(id, me, ZoneType.Graveyard)), "both surveiled cards");
	}

	// ===== Found by review =====

	// DistinctMoves keyed a cast without its modes, so two modes with the same targets became one
	// move: the AI and the console could never pick Rise of the Deathbringer's sweep.
	[Test]
	public void DistinctMoves_KeepsEveryMode()
	{
		var rise = Hand("Rise of the Deathbringer", me);
		var modes = s.DistinctMoves(s.LegalActions())
			.OfType<CastSpellAction>()
			.Where(a => a.CardId == rise)
			.Select(a => a.Modes[0]);
		Assert.That(modes, Is.EquivalentTo(new[] { 0, 1 }));
	}

	// CR 107.1b: a count that would be negative is 0. A -4 power once made "lose X life" a gain.
	[Test]
	public void ANegativeCount_IsZero()
	{
		var shrunk = Field(TestGames.Creature("Shrunk", "1/9"), me);
		s = s.UpdateObject(
			shrunk,
			(Card)s.GetCard(shrunk).WithComponent(new UntilEndOfTurn(-5, 0))
		); // TEST SETUP
		var (hand, life) = (HandCount(me), Life(me));

		Cast(Hand("Rise of the Deathbringer", me), [0]);

		Assert.That((HandCount(me), Life(me)), Is.EqualTo((hand, life)), "drew 0, lost 0");
	}

	// Choose-two modes were taken in printed order, unscored: with no opposing creature the AI's
	// Craftwork Crusher dealt 4 to one of its own.
	[Test]
	public void TheAiScoresEveryPairOfModes()
	{
		var crusher = Hand("Craftwork Crusher", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = crusher });
		for (var i = 0; i < 10 && s.GetPendingChoice() is not ChooseModesChoice; i++)
			s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });

		var (_, what) = new global::MagicCore.Ai.GreedyAi(me).Decide(s);

		Assert.That(what, Does.Not.Contain("deals 4 damage"), what);
	}

	// CR 700.2a: a "choose two" ability with fewer than two choosable modes is removed — it once
	// left a choice the console could never answer.
	[Test]
	public void ChooseTwo_WithOneChoosableMode_IsRemoved()
	{
		var needsTwo = TestGames.Creature("Picky", "1/1") with
		{
			Components =
			[
				new Effects.TriggeredAbilityComponent(Effects.TriggerEvent.SelfEnters, [])
				{
					Choose = 2,
					Modes =
					[
						new Effects.Mode(
							"hit",
							[
								new Targeting.TargetSpec(
									Targeting.TargetKind.Creature,
									Targeting.Whose.Opponent
								),
							],
							[new Effects.Tap(Effects.Ref.Target())]
						),
						new Effects.Mode("draw", [], [new Effects.DrawCards(Effects.Ref.You, 1)]),
					],
				},
			],
		};
		int picky;
		(s, picky) = TestGames.Put(s, me, needsTwo, ZoneType.Hand);

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = picky });
		for (var i = 0; i < 10 && !s.StackIsEmpty(); i++)
			s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });

		Assert.That(s.IsOnBattlefield(picky));
		Assert.That(
			(s.IsWaitingForChoice, s.StackIsEmpty()),
			Is.EqualTo((false, true)),
			"no unanswerable choice"
		);
	}
}
