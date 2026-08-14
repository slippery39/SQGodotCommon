using System.Collections.Immutable;

namespace MtgCore;

/// <summary>
/// A named pool of cards that can be drafted as a unit.
///
/// Deliberately minimal: the set owns its card list, and cards do not know which set they
/// belong to. There is no SetCode field on Card, no rarity, and no pack-composition rules —
/// packs stay uniform random samples, which is what a cube wants anyway. Add those only when
/// a set actually needs them.
///
/// Lands are not filtered here. Draft.Create excludes them from packs and Draft.BuildDeck
/// supplies the mana base, so a set may include lands for other uses.
/// </summary>
public record CardSet(string Code, string Name, IReadOnlyList<Card> Cards)
{
	/// <summary>
	/// Cards that can appear in a draft pack. Mirrors Draft.Create's own land filter so
	/// callers can size a pool (or warn about one) without duplicating the rule.
	/// </summary>
	public IReadOnlyList<Card> Draftable =>
		Cards.Where(c => !c.HasSubtype("Land")).ToImmutableList();

	public override string ToString() => $"{Name} ({Code}, {Cards.Count} cards)";
}
