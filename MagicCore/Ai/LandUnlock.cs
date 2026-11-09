using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;

namespace MagicCore.Ai;

/// <summary>
/// AI-9.1 (Shayne, 2026-11-09): a land is worth what it UNLOCKS, not a flat price - keep one only if it lets us cast a
/// card we hold that we could not otherwise, or lets us cast MORE cards by next turn; otherwise it is surplus. A
/// land has no value of its own (MtgSimulator's precedent, sim-ai.md "A land in hand is worth what it UNLOCKS").
/// Judged on next turn's mana: the sources we have, the lands in our hand (one drop this turn if it is still to
/// come, one next), then this land.
/// ponytail: colours are matched pip by pip against the sources that make them, not shared between spells, hybrid /
/// Phyrexian / X count as generic, and the cards cast are packed cheapest first, not best; upgrade to
/// ManaPayment.Find per spell if a position shows it.
/// </summary>
public static class LandUnlock
{
	/// <summary>
	/// A surveil's lands, decided by the rule (AI-17.20, Shayne 2026-11-09): from the top down, a land that
	/// <see cref="Unlocks"/> stays on top - counted as held for the lands below it - and one that does not is binned.
	/// </summary>
	public static (HashSet<int> Lands, List<int> Bin) Surveil(
		GameState s,
		ChooseFromLibraryTopChoice top
	)
	{
		var (lands, bin, kept) = (new HashSet<int>(), new List<int>(), new List<Card>());
		foreach (
			var c in s.CardsIn(top.PlayerId, ZoneType.Library)
				.Take(top.Count)
				.Where(c => c.Is(CardType.Land))
		)
		{
			lands.Add(c.Id);
			if (Unlocks(s, top.PlayerId, c, kept))
				kept.Add(c);
			else
				bin.Add(c.Id);
		}
		return (lands, bin);
	}

	public static bool Unlocks(
		GameState s,
		int player,
		Card land,
		IReadOnlyList<Card>? alsoHeld = null
	)
	{
		var inHand = s.CardsIn(player, ZoneType.Hand).ToList();
		var spells = inHand.Where(c => !c.Is(CardType.Land) && c.Id != land.Id).ToList();
		if (spells.Count == 0)
			return false;
		// land drops until we next cast: this turn's if it is ours and still to come, and our next turn's
		var drops =
			s.GetGame().ActivePlayerId == player
			&& s.GetPlayer(player).ThisTurn.LandsPlayed < s.LandDrops(player)
				? 2
				: 1;
		var board = s.CardsIn(player, ZoneType.Battlefield)
			.Select(c => ManaAbilities.ColorsOf(s, c).ToList())
			.Where(colours => colours.Count > 0)
			.ToList();
		var held = inHand
			.Where(c => c.Is(CardType.Land) && c.Id != land.Id)
			.Concat(alsoHeld ?? [])
			.ToList();
		// the best lands to play: every choice of the drops we have, scored by cards cast and by cards castable
		(int Packed, int Singles) Best(List<Card> lands)
		{
			var (packed, singles) = (0, 0);
			foreach (var play in Choices(lands, drops))
			{
				var sources = board
					.Concat(play.Select(l => ManaAbilities.ColorsOf(s, l).ToList()))
					.ToList();
				packed = Math.Max(packed, Cast(sources, spells));
				singles = Math.Max(singles, spells.Count(c => Castable(sources, c)));
			}
			return (packed, singles);
		}
		var (withPacked, withSingles) = Best([.. held, land]);
		var (withoutPacked, withoutSingles) = Best(held);
		return withSingles > withoutSingles || withPacked > withoutPacked;
	}

	/// <summary>Every way to play up to <paramref name="n"/> of the lands (as many as there are, if fewer).</summary>
	static IEnumerable<List<Card>> Choices(List<Card> lands, int n)
	{
		var take = Math.Min(n, lands.Count);
		for (var mask = 0; mask < 1 << lands.Count; mask++)
			if (System.Numerics.BitOperations.PopCount((uint)mask) == take)
				yield return [.. lands.Where((_, i) => (mask & (1 << i)) != 0)];
	}

	static bool Castable(List<List<ManaColor>> sources, Card spell) =>
		spell.ManaCost.ManaValue <= sources.Count
		&& spell
			.ManaCost.Symbols.Where(m =>
				m.Kind == SymbolKind.Colored && m.Color != ManaColor.Colorless
			)
			.GroupBy(m => m.Color)
			.All(g => g.Count() <= sources.Count(colours => colours.Contains(g.Key)));

	/// <summary>How many of the spells the sources can cast together, cheapest first.</summary>
	static int Cast(List<List<ManaColor>> sources, List<Card> spells)
	{
		var left = sources.Count;
		var n = 0;
		foreach (var c in spells.OrderBy(c => c.ManaCost.ManaValue))
			if (Castable(sources, c) && c.ManaCost.ManaValue <= left)
			{
				n++;
				left -= c.ManaCost.ManaValue;
			}
		return n;
	}
}
