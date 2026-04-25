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
	public static Card LightningBolt() =>
		new()
		{
			Name = "Lightning Bolt",
			ManaCost = 1,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.SingleTarget(
								new IsPlayerSpecification().Or(new IsCreatureSpecification())
							),
							ActionTemplate = new DealDamageAction { Amount = 3 },
						}
					),
				}
			),
		};

	/// <summary>
	/// Lightning Helix — 2 mana instant.
	/// "Lightning Helix deals 3 damage to any target and you gain 3 life."
	/// </summary>
	public static Card LightningHelix() =>
		new()
		{
			Name = "Lightning Helix",
			ManaCost = 2,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
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
				}
			),
		};

	/// <summary>
	/// Careful Study — 1 mana instant.
	/// "Draw 2 cards, then discard 2 cards."
	/// </summary>
	public static Card CarefulStudy() =>
		new()
		{
			Name = "Careful Study",
			ManaCost = 1,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
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
				}
			),
		};

	/// <summary>
	/// Telling Time — 2 mana instant.
	/// "Look at the top three cards of your library. Put one into your hand,
	///  one on top of your library, and one on the bottom of your library."
	/// </summary>
	public static Card TellingTime() =>
		new()
		{
			Name = "Telling Time",
			ManaCost = 2,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
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
				}
			),
		};

	/// <summary>
	/// Dark Confidant — 2 mana creature (2/1).
	/// "At the beginning of your upkeep, reveal the top card of your library
	///  and put it into your hand. You lose life equal to its mana cost."
	/// Triggered ability not yet implemented — see TriggeredAbilityComponent (future).
	/// </summary>
	public static Card DarkConfidant() =>
		new()
		{
			Name = "Dark Confidant",
			ManaCost = 2,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 2, Toughness = 1 }
			),
		};

	/// <summary>
	/// Prodigal Sorcerer — 3 mana creature (1/1).
	/// Activated ability: "1 mana: Deal 1 damage to any target."
	/// Classic example of a simple damage ping ability.
	/// </summary>
	public static Card ProdigalSorcerer() =>
		new()
		{
			Name = "Prodigal Sorcerer",
			ManaCost = 3,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 1, Toughness = 1 },
				new ActivatedAbilityComponent
				{
					Name = "Ping",
					ManaCost = 1,
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.SingleTarget(
							new IsPlayerSpecification().Or(new IsCreatureSpecification())
						),
						ActionTemplate = new DealDamageAction { Amount = 1 },
					},
				}
			),
		};

	/// <summary>
	/// Throne of Bone — 1 mana artifact creature (1/1).
	/// Activated ability: "1 mana: Gain 2 life."
	/// Activated ability: "2 mana: Draw a card."
	/// Simple card with two abilities to exercise the multi-ability path.
	/// </summary>
	public static Card ThroneOfBone() =>
		new()
		{
			Name = "Throne of Bone",
			ManaCost = 1,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 1, Toughness = 1 },
				new ActivatedAbilityComponent
				{
					Name = "Gain Life",
					ManaCost = 1,
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.Self(),
						ActionTemplate = new GainLifeAction { Amount = 2 },
					},
				},
				new ActivatedAbilityComponent
				{
					Name = "Draw",
					ManaCost = 2,
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.NoTarget(),
						ActionTemplate = new DrawCardsAction
						{
							Amount = 1,
							PlayerIdContextKey = ContextKeys.CastingPlayerId,
						},
					},
				}
			),
		};

	/// <summary>
	/// Giant Growth — 1 mana instant.
	/// "Target creature gets +3/+3 until end of turn."
	/// Classic combat trick — applies a UntilEndOfTurn PowerToughnessModifier.
	/// </summary>
	public static Card GiantGrowth() =>
		new()
		{
			Name = "Giant Growth",
			ManaCost = 1,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.SingleTarget(
								new IsCreatureSpecification().And(
									new IsControlledByYouSpecification()
								)
							),
							ActionTemplate = new AddModifierAction
							{
								PowerBonus = 3,
								ToughnessBonus = 3,
								Duration = ModifierDuration.UntilEndOfTurn,
							},
						}
					),
				}
			),
		};

	/// <summary>
	/// Unholy Strength — 1 mana instant.
	/// "Target creature gets +2/+1 permanently."
	/// Simplified enchantment-style permanent buff using a Permanent modifier.
	/// </summary>
	public static Card UnholyStrength() =>
		new()
		{
			Name = "Unholy Strength",
			ManaCost = 1,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.SingleTarget(
								new IsCreatureSpecification().And(
									new IsControlledByYouSpecification()
								)
							),
							ActionTemplate = new AddModifierAction
							{
								PowerBonus = 2,
								ToughnessBonus = 1,
								Duration = ModifierDuration.Permanent,
							},
						}
					),
				}
			),
		};

	// ===== ZOO DECK CARDS =====

	/// <summary>
	/// Wild Nacatl — 1 mana creature (2/2).
	/// Cat Warrior. Simplified: no domain condition, just solid stats.
	/// </summary>
	public static Card WildNacatl() =>
		new()
		{
			Name = "Wild Nacatl",
			ManaCost = 1,
			Subtypes = ImmutableList.Create("Cat", "Warrior"),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 2, Toughness = 2 }
			),
		};

	/// <summary>
	/// Kird Ape — 1 mana creature (2/3).
	/// Ape. Simplified: no Forest condition, just solid stats.
	/// </summary>
	public static Card KirdApe() =>
		new()
		{
			Name = "Kird Ape",
			ManaCost = 1,
			Subtypes = ImmutableList.Create("Ape"),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 2, Toughness = 3 }
			),
		};

	/// <summary>
	/// Tarmogoyf — 2 mana creature (*/1+*).
	/// Power and toughness each scale with the total number of cards in all graveyards.
	/// Base Power = 0, Base Toughness = 1; GraveyardCountComponent adds the dynamic bonus.
	/// </summary>
	public static Card Tarmogoyf() =>
		new()
		{
			Name = "Tarmogoyf",
			ManaCost = 2,
			Subtypes = ImmutableList.Create("Lhurgoyf"),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 0, Toughness = 1 },
				new GraveyardCountComponent { Duration = ModifierDuration.Permanent }
			),
		};

	/// <summary>
	/// Path to Exile — 1 mana instant.
	/// "Exile target creature."
	/// Simplified: no basic land search for the exiled creature's controller.
	/// </summary>
	public static Card PathToExile() =>
		new()
		{
			Name = "Path to Exile",
			ManaCost = 1,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.SingleTarget(
								new IsCreatureSpecification().And(
									new IsControlledByOpponentSpecification()
								)
							),
							ActionTemplate = new ExileAction(),
						}
					),
				}
			),
		};

	/// <summary>
	/// Tribal Flames — 2 mana instant.
	/// "Tribal Flames deals 5 damage to any target."
	/// Simplified: fixed 5 damage, ignores domain condition.
	/// </summary>
	public static Card TribalFlames() =>
		new()
		{
			Name = "Tribal Flames",
			ManaCost = 2,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.SingleTarget(
								new IsPlayerSpecification().Or(new IsCreatureSpecification())
							),
							ActionTemplate = new DealDamageAction { Amount = 5 },
						}
					),
				}
			),
		};

	/// <summary>
	/// Qasali Pridemage — 2 mana creature (2/2).
	/// Cat Wizard. Activated ability: destroy target opponent's creature (proxy for
	/// the real card's sac-to-destroy-artifact/enchantment — no artifact type yet).
	/// </summary>
	public static Card QasaliPridemage() =>
		new()
		{
			Name = "Qasali Pridemage",
			ManaCost = 2,
			Subtypes = ImmutableList.Create("Cat", "Wizard"),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 2, Toughness = 2 },
				new ActivatedAbilityComponent
				{
					Name = "Destroy",
					ManaCost = 1,
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.SingleTarget(
							new IsCreatureSpecification().And(
								new IsControlledByOpponentSpecification()
							)
						),
						ActionTemplate = new DealDamageAction { Amount = 99 },
					},
				}
			),
		};

	/// <summary>
	/// Loam Lion — 1 mana creature (2/3).
	/// Cat. Simplified: no Forest condition, just good defensive stats.
	/// </summary>
	public static Card LoamLion() =>
		new()
		{
			Name = "Loam Lion",
			ManaCost = 1,
			Subtypes = ImmutableList.Create("Cat"),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 2, Toughness = 3 }
			),
		};
}
