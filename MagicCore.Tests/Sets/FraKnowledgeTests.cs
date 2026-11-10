using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;

namespace MagicCore.Tests.Sets;

/// <summary>
/// Who knows a hidden card (story AI-3.2) — set by every FRA source that looks, reveals, or puts a
/// seen card into a hand or library; cleared by a shuffle. Tested by the card's real ability.
/// </summary>
public class FraKnowledgeTests : FraBoardTest
{
	Knowers Known(int card) => s.GetCard(card).KnownTo;

	int Top(int player) => s.CardsIn(player, ZoneType.Library).First().Id;

	int Bottom(int player) => s.CardsIn(player, ZoneType.Library).Last().Id;

	[Test]
	public void ANewGame_KnowsNothing()
	{
		Assert.That(
			new[] { me, them }
				.SelectMany(p => s.CardsIn(p, ZoneType.Library))
				.Select(c => c.KnownTo)
				.Distinct(),
			Is.EqualTo(new[] { Knowers.None })
		);
	}

	[Test]
	public void ASurveiledCardKept_IsKnownToItsOwnerOnly_UntilAShuffle()
	{
		var top = Top(me);
		Cast(Hand(TestGames.Inline("Peek", "{U}", "Instant", null, new Surveil(1)), me), []);

		Assert.That((Top(me), Known(top)), Is.EqualTo((top, Knowers.Owner)), "kept on top");
		Assert.That(
			(s.GetCard(top).IsKnownTo(me), s.GetCard(top).IsKnownTo(them)),
			Is.EqualTo((true, false)),
			"IsKnownTo reads it per player — the table shows a card face up by it (TBL-6)"
		);

		s = s.Shuffle(me);
		Assert.That(Known(top), Is.EqualTo(Knowers.None));
	}

	[Test]
	public void ACardSeenOnTop_StaysKnownOnceDrawn()
	{
		int seen;
		(s, seen) = TestGames.Put(s, me, TestGames.Card("Plains"), ZoneType.Graveyard);
		s = s.MoveCard(seen, s.ZoneId(me, ZoneType.Library), toTop: true); // from a public zone

		s = s.Draw(me);

		Assert.That(
			(s.IsIn(seen, me, ZoneType.Hand), Known(seen)),
			Is.EqualTo((true, Knowers.Opponent))
		);
	}

	// In a hand only what the OPPONENT saw counts: its owner seeing it on top says nothing there.
	[Test]
	public void ASurveiledCardDrawn_IsNotKnownToTheOpponent()
	{
		var top = Top(me);
		Cast(Hand(TestGames.Inline("Peek", "{U}", "Instant", null, new Surveil(1)), me), []);
		s = s.Draw(me);
		Assert.That(Known(top), Is.EqualTo(Knowers.None));
	}

