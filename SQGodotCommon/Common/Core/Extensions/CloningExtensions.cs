using System.Collections.Generic;
using System.Linq;

namespace DeepCloneable;

/// <summary>
/// <a href="https://en.wikipedia.org/wiki/Curiously_recurring_template_pattern">CRTP</a>-based
/// interface to implement for objects that can create deep clones of themselves,
/// but can be abused if TSelf is not specified as the same type as the
/// implementing class.
/// </summary>
/// <typeparam name="TSelf"></typeparam>
public interface IDeepCloneable<TSelf>
	where TSelf : IDeepCloneable<TSelf>
{
	public TSelf DeepClone();
}

public static class DeepCloneExtensions
{
	/// <summary>
	/// Produces another list with the same objects deeply cloned using
	/// their implementation of <see cref="IDeepCloneable{TSelf}"/>.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="collection"></param>
	/// <returns></returns>
	public static IEnumerable<T> DeepClone<T>(this IEnumerable<T> collection)
		where T : IDeepCloneable<T>
	{
		return collection.Select(x => x.DeepClone());
	}
}
