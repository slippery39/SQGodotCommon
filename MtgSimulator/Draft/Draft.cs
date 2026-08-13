using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Booster — MTG style: open a pack, pick one card, pass the remainder to the next
/// seat, repeat until the pack is empty, then open the next pack.
/// Digital — Hearthstone/Arena style: each seat is offered N cards, picks one, and
/// is offered a fresh N. Seats never interact.
/// </summary>
public enum DraftFormat
{
	Booster,
	Digital,
}

/// <summary>
/// One drafter. <see cref="Queue"/> holds this seat's unopened packs (Booster) or its
/// future offers (Digital) — both are pre-generated at <see cref="Draft.Create"/> time,
/// which is what lets one advance function serve both formats.
/// </summary>
public sealed record DraftSeat(
	ImmutableList<Card> Offer,
	ImmutableList<ImmutableList<Card>> Queue,
	ImmutableList<Card> Pool
);

/// <summary>
/// A draft in progress. Plain immutable data — deliberately NOT a GameState. Drafting
/// needs none of the action stack, pipelines, choice resolution, or event log, and the
/// cards here are owner-agnostic templates that never enter a GameState.
///
/// <see cref="Round"/> increments only when seats open their next group, so for Booster
/// it is effectively the pack number (used to alternate pass direction).
/// </summary>
public sealed record DraftState(DraftFormat Format, ImmutableList<DraftSeat> Seats, int Round)
{
	/// Seats are symmetric — they always run out of cards on the same pick.
	public bool IsComplete => Seats[0].Offer.IsEmpty;
}

public static class Draft
{
	/// How many drafted spells make the deck. Shared so training and the trained picker
	/// reason about the same cards BuildDeck will actually play.
	public const int DefaultMaxSpells = 27;

	/// <summary>
	/// Deals a whole draft up front from <paramref name="seed"/>. Every pack and every
	/// digital offer is fixed here, so the draft is fully reproducible: the advance
	/// functions below are pure and consume no randomness.
	///
	/// Lands are excluded from packs — <see cref="BuildDeck"/> supplies the mana base.
	/// </summary>
	public static DraftState Create(
		DraftFormat format,
		IReadOnlyList<Card> cardPool,
		int seed,
		int seatCount = 8,
		int packSize = 15,
		int packCount = 3,
		int offerSize = 3,
		int poolSize = 23
	)
	{
		if (seatCount < 1)
			throw new ArgumentOutOfRangeException(nameof(seatCount), "Need at least one seat.");

		var groupSize = format == DraftFormat.Booster ? packSize : offerSize;
		var groupCount = format == DraftFormat.Booster ? packCount : poolSize;
		if (groupSize < 1 || groupCount < 1)
			throw new ArgumentOutOfRangeException(
				nameof(cardPool),
				"Pack/offer size and count must both be at least 1."
			);

		var source = cardPool.Where(c => !c.HasSubtype("Land")).ToList();
		if (source.Count < groupSize)
			throw new ArgumentException(
				$"Card pool has {source.Count} non-land cards; need at least {groupSize} to fill a pack.",
				nameof(cardPool)
			);

		var rng = new Random(seed);
		var seats = Enumerable
			.Range(0, seatCount)
			.Select(_ =>
			{
				var groups = Groups(rng, source, groupSize, groupCount);
				return new DraftSeat(groups[0], groups.RemoveAt(0), ImmutableList<Card>.Empty);
			})
			.ToImmutableList();

		return new DraftState(format, seats, Round: 0);
	}

	/// N distinct cards per group — same sampling idiom as CardPool.BuildRandomDeck.
	private static ImmutableList<ImmutableList<Card>> Groups(
		Random rng,
		IReadOnlyList<Card> source,
		int size,
		int count
	) =>
		Enumerable
			.Range(0, count)
			.Select(_ => source.OrderBy(_ => rng.Next()).Take(size).ToImmutableList())
			.ToImmutableList();

