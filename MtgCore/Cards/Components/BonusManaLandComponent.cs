using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Overrides the default mana a land card produces when played.
/// PlayLandAction and PutLandIntoPlayAction add (1 + ExtraMana) to MaxMana.
/// When Deferred is true only MaxMana is incremented; CurrentMana is unchanged so the
/// mana cannot be spent this turn (StartTurnAction refills CurrentMana = MaxMana next turn).
/// Used by Bounceland: ExtraMana=1, Deferred=true gives +2 MaxMana next turn instead of +1 now.
/// </summary>
public record BonusManaLandComponent : GameComponent
{
	public int ExtraMana { get; init; } = 0;
	public bool Deferred { get; init; } = false;
}
