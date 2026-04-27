using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Additional cost: pay N life.
/// Does not require selection — validates and pays directly against player's Life total.
/// Allows paying life that would reduce you to 0 or below; state-based effects handle death.
/// </summary>
public record LifeAdditionalCost : AdditionalCost
{
	public int Amount { get; init; }

	public override bool RequiresSelection => false;

	public override ImmutableList<int> GetValidPayments(
		GameState state,
		int castingPlayerId,
		int sourceCardId
	) => ImmutableList<int>.Empty;

	public override ValidationResult Validate(
		GameState state,
		int castingPlayerId,
		int sourceCardId,
		ImmutableList<int> paymentIds
	)
	{
		var player = state.GetPlayer(castingPlayerId);
		if (player.Life < Amount)
			return ValidationResult.Invalid(
				$"Not enough life to pay {Amount} life (have {player.Life})"
			);
		return ValidationResult.Valid;
	}

	public override GameState Pay(
		GameState state,
		int castingPlayerId,
		int sourceCardId,
		ImmutableList<int> paymentIds
	)
	{
		var player = state.GetPlayer(castingPlayerId);
		return state.UpdateObject(castingPlayerId, player with { Life = player.Life - Amount });
	}
}
