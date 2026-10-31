using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Rares (8e), copying permanents — each ability fires.</summary>
public class FraRareCopyTests : FraBoardTest
{
	static CardDefinition Kill =>
		new(
			TestGames.Inline("Kill", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([new Destroy(Ref.Target())])
				{
					Targets = [TargetSpec.Creature],
				},
			]
		);

	static CardDefinition Legendary(string name, string pt, params string[] keywords)
	{
		var c = TestGames.Creature(name, pt, keywords);
		return c with { Data = c.Data with { TypeLine = "Legendary Creature — Test" } };
	}

	static CardDefinition Herald =>
		TestGames.Creature("Herald", "2/2") with
		{
			Components =
			[
				new TriggeredAbilityComponent(TriggerEvent.SelfEnters, [new GainLife(Ref.You, 3)]),
			],
		};

	IEnumerable<Card> Named(int player, string name) =>
		s.CardsIn(player, ZoneType.Battlefield).Where(c => c.Name == name);

	void ToTheirTurn() => s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);

	// ===== Sphinx of False Conclusions =====

	[Test]
	public void SphinxOfFalseConclusions_Attacking_DrawsThenDiscards()
	{
		var sphinx = Field(CardRegistry.Get("Sphinx of False Conclusions"), me);
		var hand = HandCount(me);
		var graveyard = s.CardsIn(me, ZoneType.Graveyard).Count();
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([declare.CreatureId == sphinx ? them : CombatOption.None]);
		Resolve();
		Assert.That(
			(HandCount(me), s.CardsIn(me, ZoneType.Graveyard).Count()),
			Is.EqualTo((hand, graveyard + 1))
		);
	}

	[Test]
	public void SphinxOfFalseConclusions_Dying_LeavesATokenCopy_WhichLeavesNone()
	{
		var sphinx = Field(CardRegistry.Get("Sphinx of False Conclusions"), me);
		s = s.UpdateObject(sphinx, s.GetCard(sphinx) with { PlusOneCounters = 2 }); // TEST SETUP
		Cast(Hand(Kill, me), [], sphinx);
		var copy = Named(me, "Sphinx of False Conclusions").Single();
		Assert.That(
			(copy.IsToken, s.Power(copy), s.Has(copy, "Flying"), copy.PlusOneCounters),
			Is.EqualTo((true, 4, true, 0)),
			"its copiable values, not its counters"
		);
		Cast(Hand(Kill, me), [], copy.Id);
		Assert.That(
			Named(me, "Sphinx of False Conclusions"),
			Is.Empty,
			"a token's death makes no copy"
		);
	}

	// ===== Face Yourself =====

	[Test]
	public void FaceYourself_CopiesEachOfTheirCreatures_WithHaste_GoneAtTheEndStep()
	{
		var herald = Field(Herald, them);
		s = s.UpdateObject(herald, s.GetCard(herald) with { PlusOneCounters = 1, IsTapped = true }); // TEST SETUP
		Field(TestGames.Creature("Bear", "2/2"), them);
		var life = Life(me);
		Cast(Hand("Face Yourself", me), [], them);
		var copies = s.CardsIn(me, ZoneType.Battlefield).Where(c => c.IsToken).ToList();
		Assert.That(copies.Select(c => c.Name).Order(), Is.EqualTo(new[] { "Bear", "Herald" }));
		var heraldCopy = copies.Single(c => c.Name == "Herald");
		Assert.That(
			(s.Has(heraldCopy, "Haste"), heraldCopy.PlusOneCounters, heraldCopy.IsTapped, Life(me)),
			Is.EqualTo((true, 0, false, life + 3)),
			"haste; no counters; its own 'enters' fired"
		);
		ToTheirTurn();
		Assert.That(s.CardsIn(me, ZoneType.Battlefield).Where(c => c.IsToken), Is.Empty);
	}

	[Test]
	public void FaceYourself_WithAPlaneswalker_TheCopiesStay()
	{
		Field(TestGames.Creature("Bear", "2/2"), them);
		var walker = Field(TestGames.Planeswalker("Walker", 3), me);
		s = s.UpdateObject(walker, s.GetCard(walker) with { Loyalty = 3 }); // TEST SETUP
		Cast(Hand("Face Yourself", me), [], them);
		ToTheirTurn();
		Assert.That(Named(me, "Bear").Count(), Is.EqualTo(1));
	}

	[Test]
	public void FaceYourself_OnMyOwnLegend_TheLegendRuleKeepsTheCard()
	{
		var legend = Field(Legendary("Legend", "3/3"), me);
		Cast(Hand("Face Yourself", me), [], me);
		Assert.That(
			(Alive(legend), s.CardsIn(me, ZoneType.Battlefield).Count(c => c.IsToken)),
			Is.EqualTo((true, 0)),
			"the token copy goes, not the card"
		);
	}

	[Test]
	public void FaceYourself_UnderDraconicVisitor_ArtifactCopiesAreDragons()
	{
		Field(CardRegistry.Get("Draconic Visitor"), me);
		var golem = TestGames.Creature("Golem", "3/3");
		Field(
			golem with
			{
				Data = golem.Data with { TypeLine = "Artifact Creature — Golem" },
			},
			them
		);
		Cast(Hand("Face Yourself", me), [], them);
		var tokens = s.CardsIn(me, ZoneType.Battlefield).Where(c => c.IsToken).Select(c => c.Name);
		Assert.That(tokens, Is.EqualTo(new[] { "Dragon" }));
	}

	// ===== Vindictive Triumph =====

	[Test]
	public void VindictiveTriumph_ManaValueThreeOrLess_MineTappedUntilTheEndStep()
	{
		var bear = Field(TestGames.CreatureCosting("{1}{G}", "Bear", "2/2"), them);
		Cast(Hand("Vindictive Triumph", me), [], bear);
		var card = s.GetCard(bear);
		Assert.That(
			(s.IsIn(bear, me, ZoneType.Battlefield), card.IsTapped),
			Is.EqualTo((true, true))
		);
		ToTheirTurn();
		Assert.That(s.IsIn(bear, them, ZoneType.Exile), "exiled at the next end step");
	}

	[Test]
	public void VindictiveTriumph_ManaValueFour_JustExiled()
	{
		var big = Field(TestGames.CreatureCosting("{3}{G}", "Big", "4/4"), them);
		Cast(Hand("Vindictive Triumph", me), [], big);
		Assert.That(s.IsIn(big, them, ZoneType.Exile));
	}

	// ===== Hall of Echoes =====

	[Test]
	public void HallOfEchoes_BecomesACopyUntilEndOfTurn_AndTheLegendRuleWaits()
	{
		var hall = Field(CardRegistry.Get("Hall of Echoes"), me);
		var legend = Field(Legendary("Legend", "3/3", "Flying"), me);
		s = s.Act(Activations(hall).Single(a => a.Targets[0] == legend));
		Resolve();
		var copy = s.GetCard(hall);
		Assert.That(
			(
				copy.Name,
				copy.Is(CardType.Land),
				s.Power(copy),
				s.Has(copy, "Flying"),
				Alive(hall),
				Alive(legend)
			),
			Is.EqualTo(("Legend", false, 3, true, true, true)),
			"a copy, and no legend rule this turn"
		);
		Assert.That(s.CardsIn(me, ZoneType.Command).Count(), Is.EqualTo(1), "the effect, shown");
		ToTheirTurn();
		Assert.That(s.CardsIn(me, ZoneType.Command), Is.Empty, "it ended with the turn");
		var back = s.GetCard(hall);
		Assert.That(
			(Alive(hall), back.Name, back.Is(CardType.Land), back.Is(CardType.Creature)),
			Is.EqualTo((true, "Hall of Echoes", true, false)),
			"itself again in cleanup — still on the battlefield"
		);
	}

	[Test]
	public void HallOfEchoes_DyingAsACopy_IsTheHallInTheGraveyard()
	{
		var hall = Field(CardRegistry.Get("Hall of Echoes"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		s = s.Act(Activations(hall).Single(a => a.Targets[0] == bear));
		Resolve();
		Cast(Hand(Kill, me), [], hall);
		Assert.That(
			(
				s.IsIn(hall, me, ZoneType.Graveyard),
				s.GetCard(hall).Name,
				s.GetCard(hall).Is(CardType.Land)
			),
			Is.EqualTo((true, "Hall of Echoes", true))
		);
	}
}
