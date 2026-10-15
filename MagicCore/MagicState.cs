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

	/// <summary>Who controls a spell or ability on the stack; 0 for anything else.</summary>
	public static int StackObjectControllerId(this GameState s, int id) =>
		s.GetObject(id) switch
		{
			Card spell => spell.ControllerId,
			Actions.StackAbility ability => ability.ControllerId,
			_ => 0,
		};

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
		// It would die, so it is exiled instead — and never dies: a finality counter (CR 122.1i),
		// Essence Burn's "if that permanent would die this turn", or Garruk, Veiled Butcher's
		// "a creature an opponent controls".
		if (
			zoneId == s.ZoneId(before.OwnerId, ZoneType.Graveyard)
			&& s.IsOnBattlefield(cardId)
			&& (
				before.HasFinalityCounter
				|| before.Components.OfType<UntilEndOfTurn>().Any(u => u.ExileIfItWouldDie)
				|| (
					before.Is(CardType.Creature)
					&& s.CardsIn(s.OpponentOf(before.ControllerId), ZoneType.Battlefield)
						.Any(s.HasAbility<OpponentsCreaturesExiledInsteadOfDying>)
				)
			)
		)
			zoneId = s.ZoneId(before.OwnerId, ZoneType.Exile);
		var leavesBattlefield = s.IsOnBattlefield(cardId) && s.GetParent(cardId) != zoneId;
		var milled =
			s.IsIn(cardId, before.OwnerId, ZoneType.Library)
			&& zoneId == s.ZoneId(before.OwnerId, ZoneType.Graveyard);
		s = (
			toTop ? s.MoveObjectToFront(cardId, zoneId) : s.MoveObject(cardId, zoneId)
		).UpdateObject(cardId, before.Fresh());
		if (milled) // mill, surveil, any "from your library into your graveyard" — counted in one place
			s = s.Tally(
				before.OwnerId,
				t => t with { CardsFromLibraryToGraveyard = t.CardsFromLibraryToGraveyard + 1 }
			);
		if (!leavesBattlefield)
			return s;

		// "Dies" (CR 700.4): battlefield to graveyard. The event carries the card as it LAST was —
		// its counters and controller — because the card in the graveyard is a new object.
		if (
			zoneId == s.ZoneId(before.OwnerId, ZoneType.Graveyard)
			&& before.Is(CardType.Creature | CardType.Planeswalker)
		)
		{
			// ponytail: what is attached as THIS creature moves — if one effect moved its Aura first,
			// "when enchanted creature dies" misses it. No FRA card does; record attachments per
			// resolution if one ever does.
			s = s.Raise(new DiedEvent(before) { Attached = [.. Rules.Attachments.On(s, before)] });
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
	/// <param name="cast">It is a resolving spell — "if you cast it".</param>
	public static GameState EnterBattlefield(
		this GameState s,
		int cardId,
		int controllerId,
		int x = 0,
		int extraCounters = 0,
		bool tapped = false,
		bool finality = false,
		int attachTo = 0,
		bool cast = false
	)
	{
		s = s.MoveCard(cardId, s.ZoneId(controllerId, ZoneType.Battlefield));
		s = s.Raise(new EnteredBattlefieldEvent(cardId, s.GetCard(cardId).Types));
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
				WasCast = cast,
				HasFinalityCounter = finality,
				IsTapped =
					tapped
					|| (
						card.GetComponent<EntersTappedComponent>() is { } entersTapped
						&& entersTapped.Unless?.Holds(
							s,
							ImmutableDictionary<string, object>
								.Empty.Add(ContextKeys.Controller, controllerId)
								.Add(ContextKeys.Source, card.Id)
						) != true
					),
			}
		);
		// Entering with counters is putting them on (Yoshimaru applies) — after control is set.
		s = s.AddPlusOneCounters(
			cardId,
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
			entering: true
		);
		// An Aura enters attached: to the target it was cast at, or to what its controller chooses (CR 303.4f).
		if (card.HasComponent<AuraComponent>())
			s =
				attachTo != 0
					? Rules.Attachments.Attach(s, cardId, attachTo)
					: Rules.Attachments.ChooseHostAsItEnters(s, cardId);
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

	/// <summary>
	/// The ONE discard (CR 701.8) — effects, costs and the cleanup alike: each card to its owner's
	/// graveyard, counted for "this turn", and ONE event for the batch ("discards one or more cards").
	/// MtgCore's lesson: a discard paid as a cost once fired no discard trigger.
	/// </summary>
	public static GameState Discard(this GameState s, int playerId, IReadOnlyList<int> cardIds)
	{
		if (cardIds.Count == 0)
			return s;
		foreach (var id in cardIds)
			s = s.MoveCard(id, s.ZoneId(s.GetCard(id).OwnerId, ZoneType.Graveyard));
		return s.Tally(playerId, t => t with { CardsDiscarded = t.CardsDiscarded + cardIds.Count })
			.Raise(new DiscardedEvent(playerId, [.. cardIds]));
	}

	/// <summary>
	/// The ONE way +1/+1 counters are put on a permanent — Yoshimaru's "that many plus one" applies
	/// to a creature you control (MtgCore: only to an increase, never a removal). Negative removes.
	/// </summary>
	/// <param name="entering">It is entering with them: its own replacement doesn't apply (CR 614.12).</param>
	public static GameState AddPlusOneCounters(
		this GameState s,
		int cardId,
		int n,
		bool entering = false
	)
	{
		if (n == 0)
			return s;
		var card = s.GetCard(cardId);
		if (card.Is(CardType.Creature))
			n = Rules.Replacements.Apply(
				s,
				ReplaceableEvent.PlusOneCountersOnYourCreature,
				card.ControllerId,
				n,
				exceptId: entering ? cardId : 0
			);
		return s.UpdateObject(
			cardId,
			card with
			{
				PlusOneCounters = Math.Max(0, card.PlusOneCounters + n),
			}
		);
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
