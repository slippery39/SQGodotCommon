using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Turns;

namespace MagicCore;

/// <summary>Queries and low-level moves on GameState. Moves here are building blocks for actions.</summary>
public static class MagicState
{
	public static MagicGame GetGame(this GameState s) =>
		(MagicGame)s.GetObject(s.GetWellKnownId(MagicKeys.Game));

	public static GameState WithGame(this GameState s, MagicGame game) =>
		s.UpdateObject(game.Id, game);

	public static MagicPlayer GetPlayer(this GameState s, int playerId) =>
		(MagicPlayer)s.GetObject(playerId);

	public static GameState WithPlayer(this GameState s, MagicPlayer player) =>
		s.UpdateObject(player.Id, player);

	public static int Player1Id(this GameState s) => s.GetWellKnownId(MagicKeys.Player1);

	public static int Player2Id(this GameState s) => s.GetWellKnownId(MagicKeys.Player2);

	public static int OpponentOf(this GameState s, int playerId) =>
		playerId == s.Player1Id() ? s.Player2Id() : s.Player1Id();

	public static Card GetCard(this GameState s, int cardId) => (Card)s.GetObject(cardId);

	public static int ZoneId(this GameState s, int playerId, ZoneType type) =>
		s.GetWellKnownId(MagicKeys.Zone(playerId, type));

	public static IEnumerable<Card> CardsIn(this GameState s, int playerId, ZoneType type) =>
		s.GetChildren(s.ZoneId(playerId, type)).Cast<Card>();

	public static bool IsIn(this GameState s, int cardId, int playerId, ZoneType type) =>
		s.GetParent(cardId) == s.ZoneId(playerId, type);

	/// <summary>False — not a throw — for an id that no longer exists (a dead token, a last-known copy).</summary>
	public static bool IsOnBattlefield(this GameState s, int cardId) =>
		s.HasObject(cardId)
		&& s.GetObject(cardId) is Card card
		&& s.IsIn(cardId, card.ControllerId, ZoneType.Battlefield);

