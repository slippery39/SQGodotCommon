using ImmutableGameObjects;

namespace DoomCore;

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

	public bool IsDead => Health <= 0;
}
