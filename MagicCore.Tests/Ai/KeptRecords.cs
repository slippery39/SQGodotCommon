using System.Reflection;
using ImmutableGameObjects;
using MagicCore.Ai;

namespace MagicCore.Tests.Ai;

/// <summary>
/// Table games kept as AI test cases (story TBL-5.5): <c>MagicCore.Tests/Records/NAME.json</c>,
/// embedded in this assembly. Keep one, and get a test to start from, with
/// <c>dotnet run --project MagicConsole -- --record=FILE --keep=NAME --at=N</c>.
/// </summary>
public static class KeptRecords
{
	const string Prefix = "Records.";

	public static IEnumerable<string> Names =>
		Assembly
			.GetExecutingAssembly()
			.GetManifestResourceNames()
			.Where(n => n.StartsWith(Prefix))
			.Select(n => n[Prefix.Length..^".json".Length]);

	public static GameRecord Get(string name)
	{
		using var stream =
			Assembly.GetExecutingAssembly().GetManifestResourceStream($"{Prefix}{name}.json")
			?? throw new ArgumentException($"no kept record '{name}' in MagicCore.Tests/Records");
		using var reader = new StreamReader(stream);
		return GameRecord.FromJson(reader.ReadToEnd());
	}

	/// <summary>The position before move <paramref name="at"/>, as the transcript numbers it.</summary>
	public static GameState Before(string name, int at) => Get(name).Before(at);
}
