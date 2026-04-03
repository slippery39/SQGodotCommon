using MtgSimulator;

Console.WriteLine("MTG Simulator");
Console.WriteLine();
Console.Write("How many games to simulate? (default 1000): ");

var input = Console.ReadLine()?.Trim() ?? "";
var gameCount = int.TryParse(input, out var n) && n > 0 ? n : 1000;

var simulator = new SimulatorRunner(gameCount);
simulator.Run();

Console.WriteLine("Done. Press any key to exit.");
Console.ReadKey(intercept: true);
