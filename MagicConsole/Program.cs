using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Sets;
using MagicCore.Sets.Fra;

// Play Reality Fracture against the AI in the console.
//   dotnet run --project MagicConsole                      you vs the AI
//   dotnet run --project MagicConsole -- --ai-vs-ai        watch two AIs
//   --seed=N replays a game; --deck=N skips the deck prompt (the AI plays the next deck);
//   --greedy: the AI is GreedyAi (one ply, instant), not the search (BeamSearchAi, ~1 s a decision).
//   dotnet run --project MagicConsole -- --match=greedy,random --games=72   AI vs AI, measured
//   dotnet run --project MagicConsole -- --card-values   regenerate MagicCore/Sets/FRA.values.json and TRK.values.json
//   dotnet run --project MagicConsole -- --record=game_records/<file>.json   replay a table game

var flags = args.ToHashSet();
int? Arg(string name) =>
	args.FirstOrDefault(a => a.StartsWith($"--{name}=")) is { } a
	&& int.TryParse(a[(name.Length + 3)..], out var n)
		? n
		: null;

// --match=A,B [--games=N] [--sequential] [--until-clear]: AI against AI over every deck pairing, both
// seats (AiMatch). --until-clear stops once a 5pp difference, or its absence, is settled; --games is
// then the cap (default 600). Games come in whole seeds (12 a seed), so a count rounds up to one.
if (args.FirstOrDefault(a => a.StartsWith("--match=")) is { } match)
{
	var names = match["--match=".Length..].Split(',');
	if (names.Length != 2 || names.Any(n => !AiMatch.IsKnown(n)))
	{
		Console.WriteLine($"--match=A,B with two of: {AiMatch.Usage}");
		return;
	}
	var sequential = flags.Contains("--sequential");
	Console.WriteLine(
		(
			flags.Contains("--until-clear")
				? AiMatch.RunUntilClear(names[0], names[1], Arg("games") ?? 600, sequential)
				: AiMatch.Run(names[0], names[1], Arg("games") ?? 24, sequential)
		).Report()
	);
	return;
}

// --builds=A.txt,B.txt[,…] [--games=N]: candidate sealed builds, each an Arena export (its Sideboard
// section is the rest of your pool), against the same N AI sealed opponents — the search on both sides
// (SealedBuildMatch, LIM-1.8). Default 200: the smallest difference that shows is then ~7pp.
// --compare=POOL.txt | seed:N: every build the Sealed screen offers for that pool — its suggestions
// and every close pair's (LIM-3.4) — with any --builds exports beside them (yours, say).
// --set=TRK: the pool, the ratings and the opponents are TRK's (MC-5.4); --draft takes it too.
var limitedSet =
	args.FirstOrDefault(a => a.StartsWith("--set="))?["--set=".Length..].ToUpperInvariant()
	?? "FRA";
var compareArg = args.FirstOrDefault(a => a.StartsWith("--compare="))?["--compare=".Length..];
var buildsArg = args.FirstOrDefault(a => a.StartsWith("--builds="))?["--builds=".Length..];
if (compareArg is not null || buildsArg is not null)
{
	try
	{
		var pool = compareArg switch
		{
			null => null,
			_ when compareArg.StartsWith("seed:") => int.TryParse(compareArg[5..], out var poolSeed)
				? MagicCore.Limited.Boosters.SealedPool(limitedSet, poolSeed)
				: throw new ArgumentException($"--compare={compareArg}: the seed isn't a number"),
			_ => MagicCore
				.Sets.Fra.ArenaDeck.ReadPool(File.ReadAllText(compareArg), limitedSet)
				.Pool,
		};
		if (pool is { Count: 0 })
			throw new ArgumentException($"{compareArg}: no {limitedSet} cards in it");
		var builds = (
			pool is null
				? []
				: MagicCore.Limited.SealedBuildMatch.FromSuggestions(
					MagicCore.Limited.SealedBuilder.AllBuilds(
						pool,
						MagicCore.Limited.CardRatings.Of(limitedSet)
					)
				)
		)
			.Concat(
				(buildsArg?.Split(',') ?? []).Select(path =>
					MagicCore.Limited.SealedBuildMatch.FromExport(
						Path.GetFileNameWithoutExtension(path),
						File.ReadAllText(path)
					)
				)
			)
			.ToList();
		var games = Arg("games") ?? MagicCore.Limited.SealedBuildMatch.DefaultOpponents;
		Console.WriteLine(
			$"{builds.Count} builds ({string.Join(", ", builds.Select(b => b.Name))}) x {games} opponents: ~{MagicCore.Limited.SealedBuildMatch.EstimatedMinutes(builds.Count, games):0} min"
		);
		Console.WriteLine(
			MagicCore.Limited.SealedBuildMatch.Run(builds, games, set: limitedSet).Report()
		);
	}
	catch (Exception e) when (e is IOException or ArgumentException)
	{
		Console.WriteLine(e.Message);
		Environment.ExitCode = 1;
	}
	return;
}

