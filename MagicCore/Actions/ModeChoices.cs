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
		ImmutableList<int> choosable
	)
	{
		PlayerId = playerId;
		SourceId = sourceId;
		Modes = ability.Modes;
		Choosable = choosable;
		MinChoices = MaxChoices = ability.Choose;
		OutputKey = Chosen;
		Prompt = ability.Choose == 1 ? "Choose one" : $"Choose {ability.Choose}";
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
