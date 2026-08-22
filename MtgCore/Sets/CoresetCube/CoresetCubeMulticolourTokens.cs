using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Token templates for the Core Set Cube's multicolour section. Same pattern as
/// CoresetCubeRedTokens — hand-built, because a token has no mana cost and is never cast.
///
/// Deliberately NOT part of CoresetCube.Cards — tokens must never appear in a draft pack.
///
/// Only two live here. Every other token the section makes already exists: the Soldier in
/// CoresetCubeTokens (Skyknight Vanguard, Ironroot Warlord, Heroic Reinforcements) and the Dragon
/// in CoresetCubeRedTokens (Draconic Disciple). Re-declaring either would give the cube two
/// different Soldiers, which the draft model keys by name and could not tell apart.
/// </summary>
public static class CoresetCubeMulticolourTokens
{
	/// <summary>Garruk, Apex Predator's +1 — a 3/3 with deathtouch.</summary>
	public static Card Beast() =>
		new()
		{
			Name = "Beast",
			Types = CardType.Creature,
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Beast"),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = 3,
					Toughness = 3,
					HasDeathtouch = true,
				}
			),
		};

	/// <summary>
	/// Experimental Overload's X/X, where X is the number of instant and sorcery cards in your
	/// graveyard.
	///
	/// A 0/0 body carrying the SAME live GraveyardCountComponent Enigma Drake uses, rather than a
	/// token whose stats are baked at creation. That is not a shortcut — it is what the printed
	/// card would do if it could: the token keeps growing as more spells hit the yard, and the
	/// alternative needed a way to create a token with a runtime P/T, which nothing supports.
	///
	/// AffectsToughness is TRUE here and false on the Drake, because a 0/0 with a power-only
	/// modifier dies to the zero-toughness rule the instant it arrives.
	/// </summary>
	public static Card Weird() =>
		new()
		{
			Name = "Weird",
			Types = CardType.Creature,
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Weird"),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 0, Toughness = 0 },
				new GraveyardCountComponent
				{
					Types = CardType.AnySpell,
					AffectsToughness = true,
					Duration = ModifierDuration.Permanent,
				}
			),
		};
}
