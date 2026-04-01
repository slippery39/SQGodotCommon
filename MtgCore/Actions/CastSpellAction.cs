using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Casts a spell from a player's hand.
///
/// Targets are chosen by the UI before this action is created and submitted.
/// ValidateAdd confirms the targets are legal per each effect's TargetingStrategy.
/// Execute moves the card to the stack and spawns a ResolveSpellAction.
///
/// TargetIds maps effect index → chosen target IDs for that effect.
/// Most spells have one effect so TargetIds will have one entry.
/// </summary>
public record CastSpellAction : GameAction
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }
	public int GameId { get; init; }

	/// <summary>
	/// Targets chosen by the UI, keyed by effect index.
	/// </summary>
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
		var stackId = gameState.GetStackId(GameId);

		// Move card from hand to stack
		var stateWithCardOnStack = gameState.MoveObject(CardId, stackId);

		// Spawn the resolution action
		var resolveAction = new ResolveSpellAction
		{
			CardId = CardId,
			CastingPlayerId = CastingPlayerId,
			GameId = GameId,
			TargetIds = TargetIds,
		};

		return new ActionResult(stateWithCardOnStack)
		{
			SpawnedActions = ImmutableList.Create<GameAction>(resolveAction),
		};
	}
}
