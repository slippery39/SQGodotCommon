using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4j2b + j3, LSVPower — white's counter, cost and token options and the monarch, FIRING against their printed
/// text through real casting and the real stack: paying a player's energy (Guide of Souls), converge (Prismatic
/// Ending), incubate (Sunfall), Ajani's reflexive "if" and his −4's kept permanents, the monarch (Palace Jailer).
/// </summary>
public class LsvBatch2j2bTests : FraBoardTest
{
	const string Ajani = "Ajani, Nacatl Pariah // Ajani, Nacatl Avenger";

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition Red =>
		new(TestGames.Creature("Goblin", "1/1").Data with { Colors = ["R"] }, []);

	int Energy(int p) => s.GetPlayer(p).CountersOf(CounterKinds.Energy);

	int Named(int p, string name) => s.CardsIn(p, ZoneType.Battlefield).Count(c => c.Name == name);

	// ===== Guide of Souls =====

	[Test]
	public void GuideOfSouls_ACreatureEnters_LifeAndEnergy_ThenOnAttackPaysThree_CountersFlyingAngel()
	{
		var guide = Field(Lsv("Guide of Souls"), me);
		s = s.PutCounters(me, CounterKinds.Energy, 2); // TEST SETUP
		var life = Life(me);

		Cast(Hand(Bear, me), []);
		Assert.That((Life(me), Energy(me)), Nu.EqualTo((life + 1, 3)), "1 life and {E}");

		AttackWith(Targeting(guide, yes: true), guide);

		var card = s.GetCard(guide);
		Assert.That(Energy(me), Nu.EqualTo(0), "paid {E}{E}{E}");
		Assert.That(card.PlusOneCounters, Nu.EqualTo(2));
		Assert.That(s.Has(card, "Flying"), "a flying counter");
		Assert.That(
			s.HasSubtype(card, "Angel") && s.HasSubtype(card, "Cleric"),
			"an Angel in addition"
		);
	}

	[Test]
	public void GuideOfSouls_WithTwoEnergy_NotAsked()
	{
		var guide = Field(Lsv("Guide of Souls"), me);
		s = s.PutCounters(me, CounterKinds.Energy, 2); // TEST SETUP

		AttackWith(Targeting(guide, yes: true), guide);

		Assert.That((Energy(me), s.GetCard(guide).PlusOneCounters), Nu.EqualTo((2, 0)));
	}

	// ===== Prismatic Ending =====

	void Ending(int x, int target)
	{
		var ending = Hand(Lsv("Prismatic Ending"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = ending,
				X = x,
				Targets = [target],
			}
		);
		Resolve();
	}

	[Test]
	public void PrismaticEnding_XOfTwo_SpendsThreeColours_ExilesManaValueThree()
	{
		var three = Field(TestGames.CreatureCosting("{2}{B}", "Three", "3/3"), them);

		Ending(2, three);

		Assert.That(InExile(three), "{2}{W} paid in three colours");
	}

	[Test]
	public void PrismaticEnding_ManaValueAboveTheColours_Stays()
	{
		var three = Field(TestGames.CreatureCosting("{2}{B}", "Three", "3/3"), them);

		Ending(1, three);

		Assert.That(Alive(three), "two colours: mana value 3 is too much");
	}

	// ===== Sunfall =====

	[Test]
	public void Sunfall_ExilesAllCreatures_AnIncubatorWithThatMany_TransformsToAPhyrexian()
	{
		var creatures = new[] { Field(Bear, me), Field(Bear, me), Field(Bear, them) };

		Cast(Hand(Lsv("Sunfall"), me), []);

		Assert.That(creatures.All(InExile));
		var incubator = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Incubator");
		Assert.That(
			incubator.PlusOneCounters,
			Nu.EqualTo(3),
			"X +1/+1 counters, X the creatures exiled"
		);
		Assert.That(incubator.Is(CardType.Creature), Nu.False);

		s = s.Act(new ActivateAbilityAction { PlayerId = me, CardId = incubator.Id });
		Resolve();

		var phyrexian = s.GetCard(incubator.Id);
		Assert.That(phyrexian.Is(CardType.Artifact) && phyrexian.Is(CardType.Creature));
		Assert.That(s.HasSubtype(phyrexian, "Phyrexian"));
		Assert.That(
			(s.Power(phyrexian), s.Toughness(phyrexian)),
			Nu.EqualTo((3, 3)),
			"a 0/0 with its counters"
		);
	}

	// ===== Ajani, Nacatl Pariah // Ajani, Nacatl Avenger =====

	int Transformed(int loyalty)
	{
		var card = Field(Lsv(Ajani), me);
		s = s.UpdateObject(card, s.GetCard(card).BackFaceUp()); // TEST SETUP: transformed
		s = s.PutCounters(card, CounterKinds.Loyalty, loyalty);
		return card;
	}

