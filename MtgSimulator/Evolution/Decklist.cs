using System.Collections.Immutable;
using System.Text.Json;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// A constructed deck as DATA — spell name → copy count, plus a land count.
///
/// The project had no such type: preconstructed decks are hardcoded C# builder methods and a
/// drafted deck is an ordered pool. Neither can be mutated, compared or serialized, which is
/// all this mode does.
///
/// Keyed by card NAME because every per-card table in the project already is —
/// <c>CardStat</c>, <c>CardValue</c>, <c>GameRunner</c>'s cardNames map. A card id would be a
/// second identity that has to be kept in step with all of them, and <c>Card.Id</c> is a
/// per-instance runtime id anyway: two Plains in one library have different ids.
///
/// Lands are a COUNT, not entries in <see cref="Spells"/>. There is exactly one land in the
/// engine (Plains) and no colours, so a mana base is a scalar. If duals or colours ever land,
/// this becomes a second dictionary and <see cref="Difference"/> keeps ignoring it.
/// </summary>
public sealed record Decklist(string Name, ImmutableSortedDictionary<string, int> Spells, int Lands)
{
	public const int DeckSize = 60;

	/// <summary>
	/// The constructed 4-of rule. NOTHING in the engine enforces this — ZooDeckFactory runs
	/// 4x Ancestral Recall today — so it is an invariant of this type rather than a rule the
	/// game will apply. Without it a hill climb converges on 36 copies of the best card in a
	/// handful of generations, which is a true local optimum and a useless deck.
	/// </summary>
	public const int MaxCopies = 4;

	/// <summary>
	/// The range the mutator may move Lands within — and, as measured, the single constraint that
	/// kept the evolution away from the best decks in the format.
	///
	/// **20 was set by analogy with paper Magic and is wrong for this engine.** Every hand-built
	/// deck that beats an evolved field runs FEWER lands than the floor allowed: Traditional Storm
	/// 12, Zoo 14, Affinity 14, Goblins 16 — against a legal minimum of 20. Those decks were not
	/// hard to reach, they were **illegal**, and no amount of hill climbing, gauntlet pressure or
	/// synergy detection can reach a deck the validator rejects. Evolved decks sat pinned at
	/// exactly 20, which is what a binding constraint looks like.
	///
	/// The economics differ from paper in two ways that both push the floor down: there are no
	/// colours, so a land is pure quantity rather than fixing, and **every opening hand contains
	/// three lands by rule** — so 20 of 60 on top of a guaranteed three is far more than an
	/// aggressive deck wants.
	///
	/// Overridable via `MTG_MIN_LANDS` / `MTG_MAX_LANDS` so an A/B can run both arms on ONE binary.
	/// Rebuilding between arms is how the stale-`bin/` trap gets in, and this project has paid for
	/// that twice.
	/// </summary>
	public static readonly int MinLands = ReadBound("MTG_MIN_LANDS", 20);

	public static readonly int MaxLands = ReadBound("MTG_MAX_LANDS", 26);

	private static int ReadBound(string variable, int fallback) =>
		int.TryParse(Environment.GetEnvironmentVariable(variable), out var v) && v is > 0 and < 60
			? v
			: fallback;

	public int SpellCount => Spells.Values.Sum();

	public int TotalCards => SpellCount + Lands;

	/// Distinct spell names — the "how many different cards" figure a decklist is read by.
	public int DistinctSpells => Spells.Count;