// --suite=generate: record the search's self-play as the position suite (run from the solution root).
// --suite=A [--accept]: what AI A decides at every position, against the committed answers (AI-6.4);
// --accept makes A's answers the committed ones. --suite=oracle: a much deeper search reads every
// position once and is stored, so --suite=A also reports agreement with it and the score given up.
if (args.FirstOrDefault(a => a.StartsWith("--suite=")) is { } suiteArg)
{
	var name = suiteArg["--suite=".Length..];
	var path = PositionSuite.FilePath;
	if (!Directory.Exists(Path.GetDirectoryName(path)))
	{
		Console.WriteLine($"Run from the solution root — no {Path.GetDirectoryName(path)} here.");
		return;
	}
	if (name == "generate")
	{
		var made = PositionSuite.Generate();
		File.WriteAllText(path, PositionSuite.ToText(made));
		Console.WriteLine(
			$"{path}: {made.Positions.Count} positions from {made.Games.Count} games, answered by {made.AnsweredBy}. Rebuild to embed it."
		);
		return;
	}
	if (name != "oracle" && !AiMatch.IsKnown(name))
	{
		Console.WriteLine(
			$"--suite=generate, --suite=oracle, or --suite=A with one of: {AiMatch.Usage}"
		);
		return;
	}
	var tracked = File.Exists(path) ? PositionSuite.Parse(File.ReadAllText(path)) : null;
	if (tracked is null)
	{
		Console.WriteLine($"No suite at {path} — {PositionSuite.GenerateCommand}");
		return;
	}
	if (name == "oracle") // run once, then stored beside the positions (AI-6.4b)
	{
		var clock = System.Diagnostics.Stopwatch.StartNew();
		File.WriteAllText(path, PositionSuite.ToText(PositionSuite.WithOracle(tracked)));
		Console.WriteLine(
			$"{path}: the oracle read {tracked.Positions.Count} positions in {clock.Elapsed.TotalMinutes:0.0} min. Rebuild to embed it."
		);
		return;
	}
	var check = PositionSuite.Run(tracked, name);
	Console.WriteLine(check.Report());
	if (flags.Contains("--accept"))
	{
		File.WriteAllText(path, PositionSuite.ToText(check.Accepted));
		Console.WriteLine(
			$"{path}: {name}'s answers are now the committed ones. Rebuild to embed it."
		);
	}
	return;
}

// --record=FILE [--passes]: replay a game record (the table writes them to game_records/, TBL-5) and
// print it, move by move. A record the engine no longer replays says which move, and exits 1.
// --record=FILE --keep=NAME --at=N: keep it as an AI test case (TBL-5.5) — copied to
// MagicCore.Tests/Records/NAME.json (run from the solution root), and a test to start from printed.
// --record=FILE --compare: your choices beside the AI's (TBL-5.6) — at each of your decisions, the
// default search's pick and where the oracle ranks yours; one line where either differs, then a summary.
if (args.FirstOrDefault(a => a.StartsWith("--record=")) is { } recordArg)
{
	try
	{
		var record = GameRecord.FromJson(File.ReadAllText(recordArg["--record=".Length..]));
		if (args.FirstOrDefault(a => a.StartsWith("--keep="))?["--keep=".Length..] is { } keep)
			Console.WriteLine(Keep(record, keep, Arg("at")));
		else if (
			args.FirstOrDefault(a => a.StartsWith("--card="))?["--card=".Length..] is { } traced
		)
			Console.WriteLine(CardTrace(record, traced));
		else if (flags.Contains("--compare"))
			Console.WriteLine(RecordComparison.Compare(record).Text());
		else if (Arg("explain") is { } explainAt)
		{
			// what the default search imagined before move N (AI-17): each first move's best line
			var at = record.Before(explainAt);
			var ai = new BeamSearchAi(at.DecidingPlayerId());
			var decided = ai.Decide(at);
			Console.WriteLine($"# Before #{explainAt}: the search plays {decided.What}");
			Console.WriteLine(ai.Explain());
		}
		else
			foreach (var line in record.Transcript(flags.Contains("--passes")))
				Console.WriteLine(line);
	}
	catch (KeepRefused e)
	{
		Console.WriteLine(e.Message);
		Environment.ExitCode = 1;
	}
	catch (MoveTokens.StaleException e)
	{
		Console.WriteLine($"Stale: {e.Message}");
		Environment.ExitCode = 1;
	}
	catch (Exception e)
		when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
	{
		Console.WriteLine($"Can't read the record: {e.Message}");
		Environment.ExitCode = 1;
	}
	return;
}

