using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Effects;

namespace MagicCore.Actions;

/// <summary>"Choose one/two —" for a triggered ability as it goes on the stack. Option ids are mode indexes.</summary>
public record ChooseModesChoice : ChoiceAction
{
	public const string Chosen = "modes";

	public int PlayerId { get; init; }
	public int SourceId { get; init; }
	public ImmutableList<Mode> Modes { get; init; } = [];
	public ImmutableList<int> Choosable { get; init; } = [];

	public ChooseModesChoice(
		int playerId,
		int sourceId,
		TriggeredAbilityComponent ability,
		ImmutableList<int> choosable,
		int least,
		int most
	)
	{
		PlayerId = playerId;
		SourceId = sourceId;
		Modes = ability.Modes;
		Choosable = choosable;
		MinChoices = least;
		MaxChoices = most;
		OutputKey = Chosen;
		Prompt = (least, most) switch
		{
			(1, 1) => "Choose one",
			(0, 1) => "Choose up to one",
			_ when least == most => $"Choose {most}",
			(0, _) => $"Choose up to {most}",
			(1, _) => "Choose one or more",
			_ => $"Choose {least} to {most}",
		};
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		Choosable
			.Select(i => new ChoiceOption { Id = i, DisplayText = Modes[i].Text })
			.ToImmutableList();

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;

	public static ImmutableArray<int> Read(ImmutableDictionary<string, object> context) =>
		context.GetValueOrDefault(Chosen) switch
		{
			int i => [i],
			ImmutableList<int> list => [.. list],
			_ => [],
		};
}
