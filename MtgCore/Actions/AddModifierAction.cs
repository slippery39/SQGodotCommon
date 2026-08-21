using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Applies a PowerToughnessModifier to a target creature.
///
/// Used by spells like Giant Growth (UntilEndOfTurn) and permanent
/// enchantment-style buffs (Permanent). The modifier is added as a
/// component to the target card and is factored into GetEffectivePower /
/// GetEffectiveToughness at read time.
///
/// UntilEndOfTurn modifiers are cleared by StartTurnAction at the start
/// of the controlling player's next turn.
/// </summary>
public record AddModifierAction : EffectAction
{
	public int PowerBonus { get; init; } = 0;
	public int ToughnessBonus { get; init; } = 0;
	public ModifierDuration Duration { get; init; } = ModifierDuration.UntilEndOfTurn;
	public int SourceCardId { get; init; } = 0;

	/// <summary>
	/// Read the power bonus from pipeline context instead of PowerBonus — "gets +X/+X" where X is
	/// only known at resolution (Primal Might reads ContextKeys.XValue; Overwhelming Stampede
	/// reads the greatest power among your creatures).
	///
	/// Two explicit keys rather than the inherited AmountContextKey, which would have to mean
	/// "both bonuses" and would silently make +X/+0 inexpressible.
	/// </summary>
	public string PowerBonusContextKey { get; init; } = "";

	public string ToughnessBonusContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		var powerBonus = string.IsNullOrEmpty(PowerBonusContextKey)
			? PowerBonus
			: GetInput<int>(PowerBonusContextKey, 0);
		var toughnessBonus = string.IsNullOrEmpty(ToughnessBonusContextKey)
			? ToughnessBonus
			: GetInput<int>(ToughnessBonusContextKey, 0);

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;

			var card = state.GetObject(targetId) as Card;
			if (card == null || !card.HasComponent<CreatureComponent>())
				continue;

			var modifier = new StaticPowerToughnessModifier
			{
				PowerBonus = powerBonus,
				ToughnessBonus = toughnessBonus,
				Duration = Duration,
				SourceCardId = SourceCardId,
			};

			var updatedCard = card with { Components = card.Components.Add(modifier) };

			state = state.UpdateObject(targetId, updatedCard);

			events = events.Add(
				new CreatureModifiedEvent
				{
					CreatureId = targetId,
					PowerBonus = powerBonus,
					ToughnessBonus = toughnessBonus,
				}
			);
		}

		return new ActionResult(state) { Events = events };
	}
}
