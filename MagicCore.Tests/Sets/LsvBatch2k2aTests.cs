using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4k2a, LSVPower — green's mana, land and token options, FIRING against their printed text: a mana ability's
/// "activate only if" (Fanatic of Rhonas, Undermountain Adventurer), eternalize's "except" (Fanatic), a sacrifice's
/// zone-change event (Baloth Prime), any number of lands (Fastbond), shroud (Sylvan Safekeeper), a token copy that's
/// also an artifact (Vaultborn Tyrant) and persist's -1/-1 counter (Woodfall Primus).
/// </summary>
public class LsvBatch2k2aTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");
	static CardDefinition Ogre => TestGames.Creature("Ogre", "4/4");

	int Mana(int card) => ManaAbilities.YieldOf(s, s.GetCard(card)).Units.Length;

	int ALand(int p) =>
		s.CardsIn(p, ZoneType.Battlefield).First(c => c.Is(CardType.Land) && !c.IsToken).Id;

	List<Card> Named(int p, string name) =>
		[.. s.CardsIn(p, ZoneType.Battlefield).Where(c => c.Name == name)];

	/// <summary>Triggers raised outside an action go on the stack at the next priority, then resolve.</summary>
	void Settle()
	{
		s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
		Resolve();
	}

	void Enters(CardDefinition card)
	{
		var id = Put(card, me, ZoneType.Hand);
		s = s.EnterBattlefield(id, me);
		Settle();
	}

	[Test]
	public void FanaticOfRhonas_FourGreenOnlyWithAPower4Creature()
	{
		var fanatic = Field(Lsv("Fanatic of Rhonas"), me);
		Assert.That(Mana(fanatic), Nu.EqualTo(1), "{G} alone: no creature with power 4");
		Field(Ogre, me);
		Assert.That(Mana(fanatic), Nu.EqualTo(4), "ferocious: {G}{G}{G}{G}");
	}

	[Test]
	public void FanaticOfRhonas_Eternalize_A44BlackZombieWithNoManaCost()
	{
		var fanatic = Put(Lsv("Fanatic of Rhonas"), me, ZoneType.Graveyard);
		s = s.Act(Activations(fanatic).Single());
		Resolve();

		Assert.That(InExile(fanatic), "exiled as the cost");
		var token = Named(me, "Fanatic of Rhonas").Single();
		Assert.That(token.IsToken);
		Assert.That((s.Power(token), s.Toughness(token)), Nu.EqualTo((4, 4)));
		Assert.That(token.Colors, Nu.EqualTo(new[] { ManaColor.Black }));
		Assert.That(token.Subtypes, Nu.EqualTo(new[] { "Zombie", "Snake", "Druid" }));
		Assert.That(token.ManaCost, Nu.EqualTo(ManaCost.None), "no mana cost");
	}

	[Test]
	public void UndermountainAdventurer_TwoGreen_SixOnceADungeonIsCompleted()
	{
		var giant = Field(Lsv("Undermountain Adventurer"), me);
		Assert.That(Mana(giant), Nu.EqualTo(2));
		s = s.WithPlayer(s.GetPlayer(me) with { DungeonsCompleted = 1 }); // TEST SETUP: one completed earlier
		Assert.That(Mana(giant), Nu.EqualTo(6), "six {G} instead");
	}

	[Test]
	public void BalothPrime_EntersStunned_ASacrificedLandMakesATappedBeast_ADestroyedOneDoesnt()
	{
		Enters(Lsv("Baloth Prime"));
		var baloth = Named(me, "Baloth Prime").Single();
		Assert.That(baloth.IsTapped, "enters tapped");
		Assert.That(baloth.StunCounters, Nu.EqualTo(6));

		s = s.Destroy([ALand(me)]);
		Settle();
		Assert.That(Named(me, "Beast"), Nu.Empty, "destroyed, not sacrificed");

		s = s.Sacrifice(ALand(me));
		Settle();
		var beast = Named(me, "Beast").Single();
		Assert.That(beast.IsTapped, "a tapped Beast");
		Assert.That((s.Power(beast), s.Toughness(beast)), Nu.EqualTo((4, 4)));
		Assert.That(
			s.GetCard(baloth.Id).StunCounters,
			Nu.EqualTo(5),
			"untapping it removes a stun counter instead"
		);
	}

	[Test]
	public void Fastbond_AnyNumberOfLands_OneDamageForEachAfterTheFirst()
	{
		Field(Lsv("Fastbond"), me);
		var life = Life(me);
		for (var i = 0; i < 3; i++)
		{
			var forest = Hand("Forest", me);
			s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
			Resolve();
		}
		Assert.That(s.GetPlayer(me).ThisTurn.LandsPlayed, Nu.EqualTo(3), "three lands this turn");
		Assert.That(Life(me), Nu.EqualTo(life - 2), "the second and the third");
	}

	[Test]
	public void SylvanSafekeeper_Shroud_NotEvenMySpellsTargetIt()
	{
		var keeper = Field(Lsv("Sylvan Safekeeper"), me);
		var bear = Field(Bear, me);
		var theirs = Field(Bear, them);
		s = s.Act(Activations(keeper).First(a => a.Targets.Contains(bear)));
		Resolve();

		Assert.That(s.Has(s.GetCard(bear), "Shroud"));
		var kill = Hand(TestCards.Kill(), me);
		Assert.That(Casts(kill).Any(c => c.Targets.Contains(theirs)), "the kill is castable");
		Assert.That(Casts(kill).Any(c => c.Targets.Contains(bear)), Nu.False, "shroud: mine too");
	}

	[Test]
	public void VaultbornTyrant_ItAndAPower4Enter_ThenItDiesIntoAnArtifactCopy()
	{
		var (life, hand) = (Life(me), HandCount(me));
		Enters(Lsv("Vaultborn Tyrant"));
		Enters(Ogre);
		Enters(Bear);
		Assert.That(Life(me), Nu.EqualTo(life + 6), "itself and the Ogre, not the Bear");
		Assert.That(HandCount(me), Nu.EqualTo(hand + 2));

		s = s.Destroy([Named(me, "Vaultborn Tyrant").Single().Id]);
		Settle();
		var token = Named(me, "Vaultborn Tyrant").Single();
		Assert.That(token.IsToken);
		Assert.That(token.Is(CardType.Artifact) && token.Is(CardType.Creature), "an artifact too");

		s = s.Destroy([token.Id]);
		Settle();
		Assert.That(Named(me, "Vaultborn Tyrant"), Nu.Empty, "a token makes no copy");
	}

	[Test]
	public void WoodfallPrimus_Persist_BackWithAMinusOneCounter_ThenNotAgain()
	{
		var primus = Field(Lsv("Woodfall Primus"), me);
		s = s.Destroy([primus]);
		Settle();
		Assert.That(Alive(primus), "returned");
		Assert.That(s.GetCard(primus).MinusOneCounters, Nu.EqualTo(1));
		Assert.That(s.Power(s.GetCard(primus)), Nu.EqualTo(5));

		s = s.Destroy([primus]);
		Settle();
		Assert.That(InGraveyard(primus), "it had a -1/-1 counter");
	}
}
