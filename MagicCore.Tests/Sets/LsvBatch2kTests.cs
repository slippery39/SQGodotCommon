using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4k1, LSVPower — green's tested pieces: the two that needed an option, FIRING against their printed text
/// through real casting and the real stack — Scythecat Cub ("instead" on the second resolution) and Titania,
/// Protector of Argoth (a land put into a graveyard from the battlefield raises the zone-change event).
/// </summary>
public class LsvBatch2kTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	/// <summary>A land enters under my control — a {1} sorcery's New Planet token — its landfall's target answered.</summary>
	void LandEnters(int target) =>
		CastAsking(
			Hand(
				TestGames.Inline(
					"Genesis",
					"{1}",
					"Sorcery",
					null,
					MagicCore.Sets.Trk.TrkCards.CreateNewPlanet
				),
				me
			),
			On<ChooseTargetChoice>(target)
		);

	int Named(int p, string name) => s.CardsIn(p, ZoneType.Battlefield).Count(c => c.Name == name);

	int ALand(int p) =>
		s.CardsIn(p, ZoneType.Battlefield).First(c => c.Is(CardType.Land) && !c.IsToken).Id;

	[Test]
	public void ScythecatCub_OneCounter_ThenTheSecondDoubles_ThenOneAgain()
	{
		Field(Lsv("Scythecat Cub"), me);
		var bear = Field(Bear, me);

		LandEnters(bear);
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(1), "a +1/+1 counter");
		s = s.PutCounters(bear, CounterKinds.PlusOne, 2); // TEST SETUP: three
		LandEnters(bear);
		Assert.That(
			s.GetCard(bear).PlusOneCounters,
			Nu.EqualTo(6),
			"the second time: double the number instead — not one more first"
		);
		LandEnters(bear);
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(7), "the third: one again");
	}

	[Test]
	public void Titania_ALandOfMineToTheGraveyard_A53Elemental_TheirsNone()
	{
		Field(Lsv("Titania, Protector of Argoth"), me);

		s = s.Sacrifice(ALand(me));
		s = s.Sacrifice(ALand(them));
		s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
		Resolve();

		var elementals = s.CardsIn(me, ZoneType.Battlefield)
			.Where(c => c.Name == "Elemental")
			.ToList();
		Assert.That(elementals.Count, Nu.EqualTo(1), "my land only");
		Assert.That((s.Power(elementals[0]), s.Toughness(elementals[0])), Nu.EqualTo((5, 3)));
		Assert.That(Named(them, "Elemental"), Nu.EqualTo(0));
	}
}
