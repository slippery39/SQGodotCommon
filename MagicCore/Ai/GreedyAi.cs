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
public sealed class GreedyAi(int playerId)
{
	public int PlayerId { get; } = playerId;

	/// <summary>Makes this player's decision — an action, or an answer to a pending choice — and says what it was.</summary>
	public (GameState State, string What) Decide(GameState s)
	{
		if (s.GetPendingChoice() is { } choice)
		{
			var answer = Answer(s, choice).ToList();
			return (s.Choose(answer), Describe.Answer(s, choice, answer));
		}
		var action = ChooseAction(s);
		return (s.Act(action), Describe.Action(s, action));
	}

	GameAction ChooseAction(GameState s)
	{
		var actions = s.DistinctMoves(s.LegalActions());
		if (actions.FirstOrDefault(a => a is KeepHandAction) is { } keep)
			return ShouldKeep(s) ? keep : actions.First(a => a is MulliganAction);

		var pass = actions.First(a => a is PassPriorityAction);
		var baseline = Evaluate(Settle(s.Act(pass)));
		var (best, score) = (pass, baseline);
		foreach (var action in actions.Where(a => a is not PassPriorityAction))
		{
			var value = Evaluate(Settle(s.Act(action)));
			if (value > score)
				(best, score) = (action, value);
		}
		return best;
	}

