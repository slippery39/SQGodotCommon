using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Checks state-based loss conditions, updates applied static ability components,
/// and evaluates pending triggered abilities.
///
/// Registered as GameState.PostActionProcessor at game setup. Runs automatically
/// after every standalone action and every completed pipeline — never between
/// individual pipeline steps.
///
/// While GameState.SuppressPostProcessor is true this action never runs.
///
/// GameId, Player1BattlefieldId, Player2BattlefieldId, Player1GraveyardId, and
/// Player2GraveyardId are stored directly to avoid scanning the entire
/// IdToGameObjectMap on every execution.
///
/// Order of operations:
///   1. Process static ability zone changes (ETB/LTB) via StaticAbilityEngine
///   2. Evaluate PendingGameEvents against all TriggeredAbilityComponents
///      on both battlefields and both graveyards; spawn ResolveEffectAction for each match.
///      Each ability's ActiveInZone determines which zone pass evaluates it.
///   3. Clear PendingGameEvents
///   4. Check loss conditions (life <= 0 only)
///
/// IsPostProcessor = true prevents recursive post-processing cycles.
/// </summary>
public record CheckStateBasedEffectsAction : GameAction
{
	public int GameId { get; init; }
	public int Player1Id { get; init; }
	public int Player2Id { get; init; }
	public int Player1BattlefieldId { get; init; }
	public int Player2BattlefieldId { get; init; }
	public int Player1GraveyardId { get; init; }
	public int Player2GraveyardId { get; init; }

	public override bool IsPostProcessor => true;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		var player1 = state.GetPlayer(Player1Id);
		var player2 = state.GetPlayer(Player2Id);

		if (player1.HasLost && player2.HasLost)
			return new ActionResult(state with { PendingGameEvents = [] });

		var pendingEvents = state.PendingGameEvents;

		state = ProcessStaticAbilityUpdates(state, pendingEvents);

		// That pass MOVES CARDS, and a move stages events of its own — detaching an Aura sends it
		// to the graveyard, which stages the Aura's own PermanentLeftBattlefieldEvent. Those land
		// in state.PendingGameEvents after this local was captured, so without picking them up
		// here EvaluateTriggeredAbilities never sees them and the wipe below discards them: an
		// Aura's "when this leaves the battlefield" trigger could not fire when the creature it
		// enchanted died, which is the only way most Auras ever leave play. Rancor never returned
		// to hand.
		//
		// The zero-toughness and zero-loyalty sweeps below thread `ref pendingEvents` for exactly
		// this reason; the static pass was the one mover that did not. Re-reading is safe because
		// nothing clears the list mid-Execute — it can only have grown, and it keeps this local as
		// the prefix.
		if (state.PendingGameEvents.Count > pendingEvents.Count)
			pendingEvents = state.PendingGameEvents;

		// Runs after statics are applied, so a creature only dies once its effective toughness
		// is final — a lord leaving play and a -X/-X effect must be judged on the same pass.
		// Deaths are appended to the pending list so death triggers still see them.
		state = DestroyZeroToughnessCreatures(state, ref pendingEvents);
		state = DestroyZeroLoyaltyPlaneswalkers(state, ref pendingEvents);

		state = CountCreatureDeaths(state, pendingEvents);

		state = EvaluateTriggeredAbilities(state, pendingEvents);
		state = state with { PendingGameEvents = ImmutableList<GameEvent>.Empty };

