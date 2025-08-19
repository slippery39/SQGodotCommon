namespace CoreNodes;

public abstract class CoreSystem : CoreNode
{
	/// <summary>
	/// Adds an event. Override this in any EventsSystems
	/// </summary>
	/// <param name="evt"></param>
	public virtual void AddEvent(CoreEvent evt)
	{
		Root.AddEvent(evt);
	}
}
