using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Blue token templates for the Core Set Cube. Same pattern as CoresetCubeTokens — hand-built,
/// because a token has no mana cost and is never cast.
///
/// Deliberately NOT part of CoresetCube.Cards; tokens must never appear in a draft pack.
///
/// Islandwalk on the Squid is dropped: lands are consumed into MaxMana rather than existing as
/// battlefield permanents, so there is no Island to check for.
/// </summary>
public static class CoresetCubeBlueTokens
{
	public static Card Drake() => Make("Drake", 2, 2, "Drake", flying: true);

	public static Card Thopter() =>
		Make("Thopter", 1, 1, "Thopter", flying: true, alsoArtifact: true);

	public static Card Squid() => Make("Squid", 1, 1, "Squid");

	public static Card ElementalBird() => Make("Elemental Bird", 4, 4, "Bird", flying: true);

	public static Card Illusion() => Make("Illusion", 2, 2, "Illusion");

	private static Card Make(
		string name,
		int power,
		int toughness,
		string subtype,
		bool flying = false,
		bool alsoArtifact = false
	)
	{
		var subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, subtype);
		if (alsoArtifact)
			subtypes = subtypes.Add("Artifact");

		return new Card
		{
			Name = name,
			Types = alsoArtifact ? CardType.Creature | CardType.Artifact : CardType.Creature,
			Subtypes = subtypes,
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
}
