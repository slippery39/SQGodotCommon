namespace MtgCore.Cards.Builders;

/// <summary>
/// Fluent builder for TargetingStrategy. Use via 'using static MtgCore.Cards.Builders.TargetBuilder;'
/// then write: Single().PlayersOrCreatures(), AllValid().Creatures(), etc.
/// </summary>
public class TargetBuilder
{
	private readonly TargetSelectionMode _mode;

	private TargetBuilder(TargetSelectionMode mode) => _mode = mode;

	public static TargetBuilder Single() => new(TargetSelectionMode.UserSelect);

	public static TargetBuilder AllValid() => new(TargetSelectionMode.AllValid);

	public static TargetBuilder Random() => new(TargetSelectionMode.Random);

	public TargetingStrategy PlayersOrCreatures() =>
		Build(TargetSpecification.PlayersOrCreatures());

	public TargetingStrategy OpponentCreatures() => Build(TargetSpecification.OpponentCreatures());

	public TargetingStrategy OpponentOrOpponentCreatures() =>
		Build(TargetSpecification.OpponentOrOpponentCreatures());

	public TargetingStrategy YourCreatures() =>
		Build(TargetSpecification.CreatureControlledByYou());

	public TargetingStrategy Creatures() => Build(TargetSpecification.Creatures());

	public TargetingStrategy WithSpec(TargetSpecification spec) => Build(spec);

	private TargetingStrategy Build(TargetSpecification spec) =>
		_mode switch
		{
			TargetSelectionMode.UserSelect => TargetingStrategy.SingleTarget(spec),
			TargetSelectionMode.AllValid => TargetingStrategy.AllValid(spec),
			TargetSelectionMode.Random => TargetingStrategy.RandomTarget(spec),
			_ => TargetingStrategy.SingleTarget(spec),
		};
}
