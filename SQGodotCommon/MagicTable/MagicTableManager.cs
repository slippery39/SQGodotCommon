using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Sets;

namespace MagicTable;

/// <summary>
/// The one bridge between the table and MagicCore — plain C#, no Godot types (like MtgGameManager
/// for MtgCore). Holds the game, the seed, the human's stops, an undo history, the log and the
/// <see cref="Record"/>. The table calls <see cref="StepAi"/> on a timer, so the AI's moves play out
/// one at a time. Every move — yours, the AI's, a pass your stops or the rules made — goes through
/// <see cref="Play"/>: recorded, then made FROM ITS TOKEN, so the record replays exactly this game.
/// </summary>
public sealed class MagicTableManager
{
	private readonly IReadOnlyList<CardDefinition> _deck1;
	private readonly IReadOnlyList<CardDefinition> _deck2;
	private readonly Dictionary<int, IAi> _ais = new();
	private readonly List<string> _log = new();
	private readonly List<(GameState State, int LogCount, int MoveCount)> _undo = new();

	public GameState State { get; private set; }
	public int Seed { get; }

	/// <summary>The bottom seat — yours, or the first AI's when watching AI vs AI.</summary>
	public int HumanId { get; }
	public int OpponentId { get; }
	public bool AiVsAi { get; }

	/// <summary>
	/// The AI is the search (<see cref="BeamSearchAi"/>, about a second a decision), not GreedyAi. The
	/// front ends' default (AI-2.8); false here so tests stay instant and seed-exact.
	/// </summary>
	public bool Search { get; }
	private Stops _stops = Stops.Arena;

	/// <summary>Changing them applies at once: a step that is no longer a stop is passed.</summary>
	public Stops Stops
	{
		get => _stops;
		set
		{
			_stops = value;
			Settle();
			Save();
		}
	}
	public IReadOnlyList<string> Log => _log;

	/// <summary>Every transition of this game (story TBL-5); undone moves leave it.</summary>
	public GameRecord Record { get; private set; }

	/// <summary>A folder to write <see cref="Record"/> into as the game goes; null writes nothing (tests).</summary>
	public string? RecordTo { get; init; }
	private string? _recordFile;
	public bool CanUndo => _undo.Count > 0;

	public MagicTableManager(
		IReadOnlyList<CardDefinition> deck1,
		IReadOnlyList<CardDefinition> deck2,
		int seed,
		bool aiVsAi = false,
		bool search = false,
		IReadOnlyList<CardDefinition>? sideboard1 = null,
		IReadOnlyList<CardDefinition>? sideboard2 = null
	)
	{
		(_deck1, _deck2, Seed, AiVsAi, Search) = (deck1, deck2, seed, aiVsAi, search);
		(_sideboard1, _sideboard2) = (sideboard1 ?? [], sideboard2 ?? []);
		State = MagicRules.NewGame(
			deck1,
			deck2,
			seed,
			names: aiVsAi ? ("AI 1", "AI 2") : ("You", "AI"),
			sideboard1: _sideboard1,
			sideboard2: _sideboard2
		);
		(HumanId, OpponentId) = (State.Player1Id(), State.Player2Id());
		var ai = search ? nameof(BeamSearchAi) : nameof(GreedyAi);
		Record = GameRecord.New(
			seed,
			deck1,
			deck2,
			aiVsAi ? ("AI 1", "AI 2") : ("You", "AI"),
			_sideboard1,
			_sideboard2
		) with
		{
			About = ImmutableDictionary<string, string>
				.Empty.Add("Player1", aiVsAi ? ai : "human")
				.Add("Player2", ai)
				.Add("Started", DateTime.Now.ToString("s")),
		};
		_ais[OpponentId] = NewAi(OpponentId);
		if (aiVsAi)
			_ais[HumanId] = NewAi(HumanId);
		_start = State;
		Settle();
	}

	private readonly GameState _start;

