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
/// turn, hold up mana or bluff. The real search comes once games run in bulk (see MagicCore/CLAUDE.md).
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
		if (choice is ChooseCardsFromHandChoice)
			return WorstCards(s, choice.Options.Select(o => o.Id), choice.MinChoices);
		if (choice.MaxChoices != 1)
			return choice.Options.Take(choice.MinChoices).Select(o => o.Id);

		var options = choice.Options.Select(o => (IEnumerable<int>)[o.Id]).ToList();
		if (choice.MinChoices == 0)
			options.Insert(0, []);
		return options.MaxBy(pick => Evaluate(Settle(s.Choose(pick))))!;
	}

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
				o.Id != CombatOption.None && s.GetCard(o.Id).Power < blocker.Toughness
			);
			return [safe.Select(o => o.Id).FirstOrDefault(CombatOption.None)];
		}
		if (choice is DeclareAttackerChoice)
			return [CombatOption.None];
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
		var creatures = board.Where(c => c.Is(CardType.Creature)).Sum(CreatureValue);
		var lands = Math.Min(board.Count(c => c.Is(CardType.Land)), 7) * 1.5;
		// Loyalty is spent, not banked: valued under half a card per counter, so trading three
		// counters for a card scores as a gain. At 0.8 the AI never once used its Jace.
		var walkers = board.Where(c => c.Is(CardType.Planeswalker)).Sum(c => 2 + c.Loyalty * 0.35);
		var others =
			board.Count(c => !c.Is(CardType.Creature | CardType.Land | CardType.Planeswalker))
			* 2.0;
		var life = s.GetPlayer(player).Life;
		var lifeValue = life <= 0 ? -50 : Math.Min(life, 20) + (life < 6 ? life - 6 : 0); // low life hurts more
		return creatures
			+ walkers
			+ lands
			+ others
			+ s.CardsIn(player, ZoneType.Hand).Count() * 1.2
			+ lifeValue
			+ s.GetPlayer(player).ManaPool.Total * 0.1;
	}

	static double CreatureValue(Card c)
	{
		// Alive is decided by the real toughness (a pump that saved it counts); its VALUE by what
		// lasts past this turn — so a temporary pump or keyword is worth only what it changed.
		if (c.Toughness - c.Damage <= 0)
			return 0;
		var power = c.BasePower + c.PlusOneCounters;
		var toughness = Math.Max(1, c.BaseToughness + c.PlusOneCounters - c.Damage);
		var keywords = new[]
		{
			"Flying",
			"Deathtouch",
			"Lifelink",
			"First strike",
			"Double strike",
			"Trample",
			"Menace",
		}.Count(c.Keywords.Contains);
		return 2 + power * 1.5 + toughness + keywords * 0.8;
	}
}
