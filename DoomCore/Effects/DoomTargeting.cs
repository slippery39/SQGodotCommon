using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Turns a <see cref="DoomTarget"/> rule into the ids it means, right now.
///
/// **This whole file replaces MtgCore's `TargetingStrategy` + `TargetSpecifications`, 523 lines
/// between them.** Everything there exists to offer a player a legal set and take their pick. No
/// effect in DOOMJAM asks anyone anything, so a target is just a question about the board, and the
/// board can always answer it.
/// </summary>
public static class DoomTargeting
{
	public static ImmutableList<int> Resolve(GameState state, DoomTarget target, int sourceId)
	{
		switch (target)
		{
			case DoomTarget.None:
				return ImmutableList<int>.Empty;

			case DoomTarget.Self:
				return state.HasObject(sourceId)
					? ImmutableList.Create(sourceId)
					: ImmutableList<int>.Empty;

			case DoomTarget.Player:
				return ImmutableList.Create(state.GetPlayer().Id);

			case DoomTarget.Opponent:
				return ImmutableList.Create(state.GetOpponent().Id);

			case DoomTarget.AllEnemies:
				return state.LivingEnemies().Select(e => e.Id).ToImmutableList();

			case DoomTarget.YourUnits:
				return state
					.Units()
					.Where(u => !u.Unit().IsDead)
					.Select(u => u.Id)
					.ToImmutableList();

			case DoomTarget.EnemyInSourceLane:
			{
				// The source has to be a unit for "its lane" to mean anything. A rite played from
				// hand has no lane, so this resolves to nothing rather than guessing at lane 0.
				if (!state.HasObject(sourceId))
					return ImmutableList<int>.Empty;

				if (state.GetObject(sourceId) is not DoomCard { } card)
					return ImmutableList<int>.Empty;

				var unit = card.GetComponent<UnitComponent>();
				if (unit is null)
					return ImmutableList<int>.Empty;

				return state.EnemyInLane(unit.Lane) is { } enemy
					? ImmutableList.Create(enemy.Id)
					: ImmutableList<int>.Empty;
			}

			case DoomTarget.YourUnitsInAdjacentLanes:
				return AdjacentLanes(state, sourceId)
					.Select(state.UnitInLane)
					.Where(u => u is not null && !u.Unit().IsDead)
					.Select(u => u!.Id)
					.ToImmutableList();

			case DoomTarget.EnemiesInAdjacentLanes:
				return AdjacentLanes(state, sourceId)
					.Select(state.EnemyInLane)
					.Where(e => e is not null && !e.IsDead)
					.Select(e => e!.Id)
					.ToImmutableList();

			default:
				throw new ArgumentOutOfRangeException(
					nameof(target),
					$"No targeting rule for {target}. A new DoomTarget without a case here would "
						+ "silently resolve to nothing, and an effect that hits nobody looks "
						+ "exactly like one that worked."
				);
		}
	}

	/// <summary>
	/// The lanes either side of the source, in bounds. Empty when the source is not a unit — a rite
	/// played from hand has no lane, so "next door" means nothing and resolves to nothing rather
	/// than guessing at lane 0. Same rule <see cref="DoomTarget.EnemyInSourceLane"/> already uses.
	///
	/// **A lane at the edge of the board has ONE neighbour, not two, and that is content rather
	/// than a limitation** — it makes the middle lane worth more than the outside ones to anything
	/// that reads adjacency, which is the first reason this game has ever had to prefer one lane
	/// over another.
	/// </summary>
	private static IEnumerable<int> AdjacentLanes(GameState state, int sourceId)
	{
		if (!state.HasObject(sourceId))
			yield break;

		if (state.GetObject(sourceId) is not DoomCard card)
			yield break;

		if (card.GetComponent<UnitComponent>() is not { } unit)
			yield break;

		if (unit.Lane - 1 >= 0)
			yield return unit.Lane - 1;

		if (unit.Lane + 1 < DoomBattle.LaneCount)
			yield return unit.Lane + 1;
	}
}
