using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Fires only when every sub-condition fires. Lets an "intervening if" clause be expressed
/// without a bespoke condition type per card:
/// "when this enters, IF an opponent controls more lands than you, ..."
/// </summary>
public record AndTriggerCondition : TriggerCondition
{
	public ImmutableList<TriggerCondition> Conditions { get; init; } =
		ImmutableList<TriggerCondition>.Empty;

	public override bool IsSatisfiedBy(GameEvent gameEvent, TriggerContext context)
	{
		foreach (var condition in Conditions)
			if (!condition.IsSatisfiedBy(gameEvent, context))
				return false;

		return !Conditions.IsEmpty;
	}
}

/// <summary>
/// True while an opponent controls more lands than the ability's controller — Knight of the
/// White Orchid's intervening-if clause.
///
/// Event-agnostic on purpose: it answers a board question, not an event question, so it is meant
/// to be combined with a real event condition via AndTriggerCondition.
///
/// Land count is MtgPlayer.LandsPlayedTotal, not a battlefield scan, because lands are consumed
/// into MaxMana and exiled when played — there are no land permanents to count.
/// </summary>
public record OpponentControlsMoreLandsCondition : TriggerCondition
{
	public override bool IsSatisfiedBy(GameEvent gameEvent, TriggerContext context) =>
		LandCounts.OpponentHasMore(context.GameState, context.ControllingPlayerId);
}

/// <summary>
/// Shared land comparison, so ControlsMoreLandsCondition (activated abilities) and
/// OpponentControlsMoreLandsCondition (triggers) cannot drift apart on what "more lands" means.
/// </summary>
public static class LandCounts
{
	public static bool OpponentHasMore(GameState state, int playerId) =>
		Compare(state, playerId, (you, opponent) => opponent > you);

	public static bool YouHaveMore(GameState state, int playerId) =>
		Compare(state, playerId, (you, opponent) => you > opponent);

	private static bool Compare(GameState state, int playerId, Func<int, int, bool> predicate)
	{
		if (state.GetObject(playerId) is not MtgPlayer player)
			return false;

		var p1Id = state.GetWellKnownId(MtgObjectKeys.Player1);
		var opponentId = playerId == p1Id ? state.GetWellKnownId(MtgObjectKeys.Player2) : p1Id;

		return state.GetObject(opponentId) is MtgPlayer opponent
			&& predicate(player.LandsPlayedTotal, opponent.LandsPlayedTotal);
	}
}
