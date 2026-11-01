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
		var actions = s.DistinctMoves(s.LegalActions());
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
