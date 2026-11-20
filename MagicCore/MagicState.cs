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

	public static IEnumerable<Card> CardsIn(this GameState s, int playerId, ZoneType type)
	{
		// One iterator over the ids, not GetChildren(…).Cast — it sits under every rule and the search.
		// A sideboard exists only in a game given one (MagicRules.WithSideboard), the phased-out zone once
		// something phased out (Rules.Phasing): none, no cards. Any other zone always exists — a missing one
		// is a bug, and still throws.
		if (
			type is ZoneType.Sideboard or ZoneType.PhasedOut
				&& !s.WellKnownIds.ContainsKey(MagicKeys.Zone(playerId, type))
			|| !s.ParentToChildren.TryGetValue(s.ZoneId(playerId, type), out var ids)
		)
			yield break;
		foreach (var id in ids)
			yield return (Card)s.GetObject(id);
	}

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
		{
			s = s.MoveObject(id, libraryId);
			if (s.GetCard(id) is { KnownTo: not Knowers.None } card) // a shuffle forgets every position anyone knew
				s = s.UpdateObject(id, card with { KnownTo = Knowers.None });
		}
		return s;
	}

	/// <summary>
	/// "… the rest on the bottom of your library in a random order" (Identity Echo, Rutherford, Stock Up): these
	/// cards of that player's library go to its bottom in a random order — which nobody knows, so whatever was
	/// known of them (a surveil's kept card, Ponder's look) is forgotten, as a shuffle forgets (story AI-3).
	/// </summary>
	public static GameState ToBottomAtRandom(this GameState s, int playerId, int[] ids)
	{
		(var rng, s) = s.TakeRng();
		rng.Shuffle(ids);
		return s.ToBottom(playerId, ids, Knowers.None);
	}

	/// <summary>
	/// Library cards to its bottom in this order, known to <paramref name="knownTo"/> there — Rutherford's random
	/// order (nobody), Dig Through Time's "in any order" (their owner).
	/// </summary>
	public static GameState ToBottom(
		this GameState s,
		int playerId,
		IEnumerable<int> ids,
		Knowers knownTo
	)
	{
		var libraryId = s.ZoneId(playerId, ZoneType.Library);
		foreach (var id in ids)
		{
			s = s.MoveObject(id, 0).MoveObject(id, libraryId); // out and back in: MoveObject appends, the bottom
			if (s.GetCard(id) is { } card && card.KnownTo != knownTo)
				s = s.UpdateObject(id, card with { KnownTo = knownTo });
		}
		return s;
	}

	/// <summary>
	/// <paramref name="who"/> now know this card where it is — a surveil's look, a "reveal" (story AI-3).
	/// Meaningful only in a library or a hand; the next zone change sets it afresh.
	/// </summary>
	public static GameState MakeKnown(this GameState s, int cardId, Knowers who)
	{
		var card = s.GetCard(cardId);
		return (card.KnownTo | who) == card.KnownTo
			? s
			: s.UpdateObject(cardId, card with { KnownTo = card.KnownTo | who });
	}

	/// <summary>
	/// Who knows a card once it moves to <paramref name="zoneId"/> (story AI-3): out of a public zone
	/// (battlefield, stack, graveyard, exile) into a hand or library, everyone saw it — a bounce, a
	/// regrowth, Paradox Shaper's bottom. Into a hand, only what the opponent saw counts (the owner
	/// always knows its hand), and only from the TOP of the library — a draw of a card seen there; a
	/// search takes its card face down, and one that reveals marks it after. Into its library, its
	/// owner knows where it went (a mulligan's bottom, Loyal Tutor's top). Anywhere public, nobody needs to.
	/// </summary>
	static Knowers KnownAfterMoving(GameState s, Card card, int zoneId)
	{
		var library = s.ZoneId(card.OwnerId, ZoneType.Library);
		var hand = s.ZoneId(card.OwnerId, ZoneType.Hand);
		var from = s.GetParent(card.Id);
		if (zoneId != library && zoneId != hand)
			return Knowers.None;
		if (from != library && from != hand)
			return Knowers.Everyone;
		if (zoneId == library)
			return card.KnownTo | Knowers.Owner;
		return s.GetChildrenIds(library).FirstOrDefault() == card.Id // drawn, not searched for
			? card.KnownTo & Knowers.Opponent
			: Knowers.None;
	}

	/// <summary>Every zone change of a card on or off the battlefield/stack — it arrives as a new object (CR 400.7).</summary>
	public static GameState MoveCard(this GameState s, int cardId, int zoneId, bool toTop = false)
	{
		var before = s.GetCard(cardId);
		// Flashback (CR 702.34a): leaving the stack — resolved, countered, fizzled, bounced — it is exiled instead.
		if (
			before.GetComponent<Actions.OnStackComponent>()
				is { CastFrom: ZoneType.Graveyard, ByPermission: false }
			&& !MayCastThisFrom.On(before, ZoneType.Graveyard) // Storied Veteran: no flashback
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
		// Necrodominance: its controller's cards go to exile instead of their graveyard, from anywhere —
		// so a creature never dies. Read while it is still on the battlefield, so it exiles itself too.
		// Yawgmoth's Will: the same rule "this turn", in its owner's command zone — of a card only (What).
		bool ExilesIt(Card holder, ExileInsteadOfYourGraveyard rule) =>
			rule.What is not { } what
			|| what.Matches(s, before, new Filters.FilterContext(before.OwnerId, holder.Id));
		if (
			zoneId == s.ZoneId(before.OwnerId, ZoneType.Graveyard)
			&& (
				s.CardsIn(before.OwnerId, ZoneType.Battlefield)
					.Any(p =>
						s.AbilitiesOf<ExileInsteadOfYourGraveyard>(p).Any(r => ExilesIt(p, r))
					)
				|| s.ThisTurnEffects<ExileInsteadOfYourGraveyard>(before.OwnerId)
					.Any(c => ExilesIt(c, c.GetComponent<ExileInsteadOfYourGraveyard>()!))
			)
		)
			zoneId = s.ZoneId(before.OwnerId, ZoneType.Exile);
		var leavesBattlefield = s.IsOnBattlefield(cardId) && s.GetParent(cardId) != zoneId;
		var leavesGraveyard = // Vash: "a card leaves your graveyard" — a token isn't a card
			!before.IsToken
			&& s.IsIn(cardId, before.OwnerId, ZoneType.Graveyard)
			&& s.GetParent(cardId) != zoneId;
		var milled =
			s.IsIn(cardId, before.OwnerId, ZoneType.Library)
			&& zoneId == s.ZoneId(before.OwnerId, ZoneType.Graveyard);
		var exiledFromLibrary = // Laelia: "put into exile from your library"
			!before.IsToken
			&& zoneId == s.ZoneId(before.OwnerId, ZoneType.Exile)
			&& s.IsIn(cardId, before.OwnerId, ZoneType.Library);
		var knownTo = KnownAfterMoving(s, before, zoneId);
		s = (
			toTop ? s.MoveObjectToFront(cardId, zoneId) : s.MoveObject(cardId, zoneId)
		).UpdateObject(cardId, before.Fresh() with { KnownTo = knownTo });
		if (
			(before.IsToken || before.IsCopy)
			&& new[] { s.Player1Id(), s.Player2Id() }.Any(p =>
				zoneId == s.ZoneId(p, ZoneType.Hand) || zoneId == s.ZoneId(p, ZoneType.Library)
			)
		) // AI-22: the state-based check scans hands and libraries for these only when one went there
			s = s.WithGame(s.GetGame() with { TokenOrCopyInHandOrLibrary = true });
		if (leavesBattlefield && zoneId == s.ZoneId(before.OwnerId, ZoneType.Hand)) // Miles O'Brien
			s = s.Tally(
				before.OwnerId,
				t => t with { PermanentsReturnedToHand = t.PermanentsReturnedToHand + 1 }
			);
		if (milled) // mill, surveil, any "from your library into your graveyard" — counted in one place
			s = s.Tally(
				before.OwnerId,
				t => t with { CardsFromLibraryToGraveyard = t.CardsFromLibraryToGraveyard + 1 }
			);
		if (leavesGraveyard || exiledFromLibrary)
			s = s.Raise(
				new ChangedZoneEvent(
					before.OwnerId,
					cardId,
					leavesGraveyard ? ZoneType.Graveyard : ZoneType.Library,
					((Zone)s.GetObject(zoneId)).ZoneType
				)
			);
		if (!leavesBattlefield)
			return s;
		// "Leaves the battlefield" (Skyclave Apparition), "put into a graveyard from the battlefield" (Serra
		// Paragon): a permanent that reads its own leaving, a token too, to the zone it really went to —
		// carried as it LAST was. Only such a one: an event staged for every Treasure sacrificed would cost
		// every search node a Collect and another state-check round.
		if (ReadsItsLeaving(before))
			s = s.Raise(
				new ChangedZoneEvent(
					before.OwnerId,
					cardId,
					ZoneType.Battlefield,
					((Zone)s.GetObject(zoneId)).ZoneType
				)
				{
					LastKnown = before,
				}
			);

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
			s =
				card.GetComponent<ExiledUntilLeaves>()!.From == ZoneType.Battlefield
					? s.EnterBattlefield(card.Id, card.OwnerId)
					: s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Hand)); // Deep-Cavern Bat's
		return s;
	}

	/// <summary>
	/// Has it a "when this leaves the battlefield" of its own (TriggerEvent.SelfLeaves) — printed (Skyclave
	/// Apparition) or gained (Serra Paragon's gift)? A plain loop: MoveCard asks it of every permanent leaving.
	/// </summary>
	static bool ReadsItsLeaving(Card card)
	{
		foreach (var c in card.Components)
			if (
				(c as TriggeredAbilityComponent ?? (c as UntilEndOfTurn)?.Trigger)?.On is
				{ On: EventKind.ChangedZone, ReadFrom: ReadFrom.TheCardThatLeft }
			)
				return true;
		return false;
	}

	/// <summary>
	/// The ONE way onto the battlefield — cast, played, created or returned. Owns the entry
	/// ceremony: controller, summoning-sickness stamp, starting loyalty, entering tapped, the event.
	/// </summary>
	/// <summary>The ONE way to sacrifice: to its owner's graveyard — a creature so moved dies.</summary>
	public static GameState Sacrifice(this GameState s, int cardId) =>
		!s.CanSacrifice(cardId)
			? s
			: s.MoveCard(cardId, s.ZoneId(s.GetCard(cardId).OwnerId, ZoneType.Graveyard));

	/// <summary>Quark, Humble Proprietor: "You can't sacrifice Treasure tokens." — every sacrifice asks: costs, choices, Treasure's own mana.</summary>
	public static bool CanSacrifice(this GameState s, int cardId) =>
		s.GetCard(cardId) is not { IsToken: true } card
		|| !card.Subtypes.Contains("Treasure")
		|| !s.CardsIn(card.ControllerId, ZoneType.Battlefield)
			.Any(s.HasAbility<CantSacrificeTreasures>);

	/// <summary>How many lands this player may play this turn: one, Way of the Paradox's extra ones, and one more per Captain Janeway (AdditionalLandEachTurn).</summary>
	public static int LandDrops(this GameState s, int playerId) =>
		1
		+ s.GetPlayer(playerId).ThisTurn.ExtraLandDrops
		+ s.CardsIn(playerId, ZoneType.Battlefield).Count(s.HasAbility<AdditionalLandEachTurn>);

	/// <summary>
	/// THE way a permanent is destroyed (CR 701.7) — a Destroy effect, lethal damage and deathtouch alike
	/// (0 toughness isn't destruction, CR 704.5f): to its owner's graveyard, unless it is indestructible
	/// or Dr. McCoy replaces it — "instead remove all +1/+1 counters from it and all damage already dealt
	/// to it is healed." Several at once are judged together, before any leaves (CR 701.7): a "destroy
	/// all" doesn't end an indestructible grant halfway, nor kill McCoy before the creature he saves.
	/// </summary>
	/// <param name="notDestroyed">The state-based check's 0-toughness creatures: they die with the rest, in
	/// the same order, but nothing replaces it or stops it.</param>
	public static GameState Destroy(
		this GameState s,
		IReadOnlyList<int> cardIds,
		IReadOnlySet<int>? notDestroyed = null
	)
	{
		var judged = cardIds
			.Select(s.GetCard)
			.Where(c => notDestroyed?.Contains(c.Id) == true || !s.IsIndestructible(c))
			.Select(c =>
				(
					Card: c,
					Saved: notDestroyed?.Contains(c.Id) != true
						&& c.Is(CardType.Creature)
						&& c.PlusOneCounters > 0
						&& s.CardsIn(c.ControllerId, ZoneType.Battlefield)
							.Any(p =>
								p.Id != c.Id && s.HasAbility<CountersRemovedInsteadOfDestruction>(p)
							)
				)
			)
			.ToList();
		foreach (var (card, saved) in judged)
		{
			if (!s.IsOnBattlefield(card.Id))
				continue;
			if (!saved)
			{
				s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Graveyard));
				continue;
			}
			s = s.PutCounters(card.Id, CounterKinds.PlusOne, -card.PlusOneCounters);
			s = s.UpdateObject(
				card.Id,
				s.GetCard(card.Id) with
				{
					Damage = 0,
					DamagedByDeathtouch = false,
				}
			);
		}
		return s;
	}

	/// <summary>
	/// MC-6.10: the other permanents' rules this one enters under (<see cref="PermanentsEnter"/>) — judged under
	/// its new controller, each from its holder's side. Never its own: its rule applies once it is there (CR 614.12).
	/// </summary>
	static List<PermanentsEnter> EntryRules(GameState s, Card entering) =>
		[
			.. new[] { s.Player1Id(), s.Player2Id() }
				.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield))
				.Where(p => p.Id != entering.Id)
				.SelectMany(p =>
					s.AbilitiesOf<PermanentsEnter>(p)
						.Where(rule => rule.Which.Matches(s, entering, new(p.ControllerId, p.Id)))
				),
		];

	/// <summary>Kira Nerys: a rule says it enters attacking, and another of your creatures is attacking.</summary>
	static bool EntersAttacking(
		GameState s,
		int cardId,
		int controllerId,
		List<PermanentsEnter> rules
	) =>
		rules.Any(r => r.Attacking)
		&& s.CardsIn(controllerId, ZoneType.Battlefield)
			.Any(c => c.Id != cardId && c.AttackingId != 0);

	/// <param name="spell">The spell it resolved from (Card.CastAs): its X — "enters with X counters" and its "when this
	/// enters" read it (CR 107.3m) — kicked, alternative cost, squad's payments.</param>
	/// <param name="cast">It is a resolving spell — "if you cast it".</param>
	/// <param name="sunburst">Sunburst: "a charge counter on it for each color of mana spent to cast it".</param>
	/// <param name="loses">MoveTo.Loses — Enduring Innocence returns "an enchantment. (It's not a creature.)".</param>
	/// <param name="attacking">CreateTokens.Attacking — mobilize's tokens are created "attacking" (tapped is its own word).</param>
	/// <param name="faceDown">Manifest dread (LookAndTake.FaceDown): it enters a face-down 2/2 — none of its own
	/// abilities as it enters (CR 708.3), so no "enters" trigger, counter or choice of its.</param>
	public static GameState EnterBattlefield(
		this GameState s,
		int cardId,
		int controllerId,
		Actions.OnStackComponent? spell = null,
		int extraCounters = 0,
		bool tapped = false,
		bool finality = false,
		int attachTo = 0,
		bool cast = false,
		int sunburst = 0,
		CardType loses = CardType.None,
		bool attacking = false,
		TriggeredAbilityComponent? gains = null,
		bool faceDown = false
	)
	{
		// Containment Priest: "If a nontoken creature would enter and it wasn't cast, exile it instead."
		// ponytail: judged by its types before it moves, less what this entry takes (loses) — a permanent's
		// type change stamped as it enters (Mogis, no creature at low devotion) isn't seen yet. Judge it
		// after Retype if a cube ever has both.
		if (
			!cast
			&& s.GetCard(cardId) is var entering
			&& EntryRules(
					s,
					// a face-down card is judged as the 2/2 it enters as
					(faceDown ? entering.TurnedFaceDown() : entering) with
					{
						ControllerId = controllerId,
						Types = faceDown ? CardType.Creature : entering.Types & ~loses,
					}
				)
				.Any(r => r.ExiledUnlessCast)
		)
			return s.MoveCard(cardId, s.ZoneId(entering.OwnerId, ZoneType.Exile));
		// "Name Sticker" Goblin: where it enters from — a token just made is in no zone
		var from = s.GetParent(cardId) is { } parent
			? (s.GetObject(parent) as Zone)?.ZoneType
			: null;
		s = s.MoveCard(cardId, s.ZoneId(controllerId, ZoneType.Battlefield));
		if (faceDown)
			s = s.UpdateObject(cardId, s.GetCard(cardId).TurnedFaceDown());
		// Enduring Innocence: "It's an enchantment. (It's not a creature.)" — the types this entry takes, there
		// as it enters and until it leaves.
		if (loses != CardType.None && s.GetCard(cardId) is var printed)
			s = s.UpdateObject(cardId, printed.WithTypesUntilItLeaves(printed.Types & ~loses));
		// Mogis: its type change is there as it enters — no "whenever a creature enters" sees a creature.
		s = Rules.Attachments.Retype(
			s.UpdateObject(cardId, s.GetCard(cardId) with { ControllerId = controllerId }),
			cardId
		);
		s = s.Raise(new EnteredBattlefieldEvent(cardId, s.GetCard(cardId).Types, from));
		var card = s.GetCard(cardId);
		var entersTapped = card.GetComponent<EntersTappedComponent>();
		var rules = EntryRules(s, card with { ControllerId = controllerId });
		var landsUntapped = rules.Any(r => r.Untapped);
		var tappedByOthers = tapped || rules.Any(r => r.Tapped);
		var entryContext = ImmutableDictionary<string, object>
			.Empty.Add(ContextKeys.Controller, controllerId)
			.Add(ContextKeys.Source, card.Id);
		var tappedByItself =
			entersTapped is not null && entersTapped.Unless?.Holds(s, entryContext) != true;
		if (card.Subtypes.Except(s.GetPlayer(controllerId).ThisTurn.SubtypesEntered).Any())
			s = s.Tally(
				controllerId,
				t => t with { SubtypesEntered = [.. t.SubtypesEntered.Union(card.Subtypes)] }
			);
		s = s.UpdateObject(
			cardId,
			card with
			{
				ControllerId = controllerId,
				ControlledSinceTurn = s.GetGame().TurnNumber,
				EnteredOnTurn = s.GetGame().TurnNumber,
				CastAs = spell,
				WasCast = cast,
				HasFinalityCounter = finality,
				IsTapped = !landsUntapped && (tappedByOthers || tappedByItself),
			}
		);
		// Serra Paragon: "If you do, it gains '[trigger]'" — for as long as it stays; a zone change ends it.
		if (gains is not null)
			s = s.UpdateObject(
				cardId,
				s.GetCard(cardId)
					.WithComponent(
						new UntilEndOfTurn { Trigger = gains, UntilTurnOf = UntilEndOfTurn.Never }
					)
			);
		// Kira Nerys: "If a creature you control would enter while you control an attacking creature, instead
		// it enters tapped and attacking."
		// ponytail: "attacking" (a token made so) isn't asked whether there is a combat — mobilize only
		// resolves in one; check the step if a card makes an attacking token outside combat (DesignNotes).
		var byRule = EntersAttacking(s, cardId, controllerId, rules);
		if (byRule || attacking)
			s = s.UpdateObject(
				cardId,
				s.GetCard(cardId) with
				{
					IsTapped = byRule || s.GetCard(cardId).IsTapped,
					AttackingId = s.OpponentOf(controllerId),
				}
			);
		// The shocklands: "you may pay 2 life. If you don't, it enters tapped." Asked only when it decides.
		if (
			entersTapped is { UnlessYouPayLife: > 0 and var life }
			&& tappedByItself
			&& !landsUntapped
			&& !tappedByOthers
		)
			s = s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = entryContext,
					Steps =
					[
						new May(
							$"Pay {life} life? If you don't, {card.Name} enters tapped.",
							new CanPayLife(life),
							[new LoseLife(Ref.You, life), new Untap(Ref.Source)]
						),
					],
				}
			);
		// Zefram Cochrane: "Spacecraft you control enter with two additional charge counters on them."
		foreach (var rule in rules.Where(r => r is { CounterKind: not null, Counters: > 0 }))
			s = s.PutCounters(cardId, rule.CounterKind!, rule.Counters, entering: true);
		s = s.PutCounters(cardId, CounterKinds.Charge, sunburst, entering: true); // Geordi doubles these
		// Entering with counters is putting them on (Yoshimaru, Brad Boimler: CR 122.6) — after control is set.
		s = s.PutCounters(cardId, CounterKinds.Loyalty, card.PrintedLoyalty)
			.PutCounters(cardId, CounterKinds.Time, card.GetComponent<Vanishing>()?.N ?? 0); // vanishing: "enters with N time counters"
		var entersWith = card.GetComponent<EntersWithCountersComponent>();
		var entersWithN =
			entersWith?.Count.Value(
				s,
				ImmutableDictionary<string, object>
					.Empty.Add(ContextKeys.Controller, controllerId)
					.Add(ContextKeys.X, spell?.X ?? 0)
			) ?? 0;
		var plusOne = entersWith is null or { Kind: CounterKinds.PlusOne };
		s = s.AddPlusOneCounters(
			cardId,
			extraCounters + (plusOne ? entersWithN : 0),
			entering: true
		);
		if (!plusOne && entersWithN > 0) // Wishclaw Talisman's three wish counters
			s = s.PutCounters(cardId, entersWith!.Kind, entersWithN, entering: true);
		// CR 714.3a: "As a Saga enters the battlefield, its controller puts a lore counter on it" — chapter I.
		if (card.HasComponent<SagaComponent>())
			s = s.PutCounters(cardId, CounterKinds.Lore, 1);
		// An Aura enters attached: to the target it was cast at, or to what its controller chooses (CR 303.4f).
		if (card.HasComponent<AuraComponent>())
			s =
				attachTo != 0
					? Rules.Attachments.Attach(s, cardId, attachTo)
					: Rules.Attachments.ChooseHostAsItEnters(s, cardId);
		if (card.PrintedLoyalty > 0)
			s = s.Raise(new LoyaltyCountersPutEvent(controllerId)); // entering with them is "putting" (CR 122.6)
		var asCopy =
			card.GetComponent<MayEnterAsCopy>() is { } copying
			// nothing to copy: no prompt with no options (an empty forced pick can't be answered)
			&& new Actions.ChooseCreatureToCopyChoice(controllerId, cardId) { Of = copying.Of }
				.GetOptions(s, ImmutableDictionary<string, object>.Empty)
				.Count > 0
				? copying
				: null;
		if (asCopy is null)
			s = s.ChooseAsItEnters(cardId);
		else
			s = s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						new Actions.ChooseCreatureToCopyChoice(controllerId, cardId)
						{
							Of = asCopy.Of,
							MinChoices = asCopy.Optional ? 0 : 1,
						},
						new Actions.BecomeCopyOfChosenAction(cardId, asCopy.VanishingIfNone)
						{
							NotLegendary = asCopy.NotLegendary,
							WithCounters = asCopy.WithCounters,
							AlsoType = asCopy.AlsoType,
						},
					],
				}
			);
		return card.HasComponent<EntersPreparedComponent>() ? Rules.Prepare.Become(s, cardId) : s;
	}

	/// <summary>
	/// The "as this enters, choose …" choices (Room of Refuge's colour, Nyota Uhura's number, True-Name Nemesis'
	/// player, Kai Winn's name), read from the permanent AS IT ENTERED — so a clone entering as a copy of one
	/// (Phyrexian Metamorph: "enter as a copy of any artifact or creature") makes the choice too, after its copy
	/// choice (CR 707.5; BecomeCopyOfChosenAction asks here).
	/// </summary>
	public static GameState ChooseAsItEnters(this GameState s, int cardId)
	{
		var card = s.GetCard(cardId);
		var controllerId = card.ControllerId;
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
		if (card.GetComponent<ChoosesNumberAsItEnters>() is { } number)
			s = s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						new Actions.ChooseNumberChoice(controllerId, number.Max)
						{
							NamesAManaValue = true,
							OddOrEven = number.OddOrEven,
							Prompt = number.OddOrEven
								? $"{card.Name}: choose odd or even"
								: $"{card.Name}: choose a number",
						},
						new Actions.SetChosenNumberAction(cardId),
					],
				}
			);
		if (card.HasComponent<ChoosesPlayerAsItEnters>())
			s = s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						new Actions.ChooseKeywordChoice(controllerId, ["An opponent", "You"])
						{
							Prompt = $"{card.Name}: choose a player",
						},
						new Actions.SetChosenPlayerAction(
							cardId,
							[s.OpponentOf(controllerId), controllerId]
						),
					],
				}
			);
		if (card.GetComponent<ChoosesNameAsItEnters>() is { } naming)
		{
			// Anointed Peacekeeper: "look at an opponent's hand, then choose any card name"
			if (naming.LooksAtHand)
				foreach (var seen in s.CardsIn(s.OpponentOf(controllerId), ZoneType.Hand).ToList())
					s = s.MakeKnown(seen.Id, Knowers.Opponent);
			var names = Sets.CardRegistry.CardNames(s, naming.AnyName);
			s = s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						new Actions.ChooseKeywordChoice(controllerId, names)
						{
							NamesACard = true,
							Prompt =
								$"{card.Name}: choose a {(naming.AnyName ? "" : "nonland ")}card name",
						},
						new Actions.SetChosenNameAction(cardId, names),
					],
				}
			);
		}
		return s;
	}

	/// <summary>
	/// The ONE way a permanent becomes tapped — attacking, a {T} or "tap N untapped" cost (Station's
	/// too), a Tap effect, tapping for mana — raising Hemmer's "whenever Hemmer becomes tapped" only
	/// when it was untapped. Entering tapped is not becoming tapped (EnterBattlefield sets it).
	/// </summary>
	public static GameState Tap(this GameState s, int cardId)
	{
		var card = s.GetCard(cardId);
		return card.IsTapped
			? s
			: s.UpdateObject(cardId, card with { IsTapped = true })
				.Raise(new BecameTappedEvent(cardId));
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
			: s.UpdateObject(
				cardId,
				// Lwaxana Troi: "for as long as [she] remains tapped" — over as she untaps.
				card with
				{
					IsTapped = false,
					Components = card.Components.RemoveAll(c =>
						c is TriggersAdditionalTime { UntilUntapped: true }
					),
				}
			);
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

	/// <summary>"Put N +1/+1 counters on it" — PutCounters with that kind.</summary>
	/// <param name="entering">It is entering with them: its own replacement doesn't apply (CR 614.12).</param>
	public static GameState AddPlusOneCounters(
		this GameState s,
		int cardId,
		int n,
		bool entering = false
	) => s.PutCounters(cardId, CounterKinds.PlusOne, n, entering);

	/// <summary>
	/// THE way counters of ANY kind are put on a permanent or removed — +1/+1, -1/-1, charge, stun, time,
	/// loyalty (their own fields on Card) and the named ones (Card.Counters) — so every replacement on
	/// putting them applies in this one place: Yoshimaru's "that many plus one" (+1/+1 on a creature you
	/// control), Brad Boimler's (every kind, Rules.Replacements.Counters), Geordi's and M'Benga's
	/// doubling. Never write a counter field elsewhere; CounterWritesTests fails on one that does.
	/// Negative removes, never below 0. Every kind put but loyalty raises CountersPutEvent: what a Saga's chapter reads
	/// (CR 714.2b: "as the number of lore counters becomes at least the chapter number"), Enterprise-D's
	/// "charge counters are put on" and Emperor of Bones' "+1/+1 counters are put on" (Trigger.Counters).
	/// </summary>
	/// <param name="entering">It is entering with them: its own replacement doesn't apply (CR 614.12).</param>
	public static GameState PutCounters(
		this GameState s,
		int cardId,
		string kind,
		int n,
		bool entering = false
	)
	{
		var card = s.GetCard(cardId);
		// ponytail: M'Benga's (and Geordi's) "if YOU would put" doubles whoever puts them — PutCounters
		// doesn't know the putter. Pass it through every caller if an opponent's counters ever matter.
		n = Rules.Replacements.Apply(
			s,
			ReplacedEvent.Counters,
			card,
			n,
			kind: kind,
			exceptId: entering ? cardId : 0
		);
		var had = card.CountersOf(kind);
		var now = Math.Max(0, had + n);
		if (now == had)
			return s;
		s = s.UpdateObject(cardId, card.WithCounters(kind, now));
		return now > had && kind != CounterKinds.Loyalty // loyalty raises its own (LoyaltyCountersPutEvent)
			? s.Raise(new CountersPutEvent(cardId, kind, had, now))
			: s;
	}

	/// <summary>
	/// THE control change (CR 108.4, Rules of Acquisition) — the same object, so its counters, damage,
	/// attachments and "until end of turn" effects stay; it isn't a zone change (no event, no Fresh). The
	/// battlefield is per controller, so it moves to its new controller's (MoveObject, as MtgCore's
	/// GainControlAction does), summoning sick for them (CR 302.6) and out of combat (CR 506.4).
	/// </summary>
	public static GameState GainControl(this GameState s, int cardId, int playerId)
	{
		if (
			!s.IsOnBattlefield(cardId)
			|| s.GetCard(cardId) is not { } card
			|| card.ControllerId == playerId
		)
			return s;
		return s.MoveObject(cardId, s.ZoneId(playerId, ZoneType.Battlefield))
			.UpdateObject(
				cardId,
				card with
				{
					ControllerId = playerId,
					ControlledSinceTurn = s.GetGame().TurnNumber,
					AttackingId = 0,
					BlockingId = 0,
				}
			);
	}

	/// <summary>A player's "this turn" effects of one kind (<see cref="Effects.ThisTurn"/>), as the objects that hold them.</summary>
	public static IEnumerable<Card> ThisTurnEffects<T>(this GameState s, int playerId)
		where T : ThisTurnEffect =>
		s.CardsIn(playerId, ZoneType.Command).Where(c => c.HasComponent<T>());

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

	/// <summary>
	/// Zefram Cochrane: "You may look at the top card of your library any time." — its owner knows it
	/// while they control one (MayLookAtTopOfLibrary). Read where the knowledge matters (the search's
	/// Hidden, the table's log), never stamped: the top changes on every path that moves a card.
	/// </summary>
	public static bool TopIsKnownToOwner(this GameState s, int playerId) =>
		s.CardsIn(playerId, ZoneType.Battlefield).Any(s.HasAbility<MayLookAtTopOfLibrary>);

	/// <summary>
	/// Draws from the top; a draw from an empty library flags the loss for the state-based check. Every
	/// draw comes here, so Mr. Spock's "If you would draw a card, draw two cards instead" applies in this
	/// one place (a NumericReplacement) — twice per copy: two draw four (each new draw is replaced again).
	/// Then each card on its own: what happens INSTEAD of drawing it (NumericReplacement.Instead — Hullbreacher's
	/// Treasure, Jace, Wielder of Mysteries' win, Narset's "can't") is asked before the library is looked at, so
	/// a draw that doesn't happen never loses the game. Its effects are spawned as the rule's controller: they
	/// run before the resolving ability's next step.
	/// </summary>
	/// <param name="turnDraw">The draw step's own draw.</param>
	public static GameState Draw(
		this GameState s,
		int playerId,
		int count = 1,
		bool turnDraw = false
	)
	{
		count = Rules.Replacements.Apply(s, ReplacedEvent.Draw, s.GetPlayer(playerId), count);
		var libraryId = s.ZoneId(playerId, ZoneType.Library);
		var handId = s.ZoneId(playerId, ZoneType.Hand);
		for (var i = 0; i < count; i++)
		{
			if (
				// The turn's draw is ONE card: a second one Spock makes of it is any other draw.
				Rules.Replacements.InsteadOfDraw(s, s.GetPlayer(playerId), turnDraw && i == 0) is
				{ } instead
			)
			{
				if (!instead.Rule.Instead!.IsEmpty)
					s = s.SpawnAction(
						new PipelineAction
						{
							Steps = instead.Rule.Instead,
							PipelineContext = Conditions.Of(instead.Holder),
						}
					);
				continue;
			}
			var top = s.GetChildrenIds(libraryId).FirstOrDefault();
			if (top == 0)
				return s.WithPlayer(
					s.GetPlayer(playerId) with
					{
						AttemptedDrawFromEmptyLibrary = true,
					}
				);
			// Numbered as it is drawn: two cards drawn at once are the first and the second (Faerie Mastermind).
			var number = s.GetPlayer(playerId).ThisTurn.CardsDrawn + 1;
			s = s.MoveCard(top, handId)
				.Tally(playerId, t => t with { CardsDrawn = number })
				.Raise(new DrewCardEvent(playerId, number, turnDraw && i == 0));
		}
		return s;
	}
}
