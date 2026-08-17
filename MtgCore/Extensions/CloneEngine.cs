using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Turns a card with CopyOnEnterComponent into a copy of a creature already on the battlefield.
/// See CopyOnEnterComponent for why the target is chosen automatically.
/// </summary>
public static class CloneEngine
{
	public static GameState ApplyCopyOnEnter(this GameState state, int cardId)
	{
		if (state.GetObject(cardId) is not Card clone)
			return state;

		var copyRule = clone.GetComponent<CopyOnEnterComponent>();
		if (copyRule == null)
			return state;

		var target = FindBestCreature(state, cardId);

		// Nothing to copy — Clone is printed 0/0 and would die immediately to the zero-toughness
		// rule, which is exactly what the real card does with an empty board.
		if (target == null)
			return state;

		// Everything except the copy rule itself is replaced. Keeping CopyOnEnterComponent would
		// make a reanimated Clone re-copy on its way back, which is wrong: a copy is a copy of
		// what it copied, not a fresh chance to choose.
		var components = target
			.Components.Where(c => c is not CopyOnEnterComponent)
			.ToImmutableArray();

		var subtypes = target.Subtypes;
		var types = target.EffectiveTypes;

		if (copyRule.RemainsIllusion)
		{
			subtypes = subtypes.Add("Illusion");
			components = components.Add(SacrificeWhenTargeted());
		}

		return state.UpdateObject(
			cardId,
			clone with
			{
				// Name, Components, Subtypes and Types come from the target. Id, OwnerId,
				// ControllerId and ManaCost stay the clone's own — a Clone of the opponent's
				// creature is still yours, and still cost what you paid.
				Name = target.Name,
				Types = types,
				Subtypes = subtypes,
				Components = components,
			}
		);
	}

	/// <summary>
	/// The highest effective power creature on either battlefield, excluding the clone itself.
	/// Ties break on the lowest card id so the choice is deterministic — a clone that resolved
	/// differently on re-evaluation would desync the AI's search from the real game.
	/// </summary>
	private static Card? FindBestCreature(GameState state, int cloneId)
	{
		Card? best = null;
		var bestPower = int.MinValue;

		foreach (
			var key in new[] { MtgObjectKeys.Player1Battlefield, MtgObjectKeys.Player2Battlefield }
		)
		{
			var zoneId = state.GetWellKnownId(key);
			if (zoneId == 0)
				continue;

			foreach (var card in state.GetCardsInZone(zoneId))
			{
				if (card.Id == cloneId)
					continue;
				if (!card.HasComponent<CreatureComponent>())
					continue;

				var power = state.GetEffectivePower(card.Id);
				if (power > bestPower || (power == bestPower && best != null && card.Id < best.Id))
				{
					best = card;
					bestPower = power;
				}
			}
		}

		return best;
	}

	/// <summary>
	/// Phantasmal Image's "when this becomes the target of a spell or ability, sacrifice it".
	///
	/// There is no targeting event to hook, so this fires on the closest observable proxy — the
	/// creature taking damage or being modified is what targeting it almost always leads to here.
	/// </summary>
	private static TriggeredAbilityComponent SacrificeWhenTargeted() =>
		new()
		{
			Name = "Illusion Fragility",
			Condition = new EventTriggerCondition
			{
				EventTypeName = EventTypeNames.CreatureDamaged,
				Filter = new IsSourceCardSpecification(),
			},
			Effect = new CardEffect
			{
				TargetingStrategy = TargetingStrategy.NoTarget(),
				ActionTemplate = new DestroyPermanentAction
				{
					TargetContextKey = ContextKeys.SourceCardId,
				},
			},
		};
}
