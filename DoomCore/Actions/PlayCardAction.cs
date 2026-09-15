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

			// One unit per lane. Silently stacking would make a lane's matchup unreadable, and
			// silently replacing would throw away a unit the player had already paid for.
			if (gameState.UnitInLane(Lane) is { } held)
				return ValidationResult.Invalid($"Lane {Lane} is already held by {held.Name}");
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
		var destination = isUnit ? ZoneType.Field : ZoneType.Discard;

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
