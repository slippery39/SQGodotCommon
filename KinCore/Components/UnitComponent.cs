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

	/// <summary>
	/// **Thorns N — when this is ATTACKED, the attacker takes N.** Both sides carry it.
	///
	/// It is damage that does not come from the intent, so it lands ON TOP of the trade rather
	/// than replacing any of it: a wall that soaks a telegraphed 8 and takes nothing now takes
	/// 8 + N, and the excess spills to your face like any other overflow. **That is the whole
	/// point — it is the only thing in the game that makes a big toughness body an unsafe
	/// answer.**
	///
	/// Blank in an open lane, deliberately, so a thorns deck still has to draft a finisher.
	/// </summary>
	public int Thorns { get; init; }

	/// <summary>
	/// **Strikes N — this deals its damage N times in one exchange.** Both sides carry it.
	///
	/// **Defaults to 1, and it has to**: a 0 here would mean every unit already in the game stops
	/// dealing damage, silently.
	///
	/// It is a HOOK, not a number. It collapses to "double power" only in a game with no per-hit
	/// rules, and thorns is exactly such a rule — a double-striker walks into a thorns wall twice.
	/// **Thorns and Strikes are anti-synergistic on purpose**, and that pairing is the first time
	/// placing a unit is a question about WHICH unit rather than which lane.
	/// </summary>
	public int Strikes { get; init; } = 1;

	public int RemainingToughness => Toughness - Damage;

	public bool IsDead => Damage >= Toughness;
}
