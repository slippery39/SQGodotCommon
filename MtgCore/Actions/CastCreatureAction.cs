using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Casts a creature card from a player's hand onto the stack.
///
/// ValidateAdd confirms the card exists, is in the player's hand,
/// is controlled by the casting player, has a CreatureComponent,
/// and that the player has enough mana.
///
/// Execute spends mana, moves the card to the stack, emits CreaturePlayedEvent,
/// and spawns ResolveCreatureAction which puts it onto the battlefield.
/// </summary>
public record CastCreatureAction : GameAction
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }

	/// <summary>
	/// Payment selections for selection-based additional costs (sacrifice, discard).
	/// Key = index into card.AdditionalCastCosts. Resource costs (life) need no entry here.
	/// </summary>
	public ImmutableDictionary<int, ImmutableList<int>> AdditionalCostPayments { get; init; } =
		ImmutableDictionary<int, ImmutableList<int>>.Empty;

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

		if (!card.HasComponent<CreatureComponent>())
			return ValidationResult.Invalid("Card is not a creature");

		var player = gameState.GetPlayer(CastingPlayerId);
		if (player.CurrentMana < card.ManaCost)
			return ValidationResult.Invalid(
				$"Not enough mana (have {player.CurrentMana}, need {card.ManaCost})"
			);

		for (int i = 0; i < card.AdditionalCastCosts.Count; i++)
		{
			var cost = card.AdditionalCastCosts[i];
			var paymentIds =
				cost.RequiresSelection && AdditionalCostPayments.TryGetValue(i, out var ids)
					? ids
					: [];
			var result = cost.Validate(gameState, CastingPlayerId, CardId, paymentIds);
			if (!result.IsValid)
				return result;
		}

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState gameState)
	{
		var card = (Card)gameState.GetObject(CardId);
		var state = PayAdditionalCosts(gameState, card);

		var player = state.GetPlayer(CastingPlayerId);
		state = state.UpdateObject(
			CastingPlayerId,
			player with
			{
				CurrentMana = player.CurrentMana - card.ManaCost,
			}
		);
		state = state.MoveObject(CardId, state.GetStackId());

		var playedEvent = new CreaturePlayedEvent { CardId = CardId, PlayerId = CastingPlayerId };
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(playedEvent) };

		return new ActionResult(
			state.SpawnAction(
				new ResolveCreatureAction { CardId = CardId, CastingPlayerId = CastingPlayerId }
			)
		).WithEvent(playedEvent);
	}

	private GameState PayAdditionalCosts(GameState state, Card card)
	{
		for (int i = 0; i < card.AdditionalCastCosts.Count; i++)
		{
			var cost = card.AdditionalCastCosts[i];
			var paymentIds =
				cost.RequiresSelection && AdditionalCostPayments.TryGetValue(i, out var ids)
					? ids
					: [];
			state = cost.Pay(state, CastingPlayerId, CardId, paymentIds);
		}
		return state;
	}
}
