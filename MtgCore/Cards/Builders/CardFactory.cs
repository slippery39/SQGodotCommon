namespace MtgCore.Cards.Builders;

/// <summary>
/// Static entry points for the fluent card builder API.
///
/// Typical usage:
///   using static MtgCore.Cards.Builders.TargetBuilder;
///
///   CardFactory.Spell("Lightning Bolt", manaCost: 1)
///       .WithDamage(3).WithTarget(Single().PlayersOrCreatures())
///       .Build()
///
///   CardFactory.Creature("Goblin Guide", manaCost: 1, power: 2, toughness: 2)
///       .WithSubtype("Goblin")
///       .WithHaste()
///       .Build()
/// </summary>
public static class CardFactory
{
	/// <summary>
	/// A spell of unspecified type. Card.EffectiveTypes reports it as Instant|Sorcery, which is
	/// enough for "noncreature spell" but not for "target instant". Prefer Instant() or Sorcery()
	/// on new cards; this stays for the several hundred existing ones.
	/// </summary>
	public static SpellCardBuilder Spell(string name, int manaCost) => new(name, manaCost);

	public static SpellCardBuilder Instant(string name, int manaCost) =>
		new SpellCardBuilder(name, manaCost).WithTypes(CardType.Instant);

	public static SpellCardBuilder Sorcery(string name, int manaCost) =>
		new SpellCardBuilder(name, manaCost).WithTypes(CardType.Sorcery);

	public static CreatureCardBuilder Creature(
		string name,
		int manaCost,
		int power,
		int toughness
	) => new(name, manaCost, power, toughness);

	public static PermanentCardBuilder Enchantment(string name, int manaCost) =>
		new(name, manaCost, CardType.Enchantment);

	public static PermanentCardBuilder Artifact(string name, int manaCost) =>
		new(name, manaCost, CardType.Artifact);

	/// <summary>
	/// A planeswalker. Follow with .WithLoyalty(n) and one .WithLoyaltyAbility(...) per ability —
	/// the once-per-turn limit is shared across all of them, as the real rule requires.
	/// </summary>
	public static PermanentCardBuilder Planeswalker(string name, int manaCost) =>
		new(name, manaCost, CardType.Planeswalker);
}
