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
	/// <summary>
	/// How many drafted spells make the deck. Shared so training, the trained picker and the
	/// Godot deck list all reason about the same cards BuildDeck will actually play.
	///
	/// 23 spells in a 40-card deck is **17 lands**, up from 13. The old figure was set for a
	/// low-curve pool where 13 was fine; the Core Set Cube's curve is much higher, and 13 lands
	/// meant getting stuck on three mana and never casting the top of the curve. Real limited
	/// Magic runs 17 lands in 40 for the same reason.
	///
	/// The three-land opening hand (SetupGameAction.OpeningHandLandCount) is what makes the
	/// difference bigger than it looks: with the opening hand fixed, the land density that
	/// actually decides whether you keep hitting drops is the REMAINDER of the library. At 13
	/// lands that was 10 in 33 (30%); at 17 it is 14 in 33 (42%).
	/// </summary>
	public const int DefaultMaxSpells = 23;

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
		var chosen = ChooseDeck(draftPool, maxSpells, deckSize);
		var spells = chosen
			.Spells.Select(template => template with { OwnerId = ownerId, ControllerId = ownerId })
			.ToList();

		// The manabase serves the SUPPORTED cards — the identity plus its splash — and not the
		// filler. A filler card was taken because it beats a surplus land, not because the deck can
		// cast it, and giving it sources would come straight out of the colours the deck plays.
		var supported = spells.Take(chosen.Supported).ToList();
		var lands = ManaBase.Build(
			supported.Count > 0 ? supported : spells,
			Math.Max(0, deckSize - spells.Count),
			ownerId
		);
		return spells.Concat(lands).ToList();
	}

	/// <summary>
	/// The spells a pool actually plays: the best <paramref name="maxSpells"/> the pool can CAST,
	/// chosen inside one <see cref="ColorIdentity"/>.
	///
	/// **This used to be the first N non-lands in pick order, and that is a five-colour pile.** A
	/// drafter who commits to a lane and then takes the best card from every other colour late
	/// played all of them, because arrival time was the only thing selecting the deck. Measured on
	/// 200 real CSC drafts: 4.97 colours per deck, and 20.9 of 23 cards uncastable.
	///
	/// Pick order is the only quality signal available here — a seat picked its first card first
	/// because it wanted it most, and threading a trained model into deck assembly would couple
	/// every caller to one. So each card an identity can play is worth <c>1 - index/poolSize</c>,
	/// the first pick being worth about one card and the last nearly nothing.
	///
	/// **That weight is then multiplied by how castable the card actually is in the manabase this
	/// deck would really build**, which is what stops the choice being a free lunch: a pair can only
	/// play more cards by splitting its sources, so every card it adds makes its other cards
	/// slightly worse. The trade prices itself and there is no coin to calibrate.
	///
	/// **A shortfall PENALTY was tried here first and was wrong twice over.** A mono deck's
	/// requirement can never exceed 22, so mono scored a zero penalty BY CONSTRUCTION while every
	/// pair paid 6-10 — and the standard being missed ("every card on curve 90% of the time") is one
	/// no real two-colour deck has ever met. The result was 1.03 colours per deck: the selector
	/// could not build a two-colour deck at all. Measured on one seat's pool, mono-white scored 8.9
	/// on 15 cards against WB's 2.1 on 22, when WB was plainly the better deck.
	/// </summary>
	public static IReadOnlyList<Card> ChooseSpells(
		IReadOnlyList<Card> draftPool,
		int maxSpells = DefaultMaxSpells,
		int deckSize = 40
	) => ChooseDeck(draftPool, maxSpells, deckSize).Spells;

	/// <summary>
	/// <see cref="ChooseSpells"/>, plus the identity it chose — which is what <see cref="BuildDeck"/>
	/// needs, because the MANABASE is built from the core alone and not from the filler below.
	/// </summary>
	/// <summary>
	/// How many off-colour cards a deck may SPLASH — play with real mana support behind them,
	/// rather than as the unsupported filler below.
	///
	/// Three because a splash is a few cards, not a third colour: past that the manabase stops
	/// being a two-colour deck with a splash and starts being a bad three-colour deck.
	/// </summary>
	private const int MaxSplash = 3;

	/// <summary>
	/// The spells a pool plays, and how many of them the manabase actually serves.
	///
	/// The first <c>Supported</c> entries are the identity's cards plus any splash, and the
	/// manabase is built from exactly those. Anything after them is filler — played because a
	/// 21st land is worse, not because the deck can cast it.
	/// </summary>
	internal readonly record struct ChosenDeck(IReadOnlyList<Card> Spells, int Supported);

	internal static ChosenDeck ChooseDeck(
		IReadOnlyList<Card> draftPool,
		int maxSpells = DefaultMaxSpells,
		int deckSize = 40
	)
	{
		var candidates = draftPool.Where(c => !c.HasSubtype("Land")).ToList();
		if (candidates.Count == 0)
			return new ChosenDeck([], 0);

		// The deck plays maxSpells whatever happens (see the filler below), so every candidate is
		// judged against the SAME land count. Judging each against its own would pay a shallow lane
		// for being shallow: fewer spells, more lands, better castability, higher score.
		var landCount = Math.Max(0, deckSize - Math.Min(maxSpells, candidates.Count));

		List<int> best = [];
		var bestScore = double.NegativeInfinity;

		// Ties resolve to ColorIdentity.Standard's order and to no splash before a splash, so a
		// pool that gains nothing from more colours stays put and the result is deterministic.
		foreach (var identity in ColorIdentity.Standard)
		foreach (var splash in Splashes(identity))
		foreach (var splashLimit in splash is null ? NoSplash : SplashSizes)
		{
			var picked = Fill(candidates, identity, splash, splashLimit, maxSpells);
			if (picked.Count == 0)
				continue;

			// The splash is IN the manabase here — that is what makes it a splash rather than a
			// dead card, and what makes it cost something. Every source it takes comes out of the
			// main colours, so the whole deck's castability pays for it.
			var sources = ManaBase.SourcesFor(picked.Select(i => candidates[i]), landCount);

			var score = 0.0;
			foreach (var i in picked)
				score +=
					(1.0 - (double)i / candidates.Count)
					* ManaBase.Castability(candidates[i], sources, landCount);

			if (score > bestScore)
				(best, bestScore) = (picked, score);
		}

		// **Short lane? Play the cards anyway.** A deck is maxSpells spells and the rest lands, and
		// a pool only ever holds enough cards for that — an identity holding 19 playables used to
		// produce a 19-spell deck with 21 lands, which is not a deckbuilding decision anybody would
		// make. Above about 18 lands almost any card beats another land, including one this deck
		// cannot cast at all, so the remaining slots go to the best picks left regardless of colour.
		//
		// They are appended, never interleaved, and they get NO sources — unlike a splash, which
		// earned its. That is what real limited does with its last few cards, and it is what stops
		// three filler cards dragging a 9/8 manabase into an 8/7/2 one.
		var supported = best.Count;
		var chosen = best.ToHashSet();
		var spells = best.Select(i => candidates[i]).ToList();
		for (var i = 0; i < candidates.Count && spells.Count < maxSpells; i++)
			if (!chosen.Contains(i))
				spells.Add(candidates[i]);

		return new ChosenDeck(spells, supported);
	}

	private static readonly int[] NoSplash = [0];
	private static readonly int[] SplashSizes = [1, 2, 3];

	/// <summary>
	/// No splash, then each colour the identity does not already have. ONE extra colour, never two
	/// — a four-colour manabase is not a deck anyone builds, and allowing it would let the search
	/// rediscover the five-colour pile this whole selection exists to prevent.
	/// </summary>
	private static IEnumerable<ManaColor?> Splashes(ColorIdentity identity)
	{
		yield return null;
		foreach (var color in ManaPool.Colors)
			if (!identity.Colors.Contains(color))
				yield return color;
	}

	/// <summary>
	/// Indices of the cards this identity plays, in pick order, taking at most
	/// <paramref name="splashLimit"/> cards that need the splash colour.
	/// </summary>
	private static List<int> Fill(
		IReadOnlyList<Card> candidates,
		ColorIdentity identity,
		ManaColor? splash,
		int splashLimit,
		int maxSpells
	)
	{
		var picked = new List<int>(maxSpells);
		var splashed = 0;

		for (var i = 0; i < candidates.Count && picked.Count < maxSpells; i++)
		{
			var card = candidates[i];
			if (identity.Allows(card))
			{
				picked.Add(i);
				continue;
			}

			if (splash is null || splashed >= splashLimit)
				continue;

			// Legal on the splash only if the splash colour is the ONLY thing it adds — a card
			// needing two colours the identity lacks is a second splash wearing one card's name.
			var needsOnly = ManaPool.Colors.All(c =>
				card.ColorPips[c] == 0 || c == splash || identity.Colors.Contains(c)
			);
			if (!needsOnly)
				continue;

			picked.Add(i);
			splashed++;
		}

		return picked;
	}
}
