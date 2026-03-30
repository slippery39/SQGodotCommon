using System.Collections.Immutable;
using NUnit.Framework;

namespace ImmutableGameObjects.Tests;

[TestFixture]
public class MoveObjectToFrontTests
{
	private GameState _gameState;
	private Player _player;
	private Item _itemA;
	private Item _itemB;
	private Item _itemC;

	[SetUp]
	public void SetUp()
	{
		_gameState = new GameState();
		_player = new Player { Name = "Hero" };
		_itemA = new Item { Name = "Item A" };
		_itemB = new Item { Name = "Item B" };
		_itemC = new Item { Name = "Item C" };
	}

	// ===== BASIC ORDERING =====

	[Test]
	public void MoveObjectToFront_MovesCardFromDifferentZoneToFrontOfTarget()
	{
		var state = _gameState
			.ThenAdd(_player) // ID 1
			.ThenAdd(_itemA, parentId: 1) // ID 2 — child of player
			.ThenAdd(_itemB, parentId: 1) // ID 3 — child of player
			.ThenAdd(_itemC, parentId: 1); // ID 4 — child of player

		// Move itemC to front
		var newState = state.MoveObjectToFront(4, 1);

		var children = newState.GetChildrenIds(1).ToList();
		Assert.That(children[0], Is.EqualTo(4), "Item C should be at front");
		Assert.That(children[1], Is.EqualTo(2), "Item A should be second");
		Assert.That(children[2], Is.EqualTo(3), "Item B should be third");
	}

	[Test]
	public void MoveObjectToFront_WhenAlreadyAtFront_RemainsAtFront()
	{
		var state = _gameState
			.ThenAdd(_player)
			.ThenAdd(_itemA, parentId: 1) // ID 2
			.ThenAdd(_itemB, parentId: 1) // ID 3
			.ThenAdd(_itemC, parentId: 1); // ID 4

		// Move itemA (already at front) to front
		var newState = state.MoveObjectToFront(2, 1);

		var children = newState.GetChildrenIds(1).ToList();
		Assert.That(children[0], Is.EqualTo(2), "Item A should still be at front");
		Assert.That(children[1], Is.EqualTo(3), "Item B should be second");
		Assert.That(children[2], Is.EqualTo(4), "Item C should be third");
	}

	[Test]
	public void MoveObjectToFront_WhenAtBack_MovesToFront()
	{
		var state = _gameState
			.ThenAdd(_player)
			.ThenAdd(_itemA, parentId: 1) // ID 2
			.ThenAdd(_itemB, parentId: 1) // ID 3
			.ThenAdd(_itemC, parentId: 1); // ID 4

		// Move itemC (at back) to front — same parent
		var newState = state.MoveObjectToFront(4, 1);

		var children = newState.GetChildrenIds(1).ToList();
		Assert.That(children[0], Is.EqualTo(4), "Item C should be at front");
		Assert.That(children[1], Is.EqualTo(2), "Item A should be second");
		Assert.That(children[2], Is.EqualTo(3), "Item B should be third");
	}

	[Test]
	public void MoveObjectToFront_WhenInMiddle_MovesToFront()
	{
		var state = _gameState
			.ThenAdd(_player)
			.ThenAdd(_itemA, parentId: 1) // ID 2
			.ThenAdd(_itemB, parentId: 1) // ID 3
			.ThenAdd(_itemC, parentId: 1); // ID 4

		// Move itemB (in middle) to front — same parent
		var newState = state.MoveObjectToFront(3, 1);

		var children = newState.GetChildrenIds(1).ToList();
		Assert.That(children[0], Is.EqualTo(3), "Item B should be at front");
		Assert.That(children[1], Is.EqualTo(2), "Item A should be second");
		Assert.That(children[2], Is.EqualTo(4), "Item C should be third");
	}

	// ===== CROSS-PARENT MOVE =====

