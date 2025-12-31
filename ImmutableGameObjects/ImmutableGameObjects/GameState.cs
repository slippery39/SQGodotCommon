using System.Collections.Immutable;

namespace ImmutableGameObjects;

public record GameState
{
	public int NextId { get; init; } = 1;
	public ImmutableDictionary<int, GameObject> IdToGameObjectMap { get; init; } =
		ImmutableDictionary<int, GameObject>.Empty;
	public ImmutableDictionary<int, ImmutableList<int>> ParentToChildren { get; init; } =
		ImmutableDictionary<int, ImmutableList<int>>.Empty;
	public ImmutableDictionary<int, int> ChildToParent { get; init; } =
		ImmutableDictionary<int, int>.Empty;

	// Action system
	public ImmutableStack<GameAction> ActionStack { get; init; } = ImmutableStack<GameAction>.Empty;

	// Helper properties for action system
	public bool HasPendingActions => !ActionStack.IsEmpty;
	public bool IsWaitingForChoice
	{
		get
		{
			if (ActionStack.IsEmpty)
				return false;

			var topAction = ActionStack.Peek();

			if (topAction is ChoiceAction)
				return true;

			var pAction = topAction as PipelineAction;

			//Note that this does not support nested PipelineActions at this point... keep in mind for the future.
			if (pAction != null)
			{
				return pAction.GetCurrentAction is ChoiceAction;
			}

			return false;
		}
	}

	// ===== EVENT SYSTEM =====
	/// <summary>
	/// Events that occurred this frame. Should be cleared after UI processes them.
	/// </summary>
	public ImmutableList<GameEvent> EventLog { get; init; } = ImmutableList<GameEvent>.Empty;

	/// <summary>
	/// Adds an event to the log with the current battle time as timestamp
	/// </summary>
	public GameState AddEvent(GameEvent gameEvent)
	{
		return this with { EventLog = EventLog.Add(gameEvent) };
	}

	/// <summary>
	/// Clears all events - call this after UI has processed them
	/// </summary>
	public GameState ClearEvents()
	{
		return this with { EventLog = ImmutableList<GameEvent>.Empty };
	}

	/// <summary>
	/// Get all events of a specific type
	/// </summary>
	public IEnumerable<T> GetEvents<T>()
		where T : GameEvent
	{
		return EventLog.OfType<T>();
	}

	public GameObject GetObject(int id) => IdToGameObjectMap[id];

	/// <summary>
	/// Returns the first object in the state that is of Type T
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public T? GetObjectOfType<T>()
	{
		return IdToGameObjectMap.Values.OfType<T>().FirstOrDefault();
	}

	public IEnumerable<GameObject> GetChildren(int parentId) =>
		ParentToChildren.TryGetValue(parentId, out var childrenIds)
			? childrenIds.Select(id => IdToGameObjectMap[id])
			: Enumerable.Empty<GameObject>();

	public IEnumerable<int> GetChildrenIds(int parentId) =>
		ParentToChildren.TryGetValue(parentId, out var childrenIds)
			? childrenIds
			: Enumerable.Empty<int>();

	public int? GetParent(int childId) =>
		ChildToParent.TryGetValue(childId, out var parentId) ? parentId : null;

	public bool HasObject(int id) => IdToGameObjectMap.ContainsKey(id);

	public GameState UpdateObject(int id, GameObject newObject)
	{
		if (!IdToGameObjectMap.ContainsKey(id))
			throw new ArgumentException($"Object with id {id} does not exist");

		var updatedObject = newObject with { Id = id };
		return this with { IdToGameObjectMap = IdToGameObjectMap.SetItem(id, updatedObject) };
	}

	public GameState MoveObject(int objId, int newParentId)
	{
		if (!IdToGameObjectMap.ContainsKey(objId))
			throw new ArgumentException($"Object with id {objId} does not exist");

		if (newParentId != 0 && !IdToGameObjectMap.ContainsKey(newParentId))
			throw new ArgumentException($"Parent with id {newParentId} does not exist");

		// Check for circular reference
		if (IsDescendant(newParentId, objId))
			throw new InvalidOperationException("Cannot move object to its own descendant");

		var currentParentId = ChildToParent.TryGetValue(objId, out var parent) ? parent : 0;

		// Remove from current parent's children list
		var updatedParentToChildren = ParentToChildren;
		if (
			currentParentId != 0
			&& ParentToChildren.TryGetValue(currentParentId, out var currentSiblings)
		)
		{
			var newSiblings = currentSiblings.Remove(objId);
			updatedParentToChildren = newSiblings.IsEmpty
				? updatedParentToChildren.Remove(currentParentId)
				: updatedParentToChildren.SetItem(currentParentId, newSiblings);
		}

		// Add to new parent's children list
		if (newParentId != 0)
		{
			var newParentChildren = updatedParentToChildren.TryGetValue(
				newParentId,
				out var existingChildren
			)
				? existingChildren.Add(objId)
				: ImmutableList.Create(objId);
			updatedParentToChildren = updatedParentToChildren.SetItem(
				newParentId,
				newParentChildren
			);
		}

		// Update child to parent mapping
		var updatedChildToParent =
			newParentId == 0
				? ChildToParent.Remove(objId)
				: ChildToParent.SetItem(objId, newParentId);

		return this with
		{
			ParentToChildren = updatedParentToChildren,
			ChildToParent = updatedChildToParent,
		};
	}

