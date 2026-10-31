using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Abilities activated from the hand (landcycling) and the graveyard, finality and stun counters.</summary>
public class FraGraveyardTests : FraBoardTest
{
	static readonly string[] Landcyclers =
	[
		"Hexhaven Battalion",
		"Undulating Witness",
		"Apex Witchstalker",
		"Awaken the Inferno",
		"Vinelasher Adept",
	];

	int InGraveyard(string fra)
	{
		int id;
		(s, id) = TestGames.Put(s, me, CardRegistry.Get(fra), ZoneType.Graveyard);
		return id;
	}

	int InGraveyard(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Graveyard);
		return id;
	}

	void Activate(ActivateAbilityAction action, Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		s = s.Act(action);
		Resolve(answer);
	}

	bool InExile(int card) => s.IsIn(card, me, ZoneType.Exile);

	[TestCaseSource(nameof(Landcyclers))]
	public void BasicLandcycling_DiscardsItForABasicLandFromTheLibrary(string fra)
	{
		var card = Hand(fra, me);
		var forest = s.CardsIn(me, ZoneType.Library).First(c => c.Name == "Forest").Id;
		var hand = HandCount(me);

		Activate(
			Activations(card).Single(),
			choice =>
				choice switch
				{
					ChooseFromLibraryChoice => [forest],
					_ => [],
				}
		);

		Assert.That(s.IsIn(card, me, ZoneType.Graveyard), "discarded");
		Assert.That(s.IsIn(forest, me, ZoneType.Hand), "the basic land");
		Assert.That(HandCount(me), Is.EqualTo(hand));
	}

	[Test]
	public void BasicLandcycling_OffersOnlyBasicLands()
	{
		var card = Hand("Hexhaven Battalion", me);
		(s, _) = TestGames.Put(s, me, TestGames.Card("Hall of Echoes"), ZoneType.Library);
		IEnumerable<string> offered = [];
		Activate(
			Activations(card).Single(),
			choice =>
			{
				if (choice is ChooseFromLibraryChoice)
					offered = choice.Options.Select(o => o.DisplayText);
				return [];
			}
		);
		Assert.That(offered, Is.EqualTo(new[] { "Forest" }), "basic, one option per name");
	}

	[Test]
	public void HexhavenBattalion_ThreeCadetsAndEmpowerJaceTwo()
	{
		Cast(Hand("Hexhaven Battalion", me), []);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Cadet"),
			Is.EqualTo(3)
		);
		Assert.That(Jace(me)!.Loyalty, Is.EqualTo(2));
	}

	[Test]
	public void UndulatingWitness_PaysTwoForPlusOneMinusOne()
	{
		var witness = Field(CardRegistry.Get("Undulating Witness"), me);
		Activate(Activations(witness).Single());
		Assert.That(
			(s.Power(s.GetCard(witness)), s.Toughness(s.GetCard(witness))),
			Is.EqualTo((4, 4))
		);
	}

	[Test]
	public void ApexWitchstalker_GainsTwoOnEnteringAndOnDying()
	{
		var stalker = Hand("Apex Witchstalker", me);
		var life = Life(me);
		Cast(stalker, []);
		Assert.That(Life(me), Is.EqualTo(life + 2));

		s = s.Sacrifice(stalker);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		Resolve();
		Assert.That(Life(me), Is.EqualTo(life + 4));
	}

	[Test]
	public void AwakenTheInferno_SixToTheirs_AndACounterOnUpToOneOfYours()
	{
		var theirs = Field(TestGames.Creature("Theirs", "5/5"), them);
		var mine = Field(TestGames.Creature("Mine", "2/2"), me);
		Cast(Hand("Awaken the Inferno", me), [], theirs, mine);
		Assert.That((Alive(theirs), s.GetCard(mine).PlusOneCounters), Is.EqualTo((false, 1)));
	}

	[Test]
	public void VinelasherAdept_EnteringPutsThreeCounters()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var adept = Hand("Vinelasher Adept", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = adept });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [bear],
				_ => [],
			}
		);
		Assert.That(s.GetCard(bear).PlusOneCounters, Is.EqualTo(3));
	}

	[Test]
	public void CampusCrier_ExilesItselfFromTheGraveyardToEmpowerJace()
	{
		var crier = InGraveyard("Campus Crier");
		Activate(Activations(crier).Single());
		Assert.That((InExile(crier), Jace(me)!.Loyalty), Is.EqualTo((true, 2)));
	}

	[Test]
	public void CampusCrier_OnTheBattlefield_HasNoAbility()
	{
		var crier = Field(CardRegistry.Get("Campus Crier"), me);
		Assert.That(Activations(crier), Is.Empty, "only from the graveyard");
	}

	[Test]
	public void CryotheoryAdept_TapsAndStuns_TheStunEatsAnUntap()
	{
		var adept = InGraveyard("Cryotheory Adept");
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);

		Activate(Activations(adept).Single(a => a.Targets[0] == bear));
		Assert.That(
			(s.GetCard(bear).IsTapped, s.GetCard(bear).StunCounters, InExile(adept)),
			Is.EqualTo((true, 1, true))
		);

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.Upkeep && x.GetGame().ActivePlayerId == them
		);
		Assert.That(
			(s.GetCard(bear).IsTapped, s.GetCard(bear).StunCounters),
			Is.EqualTo((true, 0)),
			"the stun counter came off instead"
		);

		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().Step == Step.Upkeep
				&& x.GetGame().ActivePlayerId == them
				&& x.GetGame().TurnNumber == 4
		);
		Assert.That(s.GetCard(bear).IsTapped, Is.False);
	}

	[Test]
	public void CryotheoryAdept_OnlyAsASorcery()
	{
		var adept = InGraveyard("Cryotheory Adept");
		Field(TestGames.Creature("Bear", "2/2"), them);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = them });
		Assert.That(Activations(adept), Is.Empty);
	}

	[Test]
	public void TheoreticalNecromancer_ReturnsAnotherCreatureCardToHand()
	{
		var necromancer = InGraveyard("Theoretical Necromancer");
		var other = InGraveyard(TestGames.Creature("Other", "2/2"));
		Assert.That(
			Activations(necromancer).Select(a => a.Targets[0]),
			Is.EqualTo(new[] { other }),
			"another — never itself"
		);

		Activate(Activations(necromancer).Single());
		Assert.That(
			(s.IsIn(other, me, ZoneType.Hand), InExile(necromancer)),
			Is.EqualTo((true, true))
		);
	}

	[Test]
	public void BlessedGhoul_ReturnsItselfToHand()
	{
		var ghoul = InGraveyard("Blessed Ghoul");
		Activate(Activations(ghoul).Single());
		Assert.That(s.IsIn(ghoul, me, ZoneType.Hand));
	}

	[Test]
	public void GrimRepriser_ReturnsWithFinality_OnlyAfterNoncombatDamage_AndIsExiledInsteadOfDying()
	{
		var repriser = InGraveyard("Grim Repriser");
		Assert.That(Activations(repriser), Is.Empty, "no noncombat damage yet");

		var ping = Hand(
			TestGames.Inline("Ping", "{R}", "Instant", null, new DealDamage(Ref.EachOpponent, 1)),
			me
		);
		Cast(ping, []);
		Activate(Activations(repriser).Single());
		Assert.That(Alive(repriser) && s.GetCard(repriser).HasFinalityCounter);

		s = s.Sacrifice(repriser);
		Assert.That(InExile(repriser), "a finality counter: exiled instead");
	}

	[Test]
	public void ProctorOfPotential_SurveilsAsACreatureEnters_ReturnsOnlyAfterASurveil()
	{
		var proctor = InGraveyard("Proctor of Potential");
		Assert.That(Activations(proctor), Is.Empty, "nothing surveilled yet");

		var peek = Hand(TestGames.Inline("Peek", "{U}", "Instant", null, new Surveil(1)), me);
		Cast(peek, []);
		Activate(Activations(proctor).Single());
		Assert.That(Alive(proctor) && s.GetCard(proctor).HasFinalityCounter);

		var surveils = s.GetPlayer(me).ThisTurn.Surveils;
		Cast(Hand(TestGames.Creature("Bear", "2/2"), me), []);
		Assert.That(
			s.GetPlayer(me).ThisTurn.Surveils,
			Is.EqualTo(surveils + 1),
			"another creature entered"
		);
	}

	[Test]
	public void GalliaTragicHost_ExilesAnotherCreatureCardToReturnTappedWithACounter()
	{
		var gallia = InGraveyard("Gallia, Tragic Host");
		Assert.That(Activations(gallia), Is.Empty, "no OTHER creature card to exile");
		var fodder = InGraveyard(TestGames.Creature("Fodder", "1/1"));

		Activate(Activations(gallia).Single());

		Assert.That(InExile(fodder));
		Assert.That(
			(Alive(gallia), s.GetCard(gallia).IsTapped, s.GetCard(gallia).PlusOneCounters),
			Is.EqualTo((true, true, 1))
		);
	}

	[Test]
	public void HallwayHeckler_TapAndDiscardACardToDraw()
	{
		var heckler = Field(CardRegistry.Get("Hallway Heckler // Vicious Verse"), me);
		var fodder = Hand(TestGames.Card("Forest"), me);
		var hand = HandCount(me);

		Activate(Activations(heckler).Single(a => a.DiscardId == fodder));

		Assert.That(s.IsIn(fodder, me, ZoneType.Graveyard));
		Assert.That((HandCount(me), s.GetCard(heckler).IsTapped), Is.EqualTo((hand, true)));
	}

	[Test]
	public void AGraveyardAbilitysCondition_IsJudgedForTheActivatingPlayer()
	{
		var repriser = InGraveyard("Grim Repriser");
		s = s.UpdateObject(repriser, s.GetCard(repriser) with { ControllerId = them }); // TEST SETUP: died under their control
		var ping = Hand(
			TestGames.Inline("Ping", "{R}", "Instant", null, new DealDamage(Ref.EachOpponent, 1)),
			me
		);
		Cast(ping, []);

		Assert.That(Activations(repriser), Is.Not.Empty, "MY opponent took noncombat damage");
	}

	[Test]
	public void TwoIdenticalGraveyardCards_AreOneMove()
	{
		InGraveyard("Blessed Ghoul");
		InGraveyard("Blessed Ghoul");
		Assert.That(
			s.DistinctMoves(s.LegalActions()).OfType<ActivateAbilityAction>().Count(),
			Is.EqualTo(1)
		);
	}

	// ===== Graveyard returns, hexproof, indestructible, exhaust =====

	[Test]
	public void LilianaTheRepentant_MillsTwoAsAnotherCreatureEnters()
	{
		Field(CardRegistry.Get("Liliana the Repentant"), me);
		var graveyard = s.CardsIn(me, ZoneType.Graveyard).Count();
		Cast(Hand(TestGames.Creature("Bear", "2/2"), me), []);
		Assert.That(s.CardsIn(me, ZoneType.Graveyard).Count(), Is.EqualTo(graveyard + 2));
	}

	[Test]
	public void LilianaTheRepentant_ExhaustReturnsACreatureCard_OnlyOnce()
	{
		var liliana = Field(CardRegistry.Get("Liliana the Repentant"), me);
		var first = InGraveyard(TestGames.Creature("First", "2/2"));
		var second = InGraveyard(TestGames.Creature("Second", "2/2"));

		Activate(Activations(liliana).First(a => a.Targets[0] == first));

		Assert.That(Alive(first));
		Assert.That(s.GetCard(liliana).PlusOneCounters, Is.EqualTo(1));
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.PreCombatMain && x.GetGame().TurnNumber == 3
		);
		Assert.That(s.IsIn(second, me, ZoneType.Graveyard));
		Assert.That(Activations(liliana), Is.Empty, "exhaust: once, ever");
	}

	/// <summary>What the opponent's spell may target right now, with them holding priority — the real casting path.</summary>
	IEnumerable<int> TheirTargets(int spell)
	{
		if (s.GetGame().PriorityPlayerId == me)
			s = s.Act(new PassPriorityAction { PlayerId = me });
		return s.LegalActions()
			.OfType<CastSpellAction>()
			.Where(c => c.CardId == spell)
			.Select(c => c.Targets[0]);
	}

	CardDefinition DestroyTargetPermanent() =>
		new(
			TestGames.Inline("Shatter", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([new Destroy(Ref.Target())])
				{
					Targets = [TargetSpec.Permanent()],
				},
			]
		);

	[Test]
	public void MarwynThePreserver_YourLandsHaveHexproof_AndALandCardComesBack()
	{
		var marwyn = Field(CardRegistry.Get("Marwyn, the Preserver"), me);
		var land = s.CardsIn(me, ZoneType.Battlefield).First(c => c.Is(CardType.Land)).Id;
		var theirLand = s.CardsIn(them, ZoneType.Battlefield).First(c => c.Is(CardType.Land)).Id;
		var shatter = Hand(DestroyTargetPermanent(), them);
		var targets = TheirTargets(shatter).ToList();
		Assert.That(targets, Does.Not.Contain(land), "an opponent can't target your lands");
		Assert.That(targets, Does.Contain(marwyn).And.Contain(theirLand), "only your lands");

		s = s.Act(new PassPriorityAction { PlayerId = them }); // back to my main phase
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().Step == Step.PreCombatMain
				&& x.GetGame().ActivePlayerId == me
				&& x.GetGame().TurnNumber == 3
		);

		var dead = InGraveyard(TestGames.Card("Forest"));
		Activate(Activations(marwyn).Single(a => a.Targets[0] == dead));
		Assert.That(s.IsIn(dead, me, ZoneType.Hand));
	}

	[Test]
	public void RuricTharMagecrusher_HexproofUntilItDealsCombatDamage()
	{
		var ruric = Field(CardRegistry.Get("Ruric Thar, Magecrusher"), me);
		var gasp = Hand("Last Gasp", them);
		Assert.That(TheirTargets(gasp), Does.Not.Contain(ruric), "hexproof");
		s = s.Act(new PassPriorityAction { PlayerId = them });

		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		s = s.Choose([them]);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);

		Assert.That(TheirTargets(gasp), Does.Contain(ruric), "it has dealt combat damage");
	}

	[Test]
	public void RuricTharMagecrusher_DamageToSomethingAlreadyGone_KeepsHexproof()
	{
		var ruric = Field(CardRegistry.Get("Ruric Thar, Magecrusher"), me);
		var walker = Field(TestGames.Creature("Gone", "1/1"), them);
		s = s.Sacrifice(walker); // TEST SETUP: its combat target left before damage

		s = Damage.Deal(s, s.GetCard(ruric), walker, 7, combat: true);

		Assert.That(s.GetCard(ruric).HasDealtCombatDamage, Is.False, "no damage was dealt");
	}

	[Test]
	public void FateshaperAspirant_ReturnsALegendaryCard()
	{
		var legend = InGraveyard("Liliana the Repentant");
		InGraveyard(TestGames.Creature("Plain", "2/2"));
		var aspirant = Hand("Fateshaper Aspirant", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = aspirant });
		var offered = new List<int>();
		Resolve(choice =>
			choice switch
			{
				ChooseModesChoice => [0],
				ChooseTargetChoice t => offered = [.. t.Options.Select(o => o.Id)],
				_ => [],
			}
		);
		Assert.That(offered, Is.EqualTo(new[] { legend }), "legendary only");
		Assert.That(s.IsIn(legend, me, ZoneType.Hand));
	}

	[Test]
	public void FateshaperAspirant_IndestructibleSurvivesDestroyAndDamage_NotZeroToughness()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var aspirant = Hand("Fateshaper Aspirant", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = aspirant });
		Resolve(choice =>
			choice switch
			{
				ChooseModesChoice => [1],
				ChooseTargetChoice => [bear],
				_ => [],
			}
		);
		Assert.That(
			(
				s.GetCard(bear).PlusOneCounters,
				s.Has(s.GetCard(bear), "Vigilance"),
				s.IsIndestructible(s.GetCard(bear))
			),
			Is.EqualTo((1, true, true))
		);

		Cast(
			Hand(
				TestGames.Inline("Wrath", "{1}", "Instant", null, new Destroy(Ref.AllCreatures)),
				me
			),
			[]
		);
		Assert.That(Alive(bear), "destroy doesn't");
		s = Damage.Deal(s, s.GetCard(bear), bear, 10); // TEST SETUP: lethal damage
		s = StateBasedActions.Check(s);
		Assert.That(Alive(bear), "lethal damage doesn't");

		Cast(
			Hand(
				TestGames.Inline(
					"Wither",
					"{1}",
					"Instant",
					null,
					new ModifyUntilEndOfTurn(Ref.AllCreatures, -10, -10)
				),
				me
			),
			[]
		);
		Assert.That(Alive(bear), Is.False, "0 toughness still does");
	}

	[Test]
	public void ReturnToTheLightRealms_ReturnsEveryNonlandPermanentCard()
	{
		var creature = InGraveyard(TestGames.Creature("Creature", "2/2"));
		var land = InGraveyard(TestGames.Card("Forest"));
		var instant = InGraveyard(TestGames.Inline("Trick", "{R}", "Instant"));
		Cast(Hand("Return to the Light Realms", me), []);
		Assert.That(
			(
				Alive(creature),
				s.IsIn(land, me, ZoneType.Graveyard),
				s.IsIn(instant, me, ZoneType.Graveyard)
			),
			Is.EqualTo((true, true, true))
		);
	}

	[Test]
	public void RestoreWithEmpathy_APermanentCardToHandAndFourLife()
	{
		var land = InGraveyard(TestGames.Card("Forest"));
		InGraveyard(TestGames.Inline("Trick", "{R}", "Instant"));
		var restore = Hand("Restore with Empathy", me);
		Assert.That(
			Casts(restore).Select(c => c.Targets[0]),
			Is.EqualTo(new[] { land }),
			"permanent cards only"
		);
		var life = Life(me);

		Cast(restore, [], land);

		Assert.That((s.IsIn(land, me, ZoneType.Hand), Life(me)), Is.EqualTo((true, life + 4)));
	}

	[Test]
	public void RewriteRegrets_ReturnsACreatureOfManaValueSixOrLess_AndEmpowersJaceTwo()
	{
		var six = InGraveyard(TestGames.CreatureCosting("{5}{B}", "Six", "6/6"));
		InGraveyard(TestGames.CreatureCosting("{6}{B}", "Seven", "7/7"));
		var regrets = Hand("Rewrite Regrets", me);
		Assert.That(Casts(regrets).Select(c => c.Targets[0]), Is.EqualTo(new[] { six }));

		Cast(regrets, [], six);

		Assert.That(Alive(six));
		Assert.That(Jace(me)!.Loyalty, Is.EqualTo(2));
	}
}
