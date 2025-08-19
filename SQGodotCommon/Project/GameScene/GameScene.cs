namespace Project;

/// <summary>
/// Holds all the nodes that are needed for an in-game scene. Ex. the actual scene the player plays in.
/// This should hold the world, and the in-game UI etc..
///
/// Should also hold any specific data that is needed for nodes in the scene, i.e. PlayerStats, Scores, etc..
/// </summary>
public partial class GameScene : Node
{
	public GameWorld World => GetNode<GameWorld>("World");
	public GameUI UI => GetNode<GameUI>("UI");
}

public static class GameSceneExtensions
{
	/// <summary>
	/// Traverses up the scene tree to find the "World" node, ensuring we get the proper in-game world node.
	/// Useful when the root node is not necessarily the World node (e.g., when using custom scene hierarchies).
	/// </summary>
	/// <param name="node">The node from which to start the search.</param>
	/// <returns>The World node if found; otherwise, null.</returns>
	public static GameWorld GetWorld(this Node2D node)
	{
		Node current = node;

		while (current != null)
		{
			if (current is GameWorld world)
				return world;

			current = current.GetParent();
		}

		return null;
	}

	/// <summary>
	/// Traverses up the scene tree to find the "GameScene" node, ensuring we get the proper node which contains the GameScene.    ///
	/// </summary>
	/// <param name="node"></param>
	/// <returns></returns>
	public static GameScene GetGameScene(this Node node)
	{
		Node current = node;

		while (current != null)
		{
			if (current is GameScene gameScene)
				return gameScene;

			current = current.GetParent();
		}

		return null;
	}
}
