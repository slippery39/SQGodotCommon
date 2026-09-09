using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// The colours a deck is allowed to play — its manabase's promise.
///
/// A deck-level concept, unlike a card's <see cref="Card.ColorPips"/>: the pips say what one card
/// costs, an identity says what a whole manabase can support. A card is legal in an identity when
/// its pips are a SUBSET of it, so colourless cards are legal everywhere and a gold card only in
/// the one pair that covers both its colours.
///
/// **This exists because "a uniformly random deck" stopped being a neutral control when colour
/// arrived.** A random deck over a five-colour pool is a five-colour pile, and its manabase gives
/// about five sources per colour — so a double-pip card measures as unplayable no matter how
/// strong it is, and every committed card in the format reads deflated. `ConstructedValuesStore`
/// already records what deflation costs: a payoff seeded into decks that cannot support it loses,
/// its rate sinks, and the builder then never picks it again. Seeding an identity is what makes a
/// random deck neutral again — neutral now means "random within a manabase that can cast this",
/// not "random across the pool".
/// </summary>
public sealed record ColorIdentity(string Code, ImmutableArray<ManaColor> Colors)
{
	/// <summary>True if this identity's manabase can pay for <paramref name="card"/>.</summary>
	public bool Allows(Card card)
	{
		var pips = card.ColorPips;
		if (pips.IsEmpty)
			return true;

		foreach (var color in ManaPool.Colors)
			if (pips[color] > 0 && !Colors.Contains(color))
				return false;

		return true;
	}

	public override string ToString() => Code;

	private static ColorIdentity Of(params ManaColor[] colors) =>
		new(string.Concat(colors.Select(ManaPool.Symbol)), [.. colors]);

	/// The five single-colour identities.
	public static IReadOnlyList<ColorIdentity> Mono { get; } =
		[.. ManaPool.Colors.Select(c => Of(c))];

	/// The ten two-colour identities.
	public static IReadOnlyList<ColorIdentity> Pairs { get; } =
		[.. ManaPool.Colors.SelectMany((a, i) => ManaPool.Colors.Skip(i + 1), (a, b) => Of(a, b))];

	/// <summary>
	/// The fifteen identities a deck is normally built in: five mono and ten pairs.
	///
	/// Three-colour identities are deliberately absent. They can be measured — and the wildcard
	/// slot in the evolver exists to ask whether they are viable at all — but their games must not
	/// feed the POOLED card table, because a three-colour manabase fails so often that the table
	/// would be measuring the mana rather than the card, which is the exact deflation this whole
	/// arrangement exists to avoid.
	/// </summary>
	public static IReadOnlyList<ColorIdentity> Standard { get; } = [.. Mono, .. Pairs];

	/// <summary>
	/// Every card in <paramref name="pool"/> this identity can cast.
	///
	/// ponytail: exposure is uneven and it is arithmetic, not a bug. A colourless card is legal in
	/// all 15 identities, a mono-colour card in 5 (its own plus its four pairs), and a GOLD card in
	/// exactly 1 — so with decks split evenly, gold cards accumulate about a fifteenth of the games
	/// a colourless card does and shrinkage will hold them at the prior forever. The fix is to
	/// allocate more decks to pair identities than to mono ones; do it once a run has measured the
	/// games-per-card spread, rather than guessing the ratio now.
	/// </summary>
	public IReadOnlyList<Card> Playable(IReadOnlyList<Card> pool) => [.. pool.Where(Allows)];
}
