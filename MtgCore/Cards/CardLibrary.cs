using System.Collections.Immutable;

namespace MtgCore;

/// <summary>
/// Static definitions for all cards in the game.
/// Cards are pure data templates — OwnerId and ControllerId default to 0
/// and are stamped on at deck construction time via 'with'.
///
/// Example:
///   var bolt = CardLibrary.LightningBolt() with { OwnerId = playerId, ControllerId = playerId };
/// </summary>
public static class CardLibrary
{
	/// <summary>
	/// Lightning Bolt — 1 mana instant.
	/// "Lightning Bolt deals 3 damage to any target."
	/// </summary>
	public static InstantCard LightningBolt() =>
		new()
		{
			Name = "Lightning Bolt",
			ManaCost = 1,
			Effects = ImmutableList.Create(
				new CardEffect
				{
					TargetingStrategy = TargetingStrategy.SingleTarget(
						new IsPlayerSpecification().Or(new IsCreatureSpecification())
					),
					ActionTemplate = new DealDamageAction { Amount = 3 },
				}
			),
		};

	/// <summary>
	/// Lightning Helix — 2 mana instant.
	/// "Lightning Helix deals 3 damage to any target and you gain 3 life."
	/// Two independent effects: damage to any target, life gain to the casting player.
	/// </summary>
	public static InstantCard LightningHelix() =>
		new()
		{
			Name = "Lightning Helix",
			ManaCost = 2,
			Effects = ImmutableList.Create(
				new CardEffect
				{
					TargetingStrategy = TargetingStrategy.SingleTarget(
						new IsPlayerSpecification().Or(new IsCreatureSpecification())
					),
					ActionTemplate = new DealDamageAction { Amount = 3 },
				},
				new CardEffect
				{
					TargetingStrategy = TargetingStrategy.Self(),
					ActionTemplate = new GainLifeAction { Amount = 3 },
				}
			),
		};
}
