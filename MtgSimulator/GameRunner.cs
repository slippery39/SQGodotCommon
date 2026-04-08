using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Runs a single game to completion using the provided AI strategies for both players.
/// Returns a GameResult describing the outcome.
///
/// AI strategies are injected — swap RandomAiStrategy for DepthLimitedAiStrategy
/// or any other IAiStrategy implementation without changing this class.
///
/// Limits:
///   - Max turns: 100 (flags as TurnLimitReached)
///   - Action warning: 50 actions in a single turn (logged, game continues)
///   - Action limit: 100 actions in a single turn (flags as ActionLimitReached, game ends)
/// </summary>
public class GameRunner
{
	private const int MaxTurns = 100;
	private const int ActionWarningThreshold = 50;
	private const int ActionLimitThreshold = 100;

	private readonly IAiStrategy _player1Strategy;
	private readonly IAiStrategy _player2Strategy;

	public GameRunner(IAiStrategy player1Strategy, IAiStrategy player2Strategy)
	{
		_player1Strategy = player1Strategy;
		_player2Strategy = player2Strategy;
	}

	public GameResult Run(
		GameState initialState,
		MtgGameIds ids,
		IReadOnlyDictionary<int, string> cardNames
	)
	{
		var state = initialState;
		var totalActions = 0;
		var hadActionWarning = false;
		var drawnCards1 = new List<string>();
		var drawnCards2 = new List<string>();
		var allEvents = new List<GameEvent>();

		while (true)
		{
			var game = state.GetGame(ids.GameId);

			if (game.TurnNumber > MaxTurns)
			{
				return BuildResult(
					ids,
					-1,
					GameEndReason.TurnLimitReached,
					game.TurnNumber,
					totalActions,
					hadActionWarning,
					drawnCards1,
					drawnCards2
				);
			}

			var activePlayerId = game.ActivePlayerId;
			var activeStrategy =
				activePlayerId == ids.Player1Id ? _player1Strategy : _player2Strategy;
			var actionsThisTurn = 0;

			while (true)
			{
				if (state.IsWaitingForChoice)
				{
					var choice = state.GetPendingChoice()!;
					var selectedIds = activeStrategy.ResolveChoice(state, choice, activePlayerId);
					var (resolvedState, choiceEvents) = state.ResolveChoice(selectedIds);
					state = resolvedState;
					TrackDrawnCards(choiceEvents, ids, cardNames, drawnCards1, drawnCards2);
					allEvents.AddRange(choiceEvents);
					continue;
				}

				var overEvent = allEvents.OfType<GameOverEvent>().LastOrDefault();
				if (overEvent != null)
				{
					var endReason = allEvents
						.OfType<PlayerLostEvent>()
						.Any(e => e.Reason.Contains("library"))
						? GameEndReason.LibraryEmpty
						: GameEndReason.Damage;

					return BuildResult(
						ids,
						overEvent.WinnerPlayerId,
						endReason,
						game.TurnNumber,
						totalActions,
						hadActionWarning,
						drawnCards1,
						drawnCards2
					);
				}

				if (actionsThisTurn >= ActionLimitThreshold)
				{
					return BuildResult(
						ids,
						-1,
						GameEndReason.ActionLimitReached,
						game.TurnNumber,
						totalActions,
						hadActionWarning,
						drawnCards1,
						drawnCards2
					);
				}

				if (actionsThisTurn >= ActionWarningThreshold)
					hadActionWarning = true;

				var legalActions = MtgActionGenerator.GetLegalActions(state, ids, activePlayerId);

				if (!legalActions.Any())
				{
					var (endState, endEvents) = ExecuteEndTurn(state, ids);
					state = endState;
					TrackDrawnCards(endEvents, ids, cardNames, drawnCards1, drawnCards2);
					allEvents.AddRange(endEvents);
					totalActions++;
					break;
				}

				var chosen = activeStrategy.SelectAction(state, ids, activePlayerId);
				var (newState, actionEvents) = ExecuteAction(state, chosen);
				state = newState;
				TrackDrawnCards(actionEvents, ids, cardNames, drawnCards1, drawnCards2);
				allEvents.AddRange(actionEvents);
				totalActions++;
				actionsThisTurn++;
			}
		}
	}

	private static void TrackDrawnCards(
		IEnumerable<GameEvent> events,
		MtgGameIds ids,
		IReadOnlyDictionary<int, string> cardNames,
		List<string> drawnCards1,
		List<string> drawnCards2
	)
	{
		foreach (var e in events.OfType<CardDrawnEvent>())
		{
			if (!cardNames.TryGetValue(e.CardId, out var name))
				continue;

			if (e.PlayerId == ids.Player1Id)
				drawnCards1.Add(name);
			else
				drawnCards2.Add(name);
		}
	}

	private static (GameState, ImmutableList<GameEvent>) ExecuteAction(
		GameState state,
		GameAction action
	)
	{
		var (newState, success) = state.TryAddAction(action);
		if (!success)
			return (state, ImmutableList<GameEvent>.Empty);

		return newState.ProcessAllActions();
	}

	private static (GameState, ImmutableList<GameEvent>) ExecuteEndTurn(
		GameState state,
		MtgGameIds ids
	)
	{
		var action = new EndTurnAction
		{
			GameId = ids.GameId,
			Player1Id = ids.Player1Id,
			Player2Id = ids.Player2Id,
		};

		var (newState, _) = state.TryAddAction(action);
		return newState.ProcessAllActions();
	}

	private static GameResult BuildResult(
		MtgGameIds ids,
		int winnerId,
		GameEndReason endReason,
		int turnCount,
		int totalActions,
		bool hadActionWarning,
		List<string> drawnCards1,
		List<string> drawnCards2
	) =>
		new GameResult
		{
			WinnerPlayerId = winnerId,
			Player1Id = ids.Player1Id,
			Player2Id = ids.Player2Id,
			EndReason = endReason,
			TurnCount = turnCount,
			TotalActions = totalActions,
			HadActionWarning = hadActionWarning,
			Player1DrawnCards = drawnCards1,
			Player2DrawnCards = drawnCards2,
		};
}