// --lint [--games=N] [--seed=N] [--ai=beam]: AI-vs-AI games with every decision checked for an obvious mistake
// and counted (BlunderLint, AI-17.7); the report to ai_reports/lint-<time>.md. --ai=beam+nolanddrop is the old build.
if (flags.Contains("--lint"))
{
	Directory.CreateDirectory("ai_reports");
	var lintPath = Path.Combine("ai_reports", $"lint-{DateTime.Now:yyyy-MM-dd_HHmmss}.md");
	var lintAi = args.FirstOrDefault(a => a.StartsWith("--ai="))?[5..] ?? "beam";
	var (lint, _) = MagicCore.Ai.BlunderLint.Run(
		Arg("games") ?? 20,
		Arg("seed") ?? 1,
		lintAi,
		Arg("only")
	);
	File.WriteAllText(lintPath, lint);
	Console.WriteLine(string.Join(" / ", lint.Split((char)10).Take(8)));
	Console.WriteLine(lintPath);
	return;
}

// --decisions [--games=N] [--seed=N]: AI-vs-AI games with every real decision written out — the
// options ranked and what each changes, term by term (DecisionReport) — to ai_reports/.
if (flags.Contains("--decisions"))
{
	Directory.CreateDirectory("ai_reports");
	var path = Path.Combine("ai_reports", $"decisions-{DateTime.Now:yyyy-MM-dd_HHmmss}.md");
	var (report, records) = MagicCore.Ai.DecisionReport.Run(Arg("games") ?? 2, Arg("seed") ?? 1);
	File.WriteAllText(path, report);
	Console.WriteLine(path);
	// each game as a record: --record=FILE numbers its moves, --keep makes a flagged one a test
	for (var g = 0; g < records.Count; g++)
	{
		var recordPath = Path.ChangeExtension(path, null) + $"-game{g + 1}.json";
		File.WriteAllText(recordPath, records[g].ToJson());
		Console.WriteLine(recordPath);
	}
	return;
}

