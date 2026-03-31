namespace MtgCore;

/// <summary>
/// Named constants for pipeline context keys shared across multiple cards or actions.
/// Card-specific intermediate keys should be defined as inline strings within
/// the card definition itself rather than added here.
/// </summary>
public static class ContextKeys
{
	// ===== SYSTEM KEYS =====
	/// <summary>Injected by ResolveSpellAction into pipeline context for self-targeting effects.</summary>
	public const string CastingPlayerId = "casting_player_id";

	// ===== SHARED EFFECT KEYS =====
	/// <summary>ID of a card revealed from the top of a library.</summary>
	public const string RevealedCardId = "revealed_card_id";

	/// <summary>Mana cost of the most recently revealed card.</summary>
	public const string RevealedCardManaCost = "revealed_card_mana_cost";

	/// <summary>List of card IDs selected by the player (e.g. for discard).</summary>
	public const string SelectedCardIds = "selected_card_ids";

	/// <summary>List of card IDs from the top N cards of a library.</summary>
	public const string TopCardIds = "top_card_ids";

	/// <summary>List of remaining card IDs after selections have been excluded.</summary>
	public const string RemainingCardIds = "remaining_card_ids";
}
