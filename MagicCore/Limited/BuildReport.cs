using System.Diagnostics;
using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Sets;

namespace MagicCore.Limited;

/// <summary>
/// Your sealed builds against the same AI sealed opponents, each opponent TWICE — once with you on the play, once on
/// the draw (Shayne, 2026-11-10) — and a report a player reads: win rate overall, on the play and on the draw; game
/// length; each of your cards' games-in-hand win rate beside its win rate when not drawn (17lands' GIH and GNS); and the
/// opponents' cards you lost to most. The search on both sides, so it is what THIS AI does with your 40. Your deck sits
/// in seat 1 and then seat 2 of the same seeded game; the seed's coin flip picks one seat to start, so one game of the
/// two is on the play — read from each game's state, never assumed.
/// </summary>
public static class BuildReport
{
	/// <param name="OnPlay">You started.</param>
	/// <param name="Won">1 a win, 0 a loss, ½ a draw (an unfinished game included).</param>
	/// <param name="Turns">Turns the game lasted, both players' counted.</param>
	/// <param name="Yours">Your nonbasic cards that were in your hand (drawn or kept).</param>
	/// <param name="Theirs">The opponent's nonbasic cards that were in their hand.</param>
	public sealed record Played(
		int Opponent,
		bool OnPlay,
		double Won,
		int Turns,
		IReadOnlySet<string> Yours,
		IReadOnlySet<string> Theirs
	);

	/// <param name="Games">Per build, in the same order for every build: opponent 1 in seat 1, then seat 2, … — null where a
	/// game was stopped past <see cref="SimulatedRatings.GameBudget"/> or failed.</param>
	/// <param name="Against">Who the opponents were — "AI sealed opponents", or a fixed list's name.</param>
	public sealed record Result(
		IReadOnlyList<SealedBuildMatch.Build> Builds,
		IReadOnlyList<Played?[]> Games,
		TimeSpan Elapsed,
		string Against = "AI sealed opponents"
	);

