using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-5.3 batch 7a: TRK's grouped tier-3 cards — trigger twice, assimilate, control until end of turn,
/// dilemmas this turn, casting from odd zones. Each new piece against its card's PRINTED text, through
/// real casting.
/// </summary>
public class TrkBatch7aTests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition Forest => CardRegistry.Get("Forest");

	/// <summary>"Whenever you gain life, draw a card."</summary>
	static CardDefinition Drawer =>
		TestCards.WithTrigger(Bear, TriggerEvent.YouGainLife, new DrawCards(Ref.You, 1));

	static CardDefinition GainThree =>
		new(
			TestGames.Inline("Gain", "{1}", "Instant").Data,
			[new SpellEffectsComponent([new GainLife(Ref.You, 3)])]
		);

	static CardDefinition ChooseOne =>
		new(
			TestGames.Inline("Options", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([])
				{
					Modes =
					[
						new Mode("gain 1", [], [new GainLife(Ref.You, 1)]),
						new Mode("draw", [], [new DrawCards(Ref.You, 1)]),
					],
				},
			]
		);

	int OnTop(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(player, ZoneType.Library)); // TEST SETUP: index 0 is the top
		return id;
	}

	int InGraveyard(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Graveyard);
		return id;
	}

	void PassTo(Step step) => s = TestGames.PassUntil(s, x => x.GetGame().Step == step);

	/// <summary>"Yes", and the first card offered; any other choice its minimum.</summary>
	static Func<ChoiceAction, IEnumerable<int>> Yes =>
		c =>
			c switch
			{
				YesNoChoice or ChooseAmongChoice => c.Options.Take(1).Select(o => o.Id),
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			};

	// ===== Trigger twice =====

	[Test]
	public void Crusher_YourLifeGainTriggersTwice_HerOwnToo()
	{
		var crusher = Field(Trk("Dr. Beverly Crusher"), me);
		Field(Drawer, me);
		Field(Drawer, them);
		var hand = HandCount(me);
		var theirs = HandCount(them);
		Cast(Hand(GainThree, me), []);
		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand + 2),
			"that ability triggers an additional time"
		);
		Assert.That(
			s.GetCard(crusher).PlusOneCounters,
			Nu.EqualTo(2),
			"Whenever you gain life, put a +1/+1 counter on Dr. Crusher — triggered twice"
		);
		Assert.That(HandCount(them), Nu.EqualTo(theirs), "a permanent YOU control");
	}

	[Test]
	public void Lwaxana_TheChosenCreaturesTriggersTwice_WhileSheStaysTapped()
	{
		var lwaxana = Field(Trk("Lwaxana Troi, Charming Guest"), me);
		var drawer = Field(Drawer, me);
		Field(Drawer, me); // not chosen: once
		s = s.Act(Activations(lwaxana).Single(a => a.Targets.SequenceEqual([drawer])));
		Resolve();
		var hand = HandCount(me);
		Cast(Hand(GainThree, me), []);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 3), "the chosen one twice, the other once");
		// Her untap step: asked; "yes" keeps her tapped, and the link with her.
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn + 1);
		s = TestGames.PassUntil(
			s,
			x => x.GetPendingChoice() is YesNoChoice || x.GetGame().TurnNumber == turn + 2
		);
		Assert.That(
			s.GetPendingChoice(),
			Nu.InstanceOf<YesNoChoice>(),
			"You may choose not to untap"
		);
		s = s.Choose([1]);
		Assert.That(s.GetCard(lwaxana).IsTapped, Nu.True);
		PassTo(Step.PreCombatMain);
		hand = HandCount(me);
		Cast(Hand(GainThree, me), []);
		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand + 3),
			"for as long as Lwaxana Troi remains tapped"
		);
		// Untapped: over.
		s = s.Untap(lwaxana);
		hand = HandCount(me);
		Cast(Hand(GainThree, me), []);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 2));
	}

	[Test]
	public void Lwaxana_NotKeptTapped_Untaps_AndTheLinkEnds()
	{
		var lwaxana = Field(Trk("Lwaxana Troi, Charming Guest"), me);
		var drawer = Field(Drawer, me);
		s = s.Act(Activations(lwaxana).Single(a => a.Targets.SequenceEqual([drawer])));
		Resolve();
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn + 1);
		s = TestGames.PassUntil(
			s,
			x => x.GetPendingChoice() is YesNoChoice || x.GetGame().TurnNumber == turn + 2
		);
		s = s.Choose([]);
		Assert.That(s.GetCard(lwaxana).IsTapped, Nu.False);
		Assert.That(
			s.GetCard(lwaxana).GetComponents<TriggersAdditionalTime>().Any(t => t.UntilUntapped),
			Nu.False
		);
	}

	int LinkedToLwaxana(CardDefinition creature)
	{
		var lwaxana = Field(Trk("Lwaxana Troi, Charming Guest"), me);
		var linked = Field(creature, me);
		s = s.Act(Activations(lwaxana).Single(a => a.Targets.SequenceEqual([linked])));
		Resolve();
		return linked;
	}

	[Test]
	public void Lwaxana_TheLinkedCreaturesDiesTrigger_LooksBack_TriggersTwice()
	{
		var linked = LinkedToLwaxana(
			TestCards.WithTrigger(Bear, TriggerEvent.SelfDies, new DrawCards(Ref.You, 1))
		);
		var hand = HandCount(me);
		Cast(Hand(TestCards.Kill(), me), [], linked);
		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand + 2),
			"if a triggered ability of that creature triggers"
		);
	}

	[Test]
	public void Lwaxana_ADelayedTriggerOnTheLinkedCreature_TriggersOnce()
	{
		var linked = LinkedToLwaxana(Bear);
		var delayed = new DelayedTrigger(
			new TriggeredAbilityComponent(TriggerEvent.EachEndStep, [new DrawCards(Ref.You, 1)])
		);
		s = s.UpdateObject(linked, (Card)s.GetCard(linked).WithComponent(delayed)); // TEST SETUP
		PassTo(Step.End);
		Assert.That(
			s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack)).Count(),
			Nu.EqualTo(1),
			"a delayed trigger isn't the creature's ability: once (CR 603.7c)"
		);
	}

	// ===== Assimilate =====

	[Test]
	public void DeathIsIrrelevant_AssimilatesACreatureCardFromAGraveyard_ArtifactCreaturesIndestructible()
	{
		Field(Trk("Death Is Irrelevant"), me);
		var dead = InGraveyard(TestCards.Typed(Bear, "Creature — Elf Warrior"), them);
		PassTo(Step.End);
		Resolve(On<ChooseTargetChoice>(dead));
		var card = s.GetCard(dead);
		Assert.That(
			Alive(dead) && card.ControllerId == me,
			Nu.True,
			"onto the battlefield under your control"
		);
		Assert.That(card.PlusOneCounters, Nu.EqualTo(1), "with a +1/+1 counter");
		Assert.That(
			card.Is(CardType.Artifact) && card.Is(CardType.Creature),
			Nu.True,
			"an artifact creature"
		);
		Assert.That(
			s.Subtypes(card),
			Nu.EqualTo(new[] { "Borg" }),
			"Borg, and loses all other creature types"
		);
		Assert.That(
			s.Has(card, "Indestructible"),
			Nu.True,
			"Artifact creatures you control have indestructible."
		);
		s = s.MoveCard(dead, s.ZoneId(them, ZoneType.Hand)); // TEST SETUP: it leaves
		Assert.That(
			s.GetCard(dead).Is(CardType.Artifact),
			Nu.False,
			"a new object: not an artifact"
		);
		Assert.That(s.Subtypes(s.GetCard(dead)), Nu.EqualTo(new[] { "Elf", "Warrior" }));
	}

	[Test]
	public void BorgCube_Affinity_EntersExiling_AttacksAssimilatingThatCard()
	{
		var cube = Hand(Trk("Borg Cube"), me);
		Field(TestCards.Typed(Bear, "Artifact Creature — Drone"), me);
		Field(TestCards.Typed(Bear, "Artifact Creature — Drone"), me);
		Field(TestCards.Trinket, me); // an artifact, not a creature
		Assert.That(
			Costs.Of(s, me, s.GetCard(cube)).ManaValue,
			Nu.EqualTo(5),
			"Affinity for artifact creatures: {5}{W}{B} less two"
		);
		var victim = Field(Bear, them);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = cube });
		Resolve(On<ChooseTargetChoice>(victim));
		Assert.That(
			s.IsIn(victim, them, ZoneType.Exile),
			Nu.True,
			"If you can't, exile up to one target creature."
		);
		s = s.UpdateObject(cube, s.GetCard(cube) with { ChargeCounters = 12 }); // TEST SETUP: a creature at 12+
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == turn + 2 && x.GetGame().Step == Step.PreCombatMain
		);
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose(declare.CreatureId == cube ? [them] : [0]);
		Resolve();
		Assert.That(
			Alive(victim) && s.GetCard(victim).ControllerId == me,
			Nu.True,
			"Whenever this Spacecraft attacks, assimilate a creature card exiled with it."
		);
		Assert.That(
			s.Has(s.GetCard(victim), "Flying"),
			Nu.True,
			"12+ | Artifact creatures you control have flying."
		);
	}

	[Test]
	public void BorgCube_TwoCardsExiledWithIt_OneChosen()
	{
		var cube = Field(Trk("Borg Cube"), me);
		s = s.UpdateObject(cube, s.GetCard(cube) with { ChargeCounters = 12 }); // TEST SETUP: a creature
		var link = new ExiledBy(cube, s.GetCard(cube).Incarnation);
		int first,
			second;
		(s, first) = TestGames.Put(s, them, Bear, ZoneType.Exile);
		(s, second) = TestGames.Put(s, them, Bear, ZoneType.Exile);
		s = s.UpdateObject(first, (Card)s.GetCard(first).WithComponent(link)); // TEST SETUP: exiled with it
		s = s.UpdateObject(second, (Card)s.GetCard(second).WithComponent(link));
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose(declare.CreatureId == cube ? [them] : [0]);
		Resolve(On<ChooseAmongChoice>(second));
		Assert.That(
			Alive(second) && !Alive(first),
			Nu.True,
			"assimilate a creature card exiled with it — the one chosen"
		);
	}

	// ===== Control until end of turn =====

	[Test]
	public void Worf_GainsControlOfTheirCreatureUntilEndOfTurn_UntappedHastyWarrior()
	{
		var theirs = Field(TestCards.Typed(Bear, "Creature — Elf"), them);
		s = s.Tap(theirs);
		var big = Field(TestGames.CreatureCosting("{4}", "Big", "4/4"), them);
		var cast = Hand(Trk("Worf, Intelligence Contractor"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = cast });
		Resolve(c =>
			c is ChooseTargetChoice t
				? t.Options.Select(o => o.Id).Where(id => id == theirs)
				: c.Options.Take(c.MinChoices).Select(o => o.Id)
		);
		var card = s.GetCard(theirs);
		Assert.That(card.ControllerId, Nu.EqualTo(me), "gain control of up to one target creature");
		Assert.That(card.IsTapped, Nu.False, "Untap those creatures.");
		Assert.That(s.Has(card, "Haste"), Nu.True);
		Assert.That(
			s.Subtypes(card),
			Nu.EqualTo(new[] { "Elf", "Warrior" }),
			"Warriors in addition to their other types"
		);
		Assert.That(s.GetCard(big).ControllerId, Nu.EqualTo(them), "mana value 3 or less");
		PassTo(Step.Cleanup);
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn + 1);
		Assert.That(s.GetCard(theirs).ControllerId, Nu.EqualTo(them), "until end of turn");
		Assert.That(s.Subtypes(s.GetCard(theirs)), Nu.EqualTo(new[] { "Elf" }));
	}

	[Test]
	public void TwoUntilEndOfTurnSteals_ControlGoesBackToTheFirstController()
	{
		var bear = Field(Bear, them);
		s = s.GainControl(bear, me);
		var card = s.GetCard(bear);
		s = s.UpdateObject(
			bear,
			(Card)
				card.WithComponent(new UntilEndOfTurn { ReturnControlTo = them })
					.WithComponent(new UntilEndOfTurn { ReturnControlTo = me })
		); // TEST SETUP: I took it, they took it back, I took it again — each until end of turn
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn + 1);
		Assert.That(
			s.GetCard(bear).ControllerId,
			Nu.EqualTo(them),
			"every effect ended: its first controller's"
		);
	}

	[Test]
	public void WrathOfKhan_ChapterI_4Damage_ThenControlAtTheNextCombat()
	{
		var target = Field(TestGames.Creature("Ogre", "5/5"), them);
		s = s.Tap(target);
		var cast = Hand(Trk("The Wrath of Khan"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = cast });
		Resolve(On<ChooseTargetChoice>(target));
		Assert.That(s.GetCard(target).Damage, Nu.EqualTo(4), "deals 4 damage to target creature");
		Assert.That(
			s.GetCard(target).ControllerId,
			Nu.EqualTo(them),
			"not yet: at the next combat"
		);
		PassTo(Step.BeginCombat);
		Assert.That(
			s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack)).Count(),
			Nu.EqualTo(1),
			"one delayed trigger, yours — not the creature's controller's as well"
		);
		Resolve();
		var card = s.GetCard(target);
		Assert.That(
			card.ControllerId,
			Nu.EqualTo(me),
			"gain control of that creature until end of turn"
		);
		Assert.That(card.IsTapped || !s.Has(card, "Haste"), Nu.False, "Untap it. It gains haste.");
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn + 1);
		Assert.That(s.GetCard(target).ControllerId, Nu.EqualTo(them));
	}

	[Test]
	public void MayExileACardFromYourHand_IfYouDoDraw()
	{
		var spell = new CardDefinition(
			TestGames.Inline("Swap", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent(
					[new MayDiscard([new DrawCards(Ref.You, 1)]) { Exile = true }]
				),
			]
		);
		var fodder = Hand(TestCards.Fodder, me);
		var hand = HandCount(me);
		var cast = Hand(spell, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = cast });
		Resolve(On<ChooseCardsFromHandChoice>(fodder));
		Assert.That(s.IsIn(fodder, me, ZoneType.Exile), Nu.True, "exile a card from your hand");
		Assert.That(HandCount(me), Nu.EqualTo(hand), "If you do, draw a card.");
	}

	// ===== Dilemmas this turn =====

	[Test]
	public void Sisko_AnOfficerForEachDilemmaFacedThisTurn()
	{
		Field(Trk("Captain Benjamin Sisko"), me);
		Cast(Hand(ChooseOne, me), [0]);
		Cast(Hand(ChooseOne, me), [1]);
		Cast(Hand(TestCards.Fodder, me), []);
		PassTo(Step.End);
		Resolve();
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.IsToken && c.Name.Contains("Officer")),
			Nu.EqualTo(2),
			"a 1/1 red Officer creature token for each dilemma you've faced this turn"
		);
	}

	[Test]
	public void Sisko_ExilesFive_CastsFreeThoseUpToTheOfficersYouControl()
	{
		var sisko = Field(Trk("Captain Benjamin Sisko"), me);
		var three = OnTop(TestGames.CreatureCosting("{3}", "Three", "3/3"), me);
		var one = OnTop(Bear, me);
		OnTop(Forest, me);
		OnTop(TestCards.Fodder, me);
		var two = OnTop(TestGames.CreatureCosting("{2}", "Two", "2/2"), me);
		s = s.Act(Activations(sisko).Single());
		Resolve(Yes);
		Assert.That(
			Alive(one),
			Nu.True,
			"mana value less than or equal to the number of Officers you control (1)"
		);
		Assert.That(Alive(two) || Alive(three), Nu.False);
		Assert.That(s.IsIn(three, me, ZoneType.Exile), Nu.True, "what isn't cast stays exiled");
	}

	[Test]
	public void Caretaker_AfterADilemma_TheirTopNonlandCastFree()
	{
		Field(Trk("The Caretaker"), me);
		var theirs = OnTop(Bear, them);
		var land = OnTop(Forest, them);
		PassTo(Step.End);
		Resolve(Yes);
		Assert.That(
			s.IsIn(theirs, them, ZoneType.Library),
			Nu.True,
			"if you faced a dilemma this turn"
		);
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == turn + 2 && x.GetGame().Step == Step.PreCombatMain
		);
		theirs = OnTop(Bear, them);
		land = OnTop(Forest, them);
		Cast(Hand(ChooseOne, me), [0]);
		PassTo(Step.End);
		Resolve(Yes);
		Assert.That(
			Alive(theirs) && s.GetCard(theirs).ControllerId == me,
			Nu.True,
			"You may cast that card without paying its mana cost."
		);
		Assert.That(s.IsIn(land, them, ZoneType.Exile), Nu.True, "until they exile a nonland card");
	}

	// ===== Casting from odd zones =====

	[Test]
	public void Discovery_EntersDiscovering5_CastsTheHitFree_TheRestOnTheBottom()
	{
		var bear = OnTop(Bear, me);
		var six = OnTop(TestGames.CreatureCosting("{6}", "Six", "6/6"), me);
		var forest = OnTop(Forest, me);
		var discovery = Hand(Trk("U.S.S. Discovery, at Black Alert"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = discovery });
		Resolve(Yes);
		Assert.That(
			Alive(bear),
			Nu.True,
			"a nonland card with mana value 5 or less, cast without paying"
		);
		var library = s.CardsIn(me, ZoneType.Library).Select(c => c.Id).ToList();
		Assert.That(
			library.TakeLast(2),
			Nu.EquivalentTo(new[] { six, forest }),
			"Put the rest on the bottom in a random order."
		);
	}

	[Test]
	public void Discover_NotCast_IntoYourHand()
	{
		var discover = new CardDefinition(
			TestGames.Inline("Discover", "{1}", "Sorcery").Data,
			[
				new SpellEffectsComponent(
					[
						new ExileUntilNonlandCastFree(Ref.You)
						{
							ManaValueAtMost = 5,
							OrIntoHand = true,
							RestOnBottom = true,
						},
					]
				),
			]
		);
		var bear = OnTop(Bear, me);
		Cast(Hand(discover, me), []); // the default answer: don't cast
		Assert.That(s.IsIn(bear, me, ZoneType.Hand), Nu.True, "or put it into your hand");
	}

	[Test]
	public void Scotty_InstantsInYourGraveyardHaveBeamMeUp_DuringYourMainPhase_NeverScotty()
	{
		var scotty = Field(Trk("Scotty, Miracle Worker"), me);
		var bear = Field(Bear, me);
		var victim = Field(Bear, them);
		var shock = InGraveyard(TestCards.Shock(2), me);
		var casts = Casts(shock).ToList();
		Assert.That(
			casts.Select(c => c.ReturnId).Distinct(),
			Nu.EquivalentTo(new[] { bear }),
			"Scotty can't be beamed up"
		);
		s = s.Act(casts.Single(c => c.Targets.SequenceEqual([victim])));
		Resolve();
		Assert.That(Alive(victim), Nu.False);
		Assert.That(
			s.IsIn(bear, me, ZoneType.Hand),
			Nu.True,
			"return another creature you control"
		);
		Assert.That(s.IsIn(shock, me, ZoneType.Exile), Nu.True, "Then exile that spell.");
		var again = InGraveyard(TestCards.Shock(2), me);
		Field(Bear, me);
		PassTo(Step.BeginCombat);
		Assert.That(Casts(again), Nu.Empty, "During your main phase");
		_ = scotty;
	}

	[Test]
	public void OverflowSubmatrix_AnArtifactCreatureFromYourGraveyard_OnceEachTurn_WithFinality()
	{
		Field(Trk("Overflow Submatrix"), me);
		var drone = InGraveyard(TestCards.Typed(Bear, "Artifact Creature — Drone"), me);
		var other = InGraveyard(TestCards.Typed(Bear, "Artifact Creature — Drone"), me);
		InGraveyard(Bear, me);
		Assert.That(
			Playable.Candidates(s, me).Count(c => s.IsIn(c.Id, me, ZoneType.Graveyard)),
			Nu.EqualTo(2),
			"an artifact creature spell"
		);
		Cast(drone, []);
		Assert.That(
			Alive(drone),
			Nu.True,
			"resolves onto the battlefield — not exiled as flashback is"
		);
		Assert.That(s.GetCard(drone).HasFinalityCounter, Nu.True, "enters with a finality counter");
		Assert.That(Casts(other), Nu.Empty, "Once during each of your turns");
	}

	[Test]
	public void Holodeck_APermanentCardFromYourGraveyardOrOutsideTheGame_OnceEachTurn()
	{
		Field(Trk("The Holodeck"), me);
		var forest = InGraveyard(Forest, me);
		InGraveyard(TestCards.Fodder, me);
		s = s.WithSideboard(me, [Bear]);
		var outside = s.CardsIn(me, ZoneType.Sideboard).Single().Id;
		Assert.That(Casts(outside), Nu.Not.Empty, "from outside the game");
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
		Assert.That(Alive(forest), Nu.True, "play a permanent card you own … from your graveyard");
		Assert.That(Casts(outside), Nu.Empty, "Once during each of your turns");
	}

	[Test]
	public void InfiniteDiversity_CopiesItXTimes_CastFree_ThenExiled()
	{
		Field(TestCards.Typed(Bear, "Creature — Human Officer"), me);
		Field(TestCards.Typed(Bear, "Creature — Vulcan"), me);
		Field(TestCards.Typed(Bear, "Creature — Borg"), me); // non-Borg only
		var gain = InGraveyard(GainThree, me);
		var life = Life(me);
		var diversity = Hand(Trk("Infinite Diversity"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = diversity,
				Targets = [gain],
			}
		);
		Resolve(Yes);
		Assert.That(Life(me), Nu.EqualTo(life + 9), "X = 3 creature types: Human, Officer, Vulcan");
		Assert.That(
			s.IsIn(gain, me, ZoneType.Exile),
			Nu.True,
			"Exile target instant or sorcery card"
		);
		Assert.That(s.IsIn(diversity, me, ZoneType.Exile), Nu.True, "Exile Infinite Diversity.");
	}

	[Test]
	public void BrazenBluff_TheyExileTheirSpell_OrYouCastOneFree()
	{
		var draw = new CardDefinition(
			TestGames.Inline("Draw", "{4}", "Sorcery").Data,
			[new SpellEffectsComponent([new DrawCards(Ref.You, 2)])]
		);
		var theirs = TheirSpellOnTheStack(GainThree);
		var drawId = Hand(draw, me);
		var hand = HandCount(me);
		var cast = Hand(Trk("Brazen Bluff"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = cast,
				Targets = [theirs],
			}
		);
		Resolve(On<ChooseAmongChoice>(drawId)); // the YesNo's minimum: no — they don't exile it
		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand - 1 + 2),
			"If they don't, you may cast a noncreature spell from your hand free"
		);
		theirs = TheirSpellOnTheStack(GainThree);
		var again = Hand(Trk("Brazen Bluff"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = again,
				Targets = [theirs],
			}
		);
		Resolve(On<YesNoChoice>(1));
		Assert.That(s.IsIn(theirs, them, ZoneType.Exile), Nu.True, "That player may exile it.");
	}
}
