using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Resolves a spell that is currently on the stack.
///
/// Reads effects from the card's SpellComponent. If the card has no
/// SpellComponent, the action does nothing beyond moving it to the graveyard.
///
/// For each CardEffect, resolves targets based on SelectionMode then spawns
/// the action template. If the template is a PipelineAction, CastingPlayerId
/// is seeded into its initial context so inner steps can read it via GetInput.
///
/// Then moves the card to the owner's graveyard.
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
		var spellComponent = card.GetComponent<SpellComponent>();

		var spawnedActions = ImmutableList<GameAction>.Empty;

		if (spellComponent != null)
		{
			var context = new TargetingContext
			{
				GameState = gameState,
				SourceCardId = CardId,
				CastingPlayerId = CastingPlayerId,
			};

			for (int i = 0; i < spellComponent.Effects.Count; i++)
			{
				var effect = spellComponent.Effects[i];
				var resolvedTargets = ResolveTargets(effect, i, context);

				GameAction action = effect.ActionTemplate is ITargetedAction targeted
					? targeted.WithTargets(resolvedTargets)
					: effect.ActionTemplate;

				if (action is PipelineAction pipeline)
				{
					action = pipeline with
					{
						PipelineContext = pipeline.PipelineContext.SetItem(
							ContextKeys.CastingPlayerId,
							CastingPlayerId
						),
					};
				}

				spawnedActions = spawnedActions.Add(action);
			}
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

			TargetSelectionMode.CastingPlayer => [CastingPlayerId],

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
		return [chosen];
	}
}
