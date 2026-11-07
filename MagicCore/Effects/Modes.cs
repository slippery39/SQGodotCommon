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
/// How many modes are chosen (CR 700.2) — on a spell, a triggered and an activated ability alike. "Choose one
/// —" is 1 (an int converts: exactly that many); Ertai Resurrected's "choose up to one —" is { AtLeast = 0 };
/// Sublime Epiphany's "choose one or more —" is { AtMost = 5 }. Counted as the modes are chosen, with the
/// source as "this".
/// </summary>
public record ModeChoice
{
	public Count AtLeast { get; init; } = 1;
	public Count AtMost { get; init; } = 1;

	/// <summary>
	/// Captain James T. Kirk: "choose one. If you have no cards in hand, choose one or more instead." —
	/// judged as the modes are chosen: then any number from AtLeast up.
	/// </summary>
	public Condition? AnyNumberIf { get; init; }

	/// <summary>
	/// Kobayashi Maru Scenario: "At the beginning of your upkeep, choose one that hasn't been chosen —" — the
	/// modes this object chose before (Card.ModesChosen) aren't offered; none left and it is removed (CR
	/// 700.2a). A new object starts afresh (CR 400.7). Read for triggered abilities.
	/// </summary>
	public bool NotChosenBefore { get; init; }

	public static implicit operator ModeChoice(int n) => new() { AtLeast = n, AtMost = n };

	/// <summary>
	/// The least and most modes that may be chosen now, of <paramref name="modes"/> — counted with a
	/// trigger's own context when it has one (Riku of Many Paths: "that spell" is its event's card).
	/// </summary>
	public (int Least, int Most) Range(
		GameState s,
		Cards.Card source,
		int modes,
		ImmutableDictionary<string, object>? context = null
	)
	{
		context ??= Conditions.Of(source);
		var most = AnyNumberIf?.Holds(s, context) == true ? modes : AtMost.Value(s, context);
		return (AtLeast.Value(s, context), Math.Min(most, modes));
	}

	public bool IsLegal(
		GameState s,
		Cards.Card source,
		ImmutableList<Mode> modes,
		ImmutableArray<int> chosen
	)
	{
		var (least, most) = Range(s, source, modes.Count);
		return Modal.IsLegal(modes, least, most, chosen);
	}

	public IEnumerable<ImmutableArray<int>> Choices(
		GameState s,
		Cards.Card source,
		ImmutableList<Mode> modes
	)
	{
		var (least, most) = Range(s, source, modes.Count);
		return Modal.Choices(modes, least, most);
	}
}

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
	/// A legal choice of modes: from <paramref name="least"/> to <paramref name="most"/> distinct, in
	/// range — or none for a non-modal text.
	/// </summary>
	public static bool IsLegal(
		ImmutableList<Mode> modes,
		int least,
		int most,
		ImmutableArray<int> chosen
	) =>
		modes.IsEmpty
			? chosen.IsDefaultOrEmpty
			: chosen.Length >= least
				&& chosen.Length <= most
				&& chosen.Distinct().Count() == chosen.Length
				&& chosen.All(i => i >= 0 && i < modes.Count);

	/// <summary>
	/// Every way to choose from <paramref name="least"/> to <paramref name="most"/> of the modes (just
	/// the empty choice when not modal).
	/// </summary>
	public static IEnumerable<ImmutableArray<int>> Choices(
		ImmutableList<Mode> modes,
		int least,
		int most
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
		// least past most is a mis-scripted card: no way to cast it, not a crash
		return Enumerable.Range(least, Math.Max(0, most - least + 1)).SelectMany(n => Pick(0, n));
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
