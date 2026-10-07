using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MagicCore.Actions;

/// <summary>Pick any of the top <see cref="Count"/> cards of your library — surveil's "put into your graveyard".</summary>
public record ChooseFromLibraryTopChoice : ChoiceAction
{
	public int PlayerId { get; init; }
	public int Count { get; init; }

	public ChooseFromLibraryTopChoice()
	{
		OutputKey = ChooseCardsFromHandChoice.Chosen;
		Prompt = "Put into your graveyard";
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		s.CardsIn(PlayerId, ZoneType.Library)
			.Take(Count)
			.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name })
			.ToImmutableList();

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}
