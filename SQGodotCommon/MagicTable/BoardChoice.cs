using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;

namespace MagicTable;

/// <summary>
/// A pending choice answered on the table, as on Arena: the objects it offers light up where they
/// are (a permanent, a card in your hand, a spell on the stack, a player) and are clicked there.
/// Options that are not objects ("Don't attack", "none") become buttons. A choice that offers
/// anything NOT on the table — the library, a graveyard, their hand — or that is not about objects
/// at all (modes, colours, yes/no) stays in the <see cref="ChoicePanel"/>. Plain C#.
/// </summary>
/// <param name="Clickable">The offered objects, all on the table.</param>
/// <param name="Buttons">The offered non-objects.</param>
/// <param name="Multi">Several picks, or none allowed: clicks toggle, Submit answers. Otherwise a click answers.</param>
/// <param name="AboutId">The object the question is about (the attacker, the trigger's source); 0 if none.</param>
public sealed record BoardChoice(
	ChoiceAction Choice,
	IReadOnlySet<int> Clickable,
	IReadOnlyList<ChoiceOption> Buttons,
	bool Multi,
	int AboutId
)
{
	/// <summary>Null when the choice belongs in the panel.</summary>
	public static BoardChoice? For(GameState s, ChoiceAction choice, IReadOnlySet<int> onTable)
	{
		if (!MagicRules.OptionsAreObjects(choice))
			return null;
		bool IsObject(ChoiceOption o) => o.Id != 0 && s.HasObject(o.Id);
		var objects = choice
			.Options.Where(o => o.IsEnabled && IsObject(o))
			.Select(o => o.Id)
			.ToHashSet();
		if (objects.Count == 0 || !objects.All(onTable.Contains))
			return null;
		return new(
			choice,
			objects,
			choice.Options.Where(o => o.IsEnabled && !IsObject(o)).ToList(),
			IsMulti(choice),
			About(choice)
		);
	}

	public static bool IsMulti(ChoiceAction choice) =>
		choice.MaxChoices > 1 || choice.MinChoices == 0;

	/// <summary>"choose 2" / "choose 0 to 1" — for the board prompt and the panel alike.</summary>
	public static string Range(ChoiceAction choice) =>
		choice.MinChoices == choice.MaxChoices
			? $"choose {choice.MinChoices}"
			: $"choose {choice.MinChoices} to {choice.MaxChoices}";

	private static int About(ChoiceAction choice) =>
		choice switch
		{
			DeclareAttackerChoice a => a.CreatureId,
			DeclareBlockerChoice b => b.CreatureId,
			ChooseTargetChoice t => t.SourceId,
			_ => 0,
		};
}
