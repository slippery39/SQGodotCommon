using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Resolves a spell that is currently on the stack.
///
/// Owns the resolution scope for the entire spell:
///   1. Sets SuppressPostProcessor = true (defers SBE and trigger evaluation)
///   2. Spawns ResolveEffectAction with all of the spell's effects
///   3. Spawns MoveCardToGraveyardAction — card moves to graveyard after effects resolve
///   4. Spawns EndResolutionScopeAction — clears SuppressPostProcessor, unblocking
///      the PostActionProcessor which then runs SBE and trigger evaluation
///
/// The card intentionally moves to the graveyard after effects resolve, not before.
/// </summary>
public record ResolveSpellAction : GameAction
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }
	public bool ExileAfterResolution { get; init; }

	public ImmutableDictionary<int, ImmutableList<int>> TargetIds { get; init; } =
		ImmutableDictionary<int, ImmutableList<int>>.Empty;

	public override ActionResult Execute(GameState gameState)
	{
		var card = (Card)gameState.GetObject(CardId);
		var spellComponent = card.GetComponent<SpellComponent>();

		// Open the resolution scope — SBE and trigger evaluation deferred until
		// EndResolutionScopeAction clears this flag.
		var state = gameState with
		{
			SuppressPostProcessor = true,
		};

		var spawnedActions = ImmutableList<GameAction>.Empty;

		if (spellComponent != null && spellComponent.Effects.Count > 0)
		{
			var resolveEffect = new ResolveEffectAction
			{
				Effects = spellComponent.Effects,
				CastingPlayerId = CastingPlayerId,
				SourceCardId = CardId,
				TargetIds = TargetIds,
			};

			if (spellComponent.HasStorm)
			{
				// Storm: repeat effects once per spell cast this turn (including this one).
				// SpellsCastThisTurn was already incremented by CastSpellAction before we run.
				var game = gameState.TryGetGame();
				var copies = game != null ? Math.Max(game.SpellsCastThisTurn, 1) : 1;
				for (var i = 0; i < copies; i++)
					spawnedActions = spawnedActions.Add(resolveEffect);
			}
			else
			{
				spawnedActions = spawnedActions.Add(resolveEffect);
			}
		}

		// Card moves to graveyard (or exile for flashback) after effects resolve
		spawnedActions = spawnedActions.Add(
			ExileAfterResolution
				? new MoveCardToExileAction { CardId = CardId }
				: new MoveCardToGraveyardAction { CardId = CardId }
		);

		// EndResolutionScopeAction always last — clears SuppressPostProcessor
		spawnedActions = spawnedActions.Add(new EndResolutionScopeAction());

		return new ActionResult(state.SpawnActions(spawnedActions));
	}
}
