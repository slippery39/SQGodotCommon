using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Turns;

namespace MagicCore.Actions;

/// <summary>Pick N cards from your own hand — mulligan bottoms, discard to hand size.</summary>
public record ChooseCardsFromHandChoice : ChoiceAction
{
	public const string Chosen = "chosen";

	/// <summary>Whose hand.</summary>
	public int PlayerId { get; init; }

	/// <summary>Who chooses, if not the hand's owner — Solve for Disappointment: "You choose … from it."</summary>
	public int DeciderId { get; init; }

	/// <summary>Only cards of these types ("a nonland permanent card"); None means any.</summary>
	public CardType Types { get; init; }

	public ChooseCardsFromHandChoice() => OutputKey = Chosen;

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		s.CardsIn(PlayerId, ZoneType.Hand)
			.Where(c => Types == CardType.None || c.Is(Types))
			.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name })
			.ToImmutableList();

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => DeciderId != 0 ? DeciderId : PlayerId;

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

	/// <summary>Landcycling: "a basic land card". None means any.</summary>
	public Supertype Supertype { get; init; }

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
			.Where(c => c.Is(Type) && (Supertype == Supertype.None || c.Is(Supertype)))
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
	/// <summary>Simulacrum Shaper: "onto the battlefield tapped".</summary>
	public bool Tapped { get; init; }

	/// <summary>Loyal Tutor: to the top of the library, not the bottom.</summary>
	public bool OnTop { get; init; }

	public override ActionResult Execute(GameState s)
	{
		foreach (var id in ChooseCardsFromHandChoice.Read(InputContext))
			s =
				To == ZoneType.Battlefield
					? s.EnterBattlefield(id, PlayerId, tapped: Tapped) // the one way in: its entry, its events
					: s.MoveCard(id, s.ZoneId(PlayerId, To), toTop: OnTop);
		return new ActionResult(s);
	}
}

public record ShuffleLibraryAction(int PlayerId) : GameAction
{
	public override ActionResult Execute(GameState s) => new(s.Shuffle(PlayerId));
}

/// <summary>Something Worth Saving: "You may put a [Types] card from among them into your hand."</summary>
public record ChooseAmongChoice : ChoiceAction
{
	public int PlayerId { get; init; }
	public ImmutableList<int> Ids { get; init; } = [];
	public CardType Types { get; init; }

	public ChooseAmongChoice()
	{
		OutputKey = ChooseCardsFromHandChoice.Chosen;
		MinChoices = 0;
		MaxChoices = 1;
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		Ids.Where(id => s.IsIn(id, PlayerId, ZoneType.Graveyard))
			.Select(s.GetCard)
			.Where(c => c.Is(Types))
			.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name })
			.ToImmutableList();

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>"Choose a color." Option id = ManaColor + 1: 0 means "none" across this engine, so no answer may be 0.</summary>
public record ChooseColorChoice : ChoiceAction
{
	public const string Chosen = "color";
	public int PlayerId { get; init; }

	/// <summary>The option id for a color, and back — the ONE place the +1 lives.</summary>
	public static int IdOf(ManaColor color) => (int)color + 1;

	public static ManaColor ColorOf(int id) => (ManaColor)(id - 1);

	public ChooseColorChoice(int playerId)
	{
		PlayerId = playerId;
		Prompt = "Choose a color";
		OutputKey = Chosen;
		MinChoices = 1;
		MaxChoices = 1;
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		[
			.. ManaAbilities.AnyColor.Select(c => new ChoiceOption
			{
				Id = IdOf(c),
				DisplayText = c.ToString(),
			}),
		];

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>Room of Refuge: its mana is the chosen color — while it stays this object.</summary>
public record SetChosenColorAction(int CardId) : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		if (
			!s.IsOnBattlefield(CardId)
			|| InputContext.GetValueOrDefault(ChooseColorChoice.Chosen) is not int color
		)
			return new ActionResult(s);
		return new ActionResult(
			s.UpdateObject(
				CardId,
				s.GetCard(CardId)
					.WithComponent(new Effects.ChosenColorMana(ChooseColorChoice.ColorOf(color)))
			)
		);
	}
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
/// The chosen cards go to their owner's graveyard. From the HAND that is a discard (MagicState.Discard:
/// the event, the count); from the library (surveil) it is not. Never use it to put a card from the
/// hand into the graveyard any other way — it would count as a discard.
/// </summary>
public record MoveChosenToGraveyardAction : GameAction
{
	public int PlayerId { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var graveyardId = s.ZoneId(PlayerId, ZoneType.Graveyard);
		var chosen = ChooseCardsFromHandChoice.Read(InputContext).ToList();
		var fromHand = chosen.Where(id => s.IsIn(id, PlayerId, ZoneType.Hand)).ToList();
		foreach (var id in chosen.Except(fromHand))
			s = s.MoveCard(id, graveyardId); // surveil: from the library, not a discard
		return new ActionResult(s.Discard(PlayerId, fromHand));
	}
}

/// <summary>Finishes the cleanup after it paused for the discard to hand size.</summary>
public record EndStepAction : GameAction
{
	public override ActionResult Execute(GameState s) => new(TurnStructure.FinishCleanup(s));
}
