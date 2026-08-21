using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Writes the greatest effective power among a player's creatures to OutputKey — Overwhelming
/// Stampede's "+X/+X, where X is the greatest power among creatures you control".
///
/// A query, like CountCardsWithSubtypeAction: no state change, one context value out.
///
/// Deliberately NOT a live PowerToughnessModifier. A GreatestPowerComponent would have to call
/// GetEffectivePower from inside GetPowerBonus, and GetEffectivePower reads the same component on
/// every other creature the player controls — that recurses. Computing it once at resolution and
/// stamping a plain modifier is both correct and terminating.
///
/// Effective power, not base: a Hydra's counters and an anthem both count, which is what the card
/// means and what makes it the go-wide payoff.
///
/// It also writes the creature IDs it scanned to CreatureIdsOutputKey. That second output is what
/// makes the card expressible at all: a mass buff needs both a number and a target LIST, and a
/// PipelineAction is not ITargetedAction, so nothing can inject mass targets into a pipeline step.
/// Since this action already walks exactly the right set of creatures, handing the list out costs
/// one line and keeps the whole card inside one pipeline. EffectAction.TargetContextKey already
/// accepts an ImmutableList&lt;int&gt;, so the consuming steps need no changes.
///
/// Player resolution follows the same rule as every other query action — PlayerIdContextKey wins
/// over PlayerId when set.
/// </summary>
public record CountGreatestPowerAction : GameAction
{
	public int PlayerId { get; init; } = 0;
	public string PlayerIdContextKey { get; init; } = "";

	/// Receives the greatest effective power, as an int.
	public string OutputKey { get; init; } = "";

	/// Receives the IDs of the creatures scanned, as an ImmutableList&lt;int&gt;.
	public string CreatureIdsOutputKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, PlayerId);

		if (playerId == 0)
			return new ActionResult(gameState);

		var battlefieldId = gameState.GetPlayerZoneId(playerId, ZoneType.Battlefield);

		var creatureIds = gameState
			.GetCardsInZone(battlefieldId)
			.Where(c => c.HasComponent<CreatureComponent>())
			.Select(c => c.Id)
			.ToImmutableList();

		var greatest = creatureIds.Select(gameState.GetEffectivePower).DefaultIfEmpty(0).Max();

		var result = new ActionResult(gameState);

		if (!string.IsNullOrEmpty(OutputKey))
			result = result.WithOutput(OutputKey, Math.Max(0, greatest));

		if (!string.IsNullOrEmpty(CreatureIdsOutputKey))
			result = result.WithOutput(CreatureIdsOutputKey, creatureIds);

		return result;
	}
}
