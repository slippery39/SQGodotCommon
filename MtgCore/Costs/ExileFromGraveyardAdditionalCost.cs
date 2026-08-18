using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Additional cost: exile one or more cards from your own graveyard — Despoiler of Souls'
/// "exile two other creature cards from your graveyard".
///
/// The source card is always excluded. Despoiler pays this cost to return ITSELF from the
/// graveyard, so without that exclusion it could exile itself to pay for its own recursion.
///
/// Filter narrows which cards qualify (a creature card, an instant or sorcery). Null means any
/// card in your graveyard.
///
/// This is the cost that makes a repeatable graveyard recursion self-limiting: it consumes the
/// resource it comes back from, so the loop has a hard floor and graveyard hate is live against
/// it. A recursion bounded only by mana just returns every turn forever.
/// </summary>
public record ExileFromGraveyardAdditionalCost : AdditionalCost
{
	public TargetSpecification? Filter { get; init; }
	public int Count { get; init; } = 1;

	public override bool RequiresSelection => true;

	public override int RequiredPaymentCount => Count;

	public override string Describe() =>
		Count == 1
			? "Exile a card from your graveyard"
			: $"Exile {Count} cards from your graveyard";

	public override ImmutableList<int> GetValidPayments(
		GameState state,
		int castingPlayerId,
		int sourceCardId
	)
	{
		var graveyardId = state.GetPlayerZoneId(castingPlayerId, ZoneType.Graveyard);
		var context = new TargetingContext
		{
			GameState = state,
			CastingPlayerId = castingPlayerId,
			SourceCardId = sourceCardId,
		};

		return state
			.GetCardsInZone(graveyardId)
			.Where(c => c.Id != sourceCardId)
			.Where(c => Filter == null || Filter.IsSatisfiedBy(c.Id, context))
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
			return ValidationResult.Invalid($"Must exile exactly {Count} card(s)");

		var graveyardId = state.GetPlayerZoneId(castingPlayerId, ZoneType.Graveyard);
		var context = new TargetingContext
		{
			GameState = state,
			CastingPlayerId = castingPlayerId,
			SourceCardId = sourceCardId,
		};

		foreach (var id in paymentIds)
		{
			if (!state.HasObject(id))
				return ValidationResult.Invalid($"Exile target {id} does not exist");

			if (id == sourceCardId)
				return ValidationResult.Invalid("A card cannot be exiled to pay for itself");

			if (state.GetCardZoneId(id) != graveyardId)
				return ValidationResult.Invalid($"Card {id} is not in your graveyard");

			if (Filter != null && !Filter.IsSatisfiedBy(id, context))
				return ValidationResult.Invalid($"Card {id} does not meet requirements");
		}

		return ValidationResult.Valid;
	}

	/// <remarks>
	/// MoveCardTracked, not MoveObject: leaving the graveyard emits CardLeftGraveyardEvent, which
	/// is what unregisters any graveyard-active static the exiled card had.
	/// </remarks>
	public override GameState Pay(
		GameState state,
		int castingPlayerId,
		int sourceCardId,
		ImmutableList<int> paymentIds
	)
	{
		foreach (var id in paymentIds)
		{
			if (state.GetObject(id) is not Card card)
				continue;
			var exileId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Exile);
			state = state.MoveCardTracked(id, exileId);
		}
		return state;
	}
}
