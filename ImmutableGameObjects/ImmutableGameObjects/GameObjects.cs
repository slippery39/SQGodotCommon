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

	public GameObject GetObject(int id) => IdToGameObjectMap[id];

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

	public GameState AddObject(GameObject obj, int parentId = 0)
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

		return this with
		{
			NextId = NextId + 1,
			IdToGameObjectMap = newObjectMap,
			ParentToChildren = updatedParentToChildren,
			ChildToParent = updatedChildToParent,
		};
	}

	// Fixed version of AddObjectRecursive
	public GameState AddObjectRecursive(GameObject obj, int parentId = 0)
	{
		if (parentId != 0 && !IdToGameObjectMap.ContainsKey(parentId))
			throw new ArgumentException($"Parent with id {parentId} does not exist");

		// Add the main object first
		var state = AddObject(obj, parentId);
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
}

public abstract record GameObject
{
	// Made init-only since we should set this when adding to game state
	public int Id { get; init; }
	public string Name { get; init; } = "";
	public string Description { get; init; } = "";

	/// <summary>
	/// For initialization or view purposes only. Internals do not use this as a source of truth.
	/// </summary>
	public virtual ImmutableList<GameObject> Children { get; init; } =
		ImmutableList<GameObject>.Empty;

	public GameObject LoadFrom(GameState gameState)
	{
		var children = gameState
			.GetChildren(this.Id)
			.Select(child => child.LoadFrom(gameState))
			.ToImmutableList();

		// Use reflection to call the with expression properly for the derived type
		return this with
		{
			Children = children,
		};
	}
}