	IEnumerable<int> Answer(GameState s, ChoiceAction choice)
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
			return new[] { worst, [] }.MaxBy(pick => Evaluate(Settle(s.Choose(pick))))!;
		}
		if (choice is ChooseCardsFromHandChoice)
			return WorstCards(s, choice.Options.Select(o => o.Id), choice.MinChoices);
		if (choice is ChooseColorChoice)
			return [ChooseColorChoice.IdOf(MostWantedColor(s))];
		if (choice is ChooseModesChoice { MaxChoices: > 1 } modes)
			return Combinations(modes.Options.Select(o => o.Id).ToList(), modes.MaxChoices)
				.MaxBy(pick => Evaluate(Settle(s.Choose(pick))))!;
		if (choice.MaxChoices != 1)
			return choice.Options.Take(choice.MinChoices).Select(o => o.Id);

		var options = choice.Options.Select(o => (IEnumerable<int>)[o.Id]).ToList();
		if (choice.MinChoices == 0)
			options.Insert(0, []);
		return options.MaxBy(pick => Evaluate(Settle(s.Choose(pick))))!;
	}

	static IEnumerable<IEnumerable<int>> Combinations(List<int> ids, int k) =>
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
	/// library and on the battlefield. Every color scores the same to Evaluate, so it can't decide.
	/// </summary>
	ManaColor MostWantedColor(GameState s) =>
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
			.Where(c => c != ManaColor.Colorless)
			.GroupBy(c => c)
			.OrderByDescending(g => g.Count())
			.Select(g => g.Key)
			.DefaultIfEmpty(ManaColor.White)
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

	/// <summary>
	/// Plays forward until the move's consequence is visible: answers choices cheaply, passes for
	/// both players until the stack is empty, and — mid-combat — until the damage is dealt.
	/// </summary>
	GameState Settle(GameState s)
	{
		for (var i = 0; i < 200 && !s.IsOver(); i++)
		{
			if (s.GetPendingChoice() is { } choice)
				s = s.Choose(QuickAnswer(s, choice));
			else if (!s.StackIsEmpty() || InCombat(s))
				s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
			else
				break;
		}
		return s;
	}

	static bool InCombat(GameState s) =>
		s.GetGame().Step is >= Step.DeclareAttackers and <= Step.CombatDamage
		&& s.GetGame().PriorityPlayerId != 0;

	/// <summary>Inside a simulation: nobody else attacks, blockers only take blocks they survive, targets are the first legal.</summary>
	static IEnumerable<int> QuickAnswer(GameState s, ChoiceAction choice)
	{
		if (choice is DeclareBlockerChoice block)
		{
			var blocker = s.GetCard(block.CreatureId);
			var safe = choice.Options.Where(o =>
				o.Id != CombatOption.None
				&& (
					Turns.Combat.Assigns(s, s.GetCard(o.Id)) < s.Toughness(blocker)
					|| s.IsIndestructible(blocker)
				)
			);
			return [safe.Select(o => o.Id).FirstOrDefault(CombatOption.None)];
		}
		if (choice is DeclareAttackerChoice)
			return [CombatOption.None];
		if (choice is PayToKeepChoice)
			return choice.Options.Select(o => o.Id).Take(1); // looking ahead: keep the spell if it can be paid
		return choice.Options.Take(choice.MinChoices).Select(o => o.Id);
	}

	/// <summary>
	/// Position score for this player. Permanent value counts printed stats plus counters, not
	/// until-end-of-turn pumps — a pump is worth only what it changes before it wears off.
	/// </summary>
	public double Evaluate(GameState s)
	{
		if (s.IsOver())
			return s.WinnerId() == PlayerId ? 10_000 : -10_000;
		var opponent = s.OpponentOf(PlayerId);
		return Side(s, PlayerId) - Side(s, opponent);
	}

	static double Side(GameState s, int player)
	{
		var board = s.CardsIn(player, ZoneType.Battlefield).ToList();
		var creatures = board.Where(c => c.Is(CardType.Creature)).Sum(c => CreatureValue(s, c));
		// Every term is MONOTONIC — more of a good thing always scores more, tapering instead of
		// capping. A cap makes "more" tie with "less", and a tie silently picks the passive move:
		// life capped at 20 stopped all attacks past tapped blockers once a drain deck pushed the
		// opponent above it; lands capped at 7 made an 8th land worth less in play than in hand.
		var lands = Taper(board.Count(c => c.Is(CardType.Land)), 7, 1.5, 0.4);
		// Loyalty is spent, not banked: valued under half a card per counter, so trading three
		// counters for a card scores as a gain. At 0.8 the AI never once used its Jace.
		var walkers = board.Where(c => c.Is(CardType.Planeswalker)).Sum(c => 2 + c.Loyalty * 0.35);
		var others =
			board.Count(c => !c.Is(CardType.Creature | CardType.Land | CardType.Planeswalker))
			* 2.0;
		var life = s.GetPlayer(player).Life;
		var lifeValue = life <= 0 ? -50 : Taper(life, 20, 1.0, 0.5) + (life < 6 ? life - 6 : 0); // low life hurts more
		// A land in hand is worth less than a spell, so playing even a surplus land scores a gain.
		// Floating mana scores nothing: it empties at the end of the step, which Settle never reaches.
		var hand = s.CardsIn(player, ZoneType.Hand).Sum(c => c.Is(CardType.Land) ? 0.3 : 1.2);
		return creatures + walkers + lands + others + hand + lifeValue;
	}

	/// <summary>Full value per unit up to <paramref name="knee"/>, a smaller value per unit beyond.</summary>
	static double Taper(int n, int knee, double perUnit, double beyond) =>
		Math.Min(n, knee) * perUnit + Math.Max(0, n - knee) * beyond;

	static double CreatureValue(GameState s, Card c)
	{
		// Alive is decided by the real toughness (a pump that saved it counts); its VALUE by what
		// lasts past this turn — statics yes ("creatures you control have trample" stays), until-
		// end-of-turn pumps and grants no.
		if (s.Toughness(c) <= 0 || (s.Toughness(c) - c.Damage <= 0 && !s.IsIndestructible(c)))
			return 0; // the state-based check's own question: lethal damage doesn't kill indestructible
		var pumps = c.GetComponents<UntilEndOfTurn>().ToList();
		var power = s.Power(c) - pumps.Sum(u => u.Power);
		var toughness = Math.Max(1, s.Toughness(c) - pumps.Sum(u => u.Toughness) - c.Damage);
		var keywords = new[]
		{
			"Flying",
			"Deathtouch",
			"Lifelink",
			"First strike",
			"Double strike",
			"Trample",
			"Menace",
		}.Count(k => s.Has(c, k) && !pumps.Any(u => u.Keyword == k));
		return 2 + power * 1.5 + toughness + keywords * 0.8;
	}
}
