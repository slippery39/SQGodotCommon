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

	/// <summary>
	/// A surveil's kept cards in the order to draw them (TBL-9.2): a land that <see cref="Unlocks"/> a card in hand
	/// first, then the spells dearest first (the held price; mana value without one), then the lands we don't need.
	/// ponytail: a needed land always beats the best spell — flooded or short is all it knows; weigh the two if a
	/// position shows the spell should come first.
	/// </summary>
	public static IEnumerable<int> Order(
		GameState s,
		int player,
		IReadOnlyList<int> kept,
		HeldValues? held
	) =>
		kept.Select(s.GetCard)
			.OrderByDescending(c => c.Is(CardType.Land) ? (Unlocks(s, player, c) ? 2 : 0) : 1)
			.ThenByDescending(c =>
				c.Is(CardType.Land) ? 0 : held?.Of(c.Name) ?? c.ManaCost.ManaValue
			)
			.Select(c => c.Id)
			.ToList();

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

	/// <summary>
	/// The spells <paramref name="player"/> gets to play over their next <paramref name="turns"/> turns (AI-27.9, Shayne
	/// 2026-11-18: "A needed land drop is worth the cards it allows you to play"): each turn the spells its mana casts,
	/// cheapest first - the mana in play and one land a turn from the hand (this turn's too, if still to come, as the
	/// first). Each turn also draws a card of <paramref name="deck"/> (none without one): a land at its land share, spread (13 in 27: the
	/// third draw), else a spell of its average cost - the curve still to come, which is what a fourth land is for once
	/// the hand is cast. A land that raises the count lets a card be played; one that doesn't is surplus. Playing a land
	/// never lowers it. <paramref name="skip"/>: hand cards left out (the look-ahead's guessed draws, AI-27.7);
	/// <paramref name="sources"/>: the mana sources counted, in play or lands in hand (all, without it).
	/// ponytail: lands played in hand order, not the best colours first; a drawn spell is colourless at the average cost;
	/// a cost reduction or X read as printed; a source's colour pays the pips of every spell in its turn (as
	/// <see cref="Cast"/> does) - so a second source of a colour two spells need can look surplus.
	/// </summary>
	public static int Playable(
		GameState s,
		int player,
		int turns,
		(double LandShare, int SpellCost)? deck = null,
		Func<Card, bool>? skip = null,
		Func<Card, bool>? sources = null
	)
	{
		var hand = s.CardsIn(player, ZoneType.Hand)
			.Where(c => !Hidden.IsUnknown(c) && skip?.Invoke(c) != true)
			.ToList();
		var spells = hand.Where(c => !c.Is(CardType.Land)).Select(c => c.ManaCost).ToList();
		var lands = hand.Where(c => c.Is(CardType.Land) && sources?.Invoke(c) != false)
			.Select(c => ManaAbilities.ColorsOf(s, c).ToList())
			.ToList();
		var board = s.CardsIn(player, ZoneType.Battlefield)
			.Where(c => sources?.Invoke(c) != false)
			.Select(c => ManaAbilities.ColorsOf(s, c).ToList())
			.Where(colours => colours.Count > 0)
			.ToList();
		var ours = s.GetGame().ActivePlayerId == player;
		var dropNow = ours && s.GetPlayer(player).ThisTurn.LandsPlayed < s.LandDrops(player);
		var played = 0;
		for (var k = 1; k <= turns; k++)
		{
			if (deck is { } d)
			{
				if (Math.Floor(k * d.LandShare) > Math.Floor((k - 1) * d.LandShare))
					lands.Add(
						[
							ManaColor.White,
							ManaColor.Blue,
							ManaColor.Black,
							ManaColor.Red,
							ManaColor.Green,
						]
					); // any colour: ponytail above
				else
					spells.Add(ManaCost.Parse($"{{{d.SpellCost}}}"));
			}
			var mana = board.Concat(lands.Take(k + (dropNow ? 1 : 0))).ToList();
			var left = mana.Count;
			foreach (var cost in spells.OrderBy(c => c.ManaValue).ToList())
				if (Castable(mana, cost) && cost.ManaValue <= left)
				{
					left -= cost.ManaValue;
					spells.Remove(cost);
					played++;
				}
		}
		return played;
	}

	/// <summary>What each of <paramref name="player"/>'s mana sources makes: those in play and the lands in hand (AI-9.9).</summary>
	public static List<List<ManaColor>> Sources(GameState s, int player) =>
		[
			.. s.CardsIn(player, ZoneType.Battlefield)
				.Concat(s.CardsIn(player, ZoneType.Hand).Where(c => c.Is(CardType.Land)))
				.Select(c => ManaAbilities.ColorsOf(s, c).ToList())
				.Where(colours => colours.Count > 0),
		];

	/// <summary>
	/// The coloured pips of <paramref name="spell"/> its colour's sources can't cover — two {R} with one red source is 1
	/// short, Koth's {R} with none is 1 (AI-9.9). Pips matched as <see cref="Castable"/> matches them, so a
	/// dual source counts for each of its colours.
	/// </summary>
	public static int ColorsShort(Card spell, List<List<ManaColor>> sources) =>
		spell
			.ManaCost.Symbols.Where(m =>
				m.Kind == SymbolKind.Colored && m.Color != ManaColor.Colorless
			)
			.GroupBy(m => m.Color)
			.Sum(g => Math.Max(0, g.Count() - sources.Count(colours => colours.Contains(g.Key))));

	/// <summary>Every way to play up to <paramref name="n"/> of the lands (as many as there are, if fewer).</summary>
	static IEnumerable<List<Card>> Choices(List<Card> lands, int n)
	{
		var take = Math.Min(n, lands.Count);
		for (var mask = 0; mask < 1 << lands.Count; mask++)
			if (System.Numerics.BitOperations.PopCount((uint)mask) == take)
				yield return [.. lands.Where((_, i) => (mask & (1 << i)) != 0)];
	}

	static bool Castable(List<List<ManaColor>> sources, Card spell) =>
		Castable(sources, spell.ManaCost);

	internal static bool Castable(List<List<ManaColor>> sources, ManaCost cost) =>
		cost.ManaValue <= sources.Count
		&& cost.Symbols.Where(m => m.Kind == SymbolKind.Colored && m.Color != ManaColor.Colorless)
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
