using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Resolves a list of CardEffects as a single unit.
/// Used by spells, activated abilities, and triggered abilities — all share
/// this single resolution path regardless of source.
///
/// For each effect:
///   - Resolves targets based on the TargetingStrategy
///   - Spawns the effect action with resolved targets injected
///   - Seeds CastingPlayerId into the spawned action's context
///
/// This action has no knowledge of resolution scope (SuppressPostProcessor),
/// graveyard movement, or any other source-specific concerns. Those are the
/// responsibility of the caller (ResolveSpellAction, ActivateAbilityAction, etc).
///
/// Pre-selected targets for UserSelect effects are passed in via TargetIds,
/// keyed by effect index matching the Effects list.
/// </summary>
public record ResolveEffectAction : GameAction
{
	public ImmutableList<CardEffect> Effects { get; init; } = ImmutableList<CardEffect>.Empty;
	public int CastingPlayerId { get; init; }
	public int SourceCardId { get; init; }

	/// <summary>
	/// Pre-selected targets for UserSelect effects, keyed by effect index.
	/// </summary>
	public ImmutableDictionary<int, ImmutableList<int>> TargetIds { get; init; } =
		ImmutableDictionary<int, ImmutableList<int>>.Empty;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var spawnedActions = ImmutableList<GameAction>.Empty;

		var context = new TargetingContext
		{
			GameState = state,
			SourceCardId = SourceCardId,
			CastingPlayerId = CastingPlayerId,
		};

		for (int i = 0; i < Effects.Count; i++)
		{
			var effect = Effects[i];
			var resolvedTargets = ResolveTargets(effect, i, context);

			GameAction action = effect.ActionTemplate is ITargetedAction targeted
				? targeted.WithTargets(resolvedTargets)
				: effect.ActionTemplate;

			// Seed CastingPlayerId and SourceCardId so context-key-based actions
			// (PlayerIdContextKey, EquipmentCardIdContextKey, etc.) resolve correctly
			// regardless of whether the action is a pipeline or standalone.
			action = action is PipelineAction pipeline
				? pipeline with
				{
					PipelineContext = pipeline
						.PipelineContext.SetItem(ContextKeys.CastingPlayerId, CastingPlayerId)
						.SetItem(ContextKeys.SourceCardId, SourceCardId),
				}
				: action with
				{
					InputContext = action
						.InputContext.SetItem(ContextKeys.CastingPlayerId, CastingPlayerId)
						.SetItem(ContextKeys.SourceCardId, SourceCardId),
				};

			spawnedActions = spawnedActions.Add(action);
		}

		return new ActionResult(state.SpawnActions(spawnedActions));
	}

	private ImmutableList<int> ResolveTargets(
		CardEffect effect,
		int effectIndex,
		TargetingContext context
	)
	{
		return effect.TargetingStrategy.SelectionMode switch
		{
			TargetSelectionMode.UserSelect => TargetIds.TryGetValue(effectIndex, out var targets)
				? targets
				: ImmutableList<int>.Empty,

			TargetSelectionMode.AllValid => ImmutableList.CreateRange(
				effect.TargetingStrategy.GetValidTargets(context)
			),

			TargetSelectionMode.Random => ResolveRandomTarget(effect.TargetingStrategy, context),

			TargetSelectionMode.CastingPlayer => ImmutableList.Create(CastingPlayerId),

			TargetSelectionMode.None => ImmutableList<int>.Empty,

			_ => ImmutableList<int>.Empty,
		};
	}

	private static ImmutableList<int> ResolveRandomTarget(
		TargetingStrategy strategy,
		TargetingContext context
	)
	{
		var validTargets = strategy.GetValidTargets(context);
		if (validTargets.Count == 0)
			return ImmutableList<int>.Empty;

		var chosen = validTargets[new Random().Next(validTargets.Count)];
		return ImmutableList.Create(chosen);
	}
}
