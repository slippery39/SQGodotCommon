using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Transforms a double-faced card in place.
/// The card's ID is preserved; only Name, Subtypes, and Components are swapped
/// using the data stored in TransformComponent.
///
/// Per-turn creature state (HasSummoningSickness, HasAttacked, Damage) is carried
/// from the old face to the new face so a transform never inadvertently resets
/// state that StartTurnAction already established this turn.
///
/// TransformComponent is always rebuilt pointing back at the face we came from,
/// enabling symmetric back-and-forth transforms for future cards.
/// </summary>
public record TransformAction : EffectAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var targetId = ResolveTargetIds().FirstOrDefault(-1);
		if (targetId == -1)
			return new ActionResult(gameState);

		if (!gameState.HasObject(targetId))
			return new ActionResult(gameState);

		var card = gameState.GetObject(targetId) as Card;
		if (card == null)
			return new ActionResult(gameState);

		var transformComponent = card.Components.OfType<TransformComponent>().FirstOrDefault();
		if (transformComponent == null)
			return new ActionResult(gameState);

		var currentFaceComponents = card
			.Components.Where(c => c is not TransformComponent)
			.ToImmutableList();

		var newTransformComponent = new TransformComponent
		{
			OtherFaceName = card.Name,
			OtherFaceSubtypes = card.Subtypes,
			OtherFaceComponents = currentFaceComponents,
		};

		var newFaceComponents = CarryCreatureState(
			from: card.Components,
			to: transformComponent.OtherFaceComponents
		);

		var newCard = card with
		{
			Name = transformComponent.OtherFaceName,
			Subtypes = transformComponent.OtherFaceSubtypes,
			Components = newFaceComponents.Add(newTransformComponent),
		};

		return new ActionResult(gameState.UpdateObject(targetId, newCard));
	}

	// Preserves HasSummoningSickness, HasAttacked, and Damage across the transform
	// so the new face inherits the creature's current combat state rather than
	// starting from the template defaults.
	private static ImmutableList<GameComponent> CarryCreatureState(
		ImmutableList<GameComponent> from,
		ImmutableList<GameComponent> to
	)
	{
		var oldCreature = from.OfType<CreatureComponent>().FirstOrDefault();
		var newCreature = to.OfType<CreatureComponent>().FirstOrDefault();
		if (oldCreature == null || newCreature == null)
			return to;

		var carried = newCreature with
		{
			HasSummoningSickness = oldCreature.HasSummoningSickness,
			HasAttacked = oldCreature.HasAttacked,
			Damage = oldCreature.Damage,
		};

		return to.Replace(newCreature, carried);
	}
}
