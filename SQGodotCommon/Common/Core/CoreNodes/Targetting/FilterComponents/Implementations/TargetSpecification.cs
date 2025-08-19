using DeepCloneable;

namespace CoreNodes;

/// <summary>
/// Interface for a TargetSpecification.
/// Your game must implement the actual class.
///
/// TTargetContext should be an object that includes all relevant information as to how the game will filter its objects to satisfy the condition.
/// </summary>
/// <typeparam name="TTargetContext"></typeparam>
public interface ITargetSpecification<in TTargetContext>
{
	bool IsSatisfied(TTargetContext t);
}

//TODO - do we need this? Or can we use just the interface?
public abstract class TargetSpecification<T>
	: ITargetSpecification<T>,
		IDeepCloneable<TargetSpecification<T>>
{
	public abstract bool IsSatisfied(T t);

	public virtual TargetSpecification<T> DeepClone()
	{
		return MemberwiseClone() as TargetSpecification<T>;
	}
}

public abstract class OrSpecification<TContext>
	: TargetSpecification<TContext>,
		IDeepCloneable<OrSpecification<TContext>>
{
	public TargetSpecification<TContext> First { get; set; }
	public TargetSpecification<TContext> Second { get; set; }

	public override bool IsSatisfied(TContext t)
	{
		return First.IsSatisfied(t) || Second.IsSatisfied(t);
	}

	public override OrSpecification<TContext> DeepClone()
	{
		var newObj = MemberwiseClone() as OrSpecification<TContext>;
		newObj.First = First.DeepClone();
		newObj.Second = Second.DeepClone();

		return newObj;
	}
}

public abstract class AndSpecification<TContext>
	: TargetSpecification<TContext>,
		IDeepCloneable<OrSpecification<TContext>>
{
	public TargetSpecification<TContext> First { get; set; }
	public TargetSpecification<TContext> Second { get; set; }

	public override bool IsSatisfied(TContext t)
	{
		return First.IsSatisfied(t) && Second.IsSatisfied(t);
	}

	public override OrSpecification<TContext> DeepClone()
	{
		var newObj = MemberwiseClone() as OrSpecification<TContext>;
		newObj.First = First.DeepClone();
		newObj.Second = Second.DeepClone();

		return newObj;
	}
}
