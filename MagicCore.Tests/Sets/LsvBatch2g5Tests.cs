using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Sets;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4g5, LSVPower — Adventures (CR 715): Brazen Borrower // Petty Theft, each FIRING against its printed text
/// through real casting and the real stack.
/// </summary>
public class LsvBatch2g5Tests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition Bird => TestGames.Creature("Bird", "1/1", "Flying");

	IEnumerable<CastSpellAction> AsAdventure(int card) => Casts(card).Where(c => c.Face > 0);

	[Test]
	public void PettyTheft_ReturnsTheirNonlandPermanent_ThenTheCreatureIsCastFromExile()
	{
		var bear = Field(Bear, them);
		Field(Bear, me);
		var borrower = Hand(Lsv("Brazen Borrower // Petty Theft"), me);
		Assert.That(
			AsAdventure(borrower).Select(c => c.Targets.Single()),
			Nu.EquivalentTo(new[] { bear }),
			"target nonland permanent an opponent controls"
		);
		var cast = AsAdventure(borrower).Single();
		Assert.That(
			cast.PaymentPreview(s)!.Count(p => !p.Floats),
			Nu.EqualTo(2),
			"the table previews {1}{U}, not {1}{U}{U}"
		);
		Assert.That(Describe.Action(s, cast), Nu.EqualTo("Cast Petty Theft (Adventure) → Bear"));
		var lands = UntappedLands();

		s = s.Act(cast);

		var spell = s.GetCard(borrower);
		Assert.That(
			(spell.Name, spell.Is(CardType.Instant), spell.Is(CardType.Creature)),
			Nu.EqualTo(("Petty Theft", true, false)),
			"on the stack it is the Adventure"
		);
		Assert.That(lands - UntappedLands(), Nu.EqualTo(2), "{1}{U}");
		Assert.That(s.GetPlayer(me).ThisTurn.NoncreatureSpellsCast, Nu.EqualTo(1));
		Resolve();

		Assert.That(s.IsIn(bear, them, ZoneType.Hand), "to its owner's hand");
		Assert.That(InExile(borrower), "then exile this card");
		Assert.That(Casts(borrower), Nu.Not.Empty, "you may cast the creature later from exile");
		Field(Bear, them); // something to steal
		Assert.That(AsAdventure(borrower), Nu.Empty, "never the Adventure again from there");

		Cast(borrower, []);

		var creature = s.GetCard(borrower);
		Assert.That(
			(Alive(borrower), creature.Name, s.Power(creature), s.Has(creature, "Flying")),
			Nu.EqualTo((true, "Brazen Borrower // Petty Theft", 3, true))
		);
	}

	[Test]
	public void PettyTheft_Fizzled_GoesToTheGraveyardAsTheCreature()
	{
		var bear = Field(Bear, them);
		var borrower = Hand(Lsv("Brazen Borrower // Petty Theft"), me);
		s = s.Act(AsAdventure(borrower).Single());
		s = s.MoveCard(bear, s.ZoneId(them, ZoneType.Hand)); // its target gone before it resolves

		Resolve();

		Assert.That(InGraveyard(borrower), "it didn't resolve: no adventure");
		Assert.That(s.GetCard(borrower).Is(CardType.Creature), "the card again, off the stack");
		Assert.That(Casts(borrower), Nu.Empty);
	}

	[Test]
	public void BrazenBorrower_CanBlockOnlyCreaturesWithFlying()
	{
		var borrower = Field(Lsv("Brazen Borrower // Petty Theft"), me);
		var bear = Field(Bear, them);
		var bird = Field(Bird, them);

		bool CanBlock(int attacker) => Combat.CanBlock(s, s.GetCard(borrower), s.GetCard(attacker));
		Assert.That((CanBlock(bear), CanBlock(bird)), Nu.EqualTo((false, true)));
	}
}
