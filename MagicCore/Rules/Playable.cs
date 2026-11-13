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
	/// that card." Adapted from MtgCore's ExiledPlayableComponent, whose window is "this turn".
	/// </summary>
	public static bool IsImpulsePlayableBy(GameState s, Card card, int playerId) =>
		s.IsIn(card.Id, card.OwnerId, ZoneType.Exile)
		&& card.GetComponent<PlayableFromExile>() is { } playable
		&& playable.PlayerId == playerId
		&& s.GetGame().TurnNumber <= playable.UntilTurn;

	public static bool CanCastFromHere(GameState s, Card card, int playerId) =>
		(
			s.IsIn(card.Id, playerId, ZoneType.Hand)
			|| (
				s.IsIn(card.Id, playerId, ZoneType.Graveyard)
				&& (
					Costs.Flashback(s, card) is not null
					|| s.AbilitiesOf<MayCastThisFrom>(card).Any(m => m.From == ZoneType.Graveyard)
				)
			)
			|| ZonePermission(s, card, playerId) is not null
			|| (
				s.IsIn(card.Id, playerId, ZoneType.Exile)
				&& !card.HasComponent<ExiledFaceDown>()
				&& s.AbilitiesOf<MayCastThisFrom>(card).Any(m => m.From == ZoneType.Exile)
			)
			|| Prepare.IsCastableBy(s, card, playerId)
			|| IsImpulsePlayableBy(s, card, playerId)
			|| IsCastableThroughLink(s, card, playerId)
			|| IsPlayableFromTheirZone(s, card, playerId)
		) && CastRestrictionHolds(s, card, playerId);

	/// <summary>Sen Triplets: a card in a zone one of this player's <see cref="MayPlayFrom"/> names (that player's hand).</summary>
	public static bool IsPlayableFromTheirZone(GameState s, Card card, int playerId) =>
		s.ThisTurnEffects<MayPlayFrom>(playerId)
			.Any(c =>
				c.GetComponent<MayPlayFrom>() is { } may && s.IsIn(card.Id, may.OwnerId, may.Zone)
			);

	/// <summary>
	/// MC-6.9: the permanent and its rule that let this card be played from where it is now — its owner's graveyard,
	/// the top of their library, or outside the game (<see cref="MayPlayFromZone"/>: Overflow Submatrix, The Holodeck,
	/// Nog's gained ability, Zefram Cochrane, Captain Pike). Only during your turn; an unlimited rule first, then a
	/// once-each-turn one not used this turn (<see cref="Card.PermissionUsedOn"/>). A card with its own way out of
	/// the graveyard (flashback, beam me up, Storied Veteran) uses that.
	/// </summary>
	public static (Card Holder, MayPlayFromZone Rule)? ZonePermission(
		GameState s,
		Card card,
		int playerId
	)
	{
		if (s.GetGame().ActivePlayerId != playerId || card.OwnerId != playerId)
			return null;
		ZoneType? from =
			s.IsIn(card.Id, playerId, ZoneType.Graveyard) ? ZoneType.Graveyard
			: s.GetChildrenIds(s.ZoneId(playerId, ZoneType.Library)).FirstOrDefault() == card.Id
				? ZoneType.Library
			: IsOutsideTheGame(s, card, playerId) ? ZoneType.Sideboard
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
		var context = new Filters.FilterContext(playerId, card.Id);
		(Card, MayPlayFromZone)? once = null;
		foreach (var p in s.CardsIn(playerId, ZoneType.Battlefield))
		foreach (var rule in s.AbilitiesOf<MayPlayFromZone>(p))
			if (
				(rule.From == from || from == ZoneType.Sideboard && rule.OutsideTheGame)
				&& (!rule.OncePerTurn || p.PermissionUsedOn != turn)
				&& rule.What.Matches(s, card, context)
			)
			{
				if (!rule.OncePerTurn)
					return (p, rule);
				once ??= (p, rule);
			}
		return once;
	}

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
	/// Null Summoner: the card it exiled, cast by its controller while its condition holds — and only
	/// while it is that same object on the battlefield (a static ability, CR 607.2a).
	/// </summary>
	public static bool IsCastableThroughLink(GameState s, Card card, int playerId) =>
		card.GetComponent<ExiledBy>() is { } link
		&& s.IsIn(card.Id, card.OwnerId, ZoneType.Exile)
		&& s.IsOnBattlefield(link.SourceId)
		&& s.GetCard(link.SourceId) is var source
		&& source.Incarnation == link.SourceIncarnation
		&& source.ControllerId == playerId
		&& s.AbilitiesOf<MayCastWhatItExiled>(source)
			.Any(may => may.While.Holds(s, Conditions.Of(source)));

	/// <summary>
	/// What casting this without paying its mana cost costs instead, or null when nothing allows it:
	/// Omnipresence's (a permanent of yours, a spell in your hand) or the card's own — Flare of Fortitude's
	/// "rather than pay this spell's mana cost" (What = Is.Source: Omnipresence's own static is no alternative
	/// cost for itself), from wherever it is cast but never with flashback — two alternative costs don't
	/// combine (CR 118.9a). The one asking least.
	/// </summary>
	public static System.Collections.Immutable.ImmutableList<AdditionalCost>? FreeCosts(
		GameState s,
		Card card,
		int playerId
	) =>
		(
			s.IsIn(card.Id, playerId, ZoneType.Hand)
				? s.CardsIn(playerId, ZoneType.Battlefield)
					.SelectMany(p =>
						s.AbilitiesOf<MayCastFromHandFree>(p)
							.Where(m =>
								m.What.Matches(s, card, new Filters.FilterContext(playerId, p.Id))
							)
					)
				: []
		)
			.Concat(
				Costs.Flashback(s, card) is null
					? card
						.Components.OfType<MayCastFromHandFree>()
						.Where(m => m.What.Equals(Filters.Is.Source))
					: []
			)
			.OrderBy(m => m.Costs.Count)
			.FirstOrDefault()
			?.Costs;

	public static bool MayCastWithoutPaying(GameState s, Card card, int playerId) =>
		FreeCosts(s, card, playerId) is not null;

	public static bool CanPlayLandFromHere(GameState s, Card card, int playerId) =>
		card.Is(CardType.Land)
		&& (
			s.IsIn(card.Id, playerId, ZoneType.Hand)
			|| IsImpulsePlayableBy(s, card, playerId)
			|| ZonePermission(s, card, playerId) is not null
			|| IsPlayableFromTheirZone(s, card, playerId)
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
				s.ThisTurnEffects<MayPlayFrom>(playerId) // Sen Triplets: that player's hand
					.Select(c => c.GetComponent<MayPlayFrom>()!)
					.Where(may => may.OwnerId != playerId)
					.SelectMany(may => s.CardsIn(may.OwnerId, may.Zone))
			)
			.Where(c =>
				c.Is(CardType.Land)
					? CanPlayLandFromHere(s, c, playerId)
					: CanCastFromHere(s, c, playerId)
			);
}
