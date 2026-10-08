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
	/// <summary>Impulse draw's "until the end of your next turn" — its last turn, counted from whose turn it is now.</summary>
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
		s.IsIn(card.Id, playerId, ZoneType.Hand)
		|| (s.IsIn(card.Id, playerId, ZoneType.Graveyard) && Costs.Flashback(s, card) is not null)
		|| Prepare.IsCastableBy(s, card, playerId)
		|| IsImpulsePlayableBy(s, card, playerId);

	public static bool CanPlayLandFromHere(GameState s, Card card, int playerId) =>
		card.Is(CardType.Land)
		&& (s.IsIn(card.Id, playerId, ZoneType.Hand) || IsImpulsePlayableBy(s, card, playerId));

	/// <summary>Every card this player may cast or play from where it is — hand first.</summary>
	public static IEnumerable<Card> Candidates(GameState s, int playerId) =>
		s.CardsIn(playerId, ZoneType.Hand)
			.Concat(s.CardsIn(playerId, ZoneType.Graveyard))
			.Concat(s.CardsIn(playerId, ZoneType.Exile))
			.Concat(s.CardsIn(s.OpponentOf(playerId), ZoneType.Exile)) // a prepared copy follows its creature's controller
			.Where(c =>
				c.Is(CardType.Land)
					? CanPlayLandFromHere(s, c, playerId)
					: CanCastFromHere(s, c, playerId)
			);
}