// --card-games=planeswalkers | "Name;Name" [--set=fra] [--each=2] [--seeds=3000]: self-play games (the search on both
// sides, the decks --sim-ratings plays) in which a card was DRAWN — --each its holder lost and --each it won, per card —
// as a replay to read (ai_reports/card-games-<time>.md: each game opens with every move naming the card) and each game
// as a record beside it (on the table: -GameArgs '--resume=FILE'). Why does a card lose? (Shayne, 2026-11-07)
if (
	args.FirstOrDefault(a => a.StartsWith("--card-games="))?["--card-games=".Length..] is
	{ } cardArg
)
{
	var each = Arg("each") ?? 2;
	var printings = CardRegistry.Printings(limitedSet);
	var cards = (
		cardArg == "planeswalkers"
			? printings
				.Where(p =>
					p.Card.TypeLine.Contains("Planeswalker")
					&& CardRegistry.IsImplemented(p.Card.Name)
				)
				.Select(p => p.Card.Name)
			: cardArg.Split(';')
	)
		.Distinct()
		.ToList();
	var clock = System.Diagnostics.Stopwatch.StartNew();
	// the decks come from the gameSeed alone: find each card's games before playing any
	var seeds = Arg("seeds") ?? 3000;
	var decksOf = new MagicCore.Limited.SealedDeck[seeds + 1][];
	Parallel.For(
		1,
		seeds + 1,
		gameSeed =>
		{
			var (a, b) = MagicCore.Limited.SimulatedRatings.Decks(limitedSet, gameSeed);
			decksOf[gameSeed] = [a, b];
		}
	);
	var candidates = cards.ToDictionary(
		c => c,
		c => new Queue<int>(
			Enumerable.Range(1, seeds).Where(s => decksOf[s].Any(d => d.Main.Contains(c)))
		)
	);
	Console.WriteLine(
		$"{seeds} seeds' decks in {clock.Elapsed.TotalSeconds:0} s: "
			+ string.Join(", ", cards.Select(c => $"{c} {candidates[c].Count}"))
	);
	var kept = cards.ToDictionary(
		c => c,
		_ => new List<(bool Won, int Seat, MagicCore.Ai.DecisionReport.Played Game)>()
	);
	bool Full(string c) => kept[c].Count(k => k.Won) >= each && kept[c].Count(k => !k.Won) >= each;
	for (var round = 1; cards.Any(c => !Full(c) && candidates[c].Count > 0); round++)
	{
		// a round: a few games of every card still short, all at once, each search on one thread
		var batch = cards
			.Where(c => !Full(c))
			.SelectMany(c =>
				Enumerable
					.Range(0, 4)
					.Select(_ => candidates[c].TryDequeue(out var s) ? (c, s) : default)
			)
			.Where(x => x.c is not null)
			.ToList();
		var played = new (string Card, int Seed, MagicCore.Ai.DecisionReport.Played Game)[
			batch.Count
		];
		Parallel.For(
			0,
			batch.Count,
			i =>
			{
				var (card, gameSeed) = batch[i];
				var (a, b) = (decksOf[gameSeed][0], decksOf[gameSeed][1]);
				played[i] = (
					card,
					gameSeed,
					MagicCore.Ai.DecisionReport.Play(
						gameSeed,
						[.. a.Main.Select(n => CardRegistry.Get(n, limitedSet))],
						[.. b.Main.Select(n => CardRegistry.Get(n, limitedSet))],
						($"Seat 1 ({a.Button})", $"Seat 2 ({b.Button})"),
						$"{card}, seed {gameSeed}",
						[
							.. a
								.Sideboard.Where(n =>
									CardRegistry.IsImplemented(n)
									&& !MagicCore.Limited.SealedBuilder.Basics.Contains(n)
								)
								.Select(n => CardRegistry.Get(n, limitedSet)),
						],
						[
							.. b
								.Sideboard.Where(n =>
									CardRegistry.IsImplemented(n)
									&& !MagicCore.Limited.SealedBuilder.Basics.Contains(n)
								)
								.Select(n => CardRegistry.Get(n, limitedSet)),
						],
						threads: 1
					)
				);
			}
		);
		foreach (var (card, gameSeed, game) in played)
		{
			var end = game.End;
			var seat = decksOf[gameSeed][0].Main.Contains(card) ? 1 : 2;
			var holder = seat == 1 ? end.Player1Id() : end.Player2Id();
			var drawn = MagicCore
				.Limited.SimulatedRatings.InHand(decksOf[gameSeed][seat - 1].Main, end, holder)
				.Contains(card);
			if (!drawn || !end.IsOver() || end.WinnerId() is not (> 0 and var theWinner))
				continue; // never drawn, unfinished or a draw: nothing to learn about the card
			var won = theWinner == holder;
			if (kept[card].Count(k => k.Won == won) < each)
				kept[card].Add((won, seat, game));
		}
		Console.WriteLine(
			$"round {round}, {clock.Elapsed.TotalMinutes:0.0} min: "
				+ string.Join(
					", ",
					cards.Select(c =>
						$"{c} {kept[c].Count(k => !k.Won)}L/{kept[c].Count(k => k.Won)}W"
					)
				)
		);
	}

	Directory.CreateDirectory("ai_reports");
	var path = Path.Combine("ai_reports", $"card-games-{DateTime.Now:yyyy-MM-dd_HHmmss}.md");
	var md = new System.Text.StringBuilder(
		$"# {limitedSet} self-play games each card was drawn in\n\nThe search on both sides, the decks `--sim-ratings` "
			+ $"plays. Per card, up to {each} its holder lost, then up to {each} it won. Each game opens with every move "
			+ "naming the card; the full replay follows. Each game is also a record: `--resume=FILE` on the table.\n"
	);
	foreach (var card in cards)
	{
		md.Append($"\n# {card}\n");
		if (kept[card].Count == 0)
			md.Append($"\nNo game drew it in {seeds} seeds.\n");
		foreach (var (won, seat, game) in kept[card].OrderBy(k => k.Won))
		{
			var file =
				Path.ChangeExtension(path, null)
				+ $"-{new string(card.Where(char.IsLetterOrDigit).ToArray())}-{(won ? "won" : "lost")}-{game.Record.Seed}.json";
			File.WriteAllText(file, game.Record.ToJson());
			var mentions = game
				.Md.Split('\n')
				.Where(l => l.Contains(card) && !l.StartsWith("## "))
				.ToList();
			md.Append(
				$"\n**{(won ? "WON" : "LOST")} by Seat {seat}**, seed {game.Record.Seed} — record `{file}`\n\n"
					+ $"Every move naming {card}:\n\n"
					+ (
						mentions.Count > 0
							? string.Join(
								"\n",
								mentions.Select(l => l.StartsWith("-") ? l : $"- {l.Trim()}")
							)
							: "- none (drawn, never cast)"
					)
					+ "\n"
					+ game.Md
			);
		}
	}
	File.WriteAllText(path, md.ToString());
	Console.WriteLine($"{path}, {clock.Elapsed.TotalMinutes:0.0} min");
	return;
}

// --draft [--seed=N] [--set=TRK]: a Quick Draft with every seat a bot (LIM-2.2) — each seat's colours and its
// picks in order, to judge the bots before the table's draft screen.
if (flags.Contains("--draft"))
{
	var ratings = MagicCore.Limited.CardRatings.Of(limitedSet);
	var draftSeed = Arg("seed") ?? 1;
	var drafted = MagicCore.Limited.Draft.RunBots(
		MagicCore.Limited.Draft.New(limitedSet, draftSeed),
		ratings
	);
	Console.WriteLine(
		$"{limitedSet} Quick Draft, seed {draftSeed}: 8 bots, rating plus the colour lean"
	);
	Console.WriteLine();
	for (var i = 0; i < drafted.Seats.Count; i++)
	{
		var picks = drafted.Seats[i].Picks;
		var lane = MagicCore.Limited.Draft.Lane(picks, ratings);
		var colors = picks.Select(MagicCore.Limited.Draft.ColorsOf).ToList();
		var inside = colors.Count(c => c.Count > 0 && c.All(lane.Contains));
		var colourless = colors.Count(c => c.Count == 0);
		Console.WriteLine(
			$"Seat {i + 1}: {string.Join("/", lane)} — {inside} in its colours, {picks.Count - inside - colourless} outside, {colourless} colourless"
		);
		var packSize = picks.Count / MagicCore.Limited.Draft.PackCount;
		for (var p = 0; p < picks.Count; p++)
			Console.WriteLine(
				$"  {p / packSize + 1}.{p % packSize + 1, -3} {picks[p]} ({string.Join("", colors[p].Select(c => "WUBRGC"[(int)c]))}, {(ratings.TryGetValue(picks[p], out var r) ? $"{100 * r:0.0}" : "unrated")})"
			);
		Console.WriteLine();
	}
	return;
}

