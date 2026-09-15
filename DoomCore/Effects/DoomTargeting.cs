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

			default:
				throw new ArgumentOutOfRangeException(
					nameof(target),
					$"No targeting rule for {target}. A new DoomTarget without a case here would "
						+ "silently resolve to nothing, and an effect that hits nobody looks "
						+ "exactly like one that worked."
				);
		}
	}
}
