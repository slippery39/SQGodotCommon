using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// P/T modifier stamped onto a permanent by a static ability source (lord/anthem effects).
/// Distinct from StaticPowerToughnessModifier (used by spells) so it can be identified
/// and removed by SourceCardId when the source leaves the battlefield.
/// Managed exclusively by StaticAbilityEngine — do not add or remove manually.
/// </summary>
public record AppliedStaticPTBoost : PowerToughnessModifier
{
	public int PowerBonus { get; init; } = 0;
	public int ToughnessBonus { get; init; } = 0;

	public override int GetPowerBonus(GameState state, int cardId) => PowerBonus;

	public override int GetToughnessBonus(GameState state, int cardId) => ToughnessBonus;
}
