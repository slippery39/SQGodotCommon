using System.Diagnostics;
using MtgCore;
using MtgSimulator;
using MtgSimulator.Scenarios;

Console.WriteLine("MTG Simulator");
Console.WriteLine($"  PID: {Process.GetCurrentProcess().Id}");
Console.WriteLine(
	$"To profile with dotTrace, run this command dotnet-trace collect --process-id {Process.GetCurrentProcess().Id} --output simulator.nettrace --duration 00:01:00"
);
Console.WriteLine("Then run dotnet-trace convert simulator.nettrace --format Speedscope ");
Console.WriteLine();

Console.WriteLine("Select mode:");
Console.WriteLine("  1 - Random Card Pool");
Console.WriteLine("  2 - Preconstructed Decks");
Console.WriteLine("  3 - Draft");
Console.WriteLine("  4 - Train draft pickers");
Console.WriteLine("  5 - Inspect a saved scenario");
Console.WriteLine("  6 - Evolve a constructed metagame");
Console.WriteLine("  7 - Discover synergy engines in a pool (solitaire only, no battles)");
Console.Write("Mode (default 1): ");
var modeInput = Console.ReadLine()?.Trim() ?? "";
var mode = modeInput is "2" or "3" or "4" or "5" or "6" or "7" ? int.Parse(modeInput) : 1;
Console.WriteLine();

if (mode == 5)
{
	ScenarioConsole.Run();
	return;
}

// Depth 2 rather than 3: this mode plays far more games than any other, and every one of them
// runs two AIs of equal strength, which is what the measurement requires — not that they be
// strong. Raise it if a deck's line looks unplayed rather than unplayable.
Console.Write(mode is 6 or 7 ? "AI depth? (default 2): " : "AI depth? (default 3): ");
var depthInput = Console.ReadLine()?.Trim() ?? "";
var aiDepth =
	int.TryParse(depthInput, out var d) && d > 0 ? d
	: mode is 6 or 7 ? 2
	: 3;

