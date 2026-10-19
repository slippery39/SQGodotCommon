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

	/// <summary>
	/// The position the search decides from. Under ShuffleUnknown both libraries are reshuffled where
	/// <paramref name="me"/> can't know them — from a seed of the state's, never from its RNG, which the
	/// real game draws from next — and the sample gets a fresh RNG seed, so a shuffle inside a rollout
	/// doesn't predict the real game's. The real position when a pending choice would offer different
	/// ids in the sample (a search lists the first copy of each name in library order): the search's
	/// pick must be one the real choice accepts.
	/// </summary>
	public static GameState Sample(GameState s, int me, Library mode)
	{
		if (mode == Library.RealOrder)
			return s;
		// ponytail: seeded by the game's seed, which only moves when the game uses randomness — so a
		// turn's decisions mostly share a seed, but the sample still changes as the library does (a
		// draw), and an unseeded game (seed 0) samples afresh at every decision. Seed by turn and
		// library size if plans ever need one library for the whole turn.
		var rng = s.RngSeed == 0 ? new Random() : new Random(unchecked(s.RngSeed * 31 + me));
		var sample = s;
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
