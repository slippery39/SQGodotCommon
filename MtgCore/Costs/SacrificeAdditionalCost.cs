using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Additional cost: sacrifice one or more permanents you control.
/// Requires player selection — GetValidPayments returns IDs of sacrificeable permanents.
///
/// Filter restricts which permanents qualify (e.g. IsSubtypeSpecification("Goblin")).
/// Null filter means any permanent you control on the battlefield is valid.
///
/// Sacrifice moves the permanent directly to its owner's graveyard.
/// This is not "destroy" — indestructible does not prevent it.
/// </summary>
public record SacrificeAdditionalCost : AdditionalCost
{
	public TargetSpecification? Filter { get; init; }
	public int Count { get; init; } = 1;

	public override bool RequiresSelection => true;

	public override ImmutableList<int> GetValidPayments(
		GameState state,
		int castingPlayerId,
		int sourceCardId
	)
	{
		var battlefieldId = state.GetPlayerZoneId(castingPlayerId, ZoneType.Battlefield);
		var context = new TargetingContext
		{
			GameState = state,
			CastingPlayerId = castingPlayerId,
			SourceCardId = sourceCardId,
		};

		return state
			.GetCardsInZone(battlefieldId)
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
			return ValidationResult.Invalid($"Must sacrifice exactly {Count} permanent(s)");

		var battlefieldId = state.GetPlayerZoneId(castingPlayerId, ZoneType.Battlefield);
		var context = new TargetingContext
		{
			GameState = state,
			CastingPlayerId = castingPlayerId,
			SourceCardId = sourceCardId,
		};

		foreach (var id in paymentIds)
		{
			if (!state.HasObject(id))
				return ValidationResult.Invalid($"Sacrifice target {id} does not exist");

			if (state.GetObject(id) is not Card)
				return ValidationResult.Invalid($"Sacrifice target {id} is not a card");

			if (state.GetCardZoneId(id) != battlefieldId)
				return ValidationResult.Invalid(
					$"Sacrifice target {id} is not on your battlefield"
				);

			if (Filter != null && !Filter.IsSatisfiedBy(id, context))
				return ValidationResult.Invalid(
					$"Sacrifice target {id} does not meet requirements"
				);
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
