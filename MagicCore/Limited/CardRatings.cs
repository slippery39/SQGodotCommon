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

	static readonly Lazy<IReadOnlyDictionary<string, double>> fra =
		new(
			() =>
				From17Lands(
					JsonSerializer
						.Deserialize<Table>(
							CardDataSet.ResourceText("FRA.17lands.json")
								?? throw new InvalidOperationException(
									"No FRA.17lands.json. Run: python tools/import_17lands.py fra"
								),
							new JsonSerializerOptions
							{
								PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
							}
						)!
						.Cards
				)
		);

	/// <summary>FRA's 17lands GIH WR for every card of the set and its packs.</summary>
	public static IReadOnlyDictionary<string, double> Fra => fra.Value;

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
