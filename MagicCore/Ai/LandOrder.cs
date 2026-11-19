using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;

namespace MagicCore.Ai;

/// <summary>
/// AI-29.3 (Shayne, 2026-11-19): which land to play is a rule, not the look-ahead — "look at our hand / deck, determine
/// color requirements, determine which colors we might need this turn / next turn and play out our lands accordingly";
/// "a real player wouldn't look 3 turns ahead to realize playing the red green land is better than a forest". The
/// search keeps everything else: when to play the land, what to cast. The lands in hand are compared, first difference
/// wins:
/// 1. the mana the hand can spend THIS turn - with our untapped sources, and it if it enters untapped; an X spell for X 1
///    or more, spending the rest (2026-11-19: counting spells, Ajani's Anguish for X=0 played a Mountain on turn 1, and
///    a 2-drop tied Way of the Wildspeaker, so a tapped Annex wasted a turn's mana);
/// 2. the spells in hand short of a colour no source in play makes, that it makes;
/// 3. untapped now on a condition that can break (Konstrari Annex: "enters tapped unless you control a planeswalker") -
///    played before anything else, so nothing this turn breaks it;
/// 4. it enters tapped now - free when the mana isn't needed (Rockfall Vale on turn 2);
/// 5. the spells left in the deck short of a colour it makes.
/// Lands still tied are all left to the search. Off: <see cref="Evaluator.Terms.LandOrder"/> (`+nolandorder`).
/// ponytail: printed costs (no reductions, X as 0); a summoning-sick mana creature counts as untapped; a shockland's
/// "pay 2 life" read as untapped; each spell judged alone for colour; step 3 goes first even when nothing this turn
/// can break the condition (a Jace token at 5 and its −3: the card is drawn after the land is chosen). Upgrade per
/// position that shows one.
/// </summary>
public static class LandOrder
{
	/// <summary>
	/// The lands of <paramref name="lands"/> the rule keeps (one, unless tied), and whether they go before any other
	/// move. <paramref name="deck"/>: our unseen nonland cards, known as a deck list is.
	/// </summary>
	public static (List<PlayLandAction> Keep, bool First) Pick(
		GameState s,
		int player,
		IReadOnlyList<PlayLandAction> lands,
		IReadOnlyList<Card> deck
	)
	{
		var spells = s.CardsIn(player, ZoneType.Hand)
			.Where(c => !c.Is(CardType.Land) && !Hidden.IsUnknown(c))
			.ToList();
		var board = s.CardsIn(player, ZoneType.Battlefield)
			.Select(c => (c, Colours: ManaAbilities.ColorsOf(s, c).ToList()))
			.Where(x => x.Colours.Count > 0)
			.ToList();
		var untapped = board.Where(x => !x.c.IsTapped).Select(x => x.Colours).ToList();
		var inPlay = board.Select(x => x.Colours).ToList();
		var keyed = lands
			.Select(l =>
			{
				var land = s.GetCard(l.CardId);
				// Room of Refuge's colour is chosen as it enters: in hand, any of them
				var makes = land.HasComponent<Effects.ChoosesColorAsItEnters>()
					?
					[
						ManaColor.White,
						ManaColor.Blue,
						ManaColor.Black,
						ManaColor.Red,
						ManaColor.Green,
					]
					: ManaAbilities.ColorsOf(s, land).ToList();
				var tapped = EntersTapped(s, l);
				var spend = Spend(
					tapped || makes.Count == 0 ? untapped : [.. untapped, makes],
					spells
				);
				return (
					Land: l,
					Key: (
						spend,
						Short(spells, inPlay, makes),
						Fragile: !tapped && Fragile(s, player, l) ? 1 : 0,
						Tapped: tapped ? 1 : 0,
						Short(deck, inPlay, makes)
					)
				);
			})
			.ToList();
		var best = keyed.Max(k => k.Key);
		return ([.. keyed.Where(k => k.Key == best).Select(k => k.Land)], best.Fragile == 1);
	}

	/// <summary>
	/// The most mana <paramref name="spells"/> can spend together from <paramref name="sources"/>: every set of them that
	/// fits, each castable alone on colour; an X spell costs X = 1 at least and spends whatever is left.
	/// ponytail: every subset of the hand (2^n, n a hand's size); colours not shared between spells.
	/// </summary>
	static int Spend(List<List<ManaColor>> sources, List<Card> spells)
	{
		var fits = spells
			.Select(c => (c.ManaCost, X: c.ManaCost.Symbols.Count(m => m.Kind == SymbolKind.X)))
			.Where(c =>
				c.ManaCost.ManaValue + c.X <= sources.Count
				&& LandUnlock.Castable(sources, c.ManaCost)
			)
			.ToList();
		var best = 0;
		for (var mask = 1; mask < 1 << Math.Min(fits.Count, 12); mask++)
		{
			var picked = fits.Where((_, i) => (mask & (1 << i)) != 0).ToList();
			var cost = picked.Sum(c => c.ManaCost.ManaValue + c.X);
			if (cost <= sources.Count)
				best = Math.Max(best, picked.Any(c => c.X > 0) ? sources.Count : cost);
		}
		return best;
	}

	/// <summary>How many of <paramref name="spells"/> are short of a colour <paramref name="makes"/> has and no source in play makes enough of.</summary>
	static int Short(
		IEnumerable<Card> spells,
		List<List<ManaColor>> inPlay,
		List<ManaColor> makes
	) =>
		spells.Count(c =>
			makes.Any(colour =>
				c.ManaCost.Symbols.Count(m => m.Kind == SymbolKind.Colored && m.Color == colour)
				> inPlay.Count(sources => sources.Contains(colour))
			)
		);

	/// <summary>
	/// Whether it enters tapped if played now - the play itself, so every rule that taps it is read (the tapped state is
	/// set before any "as it enters" choice). A shockland's pending "pay 2 life" is an untapped option.
	/// </summary>
	static bool EntersTapped(GameState s, PlayLandAction land)
	{
		var after = s.Act(land);
		return after.GetCard(land.CardId).IsTapped
			&& !(
				after.IsWaitingForChoice
				&& s.GetCard(land.CardId).GetComponent<Effects.EntersTappedComponent>()
					is { UnlessYouPayLife: > 0 }
			);
	}

	/// <summary>
	/// Untapped now only through something other than our lands: with our other permanents and our hand gone it would
	/// enter tapped (a planeswalker, a Jace to behold). A land count only grows, so Rockfall Vale's isn't.
	/// </summary>
	static bool Fragile(GameState s, int player, PlayLandAction land)
	{
		if (s.GetCard(land.CardId).GetComponent<Effects.EntersTappedComponent>()?.Unless is null)
			return false;
		var bare = s;
		foreach (
			var c in s.CardsIn(player, ZoneType.Battlefield)
				.Where(c => !c.Is(CardType.Land))
				.Concat(s.CardsIn(player, ZoneType.Hand).Where(c => c.Id != land.CardId))
				.ToList()
		)
			bare = bare.RemoveObject(c.Id);
		return EntersTapped(bare, land);
	}
}
