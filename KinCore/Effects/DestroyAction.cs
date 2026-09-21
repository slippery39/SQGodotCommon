using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// Kills its targets. **Sacrifice** — the first thing in the game that can make a unit die on
/// demand.
///
/// **Why it matters more than it looks.** Combat v3 made units withdraw at end of turn, so an
/// ordinary unit almost never dies — `CountOf.DiedLastTurn` fixed the READS of that, and this fixes
/// the SUPPLY. Every death payoff in the pool (Ash, Gravedigger, Pyre Tender, The Choirmaster,
/// Zombie) is currently fed only by what the enemy chooses to kill. With a sacrifice outlet the
/// attrition axis becomes something the player drives.
///
/// **It does not write a second account of dying.** It marks the target dead and calls
/// <see cref="EndTurnAction.ClearTheDead"/>, which is the one place that fires `OnDeath`, records
/// the run id in `DiedThisTurnRunCardIds` and moves the card to Discard. A second implementation
/// would drift from the first, and the half that nobody was looking at would be the wrong one.
///
/// **Cleared INLINE rather than left for end of turn.** `PlayCardAction` documents that nothing
/// kills a unit during your own turn and that a corpse left on the Field would be discarded without
/// firing its death. This is the thing that changed, so it clears up after itself immediately and
/// that assumption stays true.
///
/// **The companion is never sacrificed.** `SweepFieldAction` spares it and `PlayCardAction` refuses
/// to build over it; it is the one thing the game promises cannot be taken from you, and an outlet
/// that ate it would make the promise conditional.
/// </summary>
public record DestroyAction : EffectAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var killed = 0;

		foreach (var id in TargetIds)
		{
			if (!state.HasObject(id))
				continue;

			if (state.GetObject(id) is not KinCard card)
				continue;

			if (card.GetComponent<UnitComponent>() is not { } unit)
				continue;

			if (card.HasComponent<CompanionComponent>())
				continue;

			if (unit.IsDead)
				continue;

			state = state.UpdateObject(
				id,
				card.WithComponentReplaced(unit with { Damage = unit.Toughness })
			);
			killed++;
		}

		if (killed == 0)
			return new ActionResult(state);

		var (cleared, events) = EndTurnAction.ClearTheDead(state, ImmutableList<GameEvent>.Empty);
		return new ActionResult(cleared).WithEvents(events);
	}
}