	void Loyalty(int walker, int ability, Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		s = s.Act(
			new ActivateLoyaltyAction
			{
				PlayerId = me,
				CardId = walker,
				AbilityIndex = ability,
			}
		);
		Resolve(answer);
	}

	[Test]
	public void Ajani_EntersWithACat_TheCatDies_HeReturnsTransformed()
	{
		var ajani = Hand(Lsv(Ajani), me);
		Cast(ajani, []);
		var cat = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Cat Warrior");
		Assert.That((s.Power(cat), s.Toughness(cat)), Nu.EqualTo((2, 1)));

		var kill = Hand(TestCards.Kill(), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = kill,
				Targets = [cat.Id],
			}
		);
		Resolve(Targeting(0, yes: true));

		Assert.That(
			Alive(ajani) && s.GetCard(ajani).Is(CardType.Planeswalker),
			"Ajani, Nacatl Avenger"
		);
	}

	[Test]
	public void AjaniAvenger_Zero_ACat_NoRedPermanent_NoDamage()
	{
		var ajani = Transformed(3);
		var life = Life(them);

		s = s.Act(
			new ActivateLoyaltyAction
			{
				PlayerId = me,
				CardId = ajani,
				AbilityIndex = 1,
			}
		);
		var asked = ResolveAsking(Targeting(them, yes: false));

		Assert.That(Named(me, "Cat Warrior"), Nu.EqualTo(1));
		Assert.That(
			asked.OfType<ChooseTargetChoice>(),
			Nu.Empty,
			"the intervening if: nothing triggers"
		);
		Assert.That(Life(them), Nu.EqualTo(life), "no red permanent other than Ajani");
	}

	[Test]
	public void AjaniAvenger_Zero_WithARedPermanent_DamageForEachCreatureIControl()
	{
		var ajani = Transformed(3);
		Field(Red, me);
		var life = Life(them);

		Loyalty(ajani, 1, Targeting(them, yes: false));

		Assert.That(Life(them), Nu.EqualTo(life - 2), "the Goblin and the new Cat");
	}

	[Test]
	public void AjaniAvenger_MinusFour_TheyKeepOneOfEachType_SacrificeTheRest()
	{
		var ajani = Transformed(4);
		var golem = Field(
			new(
				TestGames.Inline("Golem", "{2}", "Artifact Creature — Golem").Data with
				{
					Power = "3",
					Toughness = "3",
				},
				[]
			),
			them
		);
		var bears = new[] { Field(Bear, them), Field(Bear, them) };
		var aura = Field(TestGames.Inline("Charm", "{1}", "Enchantment"), them);
		int Lands() => s.CardsIn(them, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		var lands = Lands();

		Loyalty(ajani, 2); // each keep: the first offered — the Golem as the artifact AND the creature

		Assert.That(Alive(golem) && Alive(aura), "kept");
		Assert.That(bears.Any(Alive), Nu.False, "the rest sacrificed");
		Assert.That(Lands(), Nu.EqualTo(lands), "lands aren't among them");
	}

	// ===== Palace Jailer, the monarch =====

	int Monarch => DesignationComponent.HolderOf(s, DesignationComponent.Monarch);

	int Jailer(int target)
	{
		var jailer = Hand(Lsv("Palace Jailer"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = jailer });
		Resolve(Targeting(target, yes: false));
		return jailer;
	}

	[Test]
	public void PalaceJailer_IBecomeTheMonarch_TheirCreatureStaysExiled_UntilAnOpponentBecomesIt()
	{
		var bear = Field(Bear, them);
		var jailer = Jailer(bear);
		Assert.That(Monarch, Nu.EqualTo(me));
		Assert.That(InExile(bear));

		Cast(Hand(TestCards.Kill(), me), [], jailer);
		Assert.That(InExile(bear), "not until the Jailer leaves");

		s = DesignationComponent.Take(s, them, DesignationComponent.Monarch);

		Assert.That(
			s.IsIn(bear, them, ZoneType.Battlefield),
			"they became the monarch: it returns"
		);
	}

	[Test]
	public void TheMonarch_AtMyEndStep_IDraw()
	{
		Jailer(Field(Bear, them));
		var hand = HandCount(me);

		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.Cleanup);

		Assert.That(HandCount(me), Nu.EqualTo(hand + 1));
	}

	[Test]
	public void TheMonarch_CombatDamageToIt_TheAttackerBecomesIt()
	{
		s = DesignationComponent.Take(s, them, DesignationComponent.Monarch); // TEST SETUP
		var bear = Field(Bear, me);

		AttackWith(c => c.Options.Take(c.MinChoices).Select(o => o.Id), bear);

		Assert.That(Monarch, Nu.EqualTo(me));
		Assert.That(
			s.CardsIn(them, ZoneType.Command).Any(c => c.Name == DesignationComponent.Monarch),
			Nu.False
		);
	}
}
