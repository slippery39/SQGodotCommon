using System.Text;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Sets.Fra;

namespace MagicCore.Ai;

/// <summary>
/// AI-vs-AI games as a REPLAY to watch (story AI-14 — Shayne: "think of the log more like a replay, where
/// I am just watching a game … the main thing is that I can follow what actually happened"). The default
/// search on both sides; a section a turn; every move one line — a whole attack and a whole block each
/// one line — with what it did under it (who died, left or entered, life changed), read from the game
/// itself; and, where the search had a real choice, its score against the next best. Close calls first.
/// </summary>
public static class DecisionReport
{
	/// <summary>Within this of the best, a second option makes a decision a close call — listed first.</summary>
	const double CloseCall = 1.0;

	/// <returns>The report, and each game as a record — so a flagged position can be kept as a test
	/// (`--record=FILE --keep=NAME --at=N`).</returns>
	public static (string Report, IReadOnlyList<GameRecord> Records) Run(
		int games = 2,
		int firstSeed = 1
	)
	{
		var decks = FraDecks.All.Keys.ToList();
		var md = new StringBuilder();
		var close = new List<string>();
		var records = new List<GameRecord>();
		for (var g = 0; g < games; g++)
		{
			var (a, b) = (decks[g % decks.Count], decks[(g + 1) % decks.Count]);
			var game = Play(
				firstSeed + g,
				FraDecks.All[a],
				FraDecks.All[b],
				(a, b),
				$"Game {g + 1}"
			);
			md.Append(game.Md);
			close.AddRange(game.Close);
			records.Add(game.Record);
		}
		var report =
			"# AI games, move by move\n\n"
			+ "The default search on both sides. Each move is one line, what it did under it (→). Where the "
			+ "AI had a real choice, its score is shown against the next best — higher is better for the side "
			+ "moving; a tie means waiting would do the same thing later in the turn.\n\n"
			+ $"## Close calls (the next best within {CloseCall:0.0}, ties left out)\n\n"
			+ (close.Count > 0 ? string.Join("\n", close) : "None.")
			+ "\n"
			+ md;
		return (report, records);
	}

	/// <summary>One game, played and written: its replay section, its close calls, its record and how it ended.</summary>
	public sealed record Played(
		string Md,
		IReadOnlyList<string> Close,
		GameRecord Record,
		GameState End
	);

	/// <summary>
	/// One game between any two decks — FRA's built decks (<see cref="Run"/>) or sealed builds (MC-5.4: the
	/// self-play games a card was drawn in, <c>--card-games</c>). The players are named <paramref name="names"/>,
	/// so every description reads as the replay does. Thread-safe: everything it writes is its own.
	/// </summary>
	public static Played Play(
		int seed,
		IReadOnlyList<CardDefinition> deck1,
		IReadOnlyList<CardDefinition> deck2,
		(string Player1, string Player2) names,
		string heading,
		IReadOnlyList<CardDefinition>? sideboard1 = null,
		IReadOnlyList<CardDefinition>? sideboard2 = null,
		int? threads = null
	)
	{
		var md = new StringBuilder();
		var close = new List<string>();
		var s = MagicRules.NewGame(
			deck1,
			deck2,
			seed,
			names: names,
			sideboard1: sideboard1,
			sideboard2: sideboard2
		);
		var first = s.DecidingPlayerId();
		var named = new[] { first, s.OpponentOf(first) }.ToDictionary(
			p => p,
			p => s.GetPlayer(p).Name
		);
		var ais = named.Keys.ToDictionary(
			p => p,
			p => new BeamSearchAi(p, new BeamSearchAi.Settings(Threads: threads)) // 1: games run in parallel
		);
		md.Append($"\n## {heading}: {names.Player1} vs {names.Player2}, seed {seed}\n");
		var replay = new Replay(md, named, s);
		var record = GameRecord.New(seed, deck1, deck2, names, sideboard1, sideboard2);
		for (var step = 0; step < 4_000 && !s.IsOver(); step++)
		{
			var who = s.DecidingPlayerId();
			var ai = ais[who];
			var decision = ai.Decide(s);
			replay.Move(s, who, decision, ai.FirstMoveScores, out var closeCall);
			if (closeCall is not null)
				close.Add($"- {heading}, turn {s.GetGame().TurnNumber}, {named[who]}: {closeCall}");
			record = record.Add(s, MoveTokens.Token(s, decision), "ai");
			s = decision.State;
		}
		replay.End(s);
		md.Append(
			$"\n**Result:** {(s.IsOver() ? (s.WinnerId() is { } w and > 0 ? $"{named[w]} won" : "a draw") : "unfinished")} on turn {s.GetGame().TurnNumber}.\n"
		);
		return new(md.ToString(), close, s.IsOver() ? record.Ended(s) : record, s);
	}

	/// <summary>Writes one game as it goes: a turn's heading, each move, a combat declaration gathered into one line, and the changes under each.</summary>
	internal sealed class Replay(StringBuilder md, Dictionary<int, string> names, GameState start)
	{
		GameState since = start; // the state the last line's changes are measured from
		int turn;
		(int Player, bool Attacking, List<string> Picks)? combat;

