using System.Collections.Immutable;
using NUnit.Framework;

namespace ImmutableGameObjects.Tests;

// =====================================================================
// Test Components
// =====================================================================

public record TappedComponent : GameComponent;

public record CounterComponent : GameComponent
{
	public string CounterType { get; init; } = "";
	public int Count { get; init; }
}

public record TransformDataComponent : GameComponent
{
	public string TransformedName { get; init; } = "";
	public int TransformedPower { get; init; }
	public int TransformedToughness { get; init; }
}

// =====================================================================
// Test Game Objects
// =====================================================================

public record ComponentTestCreature : GameObject
{
	public int Power { get; init; }
	public int Toughness { get; init; }
}

// =====================================================================
// Test Actions (for validation tests)
// =====================================================================

/// <summary>
/// Action that requires a specific target creature to exist and not be destroyed.
/// Demonstrates ValidateAdd and ValidateResolve.
/// </summary>
public record TargetedDestroyAction : GameAction
{
	public int TargetId { get; init; }

	public override ValidationResult ValidateAdd(GameState gameState)
	{
		if (!gameState.HasObject(TargetId))
			return ValidationResult.Invalid($"Target {TargetId} does not exist");

		var creature = gameState.GetObject(TargetId) as ComponentTestCreature;
		if (creature == null)
			return ValidationResult.Invalid($"Target {TargetId} is not a creature");

		return ValidationResult.Valid;
	}

	public override ValidationResult ValidateResolve(GameState gameState)
	{
		if (!gameState.HasObject(TargetId))
			return ValidationResult.Invalid($"Target {TargetId} no longer exists");

		var creature = gameState.GetObject(TargetId) as ComponentTestCreature;
		if (creature == null)
			return ValidationResult.Invalid($"Target {TargetId} is no longer a creature");

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState gameState)
	{
		// In a real engine we'd remove or mark destroyed — here we just remove it
		var newState = gameState.RemoveObject(TargetId);
		return new ActionResult(newState);
	}
}

/// <summary>
/// Removes an object from game state. Used in tests to invalidate a target
/// before an action resolves.
/// </summary>
public record RemoveObjectAction : GameAction
{
	public int ObjectId { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var newState = gameState.RemoveObject(ObjectId);
		return new ActionResult(newState);
	}
}

// =====================================================================
// Component Tests
// =====================================================================

[TestFixture]
public class ComponentTests
{
	private GameState _initialState;

	[SetUp]
	public void Setup()
	{
		_initialState = new GameState();
	}

	[Test]
	public void WithComponent_AddsComponentToObject()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var tapped = creature.WithComponent(new TappedComponent());
		state = state.UpdateObject(creature.Id, tapped);

