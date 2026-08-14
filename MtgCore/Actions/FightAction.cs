using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Two creatures each deal damage equal to their power to the other.
///
/// The source is read from ContextKeys.SourceCardId, so "this creature fights target creature"
/// needs only a target. Deliberately not routed through AttackAction: fighting is not an
/// attack, so it must not set HasAttacked, must not be blocked by summoning sickness, and
/// must not be constrained by Taunt or the Flying rule. That difference is the point of the
/// card — a ground creature can fight a flyer it could never attack.
///
/// Damage is applied through DealDamageAction so lethality, death triggers and lifelink all
/// route through the paths they already use.
/// </summary>
public record FightAction : EffectAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var sourceId = GetInput<int>(ContextKeys.SourceCardId, 0);
		var targetId = ResolveTargetIds().FirstOrDefault();

		if (sourceId == 0 || targetId == 0)
			return new ActionResult(gameState);

		if (gameState.GetObject(sourceId) is not Card source)
			return new ActionResult(gameState);
		if (gameState.GetObject(targetId) is not Card target)
			return new ActionResult(gameState);
		if (!source.HasComponent<CreatureComponent>() || !target.HasComponent<CreatureComponent>())
			return new ActionResult(gameState);

		// Both powers are read before any damage lands, so a creature that dies in the
		// exchange still deals its damage back.
		var sourcePower = gameState.GetEffectivePower(sourceId);
		var targetPower = gameState.GetEffectivePower(targetId);

		var state = gameState.SpawnActions(
			ImmutableList.Create<GameAction>(
				new DealDamageAction
				{
					Amount = sourcePower,
					TargetIds = ImmutableList.Create(targetId),
				},
				new DealDamageAction
				{
					Amount = targetPower,
					TargetIds = ImmutableList.Create(sourceId),
				}
			)
		);

		return new ActionResult(state);
	}
}
