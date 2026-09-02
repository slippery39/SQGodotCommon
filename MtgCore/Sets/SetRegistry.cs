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

	/// <summary>
	/// Hollowmere plus the Core Set Cube — every DESIGNED set, with the Legacy pool left out.
	///
	/// **Legacy is not a designed set and it distorts anything measured across the union.** It is
	/// the ad-hoc collection assembled to support the preconstructed decks, so it holds cards
	/// written to make a specific combo work rather than to a rate: Ancestral Recall reads +11.86
	/// and Steppe Lynx +14.32 in isolation, well clear of anything in HLM or CSC. A pool
	/// containing them answers "which deck can abuse the broken cards" before it answers anything
	/// about synergy.
	///
	/// It also carries most of the name collisions in <see cref="CombinedReplacements"/>: DES is
	/// 711 cards against HLM 308 + CSC 408, so those two collide on **5** names while the full
	/// union loses many more. Dropping Legacy removes most of the silent same-name substitution
	/// as a side effect.
	///
	/// (The count in <see cref="Combined"/>'s comment below — "one HLM/CSC (Corpse Knight)" — is
	/// stale; it predates cards being added to both sets. Read `CombinedReplacements` at runtime
	/// rather than trusting either number.)
	/// </summary>
	public const string DesignedCode = "DES";

	/// <summary>
	/// **Registered LAST on purpose.** CMB is a combo test instrument meant to be played inside
	/// <see cref="Designed"/> (HLM + CSC + CMB), not on its own — see <see cref="ComboProving"/> for
	/// why a 60-card pool measures the fixture rather than the builder.
	///
	/// Adding it shifted the set MENU: it is now 1=LEG 2=HLM 3=CSC 4=CMB 5=DES 6=ALL. Any piped
	/// console command written against the old numbering now runs a different set silently. Read the
	/// menu — this exact trap is already recorded twice in `MtgSimulator/CLAUDE.md`.
	/// </summary>
	public static IReadOnlyList<CardSet> All { get; } =
		[
			new CardSet(LegacyCode, "Legacy", CardLibrary.All),
			Hollowmere.Set,
			CoresetCube.Set,
			ComboProving.Set,
		];

	/// The set used when a caller does not specify one.
	public static CardSet Default => Get(LegacyCode);

	public static CardSet Get(string code) =>
		string.Equals(code, CombinedCode, StringComparison.OrdinalIgnoreCase) ? Combined
		: string.Equals(code, DesignedCode, StringComparison.OrdinalIgnoreCase) ? Designed
		: All.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase))
			?? throw new ArgumentException(
				$"Unknown set: {code}. Known sets: {string.Join(", ", All.Select(s => s.Code))}, {CombinedCode}",
				nameof(code)
			);

	/// Every set including the two unions — what a mode offers as a menu.
	public static IReadOnlyList<CardSet> AllIncludingCombined() => [.. All, Designed, Combined];

	/// <summary>
	/// The designed sets as one pool. Same machinery as <see cref="Combined"/>, one set fewer.
	/// </summary>
	public static CardSet Designed => DesignedBuilt.Value.Set;

	private static readonly Lazy<(
		CardSet Set,
		IReadOnlyList<(string Name, string WinningSet)> Replaced
	)> DesignedBuilt =
		new(
			() =>
				BuildUnion(
					DesignedCode,
					"Designed Sets",
					[
						.. All.Where(s =>
							!string.Equals(s.Code, LegacyCode, StringComparison.Ordinal)
						),
					]
				)
		);

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

	private static (CardSet, IReadOnlyList<(string, string)>) BuildCombined() =>
		BuildUnion(CombinedCode, "All Sets", All);

	private static (CardSet, IReadOnlyList<(string, string)>) BuildUnion(
		string code,
		string name,
		IReadOnlyList<CardSet> sets
	)
	{
		// Insertion-ordered so the pool is stable across runs; a later Set overwrites the
		// value but keeps the original position, which is irrelevant to correctness and keeps
		// a diff between two runs readable.
		var byName = new Dictionary<string, Card>(StringComparer.OrdinalIgnoreCase);
		var collisions = new List<(string, string)>();

		foreach (var set in sets)
		{
			foreach (var card in set.Cards)
			{
				if (byName.ContainsKey(card.Name))
					collisions.Add((card.Name, set.Code));
				byName[card.Name] = card;
			}
		}

		return (new CardSet(code, name, byName.Values.ToList()), collisions);
	}
}
