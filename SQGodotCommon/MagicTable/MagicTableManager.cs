using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Ai;
using MagicCore.Cards;

namespace MagicTable;

/// <summary>
/// The one bridge between the table and MagicCore — plain C#, no Godot types (like MtgGameManager
/// for MtgCore). Holds the game, the seed, the human's stops, an undo history and the log. The
/// table calls <see cref="StepAi"/> on a timer, so the AI's moves play out one at a time.
/// </summary>
public sealed class MagicTableManager
{
	private readonly IReadOnlyList<CardDefinition> _deck1;
	private readonly IReadOnlyList<CardDefinition> _deck2;
	private readonly Dictionary<int, GreedyAi> _ais = new();
	private readonly List<string> _log = new();
	private readonly List<(GameState State, int LogCount)> _undo = new();

	public GameState State { get; private set; }
	public int Seed { get; }

	/// <summary>The bottom seat — yours, or the first AI's when watching AI vs AI.</summary>
	public int HumanId { get; }
	public int OpponentId { get; }
	public bool AiVsAi { get; }
	private Stops _stops = Stops.Arena;

	/// <summary>Changing them applies at once: a step that is no longer a stop is passed.</summary>
	public Stops Stops
	{
		get => _stops;
		set
		{
			_stops = value;
			Settle();
		}
	}
	public IReadOnlyList<string> Log => _log;
	public bool CanUndo => _undo.Count > 0;

	public MagicTableManager(
		IReadOnlyList<CardDefinition> deck1,
		IReadOnlyList<CardDefinition> deck2,
		int seed,
		bool aiVsAi = false
	)
	{
		(_deck1, _deck2, Seed, AiVsAi) = (deck1, deck2, seed, aiVsAi);
		State = MagicRules.NewGame(deck1, deck2, seed);
		(HumanId, OpponentId) = (State.Player1Id(), State.Player2Id());
		_ais[OpponentId] = new GreedyAi(OpponentId);
		if (aiVsAi)
			_ais[HumanId] = new GreedyAi(HumanId);
		Settle();
	}

	/// <summary>The same game again: same decks, same seed, so the same shuffles.</summary>
	public MagicTableManager Replay() => new(_deck1, _deck2, Seed, AiVsAi) { Stops = Stops };

	public bool IsOver => State.IsOver();
	public bool HumanToAct => !IsOver && !_ais.ContainsKey(State.DecidingPlayerId());
	public bool AiToAct => !IsOver && _ais.ContainsKey(State.DecidingPlayerId());

	/// <summary>
	/// Plays AI decisions until one that shows (a pass shows nothing), and returns its log line;
	/// null when it is no AI's turn to decide.
	/// </summary>
	public string? StepAi()
	{
		while (AiToAct)
		{
			var who = State.DecidingPlayerId();
			var turn = State.GetGame().TurnNumber;
			var (next, what) = _ais[who].Decide(State);
			State = next;
			Settle();
			if (what != "Pass")
				return Write(turn, who, what);
		}
		return null;
	}

	/// <summary>Your move. Ignored unless it is yours to make (a click landing while the AI decides).</summary>
	public void Act(GameAction action)
	{
		if (HumanToAct)
			Decided(Describe.Action(State, action), State.Act(action));
	}

	/// <summary>Your answer to the pending choice. Ignored unless it is yours to give.</summary>
	public void Choose(IEnumerable<int> ids)
	{
		if (!HumanToAct)
			return;
		var picked = ids.ToList();
		Decided(Describe.Answer(State, State.GetPendingChoice()!, picked), State.Choose(picked));
	}

	/// <summary>
	/// Debug (<c>-GameArgs --autoplay=N</c>): a stand-in AI plays your seat too until turn N, so the
	/// table opens mid-game for a capture. Leaves no undo steps.
	/// </summary>
	public void AutoplayTo(int turn)
	{
		var standIn = new GreedyAi(HumanId);
		while (!IsOver && State.GetGame().TurnNumber < turn)
		{
			if (AiToAct)
			{
				StepAi();
				continue;
			}
			State = standIn.Decide(State).State;
			Settle();
		}
	}

	/// <summary>Back to just before your last decision — the AI's replies since then go too.</summary>
	public void Undo()
	{
		if (!CanUndo)
			return;
		var (state, logCount) = _undo[^1];
		_undo.RemoveAt(_undo.Count - 1);
		State = state;
		_log.RemoveRange(logCount, _log.Count - logCount);
	}

	/// <summary>Applied only once the engine accepted it, so a rejected move leaves no undo step.</summary>
	private void Decided(string line, GameState next)
	{
		_undo.Add((State, _log.Count));
		var turn = State.GetGame().TurnNumber;
		State = next;
		Write(turn, HumanId, line);
		Settle();
	}

	private string Write(int turn, int who, string what)
	{
		var line = $"T{turn} {(who == HumanId ? (AiVsAi ? "AI 1" : "You") : "AI")}: {what}";
		_log.Add(line);
		return line;
	}

	/// <summary>Passes wherever passing is the only move (anyone), and for the human between stops.</summary>
	private void Settle()
	{
		GameState before;
		do
		{
			before = State;
			State = State.AutoPass();
			if (HumanToAct)
				State = Stops.PassUntilStop(State, HumanId);
		} while (!ReferenceEquals(before, State));
	}
}
