using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// P/T modifier stamped onto a creature when equipment attaches to it.
/// SourceCardId identifies the equipment card so the boost can be removed
/// when the equipment detaches or moves to a different creature.
///
/// Extends PowerToughnessModifier so CreatureEvaluator picks it up in its
/// normal component scan — no special-casing required.
///
/// Managed exclusively by AttachEquipmentAction and CheckStateBasedEffectsAction.
/// </summary>
public record EquippedBoostComponent : PowerToughnessModifier
{
	public int PowerBonus { get; init; }
	public int ToughnessBonus { get; init; }

	// Keyword grants from an attachment (Angelic Destiny's flying and first strike, Spirit
	// Mantle's protection). Read by CreatureEvaluator in its own pass rather than reusing
	// AppliedKeywordComponent, because Permanent-duration AppliedKeywordComponents are owned
	// exclusively by StaticAbilityEngine — stamping one here would have the engine strip it.
	public bool GrantsFlying { get; init; }
	public bool GrantsFirstStrike { get; init; }
	public bool GrantsDoubleStrike { get; init; }
	public bool GrantsLifelink { get; init; }
	public bool GrantsTrample { get; init; }
	public bool GrantsHaste { get; init; }
	public bool GrantsTaunt { get; init; }
	public bool GrantsReach { get; init; }
	public bool GrantsIndestructible { get; init; }
	public bool GrantsHexproof { get; init; }
	public bool GrantsShroud { get; init; }
	public bool GrantsDeathtouch { get; init; }

	/// <summary>
	/// "Enchanted creature can't attack" — Pacifism, Faith's Fetters.
	///
	/// Not modelled as permanent exhaustion: IsExhausted is cleared every turn by
	/// StartTurnAction, so a Pacifism built on it would wear off after one turn.
	/// </summary>
	public bool PreventsAttacking { get; init; }

	public override int GetPowerBonus(GameState state, int cardId) => PowerBonus;

	public override int GetToughnessBonus(GameState state, int cardId) => ToughnessBonus;
}
