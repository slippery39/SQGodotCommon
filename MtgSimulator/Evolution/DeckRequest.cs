using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// One resolved request: the deck, the constraints it compiled to, and what could not be met.
/// </summary>
/// <param name="Matched">
/// Which demands a theme string resolved to. **Echoed back on purpose** — a substring query is
/// convenient and ambiguous, and a request that quietly matched the wrong concept is far worse than
/// one that says which it took.
/// </param>
/// <param name="Problems">
/// Constraints that could not be satisfied. A request that returns a deck AND a problem is honest:
/// the deck is what was buildable, and the problem says what was dropped to build it.
/// </param>
public sealed record DeckRequestResult(
	Decklist? Deck,
	DeckCore Core,
	IReadOnlyList<string> Matched,
	IReadOnlyList<string> Problems
)
{
	public bool Succeeded => Deck is not null && Problems.Count == 0;
}

/// <summary>
/// **"Build me a deck that ..." — a constraint, compiled into a deck.**
///
/// The unifying observation is that a <see cref="DeckCore"/> is already just *a list of slots*, so
/// every kind of constraint can be reduced to slots plus an optional curve:
///
/// | Request | Compiles to |
/// |---|---|
/// | "a Dragonstorm deck" | <see cref="DeckCore.For"/> — slots derived from that card's demands |
/// | "a graveyard deck" | <see cref="DeckCore.ForDemand"/> — slots derived from the demand itself |
/// | "a midrange deck" | a curve band; no slots at all |
/// | "Liliana + Tarmogoyf, midrange" | two required cards, no derived slots, curve fill |
///
/// **Nothing infers an archetype LABEL.** "Dragonstorm needs dragons and ramp" is not knowledge
/// added here — `PoolFeatures` harvested both demands off the card the day it was written, and this
/// only asks. Equally, a request naming cards that ask nothing answerable produces no slots and
/// falls through to a curve-filled pile of good cards. **That is the correct answer**, not a
/// failure: Tarmogoyf and Liliana of the Veil generate no core in this pool, so "a deck with
/// Liliana and Tarmogoyf" IS a generic midrange deck, and it costs no special case to say so.
/// </summary>
/// <param name="MustInclude">
/// Card names the deck must play. Each contributes a hard slot AND the support its own demands
/// imply, so "a Dragonstorm deck that uses Dragonstorm" guarantees the card and derives the dragons
/// and the rituals.
/// </param>
/// <param name="Themes">
/// Case-insensitive substrings matched against demand descriptions — "graveyard", "Goblin",
/// "Artifact". Stringly on purpose: this is the human-facing query, not model internals, and it is
/// the layer where a person should be able to type what they mean. Everything it resolves to is
/// reported in <see cref="DeckRequestResult.Matched"/>.
/// </param>
/// <param name="Curve">
/// A preference, not a constraint. It sets the land count and breaks ties in the fill; it never
/// evicts a slot card. "Dragonstorm with a midrange curve" is a real conflict — storm wants a low
/// curve — and the archetype wins, because the curve is the weaker statement of intent.
/// </param>
public sealed record DeckRequest(
	IReadOnlyList<string> MustInclude,
	IReadOnlyList<string> Themes,
	DeckBuilder.DeckProfile Curve = DeckBuilder.DeckProfile.Any
)
{
	public static DeckRequest ForCards(params string[] cards) => new(cards, []);

	public static DeckRequest ForTheme(string theme) => new([], [theme]);

	public static DeckRequest ForCurve(DeckBuilder.DeckProfile profile) => new([], [], profile);

	/// How many copies of an explicitly named card the deck must play.
	private const int RequiredCopies = 4;

	public DeckRequestResult Resolve(
		PoolFeatures features,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		int seed = 0
	)
	{
		var rng = new Random(seed);
		var present = spells.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
		var slots = new List<CoreSlot>();
		var matched = new List<string>();
		var problems = new List<string>();

		foreach (var name in MustInclude)
		{
			if (!present.Contains(name))
			{
				problems.Add($"'{name}' is not in this pool");
				continue;
			}

			slots.Add(
				new CoreSlot(
					$"Required: {name}",
					[name],
					Math.Min(RequiredCopies, Decklist.MaxCopies)
				)
			);

			// The card's OWN derived support. Its payoff slot is dropped — the card is already
			// guaranteed above, and keeping it would let an interchangeable payoff satisfy the
			// requirement instead of the card that was actually asked for.
			if (DeckCore.For(features, name) is { } derived)
				slots.AddRange(derived.Slots.Skip(1));
			else
				matched.Add($"'{name}' asks nothing answerable — contributing no support slots");
		}

		foreach (var theme in Themes)
		{
			var hits = Enumerable
				.Range(0, features.Demands.Count)
				.Where(features.Informative)
				.Where(d =>
					features.Describe(d).Contains(theme, StringComparison.OrdinalIgnoreCase)
				)
				// Narrowest first: a broad demand answered by most of the pool constrains nothing, so
				// when a word matches several, the specific one is the better reading of the request.
				.OrderBy(features.SuppliersInPool)
				.ToList();

			if (hits.Count == 0)
			{
				problems.Add($"no demand matches theme '{theme}'");
				continue;
			}

			var chosen = hits[0];
			matched.Add(
				$"'{theme}' -> {features.Describe(chosen)} ({features.SuppliersInPool(chosen)} suppliers)"
			);

			if (DeckCore.ForDemand(features, chosen) is { } themed)
				slots.AddRange(themed.Slots);
			else
				problems.Add($"theme '{theme}' matched a demand no deck can be built around");
		}

		var core = new DeckCore(Describe(), Disjoint(slots));

		if (core.Slots.Sum(s => s.MinCopies) > Decklist.DeckSize - Decklist.MinLands)
		{
			problems.Add(
				"the constraints cannot fit in one deck: "
					+ string.Join(" + ", core.Slots.Select(s => $"{s.MinCopies}x {Short(s.Role)}"))
			);
			return new DeckRequestResult(null, core, matched, problems);
		}

		var coreCards = core.Slots.SelectMany(s => s.Cards).ToHashSet(StringComparer.Ordinal);
		var lands =
			Curve != DeckBuilder.DeckProfile.Any || coreCards.Count == 0
				? DeckBuilder.LandsForProfile(Curve, rng)
				: DeckBuilder.LandsForConcept(spells.Where(c => coreCards.Contains(c.Name)), rng);

		var deck = core.Complete(Decklist.Empty(Describe()) with { Lands = lands }, spells, values);

		if (deck.Validate() is { } invalid)
			problems.Add($"built an illegal deck: {invalid}");
		else if (!core.Holds(deck))
			problems.Add($"the deck misses its own core: {string.Join(", ", core.Missing(deck))}");

		return new DeckRequestResult(deck, core, matched, problems);
	}

	/// <summary>
	/// Slots made pairwise disjoint, earliest first.
	///
	/// **Slots are counted independently**, so a card appearing in two of them satisfies both off
	/// one copy — the same rule that strips payoffs from their own support slot. Merging two cores
	/// makes overlap the normal case rather than the exception: "an artifact deck with Atog"
	/// derives the artifact slot twice.
	///
	/// Earliest wins because the request is read in order: an explicitly named card outranks a
	/// theme that happens to include it.
	/// </summary>
	private static IReadOnlyList<CoreSlot> Disjoint(IReadOnlyList<CoreSlot> slots)
	{
		var taken = new HashSet<string>(StringComparer.Ordinal);
		var result = new List<CoreSlot>();

		foreach (var slot in slots)
		{
			var free = slot.Cards.Except(taken).ToImmutableHashSet(StringComparer.Ordinal);
			if (free.IsEmpty)
				continue;

			taken.UnionWith(free);
			result.Add(
				slot with
				{
					Cards = free,
					MinCopies = Math.Min(slot.MinCopies, free.Count * Decklist.MaxCopies),
				}
			);
		}

		return result;
	}

	public string Describe()
	{
		var parts = new List<string>();
		if (MustInclude.Count > 0)
			parts.Add(string.Join(" + ", MustInclude));
		if (Themes.Count > 0)
			parts.Add(string.Join("/", Themes));
		if (Curve != DeckBuilder.DeckProfile.Any)
			parts.Add(Curve.ToString());
		return parts.Count == 0 ? "Any" : string.Join(", ", parts);
	}

	private static string Short(string role) => role.Length > 40 ? role[..40] : role;
}
