using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Applies an arbitrary PowerToughnessModifier to each target creature.
///
/// AddModifierAction always constructs a StaticPowerToughnessModifier from flat numbers, which
/// cannot express a live-evaluated modifier such as BecomesBaseCreatureComponent — that one has to
/// read the creature's current base stats to cancel them out. This action carries the modifier
/// itself instead of the numbers to build one.
///
/// The template is cloned per target with SourceCardId stamped, so a mass effect
/// (Polymorphist's Jest) does not share one instance across creatures.
/// </summary>
public record AddCustomModifierAction : EffectAction
{
	public PowerToughnessModifier? Modifier { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		if (Modifier == null)
			return new ActionResult(gameState);

		var state = gameState;
		var sourceId = GetInput<int>(ContextKeys.SourceCardId, 0);

		foreach (var targetId in ResolveTargetIds())
		{
			if (state.GetObject(targetId) is not Card card)
				continue;
			if (!card.HasComponent<CreatureComponent>())
				continue;

			var stamped = Modifier with { SourceCardId = sourceId };

			state = state.UpdateObject(
				targetId,
				card with
				{
					Components = card.Components.Add(stamped),
				}
			);
		}

		return new ActionResult(state);
	}
}
