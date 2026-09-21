using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// Turns a <see cref="KinTarget"/> rule into the ids it means, right now.
///
/// **This whole file replaces MtgCore's `TargetingStrategy` + `TargetSpecifications`, 523 lines
/// between them.** Everything there exists to offer a player a legal set and take their pick. No
/// effect in DOOMJAM asks anyone anything, so a target is just a question about the board, and the
/// board can always answer it.
/// </summary>
public static class KinTargeting
{
	/// <param name="playedLane">
	/// The lane the source was played into, when it was played this instant and is not a unit.
	/// **This is what makes a rite a targeted card.** `PlayCardAction` has always carried a lane and
	/// only units validated it; passing it through means "the enemy in this lane", "your unit in
	/// this lane" and both adjacency rules work from a rite, with no targeting UI and nothing ever
	/// asking the player anything. The drop is the choice.
	/// </param>
	public static ImmutableList<int> Resolve(
		GameState state,
		KinTarget target,
		int sourceId,
		int? playedLane = null
	)
	{
		switch (target)
		{
			case KinTarget.None:
				return ImmutableList<int>.Empty;

			case KinTarget.Self:
				return state.HasObject(sourceId)
					? ImmutableList.Create(sourceId)
					: ImmutableList<int>.Empty;

			case KinTarget.Player:
				return ImmutableList.Create(state.GetPlayer().Id);

			case KinTarget.Opponent:
				return ImmutableList.Create(state.GetOpponent().Id);

			case KinTarget.AllEnemies:
				return state.LivingEnemies().Select(e => e.Id).ToImmutableList();

			case KinTarget.YourUnits:
				return state
					.Units()
					.Where(u => !u.Unit().IsDead)
					.Select(u => u.Id)
					.ToImmutableList();

			case KinTarget.EnemyInSourceLane:
			{
				if (SourceLane(state, sourceId, playedLane) is not { } lane)
					return ImmutableList<int>.Empty;

				return state.EnemyInLane(lane) is { } enemy
					? ImmutableList.Create(enemy.Id)
					: ImmutableList<int>.Empty;
			}

			case KinTarget.UnitInSourceLane:
			{
				if (SourceLane(state, sourceId, playedLane) is not { } lane)
					return ImmutableList<int>.Empty;

				return state.UnitInLane(lane) is { } unit
					? ImmutableList.Create(unit.Id)
					: ImmutableList<int>.Empty;
			}

			case KinTarget.YourUnitsInAdjacentLanes:
				return AdjacentLanes(state, sourceId, playedLane)
					.Select(state.UnitInLane)
					.Where(u => u is not null && !u.Unit().IsDead)
					.Select(u => u!.Id)
					.ToImmutableList();

			case KinTarget.EnemiesInAdjacentLanes:
				return AdjacentLanes(state, sourceId, playedLane)
					.Select(state.EnemyInLane)
					.Where(e => e is not null && !e.IsDead)
					.Select(e => e!.Id)
					.ToImmutableList();

			default:
				throw new ArgumentOutOfRangeException(
					nameof(target),
					$"No targeting rule for {target}. A new KinTarget without a case here would "
						+ "silently resolve to nothing, and an effect that hits nobody looks "
						+ "exactly like one that worked."
				);
		}
	}

	/// <summary>
	/// Does this target rule read a LANE? Then a rite carrying it is a targeted card, and where it
	/// is dropped is a real decision — which `KinBot` has to enumerate, or it will play every such
	/// rite into lane 0 for ever and measure the card as a blank.
	/// </summary>
	public static bool IsLaneScoped(KinTarget target) =>
		target
			is KinTarget.EnemyInSourceLane
				or KinTarget.UnitInSourceLane
				or KinTarget.YourUnitsInAdjacentLanes
				or KinTarget.EnemiesInAdjacentLanes;

	/// <summary>
	/// Which lane an effect's "here" means: the source unit's lane, or — for a rite, which has no
	/// body — the lane it was dropped on. Null when neither is known, and every lane-scoped target
	/// then resolves to nothing rather than guessing at lane 0.
	/// </summary>
	private static int? SourceLane(GameState state, int sourceId, int? playedLane)
	{
		if (!state.HasObject(sourceId))
			return playedLane;

		if (state.GetObject(sourceId) is not KinCard card)
			return playedLane;

		return card.GetComponent<UnitComponent>()?.Lane ?? playedLane;
	}

	/// <summary>
	/// The lanes either side of the source, in bounds. Empty when there is no lane to be beside —
	/// see <see cref="SourceLane"/>.
	///
	/// **A lane at the edge of the board has ONE neighbour, not two, and that is content rather
	/// than a limitation** — it makes the middle lane worth more than the outside ones to anything
	/// that reads adjacency, which is the first reason this game has ever had to prefer one lane
	/// over another.
	/// </summary>
	private static IEnumerable<int> AdjacentLanes(GameState state, int sourceId, int? playedLane)
	{
		if (SourceLane(state, sourceId, playedLane) is not { } lane)
			yield break;

		if (lane - 1 >= 0)
			yield return lane - 1;

		if (lane + 1 < KinBattle.LaneCount)
			yield return lane + 1;
	}
}
