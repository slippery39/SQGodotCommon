using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Puts a land card into play from any zone (typically Library) as part of a spell effect.
/// Used by Rampant Growth, Primeval Titan's ETB, and similar effects.
///
/// Unlike PlayLandAction, this does NOT count against the player's land-per-turn limit
/// and does NOT require the card to be in hand. It does count toward LandsPlayedTotal
/// for Terravore's dynamic P/T.
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

		var card = state.GetObject(cardId) as Card;

		var bonus = card?.GetComponent<BonusManaLandComponent>();
		var totalMana = 1 + (bonus?.ExtraMana ?? 0);
		var manaThisTurn = bonus?.Deferred == true ? 0 : totalMana;

		var player = state.GetPlayer(playerId);
		state = state.UpdateObject(
			playerId,
			player with
			{
				MaxMana = player.MaxMana + totalMana,
				CurrentMana = player.CurrentMana + manaThisTurn,
				LandsPlayedTotal = player.LandsPlayedTotal + 1,
			}
		);

		var grantEmblem = card?.GetComponent<GrantEmblemComponent>();
		if (grantEmblem != null)
		{
			var updatedPlayer = state.GetPlayer(playerId);
			state = state.UpdateObject(
				playerId,
				updatedPlayer with
				{
					Emblems = updatedPlayer.Emblems.Add(grantEmblem.Emblem),
				}
			);
		}

		var exileId = state.GetPlayerZoneId(playerId, ZoneType.Exile);
		state = state.MoveObject(cardId, exileId);

		var landPlayedEvent = new LandPlayedEvent { PlayerId = playerId, CardId = cardId };
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(landPlayedEvent) };

		var landEffect = card?.GetComponent<LandPlayEffectComponent>();
		if (landEffect != null)
			state = state.SpawnAction(
				new ResolveEffectAction
				{
					Effects = [landEffect.Effect],
					CastingPlayerId = playerId,
					SourceCardId = cardId,
				}
			);

		return new ActionResult(state).WithEvent(landPlayedEvent);
	}
}
