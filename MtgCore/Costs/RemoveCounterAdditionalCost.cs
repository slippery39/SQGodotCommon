using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Additional cost: remove one or more charge counters from the permanent activating the ability —
/// "{T}, Remove a gold counter from this artifact: Draw a card" (Dragon's Hoard).
///
/// The green pass deliberately deferred this, with the stated trigger being "a second card that
/// wants to spend counters". Barkhide Troll's counter removal was reskinned as a once-per-turn cap
/// because what the removal was FOR was bounding the ability; Dragon's Hoard is different — the
/// counters are accumulated by a trigger and the whole card is the exchange rate between them and
/// cards drawn, so a cap would delete the card rather than approximate it.
///
/// NOT A SELECTION COST. Every other AdditionalCost that takes payment asks the player which
/// object to spend; a counter is fungible, and "which of your three identical gold counters" is
/// not a decision. So RequiresSelection is false and GetValidPayments returns empty, which puts
/// this in the same category as LifeAdditionalCost: validated against state, paid without a
/// prompt. That also means MtgActionGenerator and the Godot cost walker need no changes at all.
///
/// Counters are removed from the SOURCE. A cost that could eat counters off an arbitrary permanent
/// is a different card and can add a target when one exists.
/// </summary>
public record RemoveCounterAdditionalCost : AdditionalCost
{
	public string Kind { get; init; } = "charge";
	public int Count { get; init; } = 1;

	public override bool RequiresSelection => false;

	public override string Describe() =>
		Count == 1
			? $"Remove a {Kind} counter from this permanent"
			: $"Remove {Count} {Kind} counters from this permanent";

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
	)
	{
		if (CounterCount(state, sourceCardId) < Count)
			return ValidationResult.Invalid($"Not enough {Kind} counters to remove");

		return ValidationResult.Valid;
	}

	public override GameState Pay(
		GameState state,
		int castingPlayerId,
		int sourceCardId,
		ImmutableList<int> paymentIds
	)
	{
		if (!state.HasObject(sourceCardId) || state.GetObject(sourceCardId) is not Card card)
			return state;

		var existing = card.GetComponents<ChargeCounterComponent>()
			.FirstOrDefault(c => c.IsKind(Kind));
		if (existing == null)
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

	private int CounterCount(GameState state, int sourceCardId)
	{
		if (!state.HasObject(sourceCardId) || state.GetObject(sourceCardId) is not Card card)
			return 0;

		return card.GetComponents<ChargeCounterComponent>()
				.FirstOrDefault(c => c.IsKind(Kind))
				?.Count ?? 0;
	}
}
