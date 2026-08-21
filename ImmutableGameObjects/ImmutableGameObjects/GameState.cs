using System.Collections.Immutable;

namespace ImmutableGameObjects;

public record GameState
{
	public int NextId { get; init; } = 1;

	/// <summary>
	/// Seed used to drive in-game randomness (e.g. random discard, random targeting).
	/// 0 = unseeded (truly random). Set once at game start; advances with each random pick.
	/// </summary>
	public int RngSeed { get; init; } = 0;
	public ImmutableDictionary<int, GameObject> IdToGameObjectMap { get; init; } =
		ImmutableDictionary<int, GameObject>.Empty;
	public ImmutableDictionary<int, ImmutableList<int>> ParentToChildren { get; init; } =
		ImmutableDictionary<int, ImmutableList<int>>.Empty;
	public ImmutableDictionary<int, int> ChildToParent { get; init; } =
		ImmutableDictionary<int, int>.Empty;

	public ImmutableStack<GameAction> ActionStack { get; init; } = ImmutableStack<GameAction>.Empty;

	/// <summary>
	/// Staging area for actions spawned during Execute().
	/// Actions placed here by SpawnAction/SpawnActions are flushed onto the
	/// ActionStack by the executor after every action or pipeline step completes.
	/// This allows both standalone actions and pipeline steps to safely spawn
	/// follow-up actions without clobbering the stack mid-execution.
	/// </summary>
	public ImmutableList<GameAction> SpawnQueue { get; init; } = ImmutableList<GameAction>.Empty;

	/// <summary>
	/// Game events staged during action execution for consumption by the
	/// PostActionProcessor after a resolution scope closes.
	///
	/// Actions that produce trigger-relevant events (creature dies, enters
	/// battlefield, attacks) append to this list explicitly. CheckStateBasedEffectsAction
	/// reads the list, evaluates all TriggeredAbilityComponents against it, spawns
	/// any triggered abilities, then clears it.
	///
	/// Cleared every time the PostActionProcessor runs — never accumulates
	/// across multiple resolutions.
	/// </summary>
	public ImmutableList<GameEvent> PendingGameEvents { get; init; } =
		ImmutableList<GameEvent>.Empty;

	/// <summary>
	/// An optional action template that is automatically pushed onto the stack after
	/// every non-post-processor action resolves (standalone actions and completed pipelines).
	///
	/// It does NOT fire between individual steps of a pipeline — only after the whole
	/// pipeline completes. This mirrors MTG's rule of checking state-based effects after
	/// each spell or ability fully resolves, not between its individual steps.
	///
	/// The processor must set IsPostProcessor = true on its GameAction subclass to prevent
	/// itself from triggering another post-processing cycle after it runs.
	///
	/// Set this once at game setup (e.g. CheckStateBasedEffectsAction for MTG).
	/// Leave null for games or tests that don't need post-action processing.
	/// </summary>
	public GameAction? PostActionProcessor { get; init; } = null;

	/// <summary>
	/// When true, the PostActionProcessor is skipped after each action resolves.
	///
	/// General-purpose suppression flag — the game layer decides when to set and
	/// clear it. GameState itself has no opinion on when or why it is used.
	///
	/// In MtgCore this is set by ResolveSpellAction and ActivateAbilityAction to
	/// defer state-based effect checks until a spell or ability fully resolves.
	/// Cleared by EndResolutionScopeAction.
	/// </summary>
	public bool SuppressPostProcessor { get; init; } = false;

	// ===== WELL-KNOWN OBJECT REGISTRY =====

	/// <summary>
	/// Maps string keys to object IDs for objects that need O(1) access throughout the game.
	/// Populated once at game setup; never modified during play.
	/// The game layer (e.g. MtgCore) defines the key constants — this dictionary is generic.
	/// </summary>
	public ImmutableDictionary<string, int> WellKnownIds { get; init; } =
		ImmutableDictionary<string, int>.Empty;

	/// <summary>
	/// Returns the ID registered under the given key. Throws if the key is not registered.
	/// </summary>
	public int GetWellKnownId(string key) => WellKnownIds[key];

	/// <summary>
	/// Returns a new GameState with the given key registered to the given ID.
	/// </summary>
	public GameState RegisterWellKnownId(string key, int id) =>
		this with
		{
			WellKnownIds = WellKnownIds.SetItem(key, id),
		};

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

	public IEnumerable<GameObject> GetChildren(int parentId)
	{
		if (!ParentToChildren.TryGetValue(parentId, out var childrenIds))
			yield break;
		foreach (var id in childrenIds)
			yield return IdToGameObjectMap[id];
	}

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

