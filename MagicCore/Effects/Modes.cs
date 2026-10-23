using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Targeting;

namespace MagicCore.Effects;

/// <summary>One bullet of a "Choose one —" text: its printed line, its target words, its effects.</summary>
public record Mode(
	string Text,
	ImmutableList<TargetSpec> Targets,
	ImmutableList<GameAction> Effects
);

/// <summary>
/// Modal spells and abilities (CR 700.2). The chosen modes become ONE list of target words and ONE
/// list of steps, in printed order. Before each mode's effects a <see cref="ScopeTargetsAction"/>
/// narrows "the targets" to that mode's own slice, so a mode's effects always say Target(0) for its
/// first target, wherever the mode sits.
/// </summary>
public static class Modal
{
	public static (ImmutableList<TargetSpec> Specs, ImmutableList<GameAction> Steps) Compose(
		ImmutableList<TargetSpec> targets,
		ImmutableList<GameAction> effects,
		ImmutableList<Mode> modes,
		ImmutableArray<int> chosen
	)
	{
		if (modes.IsEmpty)
			return (targets, effects);
		if (!targets.IsEmpty || !effects.IsEmpty)
			throw new InvalidOperationException(
				"A modal text has a fixed part as well as modes — it would never run"
			);
		var specs = ImmutableList.CreateBuilder<TargetSpec>();
		var steps = ImmutableList.CreateBuilder<GameAction>();
		foreach (var mode in chosen.Order().Select(i => modes[i]))
		{
			steps.Add(new ScopeTargetsAction(specs.Count, mode.Targets.Count));
			steps.AddRange(mode.Effects);
			specs.AddRange(mode.Targets);
		}
		return (specs.ToImmutable(), steps.ToImmutable());
	}

	/// <summary>
	/// A legal choice of modes: <paramref name="choose"/> distinct, in range (from
	/// <paramref name="atLeast"/> for "choose one or more") — or none for a non-modal text.
	/// </summary>
	public static bool IsLegal(
		ImmutableList<Mode> modes,
		int choose,
		ImmutableArray<int> chosen,
		int? atLeast = null
	) =>
		modes.IsEmpty
			? chosen.IsDefaultOrEmpty
			: chosen.Length >= (atLeast ?? choose)
				&& chosen.Length <= choose
				&& chosen.Distinct().Count() == chosen.Length
				&& chosen.All(i => i >= 0 && i < modes.Count);

	/// <summary>
	/// Every way to choose <paramref name="choose"/> of the modes — or any number from
	/// <paramref name="atLeast"/> up (just the empty choice when not modal).
	/// </summary>
	public static IEnumerable<ImmutableArray<int>> Choices(
		ImmutableList<Mode> modes,
		int choose,
		int? atLeast = null
	)
	{
		if (modes.IsEmpty)
			return
			[
				[],
			];
		IEnumerable<ImmutableArray<int>> Pick(int from, int left) =>
			left == 0
				?
				[
					[],
				]
				: Enumerable
					.Range(from, modes.Count - from)
					.SelectMany(i => Pick(i + 1, left - 1).Select(rest => rest.Insert(0, i)));
		var least = atLeast ?? choose; // more than choose is a mis-scripted card: no way to cast it, not a crash
		return Enumerable.Range(least, Math.Max(0, choose - least + 1)).SelectMany(n => Pick(0, n));
	}
}

/// <summary>Narrows the context's targets to one mode's slice of all chosen targets.</summary>
public record ScopeTargetsAction(int Offset, int Count) : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		var all = GetInput<ImmutableArray<int>>(ContextKeys.AllTargets);
		return new ActionResult(s).WithOutput(
			ContextKeys.Targets,
			all.Skip(Offset).Take(Count).ToImmutableArray()
		);
	}
}
