using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Washes its targets off the field into Discard, whole.
///
/// **Flood, as an action rather than a special case.** The card comes back undamaged because what
/// returns from Discard is the card, not the body that stood in the lane — and the damage is
/// cleared on the way IN by `PlayCardAction`, so nothing needs doing here.
///
/// The companion rides it out. It is not a card and has nowhere to be discarded TO: putting it in
/// Discard would make it drawable, and it is meant to be the one thing an apocalypse cannot touch.
/// That rule lives here, with the action, rather than in whichever scenario happens to sweep.
/// </summary>
public record SweepFieldAction : EffectAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var discardId = state.ZoneId(ZoneType.Discard);
		var swept = 0;

		foreach (var id in TargetIds)
		{
			if (!state.HasObject(id) || state.GetObject(id) is not DoomCard card)
				continue;

			if (card.HasComponent<CompanionComponent>())
				continue;

			state = state.MoveObject(id, discardId);
			swept++;
		}

		return new ActionResult(state).WithEvent(new FieldSweptEvent { Count = swept });
	}
}
