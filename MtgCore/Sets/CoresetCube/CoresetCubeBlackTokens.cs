using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Black token templates for the Core Set Cube. Same pattern as CoresetCubeTokens — hand-built,
/// because a token has no mana cost and is never cast.
///
/// Deliberately NOT part of CoresetCube.Cards; tokens must never appear in a draft pack.
///
/// Xathrid Necromancer's Zombie is printed as entering TAPPED. Nothing here enters exhausted:
/// the token is created by CreateCardAction, which routes through PutIntoBattlefieldAction's ETB
/// ceremony, and that stamps summoning sickness — so the Zombie cannot attack the turn it
/// arrives regardless. Printing it tapped as well would cost it a second turn, not the one the
/// card intends.
/// </summary>
public static class CoresetCubeBlackTokens
{
	public static Card Zombie() => Make("Zombie", 2, 2, "Zombie");

	public static Card Saproling() => Make("Saproling", 1, 1, "Saproling");

	public static Card Demon() => Make("Demon", 5, 5, "Demon", flying: true);

	private static Card Make(
		string name,
		int power,
		int toughness,
		string subtype,
		bool flying = false
	) =>
		new()
		{
			Name = name,
			Types = CardType.Creature,
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
