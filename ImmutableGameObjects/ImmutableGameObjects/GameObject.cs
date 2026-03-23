using System.Collections.Immutable;

namespace ImmutableGameObjects;

public abstract record GameObject
{
	public int Id { get; init; }
	public string Name { get; init; } = "";
	public string Description { get; init; } = "";

	/// <summary>
	/// For initialization or view purposes only. Internals do not use this as a source of truth.
	/// </summary>
	public virtual ImmutableList<GameObject> Children { get; init; } =
		ImmutableList<GameObject>.Empty;

	// ===== COMPONENT SYSTEM =====

	/// <summary>
	/// Strongly typed data attachments. Multiple components of the same type are allowed.
	/// Use GetComponent/GetComponents to retrieve, WithComponent/WithoutComponents to modify.
	/// Changes must be applied back to GameState via UpdateObject to take effect.
	/// </summary>
	public ImmutableList<GameComponent> Components { get; init; } =
		ImmutableList<GameComponent>.Empty;

	/// <summary>
	/// Returns the first component of type T, or null if none exists.
	/// </summary>
	public T? GetComponent<T>()
		where T : GameComponent => Components.OfType<T>().FirstOrDefault();

	/// <summary>
	/// Returns all components of type T.
	/// </summary>
	public IEnumerable<T> GetComponents<T>()
		where T : GameComponent => Components.OfType<T>();

	/// <summary>
	/// Returns true if this object has at least one component of type T.
	/// </summary>
	public bool HasComponent<T>()
		where T : GameComponent => Components.OfType<T>().Any();

	/// <summary>
	/// Returns a new GameObject with the given component added.
	/// Does not remove existing components of the same type.
	/// </summary>
	public GameObject WithComponent(GameComponent component) =>
		this with
		{
			Components = Components.Add(component),
		};

	/// <summary>
	/// Returns a new GameObject with all components of type T removed.
	/// </summary>
	public GameObject WithoutComponents<T>()
		where T : GameComponent => this with { Components = Components.RemoveAll(c => c is T) };

	/// <summary>
	/// Returns a new GameObject with all components of type T replaced by the given component.
	/// Equivalent to WithoutComponents followed by WithComponent.
	/// </summary>
	public GameObject WithComponentReplaced<T>(T component)
		where T : GameComponent =>
		this with
		{
			Components = Components.RemoveAll(c => c is T).Add(component),
		};

	// ===== METADATA =====

	/// <summary>
	/// Lightweight dynamic key-value store for sparse, one-off data that doesn't
	/// warrant a full component type. Use components for anything strongly typed
	/// or shared across multiple object types.
	/// Changes must be applied back to GameState via UpdateObject to take effect.
	/// </summary>
	public ImmutableDictionary<string, object> Metadata { get; init; } =
		ImmutableDictionary<string, object>.Empty;

	/// <summary>
	/// Returns the metadata value for the given key, cast to T.
	/// Returns defaultValue if the key does not exist.
	/// </summary>
	public T GetMeta<T>(string key, T defaultValue = default!) =>
		Metadata.TryGetValue(key, out var value) ? (T)value : defaultValue;

	/// <summary>
	/// Returns a new GameObject with the given metadata key set to value.
	/// Overwrites any existing value for the same key.
	/// </summary>
	public GameObject WithMeta(string key, object value) =>
		this with
		{
			Metadata = Metadata.SetItem(key, value),
		};

	/// <summary>
	/// Returns a new GameObject with the given metadata key removed.
	/// </summary>
	public GameObject WithoutMeta(string key) => this with { Metadata = Metadata.Remove(key) };

	// ===== HIERARCHY =====

	public GameObject LoadFrom(GameState gameState)
	{
		var children = gameState
			.GetChildren(this.Id)
			.Select(child => child.LoadFrom(gameState))
			.ToImmutableList();

		return this with
		{
			Children = children,
		};
	}
}
