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
					|| s.HasAbility<MayCastFromYourGraveyard>(card)
				)
			)
			|| GraveyardPermission(s, card, playerId) is not null
			|| (
				s.IsIn(card.Id, playerId, ZoneType.Exile)
				&& !card.HasComponent<ExiledFaceDown>()
				&& s.HasAbility<MayCastFromExile>(card)
			)
			|| Prepare.IsCastableBy(s, card, playerId)
			|| IsImpulsePlayableBy(s, card, playerId)
			|| IsCastableThroughLink(s, card, playerId)
			|| LibraryTopPermission(s, card, playerId) is not null
		) && CastRestrictionHolds(s, card, playerId);

	/// <summary>
	/// Zefram Cochrane: "Once during each of your turns, you may cast an artifact spell from the top of
	/// your library." — the permanent that lets this card be cast, if any (one not used this turn).
	/// </summary>
	public static Card? LibraryTopPermission(GameState s, Card card, int playerId) =>
		card.Is(CardType.Artifact)
		&& s.GetGame().ActivePlayerId == playerId
		&& s.GetChildrenIds(s.ZoneId(playerId, ZoneType.Library)).FirstOrDefault() == card.Id
			? s.CardsIn(playerId, ZoneType.Battlefield)
				.FirstOrDefault(p =>
					p.PermissionUsedOn != s.GetGame().TurnNumber
					&& s.HasAbility<MayCastArtifactFromLibraryTop>(p)
				)
			: null;

	/// <summary>
	/// Overflow Submatrix, The Holodeck: "Once during each of your turns, you may cast [play] … from your
	/// graveyard [or from outside the game]" — the permanent that lets this card be played now (one not
	/// used this turn, or Nog's unlimited one first), if any. A card with its own way out of the graveyard (flashback, beam me up,
	/// Storied Veteran) uses that.
	/// </summary>
	public static Card? GraveyardPermission(GameState s, Card card, int playerId)
	{
		if (s.GetGame().ActivePlayerId != playerId || card.OwnerId != playerId)
			return null;
		var inGraveyard = s.IsIn(card.Id, playerId, ZoneType.Graveyard);
		var outside =
			!inGraveyard
			&& s.WellKnownIds.TryGetValue(
				MagicKeys.Zone(playerId, ZoneType.Sideboard),
				out var sideboard
			)
			&& s.GetParent(card.Id) == sideboard;
		if (
			!inGraveyard && !outside
			|| inGraveyard
				&& (
					Costs.Flashback(s, card) is not null
					|| s.HasAbility<MayCastFromYourGraveyard>(card)
				)
		)
			return null;
		var turn = s.GetGame().TurnNumber;
		var context = new Filters.FilterContext(playerId, card.Id);
		bool Lets(Card p, bool unlimited) =>
			s.AbilitiesOf<MayPlayFromYourGraveyard>(p)
				.Any(may =>
					may.Unlimited == unlimited
					&& (inGraveyard || may.OutsideTheGame)
					&& may.What.Matches(s, card, context)
				);
		var permanents = s.CardsIn(playerId, ZoneType.Battlefield);
		return permanents.FirstOrDefault(p => Lets(p, unlimited: true))
			?? permanents.FirstOrDefault(p =>
				p.PermissionUsedOn != turn && Lets(p, unlimited: false)
			);
	}

	/// <summary>Whether any permanent of this player lets cards be played from outside the game (The Holodeck) — Candidates looks there only then.</summary>
	static bool MayPlayFromOutside(GameState s, int playerId) =>
		s.CardsIn(playerId, ZoneType.Battlefield)
			.Any(p => s.AbilitiesOf<MayPlayFromYourGraveyard>(p).Any(may => may.OutsideTheGame));

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

	/// <summary>Omnipresence: from your hand, with mana value at most the number of creatures you control.</summary>
	public static bool MayCastWithoutPaying(GameState s, Card card, int playerId)
	{
		if (!s.IsIn(card.Id, playerId, ZoneType.Hand))
			return false;
		var yours = s.CardsIn(playerId, ZoneType.Battlefield).ToList();
		return yours.Any(s.HasAbility<CastFromHandFreeUpToCreatureCount>)
			&& card.ManaCost.ManaValue <= yours.Count(c => c.Is(CardType.Creature));
	}

	public static bool CanPlayLandFromHere(GameState s, Card card, int playerId) =>
		card.Is(CardType.Land)
		&& (
			s.IsIn(card.Id, playerId, ZoneType.Hand)
			|| IsImpulsePlayableBy(s, card, playerId)
			|| IsLandPlayableFromLibraryTop(s, card, playerId)
			|| GraveyardPermission(s, card, playerId) is not null
		);

	/// <summary>Captain Pike, Fullness of Time: "You may play lands from the top of your library." — the land drop is the only limit.</summary>
	static bool IsLandPlayableFromLibraryTop(GameState s, Card card, int playerId) =>
		s.GetChildrenIds(s.ZoneId(playerId, ZoneType.Library)).FirstOrDefault() == card.Id
		&& s.CardsIn(playerId, ZoneType.Battlefield).Any(s.HasAbility<MayPlayLandsFromLibraryTop>);

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
			.Where(c =>
				c.Is(CardType.Land)
					? CanPlayLandFromHere(s, c, playerId)
					: CanCastFromHere(s, c, playerId)
			);
}
