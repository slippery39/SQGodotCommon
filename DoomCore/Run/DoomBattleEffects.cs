using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// The hook for <see cref="DoomScope.Battle"/> scenarios: apocalypses that reshape THIS FIGHT and
/// leave the run deck alone.
///
/// The sibling of <see cref="DoomTransforms"/>, split by what each one is allowed to touch. The
/// split is not a second mechanism — it is the same hook over the two things a doom can change, and
/// it exists because a `GameState` dies with the battle while a `Run` does not.
///
/// Adding a battle scenario is one case here, a `ScopeOf` row and a `CountdownFor` row.
/// </summary>
public static class DoomBattleEffects
{
	public static GameState Apply(GameState state, DoomScenario scenario)
	{
		if (StarterContent.ScopeOf(scenario) != DoomScope.Battle)
			throw new InvalidOperationException(
				$"{scenario} is a {StarterContent.ScopeOf(scenario)} scenario and belongs in "
					+ "DoomTransforms, not here. A scenario in the wrong hook does nothing at all "
					+ "and looks exactly like one that worked."
			);

		return scenario switch
		{
			DoomScenario.Flood => Flood(state),
			_ => throw new ArgumentOutOfRangeException(nameof(scenario)),
		};
	}

	/// <summary>
	/// **The water takes what is standing.** Every unit in play is washed to Discard — you keep the
	/// cards, you lose the board and the energy you spent putting it there.
	///
	/// Flood used to delete never-summoned units from the run deck. Permanent card removal caused
	/// more trouble than it was worth: on a starter deck it could empty a run outright, which is why
	/// it had to be gated behind floor 8 and therefore did not exist for the first seven. As a wash
	/// it appears from floor 1 and teaches its fiction long before anything with teeth does.
	///
	/// It costs tempo, not material, which is what makes it an obstacle rather than a tax — and it
	/// is the question the lane game wants asked: how much do you commit to a board about to go
	/// under?
	/// </summary>
	private static GameState Flood(GameState state)
	{
		var discardId = state.ZoneId(ZoneType.Discard);

		foreach (var card in state.Units().ToList())
		{
			// The companion is not a card and has nowhere to be discarded TO — it would become
			// drawable. It rides the flood out, the same way it rides out every other apocalypse.
			if (card.HasComponent<CompanionComponent>())
				continue;

			// Marked damage is washed off with everything else: the card comes back whole, because
			// what returns from Discard is the card, not the body that was standing in the lane.
			state = state.UpdateObject(
				card.Id,
				card.WithComponentReplaced(card.Unit() with { Damage = 0 })
			);
			state = state.MoveObject(card.Id, discardId);
		}

		return state;
	}
}
