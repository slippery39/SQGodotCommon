using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Dynamic P/T modifier that scales with the number of cards in the controller's graveyard.
/// Used by Tarmogoyf: base Power = 0, base Toughness = 1, so effective values become
/// (graveyard count) / (graveyard count + 1) at read time.
///
/// TWO FIELDS RATHER THAN A SECOND TYPE, added for the Core Set Cube's Enigma Drake ("power is
/// equal to the number of instant and sorcery cards in your graveyard", a */4). Both default to
/// the original behaviour, so Tarmogoyf is untouched:
///   - Types            null counts every card; a flag set counts only matching cards.
///   - AffectsToughness false makes this a +X/+0, which is what a */N creature needs.
///
/// Card.EffectiveTypes reports Instant|Sorcery for a spell built through CardFactory.Spell, which
/// declares no type — it cannot tell the two apart. That is exactly the union these cards count,
/// so the derivation fallback is correct here rather than merely tolerable.
/// </summary>
public record GraveyardCountComponent : PowerToughnessModifier
{
	/// <summary>Card types to count. Null counts every card in the graveyard.</summary>
	public CardType? Types { get; init; } = null;

	/// <summary>False makes this a +X/+0 — the */N templating. True is Tarmogoyf's X/X+1.</summary>
	public bool AffectsToughness { get; init; } = true;

	public override int GetPowerBonus(GameState state, int cardId) =>
		CountControllerGraveyardCards(state, cardId);

	public override int GetToughnessBonus(GameState state, int cardId) =>
		AffectsToughness ? CountControllerGraveyardCards(state, cardId) : 0;

	private int CountControllerGraveyardCards(GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return 0;
		var graveyardId = state.GetPlayerZoneId(card.ControllerId, ZoneType.Graveyard);

		if (Types == null)
			return state.GetChildrenIds(graveyardId).Count();

		return state.GetCardsInZone(graveyardId).Count(c => c.HasType(Types.Value));
	}
}
