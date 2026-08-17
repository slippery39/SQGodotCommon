using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Keyword grants stamped onto a permanent by a static ability source.
/// Identified by SourceCardId for cleanup when the source leaves the battlefield.
///
/// Permanent grants (the default) are managed exclusively by StaticAbilityEngine —
/// do not add or remove those manually. UntilEndOfTurn grants are the exception:
/// GrantKeywordAction stamps them and StartTurnAction clears them, mirroring how
/// PowerToughnessModifier handles temporary P/T buffs. StaticAbilityEngine ignores
/// UntilEndOfTurn grants entirely, so the two lifetimes never fight over the same component.
/// </summary>
public record AppliedKeywordComponent : GameComponent
{
	public int SourceCardId { get; init; }
	public ModifierDuration Duration { get; init; } = ModifierDuration.Permanent;
	public bool GrantsHaste { get; init; } = false;
	public bool GrantsFlying { get; init; } = false;
	public bool GrantsTaunt { get; init; } = false;
	public bool GrantsReach { get; init; } = false;
	public bool GrantsLifelink { get; init; } = false;
	public bool GrantsTrample { get; init; } = false;
	public bool GrantsShroud { get; init; } = false;
	public bool GrantsHexproof { get; init; } = false;
	public bool GrantsDeathtouch { get; init; } = false;
	public bool GrantsFirstStrike { get; init; } = false;
	public bool GrantsDoubleStrike { get; init; } = false;
	public bool GrantsIndestructible { get; init; } = false;

	/// <summary>
	/// Exalted is counted rather than tested — see StaticGrantKeywordAbility.GrantsExalted.
	/// </summary>
	public bool GrantsExalted { get; init; } = false;
}
