using System.Collections.Immutable;
using System.Linq;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Plays a land card from a player's hand.
///
/// Unlike spells, lands do not use the MTG stack — they resolve immediately with
/// no window for opponents to respond. The card moves directly from hand to exile
/// (lands do not stay in play; they add permanent mana to the pool instead).
///
/// ValidateAdd confirms the card is a Land in the player's hand and that the player
/// has not yet used their land play for this turn (Exploration and similar artifacts
/// allow additional land plays by contributing ExtraLandPerTurnComponent).
///
/// Execute: increments MaxMana and CurrentMana by 1, increments LandsPlayedThisTurn
/// and LandsPlayedTotal, moves card to exile, emits LandPlayedEvent.
/// </summary>
public record PlayLandAction : GameAction
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }

	public override ValidationResult ValidateAdd(GameState gameState)
	{
		if (!gameState.HasObject(CardId))
			return ValidationResult.Invalid($"Card {CardId} does not exist");

		var card = gameState.GetObject(CardId) as Card;
		if (card == null)
			return ValidationResult.Invalid($"Object {CardId} is not a card");

		if (card.ControllerId != CastingPlayerId)
			return ValidationResult.Invalid("You do not control this card");

		var handId = gameState.GetPlayerZoneId(CastingPlayerId, ZoneType.Hand);
		if (gameState.GetCardZoneId(CardId) != handId)
			return ValidationResult.Invalid("Card is not in your hand");

		if (!card.HasSubtype("Land"))
			return ValidationResult.Invalid("Card is not a land");

		var player = gameState.GetPlayer(CastingPlayerId);
		var landsAllowed = ComputeLandsAllowed(gameState, CastingPlayerId);
		if (player.LandsPlayedThisTurn >= landsAllowed)
			return ValidationResult.Invalid("You have already played your land this turn");

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		var card = state.GetObject(CardId) as Card;

		var bonus = card?.GetComponent<BonusManaLandComponent>();
		var totalMana = 1 + (bonus?.ExtraMana ?? 0);
		var manaThisTurn = bonus?.Deferred == true ? 0 : totalMana;

		var player = state.GetPlayer(CastingPlayerId);
		state = state.UpdateObject(
			CastingPlayerId,
			player with
			{
				MaxMana = player.MaxMana + totalMana,
				CurrentMana = player.CurrentMana + manaThisTurn,
				LandsPlayedThisTurn = player.LandsPlayedThisTurn + 1,
				LandsPlayedTotal = player.LandsPlayedTotal + 1,
			}
		);

		var grantEmblem = card?.GetComponent<GrantEmblemComponent>();
		if (grantEmblem != null)
		{
			var updatedPlayer = state.GetPlayer(CastingPlayerId);
			state = state.UpdateObject(
				CastingPlayerId,
				updatedPlayer with
				{
					Emblems = updatedPlayer.Emblems.Add(grantEmblem.Emblem),
				}
			);
		}

		var exileId = state.GetPlayerZoneId(CastingPlayerId, ZoneType.Exile);
		state = state.MoveObject(CardId, exileId);

		var landPlayedEvent = new LandPlayedEvent { PlayerId = CastingPlayerId, CardId = CardId };
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(landPlayedEvent) };

		var landEffect = card?.GetComponent<LandPlayEffectComponent>();
		if (landEffect != null)
			state = state.SpawnAction(
				new ResolveEffectAction
				{
					Effects = [landEffect.Effect],
					CastingPlayerId = CastingPlayerId,
					SourceCardId = CardId,
				}
			);

		return new ActionResult(state).WithEvent(landPlayedEvent);
	}

	/// <summary>
	/// Returns how many lands the player may play this turn:
	/// 1 (base) + count of ExtraLandPerTurnComponent permanents they control.
	/// </summary>
	public static int ComputeLandsAllowed(GameState state, int playerId)
	{
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		var extra = state
			.GetCardsInZone(battlefieldId)
			.Count(c => c.HasComponent<ExtraLandPerTurnComponent>());
		return 1 + extra;
	}
}
