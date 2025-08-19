using System;
using DeepCloneable;

namespace CoreNodes;

public class TargetInfo<T> : IDeepCloneable<TargetInfo<T>>
{
	/// <summary>
	/// Which entities are the ones that could potentially chosen by the effect
	/// </summary>
	public TargetSpecification<T> Specification { get; set; }

	/// <summary>
	/// How the entities are actually chosen
	/// </summary>
	public TargetMode TargetMode { get; set; }

	/// <summary>
	/// The total amount of entities that will have the effect applied to.
	/// Only applies to TargetModes of Target or Random.
	/// </summary>
	public int AmountToApplyEffect { get; set; } = 1;

	[Obsolete(
		"We don't know if we are actually going to use this or not... Need more time to think"
	)]
	public OwnerType OwnerType { get; set; } = OwnerType.Any;

	public bool NeedsTargets() => TargetMode == TargetMode.Target;

	public TargetInfo<T> DeepClone()
	{
		var clone = MemberwiseClone() as TargetInfo<T>;
		clone.Specification = Specification.DeepClone();
		return clone;
	}
}
