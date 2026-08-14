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
		state = EvaluateTriggeredAbilities(state, pendingEvents);
		state = state with { PendingGameEvents = ImmutableList<GameEvent>.Empty };

		var (finalState, events) = CheckLossConditions(state, player1, player2);
		return new ActionResult(finalState) { Events = events };
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

		state = EvaluateEmblemTriggers(state, Player1Id, pendingEvents);
		state = EvaluateEmblemTriggers(state, Player2Id, pendingEvents);

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
					}
				);
		}

		return state;
	}

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

		foreach (var ability in card.GetComponents<TriggeredAbilityComponent>())
		{
			if (ability.ActiveInZone != activeZone)
				continue;

			foreach (var e in pendingEvents)
			{
				if (ability.Condition.IsSatisfiedBy(e, triggerContext))
					state = state.SpawnAction(
						new ResolveEffectAction
						{
							Effects = ability.Effects,
							CastingPlayerId = card.ControllerId,
							SourceCardId = card.Id,
						}
					);
			}
		}

		return state;
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

		var p1ShouldLose = !player1.HasLost && player1.Life <= 0;
		var p2ShouldLose = !player2.HasLost && player2.Life <= 0;

		if (p1ShouldLose)
		{
			player1 = player1 with { HasLost = true };
			state = state.UpdateObject(Player1Id, player1);
			events = events.Add(
				new PlayerLostEvent { PlayerId = Player1Id, Reason = "life total reached zero" }
			);
		}

		if (p2ShouldLose)
		{
			player2 = player2 with { HasLost = true };
			state = state.UpdateObject(Player2Id, player2);
			events = events.Add(
				new PlayerLostEvent { PlayerId = Player2Id, Reason = "life total reached zero" }
			);
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
}