	public GameState RemoveObject(int objId, bool removeChildren = false)
	{
		if (!IdToGameObjectMap.ContainsKey(objId))
			return this; // Already removed

		var state = this;

		// Handle children
		if (GetChildrenIds(objId).Any())
		{
			if (removeChildren)
			{
				// Recursively remove all children
				foreach (var childId in GetChildrenIds(objId).ToList())
				{
					state = state.RemoveObject(childId, removeChildren: true);
				}
			}
			else
			{
				// Move children to this object's parent
				var newParentId = GetParent(objId) ?? 0;
				foreach (var childId in GetChildrenIds(objId).ToList())
				{
					state = state.MoveObject(childId, newParentId);
				}
			}
		}

		// Remove from parent's children list
		var parentId = state.GetParent(objId);
		if (
			parentId.HasValue
			&& state.ParentToChildren.TryGetValue(parentId.Value, out var siblings)
		)
		{
			var newSiblings = siblings.Remove(objId);
			state = state with
			{
				ParentToChildren = newSiblings.IsEmpty
					? state.ParentToChildren.Remove(parentId.Value)
					: state.ParentToChildren.SetItem(parentId.Value, newSiblings),
			};
		}

		// Remove from all dictionaries
		return state with
		{
			IdToGameObjectMap = state.IdToGameObjectMap.Remove(objId),
			ParentToChildren = state.ParentToChildren.Remove(objId),
			ChildToParent = state.ChildToParent.Remove(objId),
		};
	}

	public (GameState GameState, T GameObject) AddObject<T>(T obj, int parentId = 0)
		where T : GameObject
	{
		if (parentId != 0 && !IdToGameObjectMap.ContainsKey(parentId))
			throw new ArgumentException($"Parent with id {parentId} does not exist");

		var id = NextId;
		var newObject = obj with { Id = id };

		var newObjectMap = IdToGameObjectMap.Add(id, newObject);

		var updatedParentToChildren = ParentToChildren;
		var updatedChildToParent = ChildToParent;

		// Add to parent's children if parent exists
		if (parentId != 0)
		{
			var parentChildren = ParentToChildren.TryGetValue(parentId, out var existingChildren)
				? existingChildren.Add(id)
				: ImmutableList.Create(id);
			updatedParentToChildren = updatedParentToChildren.SetItem(parentId, parentChildren);
			updatedChildToParent = updatedChildToParent.Add(id, parentId);
		}

		return (
			this with
			{
				NextId = NextId + 1,
				IdToGameObjectMap = newObjectMap,
				ParentToChildren = updatedParentToChildren,
				ChildToParent = updatedChildToParent,
			},
			newObject
		);
	}

	// Fixed version of AddObjectRecursive
	public GameState AddObjectRecursive(GameObject obj, int parentId = 0)
	{
		if (parentId != 0 && !IdToGameObjectMap.ContainsKey(parentId))
			throw new ArgumentException($"Parent with id {parentId} does not exist");

		// Add the main object first
		var state = AddObject(obj, parentId).GameState;
		var newObjectId = state.NextId - 1; // The ID that was just assigned

		// Recursively add children
		foreach (var child in obj.Children)
		{
			state = state.AddObjectRecursive(child, newObjectId);
		}

		return state;
	}

	private bool IsDescendant(int potentialDescendant, int ancestor)
	{
		if (potentialDescendant == ancestor)
			return true;

		if (!ChildToParent.TryGetValue(potentialDescendant, out var parentId))
			return false;

		return IsDescendant(parentId, ancestor);
	}

	// Helper method to get all descendants of an object
	public IEnumerable<int> GetAllDescendants(int parentId)
	{
		var descendants = new List<int>();
		var queue = new Queue<int>(GetChildrenIds(parentId));

		while (queue.Count > 0)
		{
			var currentId = queue.Dequeue();
			descendants.Add(currentId);

			foreach (var childId in GetChildrenIds(currentId))
			{
				queue.Enqueue(childId);
			}
		}

		return descendants;
	}

	// Method to get the root object with fully loaded hierarchy
	public GameObject LoadHierarchy(int rootId)
	{
		if (!IdToGameObjectMap.TryGetValue(rootId, out var rootObject))
			throw new ArgumentException($"Object with id {rootId} does not exist");

		return rootObject.LoadFrom(this);
	}

	// ===== ACTION SYSTEM METHODS =====

