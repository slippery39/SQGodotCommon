using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Rules;

/// <summary>
/// THE answer to "may this player cast or play this card from where it is": the hand; a graveyard
/// with flashback (Costs.Flashback); a prepared copy in exile (Prepare); a card exiled by impulse
/// draw. Casting, playing a land and the move list all ask here, so an option can never be offered
/// but refused, or allowed but never offered.
/// </summary>
public static class Playable
{
	/// <summary>
	/// Impulse draw's "until the end of your next turn" — its last turn, counted from whose turn it is now.
	/// ponytail: fixed as it is exiled, so an opponent's extra turn before yours (H.M.S. Bounty) shortens it by
	/// one; count the player's own turns if that matters (DesignNotes).
	/// </summary>
	public static int EndOfNextTurnOf(GameState s, int playerId) =>
		s.GetGame().TurnNumber + (s.GetGame().ActivePlayerId == playerId ? 2 : 1);

	/// <summary>
	/// Impulse draw: "Exile the top card of your library. Until the end of your next turn, you may play
	/// that card." Adapted from MtgCore's ExiledPlayableComponent, whose window is "this turn". The mark in a
	/// graveyard too (Emry, Lurker of the Loch: "You may cast that card this turn" — MakePlayable).
	/// </summary>
	public static bool IsImpulsePlayableBy(GameState s, Card card, int playerId) =>
		(
			s.IsIn(card.Id, card.OwnerId, ZoneType.Exile)
			|| s.IsIn(card.Id, card.OwnerId, ZoneType.Graveyard)
		)
		&& card.GetComponent<PlayableFromExile>() is { } playable
		&& playable.PlayerId == playerId
		&& s.GetGame().TurnNumber <= playable.UntilTurn
		&& s.GetGame().TurnNumber >= playable.FromTurn
		&& !(playable.CastOnly && card.Is(CardType.Land))
		&& (
			playable.If is null
			|| playable.If.Holds(s, Conditions.Of(card with { ControllerId = playerId }))
		);

	/// <summary>
	/// Emry, Lurker of the Loch: a card in its owner's graveyard this player may cast by its mark (MakePlayable) — not
	/// flashback (a card with flashback casts by that), and no permanent's zone permission: the mark asks nothing more,
	/// so Underworld Breach's escape cost isn't added nor a once-each-turn rule used up (CastSpellAction).
	/// </summary>
	public static bool MarkedInGraveyard(GameState s, Card card, int playerId) =>
		s.IsIn(card.Id, card.OwnerId, ZoneType.Graveyard)
		&& Costs.Flashback(s, card) is null
		&& IsImpulsePlayableBy(s, card, playerId);

	/// <param name="x">The X it would be cast with: a permission's mana-value bound counts it (Serra Paragon).</param>
	public static bool CanCastFromHere(GameState s, Card card, int playerId, int x = 0) =>
		(
			s.IsIn(card.Id, playerId, ZoneType.Hand)
			|| (
				s.IsIn(card.Id, playerId, ZoneType.Graveyard)
				&& (
					Costs.Flashback(s, card) is not null
					|| s.AbilitiesOf<MayCastThisFrom>(card).Any(m => m.From == ZoneType.Graveyard)
				)
			)
			|| ZonePermission(s, card, playerId, x) is not null
			|| (
				s.IsIn(card.Id, playerId, ZoneType.Exile)
				&& !card.HasComponent<ExiledFaceDown>()
				&& s.AbilitiesOf<MayCastThisFrom>(card).Any(m => m.From == ZoneType.Exile)
			)
			|| Prepare.IsCastableBy(s, card, playerId)
			|| IsImpulsePlayableBy(s, card, playerId)
		) && CastRestrictionHolds(s, card, playerId);

