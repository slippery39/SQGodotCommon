using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;

namespace MagicCore.Ai;

/// <summary>
/// One-ply AI: tries each legal move on a copy of the game, plays it out until the consequence is
/// visible (the stack resolved; in combat, the damage dealt), and keeps the best-scoring result.
/// Card-agnostic — it reads outcomes, never card text, so every new card works with no AI code.
///
/// ponytail: one ply and an opponent that never responds inside the simulation. It cannot plan a
/// turn, hold up mana or bluff, and a tapped dual and an untapped basic play as equals — it never
/// asks "do I need this mana now". The real search comes once games run in bulk (see MagicCore/CLAUDE.md).
/// </summary>
public sealed class GreedyAi(int playerId, HeldValues? held = null) : IAi
{
	public int PlayerId { get; } = playerId;

	/// <summary>Makes this player's decision — an action, or an answer to a pending choice — and says what it was.</summary>
	public AiDecision Decide(GameState s) =>
		s.GetPendingChoice() is { } choice
			? AiDecision.Answering(s, [.. Answer(s, choice)])
			: AiDecision.Acting(s, ChooseAction(s));

	/// <summary>The same decision, played and not described — inside a search's rollouts (story AI-3.7).</summary>
	public GameState Play(GameState s) =>
		s.GetPendingChoice() is { } choice
			? s.Choose([.. Answer(s, choice)])
			: s.Act(ChooseAction(s));

	GameAction ChooseAction(GameState s)
	{
		var actions = s.DistinctMoves(s.SearchMoves());
		if (actions.FirstOrDefault(a => a is KeepHandAction) is { } keep)
			return ShouldKeep(s) ? keep : actions.First(a => a is MulliganAction);

		var pass = actions.First(a => a is PassPriorityAction);
		var baseline = Evaluate(Lookahead.Settle(s.Act(pass)));
		var (best, score) = (pass, baseline);
		foreach (var action in actions.Where(a => a is not PassPriorityAction))
		{
			var value = Evaluate(Lookahead.Settle(s.Act(action)));
			if (value > score)
				(best, score) = (action, value);
		}
		return best;
	}

	/// <summary>
	/// Kai Winn in LIMITED (Shayne, 2026-11-06 — "in a constructed deck what you name would be determined
	/// completely differently"), step by step: a card with an activated ability on the opponent's battlefield
	/// (a mana or loyalty ability too); if none, a card known to be in their hand; if none, the highest-rated
	/// common in a colour their lands show — the set's ratings, or the cheapest while a set has none (TRK
	/// until MC-5.4); else the list's first. Several at one step: the most copies, then the list's order.
	/// </summary>
	string LikeliestName(GameState s, ImmutableArray<string> names)
	{
		var opponent = s.OpponentOf(PlayerId);
		var named = names.ToHashSet();
		string? Commonest(IEnumerable<Card> cards) =>
			cards
				.Where(c => !c.Is(CardType.Land) && !c.IsToken && named.Contains(c.Name))
				.GroupBy(c => c.Name)
				.OrderByDescending(g => g.Count())
				.ThenBy(g => names.IndexOf(g.Key))
				.Select(g => g.Key)
				.FirstOrDefault();
		var board = s.CardsIn(opponent, ZoneType.Battlefield).ToList();
		return Commonest(
				board.Where(c =>
					s.ActivatedAbilitiesOf(c).Any()
					|| ManaAbilities.ColorsOf(s, c).Any()
					|| Loyalty.AbilitiesOf(s, c).Any()
				)
			)
			?? Commonest(
				s.CardsIn(opponent, ZoneType.Hand)
					.Where(c => (c.KnownTo & Knowers.Opponent) != 0 && !c.IsHiddenFrom(PlayerId))
			)
			?? BestCommonIn(
				board
					.Where(c => c.Is(CardType.Land))
					.SelectMany(c => ManaAbilities.ColorsOf(s, c))
					.Where(c => c != ManaColor.Colorless)
					.ToHashSet(),
				named
			)
			?? names[0];
	}

