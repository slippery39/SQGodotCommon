using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// Changes a unit's Power and Toughness. The second thing the game could not say.
///
/// **Triggered and one-shot, never continuous, and that is a deliberate ceiling.** A continuous
/// layer — "your Scavengers get +2/+0 while this stands" — is MtgCore's expensive machinery
/// (replacement effects, layers, re-derivation on every read) and this game does not need it. A
/// buff here happens once, at the moment it fires, and the new numbers ARE the unit's numbers.
/// Everything the design wants is sayable that way: enter-the-lane buffs, adjacency, a companion
/// that grows on what it survived.
///
/// **Negative values are legal and are how a debuff is written**, the same way healing is a negative
/// `DealDamageAction`. Toughness is floored at 1: a unit taken to 0 toughness would be dead the
/// instant it was touched, and "this kills it" is `DealDamageAction`'s job, not a stat change's.
///
/// Raising Toughness does NOT heal — <see cref="UnitComponent.Damage"/> is untouched, so a unit on
/// 6 damage of 6 toughness that gains +0/+4 is alive again with 4 left. That is intentional and it
/// is the only way a buff can save something mid-battle.
/// </summary>
public record BuffAction : EffectAction
{
	public int Power { get; init; }
	public int Toughness { get; init; }

	/// <summary>Thorns GRANTED, added to what the unit already has. See <see cref="UnitComponent.Thorns"/>.</summary>
	public int Thorns { get; init; }

	/// <summary>
	/// EXTRA strikes, added to the unit's count — so 1 turns a single-striker into a double. A
	/// delta rather than a set, for the same reason Power is: two Whetstones on one body stack.
	/// </summary>
	public int Strikes { get; init; }

	/// <summary>
	/// **Guard GRANTED this turn** — see <see cref="UnitComponent.Guard"/>. Block, in Slay the Spire's
	/// terms: it stacks on top of the companion's refreshed Guard and is gone next turn.
	/// </summary>
	public int Guard { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		var power = Scaled(state, Power);
		var toughness = Scaled(state, Toughness);
		var thorns = Scaled(state, Thorns);

		// A buff of nothing is not an error — "+2/+0 for each unit that died last turn" on a turn
		// nothing died is a real and common case, and it should be quiet rather than noisy.
		var guard = Scaled(state, Guard);

		if (power == 0 && toughness == 0 && thorns == 0 && Strikes == 0 && guard == 0)
			return new ActionResult(state);

		foreach (var id in TargetIds)
		{
			if (!state.HasObject(id))
				continue;

			if (state.GetObject(id) is not KinCard card)
				continue;

			if (card.GetComponent<UnitComponent>() is not { } unit)
				continue;

			var buffed = unit with
			{
				Power = Math.Max(0, unit.Power + power),
				Toughness = Math.Max(1, unit.Toughness + toughness),
				Thorns = Math.Max(0, unit.Thorns + thorns),
				// Floored at 1: a unit that strikes zero times has had its power deleted by a rule
				// nobody can see on the card.
				Strikes = Math.Max(1, unit.Strikes + Strikes),
				// **Toughness on the companion IS its Guard**, so "+0/+4" from a Warden beside it has
				// to help THIS turn too, not only after the next refresh.
				Guard = Math.Max(
					0,
					unit.Guard + guard + (card.HasComponent<CompanionComponent>() ? toughness : 0)
				),
			};

			state = state.UpdateObject(id, card.WithComponentReplaced(buffed));
			events = events.Add(
				new UnitBuffedEvent
				{
					CardId = id,
					CardName = card.Name,
					Power = buffed.Power - unit.Power,
					Toughness = buffed.Toughness - unit.Toughness,
				}
			);
		}

		return new ActionResult(state).WithEvents(events);
	}
}
