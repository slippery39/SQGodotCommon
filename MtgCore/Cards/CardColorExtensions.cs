namespace MtgCore;

/// <summary>
/// Bulk colour assignment for set files that are already organised by colour.
///
/// The cube's colour sections hold ~67 cards each and nearly all of them want exactly one pip of
/// their section's colour, so stamping at the section boundary is one edit instead of 335. A card
/// that needs something else — a double pip, or a gold card's two colours — sets its own pips via
/// the builder's WithPips and is left alone here.
/// </summary>
public static class CardColorExtensions
{
	/// <summary>
	/// One pip of <paramref name="color"/> on every card that has not already declared its own.
	///
	/// Lands are skipped: a land's colour is what it PRODUCES (LandColorComponent), not what it
	/// costs, and giving one a pip would make it uncastable by the very colour it exists to
	/// supply. The cube has no lands today, but this is shared and a future set will.
	/// </summary>
	public static IEnumerable<Card> InColor(this IEnumerable<Card> cards, ManaColor color) =>
		cards.Select(card =>
			card.ColorPips.IsEmpty && !card.HasSubtype("Land")
				? card with
				{
					ColorPips = ManaPool.Empty.Add(color, 1),
				}
				: card
		);
}
