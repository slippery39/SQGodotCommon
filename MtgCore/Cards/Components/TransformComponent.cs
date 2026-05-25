using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Stores the other face of a double-faced card.
/// The component travels with the card regardless of which face is active:
/// the current face is the card itself; this component holds the other face's
/// name, subtypes, and non-transform components so TransformAction can swap them.
///
/// Back-and-forth transforms work symmetrically — TransformAction always
/// rebuilds a fresh TransformComponent pointing back at the face it came from.
/// </summary>
public record TransformComponent : GameComponent
{
	public string OtherFaceName { get; init; } = "";
	public ImmutableHashSet<string> OtherFaceSubtypes { get; init; } =
		ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);
	public ImmutableArray<GameComponent> OtherFaceComponents { get; init; } =
		ImmutableArray<GameComponent>.Empty;
}
