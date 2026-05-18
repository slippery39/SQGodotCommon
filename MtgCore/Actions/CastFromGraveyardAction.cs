using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Casts a flashback spell from a player's graveyard using its flashback cost.
/// After resolution the card is exiled, not returned to the graveyard.
/// </summary>
public record CastFromGraveyardAction : GameAction
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }

	public ImmutableDictionary<int, ImmutableList<int>> TargetIds { get; init; } =
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

		var graveyardId = gameState.GetPlayerZoneId(CastingPlayerId, ZoneType.Graveyard);
		if (gameState.GetCardZoneId(CardId) != graveyardId)
			return ValidationResult.Invalid("Card is not in your graveyard");

		var flashback = card.GetComponent<FlashbackComponent>();
		if (flashback == null)
			return ValidationResult.Invalid("Card does not have Flashback");

		var spellComponent = card.GetComponent<SpellComponent>();
		if (spellComponent == null)
			return ValidationResult.Invalid("Card is not a spell");

		var player = gameState.GetPlayer(CastingPlayerId);
		if (player.CurrentMana < flashback.FlashbackManaCost)
			return ValidationResult.Invalid(
				$"Not enough mana for Flashback (have {player.CurrentMana}, need {flashback.FlashbackManaCost})"
			);

		return ValidateTargets(gameState, spellComponent);
	}

	public override ActionResult Execute(GameState gameState)
	{
		var card = (Card)gameState.GetObject(CardId);
		var flashback = card.GetComponent<FlashbackComponent>()!;

		var player = gameState.GetPlayer(CastingPlayerId);
		var state = gameState.UpdateObject(
			CastingPlayerId,
			player with
			{
				CurrentMana = player.CurrentMana - flashback.FlashbackManaCost,
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

		var castEvent = new SpellCastEvent { CardId = CardId, CastingPlayerId = CastingPlayerId };
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(castEvent) };

		return new ActionResult(
			state.SpawnAction(
				new ResolveSpellAction
				{
					CardId = CardId,
					CastingPlayerId = CastingPlayerId,
					TargetIds = TargetIds,
					ExileAfterResolution = true,
				}
			)
		).WithEvent(castEvent);
	}

	private ValidationResult ValidateTargets(GameState gameState, SpellComponent spellComponent)
	{
		var context = new TargetingContext
		{
			GameState = gameState,
			SourceCardId = CardId,
			CastingPlayerId = CastingPlayerId,
		};

		for (int i = 0; i < spellComponent.Effects.Count; i++)
		{
			var effect = spellComponent.Effects[i];
			if (!effect.TargetingStrategy.RequiresUserSelection)
				continue;

			if (!TargetIds.TryGetValue(i, out var targets))
				return ValidationResult.Invalid($"No targets provided for effect {i}");

			if (!effect.TargetingStrategy.ValidateTargets(targets, context))
				return ValidationResult.Invalid($"Invalid targets for effect {i}");
		}
		return ValidationResult.Valid;
	}
}
