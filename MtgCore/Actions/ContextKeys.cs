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

	/// <summary>Injected by ResolveEffectAction so ITargetedActions can identify their source card.</summary>
	public const string SourceCardId = "source_card_id";

	// ===== SHARED EFFECT KEYS =====
	/// <summary>ID of a card revealed from the top of a library.</summary>
	public const string RevealedCardId = "revealed_card_id";

	/// <summary>Mana cost of the most recently revealed card.</summary>
	public const string RevealedCardManaCost = "revealed_card_mana_cost";

	/// <summary>List of card IDs selected by the player (e.g. for discard).</summary>
	public const string SelectedCardIds = "selected_card_ids";

	/// <summary>List of card IDs from the top N cards of a library.</summary>
	public const string TopCardIds = "top_card_ids";

	/// <summary>
	/// The X chosen when casting an {X} spell. Injected by ResolveSpellAction so the effect can
	/// scale with it — an X spell's effect is meaningless without this.
	/// </summary>
	public const string XValue = "x_value";

	/// <summary>List of remaining card IDs after selections have been excluded.</summary>
	public const string RemainingCardIds = "remaining_card_ids";

	/// <summary>
	/// The numeric payload of the event that fired a triggered ability — "whenever you lose
	/// life, draw THAT MANY cards" (Vilis). Injected by ResolveEffectAction from the triggering
	/// event; 0 for events that carry no amount.
	///
	/// Read it with EffectAction.AmountContextKey. Without it a trigger can only ever act on a
	/// number baked into the card, which is why every "that many" clause in the engine had
	/// previously been flattened to a constant.
	/// </summary>
	public const string TriggerAmount = "trigger_amount";

	/// <summary>
	/// The id of the card or player the triggering event was ABOUT — "whenever a creature
	/// attacks this, tap THAT CREATURE" (Wall of Frost). Injected by ResolveEffectAction from
	/// the same extraction the trigger's Filter runs against.
	///
	/// Without it a trigger can only act on a target chosen by a targeting strategy, which
	/// cannot see the event at all: Wall of Frost had to freeze every creature the opponent
	/// controlled because it had no way to name the one that attacked it.
	/// </summary>
	public const string TriggerSubjectId = "trigger_subject_id";
}
