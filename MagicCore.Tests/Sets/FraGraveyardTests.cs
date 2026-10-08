using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;
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
		(s, id) = TestGames.Put(s, me, FraCards.Get(fra), ZoneType.Graveyard);
		return id;
	}

	int InGraveyard(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Graveyard);
		return id;
	}

	IEnumerable<ActivateAbilityAction> Activations(int card) =>
		s.LegalActions().OfType<ActivateAbilityAction>().Where(a => a.CardId == card);

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
		var witness = Field(FraCards.Get("Undulating Witness"), me);
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
		var crier = Field(FraCards.Get("Campus Crier"), me);
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
		var heckler = Field(FraCards.Get("Hallway Heckler // Vicious Verse"), me);
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
}
