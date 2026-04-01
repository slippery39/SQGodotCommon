using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Static definitions for all cards in the game.
/// Cards are pure data templates — OwnerId and ControllerId default to 0
/// and are stamped on at deck construction time via 'with'.
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
	/// Careful Study — 1 mana instant.
	/// "Draw 2 cards, then discard 2 cards."
	/// </summary>
	public static InstantCard CarefulStudy() =>
		new()
		{
			Name = "Careful Study",
			ManaCost = 1,
			Effects = ImmutableList.Create(
				new CardEffect
				{
					TargetingStrategy = TargetingStrategy.NoTarget(),
					ActionTemplate = new PipelineAction
					{
						Steps = ImmutableList.Create<GameAction>(
							new DrawCardsAction
							{
								Amount = 2,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
							new SelectCardsFromHandAction
							{
								Prompt = "Choose 2 cards to discard",
								MinChoices = 2,
								MaxChoices = 2,
								OutputKey = ContextKeys.SelectedCardIds,
							},
							new DiscardCardsAction
							{
								CardIdsContextKey = ContextKeys.SelectedCardIds,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							}
						),
					},
				}
			),
		};

	/// <summary>
	/// Telling Time — 2 mana instant.
	/// "Look at the top three cards of your library. Put one into your hand,
	///  one on top of your library, and one on the bottom of your library."
	///
	/// Intermediate context keys are defined inline here since they are
	/// only meaningful within this card's pipeline.
	/// </summary>
	public static InstantCard TellingTime() =>
		new()
		{
			Name = "Telling Time",
			ManaCost = 2,
			Effects = ImmutableList.Create(
				new CardEffect
				{
					TargetingStrategy = TargetingStrategy.NoTarget(),
					ActionTemplate = new PipelineAction
					{
						Steps = ImmutableList.Create<GameAction>(
							new LookAtTopCardsAction
							{
								Amount = 3,
								OutputKey = ContextKeys.TopCardIds,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
							new SelectCardFromContextAction
							{
								Prompt = "Choose a card to put into your hand",
								MinChoices = 1,
								MaxChoices = 1,
								OutputKey = "tt_hand_pick",
								CardIdsContextKey = ContextKeys.TopCardIds,
							},
							new MoveCardToHandAction
							{
								CardIdContextKey = "tt_hand_pick",
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
							new SelectCardFromContextAction
							{
								Prompt = "Choose a card to put on top of your library",
								MinChoices = 1,
								MaxChoices = 1,
								OutputKey = "tt_top_pick",
								CardIdsContextKey = ContextKeys.TopCardIds,
								ExcludeContextKeys = ImmutableList.Create("tt_hand_pick"),
							},
							new MoveCardToTopOfLibraryAction
							{
								CardIdContextKey = "tt_top_pick",
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
							new ExcludeSelectedCardsAction
							{
								CardIdsContextKey = ContextKeys.TopCardIds,
								ExcludeContextKeys = ImmutableList.Create(
									"tt_hand_pick",
									"tt_top_pick"
								),
								OutputKey = ContextKeys.RemainingCardIds,
							},
							new MoveCardToBottomOfLibraryAction
							{
								CardIdsContextKey = ContextKeys.RemainingCardIds,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							}
						),
					},
				}
			),
		};

	/// <summary>
	/// Dark Confidant — 2 mana creature (2/1).
	/// "At the beginning of your upkeep, reveal the top card of your library
	///  and put it into your hand. You lose life equal to its mana cost."
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