// --cast-gains [--games=N] [--set=TRK]: each instant-speed card's offers in self-play, the tracked tables AI-13's
// option value reads, one per set (all of them without --set; run from the solution root).
if (flags.Contains("--cast-gains"))
{
	if (
		args.Any(a => a.StartsWith("--set="))
		&& !MagicCore.Ai.CastGains.TableSets.Contains(limitedSet)
	)
	{
		Console.WriteLine(
			$"{limitedSet} has no cast-gains table: add it to CastGains.TableSets first"
		);
		return;
	}
	foreach (
		var set in args.Any(a => a.StartsWith("--set="))
			? [limitedSet]
			: MagicCore.Ai.CastGains.TableSets
	)
	{
		var clock = System.Diagnostics.Stopwatch.StartNew();
		var table = MagicCore.Ai.CastGains.Measure(set, Arg("games") ?? 200);
		var path = MagicCore.Ai.CastGains.TablePath(set);
		File.WriteAllText(path, MagicCore.Ai.CastGains.ToJson(table));
		Console.WriteLine($"{path}: {table.Count} cards, {clock.Elapsed.TotalSeconds:0} s");
	}
	return;
}

// --card-values: measure every FRA and TRK card and write the tracked tables (run from the solution root).
if (flags.Contains("--card-values"))
{
	var path = CardValues.TablePath;
	// The repository's Sets folder — it holds FRA.json — not any folder that happens to be called so.
	if (!File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "FRA.json")))
	{
		Console.WriteLine($"Run from the solution root — no FRA.json beside {path} here.");
		return;
	}
	foreach (var set in CardValues.TableSets)
	{
		var entries = CardValues.MeasureSet(set);
		File.WriteAllText(CardValues.TablePathFor(set), CardValues.ToJson(entries));
		Console.WriteLine(
			$"{CardValues.TablePathFor(set)}: {entries.Count} cards, {entries.Count(e => e.NotMeasured is null)} measured. Rebuild to embed it."
		);
	}
	return;
}

// --sim-ratings=FRA [--games=N] [--ai=greedy] [--first-seed=N] [--write]: DATA-2 — each card's games-in-hand win rate in
// AI-vs-AI sealed games, the tallies to sim_results/sim_ratings_<set>_<ai>[_<first seed>].json (a long beam run goes in
// chunks of seeds, each its own file — a run cut off writes nothing); --write makes them the set's ratings table
// (MagicCore/Sets/<SET>.ratings.json). Run from the solution root.
if (
	args.FirstOrDefault(a => a.StartsWith("--sim-ratings="))?["--sim-ratings=".Length..] is
	{ } simSet
)
{
	var set = simSet.ToUpperInvariant();
	var ai = args.FirstOrDefault(a => a.StartsWith("--ai="))?["--ai=".Length..] ?? "greedy";
	var games = Arg("games") ?? 2000;
	var firstSeed = Arg("first-seed") ?? 1;
	var clock = System.Diagnostics.Stopwatch.StartNew();
	var tallies = MagicCore.Limited.SimulatedRatings.Measure(
		set,
		games,
		ai,
		firstSeed,
		played: n =>
		{
			if (n % 100 == 0)
				Console.WriteLine($"{n}/{games} games, {clock.Elapsed.TotalMinutes:0.0} min");
		}
	);
	Directory.CreateDirectory("sim_results");
	var raw = Path.Combine(
		"sim_results",
		$"sim_ratings_{set.ToLowerInvariant()}_{ai}{(firstSeed == 1 ? "" : $"_{firstSeed}")}.json"
	);
	File.WriteAllText(
		raw,
		System.Text.Json.JsonSerializer.Serialize(
			tallies
				.OrderBy(t => t.Key, StringComparer.Ordinal)
				.ToDictionary(t => t.Key, t => t.Value),
			new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
		)
	);
	Console.WriteLine(
		$"{raw}: {tallies.Count} cards, {games} games ({ai}), {clock.Elapsed.TotalMinutes:0.0} min"
	);
	if (flags.Contains("--write"))
	{
		var path = Path.Combine("MagicCore", "Sets", $"{set}.ratings.json");
		File.WriteAllText(
			path,
			System.Text.Json.JsonSerializer.Serialize(
				new
				{
					source = $"SIMULATED (DATA-2) — games-in-hand win rate in {games} AI-vs-AI ({ai}) sealed games of {set}, "
						+ $"short of {MagicCore.Limited.CardRatings.MinGames} games pulled to the mean. GENERATED by MagicConsole --sim-ratings, never hand-edit",
					cards = MagicCore
						.Limited.SimulatedRatings.Rate(tallies)
						.OrderBy(r => r.Key, StringComparer.Ordinal)
						.ToDictionary(r => r.Key, r => Math.Round(r.Value, 4)),
				},
				new System.Text.Json.JsonSerializerOptions
				{
					WriteIndented = true,
					Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				}
			) + "\n"
		);
		Console.WriteLine($"{path} written. Rebuild to embed it.");
	}
	return;
}

