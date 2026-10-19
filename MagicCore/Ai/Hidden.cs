using ImmutableGameObjects;
using MagicCore.Cards;

namespace MagicCore.Ai;

/// <summary>
/// What the search may know of the hidden zones (story AI-3). It decides from ONE sampled position,
/// taken at its root, so its tree and its rollouts see the same cards; the decision is then played in
/// the real game. Who knows each card is the engine's <see cref="Card.KnownTo"/>.
/// </summary>
public static class Hidden
{
	public enum Library
	{
		/// <summary>The libraries as they really are — the AI sees its own future (AI-2's first cut).</summary>
		RealOrder,

		/// <summary>
		/// Every card whose place the searching player doesn't know is shuffled among the unknown
		/// places; the known stay put — a surveil's keep, a mulligan's bottom, a card seen going on top.
		/// </summary>
		ShuffleUnknown,
	}

	public enum Hand
	{
		/// <summary>The opponent's hand as it really is — a casting opponent casts what it really holds (AI-3.1).</summary>
		Real,

		/// <summary>
		/// The opponent's hand cards the searching player hasn't seen are drawn again from those and the
		/// cards of its library whose place is unknown; the seen ones stay. A card drawn from the library
		/// swaps places with the hand card it replaces, so the rest of the library keeps its order and the
		/// hand is measured apart from the library's mode (story AI-3.4, Shayne).
		/// </summary>
		ShuffleUnseen,
	}

	/// <summary>
	/// The position the search decides from. Under ShuffleUnknown both libraries are reshuffled where
	/// <paramref name="me"/> can't know them, and under <see cref="Hand.ShuffleUnseen"/> the opponent's
	/// unseen hand is drawn again (before the libraries) — from a seed of the state's, never from its
	/// RNG, which the real game draws from next — and the sample gets a fresh RNG seed, so a shuffle
	/// inside a rollout doesn't predict the real game's (so either mode also hides that foresight). The real position when a pending choice would offer different
	/// ids in the sample (a search lists the first copy of each name in library order): the search's
	/// pick must be one the real choice accepts.
	/// </summary>
	public static GameState Sample(GameState s, int me, Library mode, Hand hand = Hand.Real)
	{
		if (mode == Library.RealOrder && hand == Hand.Real)
			return s;
		// ponytail: seeded by the game's seed, which only moves when the game uses randomness — so a
		// turn's decisions mostly share a seed, but the sample still changes as the library does (a
		// draw), and an unseeded game (seed 0) samples afresh at every decision. Seed by turn and
		// library size if plans ever need one library for the whole turn.
		var rng = s.RngSeed == 0 ? new Random() : new Random(unchecked(s.RngSeed * 31 + me));
		var sample = s;
		if (hand == Hand.ShuffleUnseen)
			sample = ShuffleUnseenHand(sample, s.OpponentOf(me), rng);
		if (mode == Library.ShuffleUnknown)
			foreach (var owner in new[] { s.Player1Id(), s.Player2Id() })
				sample = ShuffleUnknown(
					sample,
					owner,
					owner == me ? Knowers.Owner : Knowers.Opponent,
					rng
				);
		sample = sample with { RngSeed = rng.Next(1, int.MaxValue) };
		return
			s.GetPendingChoice() is { } real
			&& !real
				.Options.Select(o => o.Id)
				.SequenceEqual(sample.GetPendingChoice()!.Options.Select(o => o.Id))
			? s
			: sample;
	}

	/// <summary>The opponent's unseen hand drawn again from its unseen cards — <see cref="Hand.ShuffleUnseen"/>.</summary>
	static GameState ShuffleUnseenHand(GameState s, int owner, Random rng)
	{
		var (handId, libraryId) = (
			s.ZoneId(owner, ZoneType.Hand),
			s.ZoneId(owner, ZoneType.Library)
		);
		var (hand, library) = (
			s.GetChildrenIds(handId).ToArray(),
			s.GetChildrenIds(libraryId).ToArray()
		);
		// Unseen by me, its opponent: in the hand, a card I haven't seen; in the library, a place I don't know.
		bool Unseen(int id) => (s.GetCard(id).KnownTo & Knowers.Opponent) == 0;
		var handSlots = Enumerable.Range(0, hand.Length).Where(i => Unseen(hand[i])).ToArray();
		var librarySlots = Enumerable
			.Range(0, library.Length)
			.Where(i => Unseen(library[i]))
			.ToArray();
		int[] pool = [.. handSlots.Select(i => hand[i]), .. librarySlots.Select(i => library[i])];
		rng.Shuffle(pool);
		var drawn = pool.Take(handSlots.Length).ToHashSet();
		var leaving = handSlots.Where(i => !drawn.Contains(hand[i])).ToArray();
		var arriving = librarySlots.Where(i => drawn.Contains(library[i])).ToArray();
		if (leaving.Length == 0)
			return s;
		rng.Shuffle(leaving); // which arriving card's place each leaving card takes — not by hand order
		for (var k = 0; k < leaving.Length; k++)
			(hand[leaving[k]], library[arriving[k]]) = (library[arriving[k]], hand[leaving[k]]);
		// Out and back in, as ShuffleUnknown does: MoveObject appends, so this is the new order of both.
		foreach (var id in hand.Concat(library))
			s = s.MoveObject(id, 0);
		foreach (var id in hand)
			s = s.MoveObject(id, handId);
		foreach (var id in library)
			s = s.MoveObject(id, libraryId);
		return s;
	}

	static GameState ShuffleUnknown(GameState s, int owner, Knowers mine, Random rng)
	{
		var libraryId = s.ZoneId(owner, ZoneType.Library);
		var ids = s.GetChildrenIds(libraryId).ToArray();
		var places = Enumerable
			.Range(0, ids.Length)
			.Where(i => (s.GetCard(ids[i]).KnownTo & mine) == 0)
			.ToArray();
		var unknown = places.Select(i => ids[i]).ToArray();
		rng.Shuffle(unknown);
		for (var k = 0; k < places.Length; k++)
			ids[places[k]] = unknown[k];
		// Out and back in, as MagicState.Shuffle does: MoveObject appends, so this is the new order.
		foreach (var id in ids)
			s = s.MoveObject(id, 0);
		foreach (var id in ids)
			s = s.MoveObject(id, libraryId);
		return s;
	}
}
