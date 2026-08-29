namespace MtgCore;

/// <summary>
/// The draftable card sets, looked up by code. Same shape as DeckRegistry — a static list
/// plus a lookup that throws on an unknown name.
///
/// Swapping which set is drafted means passing a different CardSet to Draft.Create; the
/// plumbing for that already existed, since Draft.Create has always taken an
/// IReadOnlyList&lt;Card&gt; card pool.
///
/// Sets are kept as separate pools rather than merged into one. Merging would invalidate the
/// trained draft model (which is keyed by card name and does not generalise across pools) and
/// would mix cards designed for different formats.
///
/// <see cref="Combined"/> is the deliberate exception — see its own comment for why that
/// reasoning does not bind a mode that uses the model only as a starting prior.
/// </summary>
public static class SetRegistry
{
	/// <summary>
	/// The original CardLibrary.All pool — the ad-hoc set of cards assembled to support the
	/// preconstructed decks. Registered as a set so the existing draft and its trained model
	/// keep working unchanged.
	/// </summary>
	public const string LegacyCode = "LEG";

	/// The union of every registered set. Not itself registered — see <see cref="Combined"/>.
	public const string CombinedCode = "ALL";

	public static IReadOnlyList<CardSet> All { get; } =
		[new CardSet(LegacyCode, "Legacy", CardLibrary.All), Hollowmere.Set, CoresetCube.Set];

	/// The set used when a caller does not specify one.
	public static CardSet Default => Get(LegacyCode);

	public static CardSet Get(string code) =>
		string.Equals(code, CombinedCode, StringComparison.OrdinalIgnoreCase)
			? Combined
			: All.FirstOrDefault(s =>
				string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase)
			)
				?? throw new ArgumentException(
					$"Unknown set: {code}. Known sets: {string.Join(", ", All.Select(s => s.Code))}, {CombinedCode}",
					nameof(code)
				);

	/// Every set including <see cref="Combined"/> — what a mode offers as a menu.
	public static IReadOnlyList<CardSet> AllIncludingCombined() => [.. All, Combined];

	/// <summary>
	/// Every registered set as one pool — a larger "format" to build decks in, at no authoring
	/// cost. Lazy because it walks every set and only the evolution mode asks for it.
	///
	/// This does NOT contradict the no-merging rule above. That rule protects the trained
	/// DRAFT PICKER, which is keyed by card name and would score a whole set at the prior and
	/// never pick from it. A merged pool is fine wherever the model is a starting prior rather
	/// than the pick policy — evolution measures its own fitness by playing games.
	///
	/// **Duplicate names are resolved LAST REGISTERED WINS.** Eighteen names collide — sixteen
	/// LEG/CSC (Lightning Bolt, Doom Blade, Krenko, Llanowar Elves among them), one LEG/HLM
	/// (Faithless Looting) and one HLM/CSC (Corpse Knight). Two cards sharing a name cannot
	/// both exist here: a decklist, CardStat, CardValue and the runner's cardNames map are all
	/// name-keyed, so the name has to identify one card. Two cards printed with the same name
	/// do the same thing, and the most recent printing is the current one — which given the
	/// registration order means CSC's version plays, except for Faithless Looting where HLM is
	/// the later registration.
	/// <see cref="CombinedReplacements"/> names them, because a silent behaviour swap between
	/// two same-named cards is otherwise invisible.
	/// </summary>
	public static CardSet Combined => Built.Value.Set;

	/// Names that appeared in more than one set, with the set whose version won.
	public static IReadOnlyList<(string Name, string WinningSet)> CombinedReplacements =>
		Built.Value.Replaced;

	private static readonly Lazy<(
		CardSet Set,
		IReadOnlyList<(string Name, string WinningSet)> Replaced
	)> Built = new(BuildCombined);

	private static (CardSet, IReadOnlyList<(string, string)>) BuildCombined()
	{
		// Insertion-ordered so the pool is stable across runs; a later Set overwrites the
		// value but keeps the original position, which is irrelevant to correctness and keeps
		// a diff between two runs readable.
		var byName = new Dictionary<string, Card>(StringComparer.OrdinalIgnoreCase);
		var collisions = new List<(string, string)>();

		foreach (var set in All)
		{
			foreach (var card in set.Cards)
			{
				if (byName.ContainsKey(card.Name))
					collisions.Add((card.Name, set.Code));
				byName[card.Name] = card;
			}
		}

		return (new CardSet(CombinedCode, "All Sets", byName.Values.ToList()), collisions);
	}
}
