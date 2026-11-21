using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Turns;
using Is = MagicCore.Filters.Is;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4i7, LSVPower — the blue and black double-faced cards every survey missed, FIRING against their printed text
/// through real casting and the real stack: Tamiyo, Inquisitive Student; Jace, Vryn's Prodigy; Cecil, Dark Knight;
/// Concealing Curtains (transforming); Sink into Stupor // Soporific Springs (a modal DFC).
/// </summary>
public class LsvBatch2i7Tests : FraBoardTest
{
	const string Tamiyo = "Tamiyo, Inquisitive Student // Tamiyo, Seasoned Scholar";
	const string JaceVryn = "Jace, Vryn's Prodigy // Jace, Telepath Unbound";
	const string Cecil = "Cecil, Dark Knight // Cecil, Redeemed Paladin";
	const string Curtains = "Concealing Curtains // Revealing Eye";
	const string Sink = "Sink into Stupor // Soporific Springs";

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition Peek =>
		TestGames.Inline("Peek", "{1}", "Instant", null, new DrawCards(Ref.You, 1));

	/// <summary>I have it on the battlefield back face up, with this much loyalty.</summary>
	int Transformed(string name, int loyalty)
	{
		var card = Field(Lsv(name), me);
		s = s.UpdateObject(card, s.GetCard(card).BackFaceUp()); // TEST SETUP: transformed
		s = s.PutCounters(card, CounterKinds.Loyalty, loyalty);
		return card;
	}

	void Loyalty(int walker, int ability, params int[] targets)
	{
		s = s.Act(
			new ActivateLoyaltyAction
			{
				PlayerId = me,
				CardId = walker,
				AbilityIndex = ability,
				Targets = [.. targets],
			}
		);
		Resolve();
	}

	Card Now(int card) => s.GetCard(card);