	/// <summary>
	/// A recorded game, resumed (story TBL-5.7): every move of <paramref name="record"/> made again
	/// through <see cref="Play"/>, with an undo step before each decision of yours — so Undo steps
	/// back to before the play you want to see again, and a fixed AI decides from there. Moves are
	/// tokens, never re-decided. Written as a NEW record; the old file is never touched. If the engine
	/// has moved, the replay goes on while the moves still apply — the log says where the first
	/// position differs and where it stopped.
	/// </summary>
	public static MagicTableManager Resume(GameRecord record, bool search, string? recordTo = null)
	{
		record.CheckFormat();
		var m = new MagicTableManager(
			[.. record.Deck1.Select(MagicCore.Sets.CardRegistry.Get)],
			[.. record.Deck2.Select(MagicCore.Sets.CardRegistry.Get)],
			record.Seed,
			aiVsAi: record.About.GetValueOrDefault("Player1") != "human",
			search,
			[.. record.Sideboard1.Select(MagicCore.Sets.CardRegistry.Get)],
			[.. record.Sideboard2.Select(MagicCore.Sets.CardRegistry.Get)]
		)
		{
			RecordTo = recordTo,
		};
		// The constructor settled by today's stops; the record says what was played instead.
		m.State = m._start;
		m.Record = m.Record.Truncate(0) with
		{
			About = m.Record.About.Add(
				"ResumedFrom",
				record.About.GetValueOrDefault("Started", "a record")
			),
		};
		int? differs = null;
		var played = 0;
		var declaring = false;
		foreach (var move in record.Moves)
		{
			if (differs is null && MoveTokens.Offers(m.State) != move.Offers)
				differs = played + 1;
			// A combat declaration is a move per creature but one decision of yours: one undo step.
			var declaration = move.Kind == "you" && IsDeclaration(m.State.GetPendingChoice());
			var step = move.Kind is "you" or "sandbox" && !(declaration && declaring);
			if (step)
				m._undo.Add((m.State, m._log.Count, m.Record.Moves.Count));
			declaring = declaration;
			// As the table logged it: your decisions and sandbox puts are yours, whoever held priority.
			var (turn, who) = (
				m.State.GetGame().TurnNumber,
				move.Kind is "you" or "sandbox" ? m.HumanId : m.State.DecidingPlayerId()
			);
			try
			{
				m.Play(move.Token, move.Kind, move.Said);
			}
			catch (MoveTokens.StaleException)
			{
				if (step)
					m._undo.RemoveAt(m._undo.Count - 1);
				break;
			}
			if (move.Said != "")
				m.Write(turn, who, move.Kind == "sandbox" ? $"[sandbox] {move.Said}" : move.Said);
			played++;
		}
		// A whole record ends settled by the stops it was played with; only a broken replay needs today's.
		if (played < record.Moves.Count)
			m.Settle();
		m._log.Add(
			played == record.Moves.Count && differs is null
				? $"[resumed] all {played} moves — Undo steps back through your decisions"
				: $"[resumed] the engine has moved: the first position to differ is move {differs ?? played + 1}; {played} of {record.Moves.Count} moves replayed"
		);
		m.Save();
		return m;
	}

	/// <summary>The same game again: same decks, same seed, so the same shuffles.</summary>
	public MagicTableManager Replay() =>
		new(_deck1, _deck2, Seed, AiVsAi, Search, _sideboard1, _sideboard2)
		{
			RecordTo = RecordTo,
			Stops = Stops,
		};

	private readonly IReadOnlyList<CardDefinition> _sideboard1;
	private readonly IReadOnlyList<CardDefinition> _sideboard2;

	private IAi NewAi(int playerId) =>
		Search
			? new BeamSearchAi(playerId, BeamSearchAi.Settings.Interactive)
			: new GreedyAi(playerId);

	public bool IsOver => State.IsOver();
	public bool HumanToAct => !IsOver && !_ais.ContainsKey(State.DecidingPlayerId());
	public bool AiToAct => !IsOver && _ais.ContainsKey(State.DecidingPlayerId());

	/// <summary>
	/// Plays AI decisions until one that shows (a pass shows nothing), and returns its log line;
	/// null when it is no AI's turn to decide.
	/// </summary>
	public string? StepAi()
	{
		string? shown = null;
		while (AiToAct && shown is null)
		{
			var who = State.DecidingPlayerId();
			var turn = State.GetGame().TurnNumber;
			var decision = _ais[who].Decide(State);
			var what = Declared(State, decision) ?? decision.What;
			Play(MoveTokens.Token(State, decision), "ai", what == "Pass" ? "" : what);
			Settle();
			if (what != "Pass")
				shown = Write(turn, who, what);
		}
		Save();
		return shown;
	}

	/// <summary>Your move. Ignored unless it is yours to make (a click landing while the AI decides).</summary>
	public void Act(GameAction action)
	{
		if (HumanToAct)
			Decided(
				Describe.Action(State, action),
				[MoveTokens.Token(State, action, null)],
				// A manual tap keeps your priority: auto-passing now would empty the pool you just filled.
				settle: action is not TapForManaAction
			);
	}

	/// <summary>Your answer to the pending choice. Ignored unless it is yours to give.</summary>
	public void Choose(IEnumerable<int> ids)
	{
		if (!HumanToAct)
			return;
		var picked = ids.ToList();
		Decided(
			Describe.Answer(State, State.GetPendingChoice()!, picked),
			[MoveTokens.Token(State, null, picked)]
		);
	}

