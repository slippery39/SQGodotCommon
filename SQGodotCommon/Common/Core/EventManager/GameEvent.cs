using System;

namespace Common;

public class GameEvent
{
	public int Id { get; set; }
	public DateTime Time { get; set; }
	public string Channel { get; set; } = "";
	public string Message { get; set; } = "";
}

public class GameEvent<T> : GameEvent
{
	public T Data { get; set; }
}
