using System.Text.Json;
using MagicCore.Cards;
using MagicCore.Sets.Fra;

namespace MagicCore.Limited;

/// <summary>A card in a pack, and the sheet it came from (<c>common</c>, <c>pairAjani</c>, <c>rareMythic</c> …).</summary>
public sealed record PackCard(string Card, string Sheet);

/// <summary>
/// FRA play boosters as Arena opens them (story LIM-1): "Each Play Booster contains 14 Magic: The
/// Gathering cards", including the echoed pairs — "Includes 2 cards of the same rarity from the same
/// set of echoed pairs cards" and a third from another pair. The collation is MTGJSON's, imported
/// whole into <c>Sets/FRA.boosters.json</c>: a weighted pack variant names how many cards it takes
/// from each sheet. Arena has no foils, so paper's foil slot is simply a second wildcard (Wizards:
/// "two non-foil wildcard slots") — foil flags are never read. On top, the one rule Wizards states
/// for commons: "guarantee four of the five colors appear in common in each booster".
/// </summary>
public static class Boosters
{
	sealed record Sheet(bool Fixed, Dictionary<string, int> Cards);

	sealed record Variant(int Weight, Dictionary<string, int> Contents);

	sealed record Config(List<Variant> Boosters, Dictionary<string, Sheet> Sheets);

	static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

	static readonly Lazy<Config> Fra =
		new(
			() =>
				JsonSerializer.Deserialize<Config>(
					CardDataSet.ResourceText("FRA.boosters.json")
						?? throw new InvalidOperationException(
							"No FRA.boosters.json. Run: python tools/import_mtgjson_boosters.py fra"
						),
					Options
				)!
		);

	/// <summary>
	/// False until the 10 Special Guests are scripted (story MC-3): until then the pack that would
	/// hold one takes a sixth common instead. Shayne, 2026-10-20: no unscripted guest in a pool.
	/// </summary>
	public const bool GuestsPlayable = false;

	/// <summary>The four-of-five-colours rule, applied to the <c>common</c> sheet's cards.</summary>
	public const int MinCommonColors = 4;

	/// <summary>A sealed pool: <paramref name="packs"/> packs (Arena: six), the same pool for the same seed.</summary>
	public static IReadOnlyList<IReadOnlyList<PackCard>> Sealed(int seed, int packs = 6)
	{
		var rng = new Random(seed);
		return [.. Enumerable.Range(0, packs).Select(_ => Pack(rng))];
	}

	/// <summary>One pack: a weighted variant, then each of its sheets drawn — no card twice from a sheet.</summary>
	public static IReadOnlyList<PackCard> Pack(Random rng)
	{
		var config = Fra.Value;
		var contents = new Dictionary<string, int>(
			Pick(config.Boosters, v => v.Weight, rng).Contents
		);
		if (!GuestsPlayable && contents.Remove("specialGuest", out var guests))
			contents["common"] += guests;
		var pack = new List<PackCard>();
		foreach (var (sheet, count) in contents)
			pack.AddRange(
				Draw(config.Sheets[sheet], count, rng, balance: sheet == "common")
					.Select(c => new PackCard(c, sheet))
			);
		return pack;
	}

	static List<string> Draw(Sheet sheet, int count, Random rng, bool balance)
	{
		if (sheet.Fixed)
			return [.. sheet.Cards.Keys];
		// ponytail: redraw until the colour rule holds — Wizards' real method is unpublished; a
		// rejection keeps every pack that already passes at its MTGJSON odds, and lifts the rest in
		// proportion. Collate by FRA's real print runs instead if opened-pack data ever reveals them.
		for (var attempt = 0; attempt < 1000; attempt++)
		{
			var left = sheet.Cards.ToList();
			var drawn = new List<string>();
			for (var i = 0; i < count; i++)
			{
				var card = Pick(left, c => c.Value, rng);
				left.Remove(card);
				drawn.Add(card.Key);
			}
			if (
				!balance
				|| drawn.SelectMany(c => FraCards.Get(c).Data.Colors ?? []).Distinct().Count()
					>= MinCommonColors
			)
				return drawn;
		}
		throw new InvalidOperationException(
			$"{count} commons never showed {MinCommonColors} colours in 1000 draws"
		);
	}

	static T Pick<T>(IReadOnlyList<T> items, Func<T, int> weight, Random rng)
	{
		var roll = rng.NextInt64(items.Sum(i => (long)weight(i)));
		foreach (var item in items)
			if ((roll -= weight(item)) < 0)
				return item;
		throw new InvalidOperationException("unreachable: the roll is below the total weight");
	}
}
