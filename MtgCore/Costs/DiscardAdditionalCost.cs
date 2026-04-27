using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Additional cost: discard one or more cards from your hand.
/// Requires player selection — GetValidPayments returns IDs of cards in hand,
/// excluding the source card itself (which hasn't been cast yet but is still in hand).
///
/// Discarded cards move to their owner's graveyard.
/// </summary>
public record DiscardAdditionalCost : AdditionalCost
{
	public int Count { get; init; } = 1;

	public override bool RequiresSelection => true;

	public override ImmutableList<int> GetValidPayments(
		GameState state,
		int castingPlayerId,
		int sourceCardId
	)
	{
		var handId = state.GetPlayerZoneId(castingPlayerId, ZoneType.Hand);
		return state
			.GetCardsInZone(handId)
			.Where(c => c.Id != sourceCardId)
			.Select(c => c.Id)
			.ToImmutableList();
	}

	public override ValidationResult Validate(
		GameState state,
		int castingPlayerId,
		int sourceCardId,
		ImmutableList<int> paymentIds
	)
	{
		if (paymentIds.Count != Count)
			return ValidationResult.Invalid($"Must discard exactly {Count} card(s)");

		var handId = state.GetPlayerZoneId(castingPlayerId, ZoneType.Hand);

		foreach (var id in paymentIds)
		{
			if (!state.HasObject(id))
				return ValidationResult.Invalid($"Discard target {id} does not exist");

			if (state.GetCardZoneId(id) != handId)
				return ValidationResult.Invalid($"Card {id} is not in your hand");
		}

		return ValidationResult.Valid;
	}

	public override GameState Pay(
		GameState state,
		int castingPlayerId,
		int sourceCardId,
		ImmutableList<int> paymentIds
	)
	{
		foreach (var id in paymentIds)
		{
			var card = (Card)state.GetObject(id);
			var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
			state = state.MoveObject(id, graveyardId);
		}
		return state;
	}
}
