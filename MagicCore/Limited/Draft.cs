using System.Collections.Immutable;
using MagicCore.Sets;

namespace MagicCore.Limited;

/// <summary>One drafter: the pack in front of them, their packs still unopened, and their picks in order.</summary>
public sealed record DraftSeat(
	ImmutableList<string> Pack,
	ImmutableList<ImmutableList<string>> Unopened,
	ImmutableList<string> Picks
);

/// <summary>
/// A draft in progress — plain immutable data, not a GameState. <see cref="Round"/> is the pack being
/// drafted (0, 1, 2), which sets the passing direction.
/// </summary>
public sealed record DraftState(ImmutableList<DraftSeat> Seats, int Round)
{
	/// Seats are symmetric — every pack is the same size, so they run out on the same pick.
	public bool IsComplete => Seats[0].Pack.IsEmpty;
}

/// <summary>
/// Arena's Quick Draft for FRA (story LIM-2): Shayne and 7 bots, three packs each, the first and third
/// passed left, the second right. Copied from MtgSimulator's <c>Draft</c> (MtgCore's cards, so a copy,
/// not a reference). The cards are NAMES and the packs are dealt up front, so a cube drafts the same
/// way from its own list (<see cref="Create"/>) — the draft never assumes packs (Shayne, 2026-10-23).
/// ponytail: a card's colours come from FRA's card data (<see cref="ColorsOf"/>), so a cube's cards
/// need their own lookup passed in — MC-4's, when the cube can be drafted.
/// Packs lose cards only by picks, so the echoed-pair block leaves its signal honestly: a pack showing
/// two pair cards from different pairs means the partner of one was taken.
/// </summary>
public static class Draft
{
	public const int SeatCount = 8;
	public const int PackCount = 3;

	/// <summary>
	/// The bots' colour lean, in points of rating (Shayne, 2026-10-29: MtgSimulator's,
	/// <c>DraftPickers.DefaultLaneWeight</c> — 55.9% over 960 games against no lean at 2 and 4 worse, it
	/// passes the bombs — measured for ITS sampled picker on its own ratings, not re-measured for this
	/// one): a card inside the bot's two colours gains it, one outside loses it.
	/// </summary>
	public const double LaneWeight = 2.0;

	/// <summary>Picks over which the lean ramps from nothing to full — about when a drafter knows their colours.</summary>
	public const int LaneRampPicks = 10;

	/// <summary>A set's draft: <see cref="SeatCount"/> seats × <see cref="PackCount"/> Arena packs, the same for the same seed.</summary>
	public static DraftState New(string set, int seed)
	{
		var rng = new Random(seed);
		var packs = new List<IReadOnlyList<IReadOnlyList<string>>>();
		for (var seat = 0; seat < SeatCount; seat++)
			packs.Add(
				[
					.. Enumerable
						.Range(0, PackCount)
						.Select(_ => Boosters.Pack(set, rng).Select(c => c.Card).ToList()),
				]
			);
		return Create(packs);
	}

	/// <summary>A draft from packs already dealt: <paramref name="packs"/>[seat][round], every pack the same size.</summary>
	public static DraftState Create(IReadOnlyList<IReadOnlyList<IReadOnlyList<string>>> packs) =>
		packs.SelectMany(seat => seat).Select(p => p.Count).Distinct().Count() > 1
			? throw new ArgumentException(
				"every pack must be the same size, or a seat runs out first",
				nameof(packs)
			)
			: new(
				[
					.. packs.Select(seat => new DraftSeat(
						[.. seat[0]],
						[.. seat.Skip(1).Select(p => p.ToImmutableList())],
						[]
					)),
				],
				Round: 0
			);

	/// <summary>
	/// One pick per seat, all at once, as a real draft does; <paramref name="picks"/>[i] is an INDEX into
	/// seat i's pack — a pack can hold two of a name. Then the packs pass: to the next seat in rounds 0
	/// and 2, to the previous in round 1; an empty pack opens each seat's next.
	/// </summary>
	public static DraftState Apply(DraftState state, IReadOnlyList<int> picks)
	{
		if (picks.Count != state.Seats.Count)
			throw new ArgumentException(
				$"{picks.Count} picks for {state.Seats.Count} seats",
				nameof(picks)
			);
		var taken = state
			.Seats.Select(
				(seat, i) =>
					picks[i] >= 0 && picks[i] < seat.Pack.Count
						? seat with
						{
							Picks = seat.Picks.Add(seat.Pack[picks[i]]),
							Pack = seat.Pack.RemoveAt(picks[i]),
						}
						: throw new ArgumentOutOfRangeException(
							nameof(picks),
							$"seat {i} picked {picks[i]} from a pack of {seat.Pack.Count}"
						)
			)
			.ToImmutableList();

		var n = taken.Count;
		if (!taken[0].Pack.IsEmpty)
		{
			var from = state.Round % 2 == 0 ? -1 : 1; // left: seat i receives seat i-1's pack
			return state with
			{
				Seats =
				[
					.. taken.Select(
						(seat, i) => seat with { Pack = taken[(i + from + n) % n].Pack }
					),
				],
			};
		}
		return new(
			[
				.. taken.Select(seat =>
					seat.Unopened.IsEmpty
						? seat
						: seat with
						{
							Pack = seat.Unopened[0],
							Unopened = seat.Unopened.RemoveAt(0),
						}
				),
			],
			state.Round + 1
		);
	}

