using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// **The core hook of the whole game: read the board at countdown 0, rewrite the run deck.**
///
/// Every apocalypse is one function of (run, finished battle) → run. There is deliberately no
/// second mechanism — a new scenario is a new case here plus a value on
/// <see cref="DoomScenario"/>, and nothing else in the engine changes.
///
/// **Every scenario must be a BARGAIN, never a pure tax.** It converts one resource into another,
/// and a greedy line must exist. A deck that only ever gets worse is a misery engine players quit,
/// and it makes escalating enemies unbalanceable — the apocalypses ARE the power curve, so there
/// is no separate progression system to lean on.
/// </summary>
public static class DoomTransforms
{
	public const string ZombieTag = "Zombie";
	public const string IrradiatedTag = "Irradiated";

	/// <summary>
	/// Replays every firing of the battle against the run, in order.
	///
	/// **A fold, because the doom recurs.** Each firing carries its own snapshot of what it read
	/// (<see cref="DoomFiring"/>), so a Nuclear that fired twice irradiates whatever was standing
	/// on each occasion rather than the final board twice.
	/// </summary>
	public static Run Apply(Run run, GameState finishedBattle) =>
		finishedBattle
			.GetBattle()
			.Firings
			// Battle-scope firings already happened, inside the battle. Replaying them here would
			// apply them twice — and against a deck they were never meant to touch.
			.Where(f => StarterContent.ScopeOf(f.Scenario) == DoomScope.Permanent)
			.Aggregate(run, ApplyFiring);

	public static Run ApplyFiring(Run run, DoomFiring firing)
	{
		if (StarterContent.ScopeOf(firing.Scenario) != DoomScope.Permanent)
			throw new InvalidOperationException(
				$"{firing.Scenario} is a Battle scenario and belongs in DoomBattleEffects, not "
					+ "here. A scenario in the wrong hook does nothing at all and looks exactly "
					+ "like one that worked."
			);

		return firing.Scenario switch
		{
			DoomScenario.None => run,
			DoomScenario.Zombie => Zombie(run, firing),
			DoomScenario.Nuclear => Nuclear(run, firing),
			DoomScenario.Flood => Flood(run, firing),

			// Rapture needs a sacrifice mechanic, which does not exist yet. Throwing beats a silent
			// no-op: an apocalypse that quietly does nothing looks exactly like one that worked.
			DoomScenario.Rapture => throw new NotSupportedException(
				"Rapture needs sacrifice, which is not implemented. Do not offer it as a floor yet."
			),

			_ => throw new ArgumentOutOfRangeException(nameof(firing.Scenario)),
		};
	}

	/// <summary>
	/// Reads what DIED. Every death returns as a 1/1 Zombie in the deck.
	///
	/// The bargain is quantity for quality: bodies are life (toughness absorbs damage), but each
	/// one dilutes the deck. Feeding it your good units is the greedy line and the trap at once.
	/// </summary>
	private static Run Zombie(Run run, DoomFiring firing) =>
		run.WithCards(
			firing.DiedRunCardIds.Select(_ => new RunCard
			{
				Name = "Zombie",
				Description = "Shambles back. 1/1.",
				Cost = 0,
				IsUnit = true,
				Power = 1,
				Toughness = 1,
				Tags = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, ZombieTag),
			})
		);

	/// <summary>
	/// Reads what was LEFT ON THE FIELD. Those units become Irradiated: permanently +2/+2, and you
	/// lose 1 life every time you draw one.
	///
	/// The bargain is a stronger, sicker deck. Leaving your best unit exposed is a temptation, not
	/// a mistake — the thing that saves life costs life, which is the torch from Darkest Dungeon.
	/// Already-Irradiated units stack the buff and the drawback both.
	/// </summary>
	private static Run Nuclear(Run run, DoomFiring firing)
	{
		var exposed = firing.OnFieldRunCardIds;

		return run with
		{
			Deck = run
				.Deck.Select(card =>
					!exposed.Contains(card.RunCardId)
						? card
						: card with
						{
							Power = card.Power + 2,
							Toughness = card.Toughness + 2,
							Tags = card.Tags.Add(IrradiatedTag),
						}
				)
				.ToImmutableList(),
		};
	}

	/// <summary>
	/// Reads what you COMMITTED. Units summoned this battle are duplicated; units never summoned
	/// are removed from the deck entirely.
	///
	/// The flagship scenario and the one that forced the whole run layer to exist. It is pure
	/// bargain — thin the deck violently and double down on what you played — and it is the only
	/// thing in the game that can permanently REMOVE a card from a run.
	///
	/// Non-units are untouched: the flood takes the living.
	/// </summary>
	private static Run Flood(Run run, DoomFiring firing)
	{
		var survivors = run
			.Deck.Where(card => !card.IsUnit || firing.SummonedRunCardIds.Contains(card.RunCardId))
			.ToImmutableList();

		var duplicated = survivors.Where(card => card.IsUnit).ToList();

		return (run with { Deck = survivors }).WithCards(duplicated);
	}
}
