using System.Collections.Concurrent;
using System.Text.Json;
using MagicCore.Cards;

namespace MagicCore.Limited;

/// <summary>
/// How good each card is for LIMITED, on one scale, for every card a pack can hold — what the sealed
/// builder ranks by (story LIM-1). FRA's is 17lands' games-in-hand win rate, computed from its public
/// game data (<c>Sets/FRA.17lands.json</c>, <c>tools/import_17lands.py</c>). Any source fills the same
/// table, so a set with no 17lands data needs only its own (Shayne, 2026-10-20: custom and unreleased
/// sets must play too — the engine-measured rating is its own story).
///
/// The measured <see cref="Ai.CardValues"/> are NOT this: they count only the turn a card is cast, so
/// every planeswalker reads last and value climbs with cost (FRA's median 6-drop 16.5, 1-drop 4.5).
/// </summary>
public static class CardRatings
{
	/// <summary>Games in hand a rate needs before it is taken as it stands.</summary>
	public const int MinGames = 200;

	public sealed record Gih(double? GihWr, int GihGames);

	public sealed record Formats(Gih Sealed, Gih Premier);

	sealed record Table(string Source, Dictionary<string, Formats> Cards);

	static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, double>> Tables =
		new();

	/// <summary>A set's own table, <c>Sets/&lt;SET&gt;.ratings.json</c>: the ratings as they stand (TRK's INTERIM).</summary>
	sealed record Own(string Source, Dictionary<string, double> Cards);

	/// <summary>
	/// A set's ratings for every card of the set and its packs — its own table (<c>Sets/&lt;SET&gt;.ratings.json</c>,
	/// a set with no 17lands data: TRK, MC-5.4) if it has one, else 17lands' GIH WR (<c>Sets/&lt;SET&gt;.17lands.json</c>).
	/// </summary>
	public static IReadOnlyDictionary<string, double> Of(string set) =>
		Tables.GetOrAdd(
			set,
			s =>
				CardDataSet.ResourceText($"{s}.ratings.json") is { } own
					? JsonSerializer.Deserialize<Own>(own, CaseInsensitive)!.Cards
					: From17Lands(
						JsonSerializer
							.Deserialize<Table>(
								CardDataSet.ResourceText($"{s}.17lands.json")
									?? throw new InvalidOperationException(
										$"No {s}.17lands.json. Run: python tools/import_17lands.py {s.ToLowerInvariant()}"
									),
								new JsonSerializerOptions
								{
									PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
								}
							)!
							.Cards
					)
		);

	static readonly JsonSerializerOptions CaseInsensitive =
		new() { PropertyNameCaseInsensitive = true };

	/// <summary>Of, or null for a set with no table yet — Kai Winn's blind pick asks.</summary>
	public static IReadOnlyDictionary<string, double>? TryOf(string set) =>
		Tables.ContainsKey(set)
		|| CardDataSet.ResourceText($"{set}.ratings.json") is not null
		|| CardDataSet.ResourceText($"{set}.17lands.json") is not null
			? Of(set)
			: null;

	/// <summary>How far a card's value moves it inside its rarity: ±<see cref="ValueSpread"/>/2 from the median.</summary>
	public const double ValueSpread = 0.04;

