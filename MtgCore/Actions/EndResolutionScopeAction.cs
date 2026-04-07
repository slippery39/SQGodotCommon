using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Clears the SuppressPostProcessor flag on GameState, signalling that a spell
/// or ability has fully resolved and all its spawned effects have completed.
///
/// Always spawned as the last action by ResolveSpellAction and ActivateAbilityAction.
/// After this action completes, the PostActionProcessor fires normally (since
/// SuppressPostProcessor is now false), which runs CheckStateBasedEffectsAction
/// and any other deferred post-resolution work.
///
/// IsPostProcessor is intentionally false so the PostActionProcessor fires after
/// this action, triggering the deferred SBE check.
/// </summary>
public record EndResolutionScopeAction : GameAction
{
	public override ActionResult Execute(GameState gameState) =>
		new(gameState with { SuppressPostProcessor = false });
}
