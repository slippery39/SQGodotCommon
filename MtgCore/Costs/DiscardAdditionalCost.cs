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

	/// <summary>
	/// Restricts which cards may be discarded — "discard a land card" (Magmatic Insight, Molten
	/// Vortex). Null means any card in hand. Mirrors SacrificeAdditionalCost.Filter.
	///
	/// A land is a real card in hand here (it is only consumed into MaxMana when played), so this
	/// cost is faithful rather than reskinned, and giving up a land drop is a genuine price.
	/// </summary>
	public TargetSpecification? Filter { get; init; }

	/// <summary>Player-facing name for the restriction, used by Describe().</summary>
	public string FilterDescription { get; init; } = "";

	public override bool RequiresSelection => true;

	public override int RequiredPaymentCount => Count;

	public override string Describe()
	{
		var what = string.IsNullOrEmpty(FilterDescription) ? "card" : FilterDescription;
		return Count == 1
			? $"Discard a {what} from your hand"
			: $"Discard {Count} {what}s from your hand";
	}

	public override ImmutableList<int> GetValidPayments(
		GameState state,
		int castingPlayerId,
		int sourceCardId
	)
	{
		var handId = state.GetPlayerZoneId(castingPlayerId, ZoneType.Hand);
		var context = new TargetingContext
		{
			GameState = state,
			CastingPlayerId = castingPlayerId,
			SourceCardId = sourceCardId,
		};

		return state
			.GetCardsInZone(handId)
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
			return ValidationResult.Invalid($"Must discard exactly {Count} card(s)");

		var handId = state.GetPlayerZoneId(castingPlayerId, ZoneType.Hand);
		var context = new TargetingContext
		{
			GameState = state,
			CastingPlayerId = castingPlayerId,
			SourceCardId = sourceCardId,
		};

		foreach (var id in paymentIds)
		{
			if (!state.HasObject(id))
				return ValidationResult.Invalid($"Discard target {id} does not exist");

			if (state.GetCardZoneId(id) != handId)
				return ValidationResult.Invalid($"Card {id} is not in your hand");

			// Validate must enforce the filter too, not just GetValidPayments. The generator
			// picks payments from the latter, but a hand-built action (the human UI path) reaches
			// Validate directly — enforcing in only one of the two is how "the AI can do it and I
			// can't" bugs happen in reverse.
			if (Filter != null && !Filter.IsSatisfiedBy(id, context))
				return ValidationResult.Invalid($"Card {id} cannot pay this cost");
		}

		return ValidationResult.Valid;
	}

	/// <remarks>
	/// Routes through MoveCardTracked and stages CardDiscardedEvent, exactly as
	/// DiscardCardsAction does. A plain MoveObject here left zone-dependent statics stale and
	/// fired no discard trigger, so paying this cost was invisible to every discard payoff.
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
			var card = (Card)state.GetObject(id);
			var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
			state = state.MoveCardTracked(id, graveyardId);
			state = state with
			{
				PendingGameEvents = state.PendingGameEvents.Add(
					new CardDiscardedEvent { PlayerId = card.OwnerId, CardId = id }
				),
			};
		}
		return state;
	}
}