// --interim-ratings=TRK: a set 17lands hasn't seen, rated by rarity and measured value (MC-5.4) — writes
// MagicCore/Sets/<SET>.ratings.json (run from the solution root), which CardRatings.Of reads before 17lands.
if (
	args.FirstOrDefault(a => a.StartsWith("--interim-ratings="))?["--interim-ratings=".Length..] is
	{ } ratedSet
)
{
	var set = ratedSet.ToUpperInvariant();
	var path = Path.Combine("MagicCore", "Sets", $"{set}.ratings.json");
	if (!File.Exists(Path.Combine("MagicCore", "Sets", $"{set}.json")))
	{
		Console.WriteLine($"Run from the solution root — no {set}.json in MagicCore/Sets here.");
		return;
	}
	var printings = CardRegistry
		.Printings(set)
		.Where(p => CardRegistry.IsImplemented(p.Card.Name))
		.ToList();
	var values = CardValues
		.Measure([.. printings.Select(p => p.Card.Name).Distinct().Select(CardRegistry.Get)])
		.Where(e => e.NotMeasured is null)
		.ToDictionary(e => e.Name, e => e.Value);
	var rated = MagicCore.Limited.CardRatings.Interim(
		printings,
		values,
		CardRegistry.Printings("FRA"),
		MagicCore.Limited.CardRatings.Of("FRA")
	);
	var text = System.Text.Json.JsonSerializer.Serialize(
		new
		{
			source = $"INTERIM — {set} has no 17lands data: each card at its rarity's median FRA 17lands GIH WR, "
				+ $"± {MagicCore.Limited.CardRatings.ValueSpread / 2} by its measured CardValues rank in its rarity "
				+ "(CardRatings.Interim, MC-5.4). GENERATED by MagicConsole --interim-ratings, never hand-edit",
			cards = rated
				.OrderBy(r => r.Key, StringComparer.Ordinal)
				.ToDictionary(r => r.Key, r => Math.Round(r.Value, 4)),
		},
		new System.Text.Json.JsonSerializerOptions
		{
			WriteIndented = true,
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		}
	);
	File.WriteAllText(path, text + "\n");
	Console.WriteLine($"{path}: {rated.Count} cards. Rebuild to embed it.");
	return;
}

var watch = flags.Contains("--ai-vs-ai");
var seed = Arg("seed") ?? Random.Shared.Next(1, int.MaxValue);
var decks = FraDecks.All.Keys.ToList();

var mine =
	Arg("deck") is int d && d >= 1 && d <= decks.Count ? d
	: watch ? 1
	: Pick("Your deck", decks);
var mineName = decks[mine - 1];
var theirName = decks[mine % decks.Count];

var s = MagicRules.NewGame(FraDecks.All[mineName], FraDecks.All[theirName], seed);
var (human, opponent) = (s.Player1Id(), s.Player2Id());
IAi NewAi(int player) =>
	flags.Contains("--greedy")
		? new GreedyAi(player)
		: new BeamSearchAi(player, BeamSearchAi.Settings.Interactive);
var ais = new Dictionary<int, IAi> { [opponent] = NewAi(opponent) };
if (watch)
	ais[human] = NewAi(human);

Console.WriteLine($"\n{mineName} (you) vs {theirName} (AI) — seed {seed}");
Console.WriteLine(
	$"{(s.GetGame().StartingPlayerId == human ? "You" : "The AI")} won the coin flip and play first.\n"
);

var lastHeader = "";
while (!s.IsOver())
{
	s = s.AutoPass();
	if (s.IsOver())
		break;

	var who = s.DecidingPlayerId();
	if (ais.TryGetValue(who, out var ai))
	{
		var (next, what) = ai.Decide(s);
		if (what != "Pass")
		{
			Header(s);
			Console.WriteLine($"  {(who == human ? "You" : "AI")}: {what}");
		}
		s = next;
		continue;
	}

	Header(s);
	Board(s);
	if (s.GetPendingChoice() is { } choice)
		s = s.Choose(AskChoice(s, choice));
	else
	{
		var actions = s.DistinctMoves(s.LegalActions());
		var labels = actions.Select(a => Describe.Action(s, a)).ToList();
		s = s.Act(
			actions[
				Pick("Your move (Enter = Pass)", labels, defaultIndex: labels.IndexOf("Pass") + 1)
					- 1
			]
		);
	}
}