	public static bool StackIsEmpty(this GameState s) =>
		!s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack)).Any();

	public static bool IsOver(this GameState s) =>
		s.GetPlayer(s.Player1Id()).HasLost || s.GetPlayer(s.Player2Id()).HasLost;

	/// <summary>0 while the game runs or when both players lost at once (a draw).</summary>
	public static int WinnerId(this GameState s)
	{
		var p1Lost = s.GetPlayer(s.Player1Id()).HasLost;
		var p2Lost = s.GetPlayer(s.Player2Id()).HasLost;
		return p1Lost == p2Lost ? 0
			: p1Lost ? s.Player2Id()
			: s.Player1Id();
	}

	/// <summary>
	/// A Random seeded from the state, and the state with its seed advanced — so a seeded game
	/// replays exactly. RngSeed 0 means unseeded.
	/// </summary>
	public static (Random Rng, GameState State) TakeRng(this GameState s)
	{
		if (s.RngSeed == 0)
			return (new Random(), s);
		var rng = new Random(s.RngSeed);
		return (rng, s with { RngSeed = rng.Next(1, int.MaxValue) });
	}

	public static GameState Shuffle(this GameState s, int playerId)
	{
		var libraryId = s.ZoneId(playerId, ZoneType.Library);
		var ids = s.GetChildrenIds(libraryId).ToArray();
		(var rng, s) = s.TakeRng();
		rng.Shuffle(ids);
		foreach (var id in ids)
			s = s.MoveObject(id, 0);
		foreach (var id in ids)
			s = s.MoveObject(id, libraryId);
		return s;
	}

	/// <summary>Every zone change of a card on or off the battlefield/stack — it arrives as a new object (CR 400.7).</summary>
	public static GameState MoveCard(this GameState s, int cardId, int zoneId, bool toTop = false)
	{
		var before = s.GetCard(cardId);
		// Flashback (CR 702.34a): leaving the stack — resolved, countered, fizzled, bounced — it is exiled instead.
		if (
			before.GetComponent<Actions.OnStackComponent>() is { CastFrom: ZoneType.Graveyard }
			&& zoneId != s.GetWellKnownId(MagicKeys.Stack)
		)
			zoneId = s.ZoneId(before.OwnerId, ZoneType.Exile);
		// A finality counter: it would die, so it is exiled instead — and never dies (CR 122.1i).
		if (
			before.HasFinalityCounter
			&& before.Is(CardType.Creature)
			&& s.IsOnBattlefield(cardId)
			&& zoneId == s.ZoneId(before.OwnerId, ZoneType.Graveyard)
		)
			zoneId = s.ZoneId(before.OwnerId, ZoneType.Exile);
		var leavesBattlefield = s.IsOnBattlefield(cardId) && s.GetParent(cardId) != zoneId;
		s = (
			toTop ? s.MoveObjectToFront(cardId, zoneId) : s.MoveObject(cardId, zoneId)
		).UpdateObject(cardId, before.Fresh());
		if (!leavesBattlefield)
			return s;

		// "Dies" (CR 700.4): battlefield to graveyard. The event carries the card as it LAST was —
		// its counters and controller — because the card in the graveyard is a new object.
		if (
			zoneId == s.ZoneId(before.OwnerId, ZoneType.Graveyard)
			&& before.Is(CardType.Creature | CardType.Planeswalker)
		)
		{
			s = s.Raise(new DiedEvent(before));
			if (before.Is(CardType.Creature))
				s = s.WithGame(
					s.GetGame() with
					{
						CreaturesDiedThisTurn = s.GetGame().CreaturesDiedThisTurn + 1,
					}
				);
		}

		// CR 610.3: what this permanent exiled "until it leaves the battlefield" returns at once.
		var exiled = new[] { s.Player1Id(), s.Player2Id() }
			.SelectMany(p => s.CardsIn(p, ZoneType.Exile))
			.Where(c =>
				c.GetComponent<ExiledUntilLeaves>() is { } link
				&& link.SourceId == cardId
				&& link.SourceIncarnation == before.Incarnation
			)
			.ToList();
		foreach (var card in exiled)
			s = s.EnterBattlefield(card.Id, card.OwnerId);
		return s;
	}

	/// <summary>
	/// The ONE way onto the battlefield — cast, played, created or returned. Owns the entry
	/// ceremony: controller, summoning-sickness stamp, starting loyalty, entering tapped, the event.
	/// </summary>
	/// <summary>The ONE way to sacrifice: to its owner's graveyard — a creature so moved dies.</summary>
	public static GameState Sacrifice(this GameState s, int cardId) =>
		s.MoveCard(cardId, s.ZoneId(s.GetCard(cardId).OwnerId, ZoneType.Graveyard));

	/// <param name="x">The X paid to cast it — "enters with X counters" and its "when this enters" read it (CR 107.3m).</param>
	public static GameState EnterBattlefield(
		this GameState s,
		int cardId,
		int controllerId,
		int x = 0,
		int extraCounters = 0,
		bool tapped = false,
		bool finality = false
	)
	{
		s = s.MoveCard(cardId, s.ZoneId(controllerId, ZoneType.Battlefield))
			.Raise(new EnteredBattlefieldEvent(cardId));
		var card = s.GetCard(cardId);
		s = s.UpdateObject(
			cardId,
			card with
			{
				ControllerId = controllerId,
				ControlledSinceTurn = s.GetGame().TurnNumber,
				EnteredOnTurn = s.GetGame().TurnNumber,
				Loyalty = card.PrintedLoyalty,
				CastX = x,
				PlusOneCounters =
					extraCounters
					+ (
						card.GetComponent<EntersWithCountersComponent>()
							?.Count.Value(
								s,
								ImmutableDictionary<string, object>
									.Empty.Add(ContextKeys.Controller, controllerId)
									.Add(ContextKeys.X, x)
							) ?? 0
					),
				HasFinalityCounter = finality,
				IsTapped =
					tapped
					|| (
						card.GetComponent<EntersTappedComponent>() is { } entersTapped
						&& !EntersUntapped(s, card.Id, controllerId, entersTapped.Unless)
					),
			}
		);
		if (card.PrintedLoyalty > 0)
			s = s.Raise(new LoyaltyCountersPutEvent(controllerId)); // entering with them is "putting" (CR 122.6)
		if (card.HasComponent<ChoosesColorAsItEnters>())
			s = s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						new Actions.ChooseColorChoice(controllerId),
						new Actions.SetChosenColorAction(cardId),
					],
				}
			);
		return card.HasComponent<EntersPreparedComponent>() ? Rules.Prepare.Become(s, cardId) : s;
	}

	/// <summary>"This land enters tapped unless …", judged as it enters — "other" excludes itself.</summary>
	/// <summary>Judged on the board it is ENTERING — the new controller's, not the card's old one.</summary>
	static bool EntersUntapped(GameState s, int cardId, int controllerId, EntersUntappedIf unless)
	{
		var others = s.CardsIn(controllerId, ZoneType.Battlefield)
			.Where(c => c.Id != cardId)
			.ToList();
		return unless switch
		{
			EntersUntappedIf.YouControlAPlaneswalker => others.Any(c =>
				c.Is(CardType.Planeswalker)
			),
			EntersUntappedIf.YouControlTwoOrMoreOtherLands => others.Count(c => c.Is(CardType.Land))
				>= 2,
			_ => false,
		};
	}

	/// <summary>
	/// The ONE way a permanent untaps — the untap step and "untap" effects alike. A stun counter is
	/// removed instead (CR 122.1d).
	/// </summary>
	public static GameState Untap(this GameState s, int cardId)
	{
		var card = s.GetCard(cardId);
		return !card.IsTapped ? s
			: card.StunCounters > 0
				? s.UpdateObject(cardId, card with { StunCounters = card.StunCounters - 1 })
			: s.UpdateObject(cardId, card with { IsTapped = false });
	}

	/// <summary>Updates one player's "this turn" counts.</summary>
	public static GameState Tally(
		this GameState s,
		int playerId,
		Func<TurnTally, TurnTally> change
	) =>
		s.WithPlayer(
			s.GetPlayer(playerId) with
			{
				ThisTurn = change(s.GetPlayer(playerId).ThisTurn),
			}
		);

	/// <summary>Draws from the top; a draw from an empty library flags the loss for the state-based check.</summary>
	public static GameState Draw(this GameState s, int playerId, int count = 1)
	{
		var libraryId = s.ZoneId(playerId, ZoneType.Library);
		var handId = s.ZoneId(playerId, ZoneType.Hand);
		for (var i = 0; i < count; i++)
		{
			var top = s.GetChildrenIds(libraryId).FirstOrDefault();
			if (top == 0)
				return s.WithPlayer(
					s.GetPlayer(playerId) with
					{
						AttemptedDrawFromEmptyLibrary = true,
					}
				);
			s = s.MoveCard(top, handId)
				.Tally(playerId, t => t with { CardsDrawn = t.CardsDrawn + 1 });
		}
		return s;
	}
}
