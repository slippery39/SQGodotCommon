using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// Makes a card a unit. Present in hand too — a unit card is a unit wherever it is.
///
/// **Toughness is life.** A unit in a lane absorbs up to its remaining toughness and the excess
/// hits your face, so a point of toughness standing in a lane is worth exactly a point of life.
/// That equivalence is the single axis every doom scenario trades on, so keep it exact.
/// </summary>
public record UnitComponent : GameComponent
{
	public int Power { get; init; }
	public int Toughness { get; init; }

	/// <summary>Damage marked this battle. Persists across turns; a unit dies when it reaches Toughness.</summary>
	public int Damage { get; init; }

	/// <summary>
	/// Which of the five lanes this unit holds, 0-4. Meaningless off the Field.
	///
	/// **The lane is the only decision a unit carries.** There is no attack-or-block choice and no
	/// targeting: a unit in a lane fights whatever enemy is in that lane, automatically, both ways.
	/// Choosing which lanes to contest — and which to let through to your face — is the battle.
	/// </summary>
	public int Lane { get; init; }

	public int RemainingToughness => Toughness - Damage;

	public bool IsDead => Damage >= Toughness;
}