var winner = s.WinnerId();
Console.WriteLine(
	winner == 0
		? "\nDraw."
		: $"\n{(winner == human ? "You win" : "The AI wins")} on turn {s.GetGame().TurnNumber}. (seed {seed})"
);
return;

void Header(GameState s)
{
	var game = s.GetGame();
	var header =
		$"── Turn {game.TurnNumber} ({(game.ActivePlayerId == human ? "yours" : "AI's")}) · {game.Step} ──";
	if (header == lastHeader)
		return;
	lastHeader = header;
	Console.WriteLine(header);
}

void Board(GameState s)
{
	string Zone(int p, ZoneType z) =>
		string.Join(" | ", s.CardsIn(p, z).Select(c => Describe.Card(s, c)));
	var (you, them) = (s.GetPlayer(human), s.GetPlayer(opponent));
	Console.WriteLine(
		$"  AI   life {them.Life} · hand {s.CardsIn(opponent, ZoneType.Hand).Count()} · library {s.CardsIn(opponent, ZoneType.Library).Count()}"
	);
	Console.WriteLine($"       {Zone(opponent, ZoneType.Battlefield)}");
	var stack = s.GetChildren(s.GetWellKnownId(MagicKeys.Stack)).Select(o => o.Name).ToList();
	if (stack.Count > 0)
		Console.WriteLine($"  STACK (top first): {string.Join(" ← ", stack)}");
	Console.WriteLine($"       {Zone(human, ZoneType.Battlefield)}");
	var restricted = you.ManaPool.Restricted.Count;
	var pool =
		you.ManaPool.Total + restricted > 0
			? $" · floating {you.ManaPool.Total}{(restricted > 0 ? $" (+{restricted} restricted)" : "")}"
			: "";
	Console.WriteLine(
		$"  You  life {you.Life} · library {s.CardsIn(human, ZoneType.Library).Count()}{pool}"
	);
	Console.WriteLine($"  Hand {Zone(human, ZoneType.Hand)}");
}

IEnumerable<int> AskChoice(GameState s, ChoiceAction choice)
{
	var labels = choice.Options.Select(o => o.DisplayText).ToList();
	if (choice.MaxChoices == 1)
	{
		var question = Describe.Question(s, choice);
		if (choice.MinChoices == 0)
		{
			var pick = Pick(question, ["(none)", .. labels]);
			return pick == 1 ? [] : [choice.Options[pick - 2].Id];
		}
		return [choice.Options[Pick(question, labels) - 1].Id];
	}

	while (true)
	{
		var range =
			choice.MinChoices == choice.MaxChoices
				? $"{choice.MinChoices}"
				: $"{choice.MinChoices} to {choice.MaxChoices}";
		Console.WriteLine(
			$"{Describe.Question(s, choice)} — pick {range}, comma-separated (Enter for none):"
		);
		for (var i = 0; i < labels.Count; i++)
			Console.WriteLine($"  {i + 1}. {labels[i]}");
		var picks = (Read() ?? "")
			.Split(',', StringSplitOptions.RemoveEmptyEntries)
			.Select(p => int.TryParse(p.Trim(), out var n) ? n - 1 : -1)
			.Distinct()
			.ToList();
		var countOk = picks.Count >= choice.MinChoices && picks.Count <= choice.MaxChoices;
		if (countOk && picks.All(i => i >= 0 && i < labels.Count))
			return picks.Select(i => choice.Options[i].Id);
	}
}

int Pick(string question, IReadOnlyList<string> labels, int defaultIndex = 0)
{
	while (true)
	{
		Console.WriteLine(question);
		for (var i = 0; i < labels.Count; i++)
			Console.WriteLine($"  {i + 1}. {labels[i]}");
		var line = Read();
		if (string.IsNullOrWhiteSpace(line) && defaultIndex > 0)
			return defaultIndex;
		if (int.TryParse(line, out var n) && n >= 1 && n <= labels.Count)
			return n;
	}
}

static string? Read()
{
	Console.Write("> ");
	var line = Console.ReadLine();
	if (line is null || line.Trim() == "q")
		Environment.Exit(0);
	return line;
}

