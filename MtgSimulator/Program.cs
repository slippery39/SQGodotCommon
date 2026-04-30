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

	new SimulatorRunner(gameCount, aiDepth).Run();
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
