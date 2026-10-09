using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Rares and mythics (8d), one-offs — each ability fires.</summary>
public class FraRareOneOffTests : FraBoardTest
{
	static CardDefinition Typed(CardDefinition card, string typeLine) =>
		card with
		{
			Data = card.Data with { TypeLine = typeLine },
		};

	static CardDefinition Targeted(string name, params GameAction[] effects) =>
		new(
			TestGames.Inline(name, "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([.. effects])
				{
					Targets = [new TargetSpec(TargetKind.Creature)],
				},
			]
		);

	static CardDefinition Kill => Targeted("Kill", new Destroy(Ref.Target()));

	static CardDefinition Shock(int n) => Targeted("Shock", new DealDamage(Ref.Target(), n));

	static CardDefinition Fodder => TestGames.Inline("Fodder", "{1}", "Sorcery");

	static CardDefinition WithTrigger(
		CardDefinition card,
		TriggerEvent on,
		params GameAction[] effects
	) =>
		card with
		{
			Components = [.. card.Components, new TriggeredAbilityComponent(on, [.. effects])],
		};

	void EmptyMyHand()
	{
		foreach (var c in s.CardsIn(me, ZoneType.Hand).ToList())
			s = s.MoveObject(c.Id, s.ZoneId(me, ZoneType.Exile)); // TEST SETUP
	}

	void TapAll(int player, string subtype)
	{
		foreach (
			var c in s.CardsIn(player, ZoneType.Battlefield)
				.Where(c => c.Subtypes.Contains(subtype))
				.ToList()
		)
			s = s.UpdateObject(c.Id, c with { IsTapped = true }); // TEST SETUP
	}

