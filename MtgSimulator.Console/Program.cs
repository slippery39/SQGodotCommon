using System.Diagnostics;
using MtgSimulator;

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
Console.Write("Mode (default 1): ");
var modeInput = Console.ReadLine()?.Trim() ?? "";
var mode = modeInput is "2" or "3" or "4" ? int.Parse(modeInput) : 1;
Console.WriteLine();

Console.Write("AI depth? (default 3): ");
var depthInput = Console.ReadLine()?.Trim() ?? "";
var aiDepth = int.TryParse(depthInput, out var d) && d > 0 ? d : 3;

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

	var trained = DraftTrainingStore.Load();
	if (trained is null)
		Console.WriteLine(
			$"  No training data at {DraftTrainingStore.DefaultPath} — comparing Curve vs Random."
		);
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
		synergyWeight: synergyWeight
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

	var accumulate = false;
	var existing = DraftTrainingStore.Load();
	if (generations == 1 && existing is not null)
	{
		Console.WriteLine(
			$"  Existing data: {existing.Cards.Count} cards, {existing.Pairs.Count} pairs "
				+ $"from {existing.Perspectives} deck-games."
		);
		Console.Write("Merge into it rather than replace? (Y/n): ");
		accumulate = Console.ReadLine()?.Trim().ToLowerInvariant() != "n";
	}

	var seed = ReadSeed() ?? new Random().Next();
	// Generation 1 bootstraps from whatever is on disk (nothing, on a fresh run).
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
			bootstrap: model
		).Run();

		// Overwrite by default: the model should describe the CURRENT drafting policy.
		model = accumulate && model is not null ? DraftTrainingData.Merge(model, fresh) : fresh;
		DraftTrainingStore.Save(model);
		Console.WriteLine(
			$"  Saved to {DraftTrainingStore.DefaultPath} — {model.Cards.Count} cards, "
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
					trained: model
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
else
{
	Console.Write("N (games per side per matchup, default 10): ");
	var nInput = Console.ReadLine()?.Trim() ?? "";
	var n = int.TryParse(nInput, out var nVal) && nVal > 0 ? nVal : 10;

	new PreconstructedSimulatorRunner(n, aiDepth).Run();
}

Console.WriteLine("Done. Press any key to exit.");
Console.ReadKey(intercept: true);

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
