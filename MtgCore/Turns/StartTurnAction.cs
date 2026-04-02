using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Begins the active player's turn.
///
/// Responsibilities:
///   - Spawns DrawCardsAction to draw one card for the active player
///   - Clears HasSummoningSickness, HasAttacked, and Damage on all
///     creatures the active player controls on the battlefield
///
/// Drawing is delegated to DrawCardsAction so that draw logic is not
/// duplicated here, and so the post-processor fires correctly after the draw.
///
/// Emits TurnStartedEvent.
/// </summary>
public record StartTurnAction : GameAction
{
	public int ActivePlayerId { get; init; }
	public int BattlefieldId { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		// Clear per-turn flags on all creatures the active player controls
		var battlefieldCreatures = state
			.GetCardsInZone(BattlefieldId)
			.Where(c => c.ControllerId == ActivePlayerId && c.HasComponent<CreatureComponent>())
			.ToList();

		foreach (var creature in battlefieldCreatures)
		{
			var component = creature.GetComponent<CreatureComponent>()!;
			var reset = component with
			{
				HasSummoningSickness = false,
				HasAttacked = false,
				Damage = 0,
			};
			state = state.UpdateObject(creature.Id, creature.WithComponentReplaced(reset));
		}

		var drawCard = new DrawCardsAction { PlayerId = ActivePlayerId, Amount = 1 };

		return new ActionResult(state)
		{
			Events = ImmutableList.Create<GameEvent>(
				new TurnStartedEvent { PlayerId = ActivePlayerId }
			),
			SpawnedActions = ImmutableList.Create<GameAction>(drawCard),
		};
	}
}