		public void Move(
			GameState s,
			int who,
			AiDecision decision,
			IReadOnlyList<(GameAction? Action, IReadOnlyList<int>? Answer, double Score)> options,
			out string? closeCall
		)
		{
			closeCall = null;
			var choice = s.GetPendingChoice();
			// a declaration is asked one creature at a time: gathered into one line
			if (
				choice is DeclareAttackerChoice or DeclareBlockerChoice
				&& decision.Answer is { } ids
			)
			{
				var attacking = choice is DeclareAttackerChoice;
				if (combat is not { } c || c.Player != who || c.Attacking != attacking)
				{
					Flush(s);
					combat = (who, attacking, []);
				}
				if (ids is [var id] && id != CombatOption.None)
					combat!.Value.Picks.Add(
						choice is DeclareAttackerChoice att
							? $"{Name(s, att.CreatureId)} → {Target(s, id)}"
							: $"{Name(s, ((DeclareBlockerChoice)choice).CreatureId)} on {Name(s, id)}"
					);
				return;
			}
			if (decision.Action is PassPriorityAction)
				return;
			Flush(s);
			var note = "";
			if (options.Count >= 2 && decision.Action is not PlayLandAction)
			{
				// against the move actually made — a plan can play other than the top-scored first move
				var mine = options
					.Where(o => Same(o, decision))
					.Select(o => o.Score)
					.DefaultIfEmpty(options.Max(o => o.Score))
					.First();
				var next = options.Where(o => !Same(o, decision)).MaxBy(o => o.Score);
				var gap = mine - next.Score;
				note = $" *(over {Describe(s, next)}, {gap:0.0} behind)*";
				if (gap is > 0.005 and < CloseCall)
					closeCall = $"{decision.What} over {Describe(s, next)} ({gap:0.00})";
			}
			md.Append($"- {names[who]}: {decision.What}{note}\n");
		}

		public void End(GameState s) => Flush(s);

		/// <summary>Writes a gathered declaration, and the changes since the last line, before a new line — and a turn's heading.</summary>
		void Flush(GameState s)
		{
			if (combat is { } c)
			{
				md.Append(
					$"- {names[c.Player]} {(c.Attacking ? "attacks" : "blocks")}: {(c.Picks.Count > 0 ? string.Join("; ", c.Picks) : c.Attacking ? "no attack" : "no blocks")}\n"
				);
				combat = null;
			}
			if (Changes(since, s) is { Length: > 0 } changes)
				md.Append($"  - → {changes}\n");
			since = s;
			if (s.GetGame().TurnNumber != turn && !s.IsOver())
			{
				turn = s.GetGame().TurnNumber;
				var active = s.GetGame().ActivePlayerId;
				md.Append(
					$"\n### Turn {turn}: {names[active]} ({string.Join(", ", names.Select(n => $"{n.Value} {s.GetPlayer(n.Key).Life}"))} life)\n\n"
				);
			}
		}

		string Name(GameState s, int id) => s.HasObject(id) ? s.GetCard(id).Name : $"#{id}";

		string Target(GameState s, int id) =>
			names.TryGetValue(id, out var deck) ? deck : Name(s, id);

		static bool Same(
			(GameAction? Action, IReadOnlyList<int>? Answer, double Score) o,
			AiDecision d
		) =>
			o.Action is not null
				? d.Action is not null && o.Action == d.Action
				: d.Answer is not null && o.Answer is not null && o.Answer.SequenceEqual(d.Answer);

		static string Describe(
			GameState s,
			(GameAction? Action, IReadOnlyList<int>? Answer, double Score) o
		) =>
			o.Action is not null
				? MagicCore.Describe.Action(s, o.Action)
				: MagicCore.Describe.Answer(s, s.GetPendingChoice()!, o.Answer!);

		/// <summary>What changed between two states: permanents that left play (and where to), ones that entered, life.</summary>
		string Changes(GameState before, GameState after)
		{
			var parts = new List<string>();
			var was = names.Keys.SelectMany(p => before.CardsIn(p, ZoneType.Battlefield)).ToList();
			var now = names.Keys.SelectMany(p => after.CardsIn(p, ZoneType.Battlefield)).ToList();
			foreach (var c in was.Where(c => !c.Is(CardType.Land) && now.All(n => n.Id != c.Id)))
				parts.Add($"{c.Name} {Went(after, c)}");
			foreach (var c in now.Where(c => !c.Is(CardType.Land) && was.All(w => w.Id != c.Id)))
				parts.Add($"{c.Name} enters ({names.GetValueOrDefault(c.ControllerId, "?")})");
			foreach (var (p, deck) in names)
				if (after.GetPlayer(p).Life != before.GetPlayer(p).Life)
					parts.Add(
						$"{deck} {before.GetPlayer(p).Life} → {after.GetPlayer(p).Life} life"
					);
			return string.Join("; ", parts);
		}

		string Went(GameState s, Card c)
		{
			if (!s.HasObject(c.Id))
				return c.IsToken ? "dies" : "is gone"; // a token ceases to exist as it leaves
			foreach (var p in names.Keys)
			{
				if (s.IsIn(c.Id, p, ZoneType.Graveyard))
					return "dies";
				if (s.IsIn(c.Id, p, ZoneType.Exile))
					return "is exiled";
				if (s.IsIn(c.Id, p, ZoneType.Hand))
					return "returns to hand";
				if (s.IsIn(c.Id, p, ZoneType.Library))
					return "goes to the library";
			}
			return "leaves play";
		}
	}
}
