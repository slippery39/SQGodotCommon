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
		];

	public static IReadOnlyList<Card> Build(string deckName, int ownerId)
	{
		var info =
			All.FirstOrDefault(d => d.Name == deckName)
			?? throw new ArgumentException($"Unknown deck: {deckName}", nameof(deckName));
		return info.Builder(ownerId);
	}
}
