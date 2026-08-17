using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// The Core Set Cube's counterpart to HollowmereCardBugTests.
///
/// Every rule here was written after a card was found to silently do nothing in play. A card that
/// builds, casts and resolves without throwing can still be completely inert, and none of the
/// other fixtures catch that — CoresetCubeWhiteTests only asserts a card reaches the battlefield.
/// </summary>
[TestFixture]
public class CoresetCubeCardBugTests
{
	/// <summary>
	/// Triggered abilities spawn ResolveEffectAction with NO TargetIds, so a user-select
	/// targeting strategy inside a trigger resolves to an empty target list and the effect
	/// silently does nothing.
	///
	/// Found in play: Pegasus Courser's attack trigger did nothing at all.
	///
	/// Triggers must use AllValid, Random, Self, or a context key.
	/// </summary>
	[Test]
	public void TriggeredAbilities_DoNotUseUserSelectTargeting()
	{
		var offenders = new List<string>();
		foreach (var card in CoresetCube.Cards)
		foreach (var trigger in card.GetComponents<TriggeredAbilityComponent>())
		foreach (var effect in trigger.Effects)
		{
			if (effect.TargetingStrategy.RequiresUserSelection)
				offenders.Add($"{card.Name} ({trigger.Name})");
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

		foreach (var card in CoresetCube.Cards)
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

			offenders.Add($"{card.Name} -> {effect.ActionTemplate.GetType().Name}");
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
		var offenders = CoresetCube
			.Cards.Where(c => c.GetComponent<EquipmentComponent>()?.IsAura == true)
			.Where(c => c.GetComponent<AuraTargetComponent>() == null)
			.Select(c => c.Name)
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
		var offenders = new List<string>();

		foreach (var card in CoresetCube.Cards)
		foreach (var effect in AllEffects(card))
		{
			if (MentionsExhausted(effect.TargetingStrategy.Specification))
				offenders.Add(card.Name);
		}

		// Swift Response and Gideon Jura's -2 are DELIBERATE payoffs for the tapper theme — they
		// are removal that rewards setting a creature up, and are dead without one by design.
		// Anything else wanting an exhausted target is an accident.
		Assert.That(
			offenders.Distinct(),
			Is.EquivalentTo(new[] { "Swift Response", "Gideon Jura" }),
			"Unexpected cards gated on an exhausted target: "
				+ string.Join(", ", offenders.Distinct())
		);
	}

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
