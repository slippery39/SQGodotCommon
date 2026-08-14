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
/// </summary>
public static class SetRegistry
{
	/// <summary>
	/// The original CardLibrary.All pool — the ad-hoc set of cards assembled to support the
	/// preconstructed decks. Registered as a set so the existing draft and its trained model
	/// keep working unchanged.
	/// </summary>
	public const string LegacyCode = "LEG";

	public static IReadOnlyList<CardSet> All { get; } =
		[new CardSet(LegacyCode, "Legacy", CardLibrary.All), Hollowmere.Set];

	/// The set used when a caller does not specify one.
	public static CardSet Default => Get(LegacyCode);

	public static CardSet Get(string code) =>
		All.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase))
		?? throw new ArgumentException(
			$"Unknown set: {code}. Known sets: {string.Join(", ", All.Select(s => s.Code))}",
			nameof(code)
		);
}
