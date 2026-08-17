using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Fires counterspell traps from the non-active player's hand. See CounterTrapComponent for the
/// design and why counterspells work this way.
///
/// Called from all three cast actions after the card reaches the stack and mana is paid, but
/// BEFORE the resolve action is spawned — a countered spell was still cast, so SpellCastEvent and
/// the storm counter must already have happened.
/// </summary>
public static class CounterTrapEngine
{
	/// <summary>
	/// Result of a counter attempt. Countered means the caller must NOT spawn its resolve action —
	/// the card has already been moved to wherever it ended up.
	/// </summary>
	public readonly record struct CounterResult(GameState State, bool Countered);

	public static CounterResult TryCounterCast(GameState state, int castCardId, int castingPlayerId)
	{
		if (state.GetObject(castCardId) is not Card cast)
			return new CounterResult(state, false);

		var p1Id = state.GetWellKnownId(MtgObjectKeys.Player1);
		var p2Id = state.GetWellKnownId(MtgObjectKeys.Player2);
		var trapperId = castingPlayerId == p1Id ? p2Id : p1Id;

		if (state.GetObject(trapperId) is not MtgPlayer trapper)
			return new CounterResult(state, false);

		var handId = state.GetPlayerZoneId(trapperId, ZoneType.Hand);
		if (handId == 0)
			return new CounterResult(state, false);

		// Zone order is insertion order, which is draw order — this is the documented
		// "first card in your hand wins" tiebreak when two traps could both fire.
		Card? trapCard = null;
		CounterTrapComponent? trap = null;

		foreach (var candidate in state.GetCardsInZone(handId))
		{
			var component = candidate.GetComponent<CounterTrapComponent>();
			if (component == null)
				continue;
			if (!component.Matches(cast))
				continue;
			// The mana you left up is the cost. No mana, no trap.
			if (candidate.ManaCost > trapper.CurrentMana)
				continue;

			trapCard = candidate;
			trap = component;
			break;
		}

		if (trapCard == null || trap == null)
			return new CounterResult(state, false);

		// Pay for the trap first, so TaxAllRemaining measures what is genuinely left over.
		var remaining = trapper.CurrentMana - trapCard.ManaCost;
		state = state.UpdateObject(trapperId, trapper with { CurrentMana = remaining });

		var tax = trap.TaxAllRemaining ? remaining : trap.ManaTax;

		// The trap is spent either way — a real counterspell whose tax is paid still resolves and
		// still goes to the graveyard.
		var trapGraveyardId = state.GetPlayerZoneId(trapCard.OwnerId, ZoneType.Graveyard);
		state = state.MoveCardTracked(trapCard.Id, trapGraveyardId);

		if (tax > 0 && state.GetObject(castingPlayerId) is MtgPlayer caster)
		{
			if (caster.CurrentMana >= tax)
			{
				// Caster auto-pays. They get no choice either, which keeps this symmetric.
				state = state.UpdateObject(
					castingPlayerId,
					caster with
					{
						CurrentMana = caster.CurrentMana - tax,
					}
				);
				return new CounterResult(state, false);
			}
		}

		state = MoveCounteredCard(state, cast, trap);

		if (trap.DrawOnCounter > 0)
			state = state.SpawnAction(
				new DrawCardsAction
				{
					Amount = trap.DrawOnCounter,
					TargetIds = ImmutableList.Create(trapperId),
				}
			);

		var counteredEvent = new SpellCounteredEvent
		{
			CardId = cast.Id,
			TrapCardId = trapCard.Id,
			CastingPlayerId = castingPlayerId,
		};
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(counteredEvent) };

		return new CounterResult(state, true);
	}

	private static GameState MoveCounteredCard(
		GameState state,
		Card cast,
		CounterTrapComponent trap
	)
	{
		if (trap.ReturnToHandInstead)
			return state.MoveCardTracked(
				cast.Id,
				state.GetPlayerZoneId(cast.OwnerId, ZoneType.Hand)
			);

		var zone = trap.ExileInstead ? ZoneType.Exile : ZoneType.Graveyard;
		return state.MoveCardTracked(cast.Id, state.GetPlayerZoneId(cast.OwnerId, zone));
	}
}
