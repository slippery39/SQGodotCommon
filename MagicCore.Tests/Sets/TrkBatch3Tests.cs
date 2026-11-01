using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-5.2's small mechanics and MC-5.3 batch 3 (the TRK commons and uncommons waiting on them): each new
/// piece through real casting (tier 3) — beam me up, strange new worlds, face a dilemma, power 8,
/// becomes tapped (every tap path), look and take, Federation — and each tier-2 card's new option
/// against its PRINTED text. Tier-1 cards are checked by their card-sheet entry (TrkCardSheet.md).
/// </summary>
public class TrkBatch3Tests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static ChoiceAction? Pending(GameState s) => s.GetPendingChoice();

	/// <summary>Answers every "you may" with yes; everything else as Resolve would.</summary>
	static IEnumerable<int> Yes(ChoiceAction choice) =>
		choice switch
		{
			YesNoChoice => [choice.Options[0].Id],
			_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
		};

	int InGraveyard(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Graveyard);
		return id;
	}

	int OnTop(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(me, ZoneType.Library)); // TEST SETUP: index 0 is the top
		return id;
	}

	void TapAllMyLands()
	{
		foreach (var land in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)))
			s = s.UpdateObject(land.Id, land with { IsTapped = true }); // TEST SETUP
	}

	void Pump(int card, int power) =>
		s = s.UpdateObject(card, (Card)s.GetCard(card).WithComponent(new UntilEndOfTurn(power, 0)));

	void Declare(params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (Pending(s) is DeclareAttackerChoice declare)
			s = s.Choose([attackers.Contains(declare.CreatureId) ? them : CombatOption.None]);
	}

	void ToSecondMain() => s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);

	/// <summary>A {1} instant: "Choose one or more — • You gain 1 life. • Draw a card."</summary>
	static CardDefinition ChooseOneOrMore =>
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
					Choose = 2,
					ChooseAtLeast = 1,
				},
			]
		);

	// ===== Tier 3: beam me up =====

	[Test]
	public void BeamMeUp_CastFromTheGraveyard_ReturningACreature_ThenExiled()
	{
		var comms = InGraveyard(Trk("Open Communications"));
		var bear = Field(Bear, me);
		var other = Field(TestGames.Creature("Other", "1/1"), me);
		Field(TestGames.Creature("Theirs", "3/3"), them);
		Assert.That(
			Casts(comms).Select(c => c.ReturnId),
			Is.EquivalentTo(new[] { bear, other }),
			"one cast per creature YOU control to return"
		);
		var hand = HandCount(me);
		s = s.Act(Casts(comms).Single(c => c.ReturnId == bear));
		Assert.That(s.IsIn(bear, me, ZoneType.Hand), Nu.True, "returned to its owner's hand");
		Resolve();
		Assert.That(HandCount(me), Is.EqualTo(hand + 2), "the bear, and the card drawn");
		Assert.That(s.IsIn(comms, me, ZoneType.Exile), Nu.True, "then exile this spell");
	}

	[Test]
	public void BeamMeUp_NoCreature_CantBeCast()
	{
		var comms = InGraveyard(Trk("Open Communications"));
		Assert.That(Casts(comms), Is.Empty);
	}

	[Test]
	public void Marooned_ItsCreatureCantBeBeamedUp_TapsForThree_StaysTapped()
	{
		var bear = Field(Bear, me);
		var other = Field(TestGames.Creature("Other", "1/1"), me);
		var marooned = Hand(Trk("Marooned"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = marooned,
				Targets = [bear],
			}
		);
		Resolve(Yes);
		Assert.That(s.GetCard(bear).IsTapped, Nu.True, "you may pay {3}: tap enchanted permanent");

		var comms = InGraveyard(Trk("Open Communications"));
		Assert.That(
			Casts(comms).Select(c => c.ReturnId),
			Is.EquivalentTo(new[] { other }),
			"enchanted permanent can't be beamed up"
		);
	}

	// ===== Tier 3: strange new worlds =====

	[Test]
	public void StrangeNewWorlds_CountsDifferentlyNamedLands()
	{
		var traveler = Field(Trk("The Traveler"), me);
		Assert.That(s.Power(s.GetCard(traveler)), Is.EqualTo(5), "four of each basic: five names");
		Field(TestGames.Card("Forest"), me);
		Assert.That(s.Power(s.GetCard(traveler)), Is.EqualTo(5), "another Forest is no new name");
		Field(Trk("Bajor"), me);
		Assert.That(s.Power(s.GetCard(traveler)), Is.EqualTo(6));
		Field(Trk("Bajor"), them);
		Assert.That(s.Power(s.GetCard(traveler)), Is.EqualTo(6), "lands YOU control");
	}

	// ===== Tier 3: face a dilemma =====

	[Test]
	public void Dilemma_CastingAModalSpell_OnceForOneOrMoreModes()
	{
		var adjudicator = Field(Trk("Melkotian Adjudicator"), me);
		Cast(Hand(ChooseOneOrMore, me), [0, 1]);
		Assert.That(
			s.GetCard(adjudicator).PlusOneCounters,
			Is.EqualTo(1),
			"two modes, one dilemma"
		);
		Cast(Hand(TestCards.Fodder, me), []);
		Assert.That(s.GetCard(adjudicator).PlusOneCounters, Is.EqualTo(1), "no modes, no dilemma");
	}

	[Test]
	public void Dilemma_AModalTrigger()
	{
		var adjudicator = Field(Trk("Melkotian Adjudicator"), me);
		CastWithTrigger(Hand(Trk("Dominion Assault Force"), me), [1]);
		Assert.That(s.GetCard(adjudicator).PlusOneCounters, Is.EqualTo(1));
	}

	[Test]
	public void Dilemma_AModalActivation()
	{
		var adjudicator = Field(Trk("Melkotian Adjudicator"), me);
		var tea = Field(Trk("Tea, Earl Grey, Hot"), me);
		s = s.Act(Activations(tea).First(a => a.Modes.SequenceEqual([1])));
		Resolve();
		Assert.That(s.GetCard(adjudicator).PlusOneCounters, Is.EqualTo(1));
	}

	[Test]
	public void Dilemma_TheOpponentsModesAreNotYours()
	{
		var adjudicator = Field(Trk("Melkotian Adjudicator"), me);
		var theirs = Field(Trk("Tea, Earl Grey, Hot"), them);
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().Step == Step.PreCombatMain
		);
		s = s.Act(s.LegalActions().OfType<ActivateAbilityAction>().First(a => a.CardId == theirs));
		Resolve();
		Assert.That(s.GetCard(adjudicator).PlusOneCounters, Is.EqualTo(0), "whenever YOU face one");
	}

	// ===== Tier 3: power 8 =====

	[Test]
	public void Power8_JudgedAsAttackersAreDeclared_NotAgainOnResolution()
	{
		var officer = Field(Trk("Tactical Officer"), me);
		var giant = Field(TestGames.Creature("Giant", "6/6"), me);
		Declare(officer, giant);
		Pump(giant, -5); // after the declaration, in response: the total falls to 3
		ToSecondMain();
		var card = s.GetCard(officer);
		Assert.That(card.PlusOneCounters, Is.EqualTo(1), "it was 8 as attackers were declared");
		Assert.That(card.IsTapped, Is.False, "untap it");
	}

	[Test]
	public void Power8_SevenIsNotEnough_AndNonAttackersCount()
	{
		var officer = Field(Trk("Tactical Officer"), me);
		var five = Field(TestGames.Creature("Five", "5/5"), me);
		Declare(officer);
		ToSecondMain();
		Assert.That(s.GetCard(officer).PlusOneCounters, Is.EqualTo(0), "2 + 5 = 7");

		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == me && x.GetGame().Step == Step.PreCombatMain
		);
		Field(TestGames.Creature("One", "1/1"), me); // stays home: "creatures you control"
		Declare(officer);
		ToSecondMain();
		Assert.That(s.GetCard(officer).PlusOneCounters, Is.EqualTo(1), "2 + 5 + 1 = 8");
		Assert.That(Alive(five), Nu.True);
	}

	// ===== Tier 3: becomes tapped — every tap path =====

	/// <summary>A 1/1 with "{T}: Add {G}", "{T}: nothing", and "whenever this becomes tapped, gain 1 life".</summary>
	static CardDefinition Watcher =>
		TestGames.Creature("Watcher", "1/1") with
		{
			Components =
			[
				new ManaAbilityComponent(ManaColor.Green),
				new ActivatedAbilityComponent(ManaCost.None, []) { Costs = [new TapSelfCost()] },
				new TriggeredAbilityComponent(
					TriggerEvent.SelfBecomesTapped,
					[new GainLife(Ref.You, 1)]
				),
			],
		};

	[Test]
	public void BecomesTapped_Attacking()
	{
		var watcher = Field(Watcher, me);
		var life = Life(me);
		Declare(watcher);
		ToSecondMain();
		Assert.That(Life(me), Is.EqualTo(life + 1));
	}

	[Test]
	public void BecomesTapped_ATapCost()
	{
		var watcher = Field(Watcher, me);
		var life = Life(me);
		s = s.Act(Activations(watcher).Single());
		Resolve();
		Assert.That(Life(me), Is.EqualTo(life + 1));
	}

	[Test]
	public void BecomesTapped_TheTapEffect()
	{
		var watcher = Field(Watcher, me);
		var life = Life(me);
		Cast(
			Hand(TestCards.Targeted("Tap", TargetSpec.Creature, new Tap(Ref.Target())), me),
			[],
			watcher
		);
		Assert.That(Life(me), Is.EqualTo(life + 1));
		Cast(
			Hand(TestCards.Targeted("Tap", TargetSpec.Creature, new Tap(Ref.Target())), me),
			[],
			watcher
		);
		Assert.That(Life(me), Is.EqualTo(life + 1), "already tapped: it doesn't become tapped");
	}

	[Test]
	public void BecomesTapped_TappingForMana()
	{
		var watcher = Field(Watcher, me);
		var life = Life(me);
		s = s.Act(
			new TapForManaAction
			{
				PlayerId = me,
				CardId = watcher,
				Color = ManaColor.Green,
			}
		);
		Resolve();
		Assert.That(Life(me), Is.EqualTo(life + 1));
	}

	[Test]
	public void BecomesTapped_PayingForASpell()
	{
		var watcher = Field(Watcher, me);
		TapAllMyLands();
		var life = Life(me);
		Cast(Hand(TestCards.Fodder, me), []);
		Assert.That(s.GetCard(watcher).IsTapped, Nu.True, "its {G} paid the {1}");
		Assert.That(Life(me), Is.EqualTo(life + 1));
	}

	[Test]
	public void BecomesTapped_Station()
	{
		var watcher = Field(Watcher, me);
		var ship = Field(Trk("Oberth-Class Starship"), me);
		var life = Life(me);
		s = s.Act(Activations(ship).Single(a => a.TapId == watcher));
		Resolve();
		Assert.That(Life(me), Is.EqualTo(life + 1));
	}

	[Test]
	public void BecomesTapped_NotByEnteringTapped()
	{
		var watcher = Hand(Watcher, me);
		var life = Life(me);
		s = s.EnterBattlefield(watcher, me, tapped: true);
		Resolve();
		Assert.That(Life(me), Is.EqualTo(life));
	}

	// ===== Tier 3: look and take =====

	[Test]
	public void LookAndTake_RevealsTheOneTaken_TheRestToTheBottomUnknown()
	{
		var looked = Enumerable.Range(0, 5).Select(_ => OnTop(Bear)).ToList();
		var trinket = OnTop(TestCards.Trinket);
		var rutherford = Hand(Trk("Rutherford, Upgraded Ensign"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = rutherford });
		Resolve(c =>
			c switch
			{
				ChooseAmongChoice among => among.Options.Select(o => o.Id),
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(
			s.IsIn(trinket, me, ZoneType.Hand),
			Nu.True,
			"an artifact card, into your hand"
		);
		Assert.That(s.GetCard(trinket).KnownTo, Is.EqualTo(Knowers.Everyone), "revealed");
		var library = s.CardsIn(me, ZoneType.Library).Select(c => c.Id).ToList();
		Assert.That(library.TakeLast(5), Is.EquivalentTo(looked), "the rest on the bottom");
		Assert.That(
			looked.Select(id => s.GetCard(id).KnownTo),
			Has.All.EqualTo(Knowers.None),
			"a random order is not known"
		);
	}

	[Test]
	public void LookAndTake_OnlyWhatTheFilterAllows_AndYouMayDecline()
	{
		var bears = Enumerable.Range(0, 6).Select(_ => OnTop(Bear)).ToList();
		var rutherford = Hand(Trk("Rutherford, Upgraded Ensign"), me);
		var hand = HandCount(me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = rutherford });
		Resolve();
		Assert.That(HandCount(me), Is.EqualTo(hand - 1), "no artifact among them: nothing taken");
		Assert.That(
			s.CardsIn(me, ZoneType.Library).Select(c => c.Id).TakeLast(6),
			Is.EquivalentTo(bears)
		);
	}

	[Test]
	public void LookAndTake_PatternBuffer_OneToHand_TheOtherToTheGraveyard()
	{
		var second = OnTop(Bear);
		var first = OnTop(TestCards.Trinket);
		var hand = HandCount(me);
		Cast(Hand(Trk("Pattern Buffer Preservation"), me), []);
		Assert.That(
			HandCount(me),
			Is.EqualTo(hand + 1),
			"put one of them into your hand — not 'may'"
		);
		Assert.That(
			new[] { first, second }.Count(id => s.IsIn(id, me, ZoneType.Graveyard)),
			Is.EqualTo(1),
			"and the other into your graveyard"
		);
	}

	// ===== Tier 3: Federation =====

	[Test]
	public void Federation_CreatureTypesAmongNonBorg_NeverSpacecraft()
	{
		Field(TestCards.Typed(Bear, "Creature — Human Officer"), me);
		Field(TestCards.Typed(Bear, "Creature — Vulcan Officer"), me);
		Field(TestCards.Typed(Bear, "Artifact Creature — Borg Klingon"), me); // not considered at all
		var ship = Field(Trk("Oberth-Class Starship"), me);
		s = s.UpdateObject(ship, s.GetCard(ship) with { ChargeCounters = 6 }); // a creature: Spacecraft isn't a creature type
		Field(TestCards.Typed(Bear, "Creature — Romulan"), them);
		var target = Field(TestGames.Creature("Attacker", "9/9"), them);
		s = s.UpdateObject(target, s.GetCard(target) with { AttackingId = me }); // TEST SETUP
		var saavik = Hand(Trk("Saavik, Stoic Student"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = saavik });
		Resolve(c =>
			c switch
			{
				ChooseTargetChoice => [target],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(
			s.GetCard(target).Damage,
			Is.EqualTo(4),
			"Human, Officer, Vulcan, and Saavik's Advisor — never Borg or Klingon, never Spacecraft"
		);
	}

	// ===== Tier 2 =====

	[Test]
	public void PureEnergy_OneTargetGetsBoth_TwoGetOneEach()
	{
		var bear = Field(Bear, me);
		var other = Field(Bear, me);
		Cast(Hand(Trk("Pure Energy"), me), [], bear, 0);
		Assert.That(s.GetCard(bear).PlusOneCounters, Is.EqualTo(2), "one target: both counters");
		Cast(Hand(Trk("Pure Energy"), me), [], bear, other);
		Assert.That(
			(s.GetCard(bear).PlusOneCounters, s.GetCard(other).PlusOneCounters),
			Is.EqualTo((3, 1))
		);
	}

	[Test]
	public void PureEnergy_ASecondTargetGoneIllegal_LosesItsCounter()
	{
		var bear = Field(Bear, me);
		var other = Field(Bear, me);
		var energy = Hand(Trk("Pure Energy"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = energy,
				Targets = [bear, other],
			}
		);
		var kill = Hand(TestCards.Kill(), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = kill,
				Targets = [other],
			}
		);
		Resolve();
		Assert.That(Alive(other), Nu.False);
		Assert.That(
			s.GetCard(bear).PlusOneCounters,
			Is.EqualTo(1),
			"the split was announced as it was cast"
		);
	}

	[Test]
	public void Kirk_TheExiledCardIsPlayableThisTurnOnly()
	{
		var kirk = Field(Trk("Kirk, Commanding Officer"), me);
		var giant = Field(TestGames.Creature("Giant", "5/5"), me);
		var top = OnTop(TestCards.Fodder);
		Declare(kirk, giant);
		ToSecondMain();
		Assert.That(s.IsIn(top, me, ZoneType.Exile), Nu.True);
		Assert.That(Casts(top), Is.Not.Empty, "you may play that card this turn");
		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == me && x.GetGame().Step == Step.PreCombatMain
		);
		Assert.That(Casts(top), Is.Empty, "this turn — not your next");
	}

	[Test]
	public void Worf_TheTwoTappedAreYourChoice()
	{
		var worf = Field(Trk("Worf, Honor-Bound Warrior"), me);
		var bear = Field(Bear, me);
		var trinket = Field(TestCards.Trinket, me);
		var pairs = Activations(worf).Select(a => (a.TapId, a.SecondTapId)).ToList();
		Assert.That(
			pairs,
			Has.Count.EqualTo(6),
			"Worf, the bear, the trinket: each pair, either first"
		);
		Assert.That(
			pairs.All(p => p.TapId != 0 && p.SecondTapId != 0 && p.TapId != p.SecondTapId),
			Nu.True
		);
		Assert.That(
			s.DistinctMoves(Activations(worf)),
			Has.Count.EqualTo(3),
			"either order is one move"
		);
		s = s.Act(Activations(worf).Single(a => a.TapId == trinket && a.SecondTapId == bear)); // the trinket clicked first
		Resolve();
		Assert.That(s.GetCard(bear).IsTapped && s.GetCard(trinket).IsTapped, Nu.True);
		Assert.That(s.GetCard(worf).IsTapped, Nu.False, "the two you chose — not Worf");
		Assert.That(s.GetCard(worf).PlusOneCounters, Is.EqualTo(1));
	}

	[Test]
	public void TapTwo_ACopiedCostList_IsNotSplitAgain()
	{
		var worf = CardRegistry
			.Get("Worf, Honor-Bound Warrior")
			.Components.OfType<ActivatedAbilityComponent>()
			.Single();
		var copy = new ActivatedAbilityComponent(worf.Cost, worf.Effects) { Costs = worf.Costs };
		Assert.That(
			copy.Costs.OfType<TapUntappedCost>().Select(c => c.Slot),
			Is.EqualTo(new[] { CostSlot.Tap, CostSlot.SecondTap })
		);
	}

	[Test]
	public void BeamMeUp_TheReturnedCreatureMayTapForTheMana()
	{
		var comms = InGraveyard(Trk("Open Communications"));
		TapAllMyLands();
		foreach (var name in new[] { "Island", "Plains" })
		{
			var land = s.CardsIn(me, ZoneType.Battlefield).First(c => c.Name == name);
			s = s.UpdateObject(land.Id, land with { IsTapped = false }); // TEST SETUP: {U} and one more
		}
		var watcher = Field(Watcher, me);
		Assert.That(
			Casts(comms).Select(c => c.ReturnId),
			Does.Contain(watcher),
			"{2}{U}: two lands and the creature's {G}, then it is returned"
		);
		s = s.Act(Casts(comms).Single(c => c.ReturnId == watcher));
		Assert.That(s.IsIn(watcher, me, ZoneType.Hand), Nu.True);
	}

	[Test]
	public void GabrielLorca_FromTheGraveyard_PayBOnADilemma()
	{
		var lorca = InGraveyard(Trk("Gabriel Lorca, Mirror Menace"));
		var options = Hand(ChooseOneOrMore, me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = options,
				Modes = [0],
			}
		);
		Resolve(Yes);
		Assert.That(s.IsIn(lorca, me, ZoneType.Hand), Nu.True);
	}

	[Test]
	public void TellMeWhatYouSee_TheOpponentDecides()
	{
		var tell = Hand(Trk("Tell Me What You See"), me);
		var hand = HandCount(me);
		var theirLife = Life(them);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = tell });
		Resolve(c =>
			c switch
			{
				ChooseTargetChoice => [them],
				YesNoChoice => (
					s.DecidingPlayerId() == them
						? []
						: throw new AssertionException("the opponent decides")
				),
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(Life(them), Is.EqualTo(theirLife - 4), "if they don't, they lose 4 life");
		Assert.That(HandCount(me), Is.EqualTo(hand - 1), "no card drawn");
	}
}
