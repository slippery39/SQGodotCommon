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
/// MC-5.3 batch 4a: counters of any kind (the named-counter map, quests), Sagas, kicker, can't block and
/// "activate only once each turn" through real casting (tier 3), and each tier-2 card's new option
/// against its PRINTED text. Tier-1 cards are checked by their card-sheet entry (TrkCardSheet.md).
/// </summary>
public class TrkBatch4Tests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition Fodder => TestCards.Fodder;

	int InGraveyard(int player, CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Graveyard);
		return id;
	}

	int OnTop(CardDefinition card)
	{
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(me, ZoneType.Library)); // TEST SETUP: index 0 is the top
		return id;
	}

	void CastAndResolve(int card, Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		s = s.Act(Casts(card).First(c => !c.Kicked));
		Resolve(answer);
	}

	/// <summary>On to my next precombat main phase — a Saga's next lore counter; its chapter waits on the stack.</summary>
	void ToMyNextMain()
	{
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == turn + 2 && x.GetGame().Step == Step.PreCombatMain
		);
	}

	void ToNextTurn()
	{
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn + 1);
	}

	void Declare(params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([attackers.Contains(declare.CreatureId) ? them : CombatOption.None]);
	}

	/// <summary>Answers every target choice with these (none: no target), everything else as Resolve would.</summary>
	static Func<ChoiceAction, IEnumerable<int>> Targeting(params int[] ids) =>
		c =>
			c switch
			{
				ChooseTargetChoice => ids,
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			};

	int Lore(int saga) => s.GetCard(saga).CountersOf(CounterKinds.Lore);

	// ===== Tier 3: counters of any kind, quests =====

	[Test]
	public void AnthropologicalMission_AQuestCounterPerSpell_NoDiscardAtThree()
	{
		var mission = Field(Trk("Anthropological Mission"), me);
		for (var i = 1; i <= 3; i++)
		{
			var fodder = Hand(Fodder, me);
			var hand = HandCount(me);
			CastAndResolve(fodder);
			Assert.That(s.GetCard(mission).CountersOf(CounterKinds.Quest), Is.EqualTo(i));
			Assert.That(
				HandCount(me),
				Is.EqualTo(i < 3 ? hand - 1 : hand),
				"draw a card, then discard unless three or more quest counters"
			);
		}
	}

	[Test]
	public void DominionSupervisor_TrampleForACounterOfAnyKind()
	{
		Field(Trk("Dominion Supervisor"), me);
		var plain = Field(Bear, me);
		var stunned = Field(Bear, me);
		s = s.UpdateObject(stunned, s.GetCard(stunned) with { StunCounters = 1 }); // TEST SETUP: not a +1/+1 counter
		Assert.That(s.Has(s.GetCard(stunned), "Trample"), Nu.True, "a stun counter is a counter");
		Assert.That(s.Has(s.GetCard(plain), "Trample"), Nu.False);
	}

	[Test]
	public void BeckettMariner_APromotionCounterPerTap_AtThreeRemovedAndTwoDamage()
	{
		var beckett = Field(Trk("Beckett Mariner, Impetuous Ensign"), me);
		var tap = TestCards.Targeted("Tap", TargetSpec.Creature, new Tap(Ref.Target()));
		for (var i = 1; i <= 3; i++)
		{
			var life = Life(them);
			var spell = Hand(tap, me);
			Cast(spell, [], beckett);
			Assert.That(Life(them), Is.EqualTo(i < 3 ? life : life - 2));
			Assert.That(
				s.GetCard(beckett).CountersOf(CounterKinds.Promotion),
				Is.EqualTo(i < 3 ? i : 0),
				"three or more: remove those counters"
			);
			if (i == 3)
				Assert.That(
					s.GetCard(beckett).Counters,
					Is.Empty,
					"none left is no entry, not a 0"
				);
			s = s.UpdateObject(beckett, s.GetCard(beckett) with { IsTapped = false }); // TEST SETUP
		}
	}

	[Test]
	public void Phlox_DrawsForCombatDamageFromACreatureWithACounter()
	{
		Field(Trk("Phlox, Unorthodox Doctor"), me);
		var marked = Field(Bear, me);
		s = s.UpdateObject(marked, s.GetCard(marked) with { ChargeCounters = 1 }); // TEST SETUP: any kind
		var plain = Field(Bear, me);
		var hand = HandCount(me);
		Declare(marked, plain);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);
		Assert.That(
			HandCount(me),
			Is.EqualTo(hand + 1),
			"one draw: only the creature with a counter"
		);
	}

	// ===== Tier 3: Sagas =====

	[Test]
	public void Saga_LoreAsItEntersAndEachMainPhase_SacrificedOnlyAfterChapterIIIResolves()
	{
		var saga = Hand(Trk("Those Old Scientists"), me);
		var theirs = InGraveyard(them, Fodder);
		CastAndResolve(saga);
		Assert.That(Lore(saga), Is.EqualTo(1), "as this Saga enters, add a lore counter");
		ToMyNextMain();
		Assert.That(Lore(saga), Is.EqualTo(2), "after your draw step");
		Resolve();
		ToMyNextMain();
		Assert.That(Lore(saga), Is.EqualTo(3));
		// Chapter III: up to two target cards in graveyards — theirs, and none.
		while (s.GetPendingChoice() is ChooseTargetChoice target)
			s = s.Choose(target.Index == 0 ? [theirs] : []);
		Assert.That(s.StackIsEmpty(), Nu.False, "chapter III is on the stack");
		Assert.That(Alive(saga), Nu.True, "not sacrificed while its chapter III is on the stack");
		Resolve();
		Assert.That(s.IsIn(theirs, them, ZoneType.Library), Nu.True, "its owner shuffles it in");
		Assert.That(s.IsIn(saga, me, ZoneType.Graveyard), Nu.True, "sacrifice after III");
	}

	[Test]
	public void MeasureOfAMan_ChapterIIIReturnsWhatChapterIExiled()
	{
		var bear = Field(Bear, them);
		var saga = Hand(Trk("The Measure of a Man"), me);
		CastAndResolve(saga, Targeting(bear));
		Assert.That(s.IsIn(bear, them, ZoneType.Exile), Nu.True);
		var life = Life(me);
		ToMyNextMain();
		Resolve();
		Assert.That(s.IsIn(bear, them, ZoneType.Exile), Nu.True, "chapter II returns nothing");
		Assert.That(Life(me), Is.EqualTo(life), "each chapter only as its lore count is reached");
		ToMyNextMain();
		Resolve();
		Assert.That(s.IsIn(bear, them, ZoneType.Battlefield), Nu.True, "under its owner's control");
		Assert.That(Life(me), Is.EqualTo(life + 2));
	}

	[Test]
	public void MeasureOfAMan_ASagaThatLeftAndCameBack_NeverFindsWhatTheOldOneExiled()
	{
		var bear = Field(Bear, them);
		var saga = Hand(Trk("The Measure of a Man"), me);
		s = s.Act(Casts(saga).Single());
		while (s.GetPendingChoice() is not ChooseTargetChoice)
			s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
		s = s.Choose([bear]);
		// Chapter I waits on the stack; the Saga leaves and comes back — a new object (CR 400.7).
		s = s.MoveCard(saga, s.ZoneId(me, ZoneType.Hand));
		s = s.EnterBattlefield(saga, me);
		Resolve(Targeting());
		Assert.That(
			s.IsIn(bear, them, ZoneType.Exile),
			Nu.True,
			"the old chapter I still exiles it"
		);
		ToMyNextMain();
		Resolve();
		ToMyNextMain();
		Resolve();
		Assert.That(
			s.IsIn(bear, them, ZoneType.Exile),
			Nu.True,
			"the new Saga's III didn't exile it"
		);
	}

	[Test]
	public void Saga_ThatLostAllAbilities_IsNotSacrificed()
	{
		var saga = Field(Trk("Those Old Scientists"), me);
		s = s.PutCounters(saga, CounterKinds.Lore, 3);
		s = s.UpdateObject(
			saga,
			(Card)s.GetCard(saga).WithComponent(new UntilEndOfTurn { LosesAllAbilities = true })
		);
		s = StateBasedActions.Check(s);
		Assert.That(Alive(saga), Nu.True, "no chapter abilities: no final chapter (CR 714.2d)");
	}

	[Test]
	public void GreedyAi_ChoosesAmongTheColoursOffered()
	{
		Field(Trk("Dal R'El, Augment Orphan"), me);
		for (var i = 0; i < 30; i++)
			Hand(TestGames.Inline("White", "{W}{W}{W}{W}", "Instant"), me); // white is what it wants most
		var forest = Hand(TestGames.Card("Forest"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
		while (s.GetPendingChoice() is not ChooseColorChoice)
			s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
		Assert.DoesNotThrow(
			() => s = new global::MagicCore.Ai.GreedyAi(me).Play(s),
			"red or green, never white"
		);
	}

	[Test]
	public void NakedTime_ChapterII_CreaturesEnteringThisTurnGetACounterAndHaste()
	{
		var saga = Hand(Trk("The Naked Time"), me);
		CastAndResolve(saga);
		ToMyNextMain();
		Resolve();
		var bear = Hand(Bear, me);
		CastAndResolve(bear);
		Assert.That(s.GetCard(bear).PlusOneCounters, Is.EqualTo(1));
		Assert.That(s.Has(s.GetCard(bear), "Haste"), Nu.True);
		Assert.That(
			s.CardsIn(me, ZoneType.Command).Count(c => c.Name.EndsWith("(this turn)")),
			Is.EqualTo(1)
		);
		ToNextTurn();
		Assert.That(
			s.CardsIn(me, ZoneType.Command).Any(c => c.Name.EndsWith("(this turn)")),
			Nu.False,
			"gone with the turn"
		);
	}

	// ===== Tier 3: kicker =====

	[Test]
	public void HighlyIllogical_UnkickedOnlyAtManaValueTwoOrLess_KickedAtAnySpellForMore()
	{
		var big = TheirSpellOnTheStack(TestGames.Inline("Big", "{3}", "Instant"));
		var counter = Hand(Trk("Highly Illogical"), me);
		Assert.That(Casts(counter).All(c => c.Kicked), Nu.True, "mana value 3: only kicked");
		var untapped = s.CardsIn(me, ZoneType.Battlefield).Count(c => !c.IsTapped);
		s = s.Act(Casts(counter).First(c => c.Targets.SequenceEqual([big])));
		Assert.That(
			untapped - s.CardsIn(me, ZoneType.Battlefield).Count(c => !c.IsTapped),
			Is.EqualTo(4),
			"{1}{U} plus kicker {1}{U}"
		);
		Resolve();
		Assert.That(s.IsIn(big, them, ZoneType.Graveyard), Nu.True, "countered");
	}

	[Test]
	public void HighlyIllogical_ASmallSpell_KickedAndUnkickedAreTwoMoves()
	{
		var small = TheirSpellOnTheStack(TestGames.Inline("Small", "{1}", "Instant"));
		var counter = Hand(Trk("Highly Illogical"), me);
		var casts = Casts(counter).Where(c => c.Targets.SequenceEqual([small])).ToList();
		Assert.That(casts.Select(c => c.Kicked), Is.EquivalentTo(new[] { false, true }));
		Assert.That(s.DistinctMoves(casts), Has.Count.EqualTo(2));
	}

	[Test]
	public void EjectTheWarpCore_KickedBySacrifice_MinusSix()
	{
		var big = Field(TestGames.Creature("Big", "5/5"), them);
		var fodder = Field(Bear, me);
		var eject = Hand(Trk("Eject the Warp Core"), me);
		Assert.That(
			Casts(eject)
				.Where(c => c.Kicked && c.Targets.SequenceEqual([big]))
				.Select(c => c.SacrificeId),
			Is.EquivalentTo(new[] { fodder }),
			"kicked: one cast per artifact or creature to sacrifice"
		);
		s = s.Act(Casts(eject).Single(c => c.Kicked && c.Targets.SequenceEqual([big])));
		Assert.That(Alive(fodder), Nu.False, "sacrificed as it is cast");
		Resolve();
		Assert.That(Alive(big), Nu.False, "-6/-6");
	}

	[Test]
	public void EjectTheWarpCore_Unkicked_MinusTwo()
	{
		var big = Field(TestGames.Creature("Big", "5/5"), them);
		var eject = Hand(Trk("Eject the Warp Core"), me);
		s = s.Act(Casts(eject).Single(c => !c.Kicked && c.Targets.SequenceEqual([big])));
		Resolve();
		Assert.That(s.Power(s.GetCard(big)), Is.EqualTo(3));
	}

	// ===== Tier 3: can't block, once each turn =====

	[Test]
	public void CantBlock_TheWormTokenAndAnUntilEndOfTurnGrant()
	{
		Field(Trk("Cleveland Booker, Empath"), me);
		var theirs = Field(Bear, them);
		var attacker = s.GetCard(theirs);
		var forest = Hand(TestGames.Card("Forest"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
		Resolve();
		var worm = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Worm");
		Assert.That(Combat.CanBlock(s, worm, attacker), Nu.False, "This token can't block.");
		var bear = Field(Bear, me);
		Assert.That(Combat.CanBlock(s, s.GetCard(bear), attacker), Nu.True);
		var daze = TestCards.Targeted(
			"Daze",
			TargetSpec.Creature,
			new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, Combat.CantBlock)
		);
		var spell = Hand(daze, me);
		Cast(spell, [], bear);
		Assert.That(
			Combat.CanBlock(s, s.GetCard(bear), attacker),
			Nu.False,
			"can't block this turn"
		);
	}

	[Test]
	public void Dot7RepairSquad_OnlyOnceEachTurn()
	{
		var squad = Field(Trk("DOT-7 Repair Squad"), me);
		var bear = Field(Bear, me);
		s = s.UpdateObject(bear, s.GetCard(bear) with { IsTapped = true }); // TEST SETUP
		s = s.Act(Activations(squad).First(a => a.Targets.SequenceEqual([bear])));
		Resolve();
		Assert.That(s.GetCard(bear).IsTapped, Nu.False);
		Assert.That(Activations(squad), Is.Empty, "activate only once each turn");
	}

	// ===== Tier 2: the new options =====

	[Test]
	public void RecklessImpulse_ExilesTheTopTwo_BothPlayable()
	{
		var first = OnTop(Fodder);
		var second = OnTop(Fodder);
		var impulse = Hand(Trk("Reckless Impulse"), me);
		CastAndResolve(impulse);
		Assert.That(Casts(first), Is.Not.Empty);
		Assert.That(Casts(second), Is.Not.Empty);
	}

	[Test]
	public void LRell_TheWarriorIsSacrificedAtTheEndStep()
	{
		Field(Trk("L'Rell, Fierce Visionary"), me);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.BeginCombat);
		Resolve();
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Klingon Warrior"),
			Is.EqualTo(1)
		);
		ToNextTurn();
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Any(c => c.Name == "Klingon Warrior"),
			Nu.False
		);
	}

	[Test]
	public void GrayAndAdira_ACopyThatIsntLegendary_SacrificedAtTheEndStep()
	{
		Field(Trk("Gray and Adira, Forever Joined"), me);
		var legend = Field(TestCards.Typed(Bear, "Legendary Creature — Bear"), me);
		var fodder = Hand(Fodder, me);
		CastAndResolve(fodder);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.BeginCombat);
		Resolve(Targeting(legend));
		var bears = s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Name == "Bear").ToList();
		Assert.That(
			bears,
			Has.Count.EqualTo(2),
			"the legend rule doesn't apply: it isn't legendary"
		);
		Assert.That(bears.Single(c => c.IsToken).Is(Supertype.Legendary), Nu.False);
		ToNextTurn();
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Bear"),
			Is.EqualTo(1)
		);
	}

	[Test]
	public void DalREl_LandfallOffersRedOrGreen()
	{
		Field(Trk("Dal R'El, Augment Orphan"), me);
		var forest = Hand(TestGames.Card("Forest"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
		var offered = new List<int>();
		Resolve(c =>
		{
			if (c is ChooseColorChoice)
				offered.AddRange(c.Options.Select(o => o.Id));
			return c.Options.Take(c.MinChoices).Select(o => o.Id);
		});
		Assert.That(
			offered,
			Is.EquivalentTo(
				new[]
				{
					ChooseColorChoice.IdOf(ManaColor.Red),
					ChooseColorChoice.IdOf(ManaColor.Green),
				}
			)
		);
	}

	[Test]
	public void SeventyThousandLightYears_ItsControllerSearches()
	{
		var bear = Field(Bear, them);
		var spell = Hand(Trk("70,000 Light-Years from Home"), me);
		var searcher = 0;
		CastAndResolve(
			spell,
			c =>
			{
				if (c is ChooseFromLibraryChoice search)
					searcher = search.PlayerId;
				return c.Options.Take(c.MinChoices).Select(o => o.Id);
			}
		);
		Assert.That(searcher, Is.EqualTo(them));
		Assert.That(s.IsIn(bear, them, ZoneType.Exile), Nu.True);
	}

	[Test]
	public void TemporalConqueror_ALandFromTheGraveyard_ElseATreasure()
	{
		for (var i = 0; i < 3; i++)
			OnTop(Fodder);
		var forest = InGraveyard(me, TestGames.Card("Forest"));
		var first = Hand(Trk("Temporal Conqueror"), me);
		CastAndResolve(first);
		Assert.That(s.IsIn(forest, me, ZoneType.Hand), Nu.True, "return a land card");
		Assert.That(s.CardsIn(me, ZoneType.Battlefield).Any(c => c.Name == "Treasure"), Nu.False);
		for (var i = 0; i < 3; i++)
			OnTop(Fodder);
		var second = Hand(Trk("Temporal Conqueror"), me);
		CastAndResolve(second);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Treasure"),
			Is.EqualTo(1),
			"if you can't, create a Treasure"
		);
	}

	[Test]
	public void AndromedanAutomatons_FindsEveryCopy()
	{
		var copies = new[]
		{
			OnTop(Trk("Andromedan Automatons")),
			OnTop(Trk("Andromedan Automatons")),
		};
		var automatons = Hand(Trk("Andromedan Automatons"), me);
		CastAndResolve(
			automatons,
			c =>
				c is ChooseFromLibraryChoice
					? c.Options.Select(o => o.Id)
					: c.Options.Take(c.MinChoices).Select(o => o.Id)
		);
		Assert.That(copies.All(id => s.IsIn(id, me, ZoneType.Hand)), Nu.True, "any number");
	}

	[Test]
	public void CommonGoal_BothDealTheirPower()
	{
		var two = Field(Bear, me);
		var three = Field(TestGames.Creature("Three", "3/3"), me);
		var big = Field(TestGames.Creature("Big", "5/5"), them);
		var goal = Hand(Trk("Common Goal"), me);
		Cast(goal, [], two, three, big);
		Assert.That(Alive(big), Nu.False, "2 + 3 damage");
	}
}
