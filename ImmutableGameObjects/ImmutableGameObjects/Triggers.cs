using System.Collections.Immutable;

namespace ImmutableGameObjects;

/// <summary>
/// **Does this event fire this ability?** Pure data: a game subclasses it once per kind of event
/// ("a card was discarded", "a foe died"). Lifted from MtgCore's <c>TriggerCondition</c>, with the
/// context cut down to what any game has: the state, and the object that holds the ability.
/// </summary>
public abstract record TriggerRule
{
	public abstract bool Matches(GameEvent e, GameState s, int sourceId);
}

/// <summary>
/// An effect that needs to know what fired it: the object holding the ability, and the event.
/// Effects that do not implement this are spawned exactly as authored.
/// </summary>
public interface ITriggerBound
{
	GameAction Bind(int sourceId, GameEvent trigger);
}

/// <summary>
/// **A triggered ability**: when a staged event matches <see cref="When"/>, its effects are spawned
/// in order. Attach it to any GameObject; the game decides which objects are ACTIVE by the sources
/// it hands to <see cref="Triggers.FireTriggers"/> (MTG: the battlefield).
/// </summary>
public record Trigger : GameComponent
{
	public string Name { get; init; } = "";
	public TriggerRule When { get; init; } = null!;
	public ImmutableList<GameAction> Effects { get; init; } = [];

	/// <summary>How often it may fire each turn. 0 = unlimited.</summary>
	public int MaxPerTurn { get; init; }

	public int FiredThisTurn { get; init; }

	public bool CanFire => MaxPerTurn == 0 || FiredThisTurn < MaxPerTurn;
}

public static class Triggers
{
	/// <summary>
	/// **Stages an event for triggers.** An <see cref="ActionResult"/>'s events are for the front end
	/// and no ability ever sees them; an action whose event should be triggerable stages it here too.
	/// </summary>
	public static GameState StageEvent(this GameState s, GameEvent e) =>
		s with
		{
			PendingGameEvents = s.PendingGameEvents.Add(e),
		};

	/// <summary>
	/// **Fires every <see cref="Trigger"/> on the sources that a staged event matches**, spawning its
	/// effects, then clears the staged events. Call it from the game's PostActionProcessor. Events
	/// the spawned effects stage are seen on the next pass, so chains work and never loop within one.
	/// </summary>
	public static GameState FireTriggers(this GameState s, IEnumerable<int> sourceIds)
	{
		var events = s.PendingGameEvents;
		s = s with { PendingGameEvents = [] };
		if (events.IsEmpty)
			return s;

		foreach (var id in sourceIds.ToList())
		{
			var source = s.GetObject(id);
			var components = source.Components;
			var fired = false;

			// Indexed, because a fired ability is written back with its count.
			for (var i = 0; i < components.Length; i++)
			{
				if (components[i] is not Trigger trigger)
					continue;

				foreach (var e in events)
				{
					// Re-checked per event: two matches in one batch must not both fire a once-a-turn ability.
					if (!trigger.CanFire)
						break;
					if (!trigger.When.Matches(e, s, id))
						continue;

					s = s.SpawnActions(
						trigger.Effects.Select(a =>
							a is ITriggerBound bound ? bound.Bind(id, e) : a
						)
					);
					trigger = trigger with { FiredThisTurn = trigger.FiredThisTurn + 1 };
					components = components.SetItem(i, trigger);
					fired = true;
				}
			}

			if (fired)
				s = s.UpdateObject(id, source with { Components = components });
		}
		return s;
	}

	/// <summary>Resets the per-turn count of every <see cref="Trigger"/> on the object.</summary>
	public static GameState ResetTriggers(this GameState s, int id)
	{
		var source = s.GetObject(id);
		if (!source.HasComponent<Trigger>())
			return s;

		return s.UpdateObject(
			id,
			source with
			{
				Components =
				[
					.. source.Components.Select(c =>
						c is Trigger t ? t with { FiredThisTurn = 0 } : c
					),
				],
			}
		);
	}
}
