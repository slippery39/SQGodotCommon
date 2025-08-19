using System.Collections.Generic;

namespace CoreNodes;

public abstract class BuilderBase<T>
	where T : class, new()
{
	protected T _object = new();

	public virtual void Reset()
	{
		_object = new();
	}

	public virtual T Build()
	{
		var tempObj = _object;
		Reset();
		return tempObj;
	}
}

public abstract class CoreNodeBuilder<TBuilder, TNode> : BuilderBase<TNode>
	where TNode : CoreNode, new()
	where TBuilder : CoreNodeBuilder<TBuilder, TNode>
{
	public TBuilder WithNodeName(string name)
	{
		_object.NodeName = name;
		return (TBuilder)this;
	}

	public TBuilder WithTag(string tag)
	{
		if (_object.Tags == null)
			_object.Tags = new List<string>();

		_object.Tags.Add(tag);
		return (TBuilder)this;
	}
}
