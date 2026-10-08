using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
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

/// <summary>"Search your library for a [Type] card" — any one of them, or none (a search may fail to find).</summary>
public record ChooseFromLibraryChoice : ChoiceAction
{
	public int PlayerId { get; init; }
	public CardType Type { get; init; }

	public ChooseFromLibraryChoice()
	{
		OutputKey = ChooseCardsFromHandChoice.Chosen;
		MinChoices = 0;
		MaxChoices = 1;
	}

	/// <summary>By name, not library order — the order is hidden information.</summary>
	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		s.CardsIn(PlayerId, ZoneType.Library)
			.Where(c => c.Is(Type))
			.DistinctBy(c => c.Name) // two Forests are one choice: the library is shuffled after
			.OrderBy(c => c.Name)
			.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name })
			.ToImmutableList();

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>The chosen cards go to a zone of their owner's — a searched land to the hand or graveyard.</summary>
public record MoveChosenToAction(int PlayerId, ZoneType To) : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var id in ChooseCardsFromHandChoice.Read(InputContext))
			s = s.MoveCard(id, s.ZoneId(PlayerId, To));
		return new ActionResult(s);
	}
}

public record ShuffleLibraryAction(int PlayerId) : GameAction
{
	public override ActionResult Execute(GameState s) => new(s.Shuffle(PlayerId));
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
