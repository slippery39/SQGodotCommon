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
public record AddModifierAction : GameAction, ITargetedAction
{
	public int PowerBonus { get; init; } = 0;
	public int ToughnessBonus { get; init; } = 0;
	public ModifierDuration Duration { get; init; } = ModifierDuration.UntilEndOfTurn;
	public int SourceCardId { get; init; } = 0;

	public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;

	public GameAction WithTargets(ImmutableList<int> targetIds) =>
		this with
		{
			TargetIds = targetIds,
		};

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		foreach (var targetId in TargetIds)
		{
			if (!state.HasObject(targetId))
				continue;

			var card = state.GetObject(targetId) as Card;
			if (card == null || !card.HasComponent<CreatureComponent>())
				continue;

			var modifier = new StaticPowerToughnessModifier
			{
				PowerBonus = PowerBonus,
				ToughnessBonus = ToughnessBonus,
				Duration = Duration,
				SourceCardId = SourceCardId,
			};

			var updatedCard = card with { Components = card.Components.Add(modifier) };

			state = state.UpdateObject(targetId, updatedCard);

			events = events.Add(
				new CreatureModifiedEvent
				{
					CreatureId = targetId,
					PowerBonus = PowerBonus,
					ToughnessBonus = ToughnessBonus,
				}
			);
		}

		return new ActionResult(state) { Events = events };
	}

	public override ValidationResult ValidateResolve(GameState gameState)
	{
		foreach (var targetId in TargetIds)
		{
			if (!gameState.HasObject(targetId))
				return ValidationResult.Invalid($"Target {targetId} no longer exists");
		}
		return ValidationResult.Valid;
	}
}
