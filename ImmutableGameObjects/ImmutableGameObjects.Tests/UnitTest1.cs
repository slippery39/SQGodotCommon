using System.Collections.Immutable;
using ImmutableGameObjects;
using NUnit.Framework;

[TestFixture]
public class GameStateTests
{
	private GameState _gameState;
	private Player _player;
	private Item _sword;
	private Item _potion;

	[SetUp]
	public void SetUp()
	{
		_gameState = new GameState();
		_player = new Player
		{
			Name = "Hero",
			Health = 100,
			Level = 5,
		};
		_sword = new Item
		{
			Name = "Iron Sword",
			Weight = 10,
			Value = 100,
		};
		_potion = new Item
		{
			Name = "Health Potion",
			Weight = 1,
			Value = 25,
		};
	}

	[Test]
	public void AddObject_ShouldAddObjectWithCorrectId()
	{
		// Act
		var newState = _gameState.AddObject(_player);

		// Assert
		Assert.That(newState.NextId, Is.EqualTo(2));
		Assert.That(newState.HasObject(1), Is.True);

		var retrievedPlayer = newState.GetObject(1);
		Assert.That(retrievedPlayer.Name, Is.EqualTo("Hero"));
		Assert.That(retrievedPlayer.Id, Is.EqualTo(1));
	}

	[Test]
	public void AddObject_WithParent_ShouldSetupParentChildRelationship()
	{
		// Arrange
		var stateWithPlayer = _gameState.AddObject(_player);

		// Act
		var finalState = stateWithPlayer.AddObject(_sword, parentId: 1);

		// Assert
		Assert.That(finalState.GetParent(2), Is.EqualTo(1));
		Assert.That(finalState.GetChildrenIds(1), Contains.Item(2));
		Assert.That(finalState.GetChildren(1).First().Name, Is.EqualTo("Iron Sword"));
	}

	[Test]
	public void AddObject_WithNonExistentParent_ShouldThrowException()
	{
		// Act & Assert
		Assert.Throws<ArgumentException>(() => _gameState.AddObject(_player, parentId: 999));
	}

	[Test]
	public void AddObjectRecursive_ShouldAddObjectWithAllChildren()
	{
		// Arrange
		var playerWithItems = _player with
		{
			Children = ImmutableList.Create<GameObject>(_sword, _potion),
		};

		// Act
		var newState = _gameState.AddObjectRecursive(playerWithItems);

		// Assert
		Assert.That(newState.NextId, Is.EqualTo(4)); // Player + 2 items
		Assert.That(newState.HasObject(1), Is.True); // Player
		Assert.That(newState.HasObject(2), Is.True); // Sword
		Assert.That(newState.HasObject(3), Is.True); // Potion

		// Check hierarchy
		var childrenIds = newState.GetChildrenIds(1).ToList();
		Assert.That(childrenIds, Has.Count.EqualTo(2));
		Assert.That(childrenIds, Contains.Item(2));
		Assert.That(childrenIds, Contains.Item(3));

		Assert.That(newState.GetParent(2), Is.EqualTo(1));
		Assert.That(newState.GetParent(3), Is.EqualTo(1));
	}

	[Test]
	public void AddObjectRecursive_WithNestedChildren_ShouldCreateDeepHierarchy()
	{
		// Arrange
		var nestedItem = new Item
		{
			Name = "Gem",
			Weight = 1,
			Value = 500,
		};
		var swordWithGem = _sword with { Children = ImmutableList.Create<GameObject>(nestedItem) };
		var playerWithSword = _player with
		{
			Children = ImmutableList.Create<GameObject>(swordWithGem),
		};

		// Act
		var newState = _gameState.AddObjectRecursive(playerWithSword);

		// Assert
		Assert.That(newState.NextId, Is.EqualTo(4)); // Player, Sword, Gem
		Assert.That(newState.GetParent(2), Is.EqualTo(1)); // Sword -> Player
		Assert.That(newState.GetParent(3), Is.EqualTo(2)); // Gem -> Sword
		Assert.That(newState.GetChildrenIds(2), Contains.Item(3)); // Sword has Gem
	}

	[Test]
	public void UpdateObject_ShouldReplaceExistingObject()
	{
		// Arrange
		var stateWithPlayer = _gameState.AddObject(_player);
		var updatedPlayer = new Player
		{
			Name = "Updated Hero",
			Health = 50,
			Level = 10,
		};

		// Act
		var newState = stateWithPlayer.UpdateObject(1, updatedPlayer);

		// Assert
		var retrievedPlayer = (Player)newState.GetObject(1);
		Assert.That(retrievedPlayer.Name, Is.EqualTo("Updated Hero"));
		Assert.That(retrievedPlayer.Health, Is.EqualTo(50));
		Assert.That(retrievedPlayer.Level, Is.EqualTo(10));
		Assert.That(retrievedPlayer.Id, Is.EqualTo(1)); // ID should be preserved
		Assert.That(_player, Is.Not.EqualTo(updatedPlayer));
	}

