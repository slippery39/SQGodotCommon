using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;

namespace MagicCore.Limited;

/// <summary>A built sealed deck: its colours, the 40, and the rest of the pool (the sideboard — "outside the game").</summary>
public sealed record SealedDeck(
	IReadOnlyList<ManaColor> Colors,
	IReadOnlyList<string> Main,
	IReadOnlyList<string> Sideboard
);

/// <summary>
/// The AI's sealed build (story LIM-1) — the suggested deck for a human, and the AI opponent's own deck
/// from its own pool. Two colours, no splash yet (Shayne, 2026-10-20): the pair whose best
/// <see cref="Spells"/> castable cards rate highest, by any <see cref="CardRatings"/> table. Lands: every
/// dual in the pool that makes exactly the pair's colours, then basics split by the spells' pips.
/// Like MtgSimulator's draft builder, the manabase serves the cards the deck plays, and nothing else.
/// </summary>
public static class SealedBuilder
{
	public const int DeckSize = 40;

	/// <summary>23 spells and 17 lands — the limited standard.</summary>
	public const int Spells = 23;

	static readonly ManaColor[] Wubrg =
	[
		ManaColor.White,
		ManaColor.Blue,
		ManaColor.Black,
		ManaColor.Red,
		ManaColor.Green,
	];

	static readonly string[] Basics = ["Plains", "Island", "Swamp", "Mountain", "Forest"];

	public static SealedDeck Build(
		IReadOnlyList<string> pool,
		IReadOnlyDictionary<string, double> ratings
	)
	{
		var data = pool.Distinct().ToDictionary(n => n, n => FraCards.Get(n).Data);
		// An unscripted card would play silently vanilla (MagicCore/CLAUDE.md), so it is never played.
		var spells = pool.Where(n => !IsLand(data[n]) && FraCards.IsImplemented(n)).ToList();

		var pairs =
			from i in Enumerable.Range(0, 5)
			from j in Enumerable.Range(i + 1, 4 - i)
			select new[] { Wubrg[i], Wubrg[j] };
		var (colors, chosen) = pairs
			.Select(pair =>
				(
					Pair: pair,
					Cards: spells
						.Where(n => Castable(FrontCost(data[n]), pair))
						.OrderByDescending(n => ratings[n])
						.ThenBy(n => n, StringComparer.Ordinal)
						.Take(Spells)
						.ToList()
				)
			)
			.MaxBy(p => p.Cards.Sum(n => ratings[n])); // the first pair wins a tie: deterministic

		var duals = pool.Where(n =>
				IsLand(data[n]) && DualColors(n) is { Length: 2 } c && c.All(colors.Contains)
			)
			.Take(DeckSize - chosen.Count)
			.ToList();
		var main = chosen
			.Concat(duals)
			.Concat(
				BasicsFor(
					chosen.Select(n => data[n]),
					colors,
					DeckSize - chosen.Count - duals.Count
				)
			)
			.ToList();

		var sideboard = pool.ToList();
		foreach (var card in chosen.Concat(duals))
			sideboard.Remove(card);
		return new SealedDeck(colors, main, sideboard);
	}

	/// <summary>
	/// Can these colours' lands pay every pip: a coloured pip needs its colour, a hybrid either half,
	/// {2/W} anything. {C} needs colourless mana, which basics never make.
	/// </summary>
	public static bool Castable(ManaCost cost, IReadOnlyCollection<ManaColor> colors) =>
		cost.Symbols.All(s =>
			s.Kind switch
			{
				SymbolKind.Colored => colors.Contains(s.Color),
				SymbolKind.Hybrid => colors.Contains(s.Color) || colors.Contains(s.Color2),
				_ => true,
			}
		);

	/// <summary>The cost a card is cast for from the hand — a prepare card's creature half.</summary>
	public static ManaCost FrontCost(CardData card) =>
		ManaCost.Parse(card.CardFaces?[0].ManaCost ?? card.ManaCost);

	static bool IsLand(CardData card) => card.TypeLine.Contains("Land");

	/// <summary>
	/// "{T}: Add {W} or {U}." — the colours its SCRIPT taps for (the mana abilities the engine plays),
	/// so an unscripted land makes none and is never counted as a source.
	/// </summary>
	static ManaColor[] DualColors(string land) =>
		[
			.. FraCards
				.Get(land)
				.Components.OfType<ManaAbilityComponent>()
				.Select(m => m.Color)
				.Distinct(),
		];

	/// <summary>
	/// The basics, split by the spells' pips (a hybrid's pip half to each colour it can be paid with),
	/// by largest remainder so they add up. ponytail: proportional to pips, not to WHEN they are needed
	/// — weight early pips more if builds come out colour-screwed.
	/// </summary>
	static IEnumerable<string> BasicsFor(
		IEnumerable<CardData> spells,
		ManaColor[] colors,
		int count
	)
	{
		var pips = colors.ToDictionary(c => c, _ => 0.0);
		foreach (var s in spells.SelectMany(c => FrontCost(c).Symbols))
			if (s.Kind is SymbolKind.Colored or SymbolKind.TwoBrid && pips.ContainsKey(s.Color))
				pips[s.Color] += 1;
			else if (s.Kind == SymbolKind.Hybrid)
			{
				var halves = new[] { s.Color, s.Color2 }.Where(pips.ContainsKey).ToList();
				foreach (var c in halves)
					pips[c] += 1.0 / halves.Count;
			}
		var total = pips.Values.Sum();
		var share = colors.ToDictionary(
			c => c,
			c => total == 0 ? (double)count / colors.Length : count * pips[c] / total
		);
		var whole = share.ToDictionary(kv => kv.Key, kv => (int)Math.Floor(kv.Value));
		foreach (
			var c in colors
				.OrderByDescending(c => share[c] - whole[c])
				.Take(count - whole.Values.Sum())
		)
			whole[c]++;
		return colors.SelectMany(c => Enumerable.Repeat(Basics[(int)c], whole[c]));
	}
}