if (mode == 1)
{
	Console.Write("How many games to simulate? (default 1000): ");
	var gameCountInput = Console.ReadLine()?.Trim() ?? "";
	var gameCount = int.TryParse(gameCountInput, out var g) && g > 0 ? g : 1000;

	new SimulatorRunner(gameCount, aiDepth, ReadSeed()).Run();
}
else if (mode == 3)
{
	Console.Write(
		"Format? 1 = Booster (15-card packs x3), 2 = Digital (pick 1 of 3) (default 1): "
	);
	var format = Console.ReadLine()?.Trim() == "2" ? DraftFormat.Digital : DraftFormat.Booster;

	Console.Write("How many seats? (default 8): ");
	var seatInput = Console.ReadLine()?.Trim() ?? "";
	var seats = int.TryParse(seatInput, out var s) && s >= 2 ? s : 8;

	var set = ReadSet();
	var modelPath = DraftTrainingStore.PathFor(set.Code);

	var trained = DraftTrainingStore.Load(modelPath);
	if (trained is null)
		Console.WriteLine($"  No training data at {modelPath} — comparing Curve vs Random.");
	else
		Console.WriteLine(
			$"  Loaded training data: {trained.Cards.Count} cards, {trained.Pairs.Count} pairs "
				+ $"from {trained.Perspectives} deck-games."
		);

	// 1.0 is modelled-correct; see the Draft Training section of MtgSimulator/CLAUDE.md.
	var synergyWeight = 0.0;
	if (trained is not null)
	{
		Console.Write("Synergy weight? (default 0; measured to cost win rate above 0): ");
		if (double.TryParse(Console.ReadLine()?.Trim(), out var sw) && sw >= 0)
			synergyWeight = sw;
	}

	new DraftRunner(
		format,
		seats,
		ReadSeed() ?? new Random().Next(),
		aiDepth,
		trained: trained,
		synergyWeight: synergyWeight,
		set: set
	).Run();
}
else if (mode == 4)
{
	Console.Write("Format? 1 = Booster, 2 = Digital (default 1): ");
	var format = Console.ReadLine()?.Trim() == "2" ? DraftFormat.Digital : DraftFormat.Booster;

	Console.Write("How many drafts to simulate? (default 100): ");
	var draftInput = Console.ReadLine()?.Trim() ?? "";
	var drafts = int.TryParse(draftInput, out var dr) && dr > 0 ? dr : 100;

	Console.Write("How many seats per draft? (default 8): ");
	var trainSeatInput = Console.ReadLine()?.Trim() ?? "";
	var trainSeats = int.TryParse(trainSeatInput, out var ts) && ts >= 2 ? ts : 8;

	Console.Write("How many generations? (default 1; >1 retrains on its own output): ");
	var genInput = Console.ReadLine()?.Trim() ?? "";
	var generations = int.TryParse(genInput, out var gv) && gv > 0 ? gv : 1;

	var trainSet = ReadSet();
	var trainModelPath = DraftTrainingStore.PathFor(trainSet.Code);

	var accumulate = false;
	var existing = DraftTrainingStore.Load(trainModelPath);
	if (existing is not null)
	{
		Console.WriteLine(
			$"  Existing data: {existing.Cards.Count} cards, {existing.Pairs.Count} pairs "
				+ $"from {existing.Perspectives} deck-games."
		);

		// Bootstrapping and merging are SEPARATE decisions, and conflating them is a trap:
		// "replace" only governs the output file, while bootstrapping governs which cards get
		// drafted — and therefore which cards get measured at all. Train from scratch whenever
		// the existing model's card values are stale: new cards were added (they score the
		// prior, get passed over every pick, and never accumulate data) or the rules changed
		// underneath it (the values describe a game that no longer exists).
		Console.Write("Draft with the existing model? (Y/n — n trains from scratch): ");
		if (Console.ReadLine()?.Trim().ToLowerInvariant() == "n")
		{
			existing = null;
			Console.WriteLine("  Training from scratch with Curve/Random drafters.");
		}

		if (generations == 1 && existing is not null)
		{
			Console.Write("Merge into it rather than replace? (Y/n): ");
			accumulate = Console.ReadLine()?.Trim().ToLowerInvariant() != "n";
		}
	}

	var seed = ReadSeed() ?? new Random().Next();
	// Generation 1 bootstraps from whatever is on disk, unless the user opted out above.
	var model = existing;
	var history = new List<(int Gen, double Trained, double Curve, double Random, double Spread)>();

	for (var gen = 1; gen <= generations; gen++)
	{
		if (generations > 1)
		{
			Console.WriteLine();
			Console.WriteLine($"===== Generation {gen} / {generations} =====");
		}

		var fresh = new DraftTrainer(
			format,
			drafts,
			trainSeats,
			seed + gen * 7_000_000,
			aiDepth,
			bootstrap: model,
			set: trainSet
		).Run();

		// Overwrite by default: the model should describe the CURRENT drafting policy.
		model = accumulate && model is not null ? DraftTrainingData.Merge(model, fresh) : fresh;
		DraftTrainingStore.Save(model, trainModelPath);
		Console.WriteLine(
			$"  Saved to {trainModelPath} — {model.Cards.Count} cards, "
				+ $"{model.Pairs.Count} pairs from {model.Perspectives} deck-games."
		);

		if (generations > 1)
		{
			// Fixed seats and seeds every generation, so the only thing that changes is the
			// model — otherwise the trend measures draft luck instead of learning.
			var totals = new Dictionary<string, (int Wins, int Played)>();
			foreach (var evalSeed in new[] { 111, 222, 333 })
			{
				var run = new DraftRunner(
					format,
					seatCount: 9,
					seed: evalSeed,
					aiDepth: aiDepth,
					trained: model,
					set: trainSet
				).Run(verbose: false);
				foreach (var (picker, r) in run)
				{
					var t = totals.GetValueOrDefault(picker);
					totals[picker] = (t.Wins + r.Wins, t.Played + r.Played);
				}
			}

			double Rate(string p) =>
				totals.TryGetValue(p, out var r) && r.Played > 0 ? 100.0 * r.Wins / r.Played : 0;

			var spread = model.CardRateSpread();
			history.Add((gen, Rate("Trained"), Rate("Curve"), Rate("Random"), spread));
			Console.WriteLine(
				$"  Eval: Trained {Rate("Trained"):F1}%  Curve {Rate("Curve"):F1}%  "
					+ $"Random {Rate("Random"):F1}%  ({totals["Trained"].Played} games)  "
					+ $"card-rate spread {spread:F2}pp"
			);
		}
	}

	if (history.Count > 0)
	{
		Console.WriteLine();
		Console.WriteLine("  --- Generational Trend ---");
		Console.WriteLine();
		Console.WriteLine("  Gen   Trained   Curve    Random   CardSpread");
		foreach (var h in history)
			Console.WriteLine(
				$"  {h.Gen, -6}{h.Trained, -10:F1}{h.Curve, -9:F1}{h.Random, -9:F1}"
					+ $"{h.Spread:F2}pp"
			);
		Console.WriteLine();
		Console.WriteLine(
			"  CardSpread should stay roughly flat. Rising = card rates are absorbing the "
				+ "strength of whoever drafted them; falling to ~0 = decks have converged."
		);
		Console.WriteLine();
	}
}
else if (mode == 6)
{
	var evolveSet = ReadSet(includeCombined: true);

	Console.Write("How many decks in the metagame? (default 8): ");
	var deckInput = Console.ReadLine()?.Trim() ?? "";
	var deckCount = int.TryParse(deckInput, out var dc) && dc >= 2 ? dc : 8;

	Console.Write("How many generations? (default 30): ");
	var evolveGenInput = Console.ReadLine()?.Trim() ?? "";
	var evolveGens = int.TryParse(evolveGenInput, out var eg) && eg > 0 ? eg : 30;

	Console.Write("How many mutants per deck per generation? (default 3): ");
	var mutantInput = Console.ReadLine()?.Trim() ?? "";
	var mutants = int.TryParse(mutantInput, out var mu) && mu > 0 ? mu : 3;

	Console.Write("Games per matchup while evolving? (default 6): ");
	var gpmInput = Console.ReadLine()?.Trim() ?? "";
	var gamesPerMatchup = int.TryParse(gpmInput, out var gpm) && gpm > 0 ? gpm : 6;

	Console.Write("Games per matchup in the final round-robin? (default 20): ");
	var finalInput = Console.ReadLine()?.Trim() ?? "";
	var finalGames = int.TryParse(finalInput, out var fg) && fg > 0 ? fg : 20;

	// Raising this is the lever against a field that converges on one concentrated pool of
	// cards. It costs accepted mutations — a mutant that improves but drifts toward another
	// deck is rejected — so expect slower climbing on a small pool.
	Console.Write("Minimum deck difference? (default 0.35, e.g. 0.6 for a wider field): ");
	var diffInput = Console.ReadLine()?.Trim() ?? "";
	var minDifference = double.TryParse(diffInput, out var md) && md > 0 && md < 1 ? md : 0.35;

	// Measures the whole pool with uniformly-random decks before evolution, so a card's starting
	// value does not depend on whether it happened to be picked up early. Without it, a card
	// needs data to get into a deck and needs to be in a deck to get data.
	Console.Write("Pre-simulation decks? (default 300, 0 = skip): ");
	var presimInput = Console.ReadLine()?.Trim() ?? "";
	var presimDecks = int.TryParse(presimInput, out var pd) && pd >= 0 ? pd : 300;

	// Culling resets that slot's DeckHistory, which is now the main improvement mechanism — so
	// a culled deck restarts not just bad but BLIND. Measured over 100 generations: the two
	// slots culled once reached age 78/95 and finished best, while the slots culled 10 and 13
	// times never recovered.
	Console.Write("Cull non-viable decks? (Y/n — n lets every deck keep brewing): ");
	var cullDecks = Console.ReadLine()?.Trim().ToLowerInvariant() != "n";

	// The control arm for "is this just building draft decks". With no prior every card scores
	// exactly average, so seeding is quality-blind and the constructed table builds from
	// nothing — slower, but it cannot inherit a limited valuation it never read.
	Console.Write("Seed from the draft model? (Y/n — n seeds quality-blind): ");
	var useDraftPrior = Console.ReadLine()?.Trim().ToLowerInvariant() != "n";

	// Slots seeded by COMMITTING to one mechanical concept and jamming it, instead of by
	// anchor-and-kernel. 0 keeps the mode exactly as it was, which is the A/B control: the
	// question these answer is whether committed seeding builds archetypes a hill climb cannot
	// reach, and that is only readable against a run without them.
	Console.Write("Synergy (concept) deck slots? (default 0 = off, e.g. 3 of 8): ");
	var conceptInput = Console.ReadLine()?.Trim() ?? "";
	var conceptSlots = int.TryParse(conceptInput, out var cs) && cs >= 0 ? cs : 0;

	// Fixed reference decks, counted in fitness but NOT in the diversity constraint. Without one
	// the mode has no absolute reference: a closed round-robin averages 50% by construction, so a
	// field that converges on something mediocre reports itself perfectly healthy. Measured: the
	// evolved ALL field lost to hand-built Zoo 34-66. 0 keeps the old behaviour exactly.
	Console.Write("Gauntlet games per reference deck? (default 0 = off, e.g. 4): ");
	var gauntletInput = Console.ReadLine()?.Trim() ?? "";
	var gauntletGames = int.TryParse(gauntletInput, out var gg) && gg >= 0 ? gg : 0;

	// Phase two. Seeds the top-LIFT archetypes from a mode 7 run into the field and holds each to
	// its card POOL — not to a decklist, so the deck can still pick up removal and metagame
	// answers without dissolving into the midrange pile every unconstrained run converges on.
	// Engine slots are never culled: mode 7 already judged them on whether they ASSEMBLE, and a
	// win-rate floor would delete exactly the decks this exists to keep.
	Console.Write("Engine file from mode 7? (blank = none, e.g. sim_results/engines_all_*.json): ");
	var enginesPath = Console.ReadLine()?.Trim();
	if (!string.IsNullOrWhiteSpace(enginesPath) && !File.Exists(enginesPath))
	{
		Console.WriteLine($"  WARNING: {enginesPath} does not exist — running without engines.");
		enginesPath = null;
	}

	// How many slots the engine report fills. The REST become curve-profile decks — Aggro,
	// Midrange, Control — which are good-stuff piles by design and are the control the themed
	// slots are read against: if a curve pile ends up looking like the themed decks, the themes
	// were never identities. Blank keeps the old behaviour (every slot but the wildcard).
	var engineSlots = -1;
	if (!string.IsNullOrWhiteSpace(enginesPath))
	{
		Console.Write(
			$"How many of the {deckCount} slots are engines? "
				+ "(blank = all but a wildcard; the rest become Aggro/Midrange/Control): "
		);
		if (int.TryParse(Console.ReadLine()?.Trim(), out var asked))
			engineSlots = Math.Clamp(asked, 0, deckCount);
	}

	new MetagameEvolver(
		evolveSet,
		deckCount: deckCount,
		generations: evolveGens,
		mutantsPerDeck: mutants,
		gamesPerMatchup: gamesPerMatchup,
		finalGamesPerMatchup: finalGames,
		seed: ReadSeed() ?? new Random().Next(),
		aiDepth: aiDepth,
		minDifference: minDifference,
		useDraftPrior: useDraftPrior,
		// The floor stays 0.40 either way — with culling off it still flags a non-viable deck
		// in the report, it just stops replacing it.
		cullEnabled: cullDecks,
		preSimDecks: presimDecks,
		conceptSlots: conceptSlots,
		gauntletGames: gauntletGames,
		enginesPath: string.IsNullOrWhiteSpace(enginesPath) ? null : enginesPath,
		engineSlots: engineSlots
	).Run();
}
else if (mode == 7)
{
	// Phase one of the synergy work: which engines does this pool support, and do they assemble?
	// No battles at all — a half-built combo deck loses every game, so a win rate cannot answer
	// this question and asking it anyway is what makes mode 6 converge on midrange piles.
	var engineSet = ReadSet(includeCombined: true);

	Console.Write("Solitaire games per engine? (default 10): ");
	var engineGamesInput = Console.ReadLine()?.Trim() ?? "";
	var engineGames = int.TryParse(engineGamesInput, out var eg) && eg > 0 ? eg : 10;

	// Ranking cutoff for the report only. Every viable concept is probed either way — this is
	// how many get their card lists printed and nothing more.
	Console.Write("How many engines to highlight? (default 8): ");
	var keepInput = Console.ReadLine()?.Trim() ?? "";
	var engineKeep = int.TryParse(keepInput, out var ek) && ek > 0 ? ek : 8;

	// Storm runs 12 lands, Zoo and Affinity 14. The 20 floor makes every one of them illegal,
	// so an engine probed at the default is being asked to assemble out of a deck it cannot be.
	if (Decklist.MinLands > 14)
		Console.WriteLine(
			$"  NOTE  MinLands is {Decklist.MinLands}. Set MTG_MIN_LANDS=12 — the hand-built "
				+ "combo decks all run below this floor and cannot be reproduced above it."
		);

	var report = EngineDiscovery.Run(
		engineSet,
		gamesPerEngine: engineGames,
		seed: ReadSeed() ?? new Random().Next(),
		aiDepth: aiDepth
	);

	EngineDiscovery.Print(report, engineKeep);

	var enginePath = EngineReportStore.PathFor(engineSet.Code);
	EngineReportStore.Save(report, enginePath);

	// Read it straight back. This file exists to be loaded by the evolver later, and a
	// `Decklist`'s `ImmutableSortedDictionary` is exactly the shape that round-trips fine until
	// it does not — better to fail here than in a run that has already spent an hour.
	var reloaded = EngineReportStore.Load(enginePath);
	Console.WriteLine(
		reloaded?.Engines.Count == report.Engines.Count
			? $"Saved {report.Engines.Count} engines to {enginePath}"
			: $"  WARNING  {enginePath} did not round-trip — the evolver will not be able to read it"
	);
}
else
{
	Console.Write("N (games per side per matchup, default 10): ");
	var nInput = Console.ReadLine()?.Trim() ?? "";
	var n = int.TryParse(nInput, out var nVal) && nVal > 0 ? nVal : 10;

	new PreconstructedSimulatorRunner(n, aiDepth).Run();
}