	/// <summary>Sandbox mode is open whenever someone holds priority and nothing is being asked.</summary>
	public bool CanSandbox => MagicCore.Sandbox.CanPut(State);

	/// <summary>
	/// The test tool: a fresh card put into a zone (onto the battlefield it enters — triggers fire).
	/// Logged and undoable like a decision, whoever's turn it is.
	/// </summary>
	public void Sandbox(int playerId, CardDefinition card, ZoneType zone)
	{
		if (!CanSandbox)
			return;
		var where = zone == ZoneType.Library ? "on top of the library" : $"into {zone}";
		Decided(
			$"[sandbox] {card.Data.Name} {where} ({State.GetPlayer(playerId).Name})",
			[MoveTokens.Sandbox(playerId, card.Data.Name, zone)],
			kind: "sandbox"
		);
	}

	/// <summary>
	/// Your whole combat declaration at once, as on Arena: each creature you picked → the player or
	/// planeswalker it attacks, or the attacker it blocks; every other creature "don't". The engine
	/// still asks one creature at a time; each question is answered from <paramref name="chosen"/>.
	/// The engine is the judge: a pick it doesn't offer (an attack limit) or drops afterwards (a lone
	/// blocker on menace) is REFUSED — then nothing is committed and the refusals come back, so the
	/// table can say why. Otherwise one decision: one undo step, one log line. An empty map is
	/// "No attacks" / "No blocks".
	/// </summary>
	public IReadOnlyList<string> DeclareCombat(IReadOnlyDictionary<int, int> chosen)
	{
		var attacking = State.GetPendingChoice() is DeclareAttackerChoice;
		if (!HumanToAct || !IsDeclaration(State.GetPendingChoice()))
			return [];
		var next = State;
		var asked = new HashSet<int>();
		var answers = new List<string>();
		while (
			next.DecidingPlayerId() == HumanId
			&& next.GetPendingChoice() is { } choice
			&& IsDeclaration(choice)
		)
		{
			var creature = choice switch
			{
				DeclareAttackerChoice a => a.CreatureId,
				DeclareBlockerChoice b => b.CreatureId,
				_ => 0,
			};
			asked.Add(creature);
			var pick = choice.Options.FirstOrDefault(o =>
				o.IsEnabled
				&& o.Id != CombatOption.None
				&& o.Id == chosen.GetValueOrDefault(creature)
			);
			answers.Add(MoveTokens.Token(next, null, [pick?.Id ?? CombatOption.None]));
			next = MoveTokens.Apply(next, answers[^1]);
		}

		var took = chosen
			.Where(d =>
				asked.Contains(d.Key)
				&& next.HasObject(d.Key)
				&& (attacking ? next.GetCard(d.Key).AttackingId : next.GetCard(d.Key).BlockingId)
					== d.Value
			)
			.ToList();
		var refused = chosen
			.Except(took)
			.Select(d => $"{Pair(State, d.Key, d.Value)} isn't allowed")
			.ToList();
		if (refused.Count > 0)
			return refused;
		Decided(
			took.Count == 0
				? (attacking ? "No attacks" : "No blocks")
				: $"{(attacking ? "Attack" : "Block")}: {string.Join(", ", took.Select(d => Pair(State, d.Key, d.Value)))}",
			answers
		);
		return [];
	}

	private static bool IsDeclaration(ChoiceAction? choice) =>
		choice is DeclareAttackerChoice or DeclareBlockerChoice;

	private static string Pair(GameState s, int creature, int target) =>
		$"{s.GetCard(creature).Name} → {s.GetObject(target).Name}";

