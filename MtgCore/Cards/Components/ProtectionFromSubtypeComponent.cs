using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "Protection from Demons and from Dragons" — Baneslayer Angel.
///
/// Protection from a COLOUR is impossible in this engine: cards have no colour at all. Protection
/// from a creature TYPE is possible, because types are strings in Card.Subtypes, so that is what
/// this implements.
///
/// Two of MTG's four protection clauses apply here:
///   - can't be TARGETED by a source with a protected-against subtype (enforced in
///     IsCreatureSpecification, next to Shroud and Hexproof)
///   - can't be DEALT DAMAGE by such a source (enforced in AttackAction and DealDamageAction)
/// The other two do not: "can't be blocked" needs blocking, which this engine does not have, and
/// "can't be enchanted or equipped" is left out until an aura or equipment card needs it.
///
/// Subtypes are compared case-insensitively, matching Card.Subtypes' own comparer.
/// </summary>
public record ProtectionFromSubtypeComponent : GameComponent
{
	public ImmutableHashSet<string> Subtypes { get; init; } =
		ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// True when <paramref name="sourceCardId"/> has any subtype this creature is protected
	/// from. A source of 0 (no source card — e.g. a player-sourced effect) is never protected
	/// against.
	/// </summary>
	public bool ProtectsAgainst(GameState state, int sourceCardId)
	{
		if (sourceCardId == 0 || Subtypes.IsEmpty)
			return false;

		if (state.GetObject(sourceCardId) is not Card source)
			return false;

		foreach (var subtype in Subtypes)
			if (source.HasSubtype(subtype))
				return true;

		return false;
	}
}

public static class ProtectionExtensions
{
	/// <summary>
	/// True when <paramref name="cardId"/> is protected from <paramref name="sourceCardId"/>.
	/// Single entry point so targeting and damage cannot disagree about what protection means.
	/// </summary>
	public static bool IsProtectedFrom(this GameState state, int cardId, int sourceCardId)
	{
		if (sourceCardId == 0 || state.GetObject(cardId) is not Card card)
			return false;

		foreach (var protection in card.GetComponents<ProtectionFromSubtypeComponent>())
			if (protection.ProtectsAgainst(state, sourceCardId))
				return true;

		return false;
	}
}
