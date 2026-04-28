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
	private const string GoblinSubtype = "Goblin";

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

	// ===== GOBLINS DECK CARDS =====

	/// <summary>
	/// Goblin Token — 1/1 creature token.
	/// Created by Siege-Gang Commander and Krenko, Mob Boss.
	/// OwnerId/ControllerId default to 0 and are stamped by CreateTokenAction at runtime.
	/// </summary>
	public static Card GoblinToken() =>
		new()
		{
			Name = "Goblin",
			Subtypes = ImmutableList.Create(GoblinSubtype),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 1, Toughness = 1 }
			),
		};

	/// <summary>
	/// Goblin Guide — 1 mana creature (2/2, Haste).
	/// Goblin Scout. "Whenever Goblin Guide attacks, defending player reveals the
	/// top card of their library." — Reveal clause omitted; just a 2/2 haste for 1.
	/// </summary>
	public static Card GoblinGuide() =>
		new()
		{
			Name = "Goblin Guide",
			ManaCost = 1,
			Subtypes = ImmutableList.Create(GoblinSubtype, "Scout"),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent
				{
					Power = 2,
					Toughness = 2,
					HasHaste = true,
				}
			),
		};

	/// <summary>
	/// Goblin Lackey — 1 mana creature (1/1).
	/// "Whenever Goblin Lackey deals combat damage to a player, you may put a Goblin
	///  permanent card from your hand onto the battlefield."
	/// </summary>
	public static Card GoblinLackey() =>
		new()
		{
			Name = "Goblin Lackey",
			ManaCost = 1,
			Subtypes = ImmutableList.Create(GoblinSubtype),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 1, Toughness = 1 },
				new TriggeredAbilityComponent
				{
					Name = "Lackey Trigger",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CombatDamageDealtToPlayer,
						Filter = new IsSourceCardSpecification(),
					},
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.RandomTarget(
							new IsInHandSpecification().And(
								new IsSubtypeSpecification { Subtype = GoblinSubtype }
							)
						),
						ActionTemplate = new PutIntoBattlefieldAction(),
					},
				}
			),
		};

	/// <summary>
	/// Warren Instigator — 2 mana creature (1/1, Double Strike).
	/// "Whenever Warren Instigator deals combat damage to a player, you may put a Goblin
	///  permanent card from your hand onto the battlefield." Fires twice (double strike).
	/// </summary>
	public static Card WarrenInstigator() =>
		new()
		{
			Name = "Warren Instigator",
			ManaCost = 2,
			Subtypes = ImmutableList.Create(GoblinSubtype, "Berserker"),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent
				{
					Power = 1,
					Toughness = 1,
					HasDoubleStrike = true,
				},
				new TriggeredAbilityComponent
				{
					Name = "Instigator Trigger",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CombatDamageDealtToPlayer,
						Filter = new IsSourceCardSpecification(),
					},
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.RandomTarget(
							new IsInHandSpecification().And(
								new IsSubtypeSpecification { Subtype = GoblinSubtype }
							)
						),
						ActionTemplate = new PutIntoBattlefieldAction(),
					},
				}
			),
		};

	/// <summary>
	/// Goblin Chieftain — 3 mana creature (2/2, Haste).
	/// Lord effect ("other Goblins get +1/+1 and haste") deferred until static anthems
	/// are implemented (Step 2). For now: aggressive 2/2 haste body.
	/// </summary>
	public static Card GoblinChieftain() =>
		new()
		{
			Name = "Goblin Chieftain",
			ManaCost = 3,
			Subtypes = ImmutableList.Create(GoblinSubtype),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent
				{
					Power = 2,
					Toughness = 2,
					HasHaste = true,
				},
				new StaticPTBoostAbility
				{
					PowerBonus = 1,
					ToughnessBonus = 1,
					Filter = new IsSubtypeSpecification { Subtype = GoblinSubtype }
						.And(new IsNotSelfSpecification())
						.And(new IsControlledByYouSpecification()),
				},
				new StaticGrantKeywordAbility
				{
					GrantsHaste = true,
					Filter = new IsSubtypeSpecification { Subtype = GoblinSubtype }
						.And(new IsNotSelfSpecification())
						.And(new IsControlledByYouSpecification()),
				}
			),
		};

	/// <summary>
	/// Siege-Gang Commander — 5 mana creature (2/2).
	/// ETB: create three 1/1 Goblin creature tokens.
	/// Activated: 1 mana, sacrifice a Goblin → deal 2 damage to any target.
	/// </summary>
	public static Card SiegeGangCommander() =>
		new()
		{
			Name = "Siege-Gang Commander",
			ManaCost = 5,
			Subtypes = ImmutableList.Create(GoblinSubtype),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 2, Toughness = 2 },
				new TriggeredAbilityComponent
				{
					Name = "ETB Tokens",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsSourceCardSpecification(),
					},
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.NoTarget(),
						ActionTemplate = new CreateTokenAction
						{
							TokenTemplate = GoblinToken(),
							Count = 3,
						},
					},
				},
				new ActivatedAbilityComponent
				{
					Name = "Sacrifice Goblin",
					ManaCost = 1,
					AdditionalCosts = ImmutableList.Create<AdditionalCost>(
						new SacrificeAdditionalCost
						{
							Filter = new IsSubtypeSpecification { Subtype = GoblinSubtype },
							Count = 1,
						}
					),
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.SingleTarget(
							new IsPlayerSpecification().Or(new IsCreatureSpecification())
						),
						ActionTemplate = new DealDamageAction { Amount = 2 },
					},
				}
			),
		};

	/// <summary>
	/// Krenko, Mob Boss — 4 mana creature (3/3).
	/// Activated (tap proxy — no tap cost implemented): create X 1/1 Goblin tokens,
	/// where X is the number of Goblins you control. Uses pipeline to count at resolution.
	/// </summary>
	public static Card KrenkoMobBoss() =>
		new()
		{
			Name = "Krenko, Mob Boss",
			ManaCost = 4,
			Subtypes = ImmutableList.Create(GoblinSubtype, "Warrior"),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 3, Toughness = 3 },
				new ActivatedAbilityComponent
				{
					Name = "Create Tokens",
					ManaCost = 0,
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.NoTarget(),
						ActionTemplate = new PipelineAction
						{
							Steps = ImmutableList.Create<GameAction>(
								new CountCardsWithSubtypeAction
								{
									Subtype = GoblinSubtype,
									OutputKey = "krenko_goblin_count",
									PlayerIdContextKey = ContextKeys.CastingPlayerId,
								},
								new CreateTokenAction
								{
									TokenTemplate = GoblinToken(),
									CountInputKey = "krenko_goblin_count",
								}
							),
						},
					},
				}
			),
		};

	/// <summary>
	/// Goblin Grenade — 1 mana sorcery.
	/// Additional cost: sacrifice a Goblin.
	/// "Goblin Grenade deals 5 damage to any target."
	/// </summary>
	public static Card GoblinGrenade() =>
		new()
		{
			Name = "Goblin Grenade",
			ManaCost = 1,
			AdditionalCastCosts = ImmutableList.Create<AdditionalCost>(
				new SacrificeAdditionalCost
				{
					Filter = new IsSubtypeSpecification { Subtype = GoblinSubtype },
					Count = 1,
				}
			),
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
