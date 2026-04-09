using System.Diagnostics;
using MtgSimulator;

Console.WriteLine("MTG Simulator");
Console.WriteLine($"  PID: {Process.GetCurrentProcess().Id}");
Console.WriteLine();

Console.Write("How many games to simulate? (default 1000): ");
var input = Console.ReadLine()?.Trim() ?? "";
var gameCount = int.TryParse(input, out var n) && n > 0 ? n : 1000;

Console.Write("AI depth? (default 3): ");
var depthInput = Console.ReadLine()?.Trim() ?? "";
var aiDepth = int.TryParse(depthInput, out var d) && d > 0 ? d : 3;

var simulator = new SimulatorRunner(gameCount, aiDepth);
simulator.Run();

Console.WriteLine("Done. Press any key to exit.");
Console.ReadKey(intercept: true);
