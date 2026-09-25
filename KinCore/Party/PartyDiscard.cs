using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **Choose cards from your hand** — MtgCore's <c>SelectCardsFromHandAction</c>, without the player
/// lookup: there is one trainer. A <see cref="ChoiceAction"/>, so it lives inside a pipeline and the
/// engine pauses on it until the front end (or the bot) resolves it.
/// </summary>
public record ChooseFromHandAction : ChoiceAction
{
	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> pipelineContext
	) =>
		[
			.. s.CardsIn(ZoneType.Hand)
				.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name }),
		];
}

/// <summary>
/// **Discards the cards a choice picked** (MtgCore's <c>DiscardCardsAction</c>). Only a card or an
/// ability discards — the hand thrown away at the end of a turn is not "discarding" (Shayne,
/// 2026-09-24), which is why this, and only this, stages <see cref="CardDiscardedEvent"/>.
/// </summary>
public record DiscardChosenAction : GameAction
{
	public string InputKey { get; init; } = PartyDiscard.ChosenKey;

	public override ActionResult Execute(GameState s)
	{
		var chosen = InputContext.GetValueOrDefault(InputKey) switch
		{
			int one => [one],
			ImmutableList<int> many => many,
			_ => ImmutableList<int>.Empty,
		};

		var events = ImmutableList<GameEvent>.Empty;
		foreach (var id in chosen.Where(id => s.GetParent(id) == s.ZoneId(ZoneType.Hand)))
		{
			var discarded = new CardDiscardedEvent { CardId = id };
			s = s.MoveObject(id, s.ZoneId(ZoneType.Discard)).StageEvent(discarded);
			events = events.Add(discarded);
		}
		// How many, for a later step — MtgCore's AmountContextKey pattern ("draw that many").
		return new ActionResult(s)
			.WithEvents(events)
			.WithOutput(PartyDiscard.CountKey, events.Count);
	}
}

public record CardDiscardedEvent : GameEvent
{
	public int CardId { get; init; }
}

public record OnCardDiscarded : TriggerRule
{
	public override bool Matches(GameEvent e, GameState s, int sourceId) => e is CardDiscardedEvent;
}

/// <summary>
/// **TOSS: what a card does when a card or ability discards IT.** The ability sits on the card
/// itself; `FirePartyTriggersAction` makes each discarded card a source for its own discard.
/// </summary>
public record OnSelfDiscarded : TriggerRule
{
	public override bool Matches(GameEvent e, GameState s, int sourceId) =>
		e is CardDiscardedEvent d && d.CardId == sourceId;
}

public record OnDrawOrDiscard : TriggerRule
{
	public override bool Matches(GameEvent e, GameState s, int sourceId) =>
		e is CardsDrawnEvent or CardDiscardedEvent;
}

/// <summary>
/// **"Costs 1 less for each card discarded this turn"** — on the card. Read only by
/// `PartyState.CostOf`, the one place a cost is adjusted (MtgCore's CostEngine rule).
/// </summary>
public record CostReduction : GameComponent
{
	public int PerDiscardThisTurn { get; init; }
}

public static class PartyDiscard
{
	public const string ChosenKey = "chosen";
	public const string CountKey = "discarded";

	/// <summary>A hand is never this big; the choice is "any number".</summary>
	private const int AnyNumber = 10;

	/// <summary>**Draw, then discard** — the looting pipeline, as MtgCore's Faithless Looting builds it.</summary>
	public static PipelineAction DrawThenDiscard(int draw, int discard) =>
		new()
		{
			Steps =
			[
				new DrawAction { Count = draw },
				new ChooseFromHandAction
				{
					Prompt =
						discard == 1
							? "Choose a card to discard"
							: $"Choose {discard} cards to discard",
					MinChoices = discard,
					MaxChoices = discard,
					OutputKey = ChosenKey,
				},
				new DiscardChosenAction(),
			],
		};

	/// <summary>**Discard any number, then draw that many** — the draw reads the count the discard wrote.</summary>
	public static PipelineAction DiscardThenDraw() =>
		new()
		{
			Steps =
			[
				new ChooseFromHandAction
				{
					Prompt = "Discard any number of cards — you draw that many",
					MinChoices = 0,
					MaxChoices = AnyNumber,
					OutputKey = ChosenKey,
				},
				new DiscardChosenAction(),
				new DrawAction { CountKey = CountKey },
			],
		};
}
