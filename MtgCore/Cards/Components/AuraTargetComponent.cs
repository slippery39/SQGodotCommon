using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// What an Aura may enchant, chosen when the Aura is CAST rather than when it resolves.
///
/// Auras originally attached through an ETB trigger, on the reasoning that nothing can respond
/// between cast and resolution so the two are observationally identical. Playtesting proved that
/// wrong for a reason that has nothing to do with timing: an ETB trigger cannot make the spell
/// ILLEGAL. Pacifism and Aether Tunnel could both be cast with no creature on the board at all,
/// resolved onto the battlefield, found nothing to attach to, and then sat there permanently
/// inert with no way to attach them later.
///
/// Targeting on cast fixes three things at once:
///   - the spell is uncastable with no legal target, as a real Aura is;
///   - the human gets the normal targeting flow, because the UI already knows how to prompt for
///     a cast-time target — it was the ETB trigger that had no prompt;
///   - the AI enumerates one action per legal target, so it actually chooses what to enchant.
///
/// CastPermanentAction validates and carries the target; ResolvePermanentAction performs the
/// attach. The Aura no longer needs an ETB trigger at all.
/// </summary>
public record AuraTargetComponent : GameComponent
{
	public TargetingStrategy Targeting { get; init; } =
		TargetingStrategy.SingleTarget(TargetSpecification.Creatures());
}
