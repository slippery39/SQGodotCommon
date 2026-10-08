using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets.Fra;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Commons (7e-1a): impulse draw, reflexive triggers, revealed hands, chosen colors — each ability fires.</summary>
public class FraCommonTests : FraBoardTest
{
	IEnumerable<ActivateAbilityAction> Activations(int card) =>
		s.LegalActions().OfType<ActivateAbilityAction>().Where(a => a.CardId == card);

	int OnTop(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(me, ZoneType.Library)); // TEST SETUP: index 0 is the top
		return id;
	}

	[Test]
	public void ShatterwingPegasus_PumpsYourTeam()
	{
		var pegasus = Field(FraCards.Get("Shatterwing Pegasus"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		s = s.Act(Activations(pegasus).Single());
		Resolve();
		Assert.That(
			(s.Power(s.GetCard(bear)), s.Toughness(s.GetCard(pegasus))),
			Is.EqualTo((3, 4))
		);
	}

	[Test]
	public void Unsummon_ReturnsACreatureToItsOwnersHand()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		Cast(Hand("Unsummon", me), [], bear);
		Assert.That(s.IsIn(bear, them, ZoneType.Hand));
	}

	[Test]
	public void InspiredTethermage_GrowsWhenYouPutLoyaltyCounters()
	{
		var tethermage = Field(FraCards.Get("Inspired Tethermage"), me);
		Cast(Hand(TestGames.Planeswalker("Walker", 3), me), []); // enters with loyalty: "put" (CR 122.6)
		Assert.That(s.GetCard(tethermage).PlusOneCounters, Is.EqualTo(1));

		s = s.Act(Activations(tethermage).Single()); // {6}: Empower Jace 2
		Resolve();
		Assert.That(s.GetCard(tethermage).PlusOneCounters, Is.EqualTo(2));
	}

	[Test]
	public void SomethingWorthSaving_MillsFour_YouMayTakeAPermanentCard_GainOne()
	{
		var instant = OnTop(TestGames.Inline("Trick", "{R}", "Instant"));
		var bear = OnTop(TestGames.Creature("Bear", "2/2"));
		var life = Life(me);
		IEnumerable<int> offered = [];

		var saving = Hand("Something Worth Saving", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = saving });
		Resolve(choice =>
		{
			if (choice is ChooseAmongChoice)
			{
				offered = choice.Options.Select(o => o.Id).ToList();
				return [bear];
			}
			return [];
		});

		Assert.That(offered, Does.Contain(bear).And.Not.Contain(instant), "permanent cards only");
		Assert.That(s.IsIn(bear, me, ZoneType.Hand), "the choose-and-move steps both ran");
		Assert.That(
			(s.IsIn(instant, me, ZoneType.Graveyard), Life(me)),
			Is.EqualTo((true, life + 1))
		);
	}

	[Test]
	public void SureshotSower_DiscardedFromHand_DestroysAFlier()
	{
		var sower = Hand("Sureshot Sower", me);
		var flier = Field(TestGames.Creature("Flier", "2/2", "Flying"), them);
		var walker = Field(TestGames.Creature("Walker", "2/2"), them);
		Assert.That(Activations(sower).Select(a => a.Targets[0]), Is.EqualTo(new[] { flier }));

		s = s.Act(Activations(sower).Single());
		Resolve();

		Assert.That((Alive(flier), Alive(walker)), Is.EqualTo((false, true)));
		Assert.That(s.IsIn(sower, me, ZoneType.Graveyard));
	}

	[Test]
	public void AfterthoughtSentry_FliesForTwo_AndAttackingExilesAGraveyardCard()
	{
		var sentry = Field(FraCards.Get("Afterthought Sentry"), me);
		s = s.Act(Activations(sentry).Single());
		Resolve();
		Assert.That(s.Has(s.GetCard(sentry), "Flying"));

		int theirs;
		(s, theirs) = TestGames.Put(s, them, TestGames.Creature("Dead", "2/2"), ZoneType.Graveyard);
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		s = s.Choose([them]);
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [theirs],
				_ => [],
			}
		);
		Assert.That(s.IsIn(theirs, them, ZoneType.Exile), "a card from ANY graveyard");
	}

	[Test]
	public void MurmuringVolume_TapsForAnyColor_AndLootsForTwo()
	{
		var volume = Field(FraCards.Get("Murmuring Volume"), me);
		Assert.That(ManaAbilities.ColorsOf(s.GetCard(volume)).Count(), Is.EqualTo(5));

		var fodder = Hand(TestGames.Card("Forest"), me);
		var hand = HandCount(me);
		s = s.Act(Activations(volume).Single(a => a.DiscardId == fodder));
		Resolve();
		Assert.That(
			(s.IsIn(fodder, me, ZoneType.Graveyard), HandCount(me)),
			Is.EqualTo((true, hand))
		);
	}

	[Test]
	public void RoomOfRefuge_EntersTappedForTheChosenColor_AndSacrificesForTwoCounters()
	{
		var room = Hand("Room of Refuge", me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = room });
		Assert.That(s.GetPendingChoice(), Is.InstanceOf<ChooseColorChoice>());
		s = s.Choose([(int)ManaColor.Blue]);

		Assert.That(s.GetCard(room).IsTapped);
		Assert.That(ManaAbilities.ColorsOf(s.GetCard(room)), Is.EqualTo(new[] { ManaColor.Blue }));

		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		s = s.UpdateObject(room, s.GetCard(room) with { IsTapped = false }); // TEST SETUP
		s = s.Act(Activations(room).Single(a => a.Targets[0] == bear));
		Resolve();
		Assert.That(
			(s.GetCard(bear).PlusOneCounters, s.IsIn(room, me, ZoneType.Graveyard)),
			Is.EqualTo((2, true))
		);
	}

	[Test]
	public void TetherTechnician_DiscardThenAReflexiveTriggerTargets_TwoDamage()
	{
		var fodder = Hand(TestGames.Card("Forest"), me);
		var technician = Hand("Tether Technician", me);
		var life = Life(them);
		var order = new List<string>();

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = technician });
		Resolve(choice =>
		{
			order.Add(choice.GetType().Name);
			return choice switch
			{
				ChooseCardsFromHandChoice => [fodder],
				ChooseTargetChoice => [them],
				_ => [],
			};
		});

		Assert.That(
			order,
			Is.EqualTo(new[] { nameof(ChooseCardsFromHandChoice), nameof(ChooseTargetChoice) }),
			"the target is chosen AFTER the discard — when you do"
		);
		Assert.That(Life(them), Is.EqualTo(life - 2));
	}

	[Test]
	public void TetherTechnician_NoDiscard_NoDamage()
	{
		Hand(TestGames.Card("Forest"), me);
		var technician = Hand("Tether Technician", me);
		var life = Life(them);
		Cast(technician, []); // the default answer discards nothing
		Assert.That(Life(them), Is.EqualTo(life));
	}

	[Test]
	public void BlazingCrescendo_PumpsAndLetsYouPlayTheExiledCardUntilYourNextTurnEnds()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var top = OnTop(TestGames.Creature("Topdeck", "1/1"));

		Cast(Hand("Blazing Crescendo", me), [], bear);

		Assert.That((s.Power(s.GetCard(bear)), s.Toughness(s.GetCard(bear))), Is.EqualTo((5, 3)));
		Assert.That(s.IsIn(top, me, ZoneType.Exile));
		Assert.That(Casts(top), Is.Not.Empty, "playable now");

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.PreCombatMain && x.GetGame().TurnNumber == 3
		);
		Assert.That(Casts(top), Is.Not.Empty, "still on your next turn");

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.PreCombatMain && x.GetGame().TurnNumber == 5
		);
		Assert.That(Casts(top), Is.Empty, "not after it");
	}

	[Test]
	public void SolveForDisappointment_YouChooseANonlandPermanentFromTheirHand()
	{
		var creature = Hand(TestGames.Creature("Theirs", "2/2"), them);
		var trick = Hand(TestGames.Inline("Trick", "{R}", "Instant"), them);
		IEnumerable<int> offered = [];
		int decider = 0;

		var solve = Hand("Solve for Disappointment", me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = solve,
				Targets = [them],
			}
		);
		Resolve(choice =>
		{
			if (choice is ChooseCardsFromHandChoice { DeciderId: var decides } && decides == me)
			{
				offered = choice.Options.Select(o => o.Id).ToList();
				decider = s.DecidingPlayerId();
				return [creature];
			}
			return [];
		});

		Assert.That(decider, Is.EqualTo(me), "you choose");
		Assert.That(
			offered,
			Does.Contain(creature).And.Not.Contain(trick),
			"nonland permanent cards"
		);
		Assert.That(s.IsIn(creature, them, ZoneType.Graveyard));
		Assert.That(Jace(me)!.Loyalty, Is.EqualTo(1));
	}

	[Test]
	public void AnExiledLandAndAHandLandOfTheSameName_AreTwoMoves()
	{
		Hand(TestGames.Card("Forest"), me);
		var exiled = OnTop(TestGames.Card("Forest"));
		Cast(Hand("Blazing Crescendo", me), [], Field(TestGames.Creature("Bear", "2/2"), me));
		Assert.That(s.IsIn(exiled, me, ZoneType.Exile));

		var lands = s.DistinctMoves(s.LegalActions()).OfType<PlayLandAction>().ToList();
		Assert.That(lands, Has.Count.EqualTo(2), "the exiled one expires unused if they merge");
	}

	[Test]
	public void TheAi_ChoosesTheColorItsCardsWant_ForRoomOfRefuge()
	{
		foreach (var card in s.CardsIn(me, ZoneType.Library).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(me, ZoneType.Exile)); // TEST SETUP: the deck counts too — leave only the hand
		foreach (var _ in Enumerable.Range(0, 3))
			Hand(TestGames.CreatureCosting("{R}{R}", $"Red{s.NextId}", "2/2"), me);
		var room = Hand("Room of Refuge", me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = room });

		(s, _) = new global::MagicCore.Ai.GreedyAi(me).Decide(s);

		Assert.That(
			ManaAbilities.ColorsOf(s.GetCard(room)).ToList(),
			Is.EqualTo(new[] { ManaColor.Red })
		);
	}
}
