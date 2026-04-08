using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Plays a creature card from a player's hand onto their battlefield.
///
/// On execution the card moves to the battlefield, the player's CurrentMana
/// is reduced by the card's ManaCost, HasSummoningSickness is set, and a
/// CreaturePlayedEvent is appended to PendingGameEvents for trigger evaluation.
/// </summary>
public record PlayCreatureAction : GameAction
{
	public int CardId { get; init; }
	public int PlayerId { get; init; }

	public override ValidationResult ValidateAdd(GameState gameState)
	{
		if (!gameState.HasObject(CardId))
			return ValidationResult.Invalid($"Card {CardId} does not exist");

		var card = gameState.GetObject(CardId) as Card;
		if (card == null)
			return ValidationResult.Invalid($"Object {CardId} is not a card");

		if (card.ControllerId != PlayerId)
			return ValidationResult.Invalid("You do not control this card");

		var handId = gameState.GetPlayerZoneId(PlayerId, ZoneType.Hand);
		if (gameState.GetCardZoneId(CardId) != handId)
			return ValidationResult.Invalid("Card is not in your hand");

		if (!card.HasComponent<CreatureComponent>())
			return ValidationResult.Invalid("Card is not a creature");

		var player = gameState.GetPlayer(PlayerId);
		if (player.CurrentMana < card.ManaCost)
			return ValidationResult.Invalid(
				$"Not enough mana (have {player.CurrentMana}, need {card.ManaCost})"
			);

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState gameState)
	{
		var card = (Card)gameState.GetObject(CardId);
		var battlefieldId = gameState.GetPlayerZoneId(PlayerId, ZoneType.Battlefield);

		// Spend mana
		var player = gameState.GetPlayer(PlayerId);
		var updatedPlayer = player with { CurrentMana = player.CurrentMana - card.ManaCost };
		var state = gameState.UpdateObject(PlayerId, updatedPlayer);

		// Stamp summoning sickness and move to battlefield
		var creature = card.GetComponent<CreatureComponent>()!;
		var updatedCard = card.WithComponentReplaced(creature with { HasSummoningSickness = true });
		state = state.UpdateObject(CardId, updatedCard).MoveObject(CardId, battlefieldId);

		// Emit event and stage for trigger evaluation
		var playedEvent = new CreaturePlayedEvent { CardId = CardId, PlayerId = PlayerId };
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(playedEvent) };

		return new ActionResult(state).WithEvent(playedEvent);
	}
}
