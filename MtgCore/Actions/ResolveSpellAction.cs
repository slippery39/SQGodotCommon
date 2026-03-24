using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Resolves a spell that is currently on the stack.
/// For each CardEffect:
///   - If UserSelect: injects the pre-chosen target IDs from TargetIds
///   - If AllValid: resolves targets from the current game state
///   - If None: spawns the action with no targets
/// Then moves the card to the graveyard.
/// </summary>
public record ResolveSpellAction : GameAction
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }
	public int GameId { get; init; }

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

		// Build spawned actions for each effect
		var spawnedActions = ImmutableList<GameAction>.Empty;

		for (int i = 0; i < card.Effects.Count; i++)
		{
			var effect = card.Effects[i];
			var resolvedTargets = ResolveTargets(effect, i, context);

			GameAction action;
			if (effect.ActionTemplate is ITargetedAction targeted)
				action = targeted.WithTargets(resolvedTargets);
			else
				action = effect.ActionTemplate;

			spawnedActions = spawnedActions.Add(action);
		}

		// Move card from stack to graveyard
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

			TargetSelectionMode.None => ImmutableList<int>.Empty,

			_ => ImmutableList<int>.Empty,
		};
	}
}
