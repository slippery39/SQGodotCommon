using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Casts a spell from a player's hand.
///
/// ValidateAdd confirms the card exists, is in the player's hand,
/// is controlled by the casting player, has a SpellComponent,
/// has valid targets, and that the player has enough mana.
///
/// Execute moves the card to the stack, spends mana, and spawns ResolveSpellAction.
/// </summary>
public record CastSpellAction : GameAction
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }
	public int GameId { get; init; }

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

		var handId = gameState.GetPlayerZoneId(CastingPlayerId, ZoneType.Hand);
		var cardZoneId = gameState.GetCardZoneId(CardId);
		if (cardZoneId != handId)
			return ValidationResult.Invalid("Card is not in your hand");

		var spellComponent = card.GetComponent<SpellComponent>();
		if (spellComponent == null)
			return ValidationResult.Invalid("Card is not a spell");

		var player = gameState.GetPlayer(CastingPlayerId);
		if (player.CurrentMana < card.ManaCost)
			return ValidationResult.Invalid(
				$"Not enough mana (have {player.CurrentMana}, need {card.ManaCost})"
			);

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

	public override ActionResult Execute(GameState gameState)
	{
		var card = (Card)gameState.GetObject(CardId);

		// Spend mana
		var player = gameState.GetPlayer(CastingPlayerId);
		var updatedPlayer = player with { CurrentMana = player.CurrentMana - card.ManaCost };
		var state = gameState.UpdateObject(CastingPlayerId, updatedPlayer);

		var stackId = state.GetStackId(GameId);
		state = state.MoveObject(CardId, stackId);

		var resolveAction = new ResolveSpellAction
		{
			CardId = CardId,
			CastingPlayerId = CastingPlayerId,
			GameId = GameId,
			TargetIds = TargetIds,
		};

		return new ActionResult(state.SpawnAction(resolveAction));
	}
}
