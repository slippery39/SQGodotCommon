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
//   dotnet run --project MagicConsole -- --card-values   regenerate MagicCore/Sets/FRA.values.json
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
				? MagicCore.Limited.Boosters.SealedPool("FRA", poolSeed)
				: throw new ArgumentException($"--compare={compareArg}: the seed isn't a number"),
			_ => MagicCore.Sets.Fra.ArenaDeck.ReadPool(File.ReadAllText(compareArg)).Pool,
		};
		if (pool is { Count: 0 })
			throw new ArgumentException($"{compareArg}: no FRA cards in it");
		var builds = (
			pool is null
				? []
				: MagicCore.Limited.SealedBuildMatch.FromSuggestions(
					MagicCore.Limited.SealedBuilder.AllBuilds(
						pool,
						MagicCore.Limited.CardRatings.Of("FRA")
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
		Console.WriteLine(MagicCore.Limited.SealedBuildMatch.Run(builds, games).Report());
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

// --draft [--seed=N]: an FRA Quick Draft with every seat a bot (LIM-2.2) — each seat's colours and its
// picks in order, to judge the bots before the table's draft screen.
if (flags.Contains("--draft"))
{
	var ratings = MagicCore.Limited.CardRatings.Of("FRA");
	var draftSeed = Arg("seed") ?? 1;
	var drafted = MagicCore.Limited.Draft.RunBots(
		MagicCore.Limited.Draft.New("FRA", draftSeed),
		ratings
	);
	Console.WriteLine($"FRA Quick Draft, seed {draftSeed}: 8 bots, rating plus the colour lean");
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

// --cast-gains [--games=N]: each instant-speed card's offers in self-play, the tracked table AI-13's
// option value reads (run from the solution root).
if (flags.Contains("--cast-gains"))
{
	var clock = System.Diagnostics.Stopwatch.StartNew();
	var table = MagicCore.Ai.CastGains.Measure(Arg("games") ?? 200);
	File.WriteAllText(MagicCore.Ai.CastGains.TablePath, MagicCore.Ai.CastGains.ToJson(table));
	Console.WriteLine(
		$"{MagicCore.Ai.CastGains.TablePath}: {table.Count} cards, {clock.Elapsed.TotalSeconds:0} s"
	);
	return;
}

// --card-values: measure every FRA card and write the tracked table (run from the solution root).
if (flags.Contains("--card-values"))
{
	var path = CardValues.TablePath;
	// The repository's Sets folder — it holds FRA.json — not any folder that happens to be called so.
	if (!File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "FRA.json")))
	{
		Console.WriteLine($"Run from the solution root — no FRA.json beside {path} here.");
		return;
	}
	var entries = CardValues.MeasureFra();
	File.WriteAllText(path, CardValues.ToJson(entries));
	Console.WriteLine(
		$"{path}: {entries.Count} cards, {entries.Count(e => e.NotMeasured is null)} measured. Rebuild to embed it."
	);
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
