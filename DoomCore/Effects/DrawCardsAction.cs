using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Draws cards, reusing the same draw StartTurnAction uses — so reshuffling the discard and
/// Irradiated's life-on-draw both happen here too, without this action knowing about either.
///
/// Sharing that path is deliberate: a second implementation of "draw a card" would be a second set
/// of rules to keep in step, and the Nuclear price is exactly the sort of thing it would forget.
/// </summary>
public record DrawCardsAction : EffectAction
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var (state, events) = StartTurnAction.DrawCards(gameState, Amount);
		return new ActionResult(state).WithEvents(events);
	}
}
