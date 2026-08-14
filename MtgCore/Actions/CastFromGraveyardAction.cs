using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Casts a card from a player's graveyard using its FlashbackComponent cost.
///
/// Instants and sorceries resolve as spells and are then exiled, so flashback is one-shot.
/// Creatures resolve onto the battlefield and are NOT exiled — that is the graveyard
/// recursion path (Gravecrawler / unearth), limited by mana rather than by exile.
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
		var isCreature = card.HasComponent<CreatureComponent>();
		if (spellComponent == null && !isCreature)
			return ValidationResult.Invalid("Card is neither a spell nor a creature");

		var player = gameState.GetPlayer(CastingPlayerId);
		if (player.CurrentMana < flashback.FlashbackManaCost)
			return ValidationResult.Invalid(
				$"Not enough mana for Flashback (have {player.CurrentMana}, need {flashback.FlashbackManaCost})"
			);

		// Creatures carry no SpellComponent, so they have no per-effect targets to validate.
		return spellComponent == null
			? ValidationResult.Valid
			: ValidateTargets(gameState, spellComponent);
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
		// MoveCardTracked emits CardLeftGraveyardEvent, deactivating any graveyard-active
		// static this card had.
		state = state.MoveCardTracked(CardId, state.GetStackId());

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

		// Creatures go to the battlefield and stay there; spells resolve and then exile.
		GameAction resolve = card.HasComponent<CreatureComponent>()
			? new ResolveCreatureAction { CardId = CardId, CastingPlayerId = CastingPlayerId }
			: new ResolveSpellAction
			{
				CardId = CardId,
				CastingPlayerId = CastingPlayerId,
				TargetIds = TargetIds,
				ExileAfterResolution = true,
			};

		return new ActionResult(state.SpawnAction(resolve)).WithEvent(castEvent);
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
