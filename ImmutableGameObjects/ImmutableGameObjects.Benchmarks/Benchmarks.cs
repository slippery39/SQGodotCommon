using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Jobs;
using ImmutableGameObjects;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[RankColumn]
public class GameStateBenchmarks
{
	private GameState _smallState;
	private GameState _mediumState;
	private GameState _largeState;
	private GameState _deepState;

	private const int SmallSize = 100;
	private const int MediumSize = 1000;
	private const int LargeSize = 10000;
	private const int DeepNesting = 10;

	[GlobalSetup]
	public void Setup()
	{
		_smallState = CreateFlatHierarchy(SmallSize);
		_mediumState = CreateFlatHierarchy(MediumSize);
		_largeState = CreateFlatHierarchy(LargeSize);
		_deepState = CreateDeepHierarchy(DeepNesting, 5); // 10 levels, 5 children per level
	}

	#region Add Operations Benchmarks

	[Benchmark]
	[BenchmarkCategory("Add")]
	public GameState AddSingleObject_Small()
	{
		var item = new Item
		{
			Name = "New Item",
			Weight = 1,
			Value = 10,
		};
		return _smallState.AddObject(item, parentId: 1);
	}

	[Benchmark]
	[BenchmarkCategory("Add")]
	public GameState AddSingleObject_Medium()
	{
		var item = new Item
		{
			Name = "New Item",
			Weight = 1,
			Value = 10,
		};
		return _mediumState.AddObject(item, parentId: 1);
	}

	[Benchmark]
	[BenchmarkCategory("Add")]
	public GameState AddSingleObject_Large()
	{
		var item = new Item
		{
			Name = "New Item",
			Weight = 1,
			Value = 10,
		};
		return _largeState.AddObject(item, parentId: 1);
	}

	[Benchmark]
	[BenchmarkCategory("Add")]
	public GameState AddRecursive_SmallHierarchy()
	{
		var player = CreatePlayerWithItems(5);
		return new GameState().AddObjectRecursive(player);
	}

	[Benchmark]
	[BenchmarkCategory("Add")]
	public GameState AddRecursive_MediumHierarchy()
	{
		var player = CreatePlayerWithItems(25);
		return new GameState().AddObjectRecursive(player);
	}

	#endregion

	#region Update Operations Benchmarks

	[Benchmark]
	[BenchmarkCategory("Update")]
	public GameState UpdateObject_Small()
	{
		var updatedPlayer = new Player
		{
			Name = "Updated Player",
			Health = 50,
			Level = 10,
		};
		return _smallState.UpdateObject(1, updatedPlayer);
	}

	[Benchmark]
	[BenchmarkCategory("Update")]
	public GameState UpdateObject_Medium()
	{
		var updatedPlayer = new Player
		{
			Name = "Updated Player",
			Health = 50,
			Level = 10,
		};
		return _mediumState.UpdateObject(1, updatedPlayer);
	}

	[Benchmark]
	[BenchmarkCategory("Update")]
	public GameState UpdateObject_Large()
	{
		var updatedPlayer = new Player
		{
			Name = "Updated Player",
			Health = 50,
			Level = 10,
		};
		return _largeState.UpdateObject(1, updatedPlayer);
	}

	#endregion

	#region Move Operations Benchmarks

	[Benchmark]
	[BenchmarkCategory("Move")]
	public GameState MoveObject_Small()
	{
		return _smallState.MoveObject(50, 25); // Move object in middle to another middle object
	}

	[Benchmark]
	[BenchmarkCategory("Move")]
	public GameState MoveObject_Medium()
	{
		return _mediumState.MoveObject(500, 250);
	}

	[Benchmark]
	[BenchmarkCategory("Move")]
	public GameState MoveObject_Large()
	{
		return _largeState.MoveObject(5000, 2500);
	}

	[Benchmark]
	[BenchmarkCategory("Move")]
	public GameState MoveObject_DeepHierarchy()
	{
		// Move an object from deep in the hierarchy to the root
		return _deepState.MoveObject(GetDeepObjectId(_deepState, 5), 0);
	}

	#endregion

	#region Remove Operations Benchmarks

	[Benchmark]
	[BenchmarkCategory("Remove")]
	public GameState RemoveObject_Small_KeepChildren()
	{
		return _smallState.RemoveObject(25, removeChildren: false);
	}

	[Benchmark]
	[BenchmarkCategory("Remove")]
	public GameState RemoveObject_Medium_KeepChildren()
	{
		return _mediumState.RemoveObject(250, removeChildren: false);
	}

