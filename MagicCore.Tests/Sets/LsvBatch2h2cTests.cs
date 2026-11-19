using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4h2c, LSVPower — black's costs and permissions: life paid as a counted cost and an X with no {X} (Toxic
/// Deluge), a free cast from the top of the library paid in life (Bolas's Citadel), a mana ability without {T}
/// (Rain of Filth), a "this turn" play-from-the-graveyard and exile-instead (Yawgmoth's Will), entering with wish
/// counters (Wishclaw Talisman).
/// </summary>
public class LsvBatch2h2cTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");
	static CardDefinition Ogre => TestGames.Creature("Ogre", "3/3");

	[Test]
	public void ToxicDeluge_PayXLife_AllCreaturesGetMinusX_NoManaForX()
	{
		var bear = Field(Bear, me);
		var ogre = Field(Ogre, them);
		var deluge = Hand(Lsv("Toxic Deluge"), me);
		var casts = Casts(deluge).ToList();
		Assert.That(
			casts.Select(c => c.X).Distinct(),
			Nu.EquivalentTo(Enumerable.Range(0, Life(me) + 1)),
			"one cast per X up to the life"
		);
		var (life, lands) = (Life(me), UntappedLands());
		s = s.Act(casts.First(c => c.X == 2));
		Resolve();
		Assert.That(Life(me), Nu.EqualTo(life - 2), "pay X life");
		Assert.That(UntappedLands(), Nu.EqualTo(lands - 3), "{2}{B} only: X is no mana");
		Assert.That(Alive(bear), Nu.False);
		Assert.That(Alive(ogre));
		Assert.That(s.Power(s.GetCard(ogre)), Nu.EqualTo(1));
	}

	[Test]
	public void ToxicDeluge_ItsXIsNoMana_ManaValueThreeOnTheStack()
	{
		var deluge = Hand(Lsv("Toxic Deluge"), me);
		s = s.Act(Casts(deluge).First(c => c.X == 5));
		Assert.That(
			s.GetCard(deluge).ManaValueNow,
			Nu.EqualTo(3),
			"CR 202.3e: only an X in the mana cost counts"
		);
	}

	[Test]
	public void BolasCitadel_ToxicDelugeOnTop_ChoosesX_AllItsLifeTogether()
	{
		Field(Lsv("Bolas's Citadel"), me);
		var deluge = OnTop(Lsv("Toxic Deluge"));
		s = s.WithPlayer(s.GetPlayer(me) with { Life = 6 });
		Assert.That(
			Casts(deluge).Select(c => c.X),
			Nu.EquivalentTo(new[] { 0, 1, 2, 3 }),
			"3 life for its mana value plus X: never more than the 6 there is (CR 119.4)"
		);
		s = s.Act(Casts(deluge).Single(c => c.X == 2));
		Resolve();
		Assert.That(Life(me), Nu.EqualTo(1), "3 for its mana value, 2 for X");
	}

	[Test]
	public void BolasCitadel_CastTheTopCardForLife_PlayALand_TenForTen()
	{
		Field(Lsv("Bolas's Citadel"), me);
		var bear = OnTop(TestGames.CreatureCosting("{1}{G}", "Bear", "2/2"));
		var casts = Casts(bear).ToList();
		Assert.That(casts, Nu.Not.Empty);
		Assert.That(casts.All(c => c.WithoutPayingManaCost), "life rather than its mana cost");
		var (life, lands) = (Life(me), UntappedLands());
		s = s.Act(casts.First());
		Resolve();
		Assert.That(Alive(bear));
		Assert.That(Life(me), Nu.EqualTo(life - 2), "life equal to its mana value");
		Assert.That(UntappedLands(), Nu.EqualTo(lands));

		var land = OnTop(TestGames.Card("Swamp"));
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = land });
		Assert.That(Alive(land), "play lands from the top");
		Assert.That(Life(me), Nu.EqualTo(life - 2), "a land costs no life");

		var citadel = s.CardsIn(me, ZoneType.Battlefield)
			.Single(c => c.Name == "Bolas's Citadel")
			.Id;
		Assert.That(Activations(citadel), Nu.Empty, "two nonland permanents: not ten");
		for (var i = 0; i < 8; i++)
			Field(Bear, me);
		var theirs = Life(them);
		s = s.Act(Activations(citadel).Single());
		Resolve();
		Assert.That(Life(them), Nu.EqualTo(theirs - 10));
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => !c.Is(CardType.Land)),
			Nu.Zero,
			"ten nonland permanents sacrificed, the Citadel among them"
		);
	}

	[Test]
	public void RainOfFilth_LandsGainSacrificeForB_ATappedOneToo_NeverThePayments()
	{
		var rain = Hand(Lsv("Rain of Filth"), me);
		Cast(rain, []);
		TapMyLands();
		var forest = s.CardsIn(me, ZoneType.Battlefield)
			.First(c => c.Subtypes.Contains("Forest"))
			.Id;
		s = s.Untap(forest);
		var again = Hand(Lsv("Rain of Filth"), me);
		Assert.That(
			Casts(again),
			Nu.Empty,
			"an untapped Forest still taps for {G} alone: the solver never sacrifices a land"
		);
		var moves = s.LegalActions().OfType<TapForManaAction>().Where(t => t.NoTap).ToList();
		Assert.That(
			moves.Select(t => t.CardId),
			Nu.EquivalentTo(
				s.CardsIn(me, ZoneType.Battlefield)
					.Where(c => c.Is(CardType.Land))
					.Select(c => c.Id)
			),
			"one move per land I control, tapped as they are"
		);
		var swamp = moves.First().CardId;
		s = s.Act(moves.First());
		Assert.That(InGraveyard(swamp), "sacrificed");
		Assert.That(s.GetPlayer(me).ManaPool.Black, Nu.EqualTo(1));
		Assert.That(Casts(again), Nu.Not.Empty, "its {B} pays");
		Assert.That(
			s.LegalActions().OfType<TapForManaAction>().Where(t => t.NoTap && t.PlayerId == them),
			Nu.Empty,
			"lands YOU control"
		);
	}

	[Test]
	public void YawgmothsWill_PlayFromTheGraveyard_AndExiledInstead()
	{
		var will = Hand(Lsv("Yawgmoth's Will"), me);
		var rain = Put(Lsv("Rain of Filth"), me, ZoneType.Graveyard);
		var swamp = Put(TestGames.Card("Swamp"), me, ZoneType.Graveyard);
		Assert.That(Casts(rain), Nu.Empty);
		Cast(will, []);
		Assert.That(InExile(will), "a card that would be put into your graveyard: exiled");
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = swamp });
		Assert.That(Alive(swamp), "play lands from your graveyard");
		Cast(rain, []);
		Assert.That(InExile(rain), "cast from the graveyard, then exiled instead");
	}

	[Test]
	public void WishclawTalisman_ThreeWishCounters_TutorThenTheyControlIt()
	{
		var talisman = Hand(Lsv("Wishclaw Talisman"), me);
		Cast(talisman, []);
		Assert.That(s.GetCard(talisman).CountersOf(CounterKinds.Wish), Nu.EqualTo(3));
		Assert.That(s.GetCard(talisman).PlusOneCounters, Nu.Zero);
		var wanted = Put(Ogre, me, ZoneType.Library);
		s = s.Act(Activations(talisman).Single());
		Resolve(c =>
			c switch
			{
				ChooseFromLibraryChoice => [wanted],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(s.IsIn(wanted, me, ZoneType.Hand), "search for a card, into your hand");
		Assert.That(s.GetCard(talisman).CountersOf(CounterKinds.Wish), Nu.EqualTo(2));
		Assert.That(
			s.GetCard(talisman).ControllerId,
			Nu.EqualTo(them),
			"an opponent gains control"
		);
	}
}
