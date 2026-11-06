using System.Collections.Immutable;
using System.Text.Json;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Sets.Fra;
using MagicCore.Sets.Trk;

namespace MagicCore.Sets;

/// <summary>
/// Every set's cards by name (MC-5.5): a card's script is written ONCE, in the Cards file of one set,
/// and its rules data is the same in every set that prints it; what differs by set is the printing
/// (<see cref="CardPrinting"/> — rarity, collector number, art). Built from each set's imported data
/// and its Cards file's <c>Scripts</c>; it refuses a name scripted in two sets, a script for a card its
/// set doesn't print, and a reprint whose rules data differs between sets — at load, so every test fails.
/// </summary>
public static class CardRegistry
{
	/// <summary>The sets the engine plays, oldest first — a reprint's default printing is its first.</summary>
	public static readonly ImmutableArray<string> Sets = ["FRA", "TRK"];

	static readonly Dictionary<string, IReadOnlyList<CardPrinting>> printings = Sets.ToDictionary(
		s => s,
		CardDataSet.Load
	);

	static readonly Dictionary<string, HashSet<string>> NamesIn = printings.ToDictionary(
		s => s.Key,
		s => s.Value.Select(p => p.Card.Name).ToHashSet()
	);

	static readonly Dictionary<string, CardData> Data = SameInEverySet(
		printings.Values.SelectMany(p => p)
	);

	static readonly Dictionary<string, string> FirstSet = printings
		.Values.SelectMany(p => p)
		.DistinctBy(p => p.Card.Name)
		.ToDictionary(p => p.Card.Name, p => p.Set);

	// Lazy: a set's Scripts initializer may read the printed data above (FraCards' Printed).
	static readonly Lazy<Dictionary<string, ImmutableArray<GameComponent>>> Scripts =
		new(() => Merge(printings, ("FRA", FraCards.Scripts), ("TRK", TrkCards.Scripts)));

	/// <summary>Keywords the engine implements; a text line made only of these needs no script.</summary>
	public static readonly ImmutableHashSet<string> EngineKeywords = ImmutableHashSet.Create(
		StringComparer.OrdinalIgnoreCase,
		"Flying",
		"Reach",
		"Trample",
		"Deathtouch",
		"Lifelink",
		"First strike",
		"Double strike",
		"Vigilance",
		"Haste",
		"Menace",
		"Defender",
		"Flash"
	);

	/// <summary>The card — its one script — as its first set prints it.</summary>
	public static CardDefinition Get(string name) => Get(name, FirstSet[name]);

	/// <summary>The card — its one script — as <paramref name="set"/> prints it (its art).</summary>
	public static CardDefinition Get(string name, string set)
	{
		if (!NamesIn.TryGetValue(set, out var names) || !names.Contains(name))
			throw new KeyNotFoundException($"{set} doesn't print '{name}'");
		return new(
			Data[name],
			Scripts.Value.TryGetValue(name, out var components) ? components : [],
			set
		);
	}

	/// <summary>Scripted, or nothing in its text but keywords the engine already implements.</summary>
	public static bool IsImplemented(string name) =>
		Scripts.Value.ContainsKey(name)
		|| Data[name].TypeLine.StartsWith("Basic Land") // mana from the subtype, CR 305.6
		|| (
			Data[name].CardFaces is null
			&& (Data[name].OracleText ?? "")
				.Split('\n', StringSplitOptions.RemoveEmptyEntries)
				.All(line => line.Split(", ").All(EngineKeywords.Contains))
		);

	public static IEnumerable<string> Scripted => Scripts.Value.Keys;

	/// <summary>Every card of every set, once each.</summary>
	public static IEnumerable<string> AllNames => Data.Keys;

	/// <summary>A card's printed rules data, the same in every set — FraCards' and TrkCards' Printed read its modes.</summary>
	public static CardData DataOf(string name) => Data[name];

	/// <summary>One set's cards as it prints them — its rarities, its collector numbers.</summary>
	public static IReadOnlyList<CardPrinting> Printings(string set) => printings[set];

	/// <summary>
	/// Kai Winn's "choose a nonland card name": every nonland name printed in the sets the game's cards
	/// come from (every set when none says) — never the decks themselves, which would show a hidden list.
	/// </summary>
	public static ImmutableArray<string> NonlandNames(GameState s)
	{
		var inGame = new[] { s.Player1Id(), s.Player2Id() }
			.SelectMany(p =>
				new[]
				{
					ZoneType.Library,
					ZoneType.Hand,
					ZoneType.Battlefield,
					ZoneType.Graveyard,
					ZoneType.Exile,
				}.SelectMany(z => s.CardsIn(p, z))
			)
			.Select(c => c.Set)
			.OfType<string>()
			.Distinct()
			.ToList();
		var sets = inGame.Count > 0 ? inGame.Order(StringComparer.Ordinal).ToList() : [.. Sets];
		return nonlandNames.GetOrAdd(
			string.Join(",", sets),
			_ =>
				[
					.. sets.SelectMany(Printings)
						.Select(p => p.Card)
						.Where(d => !d.TypeLine.Split("//")[0].Contains("Land"))
						.Select(d => d.Name)
						.Distinct()
						.Order(StringComparer.Ordinal),
				]
		);
	}

	/// <summary>NonlandNames by its sets — Kai Winn enters in rollouts too, and the list never changes.</summary>
	static readonly System.Collections.Concurrent.ConcurrentDictionary<
		string,
		ImmutableArray<string>
	> nonlandNames = new();

	/// <summary>A card's rules data, one per name — refusing a reprint whose rules data differs by set.</summary>
	public static Dictionary<string, CardData> SameInEverySet(IEnumerable<CardPrinting> all)
	{
		var data = new Dictionary<string, (CardData Card, string Set, string Json)>();
		foreach (var p in all)
		{
			var json = JsonSerializer.Serialize(p.Card);
			if (!data.TryGetValue(p.Card.Name, out var first))
				data[p.Card.Name] = (p.Card, p.Set, json);
			else if (first.Json != json)
				throw new InvalidOperationException(
					$"'{p.Card.Name}' reads differently in {first.Set} and {p.Set} — a reprint has ONE rules text"
				);
		}
		return data.ToDictionary(d => d.Key, d => d.Value.Card);
	}

	/// <summary>Every set's scripts in one table — refusing a name scripted twice, or not printed by its set.</summary>
	public static Dictionary<string, ImmutableArray<GameComponent>> Merge(
		IReadOnlyDictionary<string, IReadOnlyList<CardPrinting>> printed,
		params (
			string Set,
			IReadOnlyDictionary<string, ImmutableArray<GameComponent>> Scripts
		)[] sets
	)
	{
		var merged = new Dictionary<string, ImmutableArray<GameComponent>>();
		var scriptedIn = new Dictionary<string, string>();
		foreach (var (set, scripts) in sets)
		{
			var names = printed[set].Select(p => p.Card.Name).ToHashSet();
			foreach (var (name, components) in scripts)
			{
				if (scriptedIn.TryGetValue(name, out var other))
					throw new InvalidOperationException(
						$"'{name}' is scripted in both {other} and {set} — a card is scripted ONCE"
					);
				if (!names.Contains(name))
					throw new InvalidOperationException(
						$"'{name}' is scripted in {set}, which doesn't print it"
					);
				scriptedIn[name] = set;
				merged[name] = components;
			}
		}
		return merged;
	}
}
