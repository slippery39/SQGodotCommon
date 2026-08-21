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
	public static int ComputeEffectiveCost(
		this GameState state,
		Card card,
		int playerId,
		int xValue = 0
	)
	{
		var cost = card.ManaCost;

		// X is part of the printed cost, so it is added BEFORE any reduction — a convoked
		// X-spell should have its whole cost reduced, not just the fixed part.
		var x = card.GetComponent<XCostComponent>();
		if (x != null)
			cost += Math.Max(0, xValue) * x.Multiplier;

		if (card.HasComponent<AffinityComponent>())
		{
			var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
			var artifactCount = state
				.GetCardsInZone(battlefieldId)
				.Count(c => c.HasSubtype("Artifact"));
			cost = Math.Max(0, cost - artifactCount);
		}

		if (card.HasComponent<ConvokeComponent>())
			cost = Math.Max(0, cost - CountConvokers(state, playerId));

		cost = ComputeReductions(state, card, playerId, cost);

		return Math.Max(0, cost + ComputeTax(state, card));
	}

	/// <summary>
	/// Conditional reductions, from two sources.
	///
	/// The first is the card's own — "this spell costs {1} less if …" (Winged Words). The second
	/// is a permanent on the CASTER'S battlefield whose reduction applies to other cards —
	/// "creature spells you cast with power 4 or greater cost {2} less" (Goreclaw). That second
	/// source could not previously exist: this only ever read components off the card being cast.
	///
	/// CASTER'S BATTLEFIELD ONLY, unlike ComputeTax which scans both. A tax like Vryn Wingmare's
	/// is symmetric and taxes its own controller too, so scanning one side would make it stronger
	/// than printed. A discount is the opposite — Goreclaw reduces YOUR spells, and scanning both
	/// sides would hand the opponent your discount.
	/// </summary>
	private static int ComputeReductions(GameState state, Card card, int playerId, int cost)
	{
		foreach (var reduction in card.GetComponents<ConditionalCostReductionComponent>())
			if (reduction.Condition?.IsSatisfied(state, card.Id, playerId) == true)
				cost = Math.Max(0, cost - reduction.Amount);

		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return cost;

		var context = new TargetingContext
		{
			GameState = state,
			SourceCardId = card.Id,
			CastingPlayerId = playerId,
			IsNonTargeted = true,
		};

		foreach (var permanent in state.GetCardsInZone(battlefieldId))
		{
			// The card being cast is not on the battlefield, so it can never be its own source —
			// but a permanent carrying a self-reduction would otherwise be counted twice.
			if (permanent.Id == card.Id)
				continue;

			foreach (var reduction in permanent.GetComponents<ConditionalCostReductionComponent>())
			{
				if (reduction.AppliesTo == null)
					continue;
				if (!reduction.AppliesTo.IsSatisfiedBy(card.Id, context))
					continue;
				if (reduction.Condition?.IsSatisfied(state, permanent.Id, playerId) == false)
					continue;

				cost = Math.Max(0, cost - reduction.Amount);
			}
		}

		return cost;
	}

	/// <summary>
	/// Creatures that can help cast a convoke spell: ready ones the player controls.
	///
	/// Exhausted creatures and creatures that already attacked are excluded, which is what stops
	/// convoke being free after a full attack — the same bodies cannot both swing and pay.
	/// </summary>
	public static int CountConvokers(this GameState state, int playerId)
	{
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return 0;

		var count = 0;
		foreach (var card in state.GetCardsInZone(battlefieldId))
		{
			if (card.ControllerId != playerId)
				continue;

			var creature = card.GetComponent<CreatureComponent>();
			if (creature == null || creature.IsExhausted || creature.HasAttacked)
				continue;

			count++;
		}

		return count;
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
