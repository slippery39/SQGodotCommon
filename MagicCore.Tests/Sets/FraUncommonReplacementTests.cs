using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets.Fra;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Uncommons (7e-2d): replacements, -1/-1 counters, combat damage — each ability fires.</summary>
public class FraUncommonReplacementTests : FraBoardTest
{
	static CardDefinition Ping(Ref to, int n) =>
		TestGames.Inline("Ping", "{1}", "Instant", null, new DealDamage(to, n));

	void AttackWith(params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([attackers.Contains(declare.CreatureId) ? them : CombatOption.None]);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);
	}

	int Walker(int player, int loyalty)
	{
		var walker = Field(TestGames.Planeswalker("Walker", loyalty), player);
		s = s.UpdateObject(walker, s.GetCard(walker) with { Loyalty = loyalty }); // TEST SETUP: Put skips entering
		return walker;
	}

	[Test]
	public void TomikIzzet_NoncombatDamageToThemIsOneMore()
	{
		Field(FraCards.Get("Tomik, Izzet Sparkmage"), me);
		var theirs = Field(TestGames.Creature("Theirs", "2/3"), them);
		var life = Life(them);
		Cast(Hand(Ping(Ref.EachOpponent, 1), me), []);
		Assert.That(Life(them), Is.EqualTo(life - (1 + 1)));
		Cast(Hand(Ping(Ref.OpponentsPermanents(CardType.Creature), 2), me), []);
		Assert.That(s.IsIn(theirs, them, ZoneType.Graveyard), "2 + 1 kills a 2/3");
	}

	[Test]
	public void TomikIzzet_NotToYourOwn_NorInCombat()
	{
		Field(FraCards.Get("Tomik, Izzet Sparkmage"), me);
		var mine = Field(TestGames.Creature("Mine", "2/3"), me);
		Cast(Hand(Ping(new Ref(RefKind.YourPermanents, Type: CardType.Creature), 2), me), []);
		Assert.That(s.GetCard(mine).Damage, Is.EqualTo(2));

		var life = Life(them);
		AttackWith(mine);
		Assert.That(Life(them), Is.EqualTo(life - 2), "combat damage isn't replaced");
	}

	[Test]
	public void Yoshimaru_OneMoreCounterOnYourCreatures_NotTheirs()
	{
		Field(FraCards.Get("Yoshimaru, Beloved Companion"), me);
		var mine = Field(TestGames.Creature("Mine", "2/2"), me);
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		var grow = new AddCounters(Ref.AllCreatures, 1);
		Cast(Hand(TestGames.Inline("Grow", "{1}", "Instant", null, grow), me), []);
		Assert.That(
			(s.GetCard(mine).PlusOneCounters, s.GetCard(theirs).PlusOneCounters),
			Is.EqualTo((1 + 1, 1))
		);
	}

	[Test]
	public void Yoshimaru_EnteringWithCountersIsPuttingThem()
	{
		Field(FraCards.Get("Yoshimaru, Beloved Companion"), me);
		var make = new CreateTokens(FraCards.Cadet, 1) { Counters = 1 };
		Cast(Hand(TestGames.Inline("Make", "{1}", "Sorcery", null, make), me), []);
		var cadet = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Cadet");
		Assert.That(cadet.PlusOneCounters, Is.EqualTo(1 + 1));
	}

	[Test]
	public void Yoshimaru_HisOwnReplacementDoesntApplyAsHeEnters()
	{
		int yoshi;
		(s, yoshi) = TestGames.Put(
			s,
			me,
			FraCards.Get("Yoshimaru, Beloved Companion"),
			ZoneType.Graveyard
		);
		var back = new ReturnFromGraveyard(Ref.CardsInYourGraveyard, ZoneType.Battlefield)
		{
			Counters = 1,
		};
		Cast(Hand(TestGames.Inline("Back", "{1}", "Sorcery", null, back), me), []);
		Assert.That(s.GetCard(yoshi).PlusOneCounters, Is.EqualTo(1), "CR 614.12");
	}

	[Test]
	public void GraftSurgeon_ItsCountersIncludeMinusOnes()
	{
		var surgeon = Field(FraCards.Get("Graft Surgeon"), me);
		s = s.UpdateObject(surgeon, s.GetCard(surgeon) with { MinusOneCounters = 1 }); // TEST SETUP
		var bear = Field(TestGames.Creature("Bear", "3/3"), me);
		var kill = new DealDamage(Ref.Target(), 5);
		var spell = Hand(
			new CardDefinition(
				TestGames.Inline("Kill", "{1}", "Instant").Data,
				[
					new SpellEffectsComponent([kill])
					{
						Targets = [new Targeting.TargetSpec(Targeting.TargetKind.Creature)],
					},
				]
			),
			me
		);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = spell,
				Targets = [surgeon],
			}
		);
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [bear],
				_ => [],
			}
		);
		Assert.That(s.GetCard(bear).MinusOneCounters, Is.EqualTo(1));
	}

	[Test]
	public void Yoshimaru_SixForACounterOnALegend()
	{
		var yoshi = Field(FraCards.Get("Yoshimaru, Beloved Companion"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Assert.That(
			Activations(yoshi).Select(a => a.Targets[0]),
			Does.Contain(yoshi).And.Not.Contain(bear)
		);
		s = s.Act(Activations(yoshi).Single());
		Resolve();
		Assert.That(s.GetCard(yoshi).PlusOneCounters, Is.EqualTo(1 + 1), "and his own replacement");
	}

	[Test]
	public void Mabel_StripsTheirHelpfulCounters_NotTheirStun()
	{
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		s = s.UpdateObject(
			theirs,
			s.GetCard(theirs) with
			{
				PlusOneCounters = 2,
				StunCounters = 1,
			}
		); // TEST SETUP
		CastWithTrigger(Hand("Mabel, Bitter Recluse", me), [], theirs);
		var card = s.GetCard(theirs);
		Assert.That((card.PlusOneCounters, card.StunCounters), Is.EqualTo((0, 1)));
	}

	[Test]
	public void Mabel_ClearsYourHarmfulCounters_UpToThree()
	{
		var mine = Field(TestGames.Creature("Mine", "2/2"), me);
		s = s.UpdateObject(mine, s.GetCard(mine) with { StunCounters = 4, PlusOneCounters = 1 }); // TEST SETUP
		CastWithTrigger(Hand("Mabel, Bitter Recluse", me), [], mine);
		var card = s.GetCard(mine);
		Assert.That((card.StunCounters, card.PlusOneCounters), Is.EqualTo((4 - 3, 1)));
	}

	[Test]
	public void Mabel_TakesLoyaltyFromTheirPlaneswalker()
	{
		var walker = Walker(them, 5);
		CastWithTrigger(Hand("Mabel, Bitter Recluse", me), [], walker);
		Assert.That(s.GetCard(walker).Loyalty, Is.EqualTo(5 - 3));
	}

	[Test]
	public void HapatraDesertFang_MinusCountersByYourGreatestManaValue()
	{
		(s, _) = TestGames.Put(
			s,
			me,
			TestGames.CreatureCosting("{4}", "Big", "4/4"),
			ZoneType.Graveyard
		);
		(s, _) = TestGames.Put(
			s,
			me,
			TestGames.CreatureCosting("{1}", "Small", "1/1"),
			ZoneType.Graveyard
		);
		var theirs = Field(TestGames.Creature("Theirs", "5/5"), them);
		CastWithTrigger(Hand("Hapatra, the Desert Fang", me), [], theirs);
		var card = s.GetCard(theirs);
		Assert.That(
			(card.MinusOneCounters, s.Power(card), s.Toughness(card)),
			Is.EqualTo((4, 1, 1))
		);
	}

	[Test]
	public void MinusCounters_CancelPlusCounters()
	{
		(s, _) = TestGames.Put(
			s,
			me,
			TestGames.CreatureCosting("{3}", "Big", "3/3"),
			ZoneType.Graveyard
		);
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		s = s.UpdateObject(theirs, s.GetCard(theirs) with { PlusOneCounters = 2 }); // TEST SETUP
		CastWithTrigger(Hand("Hapatra, the Desert Fang", me), [], theirs);
		var card = s.GetCard(theirs);
		Assert.That((card.PlusOneCounters, card.MinusOneCounters), Is.EqualTo((0, 1)), "CR 704.5q");
	}

	[Test]
	public void Loot_NegativePowerDealsItsSize_AndSacrificingMakesItBigger()
	{
		var loot = Field(FraCards.Get("Loot, the Anomaly"), me);
		var fodder = Field(TestGames.Creature("Fodder", "1/1"), me);
		Assert.That(Activations(loot), Is.Empty, "threshold");
		for (var i = 0; i < 7; i++)
			(s, _) = TestGames.Put(s, me, TestGames.Creature("Dead", "1/1"), ZoneType.Graveyard);
		s = s.Act(Activations(loot).Single(a => a.SacrificeId == fodder));
		Resolve();
		Assert.That(s.Power(s.GetCard(loot)), Is.EqualTo(-2 - 2));

		var life = Life(them);
		AttackWith(loot);
		Assert.That(Life(them), Is.EqualTo(life - 4));
	}

	[Test]
	public void Ghalta_CostsLessByToughness_DefendersAttack_ToughnessHits()
	{
		var wall = Field(TestGames.Creature("Wall", "0/6", "Defender"), me);
		var ghalta = Hand("Ghalta the Immovable", me);
		var printed = s.GetCard(ghalta).ManaCost.ManaValue;
		Assert.That(Costs.Of(s, me, s.GetCard(ghalta)).ManaValue, Is.EqualTo(printed - 6));

		Field(FraCards.Get("Ghalta the Immovable"), me);
		var life = Life(them);
		AttackWith(wall);
		Assert.That(Life(them), Is.EqualTo(life - 6));
	}
}
