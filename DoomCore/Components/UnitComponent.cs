using ImmutableGameObjects;

namespace DoomCore;

/// <summary>What a unit is assigned to do this turn. A unit does ONE of these, never both.</summary>
public enum Assignment
{
	None = 0,
	Attack,
	Block,
}

/// <summary>
/// Makes a card a unit. Present in hand too — a unit card is a unit wherever it is.
///
/// **Toughness is life.** Blocking reduces damage rather than preventing it, so a point of
/// toughness placed in front of an attack is worth exactly a point of life. That equivalence is
/// the single axis every doom scenario trades on, so keep it exact.
/// </summary>
public record UnitComponent : GameComponent
{
	public int Power { get; init; }
	public int Toughness { get; init; }

	/// <summary>Damage marked this battle. A unit dies when it reaches Toughness.</summary>
	public int Damage { get; init; }

	/// <summary>
	/// Attack or block this turn — never both. That either/or is the core tactical decision of a
	/// battle: kill an enemy sooner and remove all its future damage, or absorb damage now.
	/// Cleared at the start of every turn.
	/// </summary>
	public Assignment Assignment { get; init; } = Assignment.None;

	/// <summary>The enemy this unit attacks, or the enemy whose attack it blocks. 0 when unassigned.</summary>
	public int AssignedEnemyId { get; init; }

	public int RemainingToughness => Toughness - Damage;

	public bool IsDead => Damage >= Toughness;
}
