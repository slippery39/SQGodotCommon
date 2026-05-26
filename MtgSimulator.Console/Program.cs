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
Console.Write("Mode (default 1): ");
var modeInput = Console.ReadLine()?.Trim() ?? "";
var mode = modeInput == "2" ? 2 : 1;
Console.WriteLine();

Console.Write("AI depth? (default 3): ");
var depthInput = Console.ReadLine()?.Trim() ?? "";
var aiDepth = int.TryParse(depthInput, out var d) && d > 0 ? d : 3;

if (mode == 1)
{
	Console.Write("How many games to simulate? (default 1000): ");
	var gameCountInput = Console.ReadLine()?.Trim() ?? "";
	var gameCount = int.TryParse(gameCountInput, out var g) && g > 0 ? g : 1000;

	Console.Write("Benchmark seed? (blank = random, number or word = fixed/reproducible): ");
	var seedInput = Console.ReadLine()?.Trim() ?? "";
	int? seed;
	if (string.IsNullOrEmpty(seedInput))
	{
		seed = null;
	}
	else if (int.TryParse(seedInput, out var parsedSeed))
	{
		seed = parsedSeed;
	}
	else
	{
		seed = StringToSeed(seedInput);
		Console.WriteLine($"  Seed for \"{seedInput}\": {seed}");
	}

	new SimulatorRunner(gameCount, aiDepth, seed).Run();
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
