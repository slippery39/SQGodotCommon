using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Applies ReplacementModifierComponents to an event amount before the event happens.
///
/// Scanned live at each call site rather than pre-computed by StaticAbilityEngine, for the same
/// reason ThresholdComponent is: the push model only re-stamps on CreatureEnteredBattlefieldEvent
/// and PermanentLeftBattlefieldEvent, so any cached answer would go stale. A live scan of one
/// player's battlefield is cheap and always correct.
/// </summary>
public static class ReplacementEngine
{
	/// <summary>
	/// Returns <paramref name="amount"/> after every replacement modifier that
	/// <paramref name="playerId"/> controls and that matches <paramref name="evt"/> has applied.
	///
	/// ORDERING RULE: all multipliers apply first, then all additions. Real MTG lets the
	/// affected player choose the order, which a deterministic engine cannot do, so one order
	/// is fixed here. Multipliers-then-additions is the choice — it means a doubler does not
	/// also double someone else's flat bonus.
	///
	/// The result is clamped at 0: prevention effects can reduce an amount to nothing but must
	/// never invert it into its opposite.
	/// </summary>
	public static int ApplyReplacements(
		this GameState state,
		ReplaceableEvent evt,
		int playerId,
		int amount
	)
	{
		if (amount == 0)
			return 0;

		var multiplier = 1;
		var bonus = 0;
		var found = false;

		// Replacements stamped directly on the PLAYER. This is where a one-shot spell effect
		// lives — Safe Passage has no permanent to attach to, so without this a prevention
		// instant would have nowhere to exist.
		if (state.GetObject(playerId) is MtgPlayer player)
			foreach (var modifier in player.GetComponents<ReplacementModifierComponent>())
			{
				if (modifier.Event != evt)
					continue;

				found = true;
				multiplier *= modifier.Multiplier;
				bonus += modifier.Bonus;
			}

		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return found ? Math.Max(0, amount * multiplier + bonus) : amount;

		foreach (var card in state.GetCardsInZone(battlefieldId))
		{
			if (card.ControllerId != playerId)
				continue;

			foreach (var modifier in card.GetComponents<ReplacementModifierComponent>())
			{
				if (modifier.Event != evt)
					continue;

				found = true;
				multiplier *= modifier.Multiplier;
				bonus += modifier.Bonus;
			}
		}

		if (!found)
			return amount;

		return Math.Max(0, amount * multiplier + bonus);
	}
}
