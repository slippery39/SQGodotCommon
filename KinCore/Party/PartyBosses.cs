using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

// **BOSSES AND ELITES — what makes them hard** (`KinFamiliesPlan.md`, round 3; Shayne, 2026-09-28):
// big TELEGRAPHED turns (the wind-up), PHASES, RULE-BENDERS (Shell, Enrage) and MINIONS (a summon
// that does not fade). Each is data on a `Foe`; the hooks are `HitFoe`, `Act` and the turn start.

/// <summary>**SHELL: a hit of this much or less does nothing to it** — chip damage and tokens bounce.</summary>
public record Shell : GameComponent
{
	public int AtMost { get; init; } = 4;
}

/// <summary>**ENRAGE: its attacks deal this much more every round** — a race the slow deck loses.</summary>
public record Enrage : GameComponent
{
	public int PerRound { get; init; } = 2;
}

/// <summary>
/// **A PHASE: at this share of its max HP, once, it changes** — a new pattern (empty keeps the old),
/// a burst of Block, new rules, and a line for the inspector. The component is spent when it fires.
/// </summary>
public record Phase : GameComponent
{
	public string Name { get; init; } = "";
	public double AtShare { get; init; } = 0.5;
	public ImmutableList<Intent> Pattern { get; init; } = [];
	public int Block { get; init; }
	public ImmutableList<GameComponent> Gains { get; init; } = [];
	public string Trait { get; init; } = "";
}

/// <summary>A foe crossed into its second phase — the screen says so.</summary>
public record FoePhaseEvent : GameEvent
{
	public int FoeId { get; init; }
	public string Name { get; init; } = "";
}

public static class PartyBosses
{
	/// <summary>SHELL: what a hit of this size really does to this foe.</summary>
	public static int ThroughShell(Foe foe, int amount) =>
		foe.GetComponent<Shell>() is { } shell && amount <= shell.AtMost ? 0 : amount;

	/// <summary>
	/// **After a hit: has it crossed into its PHASE?** Once — the component is removed as it fires.
	/// Its pattern restarts from the phase's first move, so the new telegraph shows at once.
	/// </summary>
	public static (GameState, ImmutableList<GameEvent>) CheckPhase(GameState s, int foeId)
	{
		if (
			s.GetObject(foeId) is not Foe { IsDead: false } foe
			|| foe.GetComponent<Phase>() is not { } phase
			|| foe.Hp > foe.MaxHp * phase.AtShare
		)
			return (s, []);

		s = s.UpdateObject(
			foe.Id,
			foe with
			{
				Pattern = phase.Pattern.IsEmpty ? foe.Pattern : phase.Pattern,
				PatternIndex = phase.Pattern.IsEmpty ? foe.PatternIndex : 0,
				Block = foe.Block + phase.Block,
				Trait = phase.Trait.Length > 0 ? phase.Trait : foe.Trait,
				Components = [.. foe.Components.Where(c => c is not Phase), .. phase.Gains],
			}
		);
		return (s, [new FoePhaseEvent { FoeId = foe.Id, Name = phase.Name }]);
	}

	/// <summary>
	/// **A new round: ENRAGE sharpens every attack in its pattern.** Changing the pattern itself keeps
	/// the telegraph, the forecast and the blow on one number.
	/// </summary>
	public static GameState RoundStarts(GameState s)
	{
		foreach (var foe in s.LivingFoes().ToList())
			if (foe.GetComponent<Enrage>() is { } rage)
				s = s.UpdateObject(
					foe.Id,
					foe with
					{
						Pattern =
						[
							.. foe.Pattern.Select(i =>
								i.Kind == IntentType.Attack
									? i with
									{
										Amount = i.Amount + rage.PerRound,
									}
									: i
							),
						],
					}
				);
		return s;
	}

	/// <summary>
	/// **TONGUE: your BACK monster is dragged to your FRONT** — where the next blow lands. Order is
	/// the answer: put the one who can take it at the back.
	/// </summary>
	public static GameState PullBackToFront(GameState s) =>
		s.LivingAllies().LastOrDefault() is { Position: > 0 } back ? s.MoveInLine(back.Id, 0) : s;
}
