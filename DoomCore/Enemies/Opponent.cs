using System.Collections.Immutable;
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

	/// <summary>
	/// What it will put on the board next, and where. Null when nothing is coming.
	///
	/// **Telegraphed a turn ahead**, like every intent in this game. It is announced at the end of
	/// one turn and lands at the end of the next, which is what gives you a full turn to use a lane
	/// you just cleared. Refilling the instant you broke through would mean never reaching the
	/// Opponent at all.
	/// </summary>
	public PendingSummon? NextSummon { get; init; }

	/// <summary>
	/// Turns between summons. **This is the rate limit that decides whether the player can get
	/// ahead**: refreshing every turn matches a player killing one unit a turn exactly, and the
	/// board would never open. Bigger than 1 is what makes breaking through possible.
	///
	/// Raised 2 -> 3 (2026-09-14) because dodging an apocalypse was unreachable at 2 — the summons
	/// closed damage lanes faster than the player could open them, so the Opponent could not be
	/// killed before the first firing and "apocalypses are dodgeable" was a rule with no teeth.
	/// Under playtest; see DoomJam.md.
	/// </summary>
	public int SummonInterval { get; init; } = 3;

	public int TurnsUntilSummon { get; init; } = 1;

	/// <summary>
	/// What this Opponent does on its own, beyond its units. "Heals every turn", "summons two at
	/// once", "cannot be killed while the countdown runs" — the per-battle inevitability lever
	/// DoomJam.md has wanted since dodging became content rather than a rule.
	/// </summary>
	public ImmutableList<DoomEffect> Effects { get; init; } = ImmutableList<DoomEffect>.Empty;

	public bool IsDead => Health <= 0;
}

/// <summary>
/// A summon the Opponent has announced but not yet made. Plain data with the lane already chosen,
/// because it is shown to the player before it happens — see <see cref="Opponent.NextSummon"/>.
/// </summary>
public record PendingSummon
{
	public string Name { get; init; } = "";
	public int Health { get; init; }
	public int Attack { get; init; }
	public int Lane { get; init; }

	public Enemy ToEnemy() =>
		new()
		{
			Name = Name,
			Health = Health,
			MaxHealth = Health,
			Intent = Attack > 0 ? IntentKind.Attack : IntentKind.Wait,
			IntentAmount = Attack,
			Lane = Lane,
		};
}
