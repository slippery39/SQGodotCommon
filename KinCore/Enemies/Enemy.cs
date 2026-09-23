using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

public enum IntentKind
{
	/// <summary>Does nothing this turn.</summary>
	Wait = 0,
	Attack,
}

/// <summary>
/// An enemy. Not a card and never in the deck — enemies are the battle's threat, not its content.
///
/// Intents are TELEGRAPHED a turn ahead and always visible. That is deliberate: the whole theme is
/// that certainty is permission to show the player everything, so the tension is inevitability
/// rather than surprise. Do not hide an intent.
/// </summary>
public record Enemy : GameObject
{
	public int Health { get; init; }
	public int MaxHealth { get; init; }

	public IntentKind Intent { get; init; } = IntentKind.Wait;

	/// <summary>Damage the telegraphed attack will deal. Meaningless when Intent is Wait.</summary>
	public int IntentAmount { get; init; }

	/// <summary>
	/// Which of the five lanes this enemy occupies, 0-4.
	///
	/// An enemy only ever fights the unit in its own lane, and only ever hits your face from its own
	/// lane. One enemy per lane: the lane IS the matchup.
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

	/// <summary>
	/// **Flier — its attack goes OVER the unit in its lane and lands on you.** Your unit still hits
	/// it, and still takes its Thorns if it has any, but soaks nothing of its attack — and since
	/// your unit was never attacked, your Thorns never fire either.
	///
	/// **It exists to be the Bulwark deck's problem.** A wall is the universal answer to an attack
	/// only while attacks have to go through it; a Flier is the enemy that asks you to KILL rather
	/// than absorb.
	/// </summary>
	public bool Flies { get; init; }

	/// <summary>
	/// What this enemy does beyond hitting its lane. Same `KinEffect` a card carries — that is the
	/// point of it not being card-specific: one `DealDamageAction` serves a rite and a dying enemy.
	/// </summary>
	public ImmutableList<KinEffect> Effects { get; init; } = ImmutableList<KinEffect>.Empty;

	public bool IsDead => Health <= 0;
}
