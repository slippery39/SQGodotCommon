using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Puts a card back in your hand from wherever it is.
///
/// **Built for Revenant — "when this dies, return it to your hand"** — which is the sacrifice
/// cluster's renewable fodder: a body you can spend again next turn, for another card and another
/// energy. That recurring cost is the throttle, so no loop needs a separate cap.
///
/// **It arrives AFTER the end-of-turn discard, and that is correct rather than a bug.** A unit that
/// dies in combat dies inside `EndTurnAction`, which spawns this; spawned actions resolve once that
/// action has finished, by which point the hand has already been swept. The card says it comes back
/// to your hand, and it does — the alternative is a card whose text is true only when you sacrifice
/// it yourself.
/// </summary>
public record ReturnToHandAction : EffectAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		foreach (var id in TargetIds)
		{
			if (!state.HasObject(id))
				continue;

			if (state.GetObject(id) is not DoomCard)
				continue;

			state = state.MoveObject(id, state.ZoneId(ZoneType.Hand));
		}

		return new ActionResult(state);
	}
}
