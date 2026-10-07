using MagicCore.Cards;

namespace MagicCore.Sets.Fra;

/// <summary>40-card decks built only from implemented FRA cards — the console's opponents and the fuzz tests' decks.</summary>
public static class FraDecks
{
	public static readonly IReadOnlyDictionary<string, IReadOnlyList<CardDefinition>> All =
		new Dictionary<string, IReadOnlyList<CardDefinition>>
		{
			["White-Black"] = Build(
				("Plains", 6),
				("Swamp", 9),
				("Meticulous Commons", 2),
				("Prophesied End", 3),
				("Last Gasp", 2),
				("Terminal Criticism", 2),
				("Extended Absence", 1),
				("Cast Away Doubt", 2),
				("Rank Rat", 3),
				("Rampart Hunter", 3),
				("Screeching Soulbreaker", 3),
				("Sanctum Lurker", 2),
				("Thalia, the Survivor", 1),
				("Yargle, Glutton of Urborg", 1)
			),
			["Red-Green"] = Build(
				("Mountain", 7),
				("Forest", 7),
				("Konstrari Annex", 2),
				("Rockfall Vale", 1),
				("Chandra's Emberling", 3),
				("Heartstring Puller", 3),
				("Wrath of the Bloodmane", 2),
				("No Admittance", 2),
				("Arni, Renowned Champion", 1),
				("Koth, the Geomancer", 1),
				("Tethermage's Advantage", 2),
				("Yoshimaru, Scrappy Stray", 1),
				("Greenhouse Propagator", 3),
				("Arcane Amphisbaena", 2),
				("Way of the Wildspeaker", 1),
				("Way of the Pyromancer", 1),
				("Way of the Warlord", 1)
			),
			["White-Blue Jace"] = Build(
				("Plains", 7),
				("Island", 7),
				("Fatehold Annex", 2),
				("Deserted Beach", 1),
				("Academic Ascent", 3),
				("Repurposed Enforcer", 3),
				("Prophesied End", 2),
				("Thalia, the Survivor", 1),
				("Way of the Healer", 1),
				("Way of the Mentor", 1),
				("Mindseeker Oculus", 4),
				("Protege's Awakening", 2),
				("Tam's Resistance", 3),
				("Keeper of the Quiet Hour", 3)
			),
		};

	static IReadOnlyList<CardDefinition> Build(params (string Name, int Count)[] cards) =>
		cards.SelectMany(c => Enumerable.Repeat(FraCards.Get(c.Name), c.Count)).ToList();
}
