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
	public static SpellCardBuilder Spell(string name, int manaCost) => new(name, manaCost);

	public static CreatureCardBuilder Creature(
		string name,
		int manaCost,
		int power,
		int toughness
	) => new(name, manaCost, power, toughness);
}
