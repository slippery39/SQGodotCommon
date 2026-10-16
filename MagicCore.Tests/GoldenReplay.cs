using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using ImmutableGameObjects;

namespace MagicCore.Tests;

/// <summary>
/// Fingerprints of seeded random games, checked in, so a refactor that must not change behaviour can
/// prove it didn't. Each step records who decided, the move or choice answer, AND how many options
/// there were — a filter offering one candidate more shows at that step, before any random pick
/// diverges. A running hash every <see cref="Checkpoint"/> steps names the window where a game first
/// differs, and the game's whole trace is written out for reading around it.
///
/// Three files: GoldenReplay.txt (RandomGameTests, every run), GoldenCardReplay.txt
/// (CardReplayTests, explicit — each scripted card in games built around it) and GoldenAiReplay.txt
/// (GreedyAiReplayTests, every run — GreedyAi against itself; random play never asks the AI anything).
///
/// Regenerate ONLY for an intended behaviour change, with the diff reviewed: run the fixture with
/// <c>$env:MAGIC_GOLDEN_UPDATE='1'</c> (merges the games run into the file — a filtered run updates
/// only those) or <c>'all'</c> (rewrites the file from this run, dropping games that no longer
/// exist; run the whole fixture). The fingerprint depends on <see cref="Describe"/>'s text and on the
/// ORDER of legal actions and choice options — keep both. Object ids are left out on purpose: they
/// shift with allocation order while behaviour stays the same.
/// </summary>
public sealed class GoldenReplay(string fileName)
{
	const int Checkpoint = 100;

	readonly string FilePath = Beside(fileName);

	// The source folder, not the working directory (which under dotnet test is bin/).
	static string Beside(string fileName, [CallerFilePath] string source = "") =>
		Path.Combine(Path.GetDirectoryName(source)!, fileName);

	static string? Mode => Environment.GetEnvironmentVariable("MAGIC_GOLDEN_UPDATE");

	static bool Updating => Mode is "1" or "all";

	Dictionary<string, string>? recorded;

	Dictionary<string, string> Recorded =>
		recorded ??= File.Exists(FilePath)
			? File.ReadLines(FilePath)
				.Where(l => l != "" && !l.StartsWith('#'))
				.ToDictionary(Key, l => l)
			: [];

	readonly Dictionary<string, string> Fresh = [];

	static string Key(string line) => line[..line.IndexOf(" :", StringComparison.Ordinal)];

	public sealed class Trace
	{
		readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		readonly List<string> lines = [];
		readonly List<string> checkpoints = [];

		public void Step(string line)
		{
			lines.Add(line);
			hash.AppendData(Encoding.UTF8.GetBytes(line + "\n"));
			if (lines.Count % Checkpoint == 0)
				checkpoints.Add(Short(hash.GetCurrentHash()));
		}

		public string Summary(GameState end)
		{
			var players = new[] { end.Player1Id(), end.Player2Id() }.Select(p =>
				$"{end.GetPlayer(p).Life}{(end.GetPlayer(p).HasLost ? "L" : "")}"
				+ $"[{string.Join(",", Zones.Select(z => end.CardsIn(p, z).Count()))}]"
			);
			return $"steps={lines.Count} turn={end.GetGame().TurnNumber} "
				+ $"end={string.Join(" ", players)} final={Short(hash.GetCurrentHash())}"
				+ (checkpoints.Count > 0 ? $" | {string.Join(" ", checkpoints)}" : "");
		}

		public string WriteOut(string key)
		{
			var file = string.Concat(key.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));
			var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"replay-{file}.txt");
			File.WriteAllLines(path, lines.Select((l, i) => $"{i + 1, 5}  {l}"));
			return path;
		}
	}

	static readonly ZoneType[] Zones =
	[
		ZoneType.Hand,
		ZoneType.Library,
		ZoneType.Graveyard,
		ZoneType.Battlefield,
		ZoneType.Exile,
	];

	static string Short(byte[] digest) => Convert.ToHexString(digest, 0, 4).ToLowerInvariant();

	public void Check(string game, int seed, Trace trace, GameState end)
	{
		var key = $"{game} {seed:D2}";
		var line = $"{key} : {trace.Summary(end)}";
		if (Updating)
		{
			Fresh[key] = line;
			return;
		}
		Assert.That(
			Recorded.TryGetValue(key, out var was),
			$"{key} has no recorded replay — regenerate (see GoldenReplay)"
		);
		if (was == line)
			return;
		Assert.Fail(
			$"{key} played differently from its recording — first within steps {FirstDifference(was!, line)}.\n"
				+ $"was: {was![..Math.Min(was!.Length, 160)]}\nnow: {line[..Math.Min(line.Length, 160)]}\n"
				+ $"this run's trace: {trace.WriteOut(key)}"
		);
	}

	static string FirstDifference(string was, string now)
	{
		static string[] Points(string l) =>
			l.IndexOf(" | ", StringComparison.Ordinal) is var at and >= 0
				? l[(at + 3)..].Split(' ')
				: [];
		var (a, b) = (Points(was), Points(now));
		var i = 0;
		while (i < a.Length && i < b.Length && a[i] == b[i])
			i++;
		return $"{i * Checkpoint + 1}–{(i + 1) * Checkpoint}";
	}

	/// <summary>After the fixture, in update mode: write this run's games — merged in, or alone ('all').</summary>
	public void WriteIfUpdating()
	{
		if (!Updating || Fresh.Count == 0)
			return;
		var all = new Dictionary<string, string>(Mode == "all" ? [] : Recorded);
		foreach (var (k, v) in Fresh)
			all[k] = v;
		File.WriteAllLines(
			FilePath,
			[
				"# Seeded games' fingerprints — see MagicCore.Tests/GoldenReplay.cs. Regenerate only",
				"# for an intended behaviour change, with the diff reviewed. Format: game seed : steps, last",
				"# turn, each player's life [hand,library,graveyard,battlefield,exile], hash | hash per 100 steps.",
				.. all.Keys.Order(StringComparer.Ordinal).Select(k => all[k]),
			]
		);
	}
}