		var updated = (ComponentTestCreature)state.GetObject(creature.Id);
		Assert.That(updated.HasComponent<TappedComponent>(), Is.True);
	}

	[Test]
	public void GetComponent_ReturnsFirstMatchingComponent()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var withCounter = creature.WithComponent(
			new CounterComponent { CounterType = "+1/+1", Count = 2 }
		);
		state = state.UpdateObject(creature.Id, withCounter);

		var updated = (ComponentTestCreature)state.GetObject(creature.Id);
		var counter = updated.GetComponent<CounterComponent>();

		Assert.That(counter, Is.Not.Null);
		Assert.That(counter!.CounterType, Is.EqualTo("+1/+1"));
		Assert.That(counter.Count, Is.EqualTo(2));
	}

	[Test]
	public void GetComponents_ReturnsAllMatchingComponents()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var withCounters = creature
			.WithComponent(new CounterComponent { CounterType = "+1/+1", Count = 2 })
			.WithComponent(new CounterComponent { CounterType = "-1/-1", Count = 1 });

		state = state.UpdateObject(creature.Id, withCounters);

		var updated = (ComponentTestCreature)state.GetObject(creature.Id);
		var counters = updated.GetComponents<CounterComponent>().ToList();

		Assert.That(counters, Has.Count.EqualTo(2));
		Assert.That(
			counters.Select(c => c.CounterType),
			Contains.Item("+1/+1").And.Contains("-1/-1")
		);
	}

	[Test]
	public void HasComponent_ReturnsFalseWhenComponentNotPresent()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var updated = (ComponentTestCreature)state.GetObject(creature.Id);
		Assert.That(updated.HasComponent<TappedComponent>(), Is.False);
	}

	[Test]
	public void WithoutComponents_RemovesAllComponentsOfType()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var withCounters = creature
			.WithComponent(new CounterComponent { CounterType = "+1/+1", Count = 1 })
			.WithComponent(new CounterComponent { CounterType = "+1/+1", Count = 1 })
			.WithComponent(new TappedComponent());

		var withoutCounters = withCounters.WithoutComponents<CounterComponent>();
		state = state.UpdateObject(creature.Id, withoutCounters);

		var updated = (ComponentTestCreature)state.GetObject(creature.Id);
		Assert.That(
			updated.GetComponents<CounterComponent>().Count(),
			Is.EqualTo(0),
			"All counter components should be removed"
		);
		Assert.That(
			updated.HasComponent<TappedComponent>(),
			Is.True,
			"Other component types should remain"
		);
	}

	[Test]
	public void WithComponentReplaced_ReplacesAllOfType_WithSingleNew()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var withTransform = creature.WithComponent(
			new TransformDataComponent
			{
				TransformedName = "Werewolf",
				TransformedPower = 4,
				TransformedToughness = 4,
			}
		);

		var replaced = withTransform.WithComponentReplaced(
			new TransformDataComponent
			{
				TransformedName = "Elder Werewolf",
				TransformedPower = 6,
				TransformedToughness = 6,
			}
		);

		state = state.UpdateObject(creature.Id, replaced);
		var updated = (ComponentTestCreature)state.GetObject(creature.Id);

		var transform = updated.GetComponent<TransformDataComponent>();
		Assert.That(transform, Is.Not.Null);
		Assert.That(
			transform!.TransformedName,
			Is.EqualTo("Elder Werewolf"),
			"Should have the new transform data"
		);
		Assert.That(
			updated.GetComponents<TransformDataComponent>().Count(),
			Is.EqualTo(1),
			"Should only have one transform component"
		);
	}

	[Test]
	public void Components_AreImmutable_OriginalUnchanged()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var withTapped = creature.WithComponent(new TappedComponent());

		// Original creature should be unchanged
		Assert.That(
			creature.HasComponent<TappedComponent>(),
			Is.False,
			"Original object should not be modified"
		);
		Assert.That(withTapped.HasComponent<TappedComponent>(), Is.True);
	}
}

// =====================================================================
// Metadata Tests
// =====================================================================

[TestFixture]
public class MetadataTests
{
	private GameState _initialState;

	[SetUp]
	public void Setup()
	{
		_initialState = new GameState();
	}

	[Test]
	public void WithMeta_StoresValue()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var withMeta = creature.WithMeta("times_played", 3);
		state = state.UpdateObject(creature.Id, withMeta);

		var updated = state.GetObject(creature.Id);
		Assert.That(updated.GetMeta<int>("times_played"), Is.EqualTo(3));
	}

	[Test]
	public void GetMeta_ReturnsDefaultWhenKeyNotPresent()
	{
		var (_, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		Assert.That(creature.GetMeta("times_played", 0), Is.EqualTo(0));
		Assert.That(creature.GetMeta("owner_name", "unknown"), Is.EqualTo("unknown"));
	}

	[Test]
	public void WithMeta_OverwritesExistingValue()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var updated = creature.WithMeta("times_played", 1).WithMeta("times_played", 5);

		state = state.UpdateObject(creature.Id, updated);
		var final = state.GetObject(creature.Id);

		Assert.That(final.GetMeta<int>("times_played"), Is.EqualTo(5));
	}

	[Test]
	public void WithoutMeta_RemovesKey()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var withMeta = creature.WithMeta("times_played", 3);
		var withoutMeta = withMeta.WithoutMeta("times_played");
		state = state.UpdateObject(creature.Id, withoutMeta);

		var updated = state.GetObject(creature.Id);
		Assert.That(
			updated.GetMeta("times_played", 0),
			Is.EqualTo(0),
			"Key should no longer exist after removal"
		);
	}

	[Test]
	public void Metadata_CanStoreMultipleKeys()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var withMeta = creature
			.WithMeta("times_played", 2)
			.WithMeta("entry_life_total", 14)
			.WithMeta("custom_label", "important");

		state = state.UpdateObject(creature.Id, withMeta);
		var updated = state.GetObject(creature.Id);

		Assert.That(updated.GetMeta<int>("times_played"), Is.EqualTo(2));
		Assert.That(updated.GetMeta<int>("entry_life_total"), Is.EqualTo(14));
		Assert.That(updated.GetMeta<string>("custom_label"), Is.EqualTo("important"));
	}

	[Test]
	public void Metadata_IsImmutable_OriginalUnchanged()
	{
		var (_, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var withMeta = creature.WithMeta("times_played", 3);

		Assert.That(
			creature.GetMeta("times_played", 0),
			Is.EqualTo(0),
			"Original object should not be modified"
		);
		Assert.That(withMeta.GetMeta<int>("times_played"), Is.EqualTo(3));
	}
}