	/// <summary>Kai Winn's third step: the highest-rated common all of whose colours their lands show.</summary>
	static string? BestCommonIn(HashSet<ManaColor> colors, HashSet<string> named) =>
		colors.Count == 0
			? null
			: Sets
				.CardRegistry.Sets.SelectMany(set =>
					Sets.CardRegistry.Printings(set)
						.Where(p =>
							p.Rarity == "common"
							&& named.Contains(p.Card.Name)
							&& p.Card.Colors is { Length: > 0 } own
							&& own.All(letter => colors.Contains(ManaCost.ColorOf(letter)))
						)
						.Select(p =>
							(
								p.Card.Name,
								Rating: Limited
									.CardRatings.TryOf(set)
									?.GetValueOrDefault(p.Card.Name) ?? 0,
								p.Card.Cmc
							)
						)
				)
				.OrderByDescending(c => c.Rating)
				.ThenBy(c => c.Cmc)
				.ThenBy(c => c.Name, StringComparer.Ordinal)
				.Select(c => c.Name)
				.FirstOrDefault();

	/// <summary>Nyota Uhura: the opponent's nonland cards this player knows of — on the battlefield, in the graveyard, exiled face up, seen in hand.</summary>
	IEnumerable<Card> KnownOpponentNonlands(GameState s)
	{
		var opponent = s.OpponentOf(PlayerId);
		return new[] { ZoneType.Battlefield, ZoneType.Graveyard, ZoneType.Exile, ZoneType.Hand }
			.SelectMany(zone =>
				s.CardsIn(opponent, zone)
					.Where(c => zone != ZoneType.Hand || (c.KnownTo & Knowers.Opponent) != 0)
			)
			.Where(c => !c.Is(CardType.Land) && !c.IsToken && !c.IsHiddenFrom(PlayerId));
	}

	/// <summary>
	/// Nyota Uhura (Shayne, 2026-11-05): the mana value most common among the opponent's nonland cards
	/// this player knows of — on the battlefield, in the graveyard, exiled face up, seen in hand — the
	/// lower on a tie; with none known, 2.
	/// </summary>
	int LikeliestManaValue(GameState s) =>
		KnownOpponentNonlands(s)
			.GroupBy(c => c.ManaCost.ManaValue)
			.OrderByDescending(g => g.Count())
			.ThenBy(g => g.Key)
			.Select(g => (int?)g.Key)
			.FirstOrDefault() ?? 2;

	/// <summary>This player's answer to a pending choice. The search AI asks it for the choices it doesn't branch on.</summary>
	public IEnumerable<int> Answer(GameState s, ChoiceAction choice)
	{
		if (choice is ChooseCardsFromHandChoice { PlayerId: var owner } && owner != PlayerId)
			// You choose from THEIR hand: the priciest (MtgCore's stand-in for the same choice).
			return choice
				.Options.Select(o => s.GetCard(o.Id))
				.OrderByDescending(c => c.ManaCost.ManaValue)
				.Take(Math.Max(choice.MinChoices, 1))
				.Select(c => c.Id);
		if (choice is ChooseCardsFromHandChoice { MinChoices: 0, MaxChoices: 1 })
		{
			// "You may discard a card": the worst card, or none — whichever plays out better.
			IEnumerable<int> worst = WorstCards(s, choice.Options.Select(o => o.Id), 1).ToList();
			return new[] { worst, [] }.MaxBy(pick => Evaluate(Lookahead.Settle(s.Choose(pick))))!;
		}
		if (choice is ChooseCardsFromHandChoice)
			return WorstCards(s, choice.Options.Select(o => o.Id), choice.MinChoices);
		if (choice is ChooseXTargetsChoice)
			// Dominion Infiltration copies them: the biggest. ponytail: power, then mana value, stands in for worth.
			return choice
				.Options.Select(o => s.GetCard(o.Id))
				.OrderByDescending(s.Power)
				.ThenByDescending(c => c.ManaCost.ManaValue)
				.Take(choice.MinChoices)
				.Select(c => c.Id);
		if (choice is ChooseKeywordChoice { NamesACard: true } naming)
			return [naming.Keywords.IndexOf(LikeliestName(s, naming.Keywords)) + 1];
		if (choice is ChooseNumberChoice { NamesAManaValue: true, Max: var max })
			return [Math.Min(LikeliestManaValue(s), max) + 1]; // option id = the number + 1
		if (choice is ChooseColorChoice)
			return
			[
				ChooseColorChoice.IdOf(
					MostWantedColor(
						s,
						[.. choice.Options.Select(o => ChooseColorChoice.ColorOf(o.Id))]
					)
				),
			];
		if (choice is ChooseModesChoice { MaxChoices: > 1 } modes)
			return Combinations(modes.Options.Select(o => o.Id).ToList(), modes.MaxChoices)
				.MaxBy(pick => Evaluate(Lookahead.Settle(s.Choose(pick))))!;
		if (choice is ChooseAmongChoice { MinChoices: > 1 } reveal)
			// Extrapolate the Impossible's reveal: an opponent hands you the worse, so show the priciest.
			// ponytail: mana value stands in for worth, as for "their hand" above — price by held values if it misplays.
			return reveal
				.Options.Select(o => s.GetCard(o.Id))
				.OrderByDescending(c => c.ManaCost.ManaValue)
				.Take(reveal.MinChoices)
				.Select(c => c.Id);
		if (choice is ChooseFromLibraryChoice { MaxChoices: > 1 })
			return choice.Options.Take(choice.MaxChoices).Select(o => o.Id); // "up to N lands": more is better
		if (choice.MaxChoices != 1)
			return choice.Options.Take(choice.MinChoices).Select(o => o.Id);

		var options = choice.Options.Select(o => (IEnumerable<int>)[o.Id]).ToList();
		if (choice.MinChoices == 0)
			options.Insert(0, []);
		return options.MaxBy(pick => Evaluate(Lookahead.Settle(s.Choose(pick))))!;
	}