	int Put(CardDefinition card, int player, ZoneType zone)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, zone);
		return id;
	}

	/// <summary>They cast it at this target in my main phase; I hold priority with it on the stack.</summary>
	void TheyCastAt(int spell, int target)
	{
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = spell,
				Targets = [target],
			}
		);
		s = s.Act(new PassPriorityAction { PlayerId = them });
	}

	// ===== Gideon the Oathless =====

	[Test]
	public void GideonTheOathless_WardDiscard_DecliningCountersTheSpell()
	{
		var gideon = Field(FraCards.Get("Gideon the Oathless"), me);
		var kill = Hand(Kill, them);
		TheyCastAt(kill, gideon);
		Resolve(choice =>
			choice switch
			{
				PayToKeepChoice => [],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(
			(Alive(gideon), s.IsIn(kill, them, ZoneType.Graveyard)),
			Is.EqualTo((true, true))
		);
	}

	[Test]
	public void GideonTheOathless_WardDiscard_DiscardingKeepsTheSpell()
	{
		var gideon = Field(FraCards.Get("Gideon the Oathless"), me);
		var kill = Hand(Kill, them);
		TheyCastAt(kill, gideon);
		var hand = HandCount(them);
		Resolve(choice =>
			choice switch
			{
				PayToKeepChoice => [1],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That((Alive(gideon), HandCount(them)), Is.EqualTo((false, hand - 1)));
	}

	[Test]
	public void GideonTheOathless_TheirCreatureEnters_OneDamageToThem()
	{
		Field(FraCards.Get("Gideon the Oathless"), me);
		var life = Life(them);
		TheirSpellOnTheStack(TestGames.Creature("Flashy", "1/1", "Flash"));
		Resolve();
		Assert.That(Life(them), Is.EqualTo(life - 1));
		Cast(Hand(TestGames.Creature("Mine", "1/1"), me), []);
		Assert.That(Life(them), Is.EqualTo(life - 1), "not my own");
	}

	[Test]
	public void GideonTheOathless_TheyActivateALoyaltyAbility_OneDamageToThem()
	{
		Field(FraCards.Get("Gideon the Oathless"), me);
		var walker = Field(
			TestGames.Planeswalker("Walker", 3, new LoyaltyAbilityComponent(1, [])),
			them
		);
		s = s.UpdateObject(walker, s.GetCard(walker) with { Loyalty = 3 }); // TEST SETUP: entered with it
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().Step == Step.PreCombatMain
		);
		var life = Life(them);
		s = s.Act(s.LegalActions().OfType<ActivateLoyaltyAction>().Single(a => a.CardId == walker));
		Resolve();
		Assert.That(Life(them), Is.EqualTo(life - 1));
	}

	// ===== Flickering Hound =====

	[Test]
	public void FlickeringHound_CastingACreature_FlickersAnotherOfMine()
	{
		Field(FraCards.Get("Flickering Hound"), me);
		var mine = Field(TestGames.Creature("Mine", "1/1"), me);
		s = s.UpdateObject(mine, s.GetCard(mine) with { IsTapped = true, PlusOneCounters = 2 }); // TEST SETUP
		var incarnation = s.GetCard(mine).Incarnation;
		var spell1 = Hand(TestGames.Creature("Bear", "2/2"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spell1 });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [mine],
				_ => [],
			}
		);
		var card = s.GetCard(mine);
		Assert.That(
			(Alive(mine), card.IsTapped, card.PlusOneCounters, card.Incarnation > incarnation),
			Is.EqualTo((true, false, 0, true)),
			"a new object: untapped, no counters"
		);
	}

	// ===== Gardenize =====

	[Test]
	public void Gardenize_AChargeCounterPerDeath_AndThatMuchGreenInMyFirstMainPhase()
	{
		var gardenize = Field(FraCards.Get("Gardenize"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(Hand(Kill, me), [], bear);
		Assert.That(s.GetCard(gardenize).ChargeCounters, Is.EqualTo(1));
		Cast(Hand(Kill, me), [], Field(TestGames.Creature("Theirs", "2/2"), them));
		Assert.That(s.GetCard(gardenize).ChargeCounters, Is.EqualTo(1), "only mine");

		s = s.UpdateObject(gardenize, s.GetCard(gardenize) with { ChargeCounters = 3 }); // TEST SETUP
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().ActivePlayerId == me
				&& x.GetGame().TurnNumber > turn
				&& x.GetGame().Step == Step.PreCombatMain
				&& x.StackIsEmpty()
		);
		Assert.That(s.GetPlayer(me).ManaPool[ManaColor.Green], Is.EqualTo(3));
	}

	[Test]
	public void Gardenize_DestroyedInResponse_StillAddsItsGreen()
	{
		var gardenize = Field(FraCards.Get("Gardenize"), me);
		s = s.UpdateObject(gardenize, s.GetCard(gardenize) with { ChargeCounters = 3 }); // TEST SETUP
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().ActivePlayerId == me
				&& x.GetGame().TurnNumber > turn
				&& x.GetGame().Step == Step.PreCombatMain
				&& !x.StackIsEmpty()
		);
		var shatter = Hand(
			new CardDefinition(
				TestGames.Inline("Shatter", "{1}", "Instant").Data,
				[
					new SpellEffectsComponent([new Destroy(Ref.Target())])
					{
						Targets = [new TargetSpec(TargetKind.Permanent)],
					},
				]
			),
			me
		);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = shatter,
				Targets = [gardenize],
			}
		);
		Resolve();
		Assert.That(
			(Alive(gardenize), s.GetPlayer(me).ManaPool[ManaColor.Green]),
			Is.EqualTo((false, 3)),
			"as it last existed"
		);
	}

	// ===== Cruel Calculations =====

	[Test]
	public void CruelCalculations_DrawsOnePerCardMilledFromThatPlayerThisTurn()
	{
		Cast(
			Hand(
				TestGames.Inline("Mill", "{1}", "Instant", null, new Mill(Ref.EachOpponent, 3)),
				me
			),
			[]
		);
		var cruel = Hand("Cruel Calculations", me);
		var hand = HandCount(me);
		Cast(cruel, [], them);
		Assert.That(HandCount(me), Is.EqualTo(hand - 1 + 3));
		var mine = Hand("Cruel Calculations", me);
		hand = HandCount(me);
		Cast(mine, [], me);
		Assert.That(HandCount(me), Is.EqualTo(hand - 1), "nothing milled from mine");
	}

	// ===== Curse-Marred Demon =====

	[Test]
	public void CurseMarredDemon_SearchesForACard_ThenDiscardsAtRandom()
	{
		EmptyMyHand();
		var prize = Put(TestGames.Inline("Prize", "{1}", "Sorcery"), me, ZoneType.Library);
		var spell2 = Hand("Curse-Marred Demon", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spell2 });
		Resolve(choice =>
			choice switch
			{
				ChooseFromLibraryChoice => [prize],
				_ => [],
			}
		);
		Assert.That(
			(s.IsIn(prize, me, ZoneType.Graveyard), HandCount(me)),
			Is.EqualTo((true, 0)),
			"found, then the only card in hand discarded"
		);
	}

	// ===== Hexhaven Invigorator =====

	[Test]
	public void HexhavenInvigorator_DealtDamage_UpToThatManyLandsTapped()
	{
		var hexhaven = Field(FraCards.Get("Hexhaven Invigorator"), me);
		for (var i = 0; i < 3; i++)
			Put(TestGames.Card("Forest"), me, ZoneType.Library);
		var lands = s.CardsIn(me, ZoneType.Battlefield).Select(c => c.Id).ToHashSet();
		var offered = 0;
		var shock = Hand(Shock(2), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = shock,
				Targets = [hexhaven],
			}
		);
		Resolve(choice =>
		{
			if (choice is not ChooseFromLibraryChoice)
				return [];
			offered = choice.MaxChoices;
			return choice
				.Options.Where(o => o.DisplayText == "Forest")
				.Take(choice.MaxChoices)
				.Select(o => o.Id);
		});
		var newLands = s.CardsIn(me, ZoneType.Battlefield)
			.Where(c => !lands.Contains(c.Id))
			.ToList();
		Assert.That(
			(offered, newLands.Count(c => c.Is(CardType.Land)), newLands.Count(c => c.IsTapped)),
			Is.EqualTo((2, 2, 2)),
			"two Forests — not one per name"
		);
	}

	[Test]
	public void HexhavenInvigorator_TheAiFetchesAllItMay()
	{
		var hexhaven = Field(FraCards.Get("Hexhaven Invigorator"), me);
		for (var i = 0; i < 3; i++)
			Put(TestGames.Card("Forest"), me, ZoneType.Library);
		var lands = s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		var shock = Hand(Shock(2), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = shock,
				Targets = [hexhaven],
			}
		);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseFromLibraryChoice);
		s = new MagicCore.Ai.GreedyAi(me).Decide(s).State; // the AI answers the search
		Resolve();
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land)),
			Is.EqualTo(lands + 2)
		);
	}

	// ===== Seasoned Cryomancer =====

	[Test]
	public void SeasonedCryomancer_TwoNonlandsDiscarded_TapAndStunTwo()
	{
		EmptyMyHand();
		var a = Hand(Fodder, me);
		var b = Hand(Fodder, me);
		var x = Field(TestGames.Creature("X", "2/2"), them);
		var y = Field(TestGames.Creature("Y", "2/2"), them);
		var spell3 = Hand("Seasoned Cryomancer", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spell3 });
		Resolve(choice =>
			choice switch
			{
				ChooseCardsFromHandChoice => [a, b],
				ChooseTargetChoice t => [t.Index == 0 ? x : y],
				_ => [],
			}
		);
		Assert.That(
			new[] { x, y }.Select(id => (s.GetCard(id).IsTapped, s.GetCard(id).StunCounters)),
			Is.EqualTo(new[] { (true, 1), (true, 1) })
		);
	}

	[Test]
	public void SeasonedCryomancer_ALandAndANonland_OnlyOneTarget()
	{
		EmptyMyHand();
		var fodder = Hand(Fodder, me);
		var land = Hand(TestGames.Card("Island"), me);
		Field(TestGames.Creature("X", "2/2"), them);
		var prompts = 0;
		var spell4 = Hand("Seasoned Cryomancer", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spell4 });
		Resolve(choice =>
		{
			if (choice is ChooseTargetChoice)
				prompts++;
			return choice switch
			{
				ChooseCardsFromHandChoice => [fodder, land],
				_ => [],
			};
		});
		Assert.That(prompts, Is.EqualTo(1));
	}

	[Test]
	public void SeasonedCryomancer_ExiledFromTheGraveyard_DrawsTwo()
	{
		var cryomancer = Put(FraCards.Get("Seasoned Cryomancer"), me, ZoneType.Graveyard);
		var hand = HandCount(me);
		s = s.Act(Activations(cryomancer).Single());
		Resolve();
		Assert.That(
			(HandCount(me), s.IsIn(cryomancer, me, ZoneType.Exile)),
			Is.EqualTo((hand + 2, true))
		);
	}

	// ===== Identity Echo =====

	[Test]
	public void IdentityEcho_ExilesMine_AndPutsTheFirstCreatureRevealedOntoTheBattlefield()
	{
		var echo = Field(FraCards.Get("Identity Echo"), me);
		var mine = Field(TestGames.Creature("Mine", "1/1"), me);
		var bear = Put(TestGames.Creature("Bear", "2/2"), me, ZoneType.Library);
		var island = Put(TestGames.Card("Island"), me, ZoneType.Library);
		var library = s.ZoneId(me, ZoneType.Library);
		s = s.MoveObjectToFront(bear, library).MoveObjectToFront(island, library); // TEST SETUP: Island, then Bear, on top
		s = s.Act(Activations(echo).Single(a => a.Targets[0] == mine));
		Resolve();
		Assert.That(
			(
				s.IsIn(mine, me, ZoneType.Exile),
				Alive(bear),
				s.CardsIn(me, ZoneType.Library).Last().Id
			),
			Is.EqualTo((true, true, island)),
			"the Island revealed on the way went to the bottom"
		);
	}

	// ===== Kindred Judgment =====

	[Test]
	public void KindredJudgment_DestroysEveryCreatureNotOfTheChosenType()
	{
		var myElf = Field(Typed(TestGames.Creature("Elf", "1/1"), "Creature — Elf Druid"), me);
		var theirElf = Field(Typed(TestGames.Creature("Elf", "1/1"), "Creature — Elf"), them);
		var goblin = Field(Typed(TestGames.Creature("Goblin", "1/1"), "Creature — Goblin"), them);
		var options = new List<string>();
		var spell5 = Hand("Kindred Judgment", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spell5 });
		Resolve(choice =>
		{
			if (choice is not ChooseKeywordChoice)
				return [];
			options = [.. choice.Options.Select(o => o.DisplayText)];
			return [choice.Options.Single(o => o.DisplayText == "Elf").Id];
		});
		Assert.That(
			(Alive(myElf), Alive(theirElf), Alive(goblin)),
			Is.EqualTo((true, true, false))
		);
		Assert.That(
			options,
			Does.Contain(DestroyCreaturesNotOfChosenType.NoneOfThem),
			"a type none has: destroy all"
		);
	}

	// ===== Karn, Argent Defender =====

	[Test]
	public void KarnArgentDefender_CreaturesEnteringDontTrigger_OtherPermanentsDo()
	{
		Field(FraCards.Get("Karn, Argent Defender"), me);
		var life = Life(me);
		Cast(
			Hand(
				WithTrigger(
					TestGames.Creature("Herald", "1/1"),
					TriggerEvent.SelfEnters,
					new GainLife(Ref.You, 3)
				),
				me
			),
			[]
		);
		Assert.That(Life(me), Is.EqualTo(life), "a creature entering");
		Cast(
			Hand(
				WithTrigger(
					TestGames.Inline("Shrine", "{1}", "Enchantment"),
					TriggerEvent.SelfEnters,
					new GainLife(Ref.You, 3)
				),
				me
			),
			[]
		);
		Assert.That(Life(me), Is.EqualTo(life + 3), "an enchantment entering still triggers");
	}

	// ===== Sphinx's Approach =====

	[Test]
	public void SphinxsApproach_WithFourMore_ExilesThemForASphinx()
	{
		for (var i = 0; i < 4; i++)
			Put(FraCards.Get("Sphinx's Approach"), me, ZoneType.Graveyard);
		var sphinx = Put(
			Typed(TestGames.Creature("Sphinx", "4/4"), "Creature — Sphinx"),
			me,
			ZoneType.Library
		);
		Put(TestGames.Creature("Bear", "2/2"), me, ZoneType.Library);
		var approach = Hand("Sphinx's Approach", me);
		var hand = HandCount(me);
		var offered = new List<string>();
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = approach });
		Resolve(choice =>
			choice switch
			{
				YesNoChoice => [1],
				ChooseFromLibraryChoice c => Offered(c, [sphinx]),
				_ => [],
			}
		);
		IEnumerable<int> Offered(ChoiceAction c, int[] pick)
		{
			offered = [.. c.Options.Select(o => o.DisplayText)];
			return pick;
		}
		Assert.That(offered, Is.EqualTo(new[] { "Sphinx" }), "a Sphinx creature card only");
		Assert.That(
			(
				Alive(sphinx),
				s.CardsIn(me, ZoneType.Exile).Count(c => c.Name == "Sphinx's Approach"),
				HandCount(me)
			),
			Is.EqualTo((true, 5, hand - 1 + 2))
		);
	}

	[Test]
	public void SphinxsApproach_WithThreeMore_JustDraws()
	{
		for (var i = 0; i < 3; i++)
			Put(FraCards.Get("Sphinx's Approach"), me, ZoneType.Graveyard);
		var approach = Hand("Sphinx's Approach", me);
		var asked = false;
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = approach });
		Resolve(choice =>
		{
			asked |= choice is YesNoChoice;
			return [];
		});
		Assert.That((asked, s.IsIn(approach, me, ZoneType.Graveyard)), Is.EqualTo((false, true)));
	}

	// ===== Null Summoner =====

	[Test]
	public void NullSummoner_ExilesTheirCard_CastableWithThresholdWithAnyMana()
	{
		var theirs = Hand(TestGames.CreatureCosting("{G}{G}", "Theirs", "2/2"), them);
		var spell6 = Hand("Null Summoner", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spell6 });
		Resolve(choice =>
			choice switch
			{
				ChooseCardsFromHandChoice => [theirs],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(s.IsIn(theirs, them, ZoneType.Exile));
		Assert.That(Casts(theirs), Is.Empty, "no threshold yet");
		for (var i = 0; i < 7; i++)
			Put(Fodder, me, ZoneType.Graveyard);
		TapAll(me, "Forest");
		Assert.That(Casts(theirs), Is.Not.Empty, "threshold, and any type of mana pays {G}{G}");
		s = s.Act(Casts(theirs).First());
		Resolve();
		Assert.That(
			s.IsIn(theirs, me, ZoneType.Battlefield),
			"I cast it, so it enters under my control"
		);
	}

	[Test]
	public void NullSummoner_NotCast_NoExile()
	{
		var theirs = Hand(TestGames.Inline("Theirs", "{G}{G}", "Sorcery"), them);
		var summoner = Put(FraCards.Get("Null Summoner"), me, ZoneType.Hand);
		s = s.EnterBattlefield(summoner, me); // TEST SETUP: put onto the battlefield, not cast
		Cast(Hand(TestGames.Inline("Pass", "{1}", "Instant"), me), []); // a priority to collect triggers
		Assert.That(
			s.CardsIn(them, ZoneType.Exile),
			Is.Empty,
			"no trigger, so nothing of theirs exiled"
		);
	}

	// ===== Omnipresence =====

	[Test]
	public void Omnipresence_FreeUpToTheNumberOfCreaturesIControl()
	{
		Field(FraCards.Get("Omnipresence"), me);
		Field(TestGames.Creature("A", "1/1"), me);
		Field(TestGames.Creature("B", "1/1"), me);
		var two = Hand(TestGames.Inline("Two", "{1}{G}", "Sorcery"), me);
		Assert.That(
			Casts(two).Select(c => c.WithoutPayingManaCost),
			Is.EqualTo(new[] { true }),
			"free, not also paid"
		);
		foreach (var land in new[] { "Plains", "Island", "Swamp", "Mountain", "Forest" })
			TapAll(me, land);
		var three = Hand(TestGames.Inline("Three", "{2}{G}", "Sorcery"), me);
		Assert.That(Casts(three), Is.Empty);
		s = s.Act(Casts(two).Single(c => c.WithoutPayingManaCost));
		Resolve();
		Assert.That(s.IsIn(two, me, ZoneType.Graveyard));
	}
}
