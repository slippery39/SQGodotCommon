using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

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

		// Scaled ONCE, before the loop, so every target takes the same number. Reading the count
		// per target would let the effect shrink as it killed things — "damage equal to enemies
		// standing" would deal less to each successive enemy, which is not what the card says.
		var amount = Scaled(state, Amount);
		if (amount == 0)
			return new ActionResult(state);

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
					// **HEALING CANNOT EXCEED MAXHEALTH, and this used to be the other way round.**
					//
					// A negative amount is how healing is written. The old rule let the maximum
					// RISE to meet an overheal, reasoning that the lane cell draws health against
					// MaxHealth so clamping the bar would show a lie. That was a DISPLAY argument
					// and it produced a balance disaster: `Gravecaller` heals 4 a turn and sits at
					// full health, so it grew by 4 every turn **without bound for the whole
					// battle** — and the `Shepherd` trait did it to an entire enemy line at once.
					// Found in a playtest: "they can heal past their original health, which makes
					// them super hard to beat, especially when they start to take over all lanes."
					//
					// If something should genuinely get BIGGER, that is a `BuffAction`, which says
					// so on the card. Healing restores; it does not grow.
					var healed = Math.Min(enemy.Health - amount, enemy.MaxHealth);
					var hurt = enemy with { Health = healed };
					state = state.UpdateObject(id, hurt);

					if (hurt.IsDead)
						events = events.Add(
							new EnemyDiedEvent { EnemyId = id, EnemyName = enemy.Name }
						);

					break;
				}

				case Opponent opponent:
				{
					// Same rule, and this is where it bit hardest: The Choir heals 2 a turn, The Last
					// Morning 4, and the `Zealous` trait another 4 on top. At full health each of
					// those raised the ceiling instead of topping up, so a healing Opponent could
					// outgrow anything you were able to do to it.
					var health = Math.Min(opponent.Health - amount, opponent.MaxHealth);
					state = state.UpdateObject(id, opponent with { Health = health });
					events = events.Add(
						new OpponentDamagedEvent { Amount = amount, HealthRemaining = health }
					);
					break;
				}

				case KinCard card when card.GetComponent<UnitComponent>() is { } unit:
				{
					state = state.UpdateObject(
						id,
						card.WithComponentReplaced(unit with { Damage = unit.Damage + amount })
					);
					break;
				}

				case KinPlayer player:
				{
					var life = player.Life - amount;
					state = state.UpdateObject(id, player with { Life = life });
					events = events.Add(
						new PlayerDamagedEvent { Amount = amount, LifeRemaining = life }
					);
					break;
				}
			}
		}

		// **A kill mid-turn ends the battle HERE, not at end of turn.** Direct-damage cards made
		// that difference visible: lethal from a card left the fight running until End Turn was
		// pressed on a dead Opponent. See `SettleBattleEnd`, which is idempotent, so this costs
		// nothing on the overwhelming majority of damage packets that kill nobody.
		ImmutableList<GameEvent> ending;
		(state, ending) = state.SettleBattleEnd();

		return new ActionResult(state).WithEvents(events.AddRange(ending));
	}
}
