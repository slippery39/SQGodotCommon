using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Turns;

namespace MagicCore.Actions;

/// <summary>The ability's source in a choice's context — what a counted filter bound reads (Sisay's power); 0 if none.</summary>
public static class SourceOf
{
	public static int Context(ImmutableDictionary<string, object> context) =>
		context.TryGetValue(Effects.ContextKeys.Source, out var source) ? (int)source : 0;
}

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

	/// <summary>Sphinx's Approach: "a Sphinx creature card".</summary>
	public string? Subtype { get; init; }

	/// <summary>Fblthp: "with different names".</summary>
	public bool DifferentNames { get; init; }

	/// <summary>Eye of Ugin: "a colorless creature card". None means any.</summary>
	public Filters.Filter? Filter { get; init; }

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
	)
	{
		var cards = s.CardsIn(PlayerId, ZoneType.Library)
			.Where(c =>
				(Type == CardType.None || c.Is(Type)) // None: "a card"
				&& (Supertype == Supertype.None || c.Is(Supertype))
				&& (Subtype is null || c.Subtypes.Contains(Subtype))
				&& (Filter?.Matches(s, c, new(PlayerId, SourceOf.Context(context))) ?? true)
			);
		// Taking one, two Forests are one choice: the library is shuffled after. Taking up to
		// several, Hexhaven Invigorator may take both — unless the names must differ (Fblthp).
		return (DifferentNames || MaxChoices <= 1 ? cards.DistinctBy(c => c.Name) : cards)
			.OrderBy(c => c.Name)
			.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name })
			.ToImmutableList();
	}

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

	/// <summary>Captain Jean-Luc Picard: "onto the battlefield with seven charge counters on it" — entering with them (CR 122.6: Geordi doubles them).</summary>
	public int ChargeCounters { get; init; }

	public override ActionResult Execute(GameState s)
	{
		foreach (var id in ChooseCardsFromHandChoice.Read(InputContext))
			s =
				To == ZoneType.Battlefield
					? s.EnterBattlefield(id, PlayerId, tapped: Tapped) // the one way in: its entry, its events
						.PutCounters(
							id,
							Effects.CounterKinds.Charge,
							ChargeCounters,
							entering: true
						)
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

	/// <summary>Where the cards are: a graveyard (mill), or exile (Uldaros' copies to cast).</summary>
	public ZoneType Zone { get; init; } = ZoneType.Graveyard;

	/// <summary>Extrapolate the Impossible: "An opponent chooses one of them." Who chooses; 0 is the cards' owner.</summary>
	public int DeciderId { get; init; }

	/// <summary>Extrapolate the Impossible: "two cards … with different names" — one option per name.</summary>
	public bool DifferentNames { get; init; }

	/// <summary>There's Coffee in that Nebula: "a Plains card or nonland permanent card with mana value 3 or less" — what Types can't say.</summary>
	public Filters.Filter? Filter { get; init; }

	public ChooseAmongChoice()
	{
		OutputKey = ChooseCardsFromHandChoice.Chosen;
		MinChoices = 0;
		MaxChoices = 1;
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	)
	{
		// Off the battlefield a card is in its owner's zone — Borg Cube's exiled cards are anyone's.
		var cards = Ids.Where(id =>
				s.HasObject(id)
				&& s.IsIn(id, Zone == ZoneType.Battlefield ? PlayerId : s.GetCard(id).OwnerId, Zone)
			)
			.Select(s.GetCard)
			.Where(c => Types == CardType.None || c.Is(Types)) // None: any card
			.Where(c => Filter?.Matches(s, c, new(PlayerId, SourceOf.Context(context))) ?? true);
		return (DifferentNames ? cards.DistinctBy(c => c.Name) : cards)
			.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name })
			.ToImmutableList();
	}

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => DeciderId == 0 ? PlayerId : DeciderId;
}

/// <summary>"Choose a color." Option id = ManaColor + 1: 0 means "none" across this engine, so no answer may be 0.</summary>
public record ChooseColorChoice : ChoiceAction
{
	public const string Chosen = "color";
	public int PlayerId { get; init; }