	/// <summary>
	/// The next pick for everyone: seat 0 takes <paramref name="seat0"/> (Shayne at the table), or picks
	/// as a bot when it is null (the console's all-bot draft); every other seat is a bot.
	/// </summary>
	public static DraftState Next(
		DraftState state,
		IReadOnlyDictionary<string, double> ratings,
		int? seat0 = null
	) =>
		Apply(
			state,
			[
				.. state.Seats.Select(
					(seat, i) =>
						i == 0 && seat0 is { } pick ? pick : BotPick(seat.Pack, seat.Picks, ratings)
				),
			]
		);

	/// <summary>Every seat a bot, to the end.</summary>
	public static DraftState RunBots(DraftState state, IReadOnlyDictionary<string, double> ratings)
	{
		while (!state.IsComplete)
			state = Next(state, ratings);
		return state;
	}

	/// <summary>
	/// A bot's pick (Shayne, 2026-10-29): the highest rating, in points above the table's median, plus
	/// the lean — <see cref="LaneWeight"/> × the ramp for a card inside its two colours, minus as much
	/// outside; a colourless card commits nothing and is neither, and while the bot has fewer than two
	/// colours a card may bring in the missing one. The top score; a tie takes the first,
	/// so a seeded draft replays. A card missing from the table (a cube's, later) is rated at the median.
	/// A basic land comes last, whatever its rating: deck building gives basics free, and 17lands' games-in-
	/// hand rate for one means nothing (a Forest read 56.5 and the bots took 114 in 10 drafts).
	/// </summary>
	public static int BotPick(
		IReadOnlyList<string> pack,
		IReadOnlyList<string> picks,
		IReadOnlyDictionary<string, double> ratings
	)
	{
		var Points = PointsOf(ratings);
		var ramp = Math.Min(1.0, (double)picks.Count / LaneRampPicks);
		var lane = ramp > 0 ? Lane(picks, Points) : [];

		var (best, bestScore) = (0, double.MinValue);
		for (var i = 0; i < pack.Count; i++)
		{
			var colors = ColorsOf(pack[i]);
			var score = IsBasic(pack[i])
				? BasicScore
				: Points(pack[i])
					+ (
						ramp == 0 || colors.Count == 0 ? 0
						: colors.Count(c => !lane.Contains(c)) <= 2 - lane.Count ? LaneWeight * ramp
						: -LaneWeight * ramp
					);
			if (score > bestScore)
				(best, bestScore) = (i, score);
		}
		return best;
	}

	/// <summary>
	/// The two colours a seat has invested the most VALUE in — MtgSimulator's measured version of "its
	/// top two by picks": a below-median card argues for nothing, so late filler can't set the lane.
	/// Fewer than two while no card of a second colour has been worth taking.
	/// </summary>
	public static IReadOnlyList<ManaColor> Lane(
		IReadOnlyList<string> picks,
		Func<string, double> points
	) =>
		[
			.. picks
				.SelectMany(card =>
					ColorsOf(card).Select(c => (c, worth: Math.Max(0, points(card))))
				)
				.Where(x => x.worth > 0)
				.GroupBy(x => x.c)
				.Select(g => (Color: g.Key, Worth: g.Sum(x => x.worth)))
				.OrderByDescending(x => x.Worth)
				.ThenBy(x => x.Color)
				.Take(2)
				.Select(x => x.Color),
		];

	/// <summary>The two colours a seat leans into, read from a ratings table — what a bot's next pick sees.</summary>
	public static IReadOnlyList<ManaColor> Lane(
		IReadOnlyList<string> picks,
		IReadOnlyDictionary<string, double> ratings
	) => Lane(picks, PointsOf(ratings));

	/// <summary>A card's points above the table's median (a missing card: the median, 0).</summary>
	static Func<string, double> PointsOf(IReadOnlyDictionary<string, double> ratings)
	{
		var median = CardRatings.Median(ratings.Values);
		return card => 100 * (ratings.GetValueOrDefault(card, median) - median);
	}

	const double BasicScore = -1_000;

	static bool IsBasic(string card) => SealedBuilder.Basics.Contains(card);

	/// <summary>A card's colours (its whole card, as Scryfall gives them — a land has none).</summary>
	public static IReadOnlyList<ManaColor> ColorsOf(string card) =>
		[.. (CardRegistry.Get(card).Data.Colors ?? []).Select(ManaCost.ColorOf)];
}
