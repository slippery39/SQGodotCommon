using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Targeting;

namespace MagicCore.Actions;

/// <summary>Choose one target for a triggered ability as it goes on the stack (CR 603.3d).</summary>
public record ChooseTargetChoice : ChoiceAction
{
	public const string KeyPrefix = "target:";

	public int PlayerId { get; init; }
	public int SourceId { get; init; }
	public TargetSpec Spec { get; init; }
	public int Index { get; init; }

	public ChooseTargetChoice(int playerId, int sourceId, TargetSpec spec, int index)
	{
		PlayerId = playerId;
		SourceId = sourceId;
		Spec = spec;
		Index = index;
		OutputKey = KeyPrefix + index;
		MinChoices = spec.Optional ? 0 : 1;
		MaxChoices = 1;
		Prompt = "Choose a target";
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		Spec.Candidates(s, PlayerId, SourceId)
			.Where(id =>
				!(
					Spec.NotThePrevious
					&& context.GetValueOrDefault(KeyPrefix + (Index - 1)) is int previous
					&& previous == id
				)
			)
			.Select(id => new ChoiceOption { Id = id, DisplayText = s.GetObject(id).Name })
			.ToImmutableList();

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;

	/// <summary>The chosen target per word, 0 where none was chosen.</summary>
	public static ImmutableArray<int> Read(
		ImmutableDictionary<string, object> context,
		int count
	) =>
		Enumerable
			.Range(0, count)
			.Select(i => context.TryGetValue(KeyPrefix + i, out var v) && v is int id ? id : 0)
			.ToImmutableArray();
}
