namespace MtgCore;

/// <summary>
/// Named constants for pipeline context keys.
/// Using constants prevents silent failures from typos in string keys.
/// </summary>
public static class ContextKeys
{
	public const string RevealedCardId = "revealed_card_id";
	public const string RevealedCardManaCost = "revealed_card_mana_cost";
	public const string SelectedCardIds = "selected_card_ids";
	public const string CastingPlayerId = "casting_player_id";
}
