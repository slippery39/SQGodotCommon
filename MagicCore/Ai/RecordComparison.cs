using System.Diagnostics;
using System.Globalization;

namespace MagicCore.Ai;

/// <summary>
/// A played game's choices beside the AI's (story TBL-5.6): at each real decision the human made
/// (<c>you</c> moves offering a choice), what the default search plays there, and where the oracle
/// (<see cref="PositionSuite.OracleSettings"/>, every first move scored) ranks the human's pick. The
/// search is the uncapped default, not the table's 1 s cap, so a report repeats (Shayne, 2026-10-23).
/// The summary is the position suite's <see cref="PositionSuite.Judge"/>, the human's picks as the answers.
/// </summary>
public static class RecordComparison
{
	/// <param name="Move">The move's number, from 1 — the transcript's #N and <c>--at</c>.</param>
	/// <param name="You">Your pick, the search's and the oracle's are tokens; the <c>…Said</c> fields say them.</param>
	public sealed record Decision(
		int Move,
		int Turn,
		string You,
		string Search,
		PositionSuite.OracleReading Oracle,
		string YouSaid,
		string SearchSaid,
		string OracleSaid
	)
	{
		public double? Score(string token) =>
			Oracle
				.Scores.Where(kv => MoveTokens.Same(kv.Key, token))
				.Select(kv => (double?)kv.Value)
				.FirstOrDefault();

		/// <summary>Your pick among the oracle's scores — 1 is its best, ties share; null when it didn't score it.</summary>
		public int? Rank =>
			Score(You) is { } mine ? 1 + Oracle.Scores.Values.Count(v => v > mine) : null;

		/// <summary>Not the oracle's pick, but one it scores as well (to its 2 decimals) or better — not a mistake.</summary>
		public bool Ties =>
			!MoveTokens.Same(You, Oracle.Best)
			&& Score(You) is { } mine
			&& Score(Oracle.Best) is { } best
			&& mine >= best;

		public bool Differs => !MoveTokens.Same(You, Search) || !MoveTokens.Same(You, Oracle.Best);
	}

	/// <param name="Moves">Every <c>you</c> move, the one-answer ones included.</param>
	public sealed record Report(
		string Player,
		int Moves,
		IReadOnlyList<Decision> Decisions,
		PositionSuite.OracleVerdict Verdict,
		TimeSpan Elapsed
	)
	{
		public string Text()
		{
			var n = Decisions.Count;
			if (n == 0)
				return $"No decisions of yours to compare ({Moves} moves, none with a choice).";
			string Pct(int k) => $"{100.0 * k / n:0.0}%";
			var v = Verdict;
			var lines = new List<string>();
			foreach (var d in Decisions.Where(d => d.Differs))
			{
				var oracle = MoveTokens.Same(d.You, d.Oracle.Best)
					? "oracle: same"
					: $"oracle: {d.OracleSaid} ({Ranked(d)})";
				var search = MoveTokens.Same(d.You, d.Search)
					? "search: same"
					: $"search: {d.SearchSaid}";
				lines.Add($"#{d.Move} T{d.Turn} {d.YouSaid} — {oracle} · {search}");
			}
			lines.Add(
				$"{Player}: {n} decisions compared ({Moves} moves; the rest had one answer), {Elapsed.TotalSeconds:0.0} s"
			);
			lines.Add(
				$"  agreed with the search: {Pct(Decisions.Count(d => MoveTokens.Same(d.You, d.Search)))}, with the oracle: {Pct(v.Agree)}, {Pct(v.Agree + Decisions.Count(d => d.Ties))} counting the ties (the search with the oracle: {Pct(v.CommittedAgree)})"
			);
			lines.Add(
				$"  score given up to the oracle: {v.MeanGivenUp:0.00} a decision over {v.Scored}; missed a win it found: {v.MissedWins}, walked into a loss it avoids: {v.WalkedIntoLosses}, not scored: {v.Unscored}"
			);
			return string.Join("\n", lines);
		}

		static string Ranked(Decision d)
		{
			if (d.Oracle.Scores.Count == 0)
				return "it searched nothing here";
			if (d.Rank is not { } rank || d.Score(d.Oracle.Best) is not { } best)
				return "yours not scored";
			var mine = d.Score(d.You)!.Value;
			const double win = BeamSearchAi.WinThreshold;
			var cost =
				best >= win && mine < win ? "misses a win"
				: mine <= -win && best > -win ? "walks into a loss"
				: Math.Abs(best) >= win ? "decided either way" // as Judge: a decided score would swamp the rest
				: $"-{(best - mine).ToString("0.00", CultureInfo.InvariantCulture)}";
			return d.Ties
				? (mine > best ? "yours scores higher" : "a tie with yours")
					+ $", of {d.Oracle.Scores.Count}"
				: $"yours {rank} of {d.Oracle.Scores.Count}, {cost}";
		}
	}

	/// <summary>Every real decision of the record's <c>you</c> moves, read by the search and the oracle in parallel.</summary>
	public static Report Compare(
		GameRecord record,
		BeamSearchAi.Settings? search = null,
		BeamSearchAi.Settings? oracle = null
	)
	{
		var clock = Stopwatch.StartNew();
		var (before, _) = record.Replay();
		var yours = Enumerable
			.Range(0, record.Moves.Count)
			.Where(i => record.Moves[i].Kind == "you")
			.ToList();
		var real = yours.Where(i => PositionSuite.IsReal(before[i])).ToList();
		var decisions = new Decision[real.Count];
		Parallel.For(
			0,
			real.Count,
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			k =>
			{
				var (s, you) = (before[real[k]], record.Moves[real[k]].Token);
				var searched = MoveTokens.Token(
					s,
					new BeamSearchAi(
						s.DecidingPlayerId(),
						(search ?? new BeamSearchAi.Settings()) with
						{
							Threads = 1,
						}
					).Decide(s)
				);
				var reading = PositionSuite.Read(
					s,
					k,
					(oracle ?? PositionSuite.OracleSettings) with
					{
						Threads = 1,
					}
				);
				decisions[k] = new Decision(
					real[k] + 1,
					s.GetGame().TurnNumber,
					you,
					searched,
					reading,
					MoveTokens.Describe(s, you),
					MoveTokens.Describe(s, searched),
					MoveTokens.Describe(s, reading.Best)
				);
			}
		);
		var verdict = PositionSuite.Judge(
			[.. decisions.Select(d => d.Oracle)],
			[.. decisions.Select(d => d.You)],
			[.. decisions.Select(d => d.Search)]
		);
		var player = yours.Count > 0 ? record.Players[record.Moves[yours[0]].By - 1] : "Nobody";
		return new Report(player, yours.Count, decisions, verdict, clock.Elapsed);
	}
}