	internal static IEnumerable<IEnumerable<int>> Combinations(List<int> ids, int k) =>
		k == 0
			?
			[
				[],
			]
			: ids.SelectMany(
				(id, i) => Combinations(ids[(i + 1)..], k - 1).Select(rest => rest.Prepend(id))
			);

	/// <summary>
	/// Room of Refuge: the color with the most mana symbols across this player's cards in hand,
	/// library and on the battlefield — among those offered (Dal R'El: {R} or {G}). Every color scores
	/// the same to Evaluate, so it can't decide.
	/// </summary>
	ManaColor MostWantedColor(GameState s, IReadOnlyList<ManaColor> offered) =>
		new[] { ZoneType.Hand, ZoneType.Library, ZoneType.Battlefield }
			.SelectMany(z => s.CardsIn(PlayerId, z))
			.SelectMany(c => c.ManaCost.Symbols)
			.SelectMany(sym =>
				sym.Kind switch
				{
					SymbolKind.Colored or SymbolKind.TwoBrid => new[] { sym.Color },
					SymbolKind.Hybrid => [sym.Color, sym.Color2],
					_ => [],
				}
			)
			.Where(offered.Contains)
			.GroupBy(c => c)
			.OrderByDescending(g => g.Count())
			.Select(g => g.Key)
			.DefaultIfEmpty(offered[0])
			.First();

	/// <summary>Keep 2–5 lands in seven; after two mulligans keep anything.</summary>
	bool ShouldKeep(GameState s)
	{
		var lands = s.CardsIn(PlayerId, ZoneType.Hand).Count(c => c.Is(CardType.Land));
		return s.GetPlayer(PlayerId).MulligansTaken >= 2 || lands is >= 2 and <= 5;
	}

	/// <summary>
	/// What to discard or bottom: lands once there are five in play and hand, else the priciest spells.
	/// ponytail: a heuristic, not a simulation — every discard scores the same to Evaluate.
	/// </summary>
	IEnumerable<int> WorstCards(GameState s, IEnumerable<int> ids, int count)
	{
		var landsHave = s.CardsIn(PlayerId, ZoneType.Battlefield)
			.Concat(s.CardsIn(PlayerId, ZoneType.Hand))
			.Count(c => c.Is(CardType.Land));
		return ids.Select(s.GetCard)
			.OrderByDescending(c =>
				c.Is(CardType.Land) ? (landsHave >= 5 ? 100 : -100) : c.ManaCost.ManaValue
			)
			.Take(count)
			.Select(c => c.Id);
	}

	/// <summary>Null held values — GreedyAi's own play — is the flat price; the search passes its own.</summary>
	double Evaluate(GameState s) => Evaluator.Evaluate(s, PlayerId, held);
}
