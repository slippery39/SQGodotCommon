using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets.Fra;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Auras and Equipment (Rules/Attachments.cs): attach, grant live, fall off — and each card fires.</summary>
public class FraAttachmentTests : FraBoardTest
{
	void Equip(int equipment, int creature)
	{
		s = s.Act(Activations(equipment).Single(a => a.Targets[0] == creature));
		Resolve();
	}

	void AttackWithAll()
	{
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice)
			s = s.Choose([them]);
	}

	[Test]
	public void MedicsKitesail_EquippedGetsPlusOneAndFlying_AndGainsOneAttacking()
	{
		var kitesail = Field(FraCards.Get("Medic's Kitesail"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Equip(kitesail, bear);
		Assert.That(
			(
				s.GetCard(kitesail).AttachedToId,
				s.Power(s.GetCard(bear)),
				s.Has(s.GetCard(bear), "Flying")
			),
			Is.EqualTo((bear, 3, true))
		);

		var life = Life(me);
		AttackWithAll();
		Resolve();
		Assert.That(Life(me), Is.EqualTo(life + 1), "this creature attacked: gain 1");
	}

	[Test]
	public void Equipment_ItsBonusIsLive_ItMovesWithTheEquipment()
	{
		var kitesail = Field(FraCards.Get("Medic's Kitesail"), me);
		var first = Field(TestGames.Creature("First", "2/2"), me);
		var second = Field(TestGames.Creature("Second", "2/2"), me);
		Equip(kitesail, first);
		Equip(kitesail, second);
		Assert.That(
			(s.Has(s.GetCard(first), "Flying"), s.Has(s.GetCard(second), "Flying")),
			Is.EqualTo((false, true)),
			"never stamped — read from where it is attached"
		);
	}

	[Test]
	public void Equipment_StaysWhenItsCreatureDies_Unattached()
	{
		var kitesail = Field(FraCards.Get("Medic's Kitesail"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Equip(kitesail, bear);
		s = s.Sacrifice(bear);
		s = StateBasedActions.Check(s);
		Assert.That((Alive(kitesail), s.GetCard(kitesail).AttachedToId), Is.EqualTo((true, 0)));
	}

	[Test]
	public void Equip_OnlyYourCreatures_OnlyAsASorcery()
	{
		var kitesail = Field(FraCards.Get("Medic's Kitesail"), me);
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		Field(TestGames.Creature("Mine", "2/2"), me);
		Assert.That(Activations(kitesail).Select(a => a.Targets[0]), Does.Not.Contain(theirs));

		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = them }); // beginning of combat
		Assert.That(Activations(kitesail), Is.Empty);
	}

	[Test]
	public void FerocityOfTheHunt_EnchantsAndGrants_AndReturnsItsCreatureTappedWhenItDies()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var ferocity = Hand("Ferocity of the Hunt", me);
		Cast(ferocity, [], bear);
		Assert.That(
			(
				s.GetCard(ferocity).AttachedToId,
				s.Power(s.GetCard(bear)),
				s.Has(s.GetCard(bear), "Deathtouch")
			),
			Is.EqualTo((bear, 3, true))
		);

		s = s.Sacrifice(bear);
		s = s.Act(new PassPriorityAction { PlayerId = me }); // the state check takes the Aura; its trigger looks back
		Resolve();

		Assert.That(
			(Alive(bear), s.GetCard(bear).IsTapped),
			Is.EqualTo((true, true)),
			"returned tapped"
		);
		Assert.That(s.IsIn(ferocity, me, ZoneType.Graveyard));
	}

	[Test]
	public void AnAura_CantBeCastWithNothingToEnchant()
	{
		var ferocity = Hand("Ferocity of the Hunt", me);
		foreach (
			var creature in new[] { me, them }
				.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield))
				.Where(c => c.Is(CardType.Creature))
				.ToList()
		)
			s = s.MoveCard(creature.Id, s.ZoneId(creature.OwnerId, ZoneType.Exile)); // TEST SETUP
		Assert.That(Casts(ferocity), Is.Empty);
	}

	[Test]
	public void AnAura_WhoseTargetLeft_DoesntResolve()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var ferocity = Hand("Ferocity of the Hunt", me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = ferocity,
				Targets = [bear],
			}
		);
		s = s.MoveCard(bear, s.ZoneId(me, ZoneType.Exile)); // TEST SETUP: gone in response
		var onTheStack = s.GetCard(ferocity).Incarnation;
		Resolve();
		Assert.That(s.IsIn(ferocity, me, ZoneType.Graveyard), "CR 608.3b");
		Assert.That(
			s.GetCard(ferocity).Incarnation,
			Is.EqualTo(onTheStack + 1),
			"straight to the graveyard — it never entered the battlefield (no enters triggers)"
		);
	}

	[Test]
	public void AnAura_StaysOnWhenItsCreatureGainsHexproofLater()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var ferocity = Hand("Ferocity of the Hunt", them);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = ferocity,
				Targets = [bear],
			}
		);
		Resolve();
		s = s.UpdateObject(
			bear,
			(Card)s.GetCard(bear).WithComponent(new UntilEndOfTurn(Keyword: "Hexproof"))
		); // TEST SETUP
		s = StateBasedActions.Check(s);
		Assert.That(Alive(ferocity), "enchant is not targeting: hexproof doesn't knock it off");
	}

	[Test]
	public void HuntersAxe_TheAttackerGainsTheChosenKeyword()
	{
		var axe = Field(FraCards.Get("Hunter's Axe"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Equip(axe, bear);
		Assert.That(s.Power(s.GetCard(bear)), Is.EqualTo(4));

		AttackWithAll();
		Resolve(choice =>
			choice switch
			{
				ChooseModesChoice => [1], // deathtouch
				_ => [],
			}
		);
		Assert.That(
			(s.Has(s.GetCard(bear), "Deathtouch"), s.Has(s.GetCard(bear), "Trample")),
			Is.EqualTo((true, false))
		);
	}

	[Test]
	public void WarriorsBlades_EntersForThreeAndThree_AndEquipsCheaperPerCounter()
	{
		var life = (Life(me), Life(them));
		var blades = Hand("Warrior's Blades", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = blades });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [them],
				_ => [],
			}
		);
		Assert.That((Life(me), Life(them)), Is.EqualTo((life.Item1 + 3, life.Item2 - 3)));

		var countered = Field(TestGames.Creature("Countered", "2/2"), me);
		var plain = Field(TestGames.Creature("Plain", "2/2"), me);
		s = s.UpdateObject(countered, s.GetCard(countered) with { PlusOneCounters = 2 }); // TEST SETUP
		var island = s.CardsIn(me, ZoneType.Battlefield)
			.First(c => c.Subtypes.Contains("Island") && !c.IsTapped);
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Land) && c.Id != island.Id)
				.ToList()
		)
			s = s.UpdateObject(land.Id, land with { IsTapped = true }); // TEST SETUP: one mana left

		var targets = Activations(blades).Select(a => a.Targets[0]).ToList();
		Assert.That(
			targets,
			Does.Contain(countered).And.Not.Contain(plain),
			"{3} less 2 counters = {1}"
		);
	}

	[Test]
	public void LichsRelic_PayTwo_ThenDestroyTheirCreature()
	{
		var theirs = Field(TestGames.Creature("Theirs", "3/3"), them);
		var relic = Hand("Lich's Relic", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = relic });
		Resolve(choice =>
			choice switch
			{
				YesNoChoice => [1],
				ChooseTargetChoice => [theirs],
				_ => [],
			}
		);
		Assert.That(Alive(theirs), Is.False);
	}

	[Test]
	public void LichsRelic_Declined_NothingDies()
	{
		var theirs = Field(TestGames.Creature("Theirs", "3/3"), them);
		Cast(Hand("Lich's Relic", me), []); // the default answer declines
		Assert.That(Alive(theirs));
	}

	[Test]
	public void AnAuraReturnedUncast_ChoosesWhatItEnchants()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		int ferocity;
		(s, ferocity) = TestGames.Put(
			s,
			me,
			FraCards.Get("Ferocity of the Hunt"),
			ZoneType.Graveyard
		);
		var asked = false;

		var realms = Hand("Return to the Light Realms", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = realms });
		Resolve(choice =>
		{
			if (choice is ChooseHostChoice)
			{
				asked = true;
				return [bear];
			}
			return [];
		});

		Assert.That(asked, "CR 303.4f: its controller chooses");
		Assert.That((Alive(ferocity), s.GetCard(ferocity).AttachedToId), Is.EqualTo((true, bear)));
	}

	[Test]
	public void AHostThatLeftAndCameBack_IsANewObject_NothingIsOnIt()
	{
		var kitesail = Field(FraCards.Get("Medic's Kitesail"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Equip(kitesail, bear);

		s = s.MoveCard(bear, s.ZoneId(me, ZoneType.Exile)); // TEST SETUP: it leaves and returns
		s = s.EnterBattlefield(bear, me); // in one go — before any state-based check

		Assert.That(s.Has(s.GetCard(bear), "Flying"), Is.False, "same id, new object (CR 400.7)");
	}
}
