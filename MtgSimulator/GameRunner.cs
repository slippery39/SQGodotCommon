using System.Collections.Immutable;
using System.Diagnostics;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Runs a single game to completion using the provided AI strategies for both players.
/// Returns a GameResult and the final GameState when the game ends.
///
/// AI strategies are injected — swap RandomAiStrategy for DepthLimitedAiStrategy
/// or any other IAiStrategy implementation without changing this class.
///
/// Limits:
///   - Max time:      5 000 ms wall-clock (flags as TimeLimitReached)
///   - Max turns:     100 turns           (flags as TurnLimitReached)
///   - Action warning: 50 actions in a single turn (logged, game continues)
///   - Action limit:  100 actions in a single turn (flags as ActionLimitReached)
/// </summary>
public class GameRunner
{
	private const long MaxGameTimeMs = 5_000;
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

	// Packages all mutable state for a single game run, passed through helpers to avoid
	// long parameter lists and repeated ref parameters.
	private sealed class RunContext(GameState initialState)
	{
		public GameState State = initialState;
		public int TotalActions;
		public bool HadActionWarning;
		public readonly List<string> DrawnCards1 = [];
		public readonly List<string> DrawnCards2 = [];
		public readonly List<string> PlayedCards1 = [];
		public readonly List<string> PlayedCards2 = [];
		public readonly List<GameEvent> AllEvents = [];
		public readonly Stopwatch Timer = Stopwatch.StartNew();
	}

	public (GameResult Result, GameState FinalState) Run(
		GameState initialState,
		MtgGameIds ids,
		IReadOnlyDictionary<int, string> cardNames
	)
	{
		var ctx = new RunContext(initialState);
		SeedInitialHand(initialState, ids.Player1HandId, cardNames, ctx.DrawnCards1);
		SeedInitialHand(initialState, ids.Player2HandId, cardNames, ctx.DrawnCards2);

		try
		{
			while (true)
			{
				var game = ctx.State.GetGame(ids.GameId);

				if (game.TurnNumber > MaxTurns)
					return Terminate(ctx, ids, -1, GameEndReason.TurnLimitReached, game.TurnNumber);

				var strategy =
					game.ActivePlayerId == ids.Player1Id ? _player1Strategy : _player2Strategy;

				var result = RunTurn(ctx, ids, game, strategy, cardNames);
				if (result.HasValue)
					return result.Value;
			}
		}
		catch (Exception ex)
		{
			var turnNumber = ctx.State.TryGetGame()?.TurnNumber ?? 0;
			return Terminate(ctx, ids, -1, GameEndReason.UnhandledException, turnNumber, ex);
		}
	}

	// Returns null when the turn ended normally (outer loop should continue).
	// Returns the completed game result when the game ends mid-turn.
	private static (GameResult, GameState)? RunTurn(
		RunContext ctx,
		MtgGameIds ids,
		MtgGame game,
		IAiStrategy strategy,
		IReadOnlyDictionary<int, string> cardNames
	)
	{
		var actionsThisTurn = 0;

		while (true)
		{
			if (ctx.Timer.ElapsedMilliseconds > MaxGameTimeMs)
				return Terminate(ctx, ids, -1, GameEndReason.TimeLimitReached, game.TurnNumber);

			if (ctx.State.IsWaitingForChoice)
			{
				ProcessChoice(ctx, ids, game.ActivePlayerId, strategy, cardNames);
				continue;
			}

			var gameOver = CheckGameOver(ctx, ids, game.TurnNumber);
			if (gameOver.HasValue)
				return gameOver;

			if (actionsThisTurn >= ActionLimitThreshold)
				return Terminate(ctx, ids, -1, GameEndReason.ActionLimitReached, game.TurnNumber);

			if (actionsThisTurn >= ActionWarningThreshold)
				ctx.HadActionWarning = true;

			var legalActions = MtgActionGenerator.GetLegalActions(
				ctx.State,
				ids,
				game.ActivePlayerId
			);

			if (legalActions.Count == 0)
			{
				EndTurn(ctx, ids, cardNames);
				ctx.TotalActions++;
				return null;
			}

			var chosen = strategy.SelectAction(ctx.State, ids, game.ActivePlayerId);
			var (newState, events) = ExecuteAction(ctx.State, chosen);
			ctx.State = newState;
			TrackDrawnCards(events, ids, cardNames, ctx.DrawnCards1, ctx.DrawnCards2);
			TrackPlayedCards(events, ids, cardNames, ctx.PlayedCards1, ctx.PlayedCards2);
			ctx.AllEvents.AddRange(events);
			ctx.TotalActions++;
			actionsThisTurn++;
		}
	}

