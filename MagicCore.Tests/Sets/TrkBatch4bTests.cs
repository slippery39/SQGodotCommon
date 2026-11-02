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
/// MC-5.3 batch 4b: delayed return, leaves the graveyard, type change, a second resolution, a granted
/// trigger, gaining control, "plus one" of every counter, and block and target restrictions through real
/// casting (tier 3), and each tier-2 card's new option against its PRINTED text.
/// </summary>
public class TrkBatch4bTests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static Func<ChoiceAction, IEnumerable<int>> Targeting(params int[] ids) =>
		c =>
			c switch
			{
				ChooseTargetChoice => ids,
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			};

	void CastAndResolve(int card, Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		s = s.Act(Casts(card).First());
		Resolve(answer);
	}

	void ToTurn(int turn) => s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn);

	bool InExile(int card) => s.IsIn(card, s.GetCard(card).OwnerId, ZoneType.Exile);

	// ===== Tier 3: delayed return =====

	[Test]
	public void GhostOfIllyria_ExilesMyPermanent_ItReturnsAtTheEndStepAsANewObject()
	{
		var bear = Field(Bear, me);
		s = s.AddPlusOneCounters(bear, 1);
		var ghost = Hand(Trk("Ghost of Illyria"), me);
		CastAndResolve(ghost, Targeting(bear));
		Assert.That(InExile(bear), Nu.True);
		var turn = s.GetGame().TurnNumber;
		ToTurn(turn + 1);
		Assert.That(s.IsIn(bear, me, ZoneType.Battlefield), Nu.True, "back under its owner");
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(0), "a new object (CR 400.7)");
	}

	[Test]
	public void TestOfPriorities_ExiledDuringTheEndStep_ReturnsAtTheNextOne()
	{
		var bear = Field(Bear, me);
		var test = Hand(Trk("Test of Priorities"), me);
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.End);
		Cast(test, [1], bear);
		Assert.That(InExile(bear), Nu.True);
		ToTurn(turn + 1);
		Assert.That(InExile(bear), Nu.True, "this end step had already begun");
		ToTurn(turn + 2);
		Assert.That(s.IsIn(bear, me, ZoneType.Battlefield), Nu.True);
	}

	// ===== Tier 3: block and target restrictions =====

	[Test]
	public void ColdBloodedCrew_CantBeBlockedByPowerTwoOrLess()
	{
		var crew = Field(Trk("Cold-Blooded Crew"), me);
		var bear = Field(Bear, them);
		var ox = Field(TestGames.Creature("Ox", "3/3"), them);
		Assert.That(Combat.CanBlock(s, s.GetCard(bear), s.GetCard(crew)), Nu.False);
		Assert.That(Combat.CanBlock(s, s.GetCard(ox), s.GetCard(crew)), Nu.True);
	}

	[Test]
	public void Species8472_CantBeBlockedByArtifactCreatures()
	{
		var species = Field(Trk("Species 8472"), me);
		var drone = Field(TestCards.Typed(Bear, "Artifact Creature — Drone"), them);
		var bear = Field(Bear, them);
		Assert.That(Combat.CanBlock(s, s.GetCard(drone), s.GetCard(species)), Nu.False);
		Assert.That(Combat.CanBlock(s, s.GetCard(bear), s.GetCard(species)), Nu.True);
	}

	[Test]
	public void Species8472_UntargetableByTheirArtifactsAbilities_NotByMineOrTheirCreatures()
	{
		var species = Field(Trk("Species 8472"), me);
		var theirs = Field(TestGames.Inline("Rod", "{1}", "Artifact"), them);
		var mine = Field(TestGames.Inline("Rod", "{1}", "Artifact"), me);
		var creature = Field(Bear, them);
		var spell = TargetSpec.Creature;
		Assert.That(spell.IsLegal(s, them, theirs, species), Nu.False);
		Assert.That(spell.IsLegal(s, me, mine, species), Nu.True);
		Assert.That(spell.IsLegal(s, them, creature, species), Nu.True);
	}

	[Test]
	public void Species8472_GainsLifePerDifferentlyNamedLand()
	{
		var species = Hand(Trk("Species 8472"), me);
		var life = Life(me);
		CastAndResolve(species);
		Assert.That(Life(me), Nu.EqualTo(life + 5), "the board's five basics");
	}

	// ===== Tier 3: leaves the graveyard =====

	int InGraveyard(int player, CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Graveyard);
		return id;
	}

	/// <summary>"Exile every creature card from your graveyard" — several cards leaving at once.</summary>
	static CardDefinition Purge =>
		TestGames.Inline(
			"Purge",
			"{1}",
			"Sorcery",
			null,
			new ExileFromGraveyard(Ref.All(ZoneType.Graveyard, Is.Creature, of: Ref.You))
		);

	[Test]
	public void Blastoneuron_AHastyCopyOfTheExiledCard_SacrificedAtTheEndStep_AndADrain()
	{
		var bear = InGraveyard(me, Bear);
		var blast = Hand(Trk("Blastoneuron"), me);
		var (mine, theirs) = (Life(me), Life(them));
		CastAndResolve(blast, Targeting(bear));
		Assert.That(InExile(bear), Nu.True);
		var copy = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Bear");
		Assert.That(copy.IsToken && s.Has(copy, "Haste"), Nu.True);
		Assert.That((Life(me), Life(them)), Nu.EqualTo((mine + 1, theirs - 1)), "a card left");
		ToTurn(s.GetGame().TurnNumber + 1);
		Assert.That(s.CardsIn(me, ZoneType.Battlefield).Any(c => c.Name == "Bear"), Nu.False);
	}

	[Test]
	public void Blastoneuron_OneOrMoreLeavingTogether_TriggersOnce()
	{
		Field(Trk("Blastoneuron"), me);
		InGraveyard(me, Bear);
		InGraveyard(me, Bear);
		var theirs = Life(them);
		Cast(Hand(Purge, me), []);
		Assert.That(s.CardsIn(me, ZoneType.Graveyard).Count(c => c.Name == "Bear"), Nu.EqualTo(0));
		Assert.That(Life(them), Nu.EqualTo(theirs - 1));
	}

	[Test]
	public void HologramStalker_ItsOwnReturnMakesAnIllusion_OnlyOnceEachTurn()
	{
		var bear = InGraveyard(me, Bear);
		InGraveyard(me, TestGames.Creature("Ox", "3/3"));
		var stalker = Hand(Trk("Hologram Stalker"), me);
		CastAndResolve(stalker, Targeting(bear));
		Assert.That(s.IsIn(bear, me, ZoneType.Hand), Nu.True);
		Cast(Hand(Purge, me), []);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Illusion"),
			Nu.EqualTo(1)
		);
	}

	[Test]
	public void Vash_ACardLeavingDrawsOnce_TheirGraveyardDoesntCount()
	{
		Field(Trk("Vash, Freelance Archaeologist"), me);
		InGraveyard(them, Bear);
		var theirPurge = TestGames.Inline(
			"Purge",
			"{1}",
			"Sorcery",
			null,
			new ExileFromGraveyard(Ref.All(ZoneType.Graveyard, Is.Creature, of: Ref.EachOpponent))
		);
		var spell = Hand(theirPurge, me);
		var hand = HandCount(me);
		Cast(spell, []);
		Assert.That(HandCount(me), Nu.EqualTo(hand - 1), "only the spell left my hand");
		InGraveyard(me, Bear);
		InGraveyard(me, Bear);
		var purge = Hand(Purge, me);
		hand = HandCount(me);
		Cast(purge, []);
		Assert.That(HandCount(me), Nu.EqualTo(hand), "the spell, then one draw");
	}

	// ===== Tier 3: type change =====

	static CardDefinition Officer => TestCards.Typed(Bear, "Creature — Human Officer");

	[Test]
	public void ImADoctor_ACounterUntapAndPump_ADoctorUntilEndOfTurnOnly()
	{
		var officer = Field(Officer, me);
		s = s.Tap(officer);
		Cast(Hand(Trk("I'm a Doctor, Not a . . ."), me), [], officer);
		var card = s.GetCard(officer);
		Assert.That(
			(card.PlusOneCounters, card.IsTapped, s.Power(card)),
			Nu.EqualTo((1, false, 4))
		);
		Assert.That(s.Subtypes(card), Nu.EqualTo(new[] { "Doctor" }));
		Assert.That(
			Is.Subtype("Human").Matches(s, card, new(me, 0)),
			Nu.False,
			"every reader: the filter"
		);
		ToTurn(s.GetGame().TurnNumber + 1);
		Assert.That(s.Subtypes(s.GetCard(officer)), Nu.EqualTo(new[] { "Human", "Officer" }));
	}

	[Test]
	public void ImADoctor_FederationCountsTheDoctor_NotItsOldTypes_AndABorgDoctorCounts()
	{
		var borg = Field(TestCards.Typed(Bear, "Artifact Creature — Borg"), me);
		var vulcan = Field(TestCards.Typed(Bear, "Creature — Vulcan"), me);
		var federation = Count.CreatureTypesAmong(
			Ref.Permanents(Is.Creature & Is.Yours & !Is.Subtype("Borg"))
		);
		var context = System.Collections.Immutable.ImmutableDictionary<string, object>.Empty.Add(
			ContextKeys.Controller,
			me
		);
		Assert.That(federation.Value(s, context), Nu.EqualTo(1), "Vulcan; Borg not considered");
		Cast(Hand(Trk("I'm a Doctor, Not a . . ."), me), [], borg);
		Assert.That(
			federation.Value(s, context),
			Nu.EqualTo(2),
			"no longer a Borg: Vulcan, Doctor"
		);
		Cast(Hand(Trk("I'm a Doctor, Not a . . ."), me), [], vulcan);
		Assert.That(federation.Value(s, context), Nu.EqualTo(1), "two Doctors, no Vulcan");
	}

	[Test]
	public void DeEvolve_MinusFourZero_CantBlock_ASalamander_ExiledAtUpkeepWithThreshold()
	{
		var warrior = Field(
			TestCards.Typed(TestGames.Creature("Ox", "5/5"), "Creature — Human Warrior"),
			them
		);
		Cast(Hand(Trk("De-evolve"), me), [], warrior);
		var card = s.GetCard(warrior);
		Assert.That(s.Power(card), Nu.EqualTo(1));
		Assert.That(s.Has(card, Combat.CantBlock), Nu.True);
		Assert.That(s.Subtypes(card), Nu.EqualTo(new[] { "Salamander" }));
		var first = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == first + 1 && x.GetGame().Step == Step.Draw
		);
		Assert.That(s.IsOnBattlefield(warrior), Nu.True, "their upkeep: no threshold yet");
		for (var i = 0; i < 7; i++)
			InGraveyard(me, Bear);
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == turn + 1 && x.GetGame().Step == Step.Draw
		);
		Assert.That(InExile(warrior), Nu.True);
	}

	// ===== Tier 3: the second resolution this turn =====

	/// <summary>A land enters under my control — a {1} sorcery's New Planet token — and its landfall resolves.</summary>
	void LandEnters() =>
		Cast(
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
			[]
		);

	[Test]
	public void CaptainBurnham_ADamageEachLand_ACounterOnlyOnTheSecond()
	{
		var burnham = Field(Trk("Captain Michael Burnham"), me);
		var theirs = Life(them);
		LandEnters();
		Assert.That(s.GetCard(burnham).PlusOneCounters, Nu.EqualTo(0));
		LandEnters();
		Assert.That(s.GetCard(burnham).PlusOneCounters, Nu.EqualTo(1));
		LandEnters();
		Assert.That(
			s.GetCard(burnham).PlusOneCounters,
			Nu.EqualTo(1),
			"the third isn't the second"
		);
		Assert.That(Life(them), Nu.EqualTo(theirs - 3));
	}

	[Test]
	public void JakeSisko_TheCountStartsAgainEachTurn()
	{
		Field(Trk("Jake Sisko, War Correspondent"), me);
		LandEnters();
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == turn + 2 && x.GetGame().Step == Step.PreCombatMain
		);
		LandEnters();
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Any(c => c.Name == "Clue"),
			Nu.False,
			"one each turn"
		);
		LandEnters();
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Clue"),
			Nu.EqualTo(1)
		);
	}

	// ===== Tier 3: a granted trigger =====

	/// <summary>Attacks them with these, answering every "you may" yes; how many were asked.</summary>
	int AttackWith(params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([attackers.Contains(declare.CreatureId) ? them : CombatOption.None]);
		var asked = 0;
		while (true)
		{
			s = TestGames.PassUntil(
				s,
				x => x.GetPendingChoice() is YesNoChoice || x.GetGame().Step == Step.PostCombatMain
			);
			if (s.GetPendingChoice() is not YesNoChoice yes)
				return asked;
			asked++;
			s = s.Choose([yes.Options[0].Id]);
		}
	}

	[Test]
	public void HiveMind_ItsArtifactCreaturesPayALifeToDraw_NotOthers()
	{
		var hive = Field(Trk("Hive Mind Coprocessor"), me);
		var drone = Field(TestCards.Typed(Bear, "Artifact Creature — Drone"), me);
		var ox = Field(TestGames.Creature("Ox", "3/3"), me);
		var (hand, life) = (HandCount(me), Life(me));
		var asked = AttackWith(hive, drone, ox);
		Assert.That(asked, Nu.EqualTo(2), "Hive Mind itself and the Drone");
		Assert.That((HandCount(me), Life(me)), Nu.EqualTo((hand + 2, life - 2)));
	}

	// ===== Tier 3: gaining control =====

	[Test]
	public void RulesOfAcquisition_TheThirdRule_IStealIt_TheyDrawThree_ItsSummoningSickForMe()
	{
		var bear = Field(Bear, them);
		s = s.AddPlusOneCounters(bear, 1);
		var hand = HandCount(them);
		Cast(Hand(Trk("Rules of Acquisition"), me), [0], bear);
		Assert.That(s.IsIn(bear, me, ZoneType.Battlefield), Nu.True, "in my battlefield zone");
		var card = s.GetCard(bear);
		Assert.That(
			(card.ControllerId, card.OwnerId, card.PlusOneCounters),
			Nu.EqualTo((me, them, 1))
		);
		Assert.That(Combat.CanAttack(s, card), Nu.False, "summoning sick for me");
		Assert.That(HandCount(them), Nu.EqualTo(hand + 3));
	}

	[Test]
	public void RulesOfAcquisition_TheNinetyEighthRule_TheyGetMine_IfTheyDoIDrawThree()
	{
		var bear = Field(Bear, me);
		var rules = Hand(Trk("Rules of Acquisition"), me);
		var hand = HandCount(me);
		Cast(rules, [1], them, bear);
		Assert.That(s.IsIn(bear, them, ZoneType.Battlefield), Nu.True);
		Assert.That(HandCount(me), Nu.EqualTo(hand - 1 + 3));
	}

	[Test]
	public void RulesOfAcquisition_AStolenPermanentGhostExiles_ReturnsToItsOwner()
	{
		var bear = Field(Bear, them);
		Cast(Hand(Trk("Rules of Acquisition"), me), [0], bear);
		CastAndResolve(Hand(Trk("Ghost of Illyria"), me), Targeting(bear));
		ToTurn(s.GetGame().TurnNumber + 1);
		Assert.That(s.IsIn(bear, them, ZoneType.Battlefield), Nu.True, "under its owner's control");
	}

	// ===== Tier 3: Brad Boimler's "plus one" of every kind =====

	/// <summary>A {1} instant: [effect] on target creature.</summary>
	void On(int target, GameAction effect) =>
		Cast(Hand(TestCards.Targeted("Do", TargetSpec.Creature, effect), me), [], target);

	void TapBrad(int brad) => On(brad, new Tap(Ref.Target()));

	int Entering(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Hand);
		s = s.EnterBattlefield(id, me);
		return id;
	}

	[Test]
	public void BradBoimler_OnceTapped_EveryCounterPathPutsOneMore_OnMineOnly()
	{
		var brad = Field(Trk("Brad Boimler, Eager Ensign"), me);
		var ox = Field(TestGames.Creature("Ox", "5/5"), me);
		var elk = Field(TestGames.Creature("Elk", "5/5"), me);
		var walker = Field(TestGames.Planeswalker("Walker", 3), me);
		var theirs = Field(Bear, them);
		TapBrad(brad);
		s = s.PutCounters(ox, CounterKinds.Quest, 1);
		s = s.AddPlusOneCounters(ox, 1);
		On(ox, new AddChargeCounters(Ref.Target(), 1));
		On(ox, new AddStunCounters(Ref.Target(), 1));
		On(elk, new AddMinusOneCounters(Ref.Target(), 1));
		s = Loyalty.Change(s, walker, 1);
		var card = s.GetCard(ox);
		Assert.That(
			(
				card.CountersOf(CounterKinds.Quest),
				card.PlusOneCounters,
				card.ChargeCounters,
				card.StunCounters
			),
			Nu.EqualTo((2, 2, 2, 2))
		);
		Assert.That(s.GetCard(elk).MinusOneCounters, Nu.EqualTo(2));
		Assert.That(s.GetCard(walker).Loyalty, Nu.EqualTo(2));
		var entered = Entering(TestGames.Planeswalker("Walker Two", 3));
		Assert.That(
			s.GetCard(entered).Loyalty,
			Nu.EqualTo(4),
			"entering with them is putting them on"
		);
		var vanishing = Entering(new CardDefinition(Bear.Data, [.. Vanishing.Of(2)]));
		Assert.That(s.GetCard(vanishing).TimeCounters, Nu.EqualTo(3));
		s = s.AddPlusOneCounters(theirs, 1);
		Assert.That(s.GetCard(theirs).PlusOneCounters, Nu.EqualTo(1), "a permanent YOU control");
		Cast(Hand(TestGames.Inline("Grow", "{1}", "Sorcery", null, new Proliferate(1)), me), []);
		Assert.That(s.GetCard(ox).ChargeCounters, Nu.EqualTo(4), "proliferate puts one, plus one");
	}

	[Test]
	public void BradBoimler_TwoTriggersPutTwoMore_UntilEndOfTurn()
	{
		var brad = Field(Trk("Brad Boimler, Eager Ensign"), me);
		var ox = Field(TestGames.Creature("Ox", "5/5"), me);
		TapBrad(brad);
		On(brad, new Untap(Ref.Target()));
		TapBrad(brad);
		s = s.PutCounters(ox, CounterKinds.Quest, 1);
		Assert.That(s.GetCard(ox).CountersOf(CounterKinds.Quest), Nu.EqualTo(3));
		ToTurn(s.GetGame().TurnNumber + 1);
		s = s.PutCounters(ox, CounterKinds.Quest, 1);
		Assert.That(s.GetCard(ox).CountersOf(CounterKinds.Quest), Nu.EqualTo(4));
	}

	// ===== Tier 2: exile until a nonland; tier 3: cast from your graveyard =====

	int OnTop(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(me, ZoneType.Library)); // TEST SETUP: index 0 is the top
		return id;
	}

	[Test]
	public void ExploratoryMission_ExilesUntilANonland_OnlyThatPlayable_RRAtFiveQuests()
	{
		var mission = Field(Trk("Exploratory Mission"), me);
		var bear = OnTop(Bear);
		var land = OnTop(TestGames.Inline("Waste", "", "Land"));
		LandEnters();
		Assert.That(s.GetCard(mission).CountersOf(CounterKinds.Quest), Nu.EqualTo(1));
		Assert.That(InExile(land) && InExile(bear), Nu.True, "the land, then the nonland");
		Assert.That(
			global::MagicCore.Rules.Playable.IsImpulsePlayableBy(s, s.GetCard(bear), me),
			Nu.True
		);
		Assert.That(
			global::MagicCore.Rules.Playable.IsImpulsePlayableBy(s, s.GetCard(land), me),
			Nu.False
		);
		Assert.That(s.GetPlayer(me).ManaPool.Red, Nu.EqualTo(0));
		s = s.PutCounters(mission, CounterKinds.Quest, 3);
		LandEnters();
		Assert.That(s.GetPlayer(me).ManaPool.Red, Nu.EqualTo(2), "five quest counters: add {R}{R}");
	}

	[Test]
	public void StoriedVeteran_CastFromTheGraveyard_EntersWithFinality_ExiledWhenItDies()
	{
		var veteran = InGraveyard(me, Trk("Storied Veteran"));
		Assert.That(Casts(veteran), Nu.Not.Empty);
		CastAndResolve(veteran);
		Assert.That(
			s.IsIn(veteran, me, ZoneType.Battlefield),
			Nu.True,
			"not exiled as flashback is"
		);
		Assert.That(s.GetCard(veteran).HasFinalityCounter, Nu.True);
		Assert.That(s.Has(s.GetCard(veteran), Combat.CantBlock), Nu.True);
		Cast(Hand(TestCards.Kill(), me), [], veteran);
		Assert.That(InExile(veteran), Nu.True);
	}

	[Test]
	public void StoriedVeteran_CastFromTheHand_NoFinality()
	{
		var veteran = Hand(Trk("Storied Veteran"), me);
		CastAndResolve(veteran);
		Assert.That(s.GetCard(veteran).HasFinalityCounter, Nu.False);
	}

	// ===== Tier 3: a land from the hand, a land back at the next end step =====

	int Lands(int p) => s.CardsIn(p, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));

	[Test]
	public void EricaOrtegas_TappedPutsALandFromHand_ALandReturnsAtTheEndStep()
	{
		var erica = Field(Trk("Erica Ortegas, Plucky Pilot"), me);
		var land = Hand(TestGames.Inline("Waste", "", "Land"), me);
		var lands = Lands(me);
		var tap = Hand(TestCards.Targeted("Tap", TargetSpec.Creature, new Tap(Ref.Target())), me);
		s = s.Act(Casts(tap).Single(c => c.Targets[0] == erica));
		Resolve(c =>
			c switch
			{
				ChooseCardsFromHandChoice => [land],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(s.IsIn(land, me, ZoneType.Battlefield), Nu.True);
		Assert.That(Lands(me), Nu.EqualTo(lands + 1));
		ToTurn(s.GetGame().TurnNumber + 1);
		Assert.That(Lands(me), Nu.EqualTo(lands), "a land you control, back to hand");
		Assert.That(s.CardsIn(me, ZoneType.Command), Nu.Empty, "the delayed trigger is gone");
	}

	[Test]
	public void EricaOrtegas_NoLandPut_NothingReturns()
	{
		var erica = Field(Trk("Erica Ortegas, Plucky Pilot"), me);
		Hand(TestGames.Inline("Waste", "", "Land"), me);
		var lands = Lands(me);
		On(erica, new Tap(Ref.Target())); // "you may": answered no
		ToTurn(s.GetGame().TurnNumber + 1);
		Assert.That(Lands(me), Nu.EqualTo(lands));
	}

	// ===== The review's fixes =====

	/// <summary>On to my end step with the delayed trigger on the stack; the ability.</summary>
	StackAbility EndStepTrigger()
	{
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.End && !x.StackIsEmpty());
		return (StackAbility)
			s.GetObject(s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack)).First());
	}

	static CardDefinition Stifle =>
		TestCards.Targeted("Stifle", TargetSpec.Ability(), new Counter(Ref.Target()));

	[Test]
	public void GhostOfIllyria_ItsReturnCountered_TheCardStaysExiled_TheTriggerIsMine()
	{
		var bear = Field(Bear, them);
		Cast(Hand(Trk("Rules of Acquisition"), me), [0], bear);
		CastAndResolve(Hand(Trk("Ghost of Illyria"), me), Targeting(bear));
		var trigger = EndStepTrigger();
		Assert.That(
			trigger.ControllerId,
			Nu.EqualTo(me),
			"CR 603.7d: Ghost's controller, not the card's owner"
		);
		var stifle = Hand(Stifle, me);
		Cast(stifle, [], trigger.Id);
		ToTurn(s.GetGame().TurnNumber + 2);
		Assert.That(InExile(bear), Nu.True, "a delayed trigger triggers only once (CR 603.7c)");
	}

	[Test]
	public void EricaOrtegas_TheReturnCountered_NoLandReturnsAtALaterEndStep()
	{
		var erica = Field(Trk("Erica Ortegas, Plucky Pilot"), me);
		var land = Hand(TestGames.Inline("Waste", "", "Land"), me);
		var tap = Hand(TestCards.Targeted("Tap", TargetSpec.Creature, new Tap(Ref.Target())), me);
		s = s.Act(Casts(tap).Single(c => c.Targets[0] == erica));
		Resolve(c =>
			c switch
			{
				ChooseCardsFromHandChoice => [land],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		var lands = Lands(me);
		var trigger = EndStepTrigger();
		var stifle = Hand(Stifle, me);
		Cast(stifle, [], trigger.Id);
		ToTurn(s.GetGame().TurnNumber + 2);
		Assert.That(Lands(me), Nu.EqualTo(lands));
		Assert.That(s.CardsIn(me, ZoneType.Command), Nu.Empty);
	}

	[Test]
	public void DeEvolve_ThatLostItsAbilities_NoLongerSetsTheType()
	{
		var warrior = Field(TestCards.Typed(Bear, "Creature — Human Warrior"), them);
		var aura = Hand(Trk("De-evolve"), me);
		Cast(aura, [], warrior);
		var strip = Hand(
			TestCards.Targeted(
				"Strip",
				TargetSpec.Permanent(),
				new ModifyUntilEndOfTurn(Ref.Target(), 0, 0) { LosesAllAbilities = true }
			),
			me
		);
		Cast(strip, [], aura);
		Assert.That(s.Subtypes(s.GetCard(warrior)), Nu.EqualTo(new[] { "Human", "Warrior" }));
	}
}
