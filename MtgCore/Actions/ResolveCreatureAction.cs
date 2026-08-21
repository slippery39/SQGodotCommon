using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Resolves a creature spell that is currently on the stack.
///
/// Opens a resolution scope and delegates to PutIntoBattlefieldAction,
/// which stamps HasSummoningSickness and emits CreatureEnteredBattlefieldEvent.
/// EndResolutionScopeAction closes the scope and unblocks trigger evaluation.
///
/// XValue is forwarded into PutIntoBattlefieldAction's InputContext so a Hydra with
/// EntersWithCountersComponent { FromXValue = true } arrives with that many counters. This is the
/// only route that carries an X — a reanimated or cloned Hydra was never cast and correctly
/// enters at 0.
/// </summary>
public record ResolveCreatureAction : GameAction
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }
	public int XValue { get; init; } = 0;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState with { SuppressPostProcessor = true };

		var putIntoPlay = new PutIntoBattlefieldAction
		{
			TargetIds = ImmutableList.Create(CardId),
			InputContext = ImmutableDictionary<string, object>.Empty.SetItem(
				ContextKeys.XValue,
				XValue
			),
		};

		var spawnedActions = ImmutableList<GameAction>
			.Empty.Add(putIntoPlay)
			.Add(new EndResolutionScopeAction());

		return new ActionResult(state.SpawnActions(spawnedActions));
	}
}
