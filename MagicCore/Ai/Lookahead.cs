using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;

namespace MagicCore.Ai;

/// <summary>
/// Playing a simulated game forward, the cheap way every MagicCore AI looks ahead: see what a move
/// leads to (<see cref="Settle"/>), and answer the choices a simulation meets without searching them.
/// </summary>
public static class Lookahead
{
	/// <summary>
	/// Plays forward until the move's consequence is visible: answers choices cheaply, passes for
	/// both players until the stack is empty, and — mid-combat — until the damage is dealt.
	/// </summary>
	public static GameState Settle(GameState s)
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

	public static bool InCombat(GameState s) =>
		s.GetGame().Step is >= Step.DeclareAttackers and <= Step.CombatDamage
		&& s.GetGame().PriorityPlayerId != 0;

	/// <summary>Inside a simulation: nobody else attacks, blockers only take blocks they survive, targets are the first legal.</summary>
	public static IEnumerable<int> QuickAnswer(GameState s, ChoiceAction choice)
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
}
