using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// Every unit you hold leaves the field at the end of the turn. **Combat v3** — see KinJam.md.
///
/// A unit is a piece you place for one turn, not a permanent you accumulate. That is what makes
/// "which lanes do I contest, knowing the rest hit my face" a decision on EVERY turn rather than
/// only on the first, and it is what makes a dead turn impossible: the board starts empty, so
/// whatever you drew has somewhere to go.
///
/// **Why it had to change:** the hand was Slay the Spire (drawn to 5, discarded every turn) and the
/// board was Hearthstone (pay once, keep forever). An ephemeral hand kept feeding a permanent board
/// until the board saturated, and then five drawn units had nowhere to go and End Turn was the only
/// legal move. The stall was the seam between two economies, and it was the reward for playing well.
///
/// **THIS IS ITS OWN ACTION BECAUSE OF THE SPAWN ORDER, and that is the whole reason it is not four
/// lines inside `EndTurnAction`.** `EndTurnAction` SPAWNS it, and the queue runs FIFO after Execute
/// returns, so anything queued ahead of it reads the board you actually committed. Withdrawing
/// inline would empty the field first, and every effect that reads what is STANDING would silently
/// do nothing while looking exactly like one that worked.
///
/// The six doom scenarios that read the standing board are gone; the trap is not. Any future effect
/// that reads the field at end of turn depends on this ordering, so moving this action back inline
/// would break it silently rather than loudly.
///
/// **Withdrawn is not dead.** No `OnDeath` fires, nothing is recorded as having died, and nothing
/// reading deaths is paid. A unit that walked off the board at end of turn did not die in
/// your service, and a companion paying off "units that died last turn" must not be fed by its own
/// board wiping itself every turn. Only 0 toughness is death — that still happens in
/// `EndTurnAction.ClearTheDead`, before this ever runs.
/// </summary>
public record WithdrawUnitsAction : GameAction
{
	public override ActionResult Execute(GameState gameState)
	{
		// **THE DEAD ARE CLEARED FIRST, and skipping this loses deaths silently.** The apocalypse
		// resolves after `EndTurnAction` has already cleared the dead once, so anything the doom
		// killed is still standing on the Field when this runs. Withdrawing it would move it to
		// Discard as a survivor: no `OnDeath`, nothing in `DiedRunCardIds`, no `UnitDiedEvent`, and
		// Zombie paid nothing for a corpse it should have been paid for. Found by
		// `AnApocalypseAddedAsDataAloneStillFires` throwing, not by reasoning about it.
		var (state, events) = EndTurnAction.ClearTheDead(gameState, ImmutableList<GameEvent>.Empty);

		var discardId = state.ZoneId(ZoneType.Discard);
		var withdrawn = 0;

		foreach (var card in state.Units().ToList())
		{
			// The companion holds its lane. It is the one persistent thing in the game until
			// `Persistent` exists as a keyword, at which point this check becomes that one and the
			// companion stops being a special case — it is simply persistent. See KinV3Plan.md.
			if (card.HasComponent<CompanionComponent>())
				continue;

			state = state.MoveObject(card.Id, discardId);
			withdrawn++;
		}

		if (withdrawn > 0)
			events = events.Add(new UnitsWithdrewEvent { Count = withdrawn });

		return new ActionResult(state).WithEvents(events);
	}
}
