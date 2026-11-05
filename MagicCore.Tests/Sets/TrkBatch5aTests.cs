using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;
using Is = MagicCore.Filters.Is;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-5.3 batch 5a: TRK's tier 1–2 rares and mythics through real casting — the enters-untapped lands
/// (Warp Speed's too), a tapped token copy, every token an Illusion, a discard choice's "then", look and
/// take onto the battlefield, the sacrificed creature as the event card, no maximum hand size, doubling
/// your life, "choose one or more" when your hand is empty, a card cast from exile, a copy of a card
/// that exiled itself, and a creature's own death seen by its "this or another" trigger.
/// </summary>
public class TrkBatch5aTests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	/// <summary>Answers "you may" yes, and any choice offering one of these ids with it; the rest with their minimum.</summary>
	static Func<ChoiceAction, IEnumerable<int>> Picking(params int[] ids) =>
		c =>
			c switch
			{
				YesNoChoice yes => [yes.Options[0].Id],
				_ => Offered(c, ids),
			};

	static IEnumerable<int> Offered(ChoiceAction c, int[] ids)
	{
		var picked = c.Options.Select(o => o.Id).Where(ids.Contains).Take(c.MaxChoices).ToList();
		return picked.Count > 0 ? picked : c.Options.Take(c.MinChoices).Select(o => o.Id);
	}

	/// <summary>Answers a mode choice with these modes; the rest with their minimum.</summary>
	static Func<ChoiceAction, IEnumerable<int>> Modes(params int[] modes) =>
		c =>
			c switch
			{
				ChooseModesChoice => modes,
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			};

	void CastAndResolve(int card, Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		s = s.Act(Casts(card).First());
		Resolve(answer);
	}

	int OnTop(CardDefinition card, int player)
	{
		var id = Hand(card, player);
		s = s.MoveCard(id, s.ZoneId(player, ZoneType.Library), toTop: true);
		return id;
	}

	void Activate(
		ActivateAbilityAction activation,
		Func<ChoiceAction, IEnumerable<int>>? answer = null
	)
	{
		s = s.Act(activation);
		Resolve(answer);
	}

	IEnumerable<Card> Tokens(int player) =>
		s.CardsIn(player, ZoneType.Battlefield).Where(c => c.IsToken);

	void EmptyHand(int player)
	{
		foreach (var card in s.CardsIn(player, ZoneType.Hand).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(player, ZoneType.Library));
	}

	// ===== The enters-untapped lands: "When this land enters untapped, choose one —" =====

	[Test]
	public void FederationOutpost_EntersUntapped_ItsChosenModeGainsTwo()
	{
		var outpost = Hand(Trk("Federation Outpost"), me);
		var life = Life(me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = outpost });
		Resolve(Modes(0));
		Assert.That(s.GetCard(outpost).IsTapped, Nu.False, "two or more other lands");
		Assert.That(Life(me), Nu.EqualTo(life + 2), "• You gain 2 life.");
	}

	[Test]
	public void QuarksBar_EnteringTapped_DoesntTrigger()
	{
		Field(CardRegistry.Get("Root Maze"), them); // "Artifacts and lands enter tapped."
		var bar = Hand(Trk("Quark's Bar"), me);
		var theirs = Life(them);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = bar });
		Resolve(Modes(0));
		Assert.That(s.GetCard(bar).IsTapped, Nu.True);
		Assert.That(Life(them), Nu.EqualTo(theirs), "entered tapped: no trigger");
	}

	[Test]
	public void QuarksBar_WarpSpeedUntapsIt_SoItTriggers()
	{
		Field(CardRegistry.Get("Root Maze"), them);
		Field(Trk("Warp Speed"), me);
		var bar = Hand(Trk("Quark's Bar"), me);
		var theirs = Life(them);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = bar });
		Resolve(Modes(0));
		Assert.That(
			s.GetCard(bar).IsTapped,
			Nu.False,
			"Warp Speed: lands you control enter untapped"
		);
		Assert.That(
			Life(them),
			Nu.EqualTo(theirs - 1),
			"• This land deals 1 damage to each opponent."
		);
	}

	// ===== Replicator: "{2}, {T}: Create a tapped token that's a copy of target token you control." =====

	[Test]
	public void Replicator_CopiesATokenYouControl_Tapped()
	{
		var replicator = Field(Trk("Replicator"), me);
		Cast(
			Hand(
				TestGames.Inline(
					"Muster",
					"{1}",
					"Sorcery",
					null,
					new CreateTokens(global::MagicCore.Sets.Trk.TrkCards.Officer, 1)
				),
				me
			),
			[]
		);
		var officer = Tokens(me).Single(c => c.Name == "Officer").Id;
		Activate(Activations(replicator).First(a => a.Targets.Contains(officer)));
		var copies = Tokens(me).Where(c => c.Name == "Officer").ToList();
		Assert.That(copies, Has.Count.EqualTo(2));
		Assert.That(copies.Single(c => c.Id != officer).IsTapped, Nu.True, "a tapped token");
	}

	// ===== Holo-Engineer: "If one or more tokens would be created under your control, that many 3/3 … Illusion … instead." =====

	[Test]
	public void HoloEngineer_ANonartifactTokenIsAnIllusionInstead()
	{
		Field(Trk("Holo-Engineer"), me);
		Cast(
			Hand(
				TestGames.Inline(
					"Muster",
					"{1}",
					"Sorcery",
					null,
					new CreateTokens(global::MagicCore.Sets.Trk.TrkCards.Officer, 2)
				),
				me
			),
			[]
		);
		var made = Tokens(me).ToList();
		Assert.That(made, Has.Count.EqualTo(2), "that many");
		Assert.That(made.All(c => c.Name == "Illusion" && c.Is(CardType.Artifact)), Nu.True);
		Assert.That(made.All(c => s.Power(c) == 3 && s.Toughness(c) == 3), Nu.True, "3/3");
	}

	// ===== Salt Vampire: "You may choose a nonland card from it. If you do, that player exiles that card, then draws a card." =====

	[Test]
	public void SaltVampire_TheChosenCardIsExiled_AndOnlyThenTheyDraw()
	{
		EmptyHand(them);
		var card = Hand(TestCards.Fodder, them);
		CastAndResolve(Hand(Trk("Salt Vampire"), me), Picking(card, them));
		Assert.That(s.IsIn(card, them, ZoneType.Exile), Nu.True);
		Assert.That(HandCount(them), Nu.EqualTo(1), "then draws a card");

		var kept = s.CardsIn(them, ZoneType.Hand).Single().Id;
		CastAndResolve(Hand(Trk("Salt Vampire"), me), Picking(them)); // chooses nothing
		Assert.That(s.IsIn(kept, them, ZoneType.Hand), Nu.True);
		Assert.That(HandCount(them), Nu.EqualTo(1), "no card chosen: no draw");
	}

	// ===== Gowron: "If you sacrificed a Klingon this way, you draw a card and lose 1 life." =====

	[Test]
	public void Gowron_SacrificingAKlingonDraws_AnotherCreatureDoesnt()
	{
		var gowron = Field(Trk("Gowron, Eager for Battle"), me);
		var klingon = Field(TestCards.Typed(Bear, "Creature — Klingon Warrior"), me);
		var bear = Field(Bear, me);
		var hand = HandCount(me);
		var life = Life(me);
		Activate(Activations(gowron).First(a => a.SacrificeId == klingon));
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "a Klingon: draw");
		Assert.That(Life(me), Nu.EqualTo(life - 1));
		Assert.That(s.Has(s.GetCard(gowron), "Trample"), Nu.True);
		Activate(Activations(gowron).First(a => a.SacrificeId == bear));
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "not a Klingon: no draw");
		Assert.That(
			s.GetCard(gowron).PlusOneCounters,
			Nu.EqualTo(2),
			"another creature died, twice"
		);
	}

	[Test]
	public void Gowron_SacrificingAKlingonToken_StillDraws()
	{
		var gowron = Field(Trk("Gowron, Eager for Battle"), me);
		Cast(
			Hand(
				TestGames.Inline(
					"Muster",
					"{1}",
					"Sorcery",
					null,
					new CreateTokens(global::MagicCore.Sets.Trk.TrkCards.KlingonWarrior, 1)
				),
				me
			),
			[]
		);
		var token = Tokens(me).Single().Id;
		var hand = HandCount(me);
		Activate(Activations(gowron).First(a => a.SacrificeId == token));
		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand + 1),
			"the token was a Klingon as it was sacrificed"
		);
	}

	// ===== Hit It!: "Put a land card from among them onto the battlefield tapped and the rest into your graveyard." =====

	[Test]
	public void HitIt_ALandOntoTheBattlefieldTapped_TheRestIntoTheGraveyard()
	{
		var fodder = OnTop(TestCards.Fodder, me);
		var land = OnTop(TestGames.Card("Forest"), me);
		CastAndResolve(Hand(Trk("Hit It!"), me), Picking(land));
		Assert.That(s.IsOnBattlefield(land), Nu.True);
		Assert.That(s.GetCard(land).IsTapped, Nu.True, "tapped");
		Assert.That(
			s.IsIn(fodder, me, ZoneType.Graveyard),
			Nu.True,
			"the rest into your graveyard"
		);
	}

	// ===== Deep Space Scan: "You have no maximum hand size until your next turn." =====

	[Test]
	public void DeepSpaceScan_DrawsPerLandName_AndNoDiscardAtCleanup()
	{
		var hand = HandCount(me);
		CastAndResolve(Hand(Trk("Deep Space Scan"), me));
		Assert.That(HandCount(me), Nu.EqualTo(hand + 5), "five differently named basics");
		for (var i = 0; i < 4; i++)
			Hand(TestCards.Fodder, me);
		var held = HandCount(me);
		Assert.That(held, Nu.GreaterThan(TurnStructure.MaxHandSize));
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn + 1);
		Assert.That(HandCount(me), Nu.EqualTo(held), "no maximum hand size: nothing discarded");
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == turn + 2 && x.GetGame().Step == Step.Draw
		);
		Assert.That(
			TurnStructure.MaxHandSizeOf(s, me),
			Nu.EqualTo(TurnStructure.MaxHandSize),
			"until your next turn"
		);
	}

	// ===== Live Long and Prosper: "Double your life total. Draw cards equal to the number of cards in your hand." =====

	[Test]
	public void LiveLongAndProsper_DoublesYourLife_DrawsYourHandAgain()
	{
		var spell = Hand(Trk("Live Long and Prosper"), me);
		var life = Life(me);
		var others = HandCount(me) - 1;
		CastAndResolve(spell);
		Assert.That(Life(me), Nu.EqualTo(life * 2));
		Assert.That(HandCount(me), Nu.EqualTo(others * 2));
	}

	// ===== Captain James T. Kirk: "If you have no cards in hand, choose one or more instead." =====

	[Test]
	public void CaptainKirk_WithAnEmptyHand_ChoosesTwoModes()
	{
		EmptyHand(me);
		var max = 0;
		CastAndResolve(
			Hand(Trk("Captain James T. Kirk"), me),
			c =>
			{
				if (c is ChooseModesChoice m)
					max = m.MaxChoices;
				return Modes(1, 2)(c);
			}
		);
		Assert.That(max, Nu.EqualTo(3), "one or more");
		var officer = Tokens(me).Single(c => c.Name == "Officer");
		Assert.That(s.Power(officer), Nu.EqualTo(2), "an Officer, and +1/+0");
	}

	[Test]
	public void CaptainKirk_WithACardInHand_ChoosesOne()
	{
		EmptyHand(me);
		Hand(TestCards.Fodder, me);
		var max = 0;
		CastAndResolve(
			Hand(Trk("Captain James T. Kirk"), me),
			c =>
			{
				if (c is ChooseModesChoice m)
					max = m.MaxChoices;
				return Modes(1)(c);
			}
		);
		Assert.That(max, Nu.EqualTo(1));
	}

	// ===== Khan, Vengeful Heart: "You may cast this card from exile." =====

	[Test]
	public void Khan_CastFromExile_NotFaceDown()
	{
		int khan;
		(s, khan) = TestGames.Put(s, me, Trk("Khan, Vengeful Heart"), ZoneType.Exile);
		Assert.That(Casts(khan).Any(), Nu.True, "from exile");
		CastAndResolve(khan);
		Assert.That(s.IsOnBattlefield(khan), Nu.True);

		int hidden;
		(s, hidden) = TestGames.Put(s, me, Trk("Khan, Vengeful Heart"), ZoneType.Exile);
		s = s.UpdateObject(hidden, (Card)s.GetCard(hidden).WithComponent(new ExiledFaceDown()));
		Assert.That(Casts(hidden).Any(), Nu.False, "face down it has no abilities");
	}

	// ===== Tasha Yar: "{2}{R}, Exile this card from your graveyard: Create a token that's a copy of this card." =====

	[Test]
	public void TashaYar_ExiledFromTheGraveyard_ACopyThatIsSacrificedAtTheEndStep()
	{
		int tasha;
		(s, tasha) = TestGames.Put(s, me, Trk("Tasha Yar, Out of Time"), ZoneType.Graveyard);
		Activate(Activations(tasha).Single());
		Assert.That(s.IsIn(tasha, me, ZoneType.Exile), Nu.True, "the cost");
		var copy = Tokens(me).Single(c => c.Name == "Tasha Yar, Out of Time");
		Assert.That(s.Has(copy, "Haste"), Nu.True, "a copy of this card");
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.Cleanup);
		Assert.That(s.IsOnBattlefield(copy.Id), Nu.False, "sacrificed at the next end step");
	}

	// ===== Excalbian Gamemaster: "Whenever this creature or another creature you control dies, …" =====

	[Test]
	public void ExcalbianGamemaster_ItsOwnDeath_PutsACounterOnAnother()
	{
		var gamemaster = Field(Trk("Excalbian Gamemaster"), me);
		var bear = Field(Bear, me);
		Cast(Hand(TestCards.Kill(), me), [], gamemaster);
		Resolve(Picking(bear));
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(1), "this creature … dies");
	}

	// ===== Romulan Bird-of-Prey: "As long as this Spacecraft isn't a creature, it has hexproof." =====

	[Test]
	public void RomulanBirdOfPrey_HexproofUntilItIsACreature()
	{
		var bird = Field(Trk("Romulan Bird-of-Prey"), me);
		var their = TargetSpec.Permanent(Is.Artifact);
		Assert.That(their.IsLegal(s, them, 0, bird), Nu.False, "hexproof");
		Cast(Hand(TestGames.Inline("Cantrip", "{1}", "Sorcery"), me), []);
		Assert.That(s.GetCard(bird).ChargeCounters, Nu.EqualTo(2), "a noncreature spell: two");
		s = s.PutCounters(bird, CounterKinds.Charge, 6);
		Assert.That(their.IsLegal(s, them, 0, bird), Nu.True, "a creature at 8+: no hexproof");
	}
}
