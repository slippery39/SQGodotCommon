using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// One firing of the doom, and what it read AT THAT MOMENT.
///
/// **This exists because the doom now fires repeatedly.** A transform applied once at the end could
/// only ever see the final board, so a Nuclear that fired on turn 3 and again on turn 9 would
/// irradiate the turn-9 board twice and the turn-3 board never. Each firing has to carry its own
/// snapshot.
///
/// It records RUN ids, not battle ids, for the same reason
/// <see cref="DoomBattle.SummonedRunCardIds"/> does: battle object ids die with the battle, and the
/// transform rewrites a deck that outlives it.
///
/// Plain data, no delegates — this lives on <see cref="DoomBattle"/>, which lives in GameState.
/// </summary>
public record DoomFiring
{
	public DoomScenario Scenario { get; init; } = DoomScenario.None;

	/// <summary>Which turn it landed on. For the log and for the report, not for any rule.</summary>
	public int TurnNumber { get; init; }

	/// <summary>Units on the Field when it fired. What Nuclear reads.</summary>
	public ImmutableHashSet<int> OnFieldRunCardIds { get; init; } = ImmutableHashSet<int>.Empty;

	/// <summary>
	/// Deaths SINCE THE PREVIOUS FIRING. What Zombie reads.
	///
	/// Since, not total — a cumulative list would pay Zombie for the same death once per remaining
	/// firing, so a long battle would mint an exponential pile of Zombies.
	/// </summary>
	public ImmutableList<int> DiedRunCardIds { get; init; } = ImmutableList<int>.Empty;

	/// <summary>Deaths on the turn it landed. The narrow window — see DoomBattle.</summary>
	public ImmutableList<int> DiedThisTurnRunCardIds { get; init; } = ImmutableList<int>.Empty;

	/// <summary>Units committed to the Field at any point before this firing.</summary>
	public ImmutableHashSet<int> SummonedRunCardIds { get; init; } = ImmutableHashSet<int>.Empty;

	/// <summary>
	/// What a firing RIGHT NOW would read off this battle.
	///
	/// **The single source of that answer**, used by both `ResolveDoomAction` (the real firing) and
	/// `DoomPreviewer` (the "if it landed now" dial). Two copies would drift, and the player would
	/// be planning around a preview that no longer matches the apocalypse — the same rule that keeps
	/// the preview running the real transform instead of describing it.
	/// </summary>
	public static DoomFiring Capture(GameState state, int turnNumber)
	{
		var battle = state.GetBattle();

		return new DoomFiring
		{
			Scenario = battle.Scenario,
			TurnNumber = turnNumber,
			OnFieldRunCardIds = state.Units().Select(u => u.RunCardId).ToImmutableHashSet(),
			DiedRunCardIds = battle.DiedRunCardIds,
			DiedThisTurnRunCardIds = battle.DiedThisTurnRunCardIds,
			SummonedRunCardIds = battle.SummonedRunCardIds,
		};
	}
}
