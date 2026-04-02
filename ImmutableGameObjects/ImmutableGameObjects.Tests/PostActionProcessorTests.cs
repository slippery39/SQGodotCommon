using System.Collections.Immutable;
using ImmutableGameObjects;
using NUnit.Framework;

namespace ImmutableGameObjects.Tests;

/// <summary>
/// Tests for GameState.PostActionProcessor.
///
/// Uses minimal stub actions defined locally — no game-specific types.
/// The processor is a GameAction with IsPostProcessor = true.
/// It fires after every standalone action and every completed pipeline,
/// but NOT between individual steps of a pipeline.
/// </summary>
[TestFixture]
public class PostActionProcessorTests
{
	// ===== STUBS =====

	/// <summary>
	/// A simple action that increments a counter stored in a root object's metadata.
	/// Used to verify how many times the processor fired.
	/// </summary>
	private record CounterAction : GameAction
	{
		public int RootId { get; init; }
		public string Key { get; init; } = "count";

		public override ActionResult Execute(GameState gameState)
		{
			var obj = gameState.GetObject(RootId);
			var current = obj.GetMeta<int>(Key, 0);
			var updated = obj.WithMeta(Key, current + 1);
			return new ActionResult(gameState.UpdateObject(RootId, updated));
		}
	}

	/// <summary>
	/// Same as CounterAction but IsPostProcessor = true so it does not trigger itself.
	/// </summary>
	private record PostProcessorCounterAction : GameAction
	{
		public int RootId { get; init; }
		public string Key { get; init; } = "count";

		public override bool IsPostProcessor => true;

		public override ActionResult Execute(GameState gameState)
		{
			var obj = gameState.GetObject(RootId);
			var current = obj.GetMeta<int>(Key, 0);
			var updated = obj.WithMeta(Key, current + 1);
			return new ActionResult(gameState.UpdateObject(RootId, updated));
		}
	}

	/// <summary>
	/// A no-op action that does nothing but exist on the stack.
	/// </summary>
	private record NoOpAction : GameAction
	{
		public override ActionResult Execute(GameState gameState) => new ActionResult(gameState);
	}

	// ===== SETUP =====

	private GameState _state;
	private int _rootId;

	[SetUp]
	public void Setup()
	{
		var state = new GameState();
		var root = new TestObject { Name = "Root" };
		var (s, added) = state.AddObject(root);
		_state = s;
		_rootId = added.Id;
	}

	private GameState WithProcessor() =>
		_state with
		{
			PostActionProcessor = new PostProcessorCounterAction { RootId = _rootId },
		};

	private int GetCount(GameState state) => state.GetObject(_rootId).GetMeta<int>("count", 0);

	// ===== FIRES AFTER STANDALONE ACTIONS =====

	[Test]
	public void PostProcessor_FiresOnceAfterSingleStandaloneAction()
	{
		var state = WithProcessor();
		var (finalState, _) = state.AddAction(new NoOpAction()).ProcessAllActions();

		Assert.That(GetCount(finalState), Is.EqualTo(1));
	}

	[Test]
	public void PostProcessor_FiresAfterEachStandaloneAction()
	{
		var state = WithProcessor();
		state = state.AddAction(new NoOpAction());
		state = state.AddAction(new NoOpAction());
		state = state.AddAction(new NoOpAction());

		var (finalState, _) = state.ProcessAllActions();

		Assert.That(GetCount(finalState), Is.EqualTo(3));
	}

	[Test]
	public void PostProcessor_DoesNotFireWhenNoProcessorSet()
	{
		// _state has no PostActionProcessor set
		var (finalState, _) = _state.AddAction(new NoOpAction()).ProcessAllActions();

		Assert.That(GetCount(finalState), Is.EqualTo(0));
	}

	// ===== DOES NOT RECURSE =====

	[Test]
	public void PostProcessor_DoesNotTriggerItselfAfterRunning()
	{
		// The processor runs once per action, not indefinitely
		var state = WithProcessor();
		var (finalState, _) = state.AddAction(new NoOpAction()).ProcessAllActions();

		// If recursion happened the count would be > 1 for a single action
		Assert.That(GetCount(finalState), Is.EqualTo(1));
		Assert.That(finalState.HasPendingActions, Is.False);
	}

	// ===== PIPELINE BEHAVIOUR =====

	[Test]
	public void PostProcessor_FiresOnceAfterCompletePipeline()
	{
		var state = WithProcessor();

		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new NoOpAction(),
				new NoOpAction(),
				new NoOpAction()
			),
		};

		var (finalState, _) = state.AddAction(pipeline).ProcessAllActions();

		// Should fire once after the whole pipeline completes, not once per step
		Assert.That(GetCount(finalState), Is.EqualTo(1));
	}

	[Test]
	public void PostProcessor_FiresOncePerPipelineNotPerStep()
	{
		var state = WithProcessor();

		// Two pipelines — processor should fire once per pipeline = 2 total
		var pipeline1 = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(new NoOpAction(), new NoOpAction()),
		};
		var pipeline2 = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(new NoOpAction(), new NoOpAction()),
		};

		state = state.AddAction(pipeline1);
		state = state.AddAction(pipeline2);

		var (finalState, _) = state.ProcessAllActions();

		Assert.That(GetCount(finalState), Is.EqualTo(2));
	}

	[Test]
	public void PostProcessor_FiresAfterPipelineAndAfterStandaloneActions()
	{
		var state = WithProcessor();

		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(new NoOpAction()),
		};

		// Queue: pipeline, then two standalone actions
		state = state.AddAction(pipeline);
		state = state.AddAction(new NoOpAction());
		state = state.AddAction(new NoOpAction());

		var (finalState, _) = state.ProcessAllActions();

		// 1 pipeline + 2 standalone = 3 triggers
		Assert.That(GetCount(finalState), Is.EqualTo(3));
	}

	// ===== PROCESSOR SEES UPDATED STATE =====

	[Test]
	public void PostProcessor_ReceivesStateAfterActionHasExecuted()
	{
		// The CounterAction increments "action_count".
		// The processor reads the same metadata so we can confirm it runs after.
		var actionCounterState = _state with
		{
			PostActionProcessor = new PostProcessorCounterAction
			{
				RootId = _rootId,
				Key = "processor_count",
			},
		};

		// The standalone action also increments a separate key
		var action = new CounterAction { RootId = _rootId, Key = "action_count" };

		var (finalState, _) = actionCounterState.AddAction(action).ProcessAllActions();

		Assert.That(finalState.GetObject(_rootId).GetMeta<int>("action_count", 0), Is.EqualTo(1));
		Assert.That(
			finalState.GetObject(_rootId).GetMeta<int>("processor_count", 0),
			Is.EqualTo(1)
		);
	}

	// ===== HELPER TYPES =====

	private record TestObject : GameObject;
}
