using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Engine pieces (7f): "loses all abilities" — every kind of ability goes.</summary>
public class FraEngineTests : FraBoardTest
{
	static CardDefinition Red(CardDefinition card) =>
		card with
		{
			Data = card.Data with { Colors = ["R"] },
		};

	void Grapple(int theirs, int mine) => Cast(Hand("Flourishing Grapple", me), [], theirs, mine);

	void Coursework(int creature) => Cast(Hand("Infinite Coursework", me), [], creature);

	[Test]
	public void FlourishingGrapple_KeywordsGo_AndYourCreatureHitsIt()
	{
		var flier = Field(Red(TestGames.Creature("Flier", "2/5", "Flying", "First strike")), them);
		var mine = Field(TestGames.Creature("Mine", "3/3"), me);
		Grapple(flier, mine);
		var card = s.GetCard(flier);
		Assert.That(
			(s.Has(card, "Flying"), s.Has(card, "First strike"), card.Damage),
			Is.EqualTo((false, false, 3))
		);
		Assert.That(s.GetCard(mine).Damage, Is.Zero, "it isn't a fight");
	}

	[Test]
	public void FlourishingGrapple_ItsStaticStopsApplying_ThenComesBackNextTurn()
	{
		var lord = Red(TestGames.Creature("Lord", "2/5"));
		var lordId = Field(
			lord with
			{
				Components =
				[
					new StaticEffectComponent(
						MagicCore.Filters.Is.Creature
							& MagicCore.Filters.Is.Yours
							& !MagicCore.Filters.Is.Source
					)
					{
						Power = 1,
					},
				],
			},
			them
		);
		var other = Field(TestGames.Creature("Other", "2/2"), them);
		var mine = Field(TestGames.Creature("Mine", "1/1"), me);
		Assert.That(s.Power(s.GetCard(other)), Is.EqualTo(2 + 1));

		Grapple(lordId, mine);
		Assert.That(s.Power(s.GetCard(other)), Is.EqualTo(2), "the anthem is an ability it lost");

		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);
		Assert.That(s.Power(s.GetCard(other)), Is.EqualTo(2 + 1), "until end of turn only");
	}

	[Test]
	public void FlourishingGrapple_OnlyRedOrWhite_TheirsAndYours()
	{
		var green = Field(TestGames.Creature("Green", "2/2"), them);
		var red = Field(Red(TestGames.Creature("Red", "2/2")), them);
		Field(TestGames.Creature("Mine", "1/1"), me);
		var grapple = Hand("Flourishing Grapple", me);
		var first = Casts(grapple).Select(c => c.Targets[0]).ToList();
		Assert.That(first, Does.Contain(red).And.Not.Contain(green));
	}

	[Test]
	public void InfiniteCoursework_TapsIt_AndItDoesntUntap()
	{
		var theirs = Field(TestGames.Creature("Theirs", "2/2", "Flying"), them);
		Coursework(theirs);
		Assert.That(
			(s.GetCard(theirs).IsTapped, s.Has(s.GetCard(theirs), "Flying")),
			Is.EqualTo((true, false))
		);
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().Step == Step.PreCombatMain
		);
		Assert.That(s.GetCard(theirs).IsTapped, "the Aura says it doesn't untap");
	}

	[Test]
	public void InfiniteCoursework_ItsTriggersGo()
	{
		var tinybones = Field(FraCards.Get("Tinybones, Pocket Nuisance"), them);
		Coursework(tinybones);
		Hand(TestGames.Inline("Fodder", "{1}", "Sorcery"), me);
		var life = Life(me);
		Cast(
			Hand(TestGames.Inline("Purge", "{1}", "Sorcery", null, new Discard(Ref.You, 1)), me),
			[]
		);
		Assert.That(Life(me), Is.EqualTo(life), "Tinybones lost \"whenever a player discards\"");
	}

	[Test]
	public void InfiniteCoursework_ItsManaAbilityGoes()
	{
		var dork = TestGames.Creature("Dork", "1/1");
		var dorkId = Field(
			dork with
			{
				Components = [new ManaAbilityComponent(ManaColor.Green)],
			},
			me
		);
		Assert.That(ManaAbilities.ColorsOf(s, s.GetCard(dorkId)), Is.Not.Empty);
		Coursework(dorkId);
		Assert.That(ManaAbilities.ColorsOf(s, s.GetCard(dorkId)), Is.Empty);
	}

	[Test]
	public void InfiniteCoursework_ItBecomesUnprepared()
	{
		var foreseer = Field(FraCards.Get("Semester Foreseer // Peer Review"), them);
		s = Rules.Prepare.Become(s, foreseer);
		Assert.That(s.GetCard(foreseer).IsPrepared);
		Coursework(foreseer);
		Assert.That(s.GetCard(foreseer).IsPrepared, Is.False);
	}

	[Test]
	public void InfiniteCoursework_ItsAbilitiesReturnWhenTheAuraLeaves()
	{
		var theirs = Field(TestGames.Creature("Theirs", "2/2", "Flying"), them);
		Coursework(theirs);
		var aura = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Infinite Coursework");
		Cast(
			Hand(
				TestGames.Inline("Shatter", "{1}", "Instant", null, new Destroy(Ref.Target())) with
				{
					Components =
					[
						new SpellEffectsComponent([new Destroy(Ref.Target())])
						{
							Targets = [TargetSpec.Permanent()],
						},
					],
				},
				me
			),
			[],
			aura.Id
		);
		Assert.That(s.Has(s.GetCard(theirs), "Flying"));
	}

	static CardDefinition Shatter =>
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
	public void InfiniteCoursework_ItDiesWithoutItsDiesTrigger()
	{
		var bomb = TestGames.Creature("Bomb", "2/2");
		var dies = new TriggeredAbilityComponent(
			TriggerEvent.SelfDies,
			[new DealDamage(Ref.EachOpponent, 3)]
		);
		var theirs = Field(bomb with { Components = [dies] }, them);
		Coursework(theirs);
		var life = Life(me);
		// Lethal damage: the state-based check takes the creature AND its Aura in one pass, so the
		// Aura is gone before the death's triggers are collected.
		var bolt = new CardDefinition(
			TestGames.Inline("Bolt", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([new DealDamage(Ref.Target(), 5)])
				{
					Targets = [TargetSpec.Creature],
				},
			]
		);
		Cast(Hand(bolt, me), [], theirs);
		Assert.That(s.IsIn(theirs, them, ZoneType.Graveyard));
		Assert.That(Life(me), Is.EqualTo(life), "as it last existed, it had no abilities");
	}

	[Test]
	public void InfiniteCoursework_ATriggerGrantedUntilEndOfTurnGoesToo()
	{
		var mine = Field(TestGames.Creature("Mine", "2/2"), me);
		var ping = new TriggeredAbilityComponent(
			TriggerEvent.YouGainLife,
			[new DealDamage(Ref.EachOpponent, 1)]
		);
		s = s.UpdateObject(
			mine,
			(Card)s.GetCard(mine).WithComponent(new UntilEndOfTurn { Trigger = ping })
		); // TEST SETUP
		Coursework(mine);
		var life = Life(them);
		Cast(
			Hand(TestGames.Inline("Heal", "{W}", "Instant", null, new GainLife(Ref.You, 1)), me),
			[]
		);
		Assert.That(Life(them), Is.EqualTo(life));
	}

	[Test]
	public void FlourishingGrapple_AStrippedPlaneswalkerLosesGrantedLoyaltyAbilities()
	{
		Field(FraCards.Get("Kiora of Salt and Sand"), them); // grants "[-8]: Create … Leviathan"
		var walker = Field(Red(TestGames.Planeswalker("Walker", 9)), them);
		s = s.UpdateObject(walker, s.GetCard(walker) with { Loyalty = 9 }); // TEST SETUP: Put skips entering
		Assert.That(Loyalty.AbilitiesOf(s, s.GetCard(walker)), Is.Not.Empty);
		var mine = Field(TestGames.Creature("Mine", "1/1"), me);
		Grapple(walker, mine);
		Assert.That(Loyalty.AbilitiesOf(s, s.GetCard(walker)), Is.Empty);
	}

	[Test]
	public void InfiniteCoursework_RemovedInResponse_ItsTriggerStillTapsTheCreature()
	{
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		var coursework = Hand("Infinite Coursework", me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = coursework,
				Targets = [theirs],
			}
		);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = them }); // the Aura resolves; its trigger goes on the stack
		Assert.That(s.StackIsEmpty(), Is.False);
		var shatter = Hand(Shatter, me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = shatter,
				Targets = [coursework],
			}
		);
		Resolve();
		Assert.That(s.IsIn(coursework, me, ZoneType.Graveyard));
		Assert.That(s.GetCard(theirs).IsTapped, "\"enchanted creature\" is read as it last was");
	}

	// ===== 7f-2: mana =====

	void TapAllLandsBut(params string[] keep)
	{
		var kept = new HashSet<string>();
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)).ToList()
		)
			s = s.UpdateObject(
				land.Id,
				land with
				{
					IsTapped = !(keep.Contains(land.Name) && kept.Add(land.Name)),
				}
			); // TEST SETUP: exactly one of each kept land untapped
	}

	static CardDefinition Spell(string cost) =>
		TestGames.Inline("Spell", cost, "Instant", null, new GainLife(Ref.You, 1));

	[Test]
	public void LootTheNexus_OneColourForEachDifferentPower()
	{
		Field(FraCards.Get("Loot, the Nexus"), me); // a 2/1
		Field(TestGames.Creature("Two", "2/2"), me);
		Field(TestGames.Creature("Three", "3/3"), me);
		TapAllLandsBut();
		Assert.That(Casts(Hand(Spell("{B}{B}"), me)), Is.Not.Empty, "powers 2 and 3: two mana");
		Assert.That(Casts(Hand(Spell("{U}{B}"), me)), Is.Empty, "all of ONE colour");
		Assert.That(
			Casts(Hand(Spell("{B}{B}{B}"), me)),
			Is.Empty,
			"two different powers, not three creatures"
		);
	}

	[Test]
	public void LootTheNexus_WhatTheSpellDoesntUseFloats()
	{
		var loot = Field(FraCards.Get("Loot, the Nexus"), me);
		Field(TestGames.Creature("Three", "3/3"), me);
		TapAllLandsBut();
		var spell = Hand(Spell("{G}"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spell });
		Assert.That(
			(s.GetCard(loot).IsTapped, s.GetPlayer(me).ManaPool.Green),
			Is.EqualTo((true, 1))
		);
	}

	[Test]
	public void HeartwoodCrafter_ItsManaPaysAbilities_NotSpellsFromYourHand()
	{
		var crafter = Field(FraCards.Get("Heartwood Crafter // Soul Tether"), me);
		var engine = TestGames.Creature("Engine", "1/1");
		var engineId = Field(
			engine with
			{
				Components =
				[
					new ActivatedAbilityComponent(
						ManaCost.Parse("{1}"),
						[new GainLife(Ref.You, 1)]
					),
				],
			},
			me
		);
		TapAllLandsBut();
		Assert.That(Casts(Hand(Spell("{1}"), me)), Is.Empty, "not for a spell from your hand");
		Assert.That(Activations(engineId), Is.Not.Empty, "an ability is fine");
		s = s.Act(
			new TapForManaAction
			{
				PlayerId = me,
				CardId = crafter,
				Color = ManaColor.Colorless,
			}
		);
		Assert.That(
			(s.GetPlayer(me).ManaPool.Total, s.GetPlayer(me).ManaPool.Restricted.Count),
			Is.EqualTo((0, 1)),
			"tapped by hand, it floats — still restricted"
		);
		Assert.That(
			Casts(Hand(Spell("{1}"), me)),
			Is.Empty,
			"floating, it still can't pay a spell from hand"
		);
		Assert.That(Activations(engineId), Is.Not.Empty, "but an ability, yes");
	}

	[Test]
	public void HeartwoodCrafter_ItsManaCantPayForASpellFromHand_ButItCanStillConvoke()
	{
		var winter = Hand("Winter, Team Player", me); // {4}{R}, convoke
		Field(FraCards.Get("Heartwood Crafter // Soul Tether"), me);
		Field(Red(TestGames.Creature("Red", "1/1")), me);
		for (var i = 0; i < 3; i++)
			Field(TestGames.Creature("Grey", "1/1"), me);
		TapAllLandsBut();
		Assert.That(Casts(winter), Is.Not.Empty, "tapping it to convoke isn't spending its mana");
	}

	[Test]
	public void PyreRhymer_MoltenTideMakesEachMountainTapForTwo()
	{
		var rhymer = Field(FraCards.Get("Pyre Rhymer // Molten Tide"), me);
		s = Rules.Prepare.Become(s, rhymer);
		var tide = s.CardsIn(me, ZoneType.Exile).Single(c => c.IsCopy).Id;
		var red = Hand(Spell("{R}{R}"), me);
		TapAllLandsBut("Mountain", "Plains");
		Assert.That(Casts(red), Is.Empty, "one Mountain: one {R}");

		s = s.Act(Casts(tide).Single()); // Molten Tide costs {R}: the Mountain pays it
		Resolve();
		TapAllLandsBut("Mountain");
		Assert.That(Casts(red), Is.Not.Empty, "now a Mountain makes {R}{R}");
	}

	[Test]
	public void ManyMountainsUnderMoltenTide_AnUnpayableCostFailsFast()
	{
		for (var i = 0; i < 8; i++)
			Field(TestGames.Card("Mountain"), me);
		s = GetEmblem.ToCommandZone(s, me, "Molten Tide", [new ExtraRedFromMountains()]); // TEST SETUP: Molten Tide resolved
		TapAllLandsBut();
		foreach (
			var m in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Name == "Mountain").ToList()
		)
			s = s.UpdateObject(m.Id, m with { IsTapped = false }); // TEST SETUP: 12 Mountains, 24 {R}
		var clock = System.Diagnostics.Stopwatch.StartNew();
		Assert.That(
			ManaPayment.Find(s, me, ManaCost.Parse("{R}{R}{R}{R}{R}{R}{R}{R}{R}{R}{W}"), 0),
			Is.Null
		);
		Assert.That(
			clock.ElapsedMilliseconds,
			Is.LessThan(500),
			"identical Mountains are one choice"
		);
	}

	// ===== 7f-3: copying a spell =====

	static CardDefinition Bolt(int damage) =>
		new(
			TestGames.Inline("Bolt", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([new DealDamage(Ref.Target(), damage)])
				{
					Targets = [TargetSpec.Creature],
				},
			]
		);

	/// <summary>Casts Way of the Cryomancer (Jace enters with 5), then activates the −3 it grants Jace.</summary>
	void WayMinusThree()
	{
		Cast(Hand("Way of the Cryomancer", me), []);
		var jace = Jace(me)!;
		var granted = Loyalty.AbilitiesOf(s, jace).Count - 1; // granted abilities come after its own
		s = s.Act(
			s.LegalActions()
				.OfType<ActivateLoyaltyAction>()
				.Single(a => a.CardId == jace.Id && a.AbilityIndex == granted)
		);
		Resolve();
	}

	[Test]
	public void WayOfTheCryomancer_EmpowersJaceFive_AndGrantsTheCopy()
	{
		Cast(Hand("Way of the Cryomancer", me), []);
		var jace = Jace(me)!;
		Assert.That(jace.Loyalty, Is.EqualTo(5));
		Assert.That(
			Loyalty.AbilitiesOf(s, jace).Select(a => a.Cost),
			Does.Contain(-3).And.Contain(-1)
		);
		Assert.That(
			Loyalty.AbilitiesOf(s, jace).Count(a => a.Cost == -3),
			Is.EqualTo(2),
			"its own, and Way's"
		);
	}

	[Test]
	public void WayOfTheCryomancer_TheNextInstantIsCopied_WithANewTarget()
	{
		var a = Field(TestGames.Creature("A", "2/2"), them);
		var b = Field(TestGames.Creature("B", "2/2"), them);
		WayMinusThree();
		var bolt = Hand(Bolt(2), me);
		var graveyard = s.CardsIn(me, ZoneType.Graveyard).Count();
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = bolt,
				Targets = [a],
			}
		);
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [b], // the copy's new target
				_ => [],
			}
		);
		Assert.That((Alive(a), Alive(b)), Is.EqualTo((false, false)));
		Assert.That(
			s.CardsIn(me, ZoneType.Graveyard).Count(),
			Is.EqualTo(graveyard + 1),
			"the copy ceases to exist; only the Bolt is in the graveyard"
		);
	}

	[Test]
	public void WayOfTheCryomancer_OnlyTheNextOne_AndACreatureSpellDoesntUseItUp()
	{
		var a = Field(TestGames.Creature("A", "3/3"), them);
		var b = Field(TestGames.Creature("B", "3/3"), them);
		WayMinusThree();
		Cast(Hand(TestGames.Creature("Bear", "2/2"), me), []); // a creature spell: not copied, not used up
		var first = Hand(Bolt(2), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = first,
				Targets = [a],
			}
		);
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [a], // the copy keeps the target
				_ => [],
			}
		);
		Assert.That(Alive(a), Is.False, "the Bolt and its copy: 4 damage");
		var second = Hand(Bolt(2), me);
		var copyTargetsAsked = 0;
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = second,
				Targets = [b],
			}
		);
		Resolve(choice =>
		{
			copyTargetsAsked += choice is ChooseTargetChoice ? 1 : 0;
			return choice.Options.Take(choice.MinChoices).Select(o => o.Id);
		});
		Assert.That(
			(copyTargetsAsked, s.GetCard(b).Damage),
			Is.EqualTo((0, 2)),
			"the next instant isn't copied"
		);
	}

	[Test]
	public void WayOfTheCryomancer_TheCopyIsNotCast()
	{
		var a = Field(TestGames.Creature("A", "5/5"), them);
		WayMinusThree();
		var tomik = Field(FraCards.Get("Tomik, Izzet Sparkmage"), me); // prowess — after Way, itself a cast
		Cast(Hand(Bolt(1), me), [], a);
		Assert.That(s.Power(s.GetCard(tomik)), Is.EqualTo(1 + 1), "prowess saw one cast, not two");
	}

	static CardDefinition Targeted(
		string name,
		Targeting.TargetSpec spec,
		params GameAction[] effects
	) =>
		new(
			TestGames.Inline(name, "{1}", "Instant").Data,
			[new SpellEffectsComponent([.. effects]) { Targets = [spec] }]
		);

	[Test]
	public void WayOfTheCryomancer_ACopyWhoseTargetIsGoneFizzles()
	{
		var a = Field(TestGames.Creature("A", "2/2"), them);
		WayMinusThree();
		var bolt = Hand(
			Targeted(
				"Bolt",
				TargetSpec.Creature,
				new DealDamage(Ref.Target(), 2),
				new GainLife(Ref.You, 1)
			),
			me
		);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = bolt,
				Targets = [a],
			}
		);
		// The copy trigger is on the stack: kill A in response, so nothing is left to target.
		var kill = Hand(Targeted("Kill", TargetSpec.Creature, new Destroy(Ref.Target())), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = kill,
				Targets = [a],
			}
		);
		var life = Life(me);
		Resolve();
		Assert.That(
			Life(me),
			Is.EqualTo(life),
			"the copy keeps its gone target and does nothing, as the Bolt does"
		);
	}

	[Test]
	public void WayOfTheCryomancer_ACounteredSpellIsStillCopied()
	{
		var b = Field(TestGames.Creature("B", "2/2"), them);
		var a = Field(TestGames.Creature("A", "2/2"), them);
		WayMinusThree();
		var bolt = Hand(Bolt(2), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = bolt,
				Targets = [a],
			}
		);
		var counter = Hand(Targeted("Nope", TargetSpec.Spell(), new Counter(Ref.Target())), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = counter,
				Targets = [bolt],
			}
		);
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [b],
				_ => [],
			}
		);
		Assert.That(
			(Alive(a), Alive(b)),
			Is.EqualTo((true, false)),
			"the Bolt was countered; its copy wasn't"
		);
	}

	[Test]
	public void ACopysSecondTarget_IsNeverItsFirst()
	{
		var x = Field(TestGames.Creature("X", "2/2"), me);
		var y = Field(TestGames.Creature("Y", "2/2"), me);
		WayMinusThree();
		var preparations = Hand("Predictive Preparations", me);
		IEnumerable<int> secondOffered = [];
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = preparations,
				Targets = [x, y],
			}
		);
		Resolve(choice =>
		{
			if (choice is not ChooseTargetChoice target)
				return choice.Options.Take(choice.MinChoices).Select(o => o.Id);
			if (target.Index == 0)
				return [x];
			secondOffered = choice.Options.Select(o => o.Id).ToList();
			return []; // optional: keep the original second target
		});
		Assert.That(secondOffered, Does.Not.Contain(x));
	}

	[Test]
	public void ACopyOfAnUncounterableSpell_CanBeCountered()
	{
		var a = Field(TestGames.Creature("A", "2/2"), them);
		var b = Field(TestGames.Creature("B", "2/2"), them);
		WayMinusThree();
		var proxy = Field(FraCards.Get("Theorist's Proxy"), me);
		s = s.Act(Activations(proxy).Single()); // the next spell you cast this turn can't be countered
		Resolve();
		var bolt = Hand(Bolt(2), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = bolt,
				Targets = [a],
			}
		);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = them }); // the copy trigger resolves: choose B
		s = s.Choose([b]);
		var copy = s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack)).First();
		var nope = Hand(Targeted("Nope", TargetSpec.Spell(), new Counter(Ref.Target())), me);
		Cast(nope, [], copy);
		Assert.That(
			(Alive(a), Alive(b)),
			Is.EqualTo((false, true)),
			"the copy wasn't cast; the Bolt was"
		);
	}
}
