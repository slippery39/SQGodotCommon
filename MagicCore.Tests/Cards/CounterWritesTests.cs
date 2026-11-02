using System.Text.RegularExpressions;

namespace MagicCore.Tests.Cards;

/// <summary>
/// MagicState.PutCounters is THE way counters are put on a permanent, so every replacement on putting
/// them (Yoshimaru, Brad Boimler) applies in one place. An effect that adds to a counter field itself
/// silently skips them — no error, no failing card test — so this reads the engine's source for one.
/// </summary>
public class CounterWritesTests
{
	static readonly Regex AddsToACounter =
		new(
			@"\b(PlusOneCounters|MinusOneCounters|ChargeCounters|StunCounters|TimeCounters|Loyalty)\s*=(?!=)[^;,{}]*\+|Counters\.SetItem",
			RegexOptions.Compiled
		);

	[Test]
	public void OnlyPutCountersAddsCounters()
	{
		var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
		while (
			dir is not null
			&& !File.Exists(Path.Combine(dir.FullName, "MagicCore", "MagicState.cs"))
		)
			dir = dir.Parent;
		Assert.That(dir, Is.Not.Null, "the MagicCore source folder");
		var engine = Path.Combine(dir!.FullName, "MagicCore");
		var offenders = Directory
			.EnumerateFiles(engine, "*.cs", SearchOption.AllDirectories)
			.Where(f =>
				!f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
			)
			.Where(f => !f.EndsWith(Path.Combine("Cards", "Card.cs"))) // Card.WithCounters: the funnel's own write
			.SelectMany(f =>
				AddsToACounter
					.Matches(File.ReadAllText(f))
					.Select(m => $"{Path.GetRelativePath(engine, f)}: {m.Value}")
			)
			.ToList();
		Assert.That(offenders, Is.Empty, "add counters through MagicState.PutCounters");
	}
}
