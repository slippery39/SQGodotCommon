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
	/// <summary>
	/// Runs a battle apocalypse's authored effects against this GameState.
	///
	/// **This used to be a switch with one arm per scenario. It is a runner now** — what Flood does
	/// is a list of DoomEffects in `ScenarioLibrary`, so a new battle-scope apocalypse is a single
	/// library entry and no code at all.
	///
	/// Effects are EXECUTED inline rather than spawned, which is the one place this departs from how
	/// effects run everywhere else. `DoomPreviewer` calls straight into here and diffs the board to
	/// tell the player what the firing would do; a spawned action would not have run yet and the
	/// preview would report that nothing happens. Inline keeps one definition of Flood serving both
	/// the real firing and the dial that predicts it — which is the rule that stops the preview
	/// drifting from the apocalypse.
	///
	/// **It returns the EVENTS as well, and that was a real bug for as long as it did not.** Running
	/// an action inline and keeping only its state threw away everything the firing raised — the
	/// damage Detonation deals, the cards Flood takes — so the board had no way to animate an
	/// apocalypse and DoomUI.md's standing rule (nothing may change without saying so) could not be
	/// met by a battle doom at all. Found when a taking raised an event nobody could see.
	/// </summary>
	public static (GameState State, ImmutableList<GameEvent> Events) Apply(
		GameState state,
		DoomScenario scenario
	)
	{
		var definition = ScenarioLibrary.Of(scenario);

		if (definition.Scope != DoomScope.Battle)
			throw new InvalidOperationException(
				$"{scenario} is a {definition.Scope} scenario and belongs in DoomTransforms, not "
					+ "here. A scenario in the wrong hook does nothing at all and looks exactly "
					+ "like one that worked."
			);

		if (definition.BattleEffects.IsEmpty)
			throw new InvalidOperationException(
				$"{scenario} is battle scope and declares no effects, so firing it would change "
					+ "nothing. An apocalypse that silently does nothing is the failure this "
					+ "codebase keeps rediscovering."
			);

		var events = ImmutableList<GameEvent>.Empty;

		foreach (var effect in definition.BattleEffects)
		{
			// The doom is not an object on the board, so there is no source id — "self" and
			// "my lane" correctly resolve to nothing for an apocalypse.
			var targets = DoomTargeting.Resolve(state, effect.Target, sourceId: 0);

			var action = effect.Template is EffectAction typed
				? typed with
				{
					TargetIds = targets,
				}
				: effect.Template;

			var result = action.Execute(state);
			state = result.GameState;
			events = events.AddRange(result.Events);
		}

		return (state, events);
	}
}
