using System;


namespace Core;

public static partial class R3X
{
	/// <summary>
	/// Creates an observable that executes an action returned by the provided function
	/// when subscribed to, with the disposable returned to allow cleanup of the action.
	/// </summary>
	/// <typeparam name="T">The type of data that the observable will emit.</typeparam>
	/// <param name="func">A function that takes an observer and returns an action to execute.</param>
	/// <returns>An observable that emits values of type <typeparamref name="T"/> and has a disposable for cleanup.</returns>
	public static Observable<T> Create<T>(Func<Observer<T>, Action> func)
	{
		return Observable.Create<T>(obs =>
		{
			return Disposable.Create(func(obs));
		});
	}

	/// <summary>
	/// Creates an observable that executes a given action on an observer when subscribed to,
	/// and returns an empty disposable after the action is executed.
	/// </summary>
	/// <typeparam name="T">The type of data that the observable will emit.</typeparam>
	/// <param name="action">The action to execute when an observer subscribes.</param>
	/// <returns>An observable that emits values of type <typeparamref name="T"/> and returns an empty disposable for cleanup.</returns>
	public static Observable<T> Create<T>(Action<Observer<T>> action)
	{
		return Observable.Create<T>(obs =>
		{
			action(obs);
			return Disposable.Empty;
		});
	}
}
