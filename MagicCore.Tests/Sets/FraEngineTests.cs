using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;
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
					new StaticEffectComponent(Affects.OtherCreaturesYouControl) { Power = 1 },
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
							Targets = [new Targeting.TargetSpec(Targeting.TargetKind.Permanent)],
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
					Targets = [new Targeting.TargetSpec(Targeting.TargetKind.Permanent)],
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
					Targets = [new Targeting.TargetSpec(Targeting.TargetKind.Creature)],
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
}
