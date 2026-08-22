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

	/// <summary>
	/// Gideon Jura's 0: a 6/6 that attacks the turn it appears and is gone at end of turn.
	///
	/// The printed ability turns Gideon himself into a 6/6 creature until end of turn, which this
	/// engine cannot express — creature and non-creature permanents are routed apart at cast time,
	/// and a walker carries no CreatureComponent. A one-turn token reaches the same board state:
	/// one attack from a 6/6, then nothing. Haste is what makes it an attack rather than a gift,
	/// and ExileAtEndOfTurnComponent is what stops it being a 6/6 every turn forever.
	///
	/// Gideon does NOT stop being a planeswalker while it is out, so unlike the real card he can
	/// still be attacked that turn. That is the cost of the substitution, and it is the direction
	/// that favours the opponent.
	/// </summary>
	public static Card GideonAvatar() =>
		new()
		{
			Name = "Gideon",
			Subtypes = ImmutableHashSet.Create(
				StringComparer.OrdinalIgnoreCase,
				"Human",
				"Soldier"
			),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = 6,
					Toughness = 6,
					HasHaste = true,
				},
				new ExileAtEndOfTurnComponent()
			),
		};

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
