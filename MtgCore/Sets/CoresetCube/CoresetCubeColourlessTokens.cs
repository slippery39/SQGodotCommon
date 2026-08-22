using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Token templates for the Core Set Cube's colourless section. Same pattern as
/// CoresetCubeRedTokens — hand-built, because a token has no mana cost and is never cast.
///
/// Deliberately NOT part of CoresetCube.Cards — tokens must never appear in a draft pack.
///
/// The Thopter is the workhorse: Hangarback Walker makes one per +1/+1 counter it had when it
/// died, which is the only place in the cube where a token count is read off a dead creature.
/// It carries the Artifact subtype AND CardType.Artifact, because affinity and
/// SacrificeAdditionalCost read the subtype string while IsCardTypeSpecification reads the flag —
/// a token that is only one of the two is invisible to half the engine.
/// </summary>
public static class CoresetCubeColourlessTokens
{
	/// <summary>Hangarback Walker's payoff — one per +1/+1 counter it died with.</summary>
	public static Card Thopter() => Make("Thopter", 1, 1, "Thopter", flying: true);

	/// <summary>
	/// Druidic Satchel's creature mode. NOT an artifact — it is a plain green Saproling, and
	/// making it one would quietly feed affinity and every artifact-sacrifice payoff in the cube.
	/// </summary>
	public static Card Saproling() => Make("Saproling", 1, 1, "Saproling", alsoArtifact: false);

	private static Card Make(
		string name,
		int power,
		int toughness,
		string subtype,
		bool flying = false,
		bool alsoArtifact = true
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
