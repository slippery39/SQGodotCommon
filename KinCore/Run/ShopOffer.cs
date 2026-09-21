using System.Collections.Immutable;

namespace KinCore;

/// <summary>
/// What a shop floor is selling. **Deterministic from (seed, floor)**, like a reward screen, so a
/// run replays exactly and a bug report is actionable.
///
/// **Card removal is why this exists.** Combat v3 made the deck your entire per-turn output, so a
/// card you would not play crowds out one you would — and until now the only thing in the game that
/// could take a card out of a run was an apocalypse. Buying is the smaller half.
/// </summary>
public record ShopOffer
{
	/// <summary>Cards for sale, each at <see cref="CardPrice"/>.</summary>
	public ImmutableArray<RunCard> Cards { get; init; } = [];

	public int CardPrice { get; init; }

	/// <summary>
	/// What it costs to take one card out of the deck. **Rises with every removal already bought
	/// this run** — see <see cref="Run.CardsRemoved"/> — so thinning is a decision you make a few
	/// times rather than the only thing you ever spend on.
	/// </summary>
	public int RemovalPrice { get; init; }

	public int HealPrice { get; init; }
	public int HealAmount { get; init; }
}
