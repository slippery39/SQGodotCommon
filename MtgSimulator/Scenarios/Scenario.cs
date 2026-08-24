using System.Text.Json;
using System.Text.Json.Nodes;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator.Scenarios;

/// <summary>
/// A saved position, plus enough context to know what question it is asking.
///
/// <see cref="Note"/> is the reason the position was kept — "AI vents a land here with three in
/// play", "oscillates the Boots". Without it a scenarios folder becomes twenty files called
/// game_412 and nobody remembers which one shows the bug.
/// </summary>
public sealed record Scenario(string Name, string Note, int PlayerToMove, GameState State)
{
	/// <summary>
	/// Written as one object rather than nesting the state under a property so the header stays
	/// greppable at the top of the file — a scenarios folder is browsed with a text editor long
	/// before it is loaded by anything.
	/// </summary>
	public string ToJson()
	{
		var node = new JsonObject
		{
			["Name"] = Name,
			["Note"] = Note,
			["PlayerToMove"] = PlayerToMove,
			["State"] = JsonNode.Parse(StateJson.Serialize(State)),
		};
		return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
	}

	public static Scenario FromJson(string json)
	{
		var node =
			JsonNode.Parse(json)?.AsObject()
			?? throw new JsonException("Scenario file is not a JSON object.");

		var stateNode =
			node["State"] ?? throw new JsonException("Scenario file has no State property.");

		return new Scenario(
			node["Name"]?.GetValue<string>() ?? "unnamed",
			node["Note"]?.GetValue<string>() ?? "",
			node["PlayerToMove"]?.GetValue<int>() ?? 0,
			StateJson.Deserialize(stateNode.ToJsonString())
		);
	}

	/// <summary>
	/// Captures the position as it stands. <paramref name="playerToMove"/> is taken explicitly
	/// rather than read from the active player, because the interesting scenarios are often a
	/// choice owned by the player whose turn it is NOT — see the choice-ownership rule in
	/// ImmutableGameObjects/CLAUDE.md.
	/// </summary>
	public static Scenario Capture(GameState state, int playerToMove, string name, string note) =>
		new(name, note, playerToMove, state);
}

/// <summary>
/// Reads and writes scenarios in <c>scenarios/</c> beside the working directory.
///
/// Same working-directory caveat as <c>sim_results/</c>: relative to the SHELL's cwd, not the
/// project's. Run from the repo root.
/// </summary>
public static class ScenarioStore
{
	public const string DefaultDirectory = "scenarios";

	public static string PathFor(string name, string? directory = null) =>
		Path.Combine(directory ?? DefaultDirectory, $"{Sanitize(name)}.json");

	public static string Save(Scenario scenario, string? directory = null)
	{
		var path = PathFor(scenario.Name, directory);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, scenario.ToJson());
		return path;
	}

	public static Scenario Load(string path) => Scenario.FromJson(File.ReadAllText(path));

	public static IReadOnlyList<string> List(string? directory = null)
	{
		var dir = directory ?? DefaultDirectory;
		return Directory.Exists(dir)
			? Directory.GetFiles(dir, "*.json").OrderBy(p => p).ToList()
			: [];
	}

	// Scenario names come from a human typing into the game, so they reach the filesystem
	// unfiltered otherwise.
	private static string Sanitize(string name)
	{
		var cleaned = new string(
			name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()
		).Trim();
		return string.IsNullOrEmpty(cleaned) ? "scenario" : cleaned;
	}
}
