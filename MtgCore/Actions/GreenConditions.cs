using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "If you control another Elf" — Dwynen's Elite's intervening-if clause.
///
/// ONE TYPE, NOT A TriggerCondition TWIN. The card's ETB effect is wrapped in a ConditionalAction,
/// which already reuses ActivationCondition for exactly this reason and already forwards TargetIds
/// and InputContext to the inner action. A true intervening-if also re-checks on resolution, but
/// this engine has no priority window between a trigger firing and resolving, so the difference
/// is unobservable.
///
/// "ANOTHER" IS EXPRESSED BY THE MINIMUM, not by excluding the source. ConditionalAction evaluates
/// its condition with cardId = 0 — it has no source to exclude — so a card that is itself an Elf
/// and wants "another Elf" asks for Minimum = 2 and counts itself. Say so on the card; a bare
/// Minimum = 2 reads like a different clause.
/// </summary>
public record ControlsSubtypeCondition : ActivationCondition
{
	public string Subtype { get; init; } = "";
	public int Minimum { get; init; } = 1;

	public override bool IsSatisfied(GameState state, int cardId, int playerId)
	{
		if (playerId == 0 || string.IsNullOrEmpty(Subtype))
			return false;

		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return false;

		return state
				.GetCardsInZone(battlefieldId)
				.Count(c => c.HasComponent<CreatureComponent>() && c.HasSubtype(Subtype))
			>= Minimum;
	}

	public override string Describe() =>
		Minimum <= 1 ? $"you control a {Subtype}" : $"you control another {Subtype}";
}

/// <summary>
/// "If a creature died this turn" — Fungal Rebirth.
///
/// Reads MtgGame.CreaturesDiedThisTurn, which counts EITHER player's creatures and is reset by
/// StartTurnAction. See that field for why it lives on the game rather than on MtgPlayer.
/// </summary>
public record CreatureDiedThisTurnCondition : ActivationCondition
{
	public int Minimum { get; init; } = 1;

	public override bool IsSatisfied(GameState state, int cardId, int playerId) =>
		(state.TryGetGame()?.CreaturesDiedThisTurn ?? 0) >= Minimum;

	public override string Describe() =>
		Minimum <= 1 ? "a creature died this turn" : $"{Minimum} creatures died this turn";
}

/// <summary>
/// "Cast this only if you control a creature" — Rabid Bite and the other one-sided fight spells.
///
/// Without it those spells are castable with an empty board, where FightAction's source fallback
/// finds nobody and the spell is a silent no-op at full price. The AI will happily cast it.
/// </summary>
public record RequiresCreatureRestriction : CastRestrictionComponent
{
	public int Minimum { get; init; } = 1;

	public override bool CanCast(GameState state, int playerId)
	{
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return false;

		return state.GetCardsInZone(battlefieldId).Count(c => c.HasComponent<CreatureComponent>())
			>= Minimum;
	}

	public override string Describe() => "You must control a creature";
}