	/// <summary>Dal R'El: "{R} or {G}" — the colours offered; empty is all five.</summary>
	public ImmutableArray<ManaColor> Colors { get; init; } = [];

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
			.. (Colors.IsEmpty ? ManaAbilities.AnyColor : Colors).Select(c => new ChoiceOption
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

/// <summary>Kai Winn, after the choice: the name, kept on her (ChosenName).</summary>
public record SetChosenNameAction(int CardId, ImmutableArray<string> Names) : GameAction
{
	public override ActionResult Execute(GameState s) =>
		!s.IsOnBattlefield(CardId)
		|| InputContext.GetValueOrDefault(ChooseKeywordChoice.Chosen) is not int id
			? new(s)
			: new(
				s.UpdateObject(
					CardId,
					s.GetCard(CardId).WithComponent(new Effects.ChosenName(Names[id - 1]))
				)
			);
}

/// <summary>Nyota Uhura, after the choice: the number, kept on her (ChosenNumber).</summary>
public record SetChosenNumberAction(int CardId) : GameAction
{
	public override ActionResult Execute(GameState s) =>
		!s.IsOnBattlefield(CardId)
		|| InputContext.GetValueOrDefault(ChooseNumberChoice.Chosen) is not int id
			? new(s)
			: new(
				s.UpdateObject(
					CardId,
					s.GetCard(CardId).WithComponent(new Effects.ChosenNumber(id - 1))
				)
			);
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

/// <summary>Loyal Tutor: "search your library for a planeswalker card, reveal it" — the chosen cards are known to everyone where they now are (story AI-3).</summary>
public record RevealChosenAction : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var id in ChooseCardsFromHandChoice.Read(InputContext))
			s = s.MakeKnown(id, Knowers.Everyone);
		return new ActionResult(s);
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

/// <summary>"Your choice of [keywords]" — option ids 1..n, one per keyword.</summary>
public record ChooseKeywordChoice : ChoiceAction
{
	public const string Chosen = "keyword";

	public int PlayerId { get; init; }
	public ImmutableArray<string> Keywords { get; init; }

	/// <summary>Kai Winn: "choose a nonland card name" — answered by the AI's rule, never searched (GreedyAi).</summary>
	public bool NamesACard { get; init; }

	public ChooseKeywordChoice(int playerId, ImmutableArray<string> keywords)
	{
		PlayerId = playerId;
		Keywords = keywords;
		MinChoices = 1;
		MaxChoices = 1;
		OutputKey = Chosen;
		Prompt = "Choose one";
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) => [.. Keywords.Select((k, i) => new ChoiceOption { Id = i + 1, DisplayText = k })];

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>
/// "Pay any amount of life" — a number from 0 to Max. Option id = the number + 1: 0 means "none"
/// across this engine, so no answer may be 0.
/// </summary>
public record ChooseNumberChoice : ChoiceAction
{
	public const string Chosen = "number";

	public int PlayerId { get; init; }
	public int Max { get; init; }

	/// <summary>Nyota Uhura's number is a mana value to wait for — the AI answers by its rule, never by search.</summary>
	public bool NamesAManaValue { get; init; }

	public ChooseNumberChoice(int playerId, int max)
	{
		PlayerId = playerId;
		Max = max;
		MinChoices = 1;
		MaxChoices = 1;
		OutputKey = Chosen;
		Prompt = "Choose a number";
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		[
			.. Enumerable
				.Range(0, Max + 1)
				.Select(n => new ChoiceOption { Id = n + 1, DisplayText = $"{n}" }),
		];

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>How to cast a card in the middle of a resolution: its targets, modes, X — one legal cast each.</summary>
public record ChooseCastChoice : ChoiceAction
{
	public const string Chosen = "cast";

	public int PlayerId { get; init; }
	public ImmutableList<CastSpellAction> Casts { get; init; }

	public ChooseCastChoice(int playerId, ImmutableList<CastSpellAction> casts)
	{
		PlayerId = playerId;
		Casts = casts;
		MinChoices = 1;
		MaxChoices = 1;
		OutputKey = Chosen;
		Prompt = "Cast it how?";
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		[
			.. Casts.Select(
				(c, i) => new ChoiceOption { Id = i + 1, DisplayText = Describe.Action(s, c) }
			),
		];

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>Finishes the cleanup after it paused for the discard to hand size.</summary>
public record EndStepAction : GameAction
{
	public override ActionResult Execute(GameState s) => new(TurnStructure.FinishCleanup(s));
}

/// <summary>Finishes the untap step after Lwaxana Troi's "you may choose not to untap" — a yes keeps her tapped.</summary>
public record FinishUntapAction(int AskedId) : GameAction
{
	public override ActionResult Execute(GameState s) =>
		new(
			TurnStructure.FinishUntap(
				s,
				InputContext.GetValueOrDefault(ChoosePermanentChoice.Chosen) switch
				{
					int => AskedId,
					ImmutableList<int> ids when !ids.IsEmpty => AskedId,
					_ => 0,
				}
			)
		);
}