	void UntapMyLands()
	{
		foreach (var land in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.IsTapped).ToList())
			s = s.UpdateObject(land.Id, land with { IsTapped = false });
	}

	// ===== Tamiyo, Inquisitive Student // Tamiyo, Seasoned Scholar =====

	[Test]
	public void Tamiyo_ThirdCardDrawnThisTurn_ExiledAndReturnedTransformed_WithItsTwoLoyalty()
	{
		var tamiyo = Field(Lsv(Tamiyo), me);
		var incarnation = Now(tamiyo).Incarnation;
		while (s.GetPlayer(me).ThisTurn.CardsDrawn < 2)
		{
			Cast(Hand(Peek, me), []);
			UntapMyLands();
		}
		Assert.That(Now(tamiyo).Name, Nu.EqualTo(Tamiyo), "two cards: not yet");

		Cast(Hand(Peek, me), []);
		var scholar = Now(tamiyo);
		Assert.That(
			(scholar.Name, scholar.Is(CardType.Planeswalker), scholar.Loyalty),
			Nu.EqualTo(("Tamiyo, Seasoned Scholar", true, 2)),
			"returned transformed: a planeswalker entering with its back face's loyalty"
		);
		Assert.That(
			scholar.Incarnation,
			Nu.Not.EqualTo(incarnation),
			"exiled, then returned: a new object"
		);
	}

	[Test]
	public void TamiyoSeasonedScholar_Minus7_DrawsHalfTheLibraryRoundedUp_AndAnEmblem_NoMaximumHandSize()
	{
		var tamiyo = Transformed(Tamiyo, 7);
		if (Library(me).Count % 2 == 0)
			OnTop(Bear); // TEST SETUP: an odd library, so "rounded up" shows
		var library = Library(me).Count;
		var hand = HandCount(me);

		Loyalty(tamiyo, 2);

		Assert.That(HandCount(me) - hand, Nu.EqualTo((library + 1) / 2), "half, rounded up");
		Assert.That(TurnStructure.MaxHandSizeOf(s, me), Nu.EqualTo(int.MaxValue));
		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);
		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == me);
		Assert.That(
			TurnStructure.MaxHandSizeOf(s, me),
			Nu.EqualTo(int.MaxValue),
			"an emblem: past my next turn, for good"
		);
	}

	// ===== Jace, Vryn's Prodigy // Jace, Telepath Unbound =====

	[Test]
	public void Jace_Loots_FiveOrMoreInYourGraveyard_ExiledAndReturnedTransformed_WithFiveLoyalty()
	{
		var jace = Field(Lsv(JaceVryn), me);
		for (var i = 0; i < 3; i++)
			Put(Bear, me, ZoneType.Graveyard);
		var hand = HandCount(me);

		s = s.Act(Activations(jace).Single());
		Resolve();
		Assert.That(
			(Now(jace).Name, HandCount(me) - hand),
			Nu.EqualTo((JaceVryn, 0)),
			"drew, discarded: four in the graveyard — not yet"
		);

		s = s.UpdateObject(jace, Now(jace) with { IsTapped = false }); // TEST SETUP: untapped again
		s = s.Act(Activations(jace).Single());
		Resolve();
		Assert.That(
			(Now(jace).Name, Now(jace).Loyalty),
			Nu.EqualTo(("Jace, Telepath Unbound", 5)),
			"five: returned transformed"
		);
	}

	[Test]
	public void JaceTelepathUnbound_Minus3_CastAnInstantFromYourGraveyard_ThenItIsExiled()
	{
		var jace = Transformed(JaceVryn, 5);
		var peek = Put(Peek, me, ZoneType.Graveyard);
		Assert.That(Casts(peek), Nu.Empty, "no permission yet");

		Loyalty(jace, 1, peek);
		var cast = Casts(peek).First();
		s = s.Act(cast);
		Resolve();

		Assert.That(
			(InExile(peek), InGraveyard(peek)),
			Nu.EqualTo((true, false)),
			"if that spell would be put into your graveyard, exile it instead"
		);
	}

	// ===== Cecil, Dark Knight // Cecil, Redeemed Paladin =====

	[Test]
	public void Cecil_DealsCombatDamage_YouLoseThatMuch_AtHalfYourLife_UntapsAndTransforms()
	{
		var cecil = Field(Lsv(Cecil), me);
		s = s.UpdateObject(me, s.GetPlayer(me) with { Life = 12 }); // TEST SETUP
		var theirs = Life(them);

		AttackWith(c => c.Options.Take(c.MinChoices).Select(o => o.Id), cecil);

		Assert.That((theirs - Life(them), Life(me)), Nu.EqualTo((2, 10)), "it dealt 2; you lose 2");
		Assert.That(
			(Now(cecil).Name, Now(cecil).IsTapped, s.Has(Now(cecil), "Lifelink")),
			Nu.EqualTo(("Cecil, Redeemed Paladin", false, true)),
			"10 is half of 20: untapped and transformed"
		);
	}

	[Test]
	public void Cecil_DamageToTwoCreatures_TwoTriggers_TransformsOnce()
	{
		var cecil = Field(Lsv(Cecil), me);
		Field(Bear, them);
		Field(Bear, them);
		s = s.UpdateObject(me, s.GetPlayer(me) with { Life = 11 }); // TEST SETUP
		var lash = TestGames.Inline(
			"Lash",
			"{0}",
			"Instant",
			null,
			new DamageFrom(
				Ref.Permanents(Is.Legendary),
				Ref.Permanents(Is.Creature & Is.Opponents),
				1
			)
		);

		Cast(Hand(lash, me), []);

		Assert.That(
			Life(me),
			Nu.EqualTo(9),
			"damage to a creature is damage: each trigger loses 1"
		);
		Assert.That(
			Now(cecil).Name,
			Nu.EqualTo("Cecil, Redeemed Paladin"),
			"the second trigger doesn't turn it back: it transformed since it was put on the stack (CR 701.28f)"
		);
	}

	// ===== Concealing Curtains // Revealing Eye =====

	[Test]
	public void ConcealingCurtains_TransformsAsASorcery_RevealingEye_TheyDiscardTheNonlandYouChoose_ThenDraw()
	{
		var curtains = Field(Lsv(Curtains), me);
		var bear = Hand(Bear, them);
		var main = s;
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.BeginCombat);
		Assert.That(Activations(curtains), Nu.Empty, "activate only as a sorcery");
		s = main;
		var hand = HandCount(them);

		s = s.Act(Activations(curtains).Single());
		var asked = ResolveAsking(On<ChooseCardsFromHandChoice>(bear));

		var eye = Now(curtains);
		Assert.That(
			(eye.Name, s.Power(eye), s.Toughness(eye), s.Has(eye, "Menace")),
			Nu.EqualTo(("Revealing Eye", 3, 4, true))
		);
		Assert.That(
			Offered<ChooseCardsFromHandChoice>(asked),
			Does.Contain(bear),
			"when it transforms"
		);
		Assert.That(
			(InGraveyard(bear), HandCount(them)),
			Nu.EqualTo((true, hand)),
			"that card discarded, then a card drawn"
		);
	}

	// ===== Sink into Stupor // Soporific Springs =====

	[Test]
	public void SinkIntoStupor_ReturnsTheirSpellToItsOwnersHand()
	{
		var bear = Field(Bear, them);
		Cast(Hand(Lsv(Sink), me), [], bear);
		Assert.That(
			s.IsIn(bear, them, ZoneType.Hand),
			Nu.True,
			"a nonland permanent an opponent controls"
		);

		UntapMyLands();
		var peek = TheirSpellOnTheStack(Peek);
		Cast(Hand(Lsv(Sink), me), [], peek);
		Assert.That(s.IsIn(peek, them, ZoneType.Hand), Nu.True, "or a spell");
	}

	[Test]
	public void SoporificSprings_PlayedAsItsBackFace_WithNoManaForTheFront_PayThreeLifeOrTapped()
	{
		var sink = Hand(Lsv(Sink), me);
		TapMyLands(); // TEST SETUP: the instant can't be cast — the land is still played
		var play = s.LegalActions().OfType<PlayLandAction>().Single(p => p.CardId == sink);
		Assert.That(
			(play.BackFace, Describe.Action(s, play)),
			Nu.EqualTo((true, "Play Soporific Springs"))
		);
		var life = Life(me);

		s = s.Act(play);
		Resolve(On<YesNoChoice>(1));

		var springs = Now(sink);
		Assert.That(
			(springs.Name, springs.Is(CardType.Land), springs.IsTapped, Life(me)),
			Nu.EqualTo(("Soporific Springs", true, false, life - 3)),
			"paid 3 life: untapped"
		);
		Assert.That(
			Now(sink).Transform().Name,
			Nu.EqualTo("Soporific Springs"),
			"a modal DFC never transforms"
		);

		s = s.MoveCard(sink, s.ZoneId(me, ZoneType.Graveyard));
		Assert.That(Now(sink).Name, Nu.EqualTo(Sink), "front face up off the battlefield");
	}

	[Test]
	public void SoporificSprings_FromYourGraveyard_ByAPermissionToPlayLandsOnly()
	{
		var sink = Put(Lsv(Sink), me, ZoneType.Graveyard);
		Assert.That(
			s.LegalActions().OfType<PlayLandAction>().Where(p => p.CardId == sink),
			Nu.Empty
		);
		Field(
			TestGames.Inline("Crucible", "{0}", "Artifact") with
			{
				Components = [new MayPlayFromZone(ZoneType.Graveyard, Is.Land)],
			},
			me
		);
		Assert.That(
			s.LegalActions().OfType<PlayLandAction>().Single(p => p.CardId == sink).BackFace,
			Nu.True,
			"the instant can't be cast from there; its back face, a land, can be played"
		);
	}

	[Test]
	public void SoporificSprings_LifeNotPaid_EntersTapped_TapsForBlue()
	{
		var sink = Hand(Lsv(Sink), me);
		var life = Life(me);
		s = s.Act(s.LegalActions().OfType<PlayLandAction>().Single(p => p.CardId == sink));
		Resolve(On<YesNoChoice>());
		Assert.That((Now(sink).IsTapped, Life(me)), Nu.EqualTo((true, life)));
		Assert.That(
			ManaAbilities.ColorsOf(s, Now(sink)),
			Nu.EquivalentTo(new[] { ManaColor.Blue })
		);
	}
}
