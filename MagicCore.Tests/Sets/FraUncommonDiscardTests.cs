using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Uncommons (7e-2c): discard and cast triggers — each ability fires.</summary>
public class FraUncommonDiscardTests : FraBoardTest
{
	static CardDefinition Filler(string name) => TestGames.Inline(name, "{1}", "Sorcery");

	/// <summary>An inline instant with one target word.</summary>
	static CardDefinition Targeted(string name, TargetSpec spec, GameAction effect) =>
		new(
			TestGames.Inline(name, "{1}", "Instant").Data,
			[new SpellEffectsComponent([effect]) { Targets = [spec] }]
		);

	static CardDefinition DiscardTwo =>
		TestGames.Inline("Purge", "{1}", "Sorcery", null, new Discard(Ref.You, 2));

	int Untapped() =>
		s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land) && !c.IsTapped);

	[Test]
	public void Tinybones_TheyDiscard_AndThatDiscardPingsThem()
	{
		Hand(Filler("Theirs"), them);
		var (life, hand) = (Life(them), HandCount(them));
		Cast(Hand("Tinybones, Pocket Nuisance", me), []);
		Assert.That((HandCount(them), Life(them)), Is.EqualTo((hand - 1, life - 1)));
	}

	[Test]
	public void Tinybones_TwoCardsInOneDiscardIsOneTrigger()
	{
		Field(CardRegistry.Get("Tinybones, Pocket Nuisance"), me);
		var purge = Hand(DiscardTwo, me);
		Hand(Filler("A"), me);
		Hand(Filler("B"), me);
		var life = Life(them);
		Cast(purge, []);
		Assert.That(Life(them), Is.EqualTo(life - 1), "one or more cards: one trigger");
	}

	[Test]
	public void Tinybones_ADiscardPaidAsACostTriggersToo()
	{
		Field(CardRegistry.Get("Tinybones, Pocket Nuisance"), me);
		var proft = Hand("Proft, Sinister Mastermind", me);
		var bear = Field(TestGames.Creature("Bear", "4/4"), them);
		var life = Life(them);
		s = s.Act(Activations(proft).Single(a => a.Targets[0] == bear));
		Resolve();
		Assert.That(Life(them), Is.EqualTo(life - 1));
	}

	[Test]
	public void Tinybones_ADiscardToHandSizeResolvesInThatCleanup_BeforeTheTurnEnds()
	{
		Field(CardRegistry.Get("Tinybones, Pocket Nuisance"), me);
		while (HandCount(me) <= TurnStructure.MaxHandSize)
			Hand(Filler("Extra"), me);
		var (life, turn) = (Life(them), s.GetGame().TurnNumber);
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn + 1);
		Assert.That(
			Life(them),
			Is.EqualTo(life - 1),
			"in the cleanup, then another cleanup (CR 514.3a)"
		);
		Assert.That(HandCount(me), Is.EqualTo(TurnStructure.MaxHandSize));
	}

	[Test]
	public void Tinybones_SurveilIntoTheGraveyardIsNoDiscard()
	{
		Field(CardRegistry.Get("Tinybones, Pocket Nuisance"), me);
		var peek = Hand(TestGames.Inline("Peek", "{U}", "Instant", null, new Surveil(1)), me);
		var life = Life(them);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = peek });
		Resolve(choice => choice.Options.Take(1).Select(o => o.Id)); // the top card to the graveyard
		Assert.That(Life(them), Is.EqualTo(life));
	}

	[Test]
	public void Titanbones_TwoCountersOnLifeGain_AndThreeLifeWhenDiscarded()
	{
		var titan = Field(CardRegistry.Get("Titanbones, Towering Heart"), me);
		var heal = Hand(
			TestGames.Inline("Heal", "{W}", "Instant", null, new GainLife(Ref.You, 1)),
			me
		);
		Cast(heal, []);
		Assert.That(s.GetCard(titan).PlusOneCounters, Is.EqualTo(2));

		var inHand = Hand("Titanbones, Towering Heart", me);
		var purge = Hand(
			TestGames.Inline("Purge", "{1}", "Sorcery", null, new Discard(Ref.You, 1)),
			me
		);
		var life = Life(me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = purge });
		Resolve(choice =>
			choice switch
			{
				ChooseCardsFromHandChoice => [inHand],
				_ => [],
			}
		);
		Assert.That(Life(me), Is.EqualTo(life + 3));
	}

	void AttackWith(params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([attackers.Contains(declare.CreatureId) ? them : CombatOption.None]);
	}

	[Test]
	public void JiangYanggu_AttackingAlone_LootsThenCountsTheDiscards()
	{
		Field(CardRegistry.Get("Jiang Yanggu, Alone"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Hand(Filler("Fodder"), me);
		var hand = HandCount(me);
		AttackWith(bear);
		Resolve();
		Assert.That(
			(HandCount(me), s.GetCard(bear).PlusOneCounters),
			Is.EqualTo((hand, 1)),
			"discard one, draw one, one counter per card discarded this turn"
		);
	}

	[Test]
	public void JiangYanggu_TwoAttackers_NoTrigger()
	{
		var jiang = Field(CardRegistry.Get("Jiang Yanggu, Alone"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		AttackWith(bear, jiang);
		Resolve();
		Assert.That(s.GetCard(bear).PlusOneCounters + s.GetCard(jiang).PlusOneCounters, Is.Zero);
	}

	[Test]
	public void PiaAetherAscetic_DiscardToFindAnEnchantment()
	{
		int aura;
		(s, aura) = TestGames.Put(
			s,
			me,
			TestGames.Inline("Aura", "{1}", "Enchantment"),
			ZoneType.Library
		);
		var fodder = Hand(Filler("Fodder"), me);
		var pia = Hand("Pia, Aether Ascetic", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = pia });
		Resolve(choice =>
			choice switch
			{
				_ when choice.Options.Any(o => o.Id == aura) => [aura],
				ChooseCardsFromHandChoice => [fodder],
				_ => [],
			}
		);
		Assert.That(
			(s.IsIn(fodder, me, ZoneType.Graveyard), s.IsIn(aura, me, ZoneType.Hand)),
			Is.EqualTo((true, true))
		);
	}

	[Test]
	public void DanithaSword_ASpellTargetingYourCreatureDraws_OncePerTurn()
	{
		Field(CardRegistry.Get("Danitha, Sword of Hope"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var pump = TargetSpec.Creature;
		var first = Hand(Targeted("Pump", pump, new ModifyUntilEndOfTurn(Ref.Target(), 1, 1)), me);
		var second = Hand(Targeted("Pump", pump, new ModifyUntilEndOfTurn(Ref.Target(), 1, 1)), me);
		var hand = HandCount(me);
		Cast(first, [], bear);
		Cast(second, [], bear);
		Assert.That(HandCount(me), Is.EqualTo(hand - 2 + 1));
	}

	[Test]
	public void DanithaSword_AnEquipmentSpellDraws()
	{
		Field(CardRegistry.Get("Danitha, Sword of Hope"), me);
		var axe = Hand("Hunter's Axe", me);
		var hand = HandCount(me);
		Cast(axe, []);
		Assert.That(HandCount(me), Is.EqualTo(hand - 1 + 1));
	}

	[Test]
	public void DanithaSpear_GrowsOnSpellsAtThemOrTheirCreatures_NotYours()
	{
		var danitha = Field(CardRegistry.Get("Danitha, Spear of Agony"), me);
		var mine = Field(TestGames.Creature("Mine", "2/2"), me);
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		var any = TargetSpec.AnyTarget();
		Cast(Hand(Targeted("Zap", any, new GainLife(Ref.You, 1)), me), [], mine);
		Assert.That(s.GetCard(danitha).PlusOneCounters, Is.Zero);
		Cast(Hand(Targeted("Zap", any, new GainLife(Ref.You, 1)), me), [], theirs);
		Cast(Hand(Targeted("Zap", any, new GainLife(Ref.You, 1)), me), [], them);
		Assert.That(s.GetCard(danitha).PlusOneCounters, Is.EqualTo(2));
	}

	[Test]
	public void PlanForAllOutcomes_TheOwnerChoosesTheTop()
	{
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		var plan = Hand("Plan for All Outcomes", me);
		var askedThem = false;
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = plan });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [theirs],
				YesNoChoice yes => (askedThem = yes.PlayerId == them) ? [1] : [],
				_ => [],
			}
		);
		Assert.That(askedThem);
		Assert.That(s.GetChildrenIds(s.ZoneId(them, ZoneType.Library)).First(), Is.EqualTo(theirs));
	}

	[Test]
	public void PlanForAllOutcomes_OnlyTheFirstNoncreatureSpellEmpowersJace()
	{
		Field(CardRegistry.Get("Plan for All Outcomes"), me);
		Cast(Hand(TestGames.Inline("One", "{1}", "Instant"), me), []);
		Assert.That(Jace(me)?.Loyalty, Is.EqualTo(1));
		Cast(Hand(TestGames.Inline("Two", "{1}", "Instant"), me), []);
		Assert.That(Jace(me)?.Loyalty, Is.EqualTo(1));
	}

	[Test]
	public void Proft_ThresholdToCast()
	{
		var proft = Hand("Proft, Sinister Mastermind", me);
		Assert.That(Casts(proft), Is.Empty, "six or fewer cards in your graveyard");
		for (var i = 0; i < 7; i++)
			(s, _) = TestGames.Put(s, me, Filler("Dead"), ZoneType.Graveyard);
		Assert.That(Casts(proft), Is.Not.Empty);
	}

	[Test]
	public void Proft_DiscardFromHandForMinusThreeMinusOne()
	{
		var proft = Hand("Proft, Sinister Mastermind", me);
		var bear = Field(TestGames.Creature("Bear", "4/4"), them);
		s = s.Act(Activations(proft).Single(a => a.Targets[0] == bear));
		Resolve();
		var card = s.GetCard(bear);
		Assert.That((s.Power(card), s.Toughness(card)), Is.EqualTo((4 - 3, 4 - 1)));
		Assert.That(s.IsIn(proft, me, ZoneType.Graveyard));
	}

	[Test]
	public void Countersculpt_BeholdingAJaceSavesTheOne_ThenCountersAndEmpowers()
	{
		var jace = Hand(
			TestGames.Planeswalker("Jace", 3) with
			{
				Data = TestGames.Planeswalker("Jace", 3).Data with
				{
					TypeLine = "Planeswalker — Jace",
				},
			},
			me
		);
		var trick = TheirSpellOnTheStack(
			TestGames.Inline("Trick", "{R}", "Instant", null, new GainLife(Ref.You, 1))
		);
		var sculpt = Hand("Countersculpt", me);
		Assert.That(
			Casts(sculpt).Select(c => c.BeholdId).Distinct(),
			Is.EquivalentTo(new[] { 0, jace })
		);

		var lands = Untapped();
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = sculpt,
				Targets = [trick],
				BeholdId = jace,
			}
		);
		Assert.That(Untapped(), Is.EqualTo(lands - 2), "{U}{U}, the {1} beheld away");
		Resolve();
		Assert.That(s.IsIn(trick, them, ZoneType.Graveyard));
		Assert.That((Jace(me)?.Loyalty, s.IsIn(jace, me, ZoneType.Hand)), Is.EqualTo((1, true)));
	}

	[Test]
	public void Countersculpt_WithoutAJace_PayTheOne()
	{
		var trick = TheirSpellOnTheStack(
			TestGames.Inline("Trick", "{R}", "Instant", null, new GainLife(Ref.You, 1))
		);
		var sculpt = Hand("Countersculpt", me);
		var lands = Untapped();
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = sculpt,
				Targets = [trick],
			}
		);
		Assert.That(Untapped(), Is.EqualTo(lands - 3));
	}
}