	/// <summary>
	/// Moves an object to the front (index 0) of a new parent's children list.
	/// Equivalent to MoveObject but inserts at the front rather than appending.
	/// </summary>
	public GameState MoveObjectToFront(int objId, int newParentId)
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
				? existingChildren.Insert(0, objId)
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
	/// Adds an action to the top of the stack after validating it.
	/// Throws InvalidOperationException if ValidateAdd fails.
	/// Use TryAddAction if you want to handle failure gracefully.
	/// </summary>
	public GameState AddAction(GameAction action)
	{
		var validation = action.ValidateAdd(this);
		if (!validation.IsValid)
			throw new InvalidOperationException(
				$"Action {action.GetType().Name} failed validation: {validation.Reason}"
			);

		return this with
		{
			ActionStack = ActionStack.Push(action),
		};
	}

	/// <summary>
	/// Attempts to add an action to the stack.
	/// Returns (unchanged state, false) if ValidateAdd fails, rather than throwing.
	/// </summary>
	public (GameState State, bool Success) TryAddAction(GameAction action)
	{
		var validation = action.ValidateAdd(this);
		if (!validation.IsValid)
			return (this, false);

		return (this with { ActionStack = ActionStack.Push(action) }, true);
	}

	/// <summary>
	/// Adds multiple actions to the stack with validation on each.
	/// They will execute in the order provided (first item executes first).
	/// Throws if any action fails ValidateAdd.
	/// </summary>
	public GameState AddActions(IEnumerable<GameAction> actions)
	{
		var state = this;
		foreach (var action in actions)
			state = state.AddAction(action);
		return state;
	}

	/// <summary>
	/// Pushes actions onto the stack without validation.
	/// Used internally for spawned actions, which are created by already-executing
	/// actions and assumed to be valid.
	/// </summary>
	private GameState PushActionsInternal(IEnumerable<GameAction> actions)
	{
		var newStack = ActionStack;
		foreach (var action in actions.Reverse())
			newStack = newStack.Push(action);
		return this with { ActionStack = newStack };
	}

	/// <summary>
	/// Stages a single action for spawning. The action will be pushed onto the
	/// ActionStack by the executor after the current action or pipeline step finishes.
	/// Call this from within Execute() to queue follow-up work safely.
	/// </summary>
	public GameState SpawnAction(GameAction action) =>
		this with
		{
			SpawnQueue = SpawnQueue.Add(action),
		};

	/// <summary>
	/// Stages multiple actions for spawning. First action in the list executes first.
	/// </summary>
	public GameState SpawnActions(IEnumerable<GameAction> actions) =>
		this with
		{
			SpawnQueue = SpawnQueue.AddRange(actions),
		};

	/// <summary>
	/// Flushes all staged actions from SpawnQueue onto the ActionStack and clears the queue.
	/// Called by the executor after every action or pipeline step.
	/// </summary>
	private GameState FlushSpawnQueue()
	{
		if (SpawnQueue.IsEmpty)
			return this;

		var newStack = ActionStack;
		foreach (var action in SpawnQueue.Reverse())
			newStack = newStack.Push(action);

		return this with
		{
			ActionStack = newStack,
			SpawnQueue = ImmutableList<GameAction>.Empty,
		};
	}

