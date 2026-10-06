namespace ImmutableGameObjects;

/// <summary>
/// **A triggered ability**: a component that, when a staged event matches, spawns actions. Pure data
/// like every component — a game subclasses it with its own condition and effect data, and
/// supplies what "matches" and "spawns" mean when it calls <see cref="Triggers.FireTriggers{T}"/>.
///
/// The caps live here because every game needs the same two, and they answer different card text:
/// lifetime is "once ever" (renown), per turn is "once each turn". Collapsing them silently turns a
/// once-ever ability into one that fires every turn.
/// </summary>
public abstract record TriggeredAbility : GameComponent
{
	/// <summary>Lifetime cap. 0 = unlimited. <see cref="TriggerCountTotal"/> is never reset.</summary>
	public int MaxTriggers { get; init; }

	/// <summary>Per-turn cap. 0 = unlimited. The game resets <see cref="TriggerCountThisTurn"/>.</summary>
	public int MaxTriggersPerTurn { get; init; }

	public int TriggerCountTotal { get; init; }
	public int TriggerCountThisTurn { get; init; }

	/// <summary>True when neither cap has been reached.</summary>
	public bool CanTrigger =>
		(MaxTriggers == 0 || TriggerCountTotal < MaxTriggers)
		&& (MaxTriggersPerTurn == 0 || TriggerCountThisTurn < MaxTriggersPerTurn);
}

public static class Triggers
{
	/// <summary>
	/// **Fires every <typeparamref name="T"/> on one object against a batch of staged events.** For
	/// each ability in component order, for each event in order: stop if a cap is reached, ask
	/// <paramref name="matches"/>, and spawn what <paramref name="spawn"/> returns.
	///
	/// Which objects and which abilities are live is the GAME's call — <paramref name="matches"/>
	/// can filter on anything (MTG uses it for its zone passes, where the pass is a label rather
	/// than the card's current zone). The game also owns the event list: this neither reads nor
	/// clears <see cref="GameState.PendingGameEvents"/>.
	///
	/// Two behaviours are load-bearing and match MtgCore's loop, which this replaced:
	/// - **Conditions see the state as it was when this object's evaluation began**, not the state
	///   after its own earlier spawns.
	/// - **Counts are written back only for a CAPPED ability.** An uncapped one leaves the object
	///   untouched, so firing it does not change state that searches and loop fingerprints read.
	///
	/// Remember the engine order when using this from a PostActionProcessor: spawned actions run
	/// BEFORE the post-processor, so a trigger fired by an action's resolution resolves after
	/// whatever that action spawned — see this project's CLAUDE.md.
	/// </summary>
	public static GameState FireTriggers<T>(
		this GameState state,
		int sourceId,
		IReadOnlyList<GameEvent> events,
		Func<T, GameEvent, GameState, bool> matches,
		Func<T, GameEvent, GameState, IEnumerable<GameAction>> spawn
	)
		where T : TriggeredAbility
	{
		if (events.Count == 0)
			return state;

		var evaluated = state;
		var source = state.GetObject(sourceId);
		var components = source.Components;
		var changed = false;

		// Indexed, because a capped ability is written back with its count and the index is the
		// only way to find it again.
		for (var i = 0; i < components.Length; i++)
		{
			if (components[i] is not T ability)
				continue;

			foreach (var e in events)
			{
				// Re-checked per event: two matches in one batch must not both fire a once-a-turn
				// ability.
				if (!ability.CanTrigger)
					break;

				if (!matches(ability, e, evaluated))
					continue;

				state = state.SpawnActions(spawn(ability, e, evaluated));

				if (ability.MaxTriggers > 0 || ability.MaxTriggersPerTurn > 0)
				{
					ability = ability with
					{
						TriggerCountTotal = ability.TriggerCountTotal + 1,
						TriggerCountThisTurn = ability.TriggerCountThisTurn + 1,
					};
					components = components.SetItem(i, ability);
					changed = true;
				}
			}
		}

		return changed
			? state.UpdateObject(sourceId, source with { Components = components })
			: state;
	}
}