	/// <summary>
	/// MC-6.9: the holder and its rule that let this card be played from where it is now — its owner's graveyard,
	/// the top of their library, outside the game, exile or hand (<see cref="MayPlayFromZone"/>: Overflow Submatrix,
	/// The Holodeck, Nog's gained ability, Zefram Cochrane, Captain Pike, Null Summoner; Sen Triplets' "this turn" in the
	/// command zone). During your turn unless the rule says AnyTime; an unlimited rule first, then a once-each-turn one
	/// not used this turn (<see cref="Card.PermissionUsedOn"/>). A card with its own way out of the graveyard
	/// (flashback, beam me up, Storied Veteran) uses that. The filter and If are asked from the holder's side.
	/// </summary>
	/// <param name="x">Serra Paragon: "a permanent SPELL with mana value 3 or less" — the X it would be cast with
	/// counts (CR 202.3e), so the rule's filter is asked of the card as that spell.</param>
	public static (Card Holder, MayPlayFromZone Rule)? ZonePermission(
		GameState s,
		Card card,
		int playerId,
		int x = 0
	)
	{
		var spell =
			x == 0 ? card : (Card)card.WithComponent(new Actions.OnStackComponent(x, [], []));
		// Under the AI's every candidate: nothing to scan, or none that applies now, is the common answer.
		var yourTurn = s.GetGame().ActivePlayerId == playerId;
		var rules = ZoneRules(s, playerId).Where(r => yourTurn || r.Rule.AnyTime).ToList();
		if (rules.Count == 0)
			return null;
		var owner = card.OwnerId;
		ZoneType? from =
			s.IsIn(card.Id, owner, ZoneType.Graveyard) ? ZoneType.Graveyard
			: s.GetChildrenIds(s.ZoneId(owner, ZoneType.Library)).FirstOrDefault() == card.Id
				? ZoneType.Library
			: IsOutsideTheGame(s, card, owner) ? ZoneType.Sideboard
			: s.IsIn(card.Id, owner, ZoneType.Exile) && !card.HasComponent<ExiledFaceDown>()
				? ZoneType.Exile
			: s.IsIn(card.Id, owner, ZoneType.Hand) ? ZoneType.Hand
			: null;
		if (
			from is null
			|| from == ZoneType.Graveyard
				&& (
					Costs.Flashback(s, card) is not null
					|| s.AbilitiesOf<MayCastThisFrom>(card).Any(m => m.From == ZoneType.Graveyard)
				)
		)
			return null;
		var turn = s.GetGame().TurnNumber;
		(Card, MayPlayFromZone)? once = null;
		foreach (var (p, rule) in rules)
			if (
				(rule.From == from || from == ZoneType.Sideboard && rule.OutsideTheGame)
				&& (!rule.OncePerTurn || p.PermissionUsedOn != turn)
				&& (
					rule.OwnerId != 0 ? rule.OwnerId == owner
					: rule.Of is null ? owner == playerId
					: rule.Of.Resolve(s, Conditions.Of(p)).Contains(owner)
				)
				&& rule.What.Matches(s, spell, new Filters.FilterContext(playerId, p.Id))
				&& (rule.If is null || rule.If.Holds(s, Conditions.Of(p)))
			)
			{
				if (!rule.OncePerTurn)
					return (p, rule);
				once ??= (p, rule);
			}
		return once;
	}

	/// <summary>This player's play-from-a-zone rules: on their permanents, then "this turn" ones in their command zone.</summary>
	static IEnumerable<(Card Holder, MayPlayFromZone Rule)> ZoneRules(GameState s, int playerId) =>
		s.CardsIn(playerId, ZoneType.Battlefield)
			.SelectMany(p => s.AbilitiesOf<MayPlayFromZone>(p).Select(rule => (p, rule)))
			.Concat(
				s.ThisTurnEffects<MayPlayFromZone>(playerId)
					.Select(c => (c, c.GetComponent<MayPlayFromZone>()!))
			);

	static bool IsOutsideTheGame(GameState s, Card card, int playerId) =>
		s.WellKnownIds.TryGetValue(MagicKeys.Zone(playerId, ZoneType.Sideboard), out var sideboard)
		&& s.GetParent(card.Id) == sideboard;

	/// <summary>Whether any permanent of this player lets cards be played from outside the game (The Holodeck) — Candidates looks there only then.</summary>
	static bool MayPlayFromOutside(GameState s, int playerId) =>
		s.CardsIn(playerId, ZoneType.Battlefield)
			.Any(p => s.AbilitiesOf<MayPlayFromZone>(p).Any(may => may.OutsideTheGame));

	/// <summary>Proft: "You can't cast this spell unless …" — wherever and however it is cast.</summary>
	public static bool CastRestrictionHolds(GameState s, Card card, int playerId) =>
		card.GetComponent<CastOnlyIf>() is not { } only
		|| only.When.Holds(s, Conditions.Of(card with { ControllerId = playerId }));