	[Benchmark]
	[BenchmarkCategory("Remove")]
	public GameState RemoveObject_Small_RemoveChildren()
	{
		return _smallState.RemoveObject(25, removeChildren: true);
	}

	[Benchmark]
	[BenchmarkCategory("Remove")]
	public GameState RemoveObject_Medium_RemoveChildren()
	{
		return _mediumState.RemoveObject(250, removeChildren: true);
	}

	#endregion

	#region Query Operations Benchmarks

	[Benchmark]
	[BenchmarkCategory("Query")]
	public GameObject GetObject_Small()
	{
		return _smallState.GetObject(50);
	}

	[Benchmark]
	[BenchmarkCategory("Query")]
	public GameObject GetObject_Medium()
	{
		return _mediumState.GetObject(500);
	}

	[Benchmark]
	[BenchmarkCategory("Query")]
	public GameObject GetObject_Large()
	{
		return _largeState.GetObject(5000);
	}

	[Benchmark]
	[BenchmarkCategory("Query")]
	public int GetChildren_Small()
	{
		return _smallState.GetChildren(1).Count();
	}

	[Benchmark]
	[BenchmarkCategory("Query")]
	public int GetChildren_Medium()
	{
		return _mediumState.GetChildren(1).Count();
	}

	[Benchmark]
	[BenchmarkCategory("Query")]
	public int GetChildren_Large()
	{
		return _largeState.GetChildren(1).Count();
	}

	[Benchmark]
	[BenchmarkCategory("Query")]
	public int GetAllDescendants_Small()
	{
		return _smallState.GetAllDescendants(1).Count();
	}

	[Benchmark]
	[BenchmarkCategory("Query")]
	public int GetAllDescendants_Medium()
	{
		return _mediumState.GetAllDescendants(1).Count();
	}

	[Benchmark]
	[BenchmarkCategory("Query")]
	public int GetAllDescendants_DeepHierarchy()
	{
		return _deepState.GetAllDescendants(1).Count();
	}

	[Benchmark]
	[BenchmarkCategory("Query")]
	public GameObject LoadHierarchy_DeepHierarchy()
	{
		return _deepState.LoadHierarchy(1);
	}

	#endregion

	#region Batch Operations Benchmarks

	[Benchmark]
	[BenchmarkCategory("Batch")]
	public GameState MultipleUpdates_Small()
	{
		var state = _smallState;
		for (int i = 1; i <= 10; i++)
		{
			var updatedItem = new Item
			{
				Name = $"Updated Item {i}",
				Weight = i * 2,
				Value = i * 10,
			};
			state = state.UpdateObject(i + 1, updatedItem); // Skip the player at ID 1
		}
		return state;
	}

	[Benchmark]
	[BenchmarkCategory("Batch")]
	public GameState MultipleAdds_Small()
	{
		var state = _smallState;
		for (int i = 0; i < 10; i++)
		{
			var item = new Item
			{
				Name = $"Batch Item {i}",
				Weight = 1,
				Value = 5,
			};
			state = state.AddObject(item, parentId: 1);
		}
		return state;
	}

	[Benchmark]
	[BenchmarkCategory("Batch")]
	public GameState MultipleMoves_Small()
	{
		var state = _smallState;
		// Move several objects to different parents
		for (int i = 2; i <= 11; i++) // Skip player at ID 1
		{
			var newParent = i % 2 == 0 ? 1 : 0; // Alternate between player and root
			state = state.MoveObject(i, newParent);
		}
		return state;
	}

	#endregion

	#region Memory Usage Benchmarks

	[Benchmark]
	[BenchmarkCategory("Memory")]
	public GameState CreateLargeHierarchy()
	{
		return CreateFlatHierarchy(1000);
	}

	[Benchmark]
	[BenchmarkCategory("Memory")]
	public GameState CreateDeepHierarchy()
	{
		return CreateDeepHierarchy(8, 3); // 8 levels, 3 children each
	}

	#endregion

	#region Helper Methods

	private GameState CreateFlatHierarchy(int itemCount)
	{
		var player = new Player
		{
			Name = "Test Player",
			Health = 100,
			Level = 1,
		};
		var state = new GameState().AddObject(player);

		for (int i = 0; i < itemCount - 1; i++)
		{
			var item = new Item
			{
				Name = $"Item {i}",
				Weight = i % 10 + 1,
				Value = i * 5 + 10,
			};
			state = state.AddObject(item, parentId: 1);
		}

		return state;
	}

