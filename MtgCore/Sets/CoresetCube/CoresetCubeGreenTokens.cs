using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Green token templates for the Core Set Cube. Same pattern as CoresetCubeRedTokens — hand-built,
/// because a token has no mana cost and is never cast.
///
/// Deliberately NOT part of CoresetCube.Cards; tokens must never appear in a draft pack.
///
/// The Beast is the workhorse here (Garruk, Elder Gargaroth, Thragtusk), and the Elf Warrior is
/// what makes the Elf tribal payoffs live — Dwynen's Elite and Dwynen herself both feed a lord
/// that is otherwise counting only the Elves you drew.
/// </summary>
public static class CoresetCubeGreenTokens
{
	/// <summary>Dwynen's Elite's token. An Elf, so it counts for every Elf lord in the section.</summary>
	public static Card ElfWarrior() => Make("Elf Warrior", 1, 1, CoresetCubeGreen.Elf, "Warrior");

	/// <summary>Garruk Wildspeaker, Elder Gargaroth and Thragtusk all make this.</summary>
	public static Card Beast() => Make("Beast", 3, 3, CoresetCubeGreen.Beast);

	/// <summary>Master of the Wild Hunt's upkeep token, and what its tap ability counts.</summary>
	public static Card Wolf() => Make("Wolf", 2, 2, CoresetCubeGreen.Wolf);

	/// <summary>Fungal Rebirth's payoff for a creature having died.</summary>
	public static Card Saproling() => Make("Saproling", 1, 1, CoresetCubeGreen.Saproling);

	/// <summary>Hornet Queen's swarm — flying deathtouch is the whole card.</summary>
	public static Card Insect() =>
		Make("Insect", 1, 1, CoresetCubeGreen.Insect, flying: true, deathtouch: true);

	/// <summary>
	/// Nissa, Worldwaker's animated land, rebuilt as a token — see her comment in
	/// CoresetCubeGreenPermanents for why the land half could not survive.
	/// </summary>
	public static Card Elemental() =>
		Make("Elemental", 4, 4, CoresetCubeGreen.Elemental, trample: true);

	private static Card Make(
		string name,
		int power,
		int toughness,
		string subtype,
		string secondSubtype = "",
		bool flying = false,
		bool trample = false,
		bool deathtouch = false
	)
	{
		var subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, subtype);
		if (!string.IsNullOrEmpty(secondSubtype))
			subtypes = subtypes.Add(secondSubtype);

		return new Card
		{
			Name = name,
			Types = CardType.Creature,
			Subtypes = subtypes,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = power,
					Toughness = toughness,
					HasFlying = flying,
					HasTrample = trample,
					HasDeathtouch = deathtouch,
				}
			),
		};
	}
}
