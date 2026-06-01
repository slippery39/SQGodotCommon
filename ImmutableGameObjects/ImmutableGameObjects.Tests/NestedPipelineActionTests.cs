using System.Collections.Immutable;
using NUnit.Framework;

namespace ImmutableGameObjects.Tests;

// =====================================================================
// Test Actions
// =====================================================================

/// <summary>
/// Simulates "choose a creature" - pauses for player input, outputs chosen creature ID.
/// </summary>
public record ChooseCreatureAction : ChoiceAction
{
	public ChooseCreatureAction()
	{
		Prompt = "Choose a creature";
		Options = ImmutableList.Create(
			new ChoiceOption { Id = 10, DisplayText = "Creature A" },
			new ChoiceOption { Id = 20, DisplayText = "Creature B" }
		);
		OutputKey = "chosen_creature_id";
	}
}

/// <summary>
/// Simulates "choose a target" - pauses for player input, outputs chosen target ID.
/// </summary>
public record ChooseTargetAction : ChoiceAction
{
	public ChooseTargetAction()
	{
		Prompt = "Choose a target";
		Options = ImmutableList.Create(
			new ChoiceOption { Id = 100, DisplayText = "Target A" },
			new ChoiceOption { Id = 200, DisplayText = "Target B" }
		);
		OutputKey = "chosen_target_id";
	}
}

/// <summary>
/// Reads the chosen creature ID from context and spawns an inner pipeline
/// representing that creature's triggered ability (choose target -> deal damage).
/// </summary>
public record UseCreatureAbilityAction : GameAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var chosenCreatureId = GetInput<int>("chosen_creature_id", -1);

		var innerPipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new ChooseTargetAction(),
				new DealDamageFromContextAction { SourceCreatureId = chosenCreatureId }
			),
		};

		return new ActionResult(gameState.SpawnAction(innerPipeline)).WithOutput(
			"ability_triggered_by",
			chosenCreatureId
		);
	}
}

/// <summary>
/// Reads the chosen target from context and records that damage was dealt.
/// Writes results into game state via a marker object so tests can verify execution.
/// </summary>
public record DealDamageFromContextAction : GameAction
{
	public int SourceCreatureId { get; init; }
	public int DamageAmount { get; init; } = 3;

	public override ActionResult Execute(GameState gameState)
	{
		var targetId = GetInput<int>("chosen_target_id", -1);

		var marker = new DamageMarker
		{
			SourceCreatureId = SourceCreatureId,
			TargetId = targetId,
			Amount = DamageAmount,
		};

		var (newState, _) = gameState.AddObject(marker);

		return new ActionResult(newState)
			.WithOutput("damage_dealt", DamageAmount)
			.WithOutput("damage_target", targetId);
	}
}

/// <summary>
/// A marker GameObject we add to game state so tests can verify
/// that damage was dealt with the correct source and target.
/// </summary>
public record DamageMarker : GameObject
{
	public int SourceCreatureId { get; init; }
	public int TargetId { get; init; }
	public int Amount { get; init; }
}

// =====================================================================
// Tests
// =====================================================================

[TestFixture]
public class NestedPipelineTests
{
	private GameState _initialState;

	[SetUp]
	public void Setup()
	{
		_initialState = new GameState();
	}

