using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Sets;

/// <summary>Sacrifice (cost and effect), "dies" triggers and exile-until-leaves: each card FIRES.</summary>
public class FraSacrificeTests : FraBoardTest
{
	void Activate(int card, int sacrifice = 0, params int[] targets)
	{
		s = s.Act(
			new ActivateAbilityAction
			{
				PlayerId = s.GetCard(card).ControllerId,
				CardId = card,
				SacrificeId = sacrifice,
				Targets = [.. targets],
			}
		);
		Resolve();
	}

	[Test]
	public void BuddingInsurgent_SacrificesItself_DrawingOnlyForALegendaryEnchantment()
	{
		var insurgent = Field(FraCards.Get("Budding Insurgent"), me);
		var way = Field(FraCards.Get("Way of the Pyromancer"), them); // legendary
		var hand = HandCount(me);

		Activate(insurgent, 0, way);

		Assert.That(
			(Alive(way), Alive(insurgent), HandCount(me)),
			Is.EqualTo((false, false, hand + 1))
		);

		var second = Field(FraCards.Get("Budding Insurgent"), me);
		var trap = Field(FraCards.Get("Memory Trap"), them); // not legendary
		Activate(second, 0, trap);
		Assert.That((Alive(trap), HandCount(me)), Is.EqualTo((false, hand + 1)), "no second draw");
	}

	[Test]
	public void BuddingInsurgent_OnlyAsASorcery()
	{
		var insurgent = Field(FraCards.Get("Budding Insurgent"), me);
		Field(FraCards.Get("Memory Trap"), them);
		s = s.Act(new PassPriorityAction { PlayerId = me }); // their priority, my main phase
		Assert.That(
			s.LegalActions().OfType<ActivateAbilityAction>().Where(a => a.CardId == insurgent),
			Is.Empty
		);
	}

