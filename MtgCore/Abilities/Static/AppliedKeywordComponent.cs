using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Keyword grants stamped onto a permanent by a static ability source.
/// Identified by SourceCardId for cleanup when the source leaves the battlefield.
/// Managed exclusively by StaticAbilityEngine — do not add or remove manually.
/// </summary>
public record AppliedKeywordComponent : GameComponent
{
	public int SourceCardId { get; init; }
	public bool GrantsHaste { get; init; } = false;
	public bool GrantsFlying { get; init; } = false;
	public bool GrantsTaunt { get; init; } = false;
	public bool GrantsReach { get; init; } = false;
	public bool GrantsLifelink { get; init; } = false;
	public bool GrantsTrample { get; init; } = false;
	public bool GrantsShroud { get; init; } = false;
	public bool GrantsHexproof { get; init; } = false;
}
