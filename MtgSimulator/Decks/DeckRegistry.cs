using MtgCore;

namespace MtgSimulator;

public record DeckInfo(string Name, Func<int, IReadOnlyList<Card>> Builder);

public static class DeckRegistry
{
	public static IReadOnlyList<DeckInfo> All { get; } =
		[
			new DeckInfo("Zoo", ZooDeckFactory.Build),
			new DeckInfo("Goblins", GoblinsDeckFactory.Build),
			new DeckInfo("Dragonstorm", DragonstormDeckFactory.Build),
			new DeckInfo("Delver", DelverDeckFactory.Build),
			new DeckInfo("Reanimator", ReanimatorDeckFactory.Build),
			new DeckInfo("Traditional Storm", TraditionalStormDeckFactory.Build),
			new DeckInfo("Valakut", ValakutDeckFactory.Build),
			new DeckInfo("Jund", JundDeckFactory.Build),
			new DeckInfo("Affinity", AffinityDeckFactory.Build),
			// Designed-pool references. Every deck above is built from Legacy cards, and DES is
			// defined as every set EXCEPT Legacy — so without these `Gauntlet.For("DES")` had
			// nothing to return and the pool all recent work is measured in had no yardstick.
			new DeckInfo(DesignedGauntletDecks.Twin, DesignedGauntletDecks.BuildTwin),
			new DeckInfo(DesignedGauntletDecks.Elves, DesignedGauntletDecks.BuildElves),
			new DeckInfo(DesignedGauntletDecks.Reanimator, DesignedGauntletDecks.BuildReanimator),
		];

	public static IReadOnlyList<Card> Build(string deckName, int ownerId)
	{
		var info =
			All.FirstOrDefault(d => d.Name == deckName)
			?? throw new ArgumentException($"Unknown deck: {deckName}", nameof(deckName));
		return info.Builder(ownerId);
	}
}