	private static (GameResult, GameState)? CheckGameOver(
		RunContext ctx,
		MtgGameIds ids,
		int turnNumber
	)
	{
		var overEvent = ctx.AllEvents.OfType<GameOverEvent>().LastOrDefault();
		if (overEvent == null)
			return null;

		var endReason = ctx
			.AllEvents.OfType<PlayerLostEvent>()
			.Any(e => e.Reason.Contains("library"))
			? GameEndReason.LibraryEmpty
			: GameEndReason.Damage;

		return Terminate(ctx, ids, overEvent.WinnerPlayerId, endReason, turnNumber);
	}

	private static void ProcessChoice(
		RunContext ctx,
		MtgGameIds ids,
		int activePlayerId,
		IAiStrategy strategy,
		IReadOnlyDictionary<int, string> cardNames
	)
	{
		var choice = ctx.State.GetPendingChoice()!;
		var selectedIds = strategy.ResolveChoice(ctx.State, choice, activePlayerId);
		var (resolvedState, choiceEvents) = ctx.State.ResolveChoice(selectedIds);
		ctx.State = resolvedState;
		TrackDrawnCards(choiceEvents, ids, cardNames, ctx.DrawnCards1, ctx.DrawnCards2);
		TrackPlayedCards(choiceEvents, ids, cardNames, ctx.PlayedCards1, ctx.PlayedCards2);
		ctx.AllEvents.AddRange(choiceEvents);
	}

	private static void EndTurn(
		RunContext ctx,
		MtgGameIds ids,
		IReadOnlyDictionary<int, string> cardNames
	)
	{
		var action = new EndTurnAction
		{
			GameId = ids.GameId,
			Player1Id = ids.Player1Id,
			Player2Id = ids.Player2Id,
		};
		var (newState, _) = ctx.State.TryAddAction(action);
		var (endState, endEvents) = newState.ProcessAllActions();
		ctx.State = endState;
		TrackDrawnCards(endEvents, ids, cardNames, ctx.DrawnCards1, ctx.DrawnCards2);
		TrackPlayedCards(endEvents, ids, cardNames, ctx.PlayedCards1, ctx.PlayedCards2);
		ctx.AllEvents.AddRange(endEvents);
	}

	private static (GameResult, GameState) Terminate(
		RunContext ctx,
		MtgGameIds ids,
		int winnerId,
		GameEndReason endReason,
		int turnCount,
		Exception? exception = null
	)
	{
		ctx.Timer.Stop();
		return (
			new GameResult
			{
				WinnerPlayerId = winnerId,
				Player1Id = ids.Player1Id,
				Player2Id = ids.Player2Id,
				EndReason = endReason,
				TurnCount = turnCount,
				TotalActions = ctx.TotalActions,
				HadActionWarning = ctx.HadActionWarning,
				GameDurationMs = ctx.Timer.ElapsedMilliseconds,
				Player1DrawnCards = ctx.DrawnCards1,
				Player2DrawnCards = ctx.DrawnCards2,
				Player1PlayedCards = ctx.PlayedCards1,
				Player2PlayedCards = ctx.PlayedCards2,
				AllEvents = ctx.AllEvents,
				ExceptionMessage = exception?.Message,
				ExceptionStackTrace = exception?.StackTrace,
			},
			ctx.State
		);
	}

	private static void SeedInitialHand(
		GameState state,
		int handId,
		IReadOnlyDictionary<int, string> cardNames,
		List<string> target
	)
	{
		foreach (var cardId in state.GetChildrenIds(handId))
			if (cardNames.TryGetValue(cardId, out var name))
				target.Add(name);
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

	private static void TrackPlayedCards(
		IEnumerable<GameEvent> events,
		MtgGameIds ids,
		IReadOnlyDictionary<int, string> cardNames,
		List<string> playedCards1,
		List<string> playedCards2
	)
	{
		foreach (var e in events)
		{
			int cardId,
				playerId;
			if (e is SpellCastEvent sc)
			{
				cardId = sc.CardId;
				playerId = sc.CastingPlayerId;
			}
			else if (e is CreaturePlayedEvent cp)
			{
				cardId = cp.CardId;
				playerId = cp.PlayerId;
			}
			else
				continue;

			if (!cardNames.TryGetValue(cardId, out var name))
				continue;

			if (playerId == ids.Player1Id)
				playedCards1.Add(name);
			else
				playedCards2.Add(name);
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
}
