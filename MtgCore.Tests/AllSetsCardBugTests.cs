using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Structural rules that every card in every registered set must obey.
///
/// Each rule here was written after a card was found to silently do nothing in play. A card that
/// builds, casts and resolves without throwing can still be completely inert, and no other fixture
/// catches that — the per-set fixtures only assert a card reaches the battlefield.
///
/// THIS ITERATES SetRegistry.All ON PURPOSE, and that is the entire point of the file. It began as
/// CoresetCubeCardBugTests, scoped to one set, and HollowmereCardBugTests already had the same
/// user-select-in-a-trigger rule scoped to ITS set — so when the Core Set Cube reintroduced the
/// bug, the existing test simply was not looking. A set-scoped structural fixture is worth almost
/// nothing to the set that comes after it. Adding a new set to SetRegistry must automatically
/// subject it to every rule below; never narrow one of these back to a single set.
///
/// Genuine per-card exceptions belong in the Allowed* lists, named and justified, so an exception
/// is a decision someone made rather than a rule quietly weakened.
/// </summary>
[TestFixture]
public class AllSetsCardBugTests
{
	/// <summary>Every card in every registered set, tagged with the set it came from.</summary>
	private static IEnumerable<(string Set, Card Card)> AllCards() =>
		SetRegistry.All.SelectMany(s => s.Cards.Select(c => (s.Code, c)));

	/// <summary>
	/// Triggered abilities spawn ResolveEffectAction with NO TargetIds, so a user-select
	/// targeting strategy inside a trigger resolves to an empty target list and the effect
	/// silently does nothing.
	///
	/// Found in play: Pegasus Courser's attack trigger did nothing at all.
	///
	/// Triggers must use AllValid, Random, Self, or a context key. TriggerTargeting.MakeResolvable
	/// downgrades user-select to Random at build time, so a card going through the builders cannot
	/// reintroduce this — a hand-built TriggeredAbilityComponent still can.
	/// </summary>
	[Test]
	public void TriggeredAbilities_DoNotUseUserSelectTargeting()
	{
		var offenders = new List<string>();
		foreach (var (set, card) in AllCards())
		foreach (var trigger in card.GetComponents<TriggeredAbilityComponent>())
		foreach (var effect in trigger.Effects)
		{
			if (effect.TargetingStrategy.RequiresUserSelection)
				offenders.Add($"[{set}] {card.Name} ({trigger.Name})");
		}

		Assert.That(
			offenders.Distinct(),
			Is.Empty,
			"Triggers with user-select targeting silently do nothing: "
				+ string.Join("; ", offenders.Distinct())
		);
	}

	/// <summary>
	/// ResolveEffectAction can only inject targets into an ITargetedAction. An action that reads
	/// its card ids from its own fields or a context key gets nothing from a targeting strategy,
	/// so the effect resolves against no cards at all.
	///
	/// Found in play: Condemn and Aetherspouts both did nothing.
	/// </summary>
	[Test]
	public void TargetedEffects_UseActionsThatCanReceiveTargets()
	{
		var offenders = new List<string>();

		foreach (var (set, card) in AllCards())
		foreach (var effect in AllEffects(card))
		{
			// A NoTarget strategy resolves to an empty list by design; the action is expected to
			// find its own subject from context.
			if (effect.TargetingStrategy.Specification is IsPlayerSpecification)
				continue;
			if (effect.ActionTemplate is null)
				continue;
			if (effect.ActionTemplate is ITargetedAction or EffectAction)
				continue;

			offenders.Add($"[{set}] {card.Name} -> {effect.ActionTemplate.GetType().Name}");
		}

		Assert.That(
			offenders.Distinct(),
			Is.Empty,
			"Targeted effects whose action cannot receive targets: "
				+ string.Join("; ", offenders.Distinct())
		);
	}

	/// <summary>
	/// An Aura with no legal creature to attach to still resolves onto the battlefield and then
	/// does nothing forever, with no way to attach it later.
	///
	/// Found in play: Aether Tunnel and Pacifism were both castable with no creature in sight,
	/// and just sat there.
	/// </summary>
	[Test]
	public void Auras_ChooseTheirTargetWhenCast()
	{
		var offenders = AllCards()
			.Where(x => x.Card.GetComponent<EquipmentComponent>()?.IsAura == true)
			.Where(x => x.Card.GetComponent<AuraTargetComponent>() == null)
			.Select(x => $"[{x.Set}] {x.Card.Name}")
			.ToList();

		Assert.That(
			offenders,
			Is.Empty,
			"Auras that do not target on cast can resolve with nothing to enchant: "
				+ string.Join(", ", offenders)
		);
	}

	/// <summary>
	/// A card whose only effect targets something that is almost never present is a blank card.
	///
	/// Found in play: Harbinger of the Tides never had a legal target, because creatures only
	/// become exhausted from a tapper — attacking does not exhaust in this engine, unlike the
	/// tapping that makes "target tapped creature" a live clause in real Magic.
	/// </summary>
	[Test]
	public void NoCardDependsSolelyOnAnExhaustedTarget()
	{
		// Deliberate payoffs for the tapper theme: removal that rewards setting a creature up,
		// dead without one BY DESIGN. Anything else wanting an exhausted target is an accident.
		var allowed = new[] { "Swift Response", "Gideon Jura" };

		var offenders = AllCards()
			.Where(x =>
				AllEffects(x.Card).Any(e => MentionsExhausted(e.TargetingStrategy.Specification))
			)
			.Select(x => x.Card.Name)
			.Distinct()
			.Except(allowed)
			.ToList();

		Assert.That(
			offenders,
			Is.Empty,
			"Unexpected cards gated on an exhausted target: " + string.Join(", ", offenders)
		);
	}

