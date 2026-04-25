using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Fixed-value P/T modifier. Used by spells like Giant Growth (UntilEndOfTurn)
/// and permanent enchantments like Unholy Strength (Permanent).
/// </summary>
public record StaticPowerToughnessModifier : PowerToughnessModifier
{
	public int PowerBonus { get; init; } = 0;
	public int ToughnessBonus { get; init; } = 0;

	public override int GetPowerBonus(GameState state, int cardId) => PowerBonus;

	public override int GetToughnessBonus(GameState state, int cardId) => ToughnessBonus;
}
