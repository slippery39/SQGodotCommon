using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Turns;

namespace MagicCore.Actions;

/// <summary>Pick N cards from your own hand — mulligan bottoms, discard to hand size.</summary>
public record ChooseCardsFromHandChoice : ChoiceAction
{
	public const string Chosen = "chosen";

	public int PlayerId { get; init; }

	public ChooseCardsFromHandChoice() => OutputKey = Chosen;

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		s.CardsIn(PlayerId, ZoneType.Hand)
			.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name })
			.ToImmutableList();

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;

	/// <summary>ResolveChoice stores one pick as an int and several as a list.</summary>
	public static IEnumerable<int> Read(ImmutableDictionary<string, object> context) =>
		context.GetValueOrDefault(Chosen) switch
		{
			int id => [id],
			ImmutableList<int> ids => ids,
			_ => [],
		};
}

public record PutChosenOnBottomAction : GameAction
{
	public int PlayerId { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var libraryId = s.ZoneId(PlayerId, ZoneType.Library);
		foreach (var id in ChooseCardsFromHandChoice.Read(InputContext))
			s = s.MoveCard(id, libraryId); // appends: the last child is the bottom
		return new ActionResult(s);
	}
}

/// <summary>
/// The chosen cards go to their owner's graveyard — discarding and surveiling alike. When a
/// discard trigger exists, discarding raises its own event here; surveil must not.
/// </summary>
public record MoveChosenToGraveyardAction : GameAction
{
	public int PlayerId { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var graveyardId = s.ZoneId(PlayerId, ZoneType.Graveyard);
		foreach (var id in ChooseCardsFromHandChoice.Read(InputContext))
			s = s.MoveCard(id, graveyardId);
		return new ActionResult(s);
	}
}

/// <summary>Resumes the turn after a step paused for a choice (cleanup discard).</summary>
public record EndStepAction : GameAction
{
	public override ActionResult Execute(GameState s) => new(TurnStructure.EndStep(s));
}
