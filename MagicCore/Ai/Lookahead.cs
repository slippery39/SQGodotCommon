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
	/// <paramref name="allAttack"/>: every attacker still to be declared attacks the player, where
	/// otherwise nobody else attacks (the rollout's all-in judgment, story AI-3.8).
	/// </summary>
	public static GameState Settle(GameState s, bool allAttack = false)
	{
		for (var i = 0; i < 200 && !s.IsOver(); i++)
		{
			if (s.GetPendingChoice() is { } choice)
				s = s.Choose(
					allAttack && choice is DeclareAttackerChoice attacker
						? [s.OpponentOf(attacker.PlayerId)]
						: QuickAnswer(s, choice)
				);
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

	/// <summary>
	/// Inside a simulation: nobody else attacks, blockers only take blocks they survive — or join one
	/// already declared (<see cref="Joins"/>) — and targets are the first legal.
	/// </summary>
	public static IEnumerable<int> QuickAnswer(GameState s, ChoiceAction choice)
	{
		if (choice is DeclareBlockerChoice block)
		{
			var blocker = s.GetCard(block.CreatureId);
			var safe = choice
				.Options.Where(o =>
					o.Id != CombatOption.None && Survives(s, blocker, s.GetCard(o.Id))
				)
				.Select(o => o.Id)
				.FirstOrDefault(CombatOption.None);
			return
			[
				safe != CombatOption.None || Evaluator.IsOff(Evaluator.Terms.GangBlocks)
					? safe
					: Joins(s, blocker, choice),
			];
		}
		if (choice is DeclareAttackerChoice)
			return [CombatOption.None];
		if (choice is PayToKeepChoice)
			return choice.Options.Select(o => o.Id).Take(1); // looking ahead: keep the spell if it can be paid
		return choice.Options.Take(choice.MinChoices).Select(o => o.Id);
	}

	/// <summary>
	/// The attacker this blocker gang-blocks (story AI-15), or none: one already blocked this declaration
	/// that the blockers kill only with it — or a menace attacker blocked by one, whose block is dropped
	/// without a second, when the two kill it or this one survives it (never a chump: the look-ahead
	/// doesn't chump-block). The rollout's defender tries "block X" with each blocker (Rollout.BestAlone),
	/// so a first blocker starts the gang block and the rest join here; alone, the look-ahead never
	/// starts one (it blocks only what it survives).
	/// ponytail: damage summed — first strike, the attacker's split and deathtouch on the attacker's side
	/// are not seen (a second "is it lethal" beside Survives'); count them if a misjudged gang block is
	/// shown (DesignNotes, MagicCore known ceilings).
	/// </summary>
	static int Joins(GameState s, Card blocker, ChoiceAction choice)
	{
		if (s.ActionStack.Peek() is not PipelineAction declaring)
			return CombatOption.None;
		var declared = Declarations
			.Read(declaring.PipelineContext, DeclareBlockerChoice.KeyPrefix)
			.ToList();
		bool Kill(Card attacker, IEnumerable<Card> blockers) =>
			!s.IsIndestructible(attacker)
			&& (
				blockers.Any(b => s.Has(b, "Deathtouch") && Turns.Combat.Assigns(s, b) > 0)
				|| blockers.Sum(b => Turns.Combat.Assigns(s, b))
					>= s.Toughness(attacker) - attacker.Damage
			);
		foreach (var option in choice.Options.Where(o => o.Id != CombatOption.None))
		{
			var attacker = s.GetCard(option.Id);
			var with = declared
				.Where(d => d.ChosenId == option.Id)
				.Select(d => s.GetCard(d.CreatureId))
				.ToList();
			if (with.Count == 0)
				continue;
			var kills = Kill(attacker, [.. with, blocker]);
			if (
				with.Count == 1
					&& s.Has(attacker, "Menace")
					&& (kills || Survives(s, blocker, attacker))
				|| kills && !Kill(attacker, with)
			)
				return option.Id;
		}
		return CombatOption.None;
	}

	/// <summary>
	/// Does <paramref name="blocker"/> live through blocking <paramref name="attacker"/>? Counted by the
	/// hits that land: a double striker strikes twice — unless a first-striking blocker kills it in the
	/// first-strike step, when no later hit comes (nor any hit from an attacker without first strike).
	/// Deathtouch kills with any damage; damage already marked counts. Counting the damage once let a
	/// 7/5 block a double-striking 4/3 in every look-ahead and die (story AI-8.3, Shayne's sealed game).
	/// ponytail: one attacker's full damage on this one blocker — split among several blockers,
	/// prevented, or stopped by protection is not seen; count them if a misjudged block is shown.
	/// </summary>
	static bool Survives(GameState s, Card blocker, Card attacker)
	{
		if (s.IsIndestructible(blocker))
			return true;
		var assigns = Turns.Combat.Assigns(s, attacker);
		if (Evaluator.IsOff(Evaluator.Terms.BlockSurvival)) // the check before AI-8.3, to measure against
			return assigns < s.Toughness(blocker);
		bool First(Card c) => s.Has(c, "First strike") || s.Has(c, "Double strike");
		var killedFirst =
			First(blocker)
			&& (
				s.Has(blocker, "Deathtouch") && Turns.Combat.Assigns(s, blocker) > 0
				|| Turns.Combat.Assigns(s, blocker) >= s.Toughness(attacker) - attacker.Damage
			);
		var hits =
			killedFirst ? (First(attacker) ? 1 : 0)
			: s.Has(attacker, "Double strike") ? 2
			: 1;
		var damage = assigns * hits;
		return damage <= 0
			|| !s.Has(attacker, "Deathtouch") && damage < s.Toughness(blocker) - blocker.Damage;
	}
}
