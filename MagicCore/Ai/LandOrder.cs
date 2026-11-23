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
/// One exception to step 1 (AI-29.8, Shayne 2026-11-19: "the value of the R card played on turn 1 vs the value of the WW
/// card played on turn 2"): a land spending less now that casts a spell in hand NEXT turn which the step-1 land casts
/// neither now nor next turn is played instead when that spell's measured value is higher than what the step-1 land
/// casts now. Lands still tied are all left to the search. Off: <see cref="Evaluator.Terms.LandOrder"/> (`+nolandorder`).
/// ponytail: printed costs (no reductions, X as 0); a summoning-sick mana creature counts as untapped; a shockland's
/// "pay 2 life" read as untapped; each spell judged alone for colour; the exception weighs ONE spell next turn, with
/// next turn's land the best other land in hand (a drawn one unknown), and only against step 1; step 3 goes first even when nothing this turn
/// can break the condition (a Jace token at 5 and its −3: the card is drawn after the land is chosen). Upgrade per
/// position that shows one.
/// </summary>
public static class LandOrder
{
	/// <summary>
	/// The lands of <paramref name="lands"/> the rule keeps (one, unless tied), and whether they go before any other
	/// move. <paramref name="deck"/>: our library's nonland cards, known as a deck list is. <paramref name="held"/>: the
	/// measured values the exception to step 1 compares (none: no exception).
	/// </summary>
	public static (List<PlayLandAction> Keep, bool First) Pick(
		GameState s,
		int player,
		IReadOnlyList<PlayLandAction> lands,
		IReadOnlyList<Card> deck,
		HeldValues? held = null
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
				var makes = ManaAbilities.ColorsOf(s, land).ToList();
				var tapped = EntersTapped(s, l);
				var (spend, cast) = Spend(
					tapped || makes.Count == 0 ? untapped : [.. untapped, makes],
					spells
				);
				return (
					Land: l,
					Cast: cast,
					Next: Next(s, player, l, inPlay, makes, spells),
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
		var winners = keyed.Where(k => k.Key == best).ToList();
		if (held is not null)
		{
			// the exception (AI-29.8): a spell next turn worth more than what step 1's land casts now
			var now = winners.Max(w => w.Cast.Sum(c => held.Of(c.Name)));
			var later = keyed
				.Where(k => k.Key.spend < best.spend)
				.Select(k =>
					(
						k,
						Worth: k.Next.Where(c =>
								winners.All(w => !w.Cast.Contains(c) && !w.Next.Contains(c))
							)
							.Select(c => held.Of(c.Name))
							.DefaultIfEmpty(0)
							.Max()
					)
				)
				.Where(x => x.Worth > now)
				.ToList();
			if (later.Count > 0)
			{
				var top = later.Max(x => (x.Worth, x.k.Key));
				return (
					[.. later.Where(x => (x.Worth, x.k.Key) == top).Select(x => x.k.Land)],
					false
				);
			}
		}
		return ([.. winners.Select(k => k.Land)], best.Fragile == 1);
	}

	/// <summary>
	/// The spells in hand castable NEXT turn, each alone, if <paramref name="land"/> is played now: every source in play,
	/// it, and the best other land in hand as next turn's land drop - read from the hand, not the moves: the search
	/// offers one move for two Plains.
	/// </summary>
	static List<Card> Next(
		GameState s,
		int player,
		PlayLandAction land,
		List<List<ManaColor>> inPlay,
		List<ManaColor> makes,
		List<Card> spells
	)
	{
		List<List<ManaColor>> after = makes.Count == 0 ? inPlay : [.. inPlay, makes];
		var drops = s.CardsIn(player, ZoneType.Hand)
			.Where(c => c.Is(CardType.Land) && c.Id != land.CardId)
			.Select(c => ManaAbilities.ColorsOf(s, c).ToList())
			.Where(c => c.Count > 0)
			.ToList();
		return
		[
			.. spells.Where(c =>
				drops.Count == 0
					? LandUnlock.Castable(after, c.ManaCost)
					: drops.Any(d => LandUnlock.Castable([.. after, d], c.ManaCost))
			),
		];
	}

	/// <summary>
	/// The most mana <paramref name="spells"/> can spend together from <paramref name="sources"/>, and the spells that
	/// spend it: every set of them that fits, each castable alone on colour; an X spell costs X = 1 at least and spends
	/// whatever is left.
	/// ponytail: every subset of the hand (2^n, n a hand's size); colours not shared between spells.
	/// </summary>
	static (int Mana, List<Card> Cast) Spend(List<List<ManaColor>> sources, List<Card> spells)
	{
		var fits = spells
			.Select(c =>
				(Card: c, c.ManaCost, X: c.ManaCost.Symbols.Count(m => m.Kind == SymbolKind.X))
			)
			.Where(c =>
				c.ManaCost.ManaValue + c.X <= sources.Count
				&& LandUnlock.Castable(sources, c.ManaCost)
			)
			.ToList();
		(int Mana, List<Card> Cast) best = (0, []);
		for (var mask = 1; mask < 1 << Math.Min(fits.Count, 12); mask++)
		{
			var picked = fits.Where((_, i) => (mask & (1 << i)) != 0).ToList();
			var cost = picked.Sum(c => c.ManaCost.ManaValue + c.X);
			var mana = picked.Any(c => c.X > 0) ? sources.Count : cost;
			if (cost <= sources.Count && mana > best.Mana)
				best = (mana, [.. picked.Select(c => c.Card)]);
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
				&& s.GetCard(land.CardId).GetComponent<Effects.EntryCost>() is { Instead: null }
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
