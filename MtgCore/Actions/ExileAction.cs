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

			if (state.GetCardZone(targetId).ZoneType == ZoneType.Battlefield)
			{
				var leftEvent = new PermanentLeftBattlefieldEvent
				{
					CardId = targetId,
					OwnerId = card.OwnerId,
				};
				state = state with { PendingGameEvents = state.PendingGameEvents.Add(leftEvent) };
			}

			var exileId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Exile);
			state = state.MoveObject(targetId, exileId);
			events = events.Add(new CardExiledEvent { CardId = targetId, PlayerId = card.OwnerId });
		}

		return new ActionResult(state) { Events = events };
	}

	public override ValidationResult ValidateResolve(GameState gameState)
	{
		var missingId = TargetIds.FirstOrDefault(id => !gameState.HasObject(id));
		return missingId != 0
			? ValidationResult.Invalid($"Target {missingId} no longer exists")
			: ValidationResult.Valid;
	}
}
