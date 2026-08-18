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
	/// Numeric payload of the event that fired this, surfaced to effects as
	/// ContextKeys.TriggerAmount. 0 for spells, activated abilities, and events with no amount.
	/// See TriggerAmountOf in CheckStateBasedEffectsAction for which events carry one.
	/// </summary>
	public int TriggerAmount { get; init; }

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
			var (resolvedTargets, newState) = ResolveTargets(effect, i, context, state);
			state = newState;

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
						.SetItem(ContextKeys.SourceCardId, SourceCardId)
						.SetItem(ContextKeys.TriggerAmount, TriggerAmount),
				}
				: action with
				{
					InputContext = action
						.InputContext.SetItem(ContextKeys.CastingPlayerId, CastingPlayerId)
						.SetItem(ContextKeys.SourceCardId, SourceCardId)
						.SetItem(ContextKeys.TriggerAmount, TriggerAmount),
				};

			spawnedActions = spawnedActions.Add(action);
		}

		return new ActionResult(state.SpawnActions(spawnedActions));
	}

	private (ImmutableList<int> Targets, GameState State) ResolveTargets(
		CardEffect effect,
		int effectIndex,
		TargetingContext context,
		GameState state
	)
	{
		return effect.TargetingStrategy.SelectionMode switch
		{
			TargetSelectionMode.UserSelect => (
				TargetIds.TryGetValue(effectIndex, out var targets)
					? targets
					: ImmutableList<int>.Empty,
				state
			),

			TargetSelectionMode.AllValid => (
				ImmutableList.CreateRange(
					effect.TargetingStrategy.GetValidTargets(context with { IsNonTargeted = true })
				),
				state
			),

			TargetSelectionMode.Random => ResolveRandomTarget(
				effect.TargetingStrategy,
				context,
				state
			),

			TargetSelectionMode.CastingPlayer => (ImmutableList.Create(CastingPlayerId), state),

			TargetSelectionMode.None => (ImmutableList<int>.Empty, state),

			_ => (ImmutableList<int>.Empty, state),
		};
	}

	private static (ImmutableList<int> Targets, GameState State) ResolveRandomTarget(
		TargetingStrategy strategy,
		TargetingContext context,
		GameState state
	)
	{
		var validTargets = strategy.GetValidTargets(context);
		if (validTargets.Count == 0)
			return (ImmutableList<int>.Empty, state);

		var (index, newState) = state.ConsumeRandom(validTargets.Count);
		return (ImmutableList.Create(validTargets[index]), newState);
	}
}
