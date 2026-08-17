using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Spell mastery — "if there are two or more instant and/or sorcery cards in your graveyard".
///
/// Only expressible since CardType landed: instants and sorceries previously carried no type
/// marker at all, so a graveyard could not be asked what was in it.
///
/// Note that a card built through the older CardFactory.Spell() reports Instant|Sorcery from
/// derivation, so it counts here — which is the desired answer for "instant and/or sorcery".
/// </summary>
public record SpellMasteryCondition : ActivationCondition
{
	public int Minimum { get; init; } = 2;

	public override bool IsSatisfied(GameState state, int cardId, int playerId)
	{
		var graveyardId = state.GetPlayerZoneId(playerId, ZoneType.Graveyard);
		if (graveyardId == 0)
			return false;

		return state.GetCardsInZone(graveyardId).Count(c => c.HasType(CardType.AnySpell))
			>= Minimum;
	}

	public override string Describe() =>
		$"Spell mastery — {Minimum}+ instants or sorceries in your graveyard";
}

/// <summary>
/// "Cast this spell only if you've cast another spell this turn." — Illusory Angel.
///
/// MtgGame.SpellsCastThisTurn counts every spell either player cast, and it is incremented before
/// this card's own cast would be validated, so the threshold is 1 rather than 0.
/// </summary>
public record RequiresSpellsCastThisTurnRestriction : CastRestrictionComponent
{
	public int Minimum { get; init; } = 1;

	public override bool CanCast(GameState state, int playerId) =>
		(state.TryGetGame()?.SpellsCastThisTurn ?? 0) >= Minimum;

	public override string Describe() => $"You must have cast {Minimum} other spell this turn";
}