	public static Decklist Empty(string name) =>
		new(
			name,
			ImmutableSortedDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal),
			0
		);

	/// <summary>
	/// Every rule this type promises, checked in one place. Returns null when valid.
	///
	/// Returned rather than thrown so the mutator can propose freely and discard what does not
	/// hold, which is much simpler than making every operator individually incapable of
	/// producing an invalid deck.
	/// </summary>
	public string? Validate()
	{
		if (TotalCards != DeckSize)
			return $"{Name}: {TotalCards} cards, expected {DeckSize}";
		if (Lands < MinLands || Lands > MaxLands)
			return $"{Name}: {Lands} lands, expected {MinLands}-{MaxLands}";
		foreach (var (card, count) in Spells)
		{
			if (count < 1)
				return $"{Name}: {card} has {count} copies";
			if (count > MaxCopies)
				return $"{Name}: {card} has {count} copies, max {MaxCopies}";
		}
		return null;
	}

	public bool IsValid => Validate() is null;

	/// <summary>
	/// Turns the decklist into the card instances a game is loaded from.
	///
	/// Stamps OwnerId/ControllerId, so — exactly like Draft.BuildDeck — this must run PER
	/// GAME, not once per deck: a deck is Player 1 in some pairings and Player 2 in others.
	/// GameSetup.FromDecks takes a builder rather than a deck for this reason.
	///
	/// A name missing from <paramref name="pool"/> is skipped and the shortfall is made up
	/// with lands, so a decklist saved against one set still loads against another rather
	/// than throwing mid-run. The count is reported by <see cref="MissingFrom"/>; callers
	/// that care should check it up front rather than discovering it per game.
	/// </summary>
	public IReadOnlyList<Card> Materialize(int ownerId, IReadOnlyDictionary<string, Card> pool)
	{
		var deck = new List<Card>(DeckSize);
		foreach (var (name, count) in Spells)
		{
			if (!pool.TryGetValue(name, out var template))
				continue;
			for (var i = 0; i < count; i++)
				deck.Add(template with { OwnerId = ownerId, ControllerId = ownerId });
		}

		// Pad to DeckSize rather than adding exactly Lands, so a deck referencing a card the
		// pool lacks still plays 60. Same padding idiom as Draft.BuildDeck.
		while (deck.Count < DeckSize)
			deck.Add(CardLibrary.Plains() with { OwnerId = ownerId, ControllerId = ownerId });

		return deck;
	}

	/// Spell names this list names that the pool does not have.
	public IReadOnlyList<string> MissingFrom(IReadOnlyDictionary<string, Card> pool) =>
		Spells.Keys.Where(n => !pool.ContainsKey(n)).ToList();

	/// <summary>
	/// How different two decks are, 0 (identical) to 1 (no card in common).
	///
	/// Multiset dissimilarity over SPELLS ONLY — lands are excluded because every deck plays
	/// the same 20-26 Plains and counting them would report two completely different decks as
	/// ~35% similar before a single spell was compared.
	///
	/// Copy counts matter: 4x Bolt against 1x Bolt is mostly a difference, not a match.
	/// </summary>
	public static double Difference(Decklist a, Decklist b)
	{
		var total = a.SpellCount + b.SpellCount;
		if (total == 0)
			return 0;

		var shared = 0;
		foreach (var (name, count) in a.Spells)
			shared += Math.Min(count, b.Spells.GetValueOrDefault(name));

		return 1.0 - (2.0 * shared / total);
	}

	/// <summary>
	/// Average mana cost of the spells, the "curve" the seeder targets. Lands are excluded —
	/// a land costs 0 and averaging it in would report every deck at roughly the same number.
	/// Returns 0 for a spell-less list.
	/// </summary>
	public double AverageCost(IReadOnlyDictionary<string, Card> pool)
	{
		var count = 0;
		var total = 0;
		foreach (var (name, copies) in Spells)
		{
			if (!pool.TryGetValue(name, out var card))
				continue;
			total += card.ManaCost * copies;
			count += copies;
		}
		return count == 0 ? 0 : (double)total / count;
	}

	/// Copies of one card, adjusted and clamped; removes the entry at zero.
	public Decklist WithCopies(string name, int count)
	{
		var clamped = Math.Clamp(count, 0, MaxCopies);
		return this with
		{
			Spells = clamped == 0 ? Spells.Remove(name) : Spells.SetItem(name, clamped),
		};
	}

	public int CopiesOf(string name) => Spells.GetValueOrDefault(name);

	/// Human-readable, grouped by cost — the form a decklist is actually read in.
	public string Format(IReadOnlyDictionary<string, Card> pool)
	{
		var lines = Spells
			.Select(kv =>
				(Cost: pool.TryGetValue(kv.Key, out var c) ? c.ManaCost : 0, Name: kv.Key, kv.Value)
			)
			.OrderBy(e => e.Cost)
			.ThenBy(e => e.Name, StringComparer.Ordinal)
			.Select(e => $"  {e.Value}x {e.Name} ({e.Cost})");

		return string.Join(
			Environment.NewLine,
			lines.Append($"  {Lands}x Plains").Prepend($"{Name} — {TotalCards} cards")
		);
	}
}

/// <summary>
/// The result of one evolution run: the final field and the matrix it was measured on.
/// Plain records of strings and numbers, so System.Text.Json needs no converters.
/// </summary>
public sealed record MetagameResult(
	string SetCode,
	int Generations,
	int GamesPerMatchup,
	IReadOnlyList<Decklist> Decks,
	/// Row = deck index, column = opponent index; win rate of the ROW deck. Diagonal is -1.
	IReadOnlyList<IReadOnlyList<double>> Matrix,
	IReadOnlyList<double> OverallWinRates
);

public static class DecklistStore
{
	private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

	public static string PathFor(string setCode) =>
		Path.Combine(
			"sim_results",
			$"metagame_{setCode.ToLowerInvariant()}_{DateTime.Now:yyyyMMdd_HHmmss}.json"
		);

	public static void Save(MetagameResult result, string path)
	{
		var dir = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);
		File.WriteAllText(path, JsonSerializer.Serialize(result, Options));
	}

	public static MetagameResult? Load(string path) =>
		File.Exists(path)
			? JsonSerializer.Deserialize<MetagameResult>(File.ReadAllText(path), Options)
			: null;
}
