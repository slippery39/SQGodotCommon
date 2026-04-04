using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Plays a creature card from a player's hand onto their battlefield.
///
/// ValidateAdd confirms the card exists, is in the player's hand,
/// is controlled by the playing player, has a CreatureComponent,
/// and that the player has enough mana to pay the cost.
///
/// On execution the card moves to the battlefield, the player's CurrentMana
/// is reduced by the card's ManaCost, and HasSummoningSickness is set.
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

		// Stamp summoning sickness
		var creature = card.GetComponent<CreatureComponent>()!;
		var updatedCard = card.WithComponentReplaced(creature with { HasSummoningSickness = true });

		state = state.UpdateObject(CardId, updatedCard).MoveObject(CardId, battlefieldId);

		return new ActionResult(state).WithEvent(
			new CreaturePlayedEvent { CardId = CardId, PlayerId = PlayerId }
		);
	}
}
