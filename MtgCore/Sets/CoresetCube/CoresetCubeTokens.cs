using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Token templates for the Core Set Cube. Hand-built rather than going through CardFactory
/// because a token has no mana cost and is never cast.
///
/// Deliberately NOT part of CoresetCube.Cards — tokens must never appear in a draft pack.
///
/// Vigilance is unimplemented in this engine (see CoresetCubeWhite's header), so the Knight and
/// Angel tokens are printed without it.
/// </summary>
public static class CoresetCubeTokens
{
	public static Card Soldier() => Vanilla("Soldier", 1, 1, "Soldier");

	public static Card Knight() => Vanilla("Knight", 2, 2, "Knight");

	public static Card Cat() => Vanilla("Cat", 2, 2, "Cat");

	public static Card Spirit() => Flyer("Spirit", 1, 1, "Spirit");

	public static Card Angel() => Flyer("Angel", 4, 4, "Angel");

	private static Card Vanilla(string name, int power, int toughness, string subtype) =>
		Make(name, power, toughness, subtype, flying: false);

	private static Card Flyer(string name, int power, int toughness, string subtype) =>
		Make(name, power, toughness, subtype, flying: true);

	private static Card Make(string name, int power, int toughness, string subtype, bool flying) =>
		new()
		{
			Name = name,
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, subtype),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = power,
					Toughness = toughness,
					HasFlying = flying,
				}
			),
		};
}
