using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;

namespace MagicTable;

/// <summary>
/// Arena's "click the spell, then click its target": a card's legal moves differ only by their
/// targets, so they are narrowed one target at a time by clicking objects on the table. Plain C#;
/// the moves themselves come from LegalActions — nothing here decides what is legal.
/// </summary>
public static class Targeting
{
	public static ImmutableArray<int> TargetsOf(GameAction move) =>
		move switch
		{
			CastSpellAction c => c.Targets,
			ActivateAbilityAction a => a.Targets,
			ActivateLoyaltyAction l => l.Targets,
			_ => [],
		};

	/// <summary>
	/// A card's moves as plays: the same spell or ability with the same modes, X and costs is one
	/// play whatever it targets. Each play is offered once; its targets are clicked afterwards.
	/// </summary>
	public static List<List<GameAction>> Plays(GameState s, IEnumerable<GameAction> moves) =>
		moves
			.GroupBy(m =>
				$"{Describe.Action(s, WithoutTargets(m))}|{(m as ActivateAbilityAction)?.AbilityIndex}"
			)
			.Select(g => g.ToList())
			.ToList();

	/// <summary>
	/// The first target index, from <paramref name="from"/> on, where the candidates still differ —
	/// the next thing to click. -1 when they can't be told apart by a target (one move left, or
	/// different numbers of targets), and the table lists them instead.
	/// </summary>
	public static int NextIndex(IReadOnlyList<GameAction> candidates, int from)
	{
		if (candidates.Count < 2)
			return -1;
		var lengths = candidates.Select(c => TargetsOf(c).Length).Distinct().ToList();
		if (lengths.Count != 1)
			return -1;
		for (var i = from; i < lengths[0]; i++)
			if (candidates.Select(c => TargetsOf(c)[i]).Distinct().Count() > 1)
				return i;
		return -1;
	}

	/// <summary>The objects that may be clicked for target <paramref name="index"/>.</summary>
	public static IReadOnlyList<int> Options(IReadOnlyList<GameAction> candidates, int index) =>
		candidates.Select(c => TargetsOf(c)[index]).Distinct().ToList();

	/// <summary>The candidates that target <paramref name="id"/> at <paramref name="index"/>.</summary>
	public static List<GameAction> Narrow(IEnumerable<GameAction> candidates, int index, int id) =>
		candidates.Where(c => TargetsOf(c)[index] == id).ToList();

	/// <summary>A play's label, its targets left out — they are clicked, not listed.</summary>
	public static string Label(GameState s, GameAction move) =>
		Describe.Action(s, WithoutTargets(move));

	private static GameAction WithoutTargets(GameAction move) =>
		move switch
		{
			CastSpellAction c => c with { Targets = [] },
			ActivateAbilityAction a => a with { Targets = [] },
			ActivateLoyaltyAction l => l with { Targets = [] },
			_ => move,
		};
}
