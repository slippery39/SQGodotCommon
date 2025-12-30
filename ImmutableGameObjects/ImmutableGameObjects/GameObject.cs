using System.Collections.Immutable;

namespace ImmutableGameObjects;

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
