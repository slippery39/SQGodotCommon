using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Moves one or more target cards to their owner's Exile zone.
/// Used by spells like Path to Exile.
/// Cards in Exile are permanently removed from the game (no interaction yet).
/// </summary>
public record ExileAction : GameAction, ITargetedAction
{
	public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;

	public GameAction WithTargets(ImmutableList<int> targetIds) =>
		this with
		{
			TargetIds = targetIds,
		};

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		foreach (var targetId in TargetIds)
		{
			if (!state.HasObject(targetId))
				continue;

			var card = state.GetObject(targetId) as Card;
			if (card == null)
				continue;

			var exileId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Exile);
			state = state.MoveObject(targetId, exileId);
			events = events.Add(new CardExiledEvent { CardId = targetId, PlayerId = card.OwnerId });
		}

		return new ActionResult(state) { Events = events };
	}

	public override ValidationResult ValidateResolve(GameState gameState)
	{
		foreach (var targetId in TargetIds)
		{
			if (!gameState.HasObject(targetId))
				return ValidationResult.Invalid($"Target {targetId} no longer exists");
		}
		return ValidationResult.Valid;
	}
}
