using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Unexhausts each target creature — this engine's "untap target creature", and the mirror of
/// <see cref="ExhaustCreatureAction"/>.
///
/// **Until this existed, only <see cref="StartTurnAction"/> could ever clear IsExhausted**, which
/// made every untap effect in Magic structurally unreachable: Manifold Key's "untap another target
/// artifact" was cut as unimplementable, and no combo trading in the untap operation — Splinter
/// Twin, Kiki-Jiki, a mana creature plus an untapper — could be expressed at all.
///
/// **A frozen creature does not untap**, which is the whole point of a freeze. The rule is
/// <see cref="CreatureComponent.IsFrozen"/>, shared with the untap step rather than restated here:
/// an untap effect that ignored it would silently undo Dungeon Geists and every "doesn't untap"
/// clause in the game, with nothing erroring.
///
/// **HasAttacked is deliberately NOT cleared.** Untapping is not vigilance, and a creature that has
/// already swung stays spent — the same separation <see cref="CreatureComponent.IsExhausted"/>
/// documents. Nothing needs the other behaviour today; a card that does should say so in its own
/// text rather than have it arrive by implication here.
///
/// No event is emitted. Exhausting has one because tapper payoffs (Gideon's Avenger) trigger on it;
/// nothing in the game triggers on untapping, and an event with no listener is three places to keep
/// in sync for nothing. Add <c>CreatureUnexhaustedEvent</c> — record, EventTypeNames constant, and
/// ExtractSubjectId — the day a card wants it.
/// </summary>
public record UnexhaustCreatureAction : EffectAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;

			if (state.GetObject(targetId) is not Card card)
				continue;

			var creature = card.GetComponent<CreatureComponent>();
			if (creature is null || !creature.IsExhausted || creature.IsFrozen)
				continue;

			state = state.UpdateObject(
				targetId,
				card.WithComponentReplaced(creature with { IsExhausted = false })
			);
		}

		return new ActionResult(state) { Events = ImmutableList<GameEvent>.Empty };
	}
}
