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
	public static GameState Settle(GameState s, bool allAttack = false) =>
		Settle(s, allAttack, Evaluator.IsOff(Evaluator.Terms.SettleDeep) ? 1 : 2);

	/// <param name="choose">How many NESTED choices deep the chooser's best is still asked (a choice made while judging another's answer counts one deeper; choices one after another don't) — the cheap answers below.</param>
	static GameState Settle(GameState s, bool allAttack, int choose)
	{
		for (var i = 0; i < 200 && !s.IsOver(); i++)
		{
			if (s.GetPendingChoice() is { } choice)
				s = s.Choose(
					allAttack && choice is DeclareAttackerChoice attacker
						? [s.OpponentOf(attacker.PlayerId)]
					: choose > 0 && IsChosen(choice) ? Chosen(s, choice, choose - 1)
					: QuickAnswer(s, choice)
				);
			else if (!s.StackIsEmpty() || InCombat(s))
				s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
			else
				break;
		}
		return s;
	}

	/// <summary>
	/// A choice Settle answers by its chooser's best (<see cref="Evaluator.Terms.SettleChoices"/>, AI-17.6): one
	/// target, or a single "you may" (none or one — a pay-to-keep pays, as the cheap answer does) — and
	/// under <see cref="Evaluator.Terms.SettleDeep"/> any single pick, a forced sacrifice included (an edict
	/// took the first listed: their best). Combat declarations keep their own rules (QuickAnswer).
	/// </summary>
	static bool IsChosen(ChoiceAction choice) =>
		!Evaluator.IsOff(Evaluator.Terms.SettleChoices)
		&& choice is not (DeclareAttackerChoice or DeclareBlockerChoice or PayToKeepChoice)
		&& choice.MaxChoices == 1
		&& (
			choice is ChooseTargetChoice
			|| choice.MinChoices == 0
			|| !Evaluator.IsOff(Evaluator.Terms.SettleDeep)
				&& choice.Options.Count <= MaxForcedPicks
		);

	/// <summary>
	/// The most options a forced single pick (not a target, not a "you may") is judged over — past it, the
	/// cheap answer: "pay any amount of life" offers ~20, judged at both levels (the review, AI-17.6).
	/// </summary>
	const int MaxForcedPicks = 8;

	/// <summary>
	/// Each answer (none too, if optional) settled <paramref name="deeper"/> more choices down — two in all
	/// under <see cref="Evaluator.Terms.SettleDeep"/>, so Lich's Relic's "you may pay {2}. When you do,
	/// destroy …" is judged with its target chosen; the cheap answers below, so nested triggers can't multiply
	/// the cost without bound — and the best for whoever chooses: GreedyAi's way, inside Settle.
	/// Lich's Relic ("you may pay {2}. When you do, destroy …") measured 2 in the card-value sandbox, the
	/// "you may" declined; now 16.5.
	/// Before, a trigger's target was the first legal one (ours are listed first: "destroy target creature"
	/// killed our own) and every "you may" was declined, and Settle ranks which children the search rolls
	/// out (BeamSearchAi.Narrow), which play our rollout makes, and the simulated opponent's plays.
	/// ponytail: hand cards at the flat price (Settle has no held values) — pass them in if a pick between
	/// two cards in hand is ever misjudged.
	/// </summary>
	static IEnumerable<int> Chosen(GameState s, ChoiceAction choice, int deeper)
	{
		var who = s.DecidingPlayerId();
		var picks = choice.Options.Select(o => (IEnumerable<int>)[o.Id]).ToList();
		if (choice.MinChoices == 0)
			picks.Insert(0, []);
		if (picks.Count == 1)
			return picks[0]; // nothing to weigh
		return picks.MaxBy(pick => Evaluator.Evaluate(Settle(s.Choose(pick), false, deeper), who))!;
	}

	public static bool InCombat(GameState s) =>
		s.GetGame().Step is >= Step.DeclareAttackers and <= Step.CombatDamage
		&& s.GetGame().PriorityPlayerId != 0;

	/// <summary>
	/// Inside a simulation: nobody else attacks, blockers only take blocks they survive — or join one
	/// already declared (<see cref="Joins"/>), or chump the biggest attacker when what is left unblocked
	/// would kill them (<see cref="Chumps"/>) — and targets are the first legal.
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
			// facing lethal, the chump plan comes first: a block it survives on a smaller attacker would
			// leave the planned chump unmade (the review, AI-17.3)
			if (
				!Evaluator.IsOff(Evaluator.Terms.ChumpBlocks)
				&& Chumps(s, blocker, choice) is var chump and not CombatOption.None
			)
				return [chump];
			if (safe == CombatOption.None && !Evaluator.IsOff(Evaluator.Terms.GangBlocks))
				safe = Joins(s, blocker, choice);
			return [safe];
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
	/// without a second, when the two kill it or this one survives it (a chump is <see cref="Chumps"/>'s,
	/// facing lethal only). The rollout's defender tries "block X" with each blocker (Rollout.BestAlone),
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
	/// The attacker this blocker chumps (story AI-17.3), or none — when the damage the attackers not yet
	/// blocked would deal its controller is lethal. Shayne: "you always want to chump with your least
	/// valuable creatures first" — so the chumps are planned for every blocker still to be asked: the
	/// biggest unblocked attacker first, each taken by the least valuable creature that can block it
	/// (<see cref="Evaluator.ValueOf"/>), until what is left no longer kills. And when the least valuable
	/// free blockers TOGETHER kill that attacker, they all block it (Shayne: a 4/4 and a 1/1 against a 5/5
	/// "should actually be blocked by both"; against a 6/6, the 1/1 alone). This blocker blocks only if it
	/// is one of them; the engine asks one at a time, and each asks again with the blocks made so far.
	/// ponytail: trample's excess, first strike and lifelink aren't counted; a creature's value is the
	/// evaluator's (stats and keywords, not its abilities — AI-9.6); a menace attacker is never chumped
	/// (one blocker alone is dropped; Joins pairs them).
	/// </summary>
	static int Chumps(GameState s, Card blocker, ChoiceAction choice)
	{
		if (s.ActionStack.Peek() is not PipelineAction declaring)
			return CombatOption.None;
		var declared = Declarations
			.Read(declaring.PipelineContext, DeclareBlockerChoice.KeyPrefix)
			.ToList();
		var player = blocker.ControllerId;
		int Damage(Card a) =>
			Math.Max(0, Turns.Combat.Assigns(s, a)) * (s.Has(a, "Double strike") ? 2 : 1);
		bool Blocked(Card a) =>
			declared.Count(d => d.ChosenId == a.Id) is var n
			&& (s.Has(a, "Menace") ? n >= 2 : n >= 1);
		var unblocked = Turns
			.Combat.Attackers(s)
			.Where(a => a.AttackingId == player && !Blocked(a))
			.OrderByDescending(Damage)
			.ThenBy(a => a.Id)
			.ToList();
		var life = s.GetPlayer(player).Life;
		var left = unblocked.Sum(Damage);
		if (left < life)
			return CombatOption.None;
		var free = s.CardsIn(player, ZoneType.Battlefield)
			.Where(c => c.Is(CardType.Creature) && declared.All(d => d.CreatureId != c.Id))
			.OrderBy(c => Evaluator.ValueOf(s, c))
			.ThenBy(c => c.Id)
			.ToList();
		// one blocker alone on a menace attacker is dropped: no chump there (Joins pairs them)
		foreach (var attacker in unblocked.Where(a => !s.Has(a, "Menace")))
		{
			if (left < life)
				break;
			var able = free.Where(b => Turns.Combat.CanBlock(s, b, attacker)).ToList();
			if (able.Count == 0)
				continue;
			// the fewest of the least valuable that kill it, if any do; else the least valuable alone
			var gang = able.Take(1).ToList();
			var need = s.Toughness(attacker) - attacker.Damage;
			for (
				var i = 1;
				i < able.Count && !s.IsIndestructible(attacker) && Dealt(gang) < need;
				i++
			)
				gang.Add(able[i]);
			if (s.IsIndestructible(attacker) || Dealt(gang) < need)
				gang = able.Take(1).ToList();
			if (gang.Any(b => b.Id == blocker.Id))
				return choice.Options.Any(o => o.Id == attacker.Id)
					? attacker.Id
					: CombatOption.None;
			free.RemoveAll(gang.Contains);
			left -= Damage(attacker);
		}
		return CombatOption.None;

		int Dealt(IEnumerable<Card> blockers) =>
			blockers.Any(b => s.Has(b, "Deathtouch") && Turns.Combat.Assigns(s, b) > 0)
				? int.MaxValue
				: blockers.Sum(b => Math.Max(0, Turns.Combat.Assigns(s, b)));
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
