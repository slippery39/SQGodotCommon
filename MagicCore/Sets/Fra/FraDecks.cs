using MagicCore.Cards;

namespace MagicCore.Sets.Fra;

/// <summary>Two 40-card decks built only from implemented FRA cards — the console's opponents and the fuzz tests' decks.</summary>
public static class FraDecks
{
	public static readonly IReadOnlyDictionary<string, IReadOnlyList<CardDefinition>> All =
		new Dictionary<string, IReadOnlyList<CardDefinition>>
		{
			["White-Black"] = Build(
				("Plains", 7),
				("Swamp", 10),
				("Prophesied End", 3),
				("Last Gasp", 3),
				("Terminal Criticism", 2),
				("Extended Absence", 2),
				("Cast Away Doubt", 2),
				("Rank Rat", 3),
				("Rampart Hunter", 3),
				("Screeching Soulbreaker", 3),
				("Thalia, the Survivor", 1),
				("Yargle, Glutton of Urborg", 1)
			),
			["Red-Green"] = Build(
				("Mountain", 9),
				("Forest", 8),
				("Chandra's Emberling", 4),
				("Heartstring Puller", 3),
				("Wrath of the Bloodmane", 3),
				("Artifist Acumen", 2),
				("Arni, Renowned Champion", 1),
				("Koth, the Geomancer", 1),
				("Tethermage's Advantage", 3),
				("Yoshimaru, Scrappy Stray", 2),
				("Greenhouse Propagator", 4)
			),
		};

	static IReadOnlyList<CardDefinition> Build(params (string Name, int Count)[] cards) =>
		cards.SelectMany(c => Enumerable.Repeat(FraCards.Get(c.Name), c.Count)).ToList();
}
