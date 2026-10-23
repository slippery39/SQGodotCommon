using System.Text.RegularExpressions;
using MagicCore.Cards;

namespace MagicCore.Sets.Fra;

/// <summary>
/// An Arena deck export, read into FRA cards — what you'd queue with, to playtest first:
/// <code>
/// Deck
/// 4 Last Gasp (FRA) 96
/// 17 Swamp (FRA) 283
///
/// Sideboard
/// 2 Prophesied End (FRA) 12
/// </code>
/// <see cref="Parse"/> reads the main deck, which ends at the first blank line or section header
/// after it; the sideboard is not read (Bo1). Anything not in FRA, or not playable yet, is reported
/// by name and the deck is not returned — a silent substitute would test a different deck. A sealed
/// pool is <see cref="ReadPool"/>. Either way a card is found by its name, whatever set code follows
/// it; a two-faced card by its full name (Arena's " /// " or Scryfall's " // ") or its front face.
/// </summary>
public static partial class ArenaDeck
{
	public sealed record Result(IReadOnlyList<CardDefinition> Deck, IReadOnlyList<string> Problems)
	{
		public bool Ok => Problems.Count == 0;
	}

	static readonly IReadOnlyDictionary<string, string> ByName = FraCards
		.AllNames.SelectMany(name =>
			name.Contains(" // ")
				? new[] { (Key: name, Name: name), (Key: name.Split(" // ")[0], Name: name) }
				: [(Key: name, Name: name)]
		)
		.DistinctBy(x => x.Key)
		.ToDictionary(x => x.Key, x => x.Name, StringComparer.OrdinalIgnoreCase);

	// At most three digits: a pasted "99999999999 Swamp" is a typo, not a 99-billion-card deck.
	[GeneratedRegex(@"^(\d{1,3})x?\s+(.+?)(?:\s+\([A-Za-z0-9]+\)(?:\s+\S+)?)?$")]
	private static partial Regex CardLine();

	public static Result Parse(string text)
	{
		var deck = new List<CardDefinition>();
		var problems = new List<string>();
		var lines = text.Replace("\r", "").Split('\n').Select(l => l.Trim()).ToList();
		// A Companion or Commander section may come first; the main deck starts after "Deck".
		var deckHeader = lines.FindIndex(l => l.Equals("Deck", StringComparison.OrdinalIgnoreCase));
		foreach (var raw in lines.Skip(deckHeader + 1))
		{
			var line = raw.Trim();
			if (line.Length == 0 || !char.IsDigit(line[0]))
			{
				if (deck.Count > 0 || problems.Count > 0)
					break; // the blank line or header that starts the sideboard
				continue; // "Deck", "About", "Name …" before the cards
			}
			var match = CardLine().Match(line);
			if (!match.Success)
			{
				problems.Add($"Can't read the line \"{line}\"");
				continue;
			}
			var (count, name) = (
				int.Parse(match.Groups[1].Value),
				match.Groups[2].Value.Replace(" /// ", " // ")
			);
			if (!ByName.TryGetValue(name, out var fra))
				problems.Add($"{name}: not a Reality Fracture card");
			else if (!FraCards.IsImplemented(fra))
				problems.Add($"{fra}: not playable yet");
			else
				deck.AddRange(Enumerable.Repeat(FraCards.Get(fra), count));
		}
		if (deck.Count == 0 && problems.Count == 0)
			problems.Add(
				"No cards found — paste an Arena export (\"4 Card Name (FRA) 123\" lines)"
			);
		return new Result(problems.Count == 0 ? deck : [], problems);
	}

	/// <param name="Pool">FRA card names, one per copy — basics dropped, since limited's are free.</param>
	/// <param name="LeftOut">What isn't an FRA card (a Special Guest until MC-3), one per copy, or an unreadable line.</param>
	public sealed record PoolResult(IReadOnlyList<string> Pool, IReadOnlyList<string> LeftOut);

	/// <summary>
	/// A sealed pool exported from Arena (story LIM-1.7): EVERY section is the pool — the deck you
	/// built and the sideboard alike. Unlike <see cref="Parse"/>, an unknown card never fails the
	/// whole: it is left out and named, so a pool holding one still plays.
	/// </summary>
	public static PoolResult ReadPool(string text)
	{
		var pool = new List<string>();
		var leftOut = new List<string>();
		foreach (var line in text.Replace("\r", "").Split('\n').Select(l => l.Trim()))
		{
			if (line.Length == 0 || !char.IsDigit(line[0]))
				continue; // a section header: "Deck", "Sideboard", …
			var match = CardLine().Match(line);
			if (!match.Success)
			{
				leftOut.Add(line);
				continue;
			}
			var (count, name) = (
				int.Parse(match.Groups[1].Value),
				match.Groups[2].Value.Replace(" /// ", " // ")
			);
			if (!ByName.TryGetValue(name, out var fra))
				leftOut.AddRange(Enumerable.Repeat(name, count));
			else if (!Limited.SealedBuilder.Basics.Contains(fra))
				pool.AddRange(Enumerable.Repeat(fra, count));
		}
		return new PoolResult(pool, leftOut);
	}
}
