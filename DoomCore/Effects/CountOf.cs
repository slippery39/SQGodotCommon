using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Something on the board an effect can be multiplied by, so an amount can SCALE.
///
/// **Every effect amount in the game was a literal before this** — `DealDamageAction { Amount = 6 }`
/// — so no card could get bigger, no card could reward a board you had built, and there was nothing
/// to build toward. That is most of why the pool had no synergies: not that nobody wrote them, but
/// that they were unsayable.
///
/// **It matters far more under combat v3 than it would have under v2.** Scaling is a deckbuilding
/// outcome now (see DoomJam.md) — the board no longer accumulates and hands you growth for free, so
/// the only place growth can come from is cards that grow. This enum is what lets one exist.
///
/// Serializable by construction: an enum, read at resolve time. **No delegates** — the whole reason
/// this is a `CountOf` and not a `Func&lt;GameState,int&gt;`. See the Serialization Rule in CLAUDE.md.
/// </summary>
public enum CountOf
{
	/// <summary>Not scaled. The amount is used exactly as authored.</summary>
	None = 0,

	/// <summary>Units you hold on the field right now, companion included.</summary>
	YourUnits,

	/// <summary>Enemies still standing.</summary>
	LivingEnemies,

	/// <summary>
	/// Your units the enemy KILLED last turn. Not the ones that withdrew — see
	/// <see cref="DoomBattle.DiedLastTurnRunCardIds"/>. This is the attrition axis, and it only
	/// means anything because combat v3 made withdrawing and dying different events.
	/// </summary>
	DiedLastTurn,

	/// <summary>Cards you have played so far this turn. The volume axis.</summary>
	CardsPlayedThisTurn,

	/// <summary>Apocalypses that have landed this battle. Rewards eating them rather than racing.</summary>
	DoomsFired,
}

/// <summary>
/// Reads a <see cref="CountOf"/> off the board. One switch, the same shape as
/// <see cref="DoomTargeting"/> and for the same reason: a count is a question the board can always
/// answer, so nothing has to ask a player anything.
/// </summary>
public static class DoomCounts
{
	/// <summary>
	/// The multiplier an effect should use. **<see cref="CountOf.None"/> is 1, not 0** — an
	/// unscaled effect must deal exactly what it says, and returning 0 would silently turn every
	/// existing card in the game into a no-op.
	/// </summary>
	public static int Multiplier(GameState state, CountOf count) =>
		count == CountOf.None ? 1 : Count(state, count);

	public static int Count(GameState state, CountOf count) =>
		count switch
		{
			CountOf.None => 0,
			CountOf.YourUnits => state.Units().Count(u => !u.Unit().IsDead),
			CountOf.LivingEnemies => state.LivingEnemies().Count(),
			CountOf.DiedLastTurn => state.GetBattle().DiedLastTurnRunCardIds.Count,
			CountOf.CardsPlayedThisTurn => state.GetBattle().CardsPlayedThisTurn,
			CountOf.DoomsFired => state.GetBattle().DoomsFired,
			_ => throw new ArgumentOutOfRangeException(
				nameof(count),
				$"No reading for {count}. A new CountOf without a case here would scale every "
					+ "effect that used it to zero, and an effect that does nothing looks exactly "
					+ "like one that worked."
			),
		};
}
