using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

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
		// **Scaled, like every other amount.** It read raw `Amount` until 2026-09-18, so a
		// `PerEach` on a draw was a silent no-op — the card would have drawn its base number and
		// looked exactly like a card that worked.
		var (state, events) = StartTurnAction.DrawCards(gameState, Scaled(gameState, Amount));
		return new ActionResult(state).WithEvents(events);
	}
}
