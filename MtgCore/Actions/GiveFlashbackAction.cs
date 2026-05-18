using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Adds FlashbackComponent to a target instant or sorcery card in the graveyard,
/// using the card's own ManaCost as the flashback cost.
/// No-ops if the card already has FlashbackComponent.
/// </summary>
public record GiveFlashbackAction : EffectAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;

			if (state.GetObject(targetId) is not Card card)
				continue;

			if (card.HasComponent<FlashbackComponent>())
				continue;

			var updated = card with
			{
				Components = card.Components.Add(
					new FlashbackComponent { FlashbackManaCost = card.ManaCost }
				),
			};
			state = state.UpdateObject(targetId, updated);
		}

		return new ActionResult(state);
	}
}
