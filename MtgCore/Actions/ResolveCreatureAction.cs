using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Resolves a creature spell that is currently on the stack.
///
/// Opens a resolution scope and delegates to PutIntoBattlefieldAction,
/// which stamps HasSummoningSickness and emits CreatureEnteredBattlefieldEvent.
/// EndResolutionScopeAction closes the scope and unblocks trigger evaluation.
/// </summary>
public record ResolveCreatureAction : GameAction
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState with { SuppressPostProcessor = true };

		var spawnedActions = ImmutableList<GameAction>
			.Empty.Add(new PutIntoBattlefieldAction { TargetIds = ImmutableList.Create(CardId) })
			.Add(new EndResolutionScopeAction());

		return new ActionResult(state.SpawnActions(spawnedActions));
	}
}