	public static Result Run(
		IReadOnlyList<SealedBuildMatch.Build> builds,
		int opponents,
		string set,
		string ai = "beam",
		Action<string>? log = null,
		TimeSpan? budget = null,
		SealedBuildMatch.Build? against = null,
		int firstOpponent = 1
	)
	{
		var cap = budget ?? SimulatedRatings.GameBudget;
		if (!AiMatch.IsKnown(ai))
			throw new ArgumentException($"Unknown AI '{ai}' — known: {AiMatch.Usage}");
		var clock = Stopwatch.StartNew();
		// opponent i: the AI's build of pool i — or, against a fixed list (Shayne, 2026-11-10: "a 20 game match … against
		// my previous red black aggro deck"), that list every time, a new seed's shuffles each game
		var rivals = new SealedDeck[opponents];
		Parallel.For(
			0,
			opponents,
			i =>
				rivals[i] = against is null
					? SealedBuilder.Opponent(set, i + firstOpponent)
					: new SealedDeck(
						[],
						[.. against.Main.Select(c => c.Data.Name)],
						[.. against.Sideboard.Select(c => c.Data.Name)]
					)
		);
		var perBuild = 2 * opponents;
		var games = new Played?[builds.Count * perBuild];
		var progress = new RunProgress($"{set} builds, both seats", games.Length);
		Parallel.For(
			0,
			games.Length,
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			i =>
			{
				var (build, o, seat1) = (builds[i / perBuild], i % perBuild / 2, i % 2 == 0);
				var n = o + firstOpponent; // the opponent's number: its pool's seed and the game's
				var rival = rivals[o];
				var (rivalMain, rivalOut) = (
					rival.Main.Select(n => CardRegistry.Get(n, set)).ToList(),
					SealedBuildMatch.Outside(rival.Sideboard)
				);
				var start = seat1
					? MagicRules.NewGame(
						build.Main,
						rivalMain,
						n,
						sideboard1: build.Sideboard,
						sideboard2: rivalOut
					)
					: MagicRules.NewGame(
						rivalMain,
						build.Main,
						n,
						sideboard1: rivalOut,
						sideboard2: build.Sideboard
					);
				var (me, them) = seat1
					? (start.Player1Id(), start.Player2Id())
					: (start.Player2Id(), start.Player1Id());
				var hands = new SimulatedRatings.InHandDuring(start);
				GameState? last = null;
				using var budgetCts = new CancellationTokenSource(cap);
				AiMatch.Game? game = null;
				try
				{
					game = AiMatch.Play(
						ai,
						ai,
						start,
						build.Name,
						$"opponent {n}",
						n,
						seat1, // the build is "a": its seat
						1,
						budgetCts.Token,
						seen: x =>
						{
							hands.See(x);
							last = x;
						}
					);
				}
				catch (Exception e) when (e is not OperationCanceledException) // any engine error drops the game, logged
				{
					log?.Invoke(
						$"{build.Name} v opponent {n} (seat {(seat1 ? 1 : 2)}) FAILED, dropped: {e}"
					);
				}
				if (game is null)
				{
					if (budgetCts.IsCancellationRequested && last is not null)
						log?.Invoke(
							$"{build.Name} v opponent {n}: stopped past {cap.TotalMinutes:0} min — {SimulatedRatings.Board(last)}"
						);
				}
				else
					games[i] = new(
						n,
						start.GetGame().StartingPlayerId == me,
						AiMatch.Result.ScoreOfA(game),
						game.Turns,
						hands.Of(me),
						hands.Of(them)
					);
				progress.Tick(
					() =>
						string.Join(
							", ",
							builds.Select(
								(b, k) =>
									games[(k * perBuild)..((k + 1) * perBuild)]
										.OfType<Played>()
										.ToList()
										is var done
									&& done.Count > 0
										? $"{b.Name} {done.Average(g => g.Won) * 100:0}% of {done.Count}"
										: $"{b.Name} -"
							)
						)
				);
			}
		);
		return new Result(
			builds,
			[
				.. Enumerable
					.Range(0, builds.Count)
					.Select(b => games[(b * perBuild)..((b + 1) * perBuild)]),
			],
			clock.Elapsed,
			against is null ? "AI sealed opponents" : $"games against {against.Name}"
		);
	}

	/// <summary>Mean and one standard error.</summary>
	static (double Mean, double Se) Estimate(IReadOnlyList<double> xs) =>
		xs.Count < 2
			? (xs.DefaultIfEmpty(0.5).Average(), double.NaN)
			: (xs.Average(), Math.Sqrt(EarlyStop.Variance(xs) / xs.Count));

	static string Pct((double Mean, double Se) e) =>
		double.IsNaN(e.Se) ? $"{e.Mean * 100:0.0}%" : $"{e.Mean * 100:0.0}% ± {e.Se * 100:0.0}";

