using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;

namespace MagicCore.Tests.Sets;

/// <summary>Flashback and targeting cards in a graveyard: each card's ability fires, and flashback exiles.</summary>
public class FraFlashbackTests : FraBoardTest
{
	int InGraveyard(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Graveyard);
		return id;
	}

	int InGraveyard(string fra) => InGraveyard(CardRegistry.Get(fra));

	bool InExile(int card) => s.IsIn(card, me, ZoneType.Exile);

	[Test]
	public void GenerousRevival_ReturnsACheapCreatureCardWithACounter()
	{
		var cheap = InGraveyard(TestGames.CreatureCosting("{1}{W}", "Cheap", "2/2"));
		var dear = InGraveyard(TestGames.CreatureCosting("{3}{W}", "Dear", "4/4"));
		var revival = Hand("Generous Revival", me);
		Assert.That(
			Casts(revival).Select(c => c.Targets[0]),
			Is.EquivalentTo(new[] { cheap }),
			"mana value 3 or less"
		);

		Cast(revival, [], cheap);

		Assert.That(Alive(cheap));
		Assert.That(s.GetCard(cheap).PlusOneCounters, Is.EqualTo(1));
		Assert.That(s.IsIn(revival, me, ZoneType.Graveyard), "cast from hand: to the graveyard");
		Assert.That(Alive(dear), Is.False);
	}

	[Test]
	public void Flashback_CastsFromTheGraveyardForItsCost_ThenExiles()
	{
		var incursion = InGraveyard("Bestial Incursion");
		Assert.That(Costs.Of(s, me, s.GetCard(incursion)).ManaValue, Is.EqualTo(6), "{5}{G}");

		Cast(incursion, []);

		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Beast"),
			Is.EqualTo(1)
		);
		Assert.That(InExile(incursion));
	}

	[Test]
	public void Flashback_KeepsItsCardTypesTiming()
	{
		var incursion = InGraveyard("Bestial Incursion");
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = them }); // to beginning of combat
		Assert.That(Casts(incursion), Is.Empty, "a sorcery, from the graveyard too");
	}

	[Test]
	public void Flashback_AFizzledSpellIsExiledToo()
	{
		var target = InGraveyard(TestGames.CreatureCosting("{1}{W}", "Cheap", "2/2"));
		var revival = InGraveyard("Generous Revival");

		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = revival,
				Targets = [target],
			}
		);
		s = s.MoveCard(target, s.ZoneId(me, ZoneType.Exile)); // TEST SETUP: its target leaves
		Resolve();

		Assert.That(InExile(revival), "any time it would leave the stack (CR 702.34a)");
	}

	[Test]
	public void PredictivePreparations_OneOrTwoDifferentTargets()
	{
		var a = Field(TestGames.Creature("A", "2/2"), me);
		var b = Field(TestGames.Creature("B", "2/2"), me);
		var preparations = Hand("Predictive Preparations", me);
		Assert.That(
			Casts(preparations).Any(c => c.Targets[0] == c.Targets[1]),
			Is.False,
			"not the same creature twice"
		);

		Cast(preparations, [], a, b);
		Assert.That(
			(s.GetCard(a).PlusOneCounters, s.GetCard(b).PlusOneCounters),
			Is.EqualTo((1, 1))
		);

		Cast(preparations, [], a, 0); // flashback, one target
		Assert.That(s.GetCard(a).PlusOneCounters, Is.EqualTo(2));
		Assert.That(InExile(preparations));
	}

	[Test]
	public void StingcasterMage_GivesAnInstantFlashbackForItsManaCost_ThisTurnOnly()
	{
		var instant = InGraveyard(
			TestGames.Inline("Gift", "{1}{U}", "Instant", null, new GainLife(Ref.You, 3))
		);
		var mage = Hand("Stingcaster Mage", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = mage });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [instant],
				_ => [],
			}
		);
		Assert.That(Costs.Of(s, me, s.GetCard(instant)).ManaValue, Is.EqualTo(2), "its mana cost");

		var life = Life(me);
		Cast(instant, []);
		Assert.That((Life(me), InExile(instant)), Is.EqualTo((life + 3, true)));
	}

	[Test]
	public void StingcasterMage_TheFlashbackEndsWithTheTurn()
	{
		var instant = InGraveyard(
			TestGames.Inline("Gift", "{1}{U}", "Instant", null, new GainLife(Ref.You, 3))
		);
		Cast(Hand("Stingcaster Mage", me), []);
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [instant],
				_ => [],
			}
		);

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.Upkeep && x.GetGame().ActivePlayerId == them
		);
		Assert.That(Costs.Flashback(s, s.GetCard(instant)), Is.Null);
	}

	[Test]
	public void RecursiveRecruitment_FromTheGraveyard_CountersPerThreeCards()
	{
		var recruitment = Hand("Recursive Recruitment", me);
		Cast(recruitment, []);
		var cadets = s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Name == "Cadet").ToList();
		Assert.That(cadets.Select(c => c.PlusOneCounters), Is.EqualTo(new[] { 0, 0 }), "from hand");

		for (var i = 0; i < 6; i++)
			InGraveyard(TestGames.Card("Plains"));
		Cast(recruitment, []); // flashback: six cards in the graveyard (it is on the stack)

		var fresh = s.CardsIn(me, ZoneType.Battlefield)
			.Where(c => c.Name == "Cadet" && !cadets.Any(o => o.Id == c.Id))
			.ToList();
		Assert.That(fresh.Select(c => c.PlusOneCounters), Is.EqualTo(new[] { 2, 2 }));
	}

	[Test]
	public void TwinnedVision_DrawsOneFromHand_TwoByFlashbackForADiscard()
	{
		var vision = Hand("Twinned Vision", me);
		var hand = HandCount(me);
		Cast(vision, []);
		Assert.That(HandCount(me), Is.EqualTo(hand - 1 + 1));

		var fodder = Hand(TestGames.Card("Forest"), me);
		Assert.That(Casts(vision).All(c => c.DiscardId != 0), "flashback owes a discard");
		hand = HandCount(me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = vision,
				DiscardId = fodder,
			}
		);
		Resolve();

		Assert.That(HandCount(me), Is.EqualTo(hand - 1 + 2));
		Assert.That(
			(s.IsIn(fodder, me, ZoneType.Graveyard), InExile(vision)),
			Is.EqualTo((true, true))
		);
	}

	[Test]
	public void StingcasterMage_OnTwinnedVision_ItsManaCostAndNoDiscard()
	{
		var vision = InGraveyard("Twinned Vision");
		Cast(Hand("Stingcaster Mage", me), []);
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [vision],
				_ => [],
			}
		);

		Assert.That(Costs.Of(s, me, s.GetCard(vision)).ManaValue, Is.EqualTo(2), "{1}{U/R}");
		Assert.That(
			Casts(vision).Select(c => c.DiscardId),
			Is.EqualTo(new[] { 0 }),
			"no discard owed"
		);
	}
}