	private GameState CreateDeepHierarchy(int depth, int childrenPerLevel)
	{
		var root = new Player
		{
			Name = "Root Player",
			Health = 100,
			Level = 1,
		};
		var state = new GameState().AddObject(root);

		var currentParents = new List<int> { 1 };

		for (int level = 0; level < depth; level++)
		{
			var nextParents = new List<int>();

			foreach (var parentId in currentParents)
			{
				for (int child = 0; child < childrenPerLevel; child++)
				{
					var item = new Item
					{
						Name = $"Level{level}_Item{child}",
						Weight = level + 1,
						Value = (level + 1) * 10,
					};
					state = state.AddObject(item, parentId);
					nextParents.Add(state.NextId - 1); // The ID that was just assigned
				}
			}

			currentParents = nextParents;
		}

		return state;
	}

	private Player CreatePlayerWithItems(int itemCount)
	{
		var items = new List<GameObject>();
		for (int i = 0; i < itemCount; i++)
		{
			items.Add(
				new Item
				{
					Name = $"Item {i}",
					Weight = i % 5 + 1,
					Value = i * 3 + 5,
				}
			);
		}

		return new Player
		{
			Name = "Test Player",
			Health = 100,
			Level = 5,
			Children = items.ToImmutableList(),
		};
	}

	private int GetDeepObjectId(GameState state, int targetDepth)
	{
		var currentId = 1;
		for (int depth = 0; depth < targetDepth; depth++)
		{
			var children = state.GetChildrenIds(currentId).ToList();
			if (children.Any())
			{
				currentId = children.First();
			}
			else
			{
				break;
			}
		}
		return currentId;
	}

	#endregion
}

// Comparison benchmarks to test different data structure approaches
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[BenchmarkCategory("Comparison")]
public class DataStructureComparisonBenchmarks
{
	private GameState _gameState;
	private TraditionalGameObject _traditionalRoot;

	[GlobalSetup]
	public void Setup()
	{
		// Create flat hierarchy for immutable approach
		_gameState = CreateFlatGameState(1000);

		// Create traditional mutable hierarchy for comparison
		_traditionalRoot = CreateTraditionalHierarchy(1000);
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Comparison")]
	public GameObject FindObject_Immutable()
	{
		return _gameState.GetObject(500);
	}

	[Benchmark]
	[BenchmarkCategory("Comparison")]
	public TraditionalGameObject FindObject_Traditional()
	{
		return FindTraditionalObject(_traditionalRoot, "Item 499"); // 0-based indexing
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Comparison")]
	public GameState UpdateObject_Immutable()
	{
		var updatedItem = new Item
		{
			Name = "Updated Item",
			Weight = 99,
			Value = 999,
		};
		return _gameState.UpdateObject(500, updatedItem);
	}

	[Benchmark]
	[BenchmarkCategory("Comparison")]
	public TraditionalGameObject UpdateObject_Traditional()
	{
		var target = FindTraditionalObject(_traditionalRoot, "Item 499");
		if (target != null)
		{
			target.Name = "Updated Item";
		}
		return _traditionalRoot;
	}

	private GameState CreateFlatGameState(int itemCount)
	{
		var player = new Player
		{
			Name = "Test Player",
			Health = 100,
			Level = 1,
		};
		var state = new GameState().AddObject(player);

		for (int i = 0; i < itemCount - 1; i++)
		{
			var item = new Item
			{
				Name = $"Item {i}",
				Weight = i % 10 + 1,
				Value = i * 5 + 10,
			};
			state = state.AddObject(item, parentId: 1);
		}

		return state;
	}

	private TraditionalGameObject CreateTraditionalHierarchy(int itemCount)
	{
		var root = new TraditionalGameObject { Name = "Test Player" };

		for (int i = 0; i < itemCount - 1; i++)
		{
			var item = new TraditionalGameObject { Name = $"Item {i}", Parent = root };
			root.Children.Add(item);
		}

		return root;
	}

	private TraditionalGameObject FindTraditionalObject(TraditionalGameObject root, string name)
	{
		if (root.Name == name)
			return root;

		foreach (var child in root.Children)
		{
			var found = FindTraditionalObject(child, name);
			if (found != null)
				return found;
		}

		return null;
	}
}

// Traditional mutable game object for comparison
public class TraditionalGameObject
{
	public string Name { get; set; } = "";
	public TraditionalGameObject? Parent { get; set; }
	public List<TraditionalGameObject> Children { get; set; } = new();
}

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
