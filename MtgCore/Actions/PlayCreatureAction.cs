using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Plays a creature card from a player's hand onto their battlefield.
///
/// ValidateAdd confirms the card exists, is in the player's hand,
/// is controlled by the playing player, and has a CreatureComponent.
///
/// On execution the card moves to the battlefield and its CreatureComponent
/// is updated to set HasSummoningSickness = true, ensuring it cannot
/// attack the turn it enters.
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

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState gameState)
	{
		var card = (Card)gameState.GetObject(CardId);
		var battlefieldId = gameState.GetPlayerZoneId(PlayerId, ZoneType.Battlefield);

		// Stamp summoning sickness on — always true when entering the battlefield
		var creature = card.GetComponent<CreatureComponent>()!;
		var updatedCard = card.WithComponentReplaced(creature with { HasSummoningSickness = true });

		var newState = gameState
			.UpdateObject(CardId, updatedCard)
			.MoveObject(CardId, battlefieldId);

		return new ActionResult(newState).WithEvent(
			new CreaturePlayedEvent { CardId = CardId, PlayerId = PlayerId }
		);
	}
}