	/// <summary>
	/// What allows casting this without paying its mana cost, or null when nothing does: Omnipresence's (a
	/// permanent of yours, a spell in your hand) or the card's own alternative cost — Flare of Fortitude's
	/// "rather than pay this spell's mana cost" (What = Is.Source: Omnipresence's own static is no alternative
	/// cost for itself), from wherever it is cast but never with flashback — two alternative costs don't
	/// combine (CR 118.9a). Its If holds (Snuff Out: "if you control a Swamp"; Force of Negation: "if it's not
	/// your turn"), judged as its caster's. The one asking least: no costs and no mana first.
	/// </summary>
	public static MayCastFromHandFree? FreeCast(GameState s, Card card, int playerId) => (
			// Urza, Lord High Artificer: "you may play that card without paying its mana cost" — the mark's
			IsImpulsePlayableBy(s, card, playerId)
			&& card.GetComponent<PlayableFromExile>() is { Free: true }
				? [new MayCastFromHandFree(Filters.Is.Source)]
			// Bolas's Citadel: "pay life equal to its mana value rather than pay its mana cost" — the rule's costs
			: !s.IsIn(card.Id, playerId, ZoneType.Hand)
			&& ZonePermission(s, card, playerId) is { Rule.Free: true } citadel
				? [new MayCastFromHandFree(Filters.Is.Source) { Costs = citadel.Rule.Costs }]
			: Enumerable.Empty<MayCastFromHandFree>()
		).Concat(s.IsIn(card.Id, playerId, ZoneType.Hand) ? s.CardsIn(playerId, ZoneType.Battlefield).SelectMany(p => s.AbilitiesOf<MayCastFromHandFree>(p).Where(m => m.What.Matches(s, card, new Filters.FilterContext(playerId, p.Id)) && (m.If?.Holds(s, Conditions.Of(p)) ?? true))) : []).Concat(Costs.Flashback(s, card) is null ? card.Components.OfType<MayCastFromHandFree>().Where(m => m.What.Equals(Filters.Is.Source) && (m.From.IsEmpty || m.From.Any(zone => s.IsIn(card.Id, playerId, zone))) && (m.If?.Holds(s, Conditions.Of(card with { ControllerId = playerId })) ?? true)) : []).OrderBy(m => m.Costs.Count + (m.Mana is null ? 0 : 1)).FirstOrDefault();

	/// <summary>What the free cast costs beyond mana (<see cref="FreeCast"/>), or null when nothing allows one.</summary>
	public static System.Collections.Immutable.ImmutableList<AdditionalCost>? FreeCosts(
		GameState s,
		Card card,
		int playerId
	) => FreeCast(s, card, playerId)?.Costs;

	public static bool MayCastWithoutPaying(GameState s, Card card, int playerId) =>
		FreeCast(s, card, playerId) is not null;

	public static bool CanPlayLandFromHere(GameState s, Card card, int playerId) =>
		card.Is(CardType.Land)
		&& (
			s.IsIn(card.Id, playerId, ZoneType.Hand)
			|| IsImpulsePlayableBy(s, card, playerId)
			|| ZonePermission(s, card, playerId) is not null
		);

	/// <summary>Every card this player may cast or play from where it is — hand first.</summary>
	public static IEnumerable<Card> Candidates(GameState s, int playerId) =>
		s.CardsIn(playerId, ZoneType.Hand)
			.Concat(s.CardsIn(playerId, ZoneType.Graveyard))
			.Concat(s.CardsIn(playerId, ZoneType.Exile))
			.Concat(s.CardsIn(s.OpponentOf(playerId), ZoneType.Exile)) // a prepared copy follows its creature's controller
			.Concat(s.CardsIn(playerId, ZoneType.Library).Take(1)) // Zefram Cochrane: the top card
			.Concat(
				MayPlayFromOutside(s, playerId) ? s.CardsIn(playerId, ZoneType.Sideboard) : [] // The Holodeck
			)
			.Concat(
				s.ThisTurnEffects<MayPlayFromZone>(playerId) // Sen Triplets: that player's hand
					.Select(c => c.GetComponent<MayPlayFromZone>()!)
					.Where(may => may.OwnerId is not 0 && may.OwnerId != playerId)
					.SelectMany(may => s.CardsIn(may.OwnerId, may.From))
			)
			.Where(c =>
				c.Is(CardType.Land)
					? CanPlayLandFromHere(s, c, playerId)
					: CanCastFromHere(s, c, playerId)
			);
}
