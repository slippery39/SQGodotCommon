using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// The entity the enemy units belong to, sitting behind their lanes the way the player sits behind
/// theirs. **Killing it is the only way to win a battle.**
///
/// It exists to make the board symmetric. A lane you hold with no enemy in it is no longer merely a
/// leak you plugged — your unit there hits the Opponent, so holding a lane is offence and defence in
/// the same act. Three energy will not cover five lanes, and that tension is the battle.
///
/// It has no attack of its own. Everything it does, it does through its units — see
/// <see cref="Enemy"/>, whose intents are telegraphed a turn ahead.
/// </summary>
public record Opponent : GameObject
{
	public int Health { get; init; }
	public int MaxHealth { get; init; }

	public bool IsDead => Health <= 0;
}
