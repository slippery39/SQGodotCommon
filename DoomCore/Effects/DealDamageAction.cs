using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Deals a fixed amount to every target, whatever kind of thing each one is.
///
/// **One action holds all its targets and loops.** That is MtgCore's canonical pattern and the
/// reason is the same here: spawning one action per target makes ordering and events harder to
/// reason about for no gain.
///
/// Damage means different things to different holders, and all three are the same currency: an
/// enemy and the Opponent lose health, a unit takes marked damage against its toughness. **A unit
/// absorbs up to its remaining toughness and no further** — this does not spill excess into the
/// face, because that rule belongs to a lane exchange, not to a burn effect.
/// </summary>
public record DealDamageAction : EffectAction
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		foreach (var id in TargetIds)
		{
			// Skipped, not fatal: things die between an effect being queued and resolving, and that
			// is ordinary. ValidateResolve already fails the action if EVERY target is gone.
			if (!state.HasObject(id))
				continue;

			switch (state.GetObject(id))
			{
				case Enemy enemy:
				{
					var hurt = enemy with { Health = enemy.Health - Amount };
					state = state.UpdateObject(id, hurt);

					if (hurt.IsDead)
						events = events.Add(
							new EnemyDiedEvent { EnemyId = id, EnemyName = enemy.Name }
						);

					break;
				}

				case Opponent opponent:
				{
					var health = opponent.Health - Amount;
					state = state.UpdateObject(id, opponent with { Health = health });
					events = events.Add(
						new OpponentDamagedEvent { Amount = Amount, HealthRemaining = health }
					);
					break;
				}

				case DoomCard card when card.GetComponent<UnitComponent>() is { } unit:
				{
					state = state.UpdateObject(
						id,
						card.WithComponentReplaced(unit with { Damage = unit.Damage + Amount })
					);
					break;
				}

				case DoomPlayer player:
				{
					var life = player.Life - Amount;
					state = state.UpdateObject(id, player with { Life = life });
					events = events.Add(
						new PlayerDamagedEvent { Amount = Amount, LifeRemaining = life }
					);
					break;
				}
			}
		}

		return new ActionResult(state).WithEvents(events);
	}
}
