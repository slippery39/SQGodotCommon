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
///
/// OneSided drops the return damage — "target creature you control deals damage equal to its power
/// to target creature you don't control" (Rabid Bite, Hunter's Edge). Kept as a flag on this
/// action rather than a separate one because the ordering subtlety above (read both powers first)
/// is worth having in exactly one place.
///
/// SOURCE FALLBACK: on a SPELL, ContextKeys.SourceCardId is the spell card, which has no
/// CreatureComponent — so before this fallback existed, every fight spell bailed out silently and
/// did nothing at all. Hollowmere's Set Upon the Pack shipped in that state. When the source is
/// not a creature the caster's strongest creature fights instead, chosen by the same picker
/// TargetSelectionMode.Best uses so a card that buffs then fights cannot pick two different
/// creatures.
/// </summary>
public record FightAction : EffectAction
{
	public bool OneSided { get; init; } = false;

	public override ActionResult Execute(GameState gameState)
	{
		var targetId = ResolveTargetIds().FirstOrDefault();

		// HasObject before GetObject throughout: GetObject is a raw dictionary indexer and throws,
		// and a stored source or target id can name a creature that has since died — an action is
		// re-validated against earlier states during CommitChain replay. "Gone" must mean "no
		// fight", not a KeyNotFoundException that kills the game.
		var sourceId = GetInput<int>(ContextKeys.SourceCardId, 0);
		if (
			sourceId == 0
			|| !gameState.HasObject(sourceId)
			|| gameState.GetObject(sourceId) is not Card sourceCard
			|| !sourceCard.HasComponent<CreatureComponent>()
		)
			sourceId = gameState.GetStrongestCreature(
				GetInput<int>(ContextKeys.CastingPlayerId, 0)
			);

		if (sourceId == 0 || targetId == 0 || sourceId == targetId)
			return new ActionResult(gameState);

		if (!gameState.HasObject(sourceId) || !gameState.HasObject(targetId))
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

		var damage = ImmutableList.Create<GameAction>(
			new DealDamageAction
			{
				Amount = sourcePower,
				TargetIds = ImmutableList.Create(targetId),
			}
		);

		if (!OneSided)
			damage = damage.Add(
				new DealDamageAction
				{
					Amount = targetPower,
					TargetIds = ImmutableList.Create(sourceId),
				}
			);

		return new ActionResult(gameState.SpawnActions(damage));
	}
}
