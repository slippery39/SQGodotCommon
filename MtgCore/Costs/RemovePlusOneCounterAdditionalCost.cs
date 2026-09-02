using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Additional cost: remove one or more **+1/+1 counters** from the permanent activating the ability
/// — "Remove a +1/+1 counter from this creature: it deals 1 damage to any target" (Walking Ballista).
///
/// **The sibling of <see cref="RemoveCounterAdditionalCost"/>, and deliberately a separate type for
/// the same reason the two counter COMPONENTS are separate.** That cost spends
/// <see cref="ChargeCounterComponent"/>, which is a fungible resource carrying no P/T and immune to
/// counter doublers. This one spends <see cref="PlusOneCounterComponent"/>, which IS the creature's
/// body — paying it shrinks the creature, and a counters-matter deck is built on exactly that
/// exchange. Collapsing them into one cost with a "kind" string would let a gold counter pay for a
/// +1/+1 ability and let Hardened Scales inflate a charge counter.
///
/// **It must be a COST rather than an effect, and that is not a style choice.** Written as an
/// effect — "activate: remove a counter and deal 1 damage" — the ability is still legal with zero
/// counters, because <see cref="AddCountersAction"/> floors the count at 0 while the damage half
/// resolves regardless. That is free repeatable damage: an infinite loop, and one the AI would
/// happily take. As a cost, `Validate` refuses the activation outright.
///
/// NOT A SELECTION COST, same as its sibling: a counter is fungible, so `RequiresSelection` is false
/// and `GetValidPayments` is empty. `MtgActionGenerator` and the Godot cost walker need no changes.
///
/// Counters come off the SOURCE. A cost that could eat counters from an arbitrary permanent is a
/// different card and can take a target when one exists.
/// </summary>
public record RemovePlusOneCounterAdditionalCost : AdditionalCost
{
	public int Count { get; init; } = 1;

	public override bool RequiresSelection => false;

	public override string Describe() =>
		Count == 1
			? "Remove a +1/+1 counter from this creature"
			: $"Remove {Count} +1/+1 counters from this creature";

	public override ImmutableList<int> GetValidPayments(
		GameState state,
		int castingPlayerId,
		int sourceCardId
	) => ImmutableList<int>.Empty;

	public override ValidationResult Validate(
		GameState state,
		int castingPlayerId,
		int sourceCardId,
		ImmutableList<int> paymentIds
	) =>
		CounterCount(state, sourceCardId) < Count
			? ValidationResult.Invalid("Not enough +1/+1 counters to remove")
			: ValidationResult.Valid;

	public override GameState Pay(
		GameState state,
		int castingPlayerId,
		int sourceCardId,
		ImmutableList<int> paymentIds
	)
	{
		if (!state.HasObject(sourceCardId) || state.GetObject(sourceCardId) is not Card card)
			return state;

		var existing = card.GetComponent<PlusOneCounterComponent>();
		if (existing is null)
			return state;

		return state.UpdateObject(
			sourceCardId,
			card with
			{
				Components = card.Components.Replace(
					existing,
					existing with
					{
						Count = Math.Max(0, existing.Count - Count),
					}
				),
			}
		);
	}

	private static int CounterCount(GameState state, int sourceCardId) =>
		state.HasObject(sourceCardId) && state.GetObject(sourceCardId) is Card card
			? card.GetComponent<PlusOneCounterComponent>()?.Count ?? 0
			: 0;
}
