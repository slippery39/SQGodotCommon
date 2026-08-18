using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Red token templates for the Core Set Cube. Same pattern as CoresetCubeTokens — hand-built,
/// because a token has no mana cost and is never cast.
///
/// Deliberately NOT part of CoresetCube.Cards; tokens must never appear in a draft pack.
///
/// The Goblin is the workhorse: seven red cards make them, which is what makes the tribal payoffs
/// (Goblin Chieftain, Goblin Piledriver, Volley Veteran) live rather than decorative.
/// </summary>
public static class CoresetCubeRedTokens
{
	public static Card Goblin() => Make("Goblin", 1, 1, "Goblin");

	/// <summary>Krenko's and Ogre Battledriver's hasty Goblin — Goblin Rabblemaster makes these.</summary>
	public static Card HastyGoblin() => Make("Goblin", 1, 1, "Goblin", haste: true);

	public static Card Elemental() => Make("Elemental", 1, 1, "Elemental");

	/// <summary>Flameshadow Conjuring's copy stand-in and Ogre Battledriver's payoff shape.</summary>
	public static Card HastyElemental() => Make("Elemental", 3, 1, "Elemental", haste: true);

	public static Card Thopter() =>
		Make("Thopter", 1, 1, "Thopter", flying: true, alsoArtifact: true);

	public static Card Dragon() => Make("Dragon", 4, 4, "Dragon", flying: true);

	private static Card Make(
		string name,
		int power,
		int toughness,
		string subtype,
		bool flying = false,
		bool haste = false,
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
					HasHaste = haste,
				}
			),
		};
	}
}