	// A search takes its card face down: what was known of it in the library stays there.
	[Test]
	public void ACardSearchedFor_WithoutRevealing_IsNotKnown()
	{
		int seen;
		(s, seen) = TestGames.Put(s, me, TestGames.Creature("Seen", "1/1"), ZoneType.Graveyard);
		s = s.MoveCard(seen, s.ZoneId(me, ZoneType.Library)); // to the bottom, from a public zone
		var tutor = Hand(
			TestGames.Inline(
				"Tutor",
				"{B}",
				"Sorcery",
				null,
				new SearchLibrary(CardType.None, ZoneType.Hand)
			),
			me
		);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = tutor });
		Resolve(choice => choice.Options.Select(o => o.Id).Where(id => id == seen));

		Assert.That(
			(s.IsIn(seen, me, ZoneType.Hand), Known(seen)),
			Is.EqualTo((true, Knowers.None))
		);
	}

	[Test]
	public void AMulliganBottom_IsKnownToItsOwner()
	{
		s = TestGames.New();
		var player = s.DecidingPlayerId();
		s = s.Act(new MulliganAction { PlayerId = player });
		s = s.Act(new KeepHandAction { PlayerId = player });
		var bottom = s.GetPendingChoice()!.Options.First().Id;
		s = s.Choose([bottom]);

		Assert.That((Bottom(player), Known(bottom)), Is.EqualTo((bottom, Knowers.Owner)));
	}

	[Test]
	public void ParadoxShaper_TheGraveyardCardOnTheBottom_IsKnownToEveryone()
	{
		var shaper = Field(CardRegistry.Get("Paradox Shaper // Omit Variables"), me);
		int card;
		(s, card) = TestGames.Put(s, me, TestGames.Card("Plains"), ZoneType.Graveyard);
		s = s.Act(
			new ActivateAbilityAction
			{
				PlayerId = me,
				CardId = shaper,
				Targets = [card],
			}
		);
		Resolve();

		Assert.That((Bottom(me), Known(card)), Is.EqualTo((card, Knowers.Everyone)));
	}

	[Test]
	public void Unsummon_TheBouncedCard_IsKnownToEveryone()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		Cast(Hand("Unsummon", me), [], bear);

		Assert.That(
			(s.IsIn(bear, them, ZoneType.Hand), Known(bear)),
			Is.EqualTo((true, Knowers.Everyone))
		);
	}

	[Test]
	public void BasicLandcycling_TheRevealedLand_IsKnownToEveryone()
	{
		var witness = Hand("Undulating Witness", me);
		var forest = s.CardsIn(me, ZoneType.Library).First(c => c.Name == "Forest").Id;
		s = s.Act(Activations(witness).Single());
		Resolve(choice =>
			choice switch
			{
				ChooseFromLibraryChoice => [forest],
				_ => [],
			}
		);

		Assert.That(
			(s.IsIn(forest, me, ZoneType.Hand), Known(forest)),
			Is.EqualTo((true, Knowers.Everyone))
		);
	}

	[Test]
	public void LoyalTutor_ThePlaneswalkerOnTop_IsKnownToEveryone()
	{
		int walker;
		(s, walker) = TestGames.Put(s, me, TestGames.Planeswalker("Walker", 3), ZoneType.Library);
		var tutor = Hand("Loyal Tutor", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = tutor });
		Resolve(choice => choice.Options.Select(o => o.Id).Where(id => id == walker));

		Assert.That((Top(me), Known(walker)), Is.EqualTo((walker, Knowers.Everyone)));
	}

	// Revealed, but put on the bottom in a RANDOM order — known, a miss would keep the whole library's
	// real order through "shuffle what isn't known".
	[Test]
	public void IdentityEcho_ItsRandomBottom_IsNotKnown()
	{
		var echo = Field(CardRegistry.Get("Identity Echo"), me);
		var mine = Field(TestGames.Creature("Mine", "1/1"), me);
		int bear,
			island;
		(s, bear) = TestGames.Put(s, me, TestGames.Creature("Bear", "2/2"), ZoneType.Library);
		(s, island) = TestGames.Put(s, me, TestGames.Card("Island"), ZoneType.Library);
		var library = s.ZoneId(me, ZoneType.Library);
		s = s.MoveObjectToFront(bear, library).MoveObjectToFront(island, library); // TEST SETUP: Island, then Bear, on top
		s = s.Act(Activations(echo).Single(a => a.Targets[0] == mine));
		Resolve();

		Assert.That((Bottom(me), Known(island)), Is.EqualTo((island, Knowers.None)));
	}

	[Test]
	public void StingingVitriol_TheirRevealedHand_IsKnownToEveryone()
	{
		var land = Hand(TestGames.Card("Forest"), them);
		var spell = Hand(TestGames.Inline("Spell", "{R}", "Instant", null), them);
		var vitriol = Hand("Stinging Vitriol", me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = vitriol,
				Targets = [them],
			}
		);
		Resolve(_ => [spell]);

		Assert.That(
			(s.IsIn(land, them, ZoneType.Hand), Known(land)),
			Is.EqualTo((true, Knowers.Everyone))
		);
	}

	// Countersculpt: "behold a Jace or pay {1}" — a Jace beheld from the hand is revealed.
	[Test]
	public void Countersculpt_AJaceBeheldFromTheHand_IsKnownToEveryone()
	{
		var walker = TestGames.Planeswalker("Jace", 3);
		var jace = Hand(
			walker with
			{
				Data = walker.Data with { TypeLine = "Planeswalker — Jace" },
			},
			me
		);
		var trick = TheirSpellOnTheStack(
			TestGames.Inline("Trick", "{R}", "Instant", null, new GainLife(Ref.You, 1))
		);
		var sculpt = Hand("Countersculpt", me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = sculpt,
				Targets = [trick],
				BeholdId = jace,
			}
		);
		Resolve();

		Assert.That(
			(s.IsIn(jace, me, ZoneType.Hand), Known(jace)),
			Is.EqualTo((true, Knowers.Everyone))
		);
	}
}