		var (finalState, events) = CheckLossConditions(state, player1, player2);
		return new ActionResult(finalState) { Events = events };
	}

	/// <summary>
	/// Rolls this batch's creature deaths into MtgGame.CreaturesDiedThisTurn, for "if a creature
	/// died this turn" (Fungal Rebirth).
	///
	/// Counted HERE, from the pending events, rather than at the five actions that stage
	/// CreatureDestroyedEvent (DestroyCreatureAction, AttackAction, DealDamageAction,
	/// SacrificeAdditionalCost, DestroyZeroToughnessCreatures). Every death batch passes through
	/// this method and none can bypass it, whereas five call sites are five chances to forget one
	/// — which is precisely how a sacrificed creature came not to count as having died.
	///
	/// Runs before EvaluateTriggeredAbilities so a trigger resolving this pass already sees the
	/// death that fired it.
	/// </summary>
	private static GameState CountCreatureDeaths(
		GameState state,
		ImmutableList<GameEvent> pendingEvents
	)
	{
		var deaths = pendingEvents.Count(e => e is CreatureDestroyedEvent);
		if (deaths == 0)
			return state;

		var game = state.TryGetGame();
		if (game == null)
			return state;

		return state.UpdateObject(
			game.Id,
			game with
			{
				CreaturesDiedThisTurn = game.CreaturesDiedThisTurn + deaths,
			}
		);
	}

	/// <summary>
	/// Destroys any creature whose effective toughness has fallen to zero or below.
	///
	/// Damage-based death is decided at the damage site by IsLethalDamage, but a creature
	/// shrunk by a -X/-X effect takes no damage at all — without this check it would sit on
	/// the battlefield as a 2/0, so -X/-X could never function as removal.
	/// </summary>
	/// <summary>
	/// Moves any planeswalker at 0 loyalty to its owner's graveyard.
	///
	/// The planeswalker equivalent of the zero-toughness rule. It emits
	/// PermanentLeftBattlefieldEvent but NOT CreatureDestroyedEvent — a walker is not a creature,
	/// and firing the creature-death event would make every "whenever a creature dies" payoff
	/// trigger off a planeswalker dying.
	/// </summary>
	private GameState DestroyZeroLoyaltyPlaneswalkers(
		GameState state,
		ref ImmutableList<GameEvent> pendingEvents
	)
	{
		foreach (var battlefieldId in new[] { Player1BattlefieldId, Player2BattlefieldId })
		{
			// Materialised first: the loop moves cards out of the zone it is reading.
			foreach (var card in state.GetCardsInZone(battlefieldId).ToList())
			{
				var walker = card.GetComponent<PlaneswalkerComponent>();
				if (walker == null || walker.Loyalty > 0)
					continue;

				var leftEvent = new PermanentLeftBattlefieldEvent
				{
					CardId = card.Id,
					OwnerId = card.OwnerId,
				};

				var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
				state = state.MoveCardTracked(card.Id, graveyardId);

				pendingEvents = pendingEvents.Add(leftEvent);
			}
		}

		return state;
	}

	private GameState DestroyZeroToughnessCreatures(
		GameState state,
		ref ImmutableList<GameEvent> pendingEvents
	)
	{
		foreach (var battlefieldId in new[] { Player1BattlefieldId, Player2BattlefieldId })
		{
			// Materialised first: the loop moves cards out of the zone it is reading.
			foreach (var card in state.GetCardsInZone(battlefieldId).ToList())
			{
				if (!card.HasComponent<CreatureComponent>())
					continue;
				if (state.GetEffectiveStats(card.Id).Toughness > 0)
					continue;

				var leftEvent = new PermanentLeftBattlefieldEvent
				{
					CardId = card.Id,
					OwnerId = card.OwnerId,
				};
				var destroyedEvent = new CreatureDestroyedEvent { CreatureId = card.Id };

				var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
				state = state.MoveCardTracked(card.Id, graveyardId);

				pendingEvents = pendingEvents.Add(leftEvent).Add(destroyedEvent);
			}
		}

		return state;
	}

	private GameState ProcessStaticAbilityUpdates(
		GameState state,
		ImmutableList<GameEvent> pendingEvents
	)
	{
		foreach (var e in pendingEvents)
		{
			if (e is CreatureEnteredBattlefieldEvent entered)
				state = StaticAbilityEngine.ProcessPermanentEntered(state, entered.CardId, GameId);
			else if (e is PermanentEnteredBattlefieldEvent permanentEntered)
				state = StaticAbilityEngine.ProcessPermanentEntered(
					state,
					permanentEntered.CardId,
					GameId
				);
			else if (e is PermanentLeftBattlefieldEvent left)
			{
				state = StaticAbilityEngine.ProcessPermanentLeft(state, left.CardId, GameId);
				state = DetachEquipmentFromLeavingCard(state, left.CardId);
				state = RemoveBoostFromLeavingAttachment(state, left.CardId);
				state = ReleaseFreezeFromLeavingCard(state, left.CardId);
			}
		}

		// Graveyard-active statics are processed in a second pass so that a permanent which
		// dies has its battlefield statics stripped (first pass) before its graveyard statics
		// are stamped (second pass). Interleaving the two would let the strip undo the stamp.
		foreach (var e in pendingEvents)
		{
			if (e is CardEnteredGraveyardEvent enteredGraveyard)
				state = StaticAbilityEngine.ProcessZoneSourceEntered(
					state,
					enteredGraveyard.CardId,
					GameId
				);
			else if (e is CardLeftGraveyardEvent leftGraveyard)
				state = StaticAbilityEngine.ProcessZoneSourceLeft(
					state,
					leftGraveyard.CardId,
					GameId
				);
		}

		return state;
	}

	private GameState EvaluateTriggeredAbilities(
		GameState state,
		ImmutableList<GameEvent> pendingEvents
	)
	{
		if (pendingEvents.IsEmpty)
			return state;

		foreach (var card in state.GetCardsInZone(Player1BattlefieldId))
			state = EvaluateCardTriggers(state, card, pendingEvents, ZoneType.Battlefield);
		foreach (var card in state.GetCardsInZone(Player2BattlefieldId))
			state = EvaluateCardTriggers(state, card, pendingEvents, ZoneType.Battlefield);
		foreach (var card in state.GetCardsInZone(Player1GraveyardId))
			state = EvaluateCardTriggers(state, card, pendingEvents, ZoneType.Graveyard);
		foreach (var card in state.GetCardsInZone(Player2GraveyardId))
			state = EvaluateCardTriggers(state, card, pendingEvents, ZoneType.Graveyard);

		state = EvaluateDepartedCardTriggers(state, pendingEvents);

		state = EvaluateEmblemTriggers(state, Player1Id, pendingEvents);
		state = EvaluateEmblemTriggers(state, Player2Id, pendingEvents);

		return state;
	}

	/// <summary>
	/// Evaluates the triggers of a permanent that left the battlefield for hand, exile or library.
	///
	/// None of the zone passes above look there, so its own departure trigger could never fire —
	/// an Oblivion Ring bounced to hand kept the card it had exiled. A leave-the-battlefield
	/// ability is declared ActiveInZone = Graveyard because the graveyard is where a permanent
	/// usually goes; what it actually means is "after this left play", so that is the pass these
	/// cards are given.
	/// </summary>
	private GameState EvaluateDepartedCardTriggers(
		GameState state,
		ImmutableList<GameEvent> pendingEvents
	)
	{
		foreach (var e in pendingEvents.OfType<PermanentLeftBattlefieldEvent>())
		{
			if (!state.HasObject(e.CardId) || state.GetObject(e.CardId) is not Card card)
				continue;

			// Battlefield and graveyard are already covered, and evaluating twice would fire
			// every departure trigger twice.
			var zone = state.GetCardZone(e.CardId).ZoneType;
			if (zone is ZoneType.Battlefield or ZoneType.Graveyard)
				continue;

			state = EvaluateCardTriggers(state, card, pendingEvents, ZoneType.Graveyard);
		}

		return state;
	}

	private static GameState EvaluateEmblemTriggers(
		GameState state,
		int playerId,
		ImmutableList<GameEvent> pendingEvents
	)
	{
		var player = state.GetPlayer(playerId);
		if (player.Emblems.IsEmpty)
			return state;

		var context = new TriggerContext
		{
			GameState = state,
			SourceCardId = 0,
			ControllingPlayerId = playerId,
		};

		foreach (var emblem in player.Emblems)
		foreach (var e in pendingEvents)
		{
			if (emblem.Condition.IsSatisfiedBy(e, context))
				state = state.SpawnAction(
					new ResolveEffectAction
					{
						Effects = [emblem.Effect],
						CastingPlayerId = playerId,
						SourceCardId = 0,
						TriggerAmount = TriggerAmountOf(e),
						TriggerSubjectId = EventTriggerCondition.ExtractSubjectId(e),
					}
				);
		}

		return state;
	}

	/// <summary>
	/// The numeric payload of a triggering event, for "that many" clauses (Vilis: "whenever you
	/// lose life, draw that many cards"). 0 for events that carry no amount — an effect reading
	/// ContextKeys.TriggerAmount off one of those does nothing, which is the right failure.
	///
	/// Deliberately a closed list rather than reflection over an Amount property: an event that
	/// gains an unrelated numeric field later must not silently start feeding these effects.
	/// </summary>
	private static int TriggerAmountOf(GameEvent gameEvent) =>
		gameEvent switch
		{
			PlayerLostLifeEvent e => e.Amount,
			PlayerGainedLifeEvent e => e.Amount,
			PlayerDamagedEvent e => e.Amount,
			CreatureDamagedEvent e => e.Amount,
			CombatDamageDealtToPlayerEvent e => e.Amount,
			CardRevealedEvent e => e.ManaCost,
			CountersAddedEvent e => e.Amount,
			_ => 0,
		};

	private static GameState EvaluateCardTriggers(
		GameState state,
		Card card,
		ImmutableList<GameEvent> pendingEvents,
		ZoneType activeZone
	)
	{
		var triggerContext = new TriggerContext
		{
			GameState = state,
			SourceCardId = card.Id,
			ControllingPlayerId = card.ControllerId,
		};

		// Indexed rather than foreach over GetComponents, because a capped ability has to be
		// written back with an incremented count and the index is the only way to find it again.
		var components = card.Components;
		var changed = false;

		for (int i = 0; i < components.Length; i++)
		{
			if (components[i] is not TriggeredAbilityComponent ability)
				continue;

			if (ability.ActiveInZone != activeZone)
				continue;

			foreach (var e in pendingEvents)
			{
				// Re-checked inside the event loop: two matching events in one batch must not
				// both fire a once-per-turn ability.
				if (!ability.CanTrigger)
					break;

				if (!ability.Condition.IsSatisfiedBy(e, triggerContext))
					continue;

				state = state.SpawnAction(
					new ResolveEffectAction
					{
						Effects = ability.Effects,
						CastingPlayerId = card.ControllerId,
						SourceCardId = card.Id,
						TriggerAmount = TriggerAmountOf(e),
						TriggerSubjectId = EventTriggerCondition.ExtractSubjectId(e),
					}
				);

				if (ability.MaxTriggers > 0 || ability.MaxTriggersPerTurn > 0)
				{
					ability = ability with
					{
						TriggerCountTotal = ability.TriggerCountTotal + 1,
						TriggerCountThisTurn = ability.TriggerCountThisTurn + 1,
					};
					components = components.SetItem(i, ability);
					changed = true;
				}
			}
		}

		if (changed)
			state = state.UpdateObject(card.Id, card with { Components = components });

		return state;
	}

	/// <summary>
	/// Releases any creature held down by a permanent that just left the battlefield —
	/// Dungeon Geists' "doesn't untap for as long as you control this".
	///
	/// The creature stays exhausted until its own next untap step; only the indefinite lock is
	/// lifted. That matches the card: killing the Geists frees the creature, it does not
	/// immediately untap it.
	/// </summary>
	private GameState ReleaseFreezeFromLeavingCard(GameState state, int leavingCardId)
	{
		foreach (
			var card in state
				.GetCardsInZone(Player1BattlefieldId)
				.Concat(state.GetCardsInZone(Player2BattlefieldId))
				.ToList()
		)
		{
			var creature = card.GetComponent<CreatureComponent>();
			if (creature == null || creature.FrozenBySourceId != leavingCardId)
				continue;

			state = state.UpdateObject(
				card.Id,
				card.WithComponentReplaced(creature with { FrozenBySourceId = 0 })
			);
		}

		return state;
	}

	/// <summary>
	/// The mirror of DetachEquipmentFromLeavingCard: the ATTACHMENT left, not the thing it was
	/// attached to. Its boost is stamped on the creature as a component, so without this the
	/// creature keeps it forever — a bounced or destroyed Sensory Deprivation left its -3/-0
	/// behind, which is exactly as good as the Aura never leaving.
	/// </summary>
	private static GameState RemoveBoostFromLeavingAttachment(GameState state, int leavingCardId)
	{
		if (state.GetObject(leavingCardId) is not Card leaving)
			return state;

		var equip = leaving.GetComponent<EquipmentComponent>();
		if (equip == null || equip.EquippedToCardId == 0)
			return state;

		if (state.HasObject(equip.EquippedToCardId))
		{
			var host = (Card)state.GetObject(equip.EquippedToCardId);
			state = state.UpdateObject(
				equip.EquippedToCardId,
				host with
				{
					Components = host
						.Components.Where(c =>
							c is not PowerToughnessModifier m || m.SourceCardId != leavingCardId
						)
						.ToImmutableArray(),
				}
			);
		}

		// An equipment that comes back can be re-equipped, so it must not still claim a host.
		return state.UpdateObject(
			leavingCardId,
			leaving.WithComponentReplaced(equip with { EquippedToCardId = 0 })
		);
	}

	private GameState DetachEquipmentFromLeavingCard(GameState state, int leavingCardId)
	{
		//NOTE - If we can already have the permanent that left the battlefield from the event
		//then do we really need to loop through everything here? Maybe we can just look at the equipment that is attached to it and update those directly?
		foreach (
			var card in state
				.GetCardsInZone(Player1BattlefieldId)
				.Concat(state.GetCardsInZone(Player2BattlefieldId))
		)
		{
			var equip = card.GetComponent<EquipmentComponent>();
			if (equip == null || equip.EquippedToCardId != leavingCardId)
				continue;

			// An aura cannot exist without the permanent it enchants, so it dies with it.
			// Equipment merely detaches and stays on the battlefield.
			if (equip.IsAura)
			{
				var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
				state = state.MoveCardTracked(card.Id, graveyardId);
				continue;
			}

			var updatedComponents = card.Components;
			for (int i = 0; i < updatedComponents.Length; i++)
			{
				if (updatedComponents[i] is EquipmentComponent e)
				{
					updatedComponents = updatedComponents.SetItem(
						i,
						e with
						{
							EquippedToCardId = 0,
						}
					);
					break;
				}
			}
			state = state.UpdateObject(card.Id, card with { Components = updatedComponents });
		}
		return state;
	}

	private (GameState, ImmutableList<GameEvent>) CheckLossConditions(
		GameState state,
		MtgPlayer player1,
		MtgPlayer player2
	)
	{
		ImmutableList<GameEvent> events = [];

		// Two ways to lose, checked together so a simultaneous loss is still a draw. The reason
		// string is not decoration: GameRunner reads it for "library" to report
		// GameEndReason.LibraryEmpty, which was unreachable until decking could actually kill.
		var deckingKills = state.TryGetGame()?.DeckingLossEnabled ?? true;
		bool Decked(MtgPlayer p) => deckingKills && p.AttemptedDrawFromEmptyLibrary;

		var p1ShouldLose =
			!player1.HasLost
			&& (player1.Life <= 0 || Decked(player1))
			&& !CannotLose(state, Player1Id);
		var p2ShouldLose =
			!player2.HasLost
			&& (player2.Life <= 0 || Decked(player2))
			&& !CannotLose(state, Player2Id);

		static string LossReason(MtgPlayer p) =>
			p.Life <= 0 ? "life total reached zero" : "drew from an empty library";

		if (p1ShouldLose)
		{
			var reason = LossReason(player1);
			player1 = player1 with { HasLost = true };
			state = state.UpdateObject(Player1Id, player1);
			events = events.Add(new PlayerLostEvent { PlayerId = Player1Id, Reason = reason });
		}

		if (p2ShouldLose)
		{
			var reason = LossReason(player2);
			player2 = player2 with { HasLost = true };
			state = state.UpdateObject(Player2Id, player2);
			events = events.Add(new PlayerLostEvent { PlayerId = Player2Id, Reason = reason });
		}

		if (p1ShouldLose || p2ShouldLose)
		{
			var winnerId = (p1ShouldLose, p2ShouldLose) switch
			{
				(true, true) => -1,
				(true, false) => Player2Id,
				(false, true) => Player1Id,
				_ => -1,
			};
			events = events.Add(new GameOverEvent { WinnerPlayerId = winnerId });
		}

		return (state, events);
	}

	/// <summary>
	/// "You can't lose the game" — Platinum Angel. See CannotLoseComponent.
	///
	/// Suppresses the loss, not the cause: life still falls below zero and the library still
	/// empties, so the moment the permanent leaves the battlefield the very next state-based check
	/// declares the loss that was waiting. That is what makes killing the Angel a real answer
	/// rather than a way to stop further bleeding.
	/// </summary>
	private static bool CannotLose(GameState state, int playerId)
	{
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return false;

		foreach (var card in state.GetCardsInZone(battlefieldId))
			if (card.ControllerId == playerId && card.HasComponent<CannotLoseComponent>())
				return true;

		return false;
	}
}
