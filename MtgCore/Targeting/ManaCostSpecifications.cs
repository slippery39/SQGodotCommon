using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Matches a card whose ManaCost is at most Maximum — "mana value 3 or less" (Sun Titan,
/// Return to the Ranks).
///
/// Zone-agnostic on purpose: compose it with a zone spec, or hand it to
/// SelectCardFromZoneAction.Filter, which already scopes the search to one zone.
/// </summary>
public record HasManaCostAtMostSpecification : TargetSpecification
{
	public int Maximum { get; init; } = 3;

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		context.Find(candidateId) is Card card && card.ManaCost <= Maximum;
}

/// <summary>
/// Matches a creature carrying a PERMANENT power/toughness bonus.
///
/// This is how "if it had a +1/+1 counter on it" (Basri's Lieutenant) is asked. There is no
/// counter system by design — a permanent AddModifierAction IS this engine's +1/+1 counter — so
/// checking for a permanent bonus is the faithful question, not a workaround for a missing one.
///
/// UntilEndOfTurn modifiers are excluded: a combat trick is not a counter.
/// </summary>
public record HasPermanentPowerBonusSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (context.Find(candidateId) is not Card card)
			return false;

		foreach (var modifier in card.GetComponents<PowerToughnessModifier>())
		{
			if (modifier.Duration == ModifierDuration.UntilEndOfTurn)
				continue;

			if (
				modifier.GetPowerBonus(context.GameState, candidateId) > 0
				|| modifier.GetToughnessBonus(context.GameState, candidateId) > 0
			)
				return true;
		}

		return false;
	}
}

/// <summary>
/// Matches a creature whose EFFECTIVE power is at least Minimum — Intrepid Hero's
/// "target creature with power 4 or greater".
///
/// Effective, not base: a creature pumped to 4 power is a legal target, and one shrunk below 4
/// is not, which is what the card says.
/// </summary>
public record PowerAtLeastSpecification : TargetSpecification
{
	public int Minimum { get; init; } = 4;

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		context.Find(candidateId) is Card card
		&& card.HasComponent<CreatureComponent>()
		&& context.GameState.GetEffectivePower(candidateId) >= Minimum;
}

/// <summary>
/// Matches a creature whose effective power is less than that of the source card —
/// Lena's "creatures you control with power less than Lena's power".
///
/// Reads the SOURCE's power live rather than storing a number, so a pumped Lena protects more.
/// </summary>
public record PowerLessThanSourceSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (context.SourceCardId == 0)
			return false;

		var sourcePower = context.GameState.GetEffectivePower(context.SourceCardId);
		return context.GameState.GetEffectivePower(candidateId) < sourcePower;
	}
}
