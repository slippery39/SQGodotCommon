namespace MtgCore;

/// <summary>
/// Constants for pipeline context keys.
/// Using constants prevents silent failures from typos when
/// passing data between pipeline steps.
/// </summary>
public static class ContextKeys
{
	public const string RevealedCardId = "revealed_card_id";
	public const string RevealedCardManaCost = "revealed_card_mana_cost";
}
