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
/// Limits — all deterministic except the last, which is a safety net that should never fire:
///   - Max turns:      100 turns                  (flags as TurnLimitReached)
///   - Action warning: 100 actions in a single turn (logged, game continues)
///   - Action limit:   200 actions in a single turn (flags as ActionLimitReached)
///   - Safety timeout: 300 000 ms wall-clock       (flags as TimeLimitReached)
///
/// **Wall-clock time must not decide a game.** It used to: a 20-second limit ended the game as
/// a draw, which made the result depend on how fast the machine happened to be running. That is
/// not a hypothetical — holding more finished games in memory (a change that cannot touch
/// gameplay) moved 2 578 outcomes in a 28 000-game training batch, because it slowed every game
/// down enough to push borderline ones over the line. Draw rate rose with batch size: 0.4% at
/// 1 120 games, 2.8% at 8 400, 15.2% at 28 000. Of 4 264 draws in that run, exactly one was a
/// real draw.
///
/// The turn and action limits above already bound a game deterministically (100 turns x 200
/// actions), so the clock was never load-bearing for termination — only for cost. Cost is now
/// bounded inside the AI instead, by MultiTurnBeamSearchAiStrategy's per-move rollout budget.
///
/// The 300-second net remains only so a genuine engine hang cannot wedge a training run
/// forever. A game it ends is a broken game, not a draw — see DraftTrainer, which excludes it
/// from training data rather than recording it as one.
/// </summary>
public class GameRunner
{
	private const long SafetyTimeoutMs = 300_000;
	private const int MaxTurns = 100;
	private const int ActionWarningThreshold = 100;
	private const int ActionLimitThreshold = 200;

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

		/// <summary>
		/// The actions chosen during the CURRENT turn, cleared at each turn boundary.
		///
		/// Exists because the flagged-game snapshot could not see the thing it most needed to
		/// show. `TurnLogs` is built from game EVENTS, and the actions that cause an action-limit
		/// draw are typically the ones that emit no event at all — activating an equip, moving an
		/// attachment. A snapshot of a game that burned 200 actions in one turn was showing 11
		/// events and no way to tell what was repeating, which cost two wrong diagnoses and two
		/// training runs.
		///
		/// Names only, not actions: this is held for every game, so it must stay cheap, and the
		/// same reasoning applies as to DrawDiagnostics.BoardSnapshot not being a GameStateSnapshot.
		/// </summary>
		public readonly List<string> ActionsThisTurn = [];
	}

	public (GameResult Result, GameState FinalState) Run(
		GameState preBeginState,
		MtgGameIds ids,
		IReadOnlyDictionary<int, string> cardNames,
		int shuffleSeed = 0,
		int gameRngSeed = 0
	)
	{
		var (initialState, beginEvents) = preBeginState.BeginGame(
			ids.GameId,
			ids.Player1Id,
			ids.Player2Id,
			shuffleSeed,
			gameRngSeed
		);
		var ctx = new RunContext(initialState);
		TrackDrawnCards(beginEvents, ids, cardNames, ctx.DrawnCards1, ctx.DrawnCards2);
		TrackPlayedCards(beginEvents, ids, cardNames, ctx.PlayedCards1, ctx.PlayedCards2);
		ctx.AllEvents.AddRange(beginEvents);

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
		ctx.ActionsThisTurn.Clear();

		while (true)
		{
			if (ctx.Timer.ElapsedMilliseconds > SafetyTimeoutMs)
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

			var chosen = strategy.SelectAction(ctx.State, ids, game.ActivePlayerId);
			// Recorded BEFORE execution — an action that moves a card must be described while its
			// source is still findable.
			ctx.ActionsThisTurn.Add(ActionDescriber.Describe(chosen, ctx.State));
			var (newState, events) = ExecuteAction(ctx.State, chosen);
			ctx.State = newState;
			TrackDrawnCards(events, ids, cardNames, ctx.DrawnCards1, ctx.DrawnCards2);
			TrackPlayedCards(events, ids, cardNames, ctx.PlayedCards1, ctx.PlayedCards2);
			ctx.AllEvents.AddRange(events);
			ctx.TotalActions++;
			actionsThisTurn++;

			if (ctx.State.TryGetGame()?.ActivePlayerId != game.ActivePlayerId)
				return null;
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

		// The choice's OWNER decides, not the active player. Both sides run the same strategy
		// here so the simulation never deadlocked the way the UI did — but it evaluated the
		// opponent's discards and scries as though they were the active player's, which quietly
		// mistrained every model on those cards. 0 means the owner is unknown; fall back.
		var decidingPlayerId = ctx.State.GetPendingChoiceDecidingPlayerId();
		if (decidingPlayerId == 0)
			decidingPlayerId = activePlayerId;

		var selectedIds = strategy.ResolveChoice(ctx.State, choice, decidingPlayerId);
		var (resolvedState, choiceEvents) = ctx.State.ResolveChoice(selectedIds);
		ctx.State = resolvedState;
		TrackDrawnCards(choiceEvents, ids, cardNames, ctx.DrawnCards1, ctx.DrawnCards2);
		TrackPlayedCards(choiceEvents, ids, cardNames, ctx.PlayedCards1, ctx.PlayedCards2);
		ctx.AllEvents.AddRange(choiceEvents);
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
				FinalTurnActions = ctx.ActionsThisTurn.ToList(),
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
			else if (e is PermanentPlayedEvent pe)
			{
				cardId = pe.CardId;
				playerId = pe.PlayerId;
			}
			else if (e is LandPlayedEvent lp)
			{
				cardId = lp.CardId;
				playerId = lp.PlayerId;
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