	/// <summary>
	/// Applies one pick per seat simultaneously — that is how real booster draft works,
	/// and it keeps the seats symmetric so <see cref="DraftState.IsComplete"/> can check
	/// a single seat.
	///
	/// <paramref name="picks"/>[i] is an INDEX into Seats[i].Offer, never a Card:
	/// Card is a record, so two copies of the same template in one pack compare equal
	/// and picking by value would remove the wrong one.
	/// </summary>
	public static DraftState ApplyPicks(DraftState state, IReadOnlyList<int> picks)
	{
		if (picks.Count != state.Seats.Count)
			throw new ArgumentException(
				$"Expected {state.Seats.Count} picks, got {picks.Count}.",
				nameof(picks)
			);

		var taken = state
			.Seats.Select(
				(seat, i) =>
				{
					var pick = picks[i];
					if (pick < 0 || pick >= seat.Offer.Count)
						throw new ArgumentOutOfRangeException(
							nameof(picks),
							$"Seat {i} picked index {pick} from an offer of {seat.Offer.Count}."
						);
					return seat with
					{
						Pool = seat.Pool.Add(seat.Offer[pick]),
						Offer = seat.Offer.RemoveAt(pick),
					};
				}
			)
			.ToImmutableList();

		// Booster: a pack with cards left keeps circulating. Direction alternates per pack.
		if (state.Format == DraftFormat.Booster && !taken[0].Offer.IsEmpty)
		{
			var n = taken.Count;
			var dir = state.Round % 2 == 0 ? 1 : -1;
			var passed = Enumerable
				.Range(0, n)
				.Select(i => taken[i] with { Offer = taken[((i - dir) % n + n) % n].Offer })
				.ToImmutableList();
			return state with { Seats = passed };
		}

		// Digital always; Booster once the pack is exhausted. Each seat opens its own next group.
		var refilled = taken
			.Select(seat =>
				seat with
				{
					Offer = seat.Queue.IsEmpty ? ImmutableList<Card>.Empty : seat.Queue[0],
					Queue = seat.Queue.IsEmpty ? seat.Queue : seat.Queue.RemoveAt(0),
				}
			)
			.ToImmutableList();
		return state with { Seats = refilled, Round = state.Round + 1 };
	}

	/// <summary>
	/// Drives an all-AI draft to the end. A draft with a human seat should not use this —
	/// the caller runs its own loop and supplies that seat's index from the UI, so no
	/// presentation code is needed in this library.
	/// </summary>
	public static DraftState RunToCompletion(DraftState state, IReadOnlyList<DraftPicker> pickers)
	{
		if (pickers.Count != state.Seats.Count)
			throw new ArgumentException(
				$"Expected {state.Seats.Count} pickers, got {pickers.Count}.",
				nameof(pickers)
			);

		while (!state.IsComplete)
			state = ApplyPicks(
				state,
				state.Seats.Select((seat, i) => pickers[i](seat.Offer, seat.Pool)).ToList()
			);
		return state;
	}

	/// <summary>
	/// Turns a drafted pool into a playable deck: the first <paramref name="maxSpells"/>
	/// non-lands in pick order, padded to <paramref name="deckSize"/> with Plains.
	/// Owner is stamped here, so this must be called per game — a seat is Player 1 in
	/// some games and Player 2 in others.
	///
	/// Mirrors CardPool.BuildRandomDeck's land padding but pads to a fixed total instead
	/// of using a fixed land count, so a short pool still yields a legal deck.
	/// </summary>
	public static IReadOnlyList<Card> BuildDeck(
		IReadOnlyList<Card> draftPool,
		int ownerId,
		int deckSize = 40,
		int maxSpells = DefaultMaxSpells
	)
	{
		var spells = draftPool
			.Where(c => !c.HasSubtype("Land"))
			.Take(maxSpells)
			.Select(template => template with { OwnerId = ownerId, ControllerId = ownerId })
			.ToList();
		var lands = Enumerable
			.Range(0, Math.Max(0, deckSize - spells.Count))
			.Select(_ => CardLibrary.Plains() with { OwnerId = ownerId, ControllerId = ownerId });
		return spells.Concat(lands).ToList();
	}
}
