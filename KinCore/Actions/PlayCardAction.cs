using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// Plays a card from hand. Units go to the Field; anything else resolves and goes to Discard.
///
/// Energy is the whole economy — no mana, no lands, no colours. A card that burns the countdown to
/// be played early will pay that here too, once such cards exist.
/// </summary>
public record PlayCardAction : GameAction
{
	public int CardId { get; init; }

	/// <summary>
	/// Which lane a unit is played into, 0-4. Ignored by non-units, which have no position.
	///
	/// This is the only decision playing a card carries. There is no targeting anywhere in the
	/// game — pick the lane and combat resolves itself.
	/// </summary>
	public int Lane { get; init; }

	public override ValidationResult ValidateAdd(GameState gameState)
	{
		if (!gameState.HasObject(CardId))
			return ValidationResult.Invalid($"No card {CardId}");

		if (gameState.GetParent(CardId) != gameState.ZoneId(ZoneType.Hand))
			return ValidationResult.Invalid("Card is not in hand");

		var card = (KinCard)gameState.GetObject(CardId);
		if (gameState.GetPlayer().Energy < card.Cost)
			return ValidationResult.Invalid($"Not enough energy for {card.Name}");

		// A card that is neither a body nor an effect is inert: it would cost energy, leave your
		// hand and change nothing. Refused here rather than allowed, because an inert card throws no
		// error and looks exactly like one that worked — this codebase has lost four bugs that way.
		if (!card.HasComponent<UnitComponent>() && card.Effects.IsEmpty)
			return ValidationResult.Invalid($"{card.Name} does nothing");

		if (card.HasComponent<UnitComponent>())
		{
			if (Lane < 0 || Lane >= KinBattle.LaneCount)
				return ValidationResult.Invalid(
					$"Lane must be 0-{KinBattle.LaneCount - 1}, got {Lane}"
				);

			// **A held lane is NOT a refusal — see Execute.** One unit per lane still holds; what
			// changed is that playing into an occupied lane replaces the occupant rather than
			// being rejected. This used to read "silently replacing would throw away a unit the
			// player had already paid for", and it does — that IS the cost, and it is exact.
			//
			// The ONE exception is the companion, and it is not a balance decision: the companion
			// is not a card and has nowhere to be discarded TO. Putting it in Discard would make it
			// drawable, and it is meant to be the one thing that cannot be taken from you —
			// `SweepFieldAction` already spares it for exactly this reason. It holds its lane and
			// the other four are always open, so this refusal can never produce a dead turn.
			// **Phase 7 replaces this**: the companion becomes something you place each turn, and
			// then the question is where you put IT rather than whether you may build over it.
			if (gameState.UnitInLane(Lane)?.HasComponent<CompanionComponent>() == true)
				return ValidationResult.Invalid($"Lane {Lane} is held by your companion");
		}

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState gameState)
	{
		var card = (KinCard)gameState.GetObject(CardId);
		var player = gameState.GetPlayer();

		var state = gameState.UpdateObject(
			player.Id,
			player with
			{
				Energy = player.Energy - card.Cost,
			}
		);

		// A unit enters the Field ready — there is no summoning sickness. With only 2-5 turns in a
		// battle, a turn of nothing would make half the units unplayable.
		var isUnit = card.HasComponent<UnitComponent>();

		// **Exhaust sends it out of the battle instead of to Discard**, so it is not reshuffled
		// when Draw runs dry. Units are exempt: a unit already leaves the board by withdrawing, and
		// exhausting one would be a second, differently-named removal for the same act.
		var destination =
			isUnit ? ZoneType.Field
			: card.Exhausts ? ZoneType.Exhausted
			: ZoneType.Discard;

		// **ANY LANE IS ALWAYS PLAYABLE, AND WHATEVER WAS THERE IS DISCARDED. No refund.**
		//
		// This is a law rather than a convenience, and it is what stops `Persistent` bringing the
		// stall back in miniature later: persistence must mean "it stays if you leave it", never
		// "you may not use this lane". A board you cannot play into is how five drawn units end a
		// turn with End Turn as the only legal move — see KinJam.md "Combat v3".
		//
		// **Replaced is not dead**, exactly as withdrawn is not: no `OnDeath` fires and nothing is
		// recorded as having died. You did not lose it in the lane, you took it off the board.
		// The cost needs no penalty bolted on — you spent the energy and threw away a body that
		// was absorbing damage, and that is already sharp enough to make overwriting a decision.
		// **Things DO kill a unit during your own turn now** — `DestroyAction` (sacrifice) and the
		// Devour branch below. Both clear the dead inline at the point that creates the corpse, so
		// the assumption this comment used to state — that a replaced unit is always a live one —
		// still holds here. Anything else that kills mid-turn must do the same.
		var deathEvents = ImmutableList<GameEvent>.Empty;

		if (isUnit && state.UnitInLane(Lane) is { } held && held.Id != CardId)
		{
			// **Devour: the unit it replaces DIES instead of leaving.** The only difference between
			// the two branches is whether a death happened — and that difference is the whole
			// keyword, because it is what feeds Ash, Zombie and every `Loss` read in the pool.
			//
			// **The lane is the sacrifice choice**, which is why this needs no targeting of any
			// kind: you pick what to eat by picking where to stand.
			//
			// Marked dead and cleared through `ClearTheDead` — the one account of dying, the same
			// one `WithdrawUnitsAction` and `DestroyAction` use. It runs INLINE so no corpse is
			// left in the lane the incoming unit is about to take.
			if (card.Devours)
			{
				var eaten = held.Unit();
				state = state.UpdateObject(
					held.Id,
					held.WithComponentReplaced(eaten with { Damage = eaten.Toughness })
				);
				(state, deathEvents) = EndTurnAction.ClearTheDead(state, deathEvents);
			}
			else
			{
				state = state.MoveObject(held.Id, state.ZoneId(ZoneType.Discard));
			}
		}

		// Damage is cleared HERE, at the one point every board unit enters through, rather than at
		// each exit from the Field. A card that died earlier this battle went to Discard still
		// carrying the damage that killed it; recycled into hand and replayed it would arrive with
		// IsDead already true, so `UnitInLane` would skip it and it would sit on the Field
		// invisible and inert — hand and energy spent for nothing. **What returns from Discard is
		// the card, not the body that was standing in the lane.**
		if (isUnit)
			state = state.UpdateObject(
				CardId,
				card.WithComponentReplaced(card.Unit() with { Lane = Lane, Damage = 0 })
			);

		state = state.MoveObject(CardId, state.ZoneId(destination));

		// Counted for every card, unit or rite — the volume axis reads "cards played", not "bodies
		// placed". Incremented BEFORE effects spawn, so a card that scales on it counts itself.
		var played = state.GetBattle();
		state = state.UpdateObject(
			played.Id,
			played with
			{
				CardsPlayedThisTurn = played.CardsPlayedThisTurn + 1,
			}
		);

		// Effects spawn AFTER the card has moved, so a unit's own OnPlay effect can already see it
		// standing in its lane — "deal 1 to the enemy opposite" needs the lane to be occupied.
		// **The lane goes with them, and that is what makes a rite a targeted card.** A unit knows
		// its own lane from its component; a rite has no body, so the lane it was dropped on is
		// carried here instead. No targeting UI, no prompt — the drop IS the choice, which is the
		// only kind of decision this game makes.
		if (!card.Effects.IsEmpty)
			state = state.SpawnAction(
				new ResolveEffectsAction
				{
					SourceId = CardId,
					Trigger = EffectTrigger.OnPlay,
					Effects = card.Effects,
					PlayedLane = Lane,
				}
			);

		return new ActionResult(state)
			.WithEvents(deathEvents)
			.WithEvent(
				new CardPlayedEvent
				{
					CardId = CardId,
					CardName = card.Name,
					EnergySpent = card.Cost,
				}
			);
	}
}
