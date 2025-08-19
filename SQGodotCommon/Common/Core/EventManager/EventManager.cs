using System;

namespace Common;

public class EventManager
{
	protected Subject<GameEvent> _eventsSubject { get; set; } = new Subject<GameEvent>();
	public Observable<GameEvent> Events => _eventsSubject;
	private int _nextEventId = 1;

	public void AddEvent(string channel)
	{
		var evt = new GameEvent();
		evt.Id = _nextEventId++;
		evt.Time = DateTime.Now;
		evt.Channel = channel;
		_eventsSubject.OnNext(evt);
	}

	public void AddEvent<T>(string channel, T data)
	{
		var evt = new GameEvent<T>();
		evt.Id = _nextEventId++;
		evt.Time = DateTime.Now;
		evt.Data = data;
		evt.Channel = channel;
		_eventsSubject.OnNext(evt);
	}
}
