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
				("Prophesied End", 2),
				("Memory Trap", 1),
				("Last Gasp", 1),
				("Silence the Echo", 1),
				("Terminal Criticism", 1),
				("Break Under Pressure", 1),
				("Extended Absence", 1),
				("Vraska's Final Mercy", 1),
				("Surgical Precision", 1),
				("Rank Rat", 1),
				("Winter, Tormented Loner", 1),
				("Graft Surgeon", 1),
				("Rampart Hunter", 2),
				("Edgar, Ancient Bloodlord", 1),
				("Screeching Soulbreaker", 3),
				("Sanctum Lurker", 1),
				("Massacre Girl, Most Wanted", 1),
				("Thalia, the Survivor", 1),
				("Yargle, Glutton of Urborg", 1)
			),
			["Red-Green"] = Build(
				("Mountain", 7),
				("Forest", 7),
				("Konstrari Annex", 2),
				("Rockfall Vale", 1),
				("Chandra's Emberling", 1),
				("Skilled Battlecarver", 1),
				("Budding Insurgent", 1),
				("Heartstring Puller", 2),
				("Marwyn, the Clearcutter", 1),
				("Wrath of the Bloodmane", 1),
				("Ajani's Anguish", 1),
				("No Admittance", 2),
				("Arni, Renowned Champion", 1),
				("Koth, the Geomancer", 1),
				("Konstrari Charm", 1),
				("Compel Brutality", 1),
				("Yoshimaru, Scrappy Stray", 1),
				("Greenhouse Propagator", 2),
				("Fulminous Forte", 1),
				("Arcane Amphisbaena", 1),
				("Samut, Hazoret's Champion", 1),
				("Way of the Wildspeaker", 1),
				("Way of the Pyromancer", 1),
				("Eardrum Rattler", 1)
			),
			["White-Blue Jace"] = Build(
				("Plains", 7),
				("Island", 7),
				("Fatehold Annex", 2),
				("Deserted Beach", 1),
				("Academic Ascent", 3),
				("Repurposed Enforcer", 2),
				("Living Library", 1),
				("Prophesied End", 1),
				("Tetsuko Umezawa, Fugitive", 1),
				("Thalia, the Survivor", 1),
				("Way of the Healer", 1),
				("Way of the Mentor", 1),
				("Mindseeker Oculus", 3),
				("Protege's Awakening", 2),
				("Tam's Resistance", 3),
				("Keeper of the Quiet Hour", 1),
				("Divining Duelist", 2),
				("Fatehold Charm", 1)
			),
		};

	static IReadOnlyList<CardDefinition> Build(params (string Name, int Count)[] cards) =>
		cards.SelectMany(c => Enumerable.Repeat(FraCards.Get(c.Name), c.Count)).ToList();
}