	/// <summary>
	/// A trigger that fires on a creature entering and RESPONDS by creating a creature produces
	/// the very event it listens for. Uncapped, that is not a slow card — it is an infinite loop:
	/// GameState.ProcessAllActions never returns, because the per-turn action limit lives in
	/// GameRunner rather than in the engine.
	///
	/// Found the hard way. Flameshadow Conjuring ("whenever a NONTOKEN creature enters" — and
	/// nothing here can express "nontoken") wedged training threads for over three hours, and
	/// would have frozen the Godot UI outright. A cap on either axis breaks the cycle.
	///
	/// The engine now also throws past GameState.MaxActionsPerResolution, so a future instance
	/// fails loudly rather than hanging — but this test is what stops it shipping at all.
	/// </summary>
	[Test]
	public void CreatureEtbTriggers_ThatMakeCreatures_AreCapped()
	{
		var offenders = new List<string>();

		foreach (var (set, card) in AllCards())
		foreach (var trigger in card.GetComponents<TriggeredAbilityComponent>())
		{
			if (
				trigger.Condition
				is not EventTriggerCondition
				{
					EventTypeName: EventTypeNames.CreatureEnteredBattlefield
				} etb
			)
				continue;

			// "When THIS enters" cannot feed itself: the filter matches only the source card, and
			// a token it creates is a different card. That covers the great majority of ETB
			// token-makers (Siege-Gang, Grave Titan, Captain of the Watch …), all of which are
			// perfectly safe. The loop needs a trigger that matches a creature OTHER than itself.
			if (etb.Filter is IsSourceCardSpecification)
				continue;

			if (trigger.MaxTriggersPerTurn > 0 || trigger.MaxTriggers > 0)
				continue;

			if (trigger.Effects.Any(e => MakesACreature(e.ActionTemplate)))
				offenders.Add($"[{set}] {card.Name} ({trigger.Name})");
		}

		Assert.That(
			offenders.Distinct(),
			Is.Empty,
			"Uncapped creature-ETB triggers that create creatures loop forever and hang the "
				+ "engine: "
				+ string.Join("; ", offenders.Distinct())
		);
	}

	/// <summary>
	/// A BUFF aimed at "a creature that attacked this turn" can never matter. Attacks resolve
	/// their damage immediately in this engine, so before combat the spell has no legal target at
	/// all, and after combat the buff lands on a creature that has already dealt its damage and
	/// cannot attack again. With no blocking it cannot matter defensively either — the card is
	/// blank in every window.
	///
	/// Found via the trained draft model, not by a test: Trumpet Blast measured -9.3pp, the worst
	/// red card in the set, because it genuinely did nothing.
	///
	/// The same specification is correct and deliberate for REMOVAL — Royal Assassin destroys what
	/// attacked you, on your turn — so this rule targets only the P/T-modifying case.
	/// </summary>
	[Test]
	public void BuffEffects_DoNotTargetCreaturesThatAlreadyAttacked()
	{
		var offenders = new List<string>();

		foreach (var (set, card) in AllCards())
		foreach (var effect in AllEffects(card))
		{
			if (effect.ActionTemplate is not AddModifierAction)
				continue;
			if (!MentionsAttacked(effect.TargetingStrategy.Specification))
				continue;

			offenders.Add($"[{set}] {card.Name}");
		}

		Assert.That(
			offenders.Distinct(),
			Is.Empty,
			"A buff on a creature that already attacked arrives after its damage and does "
				+ "nothing: "
				+ string.Join(", ", offenders.Distinct())
		);
	}

	private static bool MentionsAttacked(TargetSpecification? spec) =>
		spec switch
		{
			HasAttackedThisTurnSpecification => true,
			AndSpecification a => MentionsAttacked(a.Left) || MentionsAttacked(a.Right),
			OrSpecification o => MentionsAttacked(o.Left) || MentionsAttacked(o.Right),
			NotSpecification n => MentionsAttacked(n.Inner),
			_ => false,
		};

	/// <summary>Whether an action creates a creature, looking inside pipelines too.</summary>
	private static bool MakesACreature(GameAction? action) =>
		action switch
		{
			CreateCardAction c => c.CardTemplate?.HasComponent<CreatureComponent>() == true,
			PipelineAction p => p.Steps.Any(MakesACreature),
			_ => false,
		};

	private static IEnumerable<CardEffect> AllEffects(Card card)
	{
		var spell = card.GetComponent<SpellComponent>();
		if (spell != null)
			foreach (var e in spell.Effects)
				yield return e;

		foreach (var ability in card.GetComponents<ActivatedAbilityComponent>())
		foreach (var e in ability.Effects)
			yield return e;

		foreach (var trigger in card.GetComponents<TriggeredAbilityComponent>())
		foreach (var e in trigger.Effects)
			yield return e;
	}

	private static bool MentionsExhausted(TargetSpecification spec) =>
		spec switch
		{
			IsExhaustedSpecification => true,
			AndSpecification a => MentionsExhausted(a.Left) || MentionsExhausted(a.Right),
			OrSpecification o => MentionsExhausted(o.Left) || MentionsExhausted(o.Right),
			_ => false,
		};
}