	/// <summary>
	/// Process the next action on the stack.
	/// Returns the new state and any events emitted during this step.
	/// If the action fails ValidateResolve it is removed from the stack
	/// and an ActionValidationFailedEvent is emitted.
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
	/// The PostActionProcessor fires only when the pipeline is fully complete,
	/// not between individual steps.
	/// </summary>
	private (GameState State, ImmutableList<GameEvent> Events) ExecutePipelineStep(
		PipelineAction pipeline,
		ImmutableStack<GameAction> remainingStack
	)
	{
		if (pipeline.IsComplete)
		{
			var completedState = this with { ActionStack = remainingStack };

			// Pipeline finished — fire the post-processor if one is set
			if (PostActionProcessor != null && !pipeline.IsPostProcessor && !SuppressPostProcessor)
				completedState = completedState.SpawnAction(PostActionProcessor);

			// Flush the staged post-processor onto the stack so it actually runs — the
			// standalone-action path does the same. Without this the processor is stranded
			// in SpawnQueue and never executes after a completed pipeline.
			completedState = completedState.FlushSpawnQueue();

			return (completedState, ImmutableList<GameEvent>.Empty);
		}

		var step = pipeline.CurrentStep!;

		if (step is ChoiceAction choice)
		{
			// Refresh options from game state and pipeline context before pausing.
			var freshOptions = choice.GetOptions(this, pipeline.PipelineContext);
			var refreshedChoice = choice with { Options = freshOptions };
			var pipelineWithRefreshedChoice = pipeline with
			{
				Steps = pipeline.Steps.SetItem(pipeline.CurrentStepIndex, refreshedChoice),
			};

			return (
				this with
				{
					ActionStack = remainingStack.Push(pipelineWithRefreshedChoice),
				},
				ImmutableList<GameEvent>.Empty
			);
		}

		// Validate and execute the current step
		var stepWithContext = step with
		{
			InputContext = pipeline.PipelineContext,
		};
		var validation = stepWithContext.ValidateResolve(this);

		if (!validation.IsValid)
		{
			var failedEvent = new ActionValidationFailedEvent
			{
				Action = stepWithContext,
				Reason = validation.Reason,
			};

			var advancedPipeline = pipeline with
			{
				CurrentStepIndex = pipeline.CurrentStepIndex + 1,
			};

			return (
				this with
				{
					ActionStack = remainingStack.Push(advancedPipeline),
				},
				ImmutableList.Create<GameEvent>(failedEvent)
			);
		}

		// Pass post-pop state so the step has an accurate stack view
		var postPopState = this with
		{
			ActionStack = remainingStack,
		};
		var result = stepWithContext.Execute(postPopState);
		var updatedContext = pipeline.PipelineContext.SetItems(result.OutputData);
		var advancedPipelineAfterExecution = pipeline with
		{
			CurrentStepIndex = pipeline.CurrentStepIndex + 1,
			PipelineContext = updatedContext,
		};

		// If the next step is a ChoiceAction, refresh its options now before returning
		var finalPipeline = advancedPipelineAfterExecution;
		if (!finalPipeline.IsComplete && finalPipeline.CurrentStep is ChoiceAction nextChoice)
		{
			var freshOptions = nextChoice.GetOptions(
				result.GameState,
				finalPipeline.PipelineContext
			);
			var refreshedNextChoice = nextChoice with { Options = freshOptions };
			finalPipeline = finalPipeline with
			{
				Steps = finalPipeline.Steps.SetItem(
					finalPipeline.CurrentStepIndex,
					refreshedNextChoice
				),
			};
		}

		// Reconcile the stack — push the advanced pipeline back on top of remaining.
		// Any actions in SpawnQueue survive this because we only replace ActionStack.
		var newState = result.GameState with
		{
			ActionStack = remainingStack.Push(finalPipeline),
		};

		// Flush spawned actions on top of the reconciled stack
		newState = newState.FlushSpawnQueue();

		return (newState, result.Events);
	}

	/// <summary>
	/// Executes a regular (non-pipeline) action after validating it can still resolve.
	/// If ValidateResolve fails, the action is dropped and a failure event is emitted.
	/// After a successful execution, the PostActionProcessor is pushed if one is set
	/// and the action that just ran is not itself a post-processor.
	/// </summary>
	private (GameState State, ImmutableList<GameEvent> Events) ExecuteAction(
		GameAction action,
		ImmutableStack<GameAction> remainingStack
	)
	{
		var validation = action.ValidateResolve(this);

		if (!validation.IsValid)
		{
			var failedEvent = new ActionValidationFailedEvent
			{
				Action = action,
				Reason = validation.Reason,
			};

			return (
				this with
				{
					ActionStack = remainingStack,
				},
				ImmutableList.Create<GameEvent>(failedEvent)
			);
		}

		// Pass the post-pop state into Execute so actions have an accurate view
		// of the stack and can call SpawnAction directly if needed
		var postPopState = this with
		{
			ActionStack = remainingStack,
		};
		var result = action.Execute(postPopState);

		// Fire the post-processor before flushing so it goes on top of any spawned actions
		var newState = result.GameState;
		if (PostActionProcessor != null && !action.IsPostProcessor && !SuppressPostProcessor)
			newState = newState.SpawnAction(PostActionProcessor);

		// Flush any actions staged in SpawnQueue onto the ActionStack
		newState = newState.FlushSpawnQueue();

		return (newState, result.Events);
	}

	/// <summary>
	/// Hard ceiling on how many actions a single ProcessAllActions call may resolve.
	///
	/// Deliberately enormous — a wide-board mass effect or a high storm count resolves in the
	/// hundreds, so nothing legitimate comes near this. It exists solely to convert a
	/// SELF-FEEDING loop from an unrecoverable freeze into a diagnosable exception.
	///
	/// A card whose trigger produces the very event it triggers on ("whenever a creature enters,
	/// create a creature") spawns work forever, and this loop had no exit: not a slow game but a
	/// wedged thread. It hung a training batch for hours and would freeze the Godot UI outright,
	/// losing the player's game with nothing logged. Both callers already handle exceptions —
	/// GameRunner flags the game and captures a snapshot, MtgGameScene writes a crash snapshot —
	/// so throwing is strictly better than hanging.
	/// </summary>
	public const int MaxActionsPerResolution = 10_000;