	/// <summary>
	/// An AI's combat declaration as yours reads ("Attack: Samut → You"); "don't" as a pass, which
	/// shows nothing. Null for anything else. Describe.Answer would log the prompt itself
	/// ("Attack with Samut 2/2? Attack You") — and the golden replays hash that text, so it stays.
	/// </summary>
	private static string? Declared(GameState s, AiDecision decision) =>
		(s.GetPendingChoice(), decision.Answer) switch
		{
			(DeclareAttackerChoice or DeclareBlockerChoice, [var pick])
				when pick == CombatOption.None => "Pass",
			(DeclareAttackerChoice a, [var target]) => $"Attack: {Pair(s, a.CreatureId, target)}",
			(DeclareBlockerChoice b, [var attacker]) => $"Block: {Pair(s, b.CreatureId, attacker)}",
			_ => null,
		};

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
			Play(MoveTokens.Token(State, standIn.Decide(State)), "auto");
			Settle();
		}
		Save();
	}

	/// <summary>Back to just before your last decision — the AI's replies since then go too.</summary>
	public void Undo()
	{
		if (!CanUndo)
			return;
		var (state, logCount, moveCount) = _undo[^1];
		_undo.RemoveAt(_undo.Count - 1);
		State = state;
		_log.RemoveRange(logCount, _log.Count - logCount);
		Record = Record.Truncate(moveCount);
		// The search commits to a plan; one made past the undone decisions would replay out of order.
		foreach (var id in _ais.Keys.ToList())
			_ais[id] = NewAi(id);
		Save();
	}

	/// <summary>
	/// One decision of yours, as one or more moves (a combat declaration answers each creature, and
	/// has already checked them all). A rejected move leaves no undo step.
	/// </summary>
	private void Decided(
		string line,
		IReadOnlyList<string> tokens,
		bool settle = true,
		string kind = "you"
	)
	{
		var undo = (State, _log.Count, Record.Moves.Count);
		var turn = State.GetGame().TurnNumber;
		foreach (var token in tokens)
			Play(token, kind, MoveTokens.Describe(State, token));
		_undo.Add(undo);
		Write(turn, HumanId, line);
		if (settle)
			Settle();
		Save();
	}

	/// <summary>
	/// Makes the move from its token — what a replay will make — and records it. A move the engine
	/// rejects throws before either changes.
	/// </summary>
	private void Play(string token, string kind, string said = "")
	{
		var next = MoveTokens.Apply(State, token);
		Record = Record.Add(State, token, kind, said);
		var before = State;
		var seen = KnownInOpponentsHand(before).Select(c => c.Id).ToHashSet();
		State = next;
		// V'Ger's "look at target opponent's hand", a revealed search: the log says what you now know is
		// there — only cards that came from a hidden zone (a bounced creature was seen already).
		// Noted after the line of the move that caused it (Write, Save). In the hand itself: TBL-6.
		if (
			KnownInOpponentsHand(State)
				.Where(c =>
					!seen.Contains(c.Id)
					&& (
						before.IsIn(c.Id, OpponentId, ZoneType.Hand)
						|| before.IsIn(c.Id, OpponentId, ZoneType.Library)
					)
				)
				.ToList()
			is [_, ..] news
		)
			_notes.Add(
				(
					State.GetGame().TurnNumber,
					$"seen in {State.GetPlayer(OpponentId).Name}'s hand: {string.Join(", ", news.Select(c => c.Name))}"
				)
			);
	}

	/// <summary>What a move revealed, logged after the move's own line.</summary>
	private readonly List<(int Turn, string What)> _notes = new();

	private void WriteNotes()
	{
		foreach (var (turn, what) in _notes)
			Write(turn, HumanId, what, notes: false);
		_notes.Clear();
	}

	/// <summary>The opponent's hand cards you know (Card.KnownTo — relative to their owner, the opponent).</summary>
	private List<Card> KnownInOpponentsHand(GameState s) =>
		[.. s.CardsIn(OpponentId, ZoneType.Hand).Where(c => (c.KnownTo & Knowers.Opponent) != 0)];

	/// <summary>
	/// Writes the record over its file — once per public call (a decision, an AI step, an undo), not
	/// per pass. A file that can't be written is said once in the log; the game goes on.
	/// </summary>
	private void Save()
	{
		WriteNotes(); // a reveal on a move that writes no line (a pass resolving V'Ger's trigger)
		if (RecordTo is null)
			return;
		try
		{
			Directory.CreateDirectory(RecordTo);
			_recordFile ??= Path.Combine(
				RecordTo,
				$"{DateTime.Now:yyyy-MM-dd_HHmmss}_seed{Seed}.json"
			);
			File.WriteAllText(_recordFile, (IsOver ? Record.Ended(State) : Record).ToJson());
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			if (!_recordFailed)
				_log.Add($"[record] not saved: {e.Message}");
			_recordFailed = true;
		}
	}

	private bool _recordFailed;

	private string Write(int turn, int who, string what, bool notes = true)
	{
		var line = $"T{turn} {State.GetPlayer(who).Name}: {what}";
		_log.Add(line);
		if (notes)
			WriteNotes();
		return line;
	}

	/// <summary>
	/// Passes wherever passing is the only move (anyone: <c>rule</c>), and for the human between stops
	/// (<c>stop</c>) — AutoPass and Stops.PassUntilStop as one loop, so each pass is recorded.
	/// </summary>
	private void Settle()
	{
		while (!IsOver)
		{
			if (State.LegalActions() is [PassPriorityAction pass])
				Play(MoveTokens.Token(State, pass, null), "rule");
			else if (HumanToAct && !Stops.StopsAt(State, HumanId))
				Play(
					MoveTokens.Token(State, new PassPriorityAction { PlayerId = HumanId }, null),
					"stop"
				);
			else
				break;
		}
	}
}
