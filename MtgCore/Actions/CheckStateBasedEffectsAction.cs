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
/// GameId, Player1BattlefieldId and Player2BattlefieldId are stored directly to avoid
/// scanning the entire IdToGameObjectMap on every execution.
///
/// Order of operations:
///   1. Process static ability zone changes (ETB/LTB) via StaticAbilityEngine
///   2. Evaluate PendingGameEvents against all TriggeredAbilityComponents
///      on both battlefields and spawn ResolveEffectAction for each match
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
		foreach (var e in pendingEvents.OfType<CreatureEnteredBattlefieldEvent>())
			state = StaticAbilityEngine.ProcessPermanentEntered(state, e.CardId, GameId);

		foreach (var e in pendingEvents.OfType<PermanentLeftBattlefieldEvent>())
			state = StaticAbilityEngine.ProcessPermanentLeft(state, e.CardId, GameId);

		return state;
	}

	private GameState EvaluateTriggeredAbilities(
		GameState state,
		ImmutableList<GameEvent> pendingEvents
	)
	{
		if (pendingEvents.IsEmpty)
			return state;

		var battlefieldCards = state
			.GetCardsInZone(Player1BattlefieldId)
			.Concat(state.GetCardsInZone(Player2BattlefieldId));

		foreach (var card in battlefieldCards)
			state = EvaluateCardTriggers(state, card, pendingEvents);

		return state;
	}

	private static GameState EvaluateCardTriggers(
		GameState state,
		Card card,
		ImmutableList<GameEvent> pendingEvents
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
			foreach (
				var _ in pendingEvents.Where(e =>
					ability.Condition.IsSatisfiedBy(e, triggerContext)
				)
			)
			{
				state = state.SpawnAction(
					new ResolveEffectAction
					{
						Effects = [ability.Effect],
						CastingPlayerId = card.ControllerId,
						SourceCardId = card.Id,
					}
				);
			}
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
