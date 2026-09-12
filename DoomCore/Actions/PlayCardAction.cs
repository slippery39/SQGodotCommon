using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Plays a card from hand. Units go to the Field; anything else resolves and goes to Discard.
///
/// Energy is the whole economy — no mana, no lands, no colours. A card that burns the countdown to
/// be played early will pay that here too, once such cards exist.
/// </summary>
public record PlayCardAction : GameAction
{
	public int CardId { get; init; }

	public override ValidationResult ValidateAdd(GameState gameState)
	{
		if (!gameState.HasObject(CardId))
			return ValidationResult.Invalid($"No card {CardId}");

		if (gameState.GetParent(CardId) != gameState.ZoneId(ZoneType.Hand))
			return ValidationResult.Invalid("Card is not in hand");

		var card = (DoomCard)gameState.GetObject(CardId);
		if (gameState.GetPlayer().Energy < card.Cost)
			return ValidationResult.Invalid($"Not enough energy for {card.Name}");

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState gameState)
	{
		var card = (DoomCard)gameState.GetObject(CardId);
		var player = gameState.GetPlayer();

		var state = gameState.UpdateObject(
			player.Id,
			player with
			{
				Energy = player.Energy - card.Cost,
			}
		);

		// A unit enters the Field ready — there is no summoning sickness. With only 2-5 turns in a
		// battle, a turn of nothing would make half the units unplayable.
		var destination = card.HasComponent<UnitComponent>() ? ZoneType.Field : ZoneType.Discard;
		state = state.MoveObject(CardId, state.ZoneId(destination));

		return new ActionResult(state).WithEvent(
			new CardPlayedEvent
			{
				CardId = CardId,
				CardName = card.Name,
				EnergySpent = card.Cost,
			}
		);
	}
}