	[Test]
	public void MoveObjectToFront_FromDifferentParent_InsertsAtFront()
	{
		var state = _gameState
			.ThenAdd(_player) // ID 1 — parent A
			.ThenAdd(_itemA, parentId: 1) // ID 2 — child of player
			.ThenAdd(_itemB, parentId: 1) // ID 3 — child of player
			.ThenAdd(new Player { Name = "Container" }) // ID 4 — parent B
			.ThenAdd(_itemC, parentId: 4); // ID 5 — child of Container

		// Move itemC from Container to front of player
		var newState = state.MoveObjectToFront(5, 1);

		var playerChildren = newState.GetChildrenIds(1).ToList();
		Assert.That(playerChildren[0], Is.EqualTo(5), "Item C should be at front of player");
		Assert.That(playerChildren[1], Is.EqualTo(2), "Item A should be second");
		Assert.That(playerChildren[2], Is.EqualTo(3), "Item B should be third");

		var containerChildren = newState.GetChildrenIds(4).ToList();
		Assert.That(containerChildren, Is.Empty, "Container should be empty after move");
	}

	// ===== PARENT/CHILD RELATIONSHIPS =====

	[Test]
	public void MoveObjectToFront_UpdatesParentRelationship()
	{
		var state = _gameState
			.ThenAdd(_player) // ID 1
			.ThenAdd(new Player { Name = "Container" }) // ID 2
			.ThenAdd(_itemA, parentId: 2); // ID 3 — child of Container

		// Move itemA to front of player
		var newState = state.MoveObjectToFront(3, 1);

		Assert.That(newState.GetParent(3), Is.EqualTo(1), "Item A's parent should now be player");
		Assert.That(
			newState.GetChildrenIds(2),
			Is.Empty,
			"Container should no longer have Item A as child"
		);
	}

	[Test]
	public void MoveObjectToFront_SameParent_PreservesParentRelationship()
	{
		var state = _gameState
			.ThenAdd(_player)
			.ThenAdd(_itemA, parentId: 1)
			.ThenAdd(_itemB, parentId: 1);

		var newState = state.MoveObjectToFront(3, 1); // Move itemB to front

		Assert.That(
			newState.GetParent(3),
			Is.EqualTo(1),
			"Parent relationship should be preserved"
		);
		Assert.That(
			newState.GetChildrenIds(1).Count(),
			Is.EqualTo(2),
			"Child count should be unchanged"
		);
	}

	// ===== SINGLE CHILD =====

	[Test]
	public void MoveObjectToFront_SingleChild_RemainsOnlyChild()
	{
		var state = _gameState.ThenAdd(_player).ThenAdd(_itemA, parentId: 1); // ID 2

		var newState = state.MoveObjectToFront(2, 1);

		var children = newState.GetChildrenIds(1).ToList();
		Assert.That(children.Count, Is.EqualTo(1));
		Assert.That(children[0], Is.EqualTo(2));
	}

	// ===== ERROR CASES =====

	[Test]
	public void MoveObjectToFront_NonExistentObject_ThrowsException()
	{
		var state = _gameState.ThenAdd(_player);

		Assert.Throws<ArgumentException>(() => state.MoveObjectToFront(999, 1));
	}

	[Test]
	public void MoveObjectToFront_NonExistentParent_ThrowsException()
	{
		var state = _gameState.ThenAdd(_player).ThenAdd(_itemA, parentId: 1);

		Assert.Throws<ArgumentException>(() => state.MoveObjectToFront(2, 999));
	}

	[Test]
	public void MoveObjectToFront_ToDescendant_ThrowsException()
	{
		var state = _gameState
			.ThenAdd(_player) // ID 1
			.ThenAdd(_itemA, parentId: 1) // ID 2
			.ThenAdd(_itemB, parentId: 2); // ID 3 — grandchild

		Assert.Throws<InvalidOperationException>(() => state.MoveObjectToFront(1, 3));
	}

	// ===== IMMUTABILITY =====

	[Test]
	public void MoveObjectToFront_OriginalStateUnchanged()
	{
		var state = _gameState
			.ThenAdd(_player)
			.ThenAdd(_itemA, parentId: 1) // ID 2
			.ThenAdd(_itemB, parentId: 1) // ID 3
			.ThenAdd(_itemC, parentId: 1); // ID 4

		var _ = state.MoveObjectToFront(4, 1);

		var originalChildren = state.GetChildrenIds(1).ToList();
		Assert.That(originalChildren[0], Is.EqualTo(2), "Original state should be unchanged");
		Assert.That(originalChildren[1], Is.EqualTo(3));
		Assert.That(originalChildren[2], Is.EqualTo(4));
	}
}