	[Test]
	public void LivingLibrary_ShufflesTheirCreatureIntoTheirLibrary()
	{
		var library = Field(FraCards.Get("Living Library"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);

		Activate(library, 0, bear);

		Assert.That(s.IsIn(bear, them, ZoneType.Library));
		Assert.That(Alive(library), Is.False, "sacrificed as the cost");
	}

	[Test]
	public void Marwyn_SacrificesOnlyAnArtifactOrLand_ToDraw()
	{
		var marwyn = Field(FraCards.Get("Marwyn, the Clearcutter"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var offered = Activations(marwyn).Select(a => s.GetCard(a.SacrificeId)).ToList();
		Assert.That(offered.All(c => c.Is(CardType.Land | CardType.Artifact)), "never the bear");
		Assert.That(offered.Select(c => c.Id), Does.Not.Contain(bear));

		var hand = HandCount(me);
		var land = offered.First().Id;
		Activate(marwyn, land);

		Assert.That(
			(s.IsIn(land, me, ZoneType.Graveyard), HandCount(me)),
			Is.EqualTo((true, hand + 1))
		);
	}

	[Test]
	public void Edgar_SacrificesAnother_GrowsWithMenace_AndGainsFromTheDeath()
	{
		var edgar = Field(FraCards.Get("Edgar, Ancient Bloodlord"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Assert.That(
			Activations(edgar).Select(a => a.SacrificeId),
			Does.Not.Contain(edgar),
			"another"
		);
		var life = Life(me);

		Activate(edgar, bear);

		var card = s.GetCard(edgar);
		Assert.That(
			(card.PlusOneCounters, s.Has(card, "Menace"), Life(me)),
			Is.EqualTo((1, true, life + 1))
		);
	}

	[Test]
	public void BreakUnderPressure_TheyLoseTheirBiggestManaValue()
	{
		var small = Field(TestGames.CreatureCosting("{1}", "Small", "5/5"), them);
		var pricey = Field(TestGames.CreatureCosting("{4}", "Pricey", "1/1"), them);
		var life = Life(me);

		Cast(Hand("Break Under Pressure", me), [], them);

		Assert.That((Alive(pricey), Alive(small), Life(me)), Is.EqualTo((false, true, life + 2)));
	}

	[Test]
	public void SilenceTheEcho_SacrificeOrPayThree()
	{
		var victim = Field(TestGames.Creature("Victim", "2/2"), them);
		var fodder = Field(TestGames.Creature("Fodder", "1/1"), me);
		var silence = Hand("Silence the Echo", me);
		var untapped = s.CardsIn(me, ZoneType.Battlefield)
			.Count(c => c.Is(CardType.Land) && !c.IsTapped);

		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = silence,
				Targets = [victim],
				SacrificeId = fodder,
			}
		);
		Resolve();

		var tapped =
			untapped
			- s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land) && !c.IsTapped);
		Assert.That(
			(Alive(victim), s.IsIn(fodder, me, ZoneType.Graveyard)),
			Is.EqualTo((false, true))
		);
		Assert.That(
			tapped,
			Is.EqualTo(s.GetCard(silence).ManaCost.ManaValue),
			"sacrificing paid the {3}"
		);

		Field(TestGames.Creature("Another", "2/2"), them); // something left to target
		var second = Hand("Silence the Echo", me);
		var payOffered = Casts(second).Any(a => a.SacrificeId == 0);
		Assert.That(payOffered, "paying {3} instead is always an option");
	}

	/// <summary>The Way's granted [−2] — Jace's own abilities are [−1] and [−3].</summary>
	ActivateLoyaltyAction GrantedMinusTwo(int jace) =>
		s.LegalActions()
			.OfType<ActivateLoyaltyAction>()
			.First(a =>
				a.CardId == jace
				&& Actions.Loyalty.AbilitiesOf(s, s.GetCard(jace))[a.AbilityIndex].Cost == -2
			);

	[Test]
	public void WayOfTheDeathbringer_MaySacrificeForABeast()
	{
		var way = Hand("Way of the Deathbringer", me);
		Cast(way, []);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var jace = Jace(me)!.Id;

		s = s.Act(GrantedMinusTwo(jace));
		Resolve(choice =>
			choice switch
			{
				ChoosePermanentChoice => [bear],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);

		Assert.That(s.IsIn(bear, me, ZoneType.Graveyard));
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Beast"),
			Is.EqualTo(1)
		);
	}

	[Test]
	public void WayOfTheDeathbringer_DecliningMakesNoBeast()
	{
		Cast(Hand("Way of the Deathbringer", me), []);
		Field(TestGames.Creature("Bear", "2/2"), me);
		var jace = Jace(me)!.Id;

		s = s.Act(GrantedMinusTwo(jace));
		Resolve(choice => []);

		Assert.That(s.CardsIn(me, ZoneType.Battlefield).Any(c => c.Name == "Beast"), Is.False);
	}

	[Test]
	public void GraftSurgeon_EntersWithACounter_AndPassesAllItsCountersOnWhenItDies()
	{
		var surgeon = Hand("Graft Surgeon", me);
		Cast(surgeon, []);
		Assert.That(s.GetCard(surgeon).PlusOneCounters, Is.EqualTo(1), "enters with one");
		s = s.UpdateObject(surgeon, s.GetCard(surgeon) with { PlusOneCounters = 3 }); // TEST SETUP: grew
		var heir = Field(TestGames.Creature("Heir", "1/1"), me);

		var end = Hand("Prophesied End", me); // destroy target creature
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = end,
				Targets = [surgeon],
			}
		);
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [heir],
				_ => [],
			}
		);

		Assert.That(s.IsIn(surgeon, me, ZoneType.Graveyard));
		Assert.That(s.GetCard(heir).PlusOneCounters, Is.EqualTo(3), "all of them, as it last was");
	}

	[Test]
	public void MassacreGirl_AnotherDeathPings_AndThePingFeedsHer()
	{
		var girl = Field(FraCards.Get("Massacre Girl, Most Wanted"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var (mine, theirs) = (Life(me), Life(them));

		var gasp = Hand("Last Gasp", me);
		Cast(gasp, [], bear);

		Assert.That((Life(them), Life(me)), Is.EqualTo((theirs - 1, mine + 1)));
		Assert.That(
			s.GetCard(girl).PlusOneCounters,
			Is.EqualTo(1),
			"her ping was noncombat damage"
		);
	}

	[Test]
	public void WayOfTheNecromancer_ACreatureDyingLoadsYourWalkers()
	{
		Cast(Hand("Way of the Necromancer", me), []);
		var before = Jace(me)!.Loyalty;
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);

		var gasp = Hand("Last Gasp", me);
		Cast(gasp, [], bear);

		Assert.That(Jace(me)!.Loyalty, Is.EqualTo(before + 1));
	}

	[Test]
	public void MemoryTrap_ExilesUntilItLeaves_ThenTheCardReturns()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var trap = Hand("Memory Trap", me);
		CastWithTrigger(trap, [], bear);
		Assert.That(s.IsIn(bear, them, ZoneType.Exile));

		var insurgent = Field(FraCards.Get("Budding Insurgent"), them);
		s = s.MoveCard(trap, s.ZoneId(me, ZoneType.Graveyard)); // TEST SETUP: the trap is destroyed
		Assert.That(Alive(bear), "back as soon as the trap left");
		Assert.That(Alive(insurgent));
	}

	// CR 610.3c: if the source leaves before its "exile until" ability resolves, nothing is exiled.
	[Test]
	public void MemoryTrap_GoneBeforeItsTriggerResolves_ExilesNothing()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var trap = Hand("Memory Trap", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = trap });
		for (var i = 0; i < 10 && !(s.GetPendingChoice() is ChooseTargetChoice); i++)
			s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
		s = s.Choose([bear]);

		s = s.MoveCard(trap, s.ZoneId(me, ZoneType.Graveyard)); // TEST SETUP: removed in response
		Resolve();

		Assert.That(Alive(bear));
	}

	// ===== Found by review =====

	// CR 603.3b across state-based-check rounds: the non-active player's trigger (seen in round 1)
	// must end up on TOP of the active player's (seen in round 2, after the check killed a creature).
	[Test]
	public void TriggersFromLaterCheckRounds_StillGoOnTheStackApnap()
	{
		var theirEdgar = Field(FraCards.Get("Edgar, Ancient Bloodlord"), them);
		var theirBear = Field(TestGames.Creature("TheirBear", "2/2"), them);
		Field(FraCards.Get("Massacre Girl, Most Wanted"), me);
		var myOne = Field(TestGames.Creature("MyOne", "1/1"), me);
		var both = new CardDefinition(
			new CardData(
				"Both",
				"normal",
				"{1}",
				1,
				"Sorcery",
				null,
				null,
				null,
				null,
				null,
				[],
				"common",
				"0",
				null
			),
			[
				new Effects.SpellEffectsComponent(
					[
						new Effects.Destroy(Effects.Ref.Target(0)),
						new Effects.ModifyUntilEndOfTurn(Effects.Ref.Target(1), -1, -1),
					]
				)
				{
					Targets =
					[
						new Targeting.TargetSpec(Targeting.TargetKind.Creature),
						new Targeting.TargetSpec(Targeting.TargetKind.Creature),
					],
				},
			]
		);
		int spell;
		(s, spell) = TestGames.Put(s, me, both, ZoneType.Hand);

		// Destroy theirs during resolution (round 1: their Edgar); shrink mine to 0 (the check kills it, round 2: my Massacre Girl).
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = spell,
				Targets = [theirBear, myOne],
			}
		);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = them });
		while (s.GetPendingChoice() is { } choice)
			s = s.Choose(choice.Options.Take(Math.Max(1, choice.MinChoices)).Select(o => o.Id));

		var top = (StackAbility)s.GetChildren(s.GetWellKnownId(MagicKeys.Stack)).First();
		Assert.That(
			top.SourceId,
			Is.EqualTo(theirEdgar),
			"the non-active player's trigger resolves first"
		);
	}

	// A "none" answer leaves the pipeline context untouched; under the shared "chosen" key a stale
	// earlier pick was sacrificed anyway — and the Beast made.
	[Test]
	public void DecliningMaySacrifice_IgnoresAStaleEarlierPick()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var stale = System
			.Collections.Immutable.ImmutableDictionary<string, object>.Empty.Add(
				Effects.ContextKeys.Controller,
				me
			)
			.Add(Effects.ContextKeys.Source, bear)
			.Add(ChooseCardsFromHandChoice.Chosen, bear)
			.Add(ChoosePermanentChoice.Chosen, bear);
		var effect = new Effects.MaySacrifice(
			new Targeting.TargetSpec(Targeting.TargetKind.Creature, Targeting.Whose.You),
			[new Effects.CreateTokens(FraCards.Beast, 1)]
		)
		{
			InputContext = stale,
		};

		s = s.AddAction(new PipelineAction { Steps = [effect] }).ProcessAllActions().State;
		s = s.Choose([]);

		Assert.That(Alive(bear));
		Assert.That(s.CardsIn(me, ZoneType.Battlefield).Any(c => c.Name == "Beast"), Is.False);
	}

	// CR 400.7: a Memory Trap that left and came back is a new object; its old trigger exiles nothing.
	[Test]
	public void MemoryTrap_ThatLeftAndCameBack_DoesNotExile()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var trap = Hand("Memory Trap", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = trap });
		for (var i = 0; i < 10 && !(s.GetPendingChoice() is ChooseTargetChoice); i++)
			s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
		s = s.Choose([bear]);

		// TEST SETUP: it leaves and is back, same id, as a new object. Moved directly rather than through
		// EnterBattlefield, so it brings no NEW enters trigger — a new one would rightly exile the bear.
		s = s.MoveCard(trap, s.ZoneId(me, ZoneType.Hand))
			.MoveCard(trap, s.ZoneId(me, ZoneType.Battlefield));
		Resolve();

		Assert.That(Alive(bear));
	}

	// CR 704.3: the check repeats until nothing applies. The legend rule removing a NONcreature
	// raises no event — the walker it was keeping alive at 0 loyalty must still die in that check.
	[Test]
	public void StateBasedActions_RepeatUntilNothingApplies_EvenWithoutAnEvent()
	{
		CardDefinition Shrine(params GameComponent[] statics) =>
			new(
				new CardData(
					"Shrine",
					"normal",
					"{1}",
					1,
					"Legendary Enchantment",
					null,
					null,
					null,
					null,
					null,
					[],
					"common",
					"0",
					null
				),
				[.. statics]
			);
		Field(Shrine(new Effects.YourPlaneswalkersSurviveZeroLoyalty()), me); // the older copy keeps walkers alive
		var walker = Hand(TestGames.Planeswalker("Walker", 3), me);
		s = s.EnterBattlefield(walker, me);
		s = s.UpdateObject(walker, s.GetCard(walker) with { Loyalty = 0 }); // TEST SETUP
		var newer = Hand(Shrine(), me);

		s = s.EnterBattlefield(newer, me); // the legend rule will keep this one: no survival static
		s = s.Act(new PassPriorityAction { PlayerId = me });

		Assert.That(s.IsIn(walker, me, ZoneType.Graveyard));
	}
}
