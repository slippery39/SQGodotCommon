using System;
using System.Collections.Generic;

namespace CoreNodes;

public class EventsSystem : CoreSystem
{
	/// <summary>
	/// The index of the last event index to occur
	/// </summary>
	public int LastEventIndex { get; set; } = -1;

	/// <summary>
	/// The id of the last event to occur
	/// </summary>
	public int LastEventId { get; set; } = 0;

	/// <summary>
	/// Stores all game events that have occured
	/// </summary>
	public List<CoreEvent> Events { get; set; } = new();

	public EventsSystem() { }

	public override void AddEvent(CoreEvent evt)
	{
		evt.TimeStamp = DateTime.UtcNow;
		evt.EventId = ++LastEventId;
		LastEventIndex = -1;
		Events.Add(evt);
	}
}
