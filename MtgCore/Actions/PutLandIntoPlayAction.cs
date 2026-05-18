using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Puts a land card into play from any zone (typically Library) as part of a spell effect.
/// Used by Rampant Growth, Primeval Titan's ETB, and similar effects.
///
/// Unlike PlayLandAction, this does NOT count against the player's land-per-turn limit
/// and does NOT require the card to be in hand. It does count toward LandsPlayedTotal
/// for Land Elemental's dynamic P/T.
///
/// Takes the card ID from pipeline context via CardIdContextKey (or directly from CardId).
/// Takes the player ID from pipeline context via PlayerIdContextKey (or directly from PlayerId).
/// If CardId resolves to 0, the action is a no-op (library had no matching land).
///
/// Emits LandPlayedEvent so landfall triggers (Steppe Lynx, Courser of Kruphix) fire.
/// </summary>
public record PutLandIntoPlayAction : GameAction
{
	public int CardId { get; init; } = 0;
	public int PlayerId { get; init; } = 0;
	public string CardIdContextKey { get; init; } = "";
	public string PlayerIdContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		var cardId = string.IsNullOrEmpty(CardIdContextKey)
			? CardId
			: GetInput<int>(CardIdContextKey, CardId);

		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, PlayerId);

		if (cardId == 0 || playerId == 0)
			return new ActionResult(state);

		if (!state.HasObject(cardId))
			return new ActionResult(state);

		var player = state.GetPlayer(playerId);
		state = state.UpdateObject(
			playerId,
			player with
			{
				MaxMana = player.MaxMana + 1,
				CurrentMana = player.CurrentMana + 1,
				LandsPlayedTotal = player.LandsPlayedTotal + 1,
			}
		);

		var exileId = state.GetPlayerZoneId(playerId, ZoneType.Exile);
		state = state.MoveObject(cardId, exileId);

		var landPlayedEvent = new LandPlayedEvent { PlayerId = playerId, CardId = cardId };
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(landPlayedEvent) };

		return new ActionResult(state).WithEvent(landPlayedEvent);
	}
}
