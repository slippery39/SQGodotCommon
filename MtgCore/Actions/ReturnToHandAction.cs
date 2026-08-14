using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Returns each target card to its owner's hand, from any zone.
///
/// The targeted counterpart to MoveCardToHandAction, which resolves a single card through
/// pipeline context keys. This one takes TargetIds, so "return target creature card from your
/// graveyard to your hand" is expressible with the ordinary targeting strategies — exactly how
/// Reanimate uses PutIntoBattlefieldAction with CreatureInYourGraveyard().
///
/// Uses MoveCardTracked, so returning a card from a graveyard correctly deactivates any
/// graveyard-active static ability it had.
/// </summary>
public record ReturnToHandAction : EffectAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		foreach (var cardId in ResolveTargetIds())
		{
			if (state.GetObject(cardId) is not Card card)
				continue;

			var handId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Hand);
			state = state.MoveCardTracked(cardId, handId);
		}

		return new ActionResult(state);
	}
}
