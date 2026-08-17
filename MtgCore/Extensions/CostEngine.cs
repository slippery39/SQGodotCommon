using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// The single place a card's mana cost is adjusted before it is paid.
///
/// Previously CastSpellAction and CastCreatureAction each carried their own private copy of the
/// affinity calculation and CastPermanentAction had none at all, so a non-creature permanent
/// could never have its cost reduced. Both reducers and increasers now route through here, which
/// means a new cost effect is one edit rather than three, and no cast path can silently miss it.
///
/// Order: reductions first, then taxes, floored at 0. Reduction-then-tax is the MTG rule
/// (cost increases are applied after decreases), and it matters — a spell reduced to 0 by
/// affinity still costs 1 under Vryn Wingmare.
/// </summary>
public static class CostEngine
{
	public static int ComputeEffectiveCost(this GameState state, Card card, int playerId)
	{
		var cost = card.ManaCost;

		if (card.HasComponent<AffinityComponent>())
		{
			var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
			var artifactCount = state
				.GetCardsInZone(battlefieldId)
				.Count(c => c.HasSubtype("Artifact"));
			cost = Math.Max(0, cost - artifactCount);
		}

		return Math.Max(0, cost + ComputeTax(state, card));
	}

	/// <summary>
	/// Total tax from SpellTaxComponents on BOTH battlefields.
	///
	/// Both, because a tax like Vryn Wingmare's "noncreature spells cost {1} more" is symmetric —
	/// it taxes its own controller too. Scanning only the opponent's side would make the card
	/// strictly better than printed.
	/// </summary>
	private static int ComputeTax(GameState state, Card card)
	{
		var isCreature = card.HasComponent<CreatureComponent>();
		var tax = 0;

		foreach (
			var battlefieldKey in new[]
			{
				MtgObjectKeys.Player1Battlefield,
				MtgObjectKeys.Player2Battlefield,
			}
		)
		{
			var zoneId = state.GetWellKnownId(battlefieldKey);
			if (zoneId == 0)
				continue;

			foreach (var permanent in state.GetCardsInZone(zoneId))
			foreach (var spellTax in permanent.GetComponents<SpellTaxComponent>())
			{
				if (spellTax.NonCreatureOnly && isCreature)
					continue;
				tax += spellTax.Amount;
			}
		}

		return tax;
	}
}