Console.WriteLine("Done. Press any key to exit.");
Console.ReadKey(intercept: true);

// Which set to draft, train or build on. Skips the prompt entirely while only one choice exists.
//
// includeCombined offers the union of every set as one pool. It is deliberately NOT offered to
// draft or training, where the model is the pick policy and a merged pool would score two thirds
// of the cards at exactly the prior — see SetRegistry.Combined.
static CardSet ReadSet(bool includeCombined = false)
{
	var choices = includeCombined ? SetRegistry.AllIncludingCombined() : SetRegistry.All;
	if (choices.Count == 1)
		return choices[0];

	Console.WriteLine("Which set?");
	for (var i = 0; i < choices.Count; i++)
		Console.WriteLine($"  {i + 1} = {choices[i]}");
	Console.Write($"Choice (default 1): ");

	var input = Console.ReadLine()?.Trim() ?? "";
	var index = int.TryParse(input, out var v) && v >= 1 && v <= choices.Count ? v - 1 : 0;
	return choices[index];
}

// Blank = random (null); a number is used as-is; a word is hashed so it is reproducible.
static int? ReadSeed()
{
	Console.Write("Benchmark seed? (blank = random, number or word = fixed/reproducible): ");
	var seedInput = Console.ReadLine()?.Trim() ?? "";
	if (string.IsNullOrEmpty(seedInput))
		return null;
	if (int.TryParse(seedInput, out var parsedSeed))
		return parsedSeed;

	var hashed = StringToSeed(seedInput);
	Console.WriteLine($"  Seed for \"{seedInput}\": {hashed}");
	return hashed;
}

// FNV-1a hash — stable across runs and platforms, unlike string.GetHashCode().
static int StringToSeed(string s)
{
	uint hash = 2166136261u;
	foreach (var c in s)
	{
		hash ^= (byte)c;
		hash *= 16777619u;
	}
	return (int)hash;
}
