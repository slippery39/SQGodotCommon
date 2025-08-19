using System;
using System.Collections.Generic;
using DynamicExpresso;

namespace Core;

/// <summary>
/// Should be registered with our game manager.
/// </summary>
/// <typeparam name="TContext"></typeparam>
public class DynamicScriptingService<TContext>
	where TContext : class
{
	private Dictionary<string, Action<TContext>> ActionsCache { get; set; } = new();

	public Action<TContext> CreateAction(string expression)
	{
		if (!ActionsCache.ContainsKey(expression))
		{
			var options =
				InterpreterOptions.Default
				| InterpreterOptions.LambdaExpressions
				| InterpreterOptions.CommonTypes
				| InterpreterOptions.PrimitiveTypes
				| InterpreterOptions.SystemKeywords;

			var interpreter = new Interpreter(options)
			// .Reference(typeof(Technique)) //can add references like this
			// .Reference(typeof(Monster))
			// .Reference(typeof(MonsterStat))
			.EnableAssignment(AssignmentOperators.All);

			var action = interpreter.ParseAsDelegate<Action<TContext>>(expression, "ctx");
			ActionsCache.Add(expression, action);
		}

		return ActionsCache[expression];
	}
}
