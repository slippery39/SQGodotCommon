namespace ImmutableGameObjects;

/// <summary>
/// Base record for all components that can be attached to a GameObject.
/// Components are strongly typed pieces of data that augment a GameObject
/// without requiring a new subclass. Multiple components of the same type
/// are allowed — callers decide whether they expect one or many.
///
/// Components are pure data — no behaviour, no delegates.
/// </summary>
public abstract record GameComponent;