	/// <summary>The report, as Markdown: a summary table, then each build's cards and the opponents' cards it lost to.</summary>
	public static string Report(Result r, int minTheirGames = 4)
	{
		var md = new System.Text.StringBuilder();
		var per = r.Games[0].Length / 2;
		md.AppendLine($"# Sealed builds: {per} {r.Against}, each on the play and on the draw\n");
		md.AppendLine(
			$"The search on both sides — what THIS AI does with your 40. ± is one standard error. {r.Elapsed.TotalMinutes:0} min.\n"
		);
		md.AppendLine(
			"| Build | Games | Win rate | On the play | On the draw | Avg turns (both players') | Stopped |"
		);
		md.AppendLine("|---|---|---|---|---|---|---|");
		for (var b = 0; b < r.Builds.Count; b++)
		{
			var g = r.Games[b].OfType<Played>().ToList();
			md.AppendLine(
				$"| {r.Builds[b].Name} | {g.Count} | {Pct(Estimate([.. g.Select(x => x.Won)]))} | "
					+ $"{Pct(Estimate([.. g.Where(x => x.OnPlay).Select(x => x.Won)]))} ({g.Count(x => x.OnPlay)}) | "
					+ $"{Pct(Estimate([.. g.Where(x => !x.OnPlay).Select(x => x.Won)]))} ({g.Count(x => !x.OnPlay)}) | "
					+ $"{(g.Count == 0 ? 0 : g.Average(x => x.Turns)):0.0} | {r.Games[b].Count(x => x is null)} |"
			);
		}
		for (var x = 0; x < r.Builds.Count; x++)
		for (var y = x + 1; y < r.Builds.Count; y++)
		{
			var pairs = r.Games[x]
				.Zip(r.Games[y])
				.Where(p => p.First is not null && p.Second is not null)
				.Select(p => p.First!.Won - p.Second!.Won)
				.ToList();
			var (mean, se) = Estimate(pairs);
			md.AppendLine(
				$"\n**{r.Builds[x].Name} − {r.Builds[y].Name}: {mean * 100:+0.0;-0.0;0.0} points ± {se * 100:0.0}**, paired game by game "
					+ $"(same opponent, same seat, same shuffles; {pairs.Count} pairs)."
			);
		}
		for (var b = 0; b < r.Builds.Count; b++)
		{
			var g = r.Games[b].OfType<Played>().ToList();
			md.AppendLine($"\n## {r.Builds[b].Name}\n");
			md.AppendLine(
				"Your cards — GIH: win rate of the games it was drawn or kept; GNS: of the games it wasn't; IWD = GIH − GNS "
					+ "(what drawing it is worth). Over a hundred games each number is rough — a few points either way.\n"
			);
			md.AppendLine("| Card | In hand | GIH | Not seen | GNS | IWD |");
			md.AppendLine("|---|---|---|---|---|---|");
			var names = r.Builds[b]
				.Main.Select(c => c.Data.Name)
				.Where(n => !SealedBuilder.Basics.Contains(n))
				.Distinct();
			foreach (
				var row in names
					.Select(n =>
					{
						var (inHand, notSeen) = (
							g.Where(x => x.Yours.Contains(n)).ToList(),
							g.Where(x => !x.Yours.Contains(n)).ToList()
						);
						var gih = inHand.Count == 0 ? double.NaN : inHand.Average(x => x.Won);
						var gns = notSeen.Count == 0 ? double.NaN : notSeen.Average(x => x.Won);
						return (n, inHand.Count, gih, notSeen.Count, gns, Iwd: gih - gns);
					})
					.OrderByDescending(x => double.IsNaN(x.Iwd) ? double.MinValue : x.Iwd)
			)
				md.AppendLine(
					$"| {row.n} | {row.Item2} | {Cell(row.gih)} | {row.Item4} | {Cell(row.gns)} | {(double.IsNaN(row.Iwd) ? "" : $"{row.Iwd * 100:+0.0;-0.0;0.0}")} |"
				);
			var theirs = g.SelectMany(x => x.Theirs.Select(n => (n, x.Won)))
				.GroupBy(x => x.n)
				.Select(x => (Card: x.Key, Games: x.Count(), YourWin: x.Average(y => y.Won)))
				.Where(x => x.Games >= minTheirGames)
				.OrderBy(x => x.YourWin)
				.ThenByDescending(x => x.Games)
				.Take(15)
				.ToList();
			md.AppendLine(
				$"\nThe opponents' cards you lost to most — your win rate in the games they had it in hand ({minTheirGames}+ games; "
					+ "an opponent's deck is one pool, so a card meets you in few games):\n"
			);
			md.AppendLine("| Their card | Games | Your win rate |");
			md.AppendLine("|---|---|---|");
			foreach (var t in theirs)
				md.AppendLine($"| {t.Card} | {t.Games} | {t.YourWin * 100:0.0}% |");
		}
		return md.ToString();
	}

	static string Cell(double rate) => double.IsNaN(rate) ? "" : $"{rate * 100:0.0}%";
}
