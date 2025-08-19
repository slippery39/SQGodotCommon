using System;
using System.Collections.Generic;
using Godot;

namespace Common;

/// <summary>
/// Used to process events one at a time.
/// Think of a game like Pokemon or Magic the gathering where multiple things might have happened at once, 
/// but we might only want to process them in the UI one at a time.
/// </summary>
public class EventsProcessor
{
	private Queue<GameEvent> EventQueue { get; set; } = new();
	private bool RunningEvent { get; set; } = false;
	public Dictionary<Type, Delegate> EventHandlers { get; set; } = new();

	public void RegisterHandler<T>(Action<T> handler)
		where T : GameEvent
	{
		EventHandlers[typeof(T)] = handler;
	}

	public void Process()
	{
		if (EventQueue.Count > 0 && !RunningEvent)
		{
			RunningEvent = true;
			var evt = EventQueue.Dequeue();

			ProcessEvent(evt);
		}
	}

	private void ProcessEvent(GameEvent evt)
	{
		Type eventType = evt.GetType();
		if (EventHandlers.ContainsKey(eventType))
		{
			var handler = EventHandlers[eventType];
			handler.DynamicInvoke(evt);
		}
		else
		{
			GD.Print($"Unhandled event: {evt.GetType()}");
			GD.Print(evt.Message);
			Next();
		}
	}

	public void Next()
	{
		RunningEvent = false;
	}

	public void AddStream(Observable<GameEvent> stream)
	{
		stream.Subscribe(x => EventQueue.Enqueue(x));
	}

	public void AddEvent(GameEvent evt)
	{
		EventQueue.Enqueue(evt);
	}

	public void AddEvents(IEnumerable<GameEvent> events)
	{
		foreach (var e in events)
		{
			EventQueue.Enqueue(e);
		}
	}
}
