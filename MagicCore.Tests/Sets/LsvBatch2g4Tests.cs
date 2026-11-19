using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Sets.Lsv;
using MagicCore.Turns;
using static MagicCore.Sets.Scripting;
using Is = MagicCore.Filters.Is; // the filter vocabulary, over NUnit's Is
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4g4, LSVPower — face-down permanents (CR 708) and manifest dread (CR 701.62): Abhorrent Oculus, each
/// FIRING against its printed text through real casting and the real stack.
/// </summary>
public class LsvBatch2g4Tests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	/// <summary>A {1}{G} 4/4 flier: "When this enters, draw a card." — face down it has none of it.</summary>
	static CardDefinition Ogre =>
		new(
			TestGames.CreatureCosting("{1}{G}", "Ogre", "4/4", "Flying").Data,
			[When(TriggerEvent.SelfEnters, [], new DrawCards(Ref.You, 1))]
		);

	static CardDefinition ManifestSpell =>
		new(
			TestGames.Inline("Manifest", "{0}", "Instant").Data,
			[new SpellEffectsComponent([LsvCards.ManifestDread])]
		);

	static bool IsFaceDownPick(ChoiceAction c) =>
		c is ChooseAmongChoice { Prompt: var p } && p.Contains("face down");

	List<int> Graveyard(int n) =>
		[.. Enumerable.Range(0, n).Select(_ => Put(Bear, me, ZoneType.Graveyard))];

	/// <summary>Manifest dread from a {0} instant, `pick` put face down; the choices asked.</summary>
	List<ChoiceAction> Manifest(int pick) =>
		CastAsking(
			Hand(ManifestSpell, me),
			c => IsFaceDownPick(c) ? [pick] : c.Options.Take(c.MinChoices).Select(o => o.Id)
		);

	[Test]
	public void AbhorrentOculus_CostsSixCardsFromMyGraveyard_PickedAfterTheCast()
	{
		var binned = Graveyard(5);
		var oculus = Hand(Lsv("Abhorrent Oculus"), me);
		Assert.That(Casts(oculus), Nu.Empty, "five cards: it can't be cast");
		binned.Add(Put(Bear, me, ZoneType.Graveyard));

		var asked = CastAsking(oculus, c => c.Options.Take(c.MinChoices).Select(o => o.Id));

		Assert.That(Casts(oculus), Nu.Empty);
		Assert.That(
			asked.OfType<ChooseAmongChoice>().Count(),
			Nu.EqualTo(6),
			"six picked, one at a time"
		);
		Assert.That(binned.All(InExile), "exile six cards from your graveyard");
		var card = s.GetCard(oculus);
		Assert.That(
			(Alive(oculus), s.Power(card), s.Toughness(card), s.Has(card, "Flying")),
			Nu.EqualTo((true, 5, 5, true))
		);
	}

	[Test]
	public void AbhorrentOculus_AtTheirUpkeep_ManifestDread_OneFaceDown_TheOtherBinned()
	{
		Field(Lsv("Abhorrent Oculus"), me);
		var forest = OnTop(TestGames.Card("Forest"));
		var ogre = OnTop(Ogre);
		var hand = HandCount(me);

		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is { } c && IsFaceDownPick(c));
		Assert.That(s.GetGame().Step, Nu.EqualTo(Step.Upkeep));
		Assert.That(s.GetGame().ActivePlayerId, Nu.EqualTo(them), "each OPPONENT's upkeep");
		s = s.Choose([ogre]);

		var down = s.GetCard(ogre);
		Assert.That(Alive(ogre), "onto the battlefield");
		Assert.That(
			(down.Name, s.Power(down), s.Toughness(down), s.Has(down, "Flying")),
			Nu.EqualTo(("", 2, 2, false)),
			"face down as a 2/2 creature: no name, no abilities"
		);
		Assert.That(
			(down.Types, down.Subtypes.Length, down.Colors.Length, down.ManaCost.ManaValue),
			Nu.EqualTo((CardType.Creature, 0, 0, 0))
		);
		Assert.That(
			(down.IsHiddenFrom(them), down.IsHiddenFrom(me)),
			Nu.EqualTo((true, false)),
			"only its controller may look"
		);
		Assert.That(HandCount(me), Nu.EqualTo(hand), "its own 'when this enters' never was");
		Assert.That(InGraveyard(forest), "and the other into your graveyard");
	}

	[Test]
	public void FaceDown_TurnedFaceUpForItsManaCost_TheSameObject_NoEntering()
	{
		OnTop(Bear);
		var ogre = OnTop(Ogre);
		Manifest(ogre);
		var incarnation = s.GetCard(ogre).Incarnation;
		var lands = UntappedLands();
		var hand = HandCount(me);

		var turn = s.LegalActions().OfType<TurnFaceUpAction>().Single();
		Assert.That(turn.CardId, Nu.EqualTo(ogre), "any time I have priority — a special action");
		s = s.Act(turn);

		var up = s.GetCard(ogre);
		Assert.That(
			(up.Name, s.Power(up), s.Toughness(up), s.Has(up, "Flying")),
			Nu.EqualTo(("Ogre", 4, 4, true))
		);
		Assert.That(UntappedLands(), Nu.EqualTo(lands - 2), "for its mana cost, {1}{G}");
		Assert.That(up.Incarnation, Nu.EqualTo(incarnation), "the same permanent");
		Assert.That(s.StackIsEmpty(), "no stack, no 'enters' trigger");
		Assert.That(HandCount(me), Nu.EqualTo(hand));
		Assert.That(s.LegalActions().OfType<TurnFaceUpAction>(), Nu.Empty, "face up now");
	}

	[Test]
	public void FaceDown_ANoncreatureCard_CantBeTurnedUp_LeavingRevealsIt()
	{
		OnTop(Bear);
		var forest = OnTop(TestGames.Card("Forest"));
		Manifest(forest);
		var down = s.GetCard(forest);
		Assert.That(
			(down.Is(CardType.Creature), down.Is(CardType.Land)),
			Nu.EqualTo((true, false))
		);

		Assert.That(
			s.LegalActions().OfType<TurnFaceUpAction>(),
			Nu.Empty,
			"only if it's a creature card"
		);

		s = s.Sacrifice(forest);

		var card = s.GetCard(forest);
		Assert.That(InGraveyard(forest));
		Assert.That(
			(card.Name, card.Is(CardType.Land), card.HasComponent<FaceDown>()),
			Nu.EqualTo(("Forest", true, false)),
			"face up once it leaves (CR 708.9)"
		);
	}

	[Test]
	public void FaceDown_TurnedFaceUp_UnderATypeChange_IsStampedAfresh()
	{
		// "Creatures are artifacts in addition to their other types" — a type change (BecomesType), stamped
		Field(
			new(
				TestGames.Inline("Artifice", "{0}", "Enchantment").Data,
				[new BecomesType(Is.Creature) { Add = CardType.Artifact }]
			),
			me
		);
		OnTop(Bear);
		var ogre = OnTop(Ogre);
		Manifest(ogre);
		Assert.That(s.GetCard(ogre).Is(CardType.Artifact), "the 2/2 is stamped");

		s = s.Act(s.LegalActions().OfType<TurnFaceUpAction>().Single());

		var up = s.GetCard(ogre);
		Assert.That(
			(up.Is(CardType.Artifact), up.Is(CardType.Creature), s.Power(up)),
			Nu.EqualTo((true, true, 4)),
			"the Ogre, still an artifact"
		);
	}

	[Test]
	public void FaceDown_AnEntryRule_JudgesTheTwoTwo_NotTheCard()
	{
		// Containment Priest's piece, narrowed by mana value: a face-down card's is 0 (CR 708.2)
		Field(
			new(
				TestGames.Inline("Warden", "{0}", "Enchantment").Data,
				[
					new PermanentsEnter(Is.Creature & !Is.Token & Is.ManaValueAtLeast(2))
					{
						ExiledUnlessCast = true,
					},
				]
			),
			me
		);
		OnTop(Bear);
		var ogre = OnTop(Ogre);

		Manifest(ogre);

		Assert.That(Alive(ogre), "a 2/2 with mana value 0 enters");
	}

	[Test]
	public void FaceDown_ACopyOfIt_IsANamelessTwoTwo_NeverTurnedUp()
	{
		OnTop(Bear);
		var ogre = OnTop(Ogre);
		Manifest(ogre);
		var metamorph = Hand(Lsv("Phyrexian Metamorph"), me);

		CastAsking(
			metamorph,
			c =>
				c switch
				{
					ChooseCreatureToCopyChoice => [ogre],
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				}
		);

		var copy = s.GetCard(metamorph);
		Assert.That(
			(copy.Name, s.Power(copy), s.Toughness(copy), copy.HasComponent<FaceDown>()),
			Nu.EqualTo(("", 2, 2, false)),
			"the copiable values are the 2/2's (CR 707.2) — not the card"
		);
		Assert.That(
			s.LegalActions().OfType<TurnFaceUpAction>().Select(a => a.CardId),
			Nu.EquivalentTo(new[] { ogre }),
			"the copy is no face-down card"
		);
	}
}