// =====================================================================
// Validation Tests
// =====================================================================

[TestFixture]
public class ValidationTests
{
	private GameState _initialState;

	[SetUp]
	public void Setup()
	{
		_initialState = new GameState();
	}

	[Test]
	public void AddAction_ThrowsWhenValidateAddFails()
	{
		// Try to target a creature that doesn't exist
		var action = new TargetedDestroyAction { TargetId = 999 };

		Assert.Throws<InvalidOperationException>(
			() => _initialState.AddAction(action),
			"Should throw when target does not exist"
		);
	}

	[Test]
	public void AddAction_SucceedsWhenValidateAddPasses()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var action = new TargetedDestroyAction { TargetId = creature.Id };

		Assert.DoesNotThrow(() => state.AddAction(action));
	}

	[Test]
	public void TryAddAction_ReturnsFalseWhenValidateAddFails()
	{
		var action = new TargetedDestroyAction { TargetId = 999 };

		var (resultState, success) = _initialState.TryAddAction(action);

		Assert.That(success, Is.False);
		Assert.That(
			resultState.HasPendingActions,
			Is.False,
			"Action should not have been added to the stack"
		);
	}

	[Test]
	public void TryAddAction_ReturnsTrueWhenValidateAddPasses()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		var action = new TargetedDestroyAction { TargetId = creature.Id };
		var (resultState, success) = state.TryAddAction(action);

		Assert.That(success, Is.True);
		Assert.That(resultState.HasPendingActions, Is.True);
	}

	[Test]
	public void ProcessNextAction_DropsActionAndEmitsEvent_WhenValidateResolveFails()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		// Add a valid destroy action
		var destroyAction = new TargetedDestroyAction { TargetId = creature.Id };
		state = state.AddAction(destroyAction);

		// Also add a remove action that will fire first, invalidating the target
		// We use PushActionsInternal indirectly by setting up state manually.
		// Instead we'll use AddActions ordering: RemoveObject executes first.
		var removeAction = new RemoveObjectAction { ObjectId = creature.Id };

		// Rebuild: remove fires first, then destroy tries to resolve
		var freshState = _initialState;
		var (withCreature, c) = freshState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		// Add destroy first (it goes on stack), then remove on top (executes first)
		withCreature = withCreature.AddAction(new TargetedDestroyAction { TargetId = c.Id });

		// Manually push remove on top without validation since remove has no restrictions
		var stackWithRemoveOnTop = withCreature with
		{
			ActionStack = withCreature.ActionStack.Push(new RemoveObjectAction { ObjectId = c.Id }),
		};

		var (finalState, events) = stackWithRemoveOnTop.ProcessAllActions();

		Assert.That(finalState.HasPendingActions, Is.False);
		Assert.That(
			finalState.HasObject(c.Id),
			Is.False,
			"Creature should have been removed by RemoveObjectAction"
		);

		var failedEvent = events.OfType<ActionValidationFailedEvent>().FirstOrDefault();
		Assert.That(
			failedEvent,
			Is.Not.Null,
			"Should emit ActionValidationFailedEvent when target no longer exists"
		);
		Assert.That(failedEvent!.Action, Is.TypeOf<TargetedDestroyAction>());
	}

	[Test]
	public void ProcessNextAction_ExecutesNormally_WhenValidateResolvePasses()
	{
		var (state, creature) = _initialState.AddObject(
			new ComponentTestCreature
			{
				Name = "Bear",
				Power = 2,
				Toughness = 2,
			}
		);

		state = state.AddAction(new TargetedDestroyAction { TargetId = creature.Id });
		var (finalState, events) = state.ProcessAllActions();

		Assert.That(
			finalState.HasObject(creature.Id),
			Is.False,
			"Creature should have been removed"
		);
		Assert.That(
			events.OfType<ActionValidationFailedEvent>(),
			Is.Empty,
			"No validation failure events should be emitted"
		);
	}

	[Test]
	public void ValidationResult_Valid_IsValid()
	{
		Assert.That(ValidationResult.Valid.IsValid, Is.True);
	}

	[Test]
	public void ValidationResult_Invalid_ContainsReason()
	{
		var result = ValidationResult.Invalid("Target does not exist");
		Assert.That(result.IsValid, Is.False);
		Assert.That(result.Reason, Is.EqualTo("Target does not exist"));
	}
}