	/// <summary>
	/// Full happy path: outer pipeline pauses for creature choice, resumes,
	/// spawns inner pipeline, inner pipeline pauses for target choice, resumes,
	/// deals damage, outer pipeline completes.
	/// </summary>
	[Test]
	public void NestedPipeline_FullFlow_CompletesCleanly()
	{
		var outerPipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new ChooseCreatureAction(),
				new UseCreatureAbilityAction()
			),
		};

		var (stateAtCreatureChoice, _) = _initialState.AddAction(outerPipeline).ProcessAllActions();

		Assert.That(stateAtCreatureChoice.IsWaitingForChoice, Is.True);
		Assert.That(stateAtCreatureChoice.GetPendingChoice(), Is.TypeOf<ChooseCreatureAction>());

		var (stateAtTargetChoice, _) = stateAtCreatureChoice.ResolveChoice(
			ImmutableList.Create(10)
		);

		Assert.That(stateAtTargetChoice.IsWaitingForChoice, Is.True);
		Assert.That(stateAtTargetChoice.GetPendingChoice(), Is.TypeOf<ChooseTargetAction>());

		var (finalState, _) = stateAtTargetChoice.ResolveChoice(ImmutableList.Create(200));

		Assert.That(finalState.IsWaitingForChoice, Is.False);
		Assert.That(finalState.HasPendingActions, Is.False);
	}

	/// <summary>
	/// Verifies that the inner pipeline correctly received the chosen creature ID
	/// from the outer pipeline's context when spawning, and used the correct target.
	/// </summary>
	[Test]
	public void NestedPipeline_InnerPipeline_UsesCorrectSourceCreature()
	{
		var outerPipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new ChooseCreatureAction(),
				new UseCreatureAbilityAction()
			),
		};

		var (stateAtCreatureChoice, _) = _initialState.AddAction(outerPipeline).ProcessAllActions();
		var (stateAtTargetChoice, _) = stateAtCreatureChoice.ResolveChoice(
			ImmutableList.Create(20)
		);
		var (finalState, _) = stateAtTargetChoice.ResolveChoice(ImmutableList.Create(100));

		var marker = finalState.GetObjectOfType<DamageMarker>();
		Assert.That(marker, Is.Not.Null, "DamageMarker should have been added to game state");
		Assert.That(
			marker!.SourceCreatureId,
			Is.EqualTo(20),
			"Source should be the chosen creature"
		);
		Assert.That(marker.TargetId, Is.EqualTo(100), "Target should be the chosen target");
		Assert.That(marker.Amount, Is.EqualTo(3));
	}

	/// <summary>
	/// Verifies that the inner pipeline's context is isolated from the outer pipeline's context.
	/// The inner pipeline should NOT see "chosen_creature_id" from the outer context.
	/// </summary>
	[Test]
	public void NestedPipeline_InnerContext_IsIsolatedFromOuterContext()
	{
		var outerPipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new ChooseCreatureAction(),
				new UseCreatureAbilityAction()
			),
		};

		var (stateAtCreatureChoice, _) = _initialState.AddAction(outerPipeline).ProcessAllActions();
		var (stateAtTargetChoice, _) = stateAtCreatureChoice.ResolveChoice(
			ImmutableList.Create(10)
		);
		var (finalState, _) = stateAtTargetChoice.ResolveChoice(ImmutableList.Create(200));

		var marker = finalState.GetObjectOfType<DamageMarker>();
		Assert.That(marker, Is.Not.Null);

		Assert.That(
			marker!.TargetId,
			Is.EqualTo(200),
			"Inner pipeline should use its own context, not the outer pipeline's context"
		);
		Assert.That(
			marker.TargetId,
			Is.Not.EqualTo(10),
			"Target ID should not bleed from outer pipeline context"
		);
	}

	/// <summary>
	/// Verifies that the outer pipeline fully completes after the inner pipeline resolves.
	/// No orphaned actions should remain on the stack.
	/// </summary>
	[Test]
	public void NestedPipeline_AfterInnerCompletes_OuterPipelineAlsoCompletes()
	{
		var outerPipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new ChooseCreatureAction(),
				new UseCreatureAbilityAction()
			),
		};

		var (stateAtCreatureChoice, _) = _initialState.AddAction(outerPipeline).ProcessAllActions();
		var (stateAtTargetChoice, _) = stateAtCreatureChoice.ResolveChoice(
			ImmutableList.Create(10)
		);
		var (finalState, _) = stateAtTargetChoice.ResolveChoice(ImmutableList.Create(100));

		Assert.That(
			finalState.HasPendingActions,
			Is.False,
			"No actions should remain after both pipelines complete"
		);
		Assert.That(finalState.IsWaitingForChoice, Is.False);
	}
}