	[Test]
	public void UpdateObject_WithNonExistentId_ShouldThrowException()
	{
		// Act & Assert
		Assert.Throws<ArgumentException>(() => _gameState.UpdateObject(999, _player));
	}

	[Test]
	public void MoveObject_ShouldUpdateParentChildRelationships()
	{
		// Arrange
		var state = _gameState
			.AddObject(_player) // ID: 1
			.AddObject(_sword, parentId: 1) // ID: 2
			.AddObject(_potion); // ID: 3

		// Act - Move sword from player to root
		var newState = state.MoveObject(2, 0);

		// Assert
		Assert.That(newState.GetParent(2), Is.Null); // Sword has no parent
		Assert.That(newState.GetChildrenIds(1), Does.Not.Contain(2)); // Player doesn't have sword
		Assert.That(newState.HasObject(2), Is.True); // Sword still exists

		// Act - Move sword to potion (if potion could have children)
		// This would be unusual but tests the functionality
		var finalState = newState.MoveObject(2, 3);
		Assert.That(finalState.GetParent(2), Is.EqualTo(3));
		Assert.That(finalState.GetChildrenIds(3), Contains.Item(2));
	}

	[Test]
	public void MoveObject_ToDescendant_ShouldThrowException()
	{
		// Arrange - Create parent -> child -> grandchild hierarchy
		var state = _gameState
			.AddObject(_player) // ID: 1
			.AddObject(_sword, parentId: 1) // ID: 2
			.AddObject(_potion, parentId: 2); // ID: 3

		// Act & Assert - Try to move player under potion (its grandchild)
		Assert.Throws<InvalidOperationException>(() => state.MoveObject(1, 3));
	}

	[Test]
	public void MoveObject_ToSelf_ShouldThrowException()
	{
		// Arrange
		var state = _gameState.AddObject(_player);

		// Act & Assert
		Assert.Throws<InvalidOperationException>(() => state.MoveObject(1, 1));
	}

	[Test]
	public void RemoveObject_WithoutChildren_ShouldRemoveCleanly()
	{
		// Arrange
		var state = _gameState
			.AddObject(_player) // ID: 1
			.AddObject(_sword, parentId: 1); // ID: 2

		// Act
		var newState = state.RemoveObject(2);

		// Assert
		Assert.That(newState.HasObject(2), Is.False);
		Assert.That(newState.GetChildrenIds(1), Does.Not.Contain(2));
		Assert.That(newState.HasObject(1), Is.True); // Parent still exists
	}

	[Test]
	public void RemoveObject_WithChildren_RemoveChildrenFalse_ShouldMoveChildrenToParent()
	{
		// Arrange
		var state = _gameState
			.AddObject(_player) // ID: 1
			.AddObject(_sword, parentId: 1) // ID: 2
			.AddObject(_potion, parentId: 2); // ID: 3

		// Act
		var newState = state.RemoveObject(2, removeChildren: false);

		// Assert
		Assert.That(newState.HasObject(2), Is.False); // Sword removed
		Assert.That(newState.HasObject(3), Is.True); // Potion still exists
		Assert.That(newState.GetParent(3), Is.EqualTo(1)); // Potion moved to player
		Assert.That(newState.GetChildrenIds(1), Contains.Item(3)); // Player has potion
	}

	[Test]
	public void RemoveObject_WithChildren_RemoveChildrenTrue_ShouldRemoveAllDescendants()
	{
		// Arrange
		var state = _gameState
			.AddObject(_player) // ID: 1
			.AddObject(_sword, parentId: 1) // ID: 2
			.AddObject(_potion, parentId: 2); // ID: 3

		// Act
		var newState = state.RemoveObject(2, removeChildren: true);

		// Assert
		Assert.That(newState.HasObject(2), Is.False); // Sword removed
		Assert.That(newState.HasObject(3), Is.False); // Potion also removed
		Assert.That(newState.GetChildrenIds(1), Is.Empty); // Player has no children
	}

	[Test]
	public void GetChildren_WithNoChildren_ShouldReturnEmpty()
	{
		// Arrange
		var state = _gameState.AddObject(_player);

		// Act
		var children = state.GetChildren(1);

		// Assert
		Assert.That(children, Is.Empty);
	}

	[Test]
	public void GetChildren_WithMultipleChildren_ShouldReturnAllChildren()
	{
		// Arrange
		var state = _gameState
			.AddObject(_player) // ID: 1
			.AddObject(_sword, parentId: 1) // ID: 2
			.AddObject(_potion, parentId: 1); // ID: 3

		// Act
		var children = state.GetChildren(1).ToList();

		// Assert
		Assert.That(children, Has.Count.EqualTo(2));
		Assert.That(children.Select(c => c.Name), Contains.Item("Iron Sword"));
		Assert.That(children.Select(c => c.Name), Contains.Item("Health Potion"));
	}

