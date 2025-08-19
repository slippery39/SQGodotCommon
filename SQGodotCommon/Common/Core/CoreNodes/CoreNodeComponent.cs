using DeepCloneable;

namespace CoreNodes;

public abstract class CoreNodeComponent : IDeepCloneable<CoreNodeComponent>
{
	public CoreNode Node { get; set; }
	public string Name { get; set; }
	public int ComponentId { get; set; }

	public virtual CoreNodeComponent DeepClone()
	{
		return MemberwiseClone() as CoreNodeComponent;
	}
}
