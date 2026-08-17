using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Casts a non-creature permanent card (artifact, enchantment) from a player's hand
/// onto the stack.
///
/// ValidateAdd confirms the card exists, is in the player's hand, is controlled by
/// the casting player, has PermanentComponent but not CreatureComponent (creatures
/// use CastCreatureAction), and that the player has enough mana.
///
/// Execute spends mana, moves the card to the stack, increments SpellsCastThisTurn
/// (non-creature permanents are spells while on the stack), and spawns
/// ResolvePermanentAction which moves the card directly to the battlefield.
/// </summary>
public record CastPermanentAction : GameAction
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

		if (!card.HasComponent<PermanentComponent>())
			return ValidationResult.Invalid("Card is not a permanent");

		if (card.HasComponent<CreatureComponent>())
			return ValidationResult.Invalid("Use CastCreatureAction for creature cards");

		foreach (var restriction in card.GetComponents<CastRestrictionComponent>())
			if (!restriction.CanCast(gameState, CastingPlayerId))
				return ValidationResult.Invalid(restriction.Describe());

		var player = gameState.GetPlayer(CastingPlayerId);
		var effectiveCost = gameState.ComputeEffectiveCost(card, CastingPlayerId);
		if (player.CurrentMana < effectiveCost)
			return ValidationResult.Invalid(
				$"Not enough mana (have {player.CurrentMana}, need {effectiveCost})"
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
				CurrentMana =
					player.CurrentMana - state.ComputeEffectiveCost(card, CastingPlayerId),
			}
		);
		state = state.MoveObject(CardId, state.GetStackId());

		var game = state.TryGetGame();
		if (game != null)
			state = state.UpdateObject(
				game.Id,
				game with
				{
					SpellsCastThisTurn = game.SpellsCastThisTurn + 1,
				}
			);

		var playedEvent = new PermanentPlayedEvent { CardId = CardId, PlayerId = CastingPlayerId };
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(playedEvent) };

		// Negate hits enchantments and planeswalkers too — see CastSpellAction for the ordering.
		var counter = CounterTrapEngine.TryCounterCast(state, CardId, CastingPlayerId);
		state = counter.State;
		if (counter.Countered)
			return new ActionResult(state).WithEvent(playedEvent);

		return new ActionResult(
			state.SpawnAction(
				new ResolvePermanentAction { CardId = CardId, CastingPlayerId = CastingPlayerId }
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