	[Test]
	public void GetAllDescendants_ShouldReturnAllDescendantsInBreadthFirstOrder()
	{
		// Arrange - Create a tree: Player -> Sword -> Gem, Player -> Potion
		var gem = new Item
		{
			Name = "Ruby",
			Weight = 1,
			Value = 1000,
		};
		var state = _gameState
			.AddObject(_player) // ID: 1
			.AddObject(_sword, parentId: 1) // ID: 2
			.AddObject(_potion, parentId: 1) // ID: 3
			.AddObject(gem, parentId: 2); // ID: 4

		// Act
		var descendants = state.GetAllDescendants(1).ToList();

		// Assert
		Assert.That(descendants, Has.Count.EqualTo(3));
		Assert.That(descendants, Contains.Item(2)); // Sword
		Assert.That(descendants, Contains.Item(3)); // Potion
		Assert.That(descendants, Contains.Item(4)); // Gem

		// Check breadth-first order (children before grandchildren)
		var swordIndex = descendants.IndexOf(2);
		var potionIndex = descendants.IndexOf(3);
		var gemIndex = descendants.IndexOf(4);
		Assert.That(swordIndex, Is.LessThan(gemIndex));
		Assert.That(potionIndex, Is.LessThan(gemIndex));
	}

	[Test]
	public void LoadFrom_ShouldReconstructObjectWithChildren()
	{
		// Arrange
		var playerWithItems = _player with
		{
			Children = [_sword, _potion],
		};
		var state = _gameState.AddObjectRecursive(playerWithItems);

		// Act
		var loadedPlayer = (Player)state.GetObject(1).LoadFrom(state);

		// Assert
		Assert.That(loadedPlayer.Children, Has.Count.EqualTo(2));
		Assert.That(loadedPlayer.Children.Select(c => c.Name), Contains.Item("Iron Sword"));
		Assert.That(loadedPlayer.Children.Select(c => c.Name), Contains.Item("Health Potion"));

		// Check that IDs are properly set
		Assert.That(loadedPlayer.Children.All(c => c.Id > 0), Is.True);
	}

	[Test]
	public void LoadHierarchy_ShouldLoadCompleteHierarchy()
	{
		// Arrange
		var playerWithItems = _player with
		{
			Children = [_sword, _potion],
		};
		var state = _gameState.AddObjectRecursive(playerWithItems);

		// Act
		var loadedHierarchy = state.LoadHierarchy(1);

		// Assert
		Assert.That(loadedHierarchy.Children, Has.Count.EqualTo(2));
		Assert.That(loadedHierarchy.Name, Is.EqualTo("Hero"));
	}

	[Test]
	public void LoadHierarchy_WithNonExistentId_ShouldThrowException()
	{
		// Act & Assert
		Assert.Throws<ArgumentException>(() => _gameState.LoadHierarchy(999));
	}

	[Test]
	public void GameState_ShouldBeImmutable()
	{
		// Arrange
		var originalState = _gameState.AddObject(_player);
		var originalNextId = originalState.NextId;

		// Act
		var newState = originalState.AddObject(_sword);

		// Assert
		Assert.That(originalState.NextId, Is.EqualTo(originalNextId)); // Original unchanged
		Assert.That(newState.NextId, Is.EqualTo(originalNextId + 1)); // New state updated
		Assert.That(originalState.HasObject(2), Is.False); // Sword not in original
		Assert.That(newState.HasObject(2), Is.True); // Sword in new state
	}

	[Test]
	public void ComplexScenario_MultipleOperations_ShouldMaintainConsistency()
	{
		// Arrange & Act - Build a complex scenario
		var state = _gameState
			.AddObject(_player) // ID: 1
			.AddObject(_sword, parentId: 1) // ID: 2
			.AddObject(_potion, parentId: 1) // ID: 3
			.MoveObject(2, 0) // Move sword to root
			.UpdateObject(1, _player with { Name = "Updated Hero" })
			.AddObject(
				new Item
				{
					Name = "Shield",
					Weight = 15,
					Value = 75,
				},
				parentId: 1
			); // ID: 4

		// Assert - Verify final state
		var player = (Player)state.GetObject(1);
		Assert.That(player.Name, Is.EqualTo("Updated Hero"));
		Assert.That(state.GetParent(2), Is.Null); // Sword at root
		Assert.That(state.GetParent(3), Is.EqualTo(1)); // Potion with player
		Assert.That(state.GetParent(4), Is.EqualTo(1)); // Shield with player
		Assert.That(state.GetChildrenIds(1).ToList(), Has.Count.EqualTo(2)); // Player has 2 children
		Assert.That(state.NextId, Is.EqualTo(5)); // Correct next ID
	}
}

// Test-specific implementations for completeness
public record Player : GameObject
{
	public int Health { get; init; } = 100;
	public int Level { get; init; } = 1;
}

public record Item : GameObject
{
	public int Weight { get; init; }
	public int Value { get; init; }
}
