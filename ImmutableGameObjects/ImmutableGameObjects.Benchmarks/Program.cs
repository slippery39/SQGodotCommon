// See https://aka.ms/new-console-template for more information

// Run all benchmarks
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;

var config = DefaultConfig
	.Instance.AddLogger(ConsoleLogger.Default)
	.AddExporter(HtmlExporter.Default)
	.AddExporter(MarkdownExporter.GitHub);

if (args.Length > 0 && args[0] == "comparison")
{
	BenchmarkRunner.Run<DataStructureComparisonBenchmarks>(config);
}
else if (args.Length > 0 && args[0] == "category")
{
	// Run specific category: dotnet run -- category Add
	var category = args.Length > 1 ? args[1] : "Add";
	var categoryConfig = config.AddFilter(new BenchmarkDotNet.Filters.AnyCategoriesFilter([category]));
	BenchmarkRunner.Run<GameStateBenchmarks>(categoryConfig);
}
else
{
	// Run all GameState benchmarks
	BenchmarkRunner.Run<GameStateBenchmarks>(config);
}
