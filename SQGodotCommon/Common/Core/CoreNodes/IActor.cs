using System.Collections.Generic;

namespace CoreNodes;

/// <summary>
/// Describes an Actor that has actions that can be performed in a specific context
/// </summary>
/// <typeparam name="TContext"></typeparam>
public interface IActor<TContext>
{
	public List<ICoreAction<TContext>> GetPossibleActions();
}
