using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// The player. Owns the Draw, Hand, Discard and Field zones.
///
/// Life persists across battles and never heals on its own (STS-style) — it is the only fail state
/// in the game, which is why enemies matter at all when the doom itself costs no life.
/// </summary>
public record KinPlayer : GameObject
{
	public int Life { get; init; }
	public int MaxLife { get; init; }

	/// <summary>Spent to play cards. Refills to <see cref="MaxEnergy"/> every turn.</summary>
	public int Energy { get; init; }
	public int MaxEnergy { get; init; } = 3;
}