	/// <summary>
	/// Process all pending actions until the stack is empty or a choice is needed.
	/// Returns the final state and all events emitted across every step.
	/// </summary>
	public (GameState State, ImmutableList<GameEvent> Events) ProcessAllActions()
	{
		var state = this;
		var allEvents = ImmutableList<GameEvent>.Empty;
		var processed = 0;

		while (state.HasPendingActions && !state.IsWaitingForChoice)
		{
			if (++processed > MaxActionsPerResolution)
				throw new InvalidOperationException(
					$"Action resolution exceeded {MaxActionsPerResolution} steps — a self-feeding "
						+ "loop, most likely a trigger that produces the event it triggers on. "
						+ $"Last action: {(state.ActionStack.IsEmpty ? "none" : state.ActionStack.Peek().GetType().Name)}"
				);

			var (nextState, stepEvents) = state.ProcessNextAction();
			state = nextState;
			allEvents = allEvents.AddRange(stepEvents);
		}

		return (state, allEvents);
	}

	/// <summary>
	/// Returns the ChoiceAction currently blocking execution, if any, with its options
	/// resolved against the current state.
	///
	/// Options are computed here rather than trusted from the stored step: a pipeline whose
	/// FIRST step is a ChoiceAction pauses before any step executes, so the eager refresh in
	/// ExecutePipelineStep never runs on it and its stored Options are still empty. Resolving
	/// at read time is correct for every case — ResolveChoice validates the same way.
	/// </summary>
	public ChoiceAction? GetPendingChoice()
	{
		if (!IsWaitingForChoice)
			return null;

		var topAction = ActionStack.Peek();

		if (topAction is ChoiceAction choice)
			return choice with
			{
				Options = choice.GetOptions(this, ImmutableDictionary<string, object>.Empty),
			};

		if (topAction is PipelineAction pipeline && pipeline.CurrentStep is ChoiceAction step)
			return step with { Options = step.GetOptions(this, pipeline.PipelineContext) };

		return null;
	}

	/// <summary>
	/// The player who must answer the pending choice, or 0 when there is no choice or its owner
	/// cannot be determined. Callers treat 0 as "fall back to the active player" — a choice with
	/// no owner still has to be answerable by someone, or it wedges the action stack forever.
	///
	/// Read the owner from HERE rather than inferring it from whose turn it is. A triggered
	/// ability asks its question whenever it triggers, which is routinely on the opponent's turn.
	/// </summary>
	public int GetPendingChoiceDecidingPlayerId()
	{
		if (!IsWaitingForChoice)
			return 0;

		var topAction = ActionStack.Peek();

		return topAction switch
		{
			ChoiceAction choice => choice.GetDecidingPlayerId(
				this,
				ImmutableDictionary<string, object>.Empty
			),
			PipelineAction { CurrentStep: ChoiceAction step } pipeline => step.GetDecidingPlayerId(
				this,
				pipeline.PipelineContext
			),
			_ => 0,
		};
	}

	/// <summary>
	/// Resolves the current pending ChoiceAction with the player's selection.
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

		var options = choiceStep.GetOptions(this, pipeline.PipelineContext);
		var effectiveMin = Math.Min(choiceStep.MinChoices, options.Count);

		if (selectedIds.Count < effectiveMin || selectedIds.Count > choiceStep.MaxChoices)
			throw new InvalidOperationException(
				$"Must select between {choiceStep.MinChoices} and {choiceStep.MaxChoices} options"
			);

		object choiceOutput = selectedIds.Count == 1 ? selectedIds[0] : selectedIds;
		var updatedContext =
			selectedIds.Count == 0
				? pipeline.PipelineContext
				: pipeline.PipelineContext.SetItem(choiceStep.OutputKey, choiceOutput);
		var advancedPipeline = pipeline with
		{
			CurrentStepIndex = pipeline.CurrentStepIndex + 1,
			PipelineContext = updatedContext,
		};

		return (this with { ActionStack = ActionStack.Pop() })
			.PushActionInternal(advancedPipeline)
			.ProcessAllActions();
	}

	/// <summary>
	/// Pushes a single action without validation. Internal use only.
	/// </summary>
	private GameState PushActionInternal(GameAction action) =>
		this with
		{
			ActionStack = ActionStack.Push(action),
		};
}