	/// <summary>
	/// Add an action to the stack.
	/// </summary>
	public GameState AddAction(GameAction action)
	{
		return this with { ActionStack = ActionStack.Push(action) };
	}

	/// <summary>
	/// Add multiple actions to the stack (they will execute in the order provided).
	/// </summary>
	public GameState AddActions(IEnumerable<GameAction> actions)
	{
		var newStack = ActionStack;
		// Push in reverse order so first action executes first
		foreach (var action in actions.Reverse())
		{
			newStack = newStack.Push(action);
		}
		return this with { ActionStack = newStack };
	}

	/// <summary>
	/// Process the next action on the stack.
	/// Returns a new state with the action processed.
	/// </summary>
	public GameState ProcessNextAction()
	{
		if (ActionStack.IsEmpty)
			return this;

		// Stop if we hit a ChoiceAction - it needs to be resolved first
		Console.WriteLine(
			$"ProcessNextAction - IsWaitingForChoice: {IsWaitingForChoice}, StackSize: {ActionStack.Count()}"
		);

		// Stop if we're waiting for a choice (could be in a pipeline)
		if (IsWaitingForChoice)
		{
			Console.WriteLine("Stopping - waiting for choice");
			return this;
		}

		var action = ActionStack.Peek();
		Console.WriteLine($"Processing action: {action.GetType().Name}");

		var remainingStack = ActionStack.Pop();

		var result = action.Execute(this);

		var newState = this with { ActionStack = remainingStack };

		// Update the game state from the result
		newState = newState with
		{
			NextId = result.GameState.NextId,
			IdToGameObjectMap = result.GameState.IdToGameObjectMap,
			ParentToChildren = result.GameState.ParentToChildren,
			ChildToParent = result.GameState.ChildToParent,
			EventLog = result.GameState.EventLog,
		};

		// Add any spawned actions
		if (result.SpawnedActions.Any())
		{
			newState = newState.AddActions(result.SpawnedActions);
		}

		return newState;
	}

	/// <summary>
	/// Process all actions until we need player input or stack is empty.
	/// </summary>
	public GameState ProcessAllActions()
	{
		var state = this;
		while (state.HasPendingActions && !state.IsWaitingForChoice)
		{
			state = state.ProcessNextAction();
		}
		return state;
	}

	/// <summary>
	/// Get the current choice action if waiting for one.
	/// </summary>
	/// <summary>
	/// Get the current choice action if waiting for one.
	/// </summary>
	public ChoiceAction? GetPendingChoice()
	{
		if (!IsWaitingForChoice)
			return null;

		var topAction = ActionStack.Peek();

		// Direct choice
		if (topAction is ChoiceAction choice)
			return choice;

		// Choice inside pipeline
		if (
			topAction is PipelineAction pipeline
			&& pipeline.CurrentStepIndex < pipeline.Steps.Count
		)
		{
			return pipeline.Steps[pipeline.CurrentStepIndex] as ChoiceAction;
		}

		return null;
	}

	/// <summary>
	/// Resolve the current ChoiceAction with player's selection.
	/// Works for both standalone choices and choices in pipelines.
	/// </summary>
	public GameState ResolveChoice(ImmutableList<int> selectedIds)
	{
		if (!IsWaitingForChoice)
		{
			throw new Exception("No pending choice to resolve");
		}

		var topAction = ActionStack.Peek();

		// Case 1: Direct ChoiceAction on stack
		if (topAction is ChoiceAction choiceAction)
		{
			throw new InvalidOperationException(
				"Internal error: Standalone ChoiceAction on stack. Choices must be in pipelines."
			);
		}

		// Case 2: PipelineAction with ChoiceAction as current step
		// Case 2: PipelineAction with ChoiceAction as current step
		if (topAction is PipelineAction pipeline)
		{
			var choiceStep = pipeline.Steps[pipeline.CurrentStepIndex] as ChoiceAction;

			if (choiceStep == null)
			{
				throw new Exception("Current step is not a ChoiceAction");
			}

			if (
				selectedIds.Count < choiceStep.MinChoices
				|| selectedIds.Count > choiceStep.MaxChoices
			)
			{
				throw new Exception(
					$"Must select between {choiceStep.MinChoices} and {choiceStep.MaxChoices} options"
				);
			}

			// Pop pipeline, store choice in context, advance past choice
			var newState = this with
			{
				ActionStack = ActionStack.Pop(),
			};

			var choiceOutput = selectedIds.Count == 1 ? selectedIds[0] : (object)selectedIds;
			var updatedContext = pipeline.PipelineContext.Add(choiceStep.OutputKey, choiceOutput);

			var advancedPipeline = pipeline with
			{
				CurrentStepIndex = pipeline.CurrentStepIndex + 1,
				PipelineContext = updatedContext,
			};

			// Put advanced pipeline back and continue
			newState = newState.AddAction(advancedPipeline);
			return newState.ProcessAllActions();
		}

		throw new Exception("Unexpected state in ResolveChoice");
	}
}
