using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Shared planeswalker helpers.
///
/// A planeswalker reaches the battlefield through two different paths — ResolvePermanentAction
/// when cast, PutIntoBattlefieldAction when created or reanimated — and neither knew about
/// loyalty. Both call StampEntry rather than each carrying its own copy, so a walker cannot
/// arrive at 0 loyalty down one path and full loyalty down the other.
/// </summary>
public static class PlaneswalkerExtensions
{
	/// <summary>
	/// Sets a newly-arrived planeswalker to its starting loyalty with its activation available.
	/// No-op for anything that is not a planeswalker, so callers need no type check.
	/// </summary>
	public static GameState StampPlaneswalkerEntry(this GameState state, int cardId)
	{
		if (state.GetObject(cardId) is not Card card)
			return state;

		var walker = card.GetComponent<PlaneswalkerComponent>();
		if (walker == null)
			return state;

		return state.UpdateObject(
			cardId,
			card.WithComponentReplaced(
				walker with
				{
					Loyalty = walker.StartingLoyalty,
					HasActivatedThisTurn = false,
				}
			)
		);
	}

	public static bool IsPlaneswalker(this GameState state, int cardId) =>
		state.GetObject(cardId) is Card card && card.HasComponent<PlaneswalkerComponent>();

	/// <summary>
	/// Removes loyalty from a planeswalker. Loyalty is floored at 0 rather than going negative;
	/// CheckStateBasedEffectsAction moves a walker at 0 to the graveyard on the next pass.
	/// </summary>
	public static GameState DamagePlaneswalker(this GameState state, int cardId, int amount)
	{
		if (state.GetObject(cardId) is not Card card)
			return state;

		var walker = card.GetComponent<PlaneswalkerComponent>();
		if (walker == null)
			return state;

		return state.UpdateObject(
			cardId,
			card.WithComponentReplaced(
				walker with
				{
					Loyalty = Math.Max(0, walker.Loyalty - amount),
				}
			)
		);
	}
}
