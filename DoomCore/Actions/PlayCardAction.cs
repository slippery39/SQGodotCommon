using ImmutableGameObjects;

namespace DoomCore;

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

		var card = (DoomCard)gameState.GetObject(CardId);
		if (gameState.GetPlayer().Energy < card.Cost)
			return ValidationResult.Invalid($"Not enough energy for {card.Name}");

		// A card that is neither a body nor an effect is inert: it would cost energy, leave your
		// hand and change nothing. Refused here rather than allowed, because an inert card throws no
		// error and looks exactly like one that worked — this codebase has lost four bugs that way.
		if (!card.HasComponent<UnitComponent>() && card.Effects.IsEmpty)
			return ValidationResult.Invalid($"{card.Name} does nothing");

		if (card.HasComponent<UnitComponent>())
		{
			if (Lane < 0 || Lane >= DoomBattle.LaneCount)
				return ValidationResult.Invalid(
					$"Lane must be 0-{DoomBattle.LaneCount - 1}, got {Lane}"
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
		var card = (DoomCard)gameState.GetObject(CardId);
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
		// turn with End Turn as the only legal move — see DoomJam.md "Combat v3".
		//
		// **Replaced is not dead**, exactly as withdrawn is not: no `OnDeath` fires and nothing is
		// recorded as having died. You did not lose it in the lane, you took it off the board.
		// The cost needs no penalty bolted on — you spent the energy and threw away a body that
		// was absorbing damage, and that is already sharp enough to make overwriting a decision.
		// Nothing kills a unit during YOUR turn today — damage lands in `EndTurnAction` and the dead
		// are cleared there — so a replaced unit is always a live one. If something ever does kill
		// mid-turn, this would discard a corpse without firing its death, which is the silent kind
		// of loss. Guard it then, at the point that creates the corpse.
		if (isUnit && state.UnitInLane(Lane) is { } held && held.Id != CardId)
			state = state.MoveObject(held.Id, state.ZoneId(ZoneType.Discard));

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

		// Recorded at the moment of commitment, not read off the Field at the end — a unit that was
		// summoned and then died still counts as committed. Flood pays on commitment.
		if (isUnit)
		{
			var battle = state.GetBattle();
			state = state.UpdateObject(
				battle.Id,
				battle with
				{
					SummonedRunCardIds = battle.SummonedRunCardIds.Add(card.RunCardId),
				}
			);
		}

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
		if (!card.Effects.IsEmpty)
			state = state.SpawnAction(
				new ResolveEffectsAction
				{
					SourceId = CardId,
					Trigger = EffectTrigger.OnPlay,
					Effects = card.Effects,
				}
			);

		return new ActionResult(state).WithEvent(
			new CardPlayedEvent
			{
				CardId = CardId,
				CardName = card.Name,
				EnergySpent = card.Cost,
			}
		);
	}
}
