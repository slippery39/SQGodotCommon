using System.Collections.Immutable;
using ImmutableGameObjects;

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

	/// <summary>
	/// Dark Confidant — 2 mana creature (2/1).
	/// "At the beginning of your upkeep, reveal the top card of your library
	///  and put it into your hand. You lose life equal to its mana cost."
	///
	/// The creature has no CardEffects — its ability is a triggered ability
	/// that fires at upkeep. Use DarkConfidantTrigger() to get the pipeline
	/// to push onto the stack when the trigger fires.
	/// </summary>
	public static CreatureCard DarkConfidant() =>
		new()
		{
			Name = "Dark Confidant",
			ManaCost = 2,
			Power = 2,
			Toughness = 1,
		};

	/// <summary>
	/// The upkeep trigger pipeline for Dark Confidant.
	/// Push this onto the action stack at the beginning of the controlling
	/// player's upkeep.
	///
	/// Steps:
	///   1. Reveal top card — outputs card ID and mana cost to pipeline context
	///   2. Move revealed card to hand
	///   3. Lose life equal to revealed card's mana cost (read from context)
	/// </summary>
	public static PipelineAction DarkConfidantTrigger(int playerId) =>
		new()
		{
			Steps = ImmutableList.Create<GameAction>(
				new RevealTopCardAction { PlayerId = playerId },
				new MoveCardToHandAction
				{
					PlayerId = playerId,
					CardIdContextKey = ContextKeys.RevealedCardId,
				},
				new LoseLifeAction
				{
					PlayerId = playerId,
					AmountContextKey = ContextKeys.RevealedCardManaCost,
				}
			),
		};
}
