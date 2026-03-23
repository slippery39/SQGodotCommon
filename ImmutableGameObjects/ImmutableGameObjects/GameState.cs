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

			if (topAction is PipelineAction pipeline)
				return pipeline.CurrentStep is ChoiceAction;

			return false;
		}
	}

	// ===== OBJECT QUERIES =====

	public GameObject GetObject(int id) => IdToGameObjectMap[id];

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

	// ===== OBJECT MUTATIONS =====

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

		if (IsDescendant(newParentId, objId))
			throw new InvalidOperationException("Cannot move object to its own descendant");

		var currentParentId = ChildToParent.TryGetValue(objId, out var parent) ? parent : 0;

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
			return this;

		var state = this;

		if (GetChildrenIds(objId).Any())
		{
			if (removeChildren)
			{
				foreach (var childId in GetChildrenIds(objId).ToList())
					state = state.RemoveObject(childId, removeChildren: true);
			}
			else
			{
				var newParentId = GetParent(objId) ?? 0;
				foreach (var childId in GetChildrenIds(objId).ToList())
					state = state.MoveObject(childId, newParentId);
			}
		}

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

	public GameState AddObjectRecursive(GameObject obj, int parentId = 0)
	{
		if (parentId != 0 && !IdToGameObjectMap.ContainsKey(parentId))
			throw new ArgumentException($"Parent with id {parentId} does not exist");

		var state = AddObject(obj, parentId).GameState;
		var newObjectId = state.NextId - 1;

		foreach (var child in obj.Children)
			state = state.AddObjectRecursive(child, newObjectId);

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

	public IEnumerable<int> GetAllDescendants(int parentId)
	{
		var descendants = new List<int>();
		var queue = new Queue<int>(GetChildrenIds(parentId));

		while (queue.Count > 0)
		{
			var currentId = queue.Dequeue();
			descendants.Add(currentId);
			foreach (var childId in GetChildrenIds(currentId))
				queue.Enqueue(childId);
		}

		return descendants;
	}

	public GameObject LoadHierarchy(int rootId)
	{
		if (!IdToGameObjectMap.TryGetValue(rootId, out var rootObject))
			throw new ArgumentException($"Object with id {rootId} does not exist");

		return rootObject.LoadFrom(this);
	}

	// ===== ACTION SYSTEM =====

	/// <summary>
	/// Add a single action to the top of the stack.
	/// </summary>
	public GameState AddAction(GameAction action) =>
		this with
		{
			ActionStack = ActionStack.Push(action),
		};

	/// <summary>
	/// Add multiple actions to the stack. They will execute in the order provided
	/// (first item in the list executes first).
	/// </summary>
	public GameState AddActions(IEnumerable<GameAction> actions)
	{
		var newStack = ActionStack;
		foreach (var action in actions.Reverse())
			newStack = newStack.Push(action);
		return this with { ActionStack = newStack };
	}

	/// <summary>
	/// Process the next action on the stack.
	/// Returns the new state and any events emitted during this step.
	/// </summary>
	public (GameState State, ImmutableList<GameEvent> Events) ProcessNextAction()
	{
		if (ActionStack.IsEmpty || IsWaitingForChoice)
			return (this, ImmutableList<GameEvent>.Empty);

		var action = ActionStack.Peek();
		var remainingStack = ActionStack.Pop();

		if (action is PipelineAction pipeline)
			return ExecutePipelineStep(pipeline, remainingStack);

		return ExecuteAction(action, remainingStack);
	}

	/// <summary>
	/// Advances a pipeline by one step and re-pushes it for the next tick.
	/// Returns the new state and any events emitted during this step.
	/// </summary>
	private (GameState State, ImmutableList<GameEvent> Events) ExecutePipelineStep(
		PipelineAction pipeline,
		ImmutableStack<GameAction> remainingStack
	)
	{
		if (pipeline.IsComplete)
			return (this with { ActionStack = remainingStack }, ImmutableList<GameEvent>.Empty);

		var step = pipeline.CurrentStep!;

		// Pause on choice — put pipeline back unchanged
		if (step is ChoiceAction)
			return (
				this with
				{
					ActionStack = remainingStack.Push(pipeline),
				},
				ImmutableList<GameEvent>.Empty
			);

		var stepWithContext = step with { InputContext = pipeline.PipelineContext };
		var result = stepWithContext.Execute(this);

		var updatedContext = pipeline.PipelineContext.SetItems(result.OutputData);
		var advancedPipeline = pipeline with
		{
			CurrentStepIndex = pipeline.CurrentStepIndex + 1,
			PipelineContext = updatedContext,
		};

		var newState = ApplyActionResult(result, remainingStack.Push(advancedPipeline));

		if (result.SpawnedActions.Any())
			newState = newState.AddActions(result.SpawnedActions);

		return (newState, result.Events);
	}

	/// <summary>
	/// Executes a regular (non-pipeline) action and applies its result.
	/// Returns the new state and any events emitted.
	/// </summary>
	private (GameState State, ImmutableList<GameEvent> Events) ExecuteAction(
		GameAction action,
		ImmutableStack<GameAction> remainingStack
	)
	{
		var result = action.Execute(this);
		var newState = ApplyActionResult(result, remainingStack);

		if (result.SpawnedActions.Any())
			newState = newState.AddActions(result.SpawnedActions);

		return (newState, result.Events);
	}

	/// <summary>
	/// Applies the game-object changes from an ActionResult onto this state,
	/// using the provided stack as the new ActionStack.
	/// </summary>
	private GameState ApplyActionResult(ActionResult result, ImmutableStack<GameAction> stack)
	{
		return this with
		{
			ActionStack = stack,
			NextId = result.GameState.NextId,
			IdToGameObjectMap = result.GameState.IdToGameObjectMap,
			ParentToChildren = result.GameState.ParentToChildren,
			ChildToParent = result.GameState.ChildToParent,
		};
	}

	/// <summary>
	/// Process all pending actions until the stack is empty or a choice is needed.
	/// Returns the final state and all events emitted across every step.
	/// </summary>
	public (GameState State, ImmutableList<GameEvent> Events) ProcessAllActions()
	{
		var state = this;
		var allEvents = ImmutableList<GameEvent>.Empty;

		while (state.HasPendingActions && !state.IsWaitingForChoice)
		{
			var (nextState, stepEvents) = state.ProcessNextAction();
			state = nextState;
			allEvents = allEvents.AddRange(stepEvents);
		}

		return (state, allEvents);
	}

	/// <summary>
	/// Returns the ChoiceAction currently blocking execution, if any.
	/// </summary>
	public ChoiceAction? GetPendingChoice()
	{
		if (!IsWaitingForChoice)
			return null;

		var topAction = ActionStack.Peek();

		if (topAction is ChoiceAction choice)
			return choice;

		if (topAction is PipelineAction pipeline)
			return pipeline.CurrentStep as ChoiceAction;

		return null;
	}

	/// <summary>
	/// Resolves the current pending ChoiceAction with the player's selection,
	/// stores the result in the pipeline context, and resumes execution.
	/// Returns the final state and all events emitted during resumed execution.
	/// </summary>
	public (GameState State, ImmutableList<GameEvent> Events) ResolveChoice(
		ImmutableList<int> selectedIds
	)
	{
		if (!IsWaitingForChoice)
			throw new InvalidOperationException("No pending choice to resolve");

		var topAction = ActionStack.Peek();

		if (topAction is not PipelineAction pipeline)
			throw new InvalidOperationException(
				"Standalone ChoiceActions on the stack are not supported. "
					+ "ChoiceActions must live inside a PipelineAction."
			);

		var choiceStep =
			pipeline.CurrentStep as ChoiceAction
			?? throw new InvalidOperationException("Current pipeline step is not a ChoiceAction");

		if (selectedIds.Count < choiceStep.MinChoices || selectedIds.Count > choiceStep.MaxChoices)
			throw new InvalidOperationException(
				$"Must select between {choiceStep.MinChoices} and {choiceStep.MaxChoices} options"
			);

		var choiceOutput = selectedIds.Count == 1 ? (object)selectedIds[0] : selectedIds;
		var updatedContext = pipeline.PipelineContext.SetItem(choiceStep.OutputKey, choiceOutput);
		var advancedPipeline = pipeline with
		{
			CurrentStepIndex = pipeline.CurrentStepIndex + 1,
			PipelineContext = updatedContext,
		};

		return (this with { ActionStack = ActionStack.Pop() })
			.AddAction(advancedPipeline)
			.ProcessAllActions();
	}
}