	/// <summary>
	/// INTERIM ratings for a set 17lands has never seen (TRK, MC-5.4 — DATA-2's simulated ones replace them):
	/// each card at its rarity's median in the reference set's ratings (FRA's 17lands), moved by its measured
	/// value's rank among its rarity's MEASURED cards (<see cref="Ai.CardValues"/>, ± <see cref="ValueSpread"/>/2;
	/// <paramref name="values"/> holds only those). On FRA itself this ranks the cards against 17lands at
	/// ρ 0.41 (Spearman), against 0.25 for the rarity alone and 0.21 for the values
	/// alone (docs/findings/limited.md) — the values rank well INSIDE a rarity and badly across one, because
	/// they climb with cost. ponytail: the spread was picked on FRA, the set it is checked on.
	/// </summary>
	public static Dictionary<string, double> Interim(
		IReadOnlyList<CardPrinting> set,
		IReadOnlyDictionary<string, double> values,
		IReadOnlyList<CardPrinting> reference,
		IReadOnlyDictionary<string, double> referenceRatings
	)
	{
		static bool Basic(CardPrinting p) =>
			p.Card.TypeLine.StartsWith("Basic", StringComparison.Ordinal);
		var medians = reference
			.Where(p => !Basic(p) && referenceRatings.ContainsKey(p.Card.Name))
			.GroupBy(p => p.Rarity)
			.ToDictionary(g => g.Key, g => Median(g.Select(p => referenceRatings[p.Card.Name])));
		var all = Median(medians.Values);
		var rated = new Dictionary<string, double>();
		foreach (var rarity in set.Where(p => !Basic(p)).GroupBy(p => p.Rarity))
		{
			var median = medians.GetValueOrDefault(rarity.Key, all);
			var measured = rarity
				.Where(p => values.ContainsKey(p.Card.Name))
				.Select(p => values[p.Card.Name])
				.ToList();
			foreach (var p in rarity)
			{
				// a card the sandbox couldn't measure sits at the median, not last (FRA: ρ 0.41 against 0.40)
				if (!values.TryGetValue(p.Card.Name, out var v))
				{
					rated[p.Card.Name] = median;
					continue;
				}
				var rank =
					(measured.Count(x => x < v) + 0.5 * measured.Count(x => x == v))
					/ measured.Count;
				rated[p.Card.Name] = median + ValueSpread * (rank - 0.5);
			}
		}
		return rated;
	}

	/// <summary>
	/// Sealed's rate — the format being built for — when it rests on <see cref="MinGames"/>; else
	/// Premier Draft's, moved onto Sealed's scale by the mean gap between the formats over the cards
	/// rated in both (FRA's Sealed reads ~1.2pp higher, 2026-10-20); else both formats' games pooled,
	/// any games short of <see cref="MinGames"/> made up at the set's median — so a card seen twice
	/// can't read 100%, and the pull fades out as the games come in. ponytail: a rarely played card is
	/// usually rarely played for a reason, so the pull may flatter it — FRA had 11 such cards on
	/// 2026-10-20, four of them Special Guests.
	/// </summary>
	public static IReadOnlyDictionary<string, double> From17Lands(
		IReadOnlyDictionary<string, Formats> cards
	)
	{
		static bool Enough(Gih g) => g.GihGames >= MinGames;
		var both = cards.Values.Where(f => Enough(f.Sealed) && Enough(f.Premier)).ToList();
		var gap =
			both.Count == 0 ? 0 : both.Average(f => f.Sealed.GihWr!.Value - f.Premier.GihWr!.Value);

		double? Rated(Formats f) =>
			Enough(f.Sealed) ? f.Sealed.GihWr
			: Enough(f.Premier) ? f.Premier.GihWr + gap
			: null;

		var median = Median(cards.Values.Select(Rated).OfType<double>());

		double Pooled(Formats f)
		{
			var games = f.Sealed.GihGames + f.Premier.GihGames;
			var wins =
				(f.Sealed.GihWr ?? 0) * f.Sealed.GihGames
				+ ((f.Premier.GihWr ?? 0) + gap) * f.Premier.GihGames;
			return (wins + median * Math.Max(0, MinGames - games)) / Math.Max(games, MinGames);
		}

		return cards.ToDictionary(c => c.Key, c => Rated(c.Value) ?? Pooled(c.Value));
	}

	/// <summary>A table's median rating — 0.5 for an empty one (a set nobody has played yet: every card even).</summary>
	public static double Median(IEnumerable<double> ratings)
	{
		var sorted = ratings.Order().ToList();
		return sorted.Count == 0 ? 0.5
			: sorted.Count % 2 == 1 ? sorted[sorted.Count / 2]
			: (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
	}
}
