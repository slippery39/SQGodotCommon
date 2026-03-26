using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Resolves a spell that is currently on the stack.
///
/// For each CardEffect, resolves targets based on SelectionMode:
///   UserSelect — injects the pre-chosen target IDs from TargetIds
///   AllValid   — queries all valid targets from current game state
///   Random     — picks one random valid target from current game state
///   None       — no targets, spawns action as-is
///
/// Then moves the card to the owner's graveyard.
/// </summary>
public record ResolveSpellAction : GameAction
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }
	public int GameId { get; init; }

	/// <summary>
	/// Pre-chosen targets from the UI, keyed by effect index.
	/// Only populated for UserSelect effects.
	/// </summary>
	public ImmutableDictionary<int, ImmutableList<int>> TargetIds { get; init; } =
		ImmutableDictionary<int, ImmutableList<int>>.Empty;

	public override ActionResult Execute(GameState gameState)
	{
		var card = (Card)gameState.GetObject(CardId);
		var graveyardId = gameState.GetPlayerZoneId(CastingPlayerId, ZoneType.Graveyard);

		var context = new TargetingContext
		{
			GameState = gameState,
			SourceCardId = CardId,
			CastingPlayerId = CastingPlayerId,
		};

		var spawnedActions = ImmutableList<GameAction>.Empty;

		for (int i = 0; i < card.Effects.Count; i++)
		{
			var effect = card.Effects[i];
			var resolvedTargets = ResolveTargets(effect, i, context);

			GameAction action = effect.ActionTemplate is ITargetedAction targeted
				? targeted.WithTargets(resolvedTargets)
				: effect.ActionTemplate;

			spawnedActions = spawnedActions.Add(action);
		}

		var stateWithCardInGraveyard = gameState.MoveObject(CardId, graveyardId);

		return new ActionResult(stateWithCardInGraveyard) { SpawnedActions = spawnedActions };
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

			TargetSelectionMode.AllValid => effect.TargetingStrategy.GetValidTargets(context),

			TargetSelectionMode.Random => ResolveRandomTarget(effect.TargetingStrategy, context),

			TargetSelectionMode.CastingPlayer => ImmutableList.Create(context.CastingPlayerId),

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

		if (validTargets.IsEmpty)
			return ImmutableList<int>.Empty;

		var rng = new Random();
		var chosen = validTargets[rng.Next(validTargets.Count)];
		return ImmutableList.Create(chosen);
	}
}
