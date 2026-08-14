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

	public TargetingStrategy InstantOrSorceryInYourGraveyard() =>
		Build(new IsInstantOrSorceryInOwnGraveyardSpecification());

	public TargetingStrategy CreatureInYourGraveyard() =>
		Build(new IsCreatureInOwnGraveyardSpecification());

	/// Either player. Use for symmetric effects ("each player mills 4").
	public TargetingStrategy Players() => Build(new IsPlayerSpecification());

	/// The opposing player only — the mill/discard target, not their creatures.
	public TargetingStrategy Opponent() =>
		Build(new IsPlayerSpecification().And(new IsControlledByOpponentSpecification()));

	/// Any creature card in your own graveyard, as a non-targeted mass selection.
	public TargetingStrategy CreaturesInYourGraveyard() =>
		Build(new IsCreatureInOwnGraveyardSpecification());

	/// Every creature you control — for anthems and team pumps.
	public TargetingStrategy AllYourCreatures() =>
		Build(new IsCreatureSpecification().And(new IsControlledByYouSpecification()));

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
