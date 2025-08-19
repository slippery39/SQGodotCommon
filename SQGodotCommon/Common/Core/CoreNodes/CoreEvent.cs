using System;

namespace CoreNodes;

public abstract class CoreEvent
{
	public int EventId { get; set; }
	public DateTime TimeStamp { get; set; }
}