// --record=FILE --card=NAME: why a card in hand wasn't cast (MC-5.4, Shayne: "treated as dead cards?") — each turn its
// holder had it in hand, at their first main-phase decision: their lands (untapped / all), whether casting it was a
// legal move then, and what they did; and every later decision of that turn where it could be cast and wasn't.
string CardTrace(GameRecord record, string card)
{
	var (before, _) = record.Replay();
	var lines = new List<string>
	{
		$"{card} in {record.Players[0]} vs {record.Players[1]}, seed {record.Seed}:",
	};
	var seen = new HashSet<int>();
	for (var i = 0; i < before.Count; i++)
	{
		var s = before[i];
		var holder = record.Moves[i].By == 1 ? s.Player1Id() : s.Player2Id();
		var inHand = s.CardsIn(holder, ZoneType.Hand).FirstOrDefault(c => c.Name == card);
		var game = s.GetGame();
		if (
			inHand is null
			|| s.GetPendingChoice() is not null
			|| game.ActivePlayerId != holder
			|| game.Step is not (Step.PreCombatMain or Step.PostCombatMain)
		)
			continue;
		var castable = s.LegalActions().Any(a => a is CastSpellAction c && c.CardId == inHand.Id);
		var first = seen.Add(game.TurnNumber);
		if (!first && !castable)
			continue;
		var lands = s.CardsIn(holder, ZoneType.Battlefield)
			.Where(c => c.Is(CardType.Land))
			.ToList();
		var did = MagicCore.Ai.MoveTokens.Describe(s, record.Moves[i].Token);
		lines.Add(
			$"  #{i + 1} turn {game.TurnNumber} {game.Step}: {lands.Count(l => !l.IsTapped)}/{lands.Count} lands untapped "
				+ $"({string.Join(", ", lands.GroupBy(l => l.Name).Select(g => $"{g.Count()} {g.Key}"))}) — "
				+ $"{(castable ? "CASTABLE" : "not castable")}; did: {did}"
		);
	}
	// described from the state, not the record's Said (empty for an AI's moves); numbered from 1, as the transcript
	var cast = Enumerable
		.Range(0, before.Count)
		.Where(i =>
			MagicCore
				.Ai.MoveTokens.Describe(before[i], record.Moves[i].Token)
				.StartsWith($"Cast {card}")
		)
		.ToList();
	lines.Add(
		cast.Count > 0
			? $"Cast at move {string.Join(", ", cast.Select(i => $"#{i + 1}"))}."
			: "Never cast."
	);
	return string.Join("\n", lines);
}

// Copies the record into the tests and returns a test to start from: the position, what was played
// there, and what the AI that made it plays now — the CONTROL every AiPositionTests case has.
string Keep(GameRecord record, string name, int? at)
{
	if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_]*$"))
		throw new KeepRefused(
			$"--keep=NAME: a letter, then letters, digits or _ (it names the test too), not '{name}'"
		);
	if (at is not { } n || n < 1 || n > record.Moves.Count)
		throw new KeepRefused(
			$"--keep needs --at=N, a move from 1 to {record.Moves.Count} (the transcript's #N)"
		);
	var move = record.Moves[n - 1];
	if (move.Kind is not ("you" or "ai" or "auto"))
		throw new KeepRefused(
			$"move #{n} is a {move.Kind} move, not a decision — keep a you, ai or auto move"
		);
	var dir = Path.Combine("MagicCore.Tests", "Records");
	if (!Directory.Exists("MagicCore.Tests"))
		throw new KeepRefused("Run from the solution root — no MagicCore.Tests here.");
	var path = Path.Combine(dir, $"{name}.json");
	if (File.Exists(path))
		throw new KeepRefused($"{path} exists — pick another name.");

	var s = record.Before(n);
	var played = MoveTokens.Describe(s, move.Token);
	// The AI that made the play, as a test can run it: the stand-in (auto) and a greedy table are
	// GreedyAi; the table's search is capped at 1 s, which no test repeats, so here it is the default
	// search. A human's move is set beside the search's.
	var byAi = move.Kind != "you";
	var greedy =
		move.Kind == "auto"
		|| byAi && record.About.GetValueOrDefault($"Player{move.By}") == nameof(GreedyAi);
	var (ai, aiCode) = greedy
		? ((IAi)new GreedyAi(s.DecidingPlayerId()), "new GreedyAi(s.DecidingPlayerId())")
		: (new BeamSearchAi(s.DecidingPlayerId()), "new BeamSearchAi(s.DecidingPlayerId())");
	var now = MoveTokens.Token(s, ai.Decide(s));
	var nowSays = MoveTokens.Describe(s, now);
	var who = greedy ? "GreedyAi" : "the search";
	var control =
		!byAi
			? $"What {who} plays here now ({nowSays}) — a human's move; assert the play it SHOULD make."
		: MoveTokens.Same(now, move.Token)
			? $"The control: {who} still makes the recorded play ({nowSays})."
		: $"NOT A CONTROL: {who} now plays {nowSays}, not the recorded play — find a position that still provokes it.";
	Directory.CreateDirectory(dir);
	File.WriteAllText(path, record.ToJson());
	return $$"""
		{{path}}: kept. A test to start from, for MagicCore.Tests/Ai/AiPositionTests.cs:

			// TODO who reported it, when, and what looked wrong. Kept table game "{{name}}", move #{{n}}
			// (turn {{s.GetGame().TurnNumber}}): {{record.Players[
			move.By - 1
		]}} [{{move.Kind}}] {{played}}.
			[Test]
			public void {{name}}_TODO()
			{
				var s = KeptRecords.Before("{{name}}", {{n}});
				var choice = MoveTokens.Token(s, {{aiCode}}.Decide(s));

				// {{control}}
				Assert.That(MoveTokens.Same(choice, "{{now}}"));
			}
		""";
}

/// <summary>A --keep the console won't do, and why.</summary>
sealed class KeepRefused(string message) : Exception(message);
