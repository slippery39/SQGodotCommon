using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore.Tests;

/// <summary>
/// Stable card definitions for engine tests. Frozen snapshot of CardLibrary —
/// cards here never change so engine tests are not broken by gameplay tuning.
/// </summary>
public static class TestCardLibrary
{
	private const string GoblinSubtype = "Goblin";
	private const string DragonSubtype = "Dragon";
	private const string LandSubtype = "Land";

	/// <summary>
	/// All playable cards in the library as owner-agnostic templates.
	/// OwnerId and ControllerId default to 0 and are stamped at deck-build time.
	/// Excludes token cards (GoblinToken) — those are created at runtime, not drawn.
	/// </summary>
	public static IReadOnlyList<Card> All { get; } =
		new List<Card>
		{
			CardFactory
				.Spell("Lightning Bolt", manaCost: 1)
				.WithDamage(3)
				.WithTarget(Single().PlayersOrCreatures())
				.Build(),
			CardFactory
				.Spell("Gut Shot", manaCost: 0)
				.WithDamage(1)
				.WithTarget(Single().PlayersOrCreatures())
				.Build(),
			CardFactory
				.Spell("Firebolt", manaCost: 1)
				.WithDamage(2)
				.WithTarget(Single().PlayersOrCreatures())
				.WithFlashback(2)
				.Build(),
			CardFactory
				.Spell("Lightning Helix", manaCost: 2)
				.WithDamage(3)
				.WithTarget(Single().PlayersOrCreatures())
				.WithLifeGain(3)
				.Build(),
			new()
			{
				Name = "Careful Study",
				ManaCost = 1,
				Components = ImmutableArray.Create<GameComponent>(
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
											TargetContextKey = ContextKeys.CastingPlayerId,
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
											TargetContextKey = ContextKeys.SelectedCardIds,
										}
									),
								},
							}
						),
					}
				),
			},
			new()
			{
				Name = "Faithless Looting",
				ManaCost = 1,
				Components = ImmutableArray.Create<GameComponent>(
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
											Amount = 3,
											TargetContextKey = ContextKeys.CastingPlayerId,
										},
										new SelectCardsFromHandAction
										{
											Prompt = "Choose 2 cards to discard",
											MinChoices = 3,
											MaxChoices = 3,
											OutputKey = ContextKeys.SelectedCardIds,
										},
										new DiscardCardsAction
										{
											TargetContextKey = ContextKeys.SelectedCardIds,
										}
									),
								},
							}
						),
					},
					new FlashbackComponent() { FlashbackManaCost = 1 }
				),
			},
			new()
			{
				Name = "Telling Time",
				ManaCost = 2,
				Components = ImmutableArray.Create<GameComponent>(
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
											ExcludeContextKeys = ImmutableList.Create(
												"tt_hand_pick"
											),
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
			},
			new()
			{
				Name = "Dark Confidant",
				ManaCost = 2,
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 1, Toughness = 4 },
					new TriggeredAbilityComponent
					{
						Name = "Dark Condidant Trigger",
						Condition = new EventTriggerCondition
						{
							EventTypeName = EventTypeNames.TurnStarted,
							Filter = new IsControlledByYouSpecification(),
						},
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new RevealTopCardAction
									{
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new LoseLifeAction
									{
										TargetContextKey = ContextKeys.CastingPlayerId,
										AmountContextKey = ContextKeys.RevealedCardManaCost,
									},
									new DrawCardsAction
									{
										Amount = 1,
										TargetContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
						},
					}
				),
			},
			CardFactory
				.Creature("Llanowar Elves", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype("Elf")
				.WithSubtype("Druid")
				.WithActivatedAbility("Mana Ramp", manaCost: 0, effect: eb => eb.WithAddMana(1))
				.Build(),
			CardFactory.Spell("Doom Blade", manaCost: 2).WithDestroy().Build(),
			CardFactory
				.Spell("Wrath of God", manaCost: 3)
				.WithDestroy()
				.WithTarget(AllValid().Creatures())
				.Build(),
			new()
			{
				Name = "Mox Pearl",
				ManaCost = 0,
				Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Artifact"),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new ActivatedAbilityComponent
					{
						Name = "Add Mana",
						ManaCost = 0,
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.Self(),
							ActionTemplate = new AddTemporaryManaAction { Amount = 1 },
						},
					}
				),
			},
			new()
			{
				Name = "Sol Ring",
				ManaCost = 1,
				Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Artifact"),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new ActivatedAbilityComponent
					{
						Name = "Add Mana",
						ManaCost = 0,
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.Self(),
							ActionTemplate = new AddTemporaryManaAction { Amount = 2 },
						},
					}
				),
			},
			new()
			{
				Name = "Glorious Anthem",
				ManaCost = 2,
				Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Enchantment"),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsCreatureSpecification(),
					}
				),
			},
			new()
			{
				Name = "Phyrexian Arena",
				ManaCost = 3,
				Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Enchantment"),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new TriggeredAbilityComponent
					{
						Name = "Draw and Pay",
						Condition = new EventTriggerCondition
						{
							EventTypeName = EventTypeNames.TurnStarted,
							Filter = new IsControlledByYouSpecification(),
						},
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new DrawCardsAction
									{
										Amount = 1,
										TargetContextKey = ContextKeys.CastingPlayerId,
									},
									new LoseLifeAction
									{
										Amount = 1,
										TargetContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
						},
					}
				),
			},
			new()
			{
				Name = "Bonesplitter",
				ManaCost = 1,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					"Artifact",
					"Equipment"
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new EquipmentComponent { PowerBonus = 2, ToughnessBonus = 0 },
					new ActivatedAbilityComponent
					{
						Name = "Equip",
						ManaCost = 1,
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.SingleTarget(
								new AndSpecification
								{
									Left = new IsOnBattlefieldSpecification(),
									Right = new AndSpecification
									{
										Left = new IsCreatureSpecification(),
										Right = new IsControlledByYouSpecification(),
									},
								}
							),
							ActionTemplate = new AttachEquipmentAction(),
						},
					}
				),
			},
			CardFactory
				.Creature("Prodigal Sorcerer", manaCost: 3, power: 1, toughness: 1)
				.WithActivatedAbility("Ping", manaCost: 1, effect: eb => eb.WithDamage(1))
				.Build(),
			CardFactory
				.Creature("Throne of Bone", manaCost: 1, power: 1, toughness: 1)
				.WithActivatedAbility("Gain Life", manaCost: 1, effect: eb => eb.WithLifeGain(2))
				.WithActivatedAbility("Draw", manaCost: 2, effect: eb => eb.WithDraw(1))
				.Build(),
			CardFactory
				.Spell("Giant Growth", manaCost: 1)
				.WithBoost(power: 3, toughness: 3)
				.Build(),
			CardFactory
				.Spell("Unholy Strength", manaCost: 1)
				.WithBoost(power: 2, toughness: 1, ModifierDuration.Permanent)
				.Build(),
			// ===== GOBLINS DECK CARDS =====
			CardFactory
				.Creature("Goblin Guide", manaCost: 1, power: 2, toughness: 2)
				.WithSubtype(GoblinSubtype)
				.WithSubtype("Scout")
				.WithHaste()
				.Build(),
			new()
			{
				Name = "Goblin Lackey",
				ManaCost = 1,
				Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, GoblinSubtype),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
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
			},
			new()
			{
				Name = "Warren Instigator",
				ManaCost = 2,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					GoblinSubtype,
					"Berserker"
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
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
			},
			new()
			{
				Name = "Goblin Chieftain",
				ManaCost = 2,
				Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, GoblinSubtype),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
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
						Filter = new IsSubtypeSpecification { Subtype = GoblinSubtype }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					},
					new StaticGrantKeywordAbility
					{
						GrantsHaste = true,
						Filter = new IsSubtypeSpecification { Subtype = GoblinSubtype }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				),
			},
			CardFactory
				.Creature("Siege-Gang Commander", manaCost: 4, power: 2, toughness: 2)
				.WithSubtype(GoblinSubtype)
				.WithTriggeredAbility(
					"ETB Tokens",
					TriggerConditions.OnSelfEntersBattlefield(),
					effect: eb => eb.WithCreateTokens(GoblinToken(), count: 3)
				)
				.WithActivatedAbility(
					"Sacrifice Goblin",
					manaCost: 1,
					effect: eb => eb.WithDamage(2).WithTarget(Single().PlayersOrCreatures()),
					costs: cb => cb.SacrificeSubtype(GoblinSubtype)
				)
				.Build(),
			new()
			{
				Name = "Krenko, Mob Boss",
				ManaCost = 3,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					GoblinSubtype,
					"Warrior"
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 1, Toughness = 3 },
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
									new CreateCardAction
									{
										CardTemplate = GoblinToken(),
										CountInputKey = "krenko_goblin_count",
									}
								),
							},
						},
					}
				),
			},
			CardFactory
				.Spell("Goblin Grenade", manaCost: 1)
				.WithSacrificeSubtypeCost(GoblinSubtype)
				.WithDamage(5)
				.WithTarget(Single().PlayersOrCreatures())
				.Build(),
			// ===== ZOO DECK CARDS =====
			CardFactory
				.Creature("Wild Nacatl", manaCost: 1, power: 2, toughness: 2)
				.WithSubtype("Cat")
				.WithSubtype("Warrior")
				.Build(),
			CardFactory
				.Creature("Kird Ape", manaCost: 1, power: 2, toughness: 3)
				.WithSubtype("Ape")
				.Build(),
			new()
			{
				Name = "Tarmogoyf",
				ManaCost = 2,
				Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Lhurgoyf"),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 0, Toughness = 1 },
					new GraveyardCountComponent { Duration = ModifierDuration.Permanent }
				),
			},
			CardFactory
				.Spell("Path to Exile", manaCost: 1)
				.WithExile()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			CardFactory.Spell("Tribal Flames", manaCost: 2).WithDamage(5).Build(),
			new()
			{
				Name = "Qasali Pridemage",
				ManaCost = 2,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					"Cat",
					"Wizard"
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 2, Toughness = 2 },
					new ActivatedAbilityComponent
					{
						Name = "Destroy",
						ManaCost = 1,
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.SingleTarget(
								TargetSpecification.OpponentCreatures()
							),
							ActionTemplate = new DealDamageAction { Amount = 10 },
						},
					}
				),
			},
			CardFactory
				.Creature("Loam Lion", manaCost: 1, power: 2, toughness: 3)
				.WithSubtype("Cat")
				.Build(),
			CardFactory
				.Spell("Slagstorm", manaCost: 3)
				.WithDamage(3)
				.WithTarget(AllValid().PlayersOrCreatures())
				.Build(),
			new()
			{
				Name = "Geist of Saint Traft",
				ManaCost = 3,
				Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Spirit"),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 2, Toughness = 2 },
					new TriggeredAbilityComponent
					{
						Name = "Geist Attack Trigger",
						Condition = new EventTriggerCondition
						{
							EventTypeName = EventTypeNames.CreatureAttacked,
							Filter = new IsSourceCardSpecification(),
						},
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new CreateCardAction
							{
								CardTemplate = new Card
								{
									Name = "Angel Token",
									Subtypes = ImmutableHashSet.Create(
										StringComparer.OrdinalIgnoreCase,
										"Angel"
									),
									Components = ImmutableArray.Create<GameComponent>(
										new PermanentComponent(),
										new CreatureComponent
										{
											Power = 4,
											Toughness = 4,
											HasFlying = true,
											HasHaste = true,
										},
										new TriggeredAbilityComponent
										{
											Name = "Angel Sacrifice Trigger",
											Condition = new EventTriggerCondition
											{
												EventTypeName = EventTypeNames.TurnEnded,
												Filter = new IsControlledByYouSpecification(),
											},

											Effect = new CardEffect
											{
												TargetingStrategy = TargetingStrategy.AllValid(
													new IsSourceCardSpecification()
												),
												ActionTemplate = new DestroyCreatureAction { },
											},
										}
									),
								},
								Count = 1,
							},
						},
					}
				),
			},
			CardFactory
				.Creature("Wall of Roots", manaCost: 3, power: 2, toughness: 5)
				.WithSubtype("Plant")
				.WithTaunt()
				.Build(),
			CardFactory
				.Creature("Raging Goblin", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(GoblinSubtype)
				.WithHaste()
				.Build(),
			CardFactory.Creature("Hill Giant", manaCost: 3, power: 3, toughness: 4).Build(),
			CardFactory
				.Creature("Snapcaster Mage", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype("Wizard")
				.WithEtbTrigger("ETB Flashback", e => e.WithGiveFlashback())
				.Build(),
			CardFactory.Creature("Grizzly Bears", manaCost: 2, power: 2, toughness: 2).Build(),
			CardFactory.Creature("Kalonian Tusker", manaCost: 2, power: 3, toughness: 3).Build(),
			CardFactory
				.Creature("Iron Golem", manaCost: 4, power: 5, toughness: 5)
				.WithSubtype("Golem")
				.Build(),
			CardFactory
				.Creature("Craw Wurm", manaCost: 6, power: 8, toughness: 4)
				.WithSubtype("Wurm")
				.Build(),
			CardFactory.Spell("Ancestral Recall", manaCost: 1).WithDraw(3).Build(),
			CardFactory.Spell("Gitaxian Probe", manaCost: 0).WithDraw(1).Build(),
			CardFactory
				.Creature("Mahamoti Djinn", manaCost: 6, power: 6, toughness: 7)
				.WithSubtype("Djinn")
				.WithFlying()
				.Build(),
			new()
			{
				Name = "Delver of Secrets",
				ManaCost = 1,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					"Human",
					"Wizard"
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 1, Toughness = 1 },
					new TriggeredAbilityComponent
					{
						Name = "Delver Transform",
						Condition = new EventTriggerCondition
						{
							EventTypeName = EventTypeNames.TurnStarted,
							Filter = new IsControlledByYouSpecification(),
						},
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new TransformAction
							{
								TargetContextKey = ContextKeys.SourceCardId,
							},
						},
					},
					new TransformComponent
					{
						OtherFaceName = "Insectile Aberration",
						OtherFaceSubtypes = ImmutableHashSet.Create(
							StringComparer.OrdinalIgnoreCase,
							"Insect"
						),
						OtherFaceComponents = ImmutableArray.Create<GameComponent>(
							new PermanentComponent(),
							new CreatureComponent
							{
								Power = 3,
								Toughness = 2,
								HasFlying = true,
							}
						),
					}
				),
			},
			// ===== DRAGONSTORM DECK CARDS =====
			new()
			{
				Name = "Sleight of Hand",
				ManaCost = 1,
				Components = ImmutableArray.Create<GameComponent>(
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
											Amount = 2,
											OutputKey = ContextKeys.TopCardIds,
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
										},
										new SelectCardFromContextAction
										{
											Prompt = "Choose a card to put into your hand",
											MinChoices = 1,
											MaxChoices = 1,
											OutputKey = "soh_hand_pick",
											CardIdsContextKey = ContextKeys.TopCardIds,
										},
										new MoveCardToHandAction
										{
											CardIdContextKey = "soh_hand_pick",
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
										},
										new ExcludeSelectedCardsAction
										{
											CardIdsContextKey = ContextKeys.TopCardIds,
											ExcludeContextKeys = ImmutableList.Create(
												"soh_hand_pick"
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
			},
			CardFactory.Spell("Lotus Bloom", manaCost: 0).WithAddMana(3).Build(),
			new()
			{
				Name = "Rite of Flame",
				ManaCost = 1,
				Components = ImmutableArray.Create<GameComponent>(
					new SpellComponent
					{
						Effects = ImmutableList.Create(
							new CardEffect
							{
								TargetingStrategy = TargetingStrategy.NoTarget(),
								ActionTemplate = new PipelineAction
								{
									Steps = ImmutableList.Create<GameAction>(
										new CountCardsWithNameAction
										{
											CardName = "Rite of Flame",
											Zone = ZoneType.Graveyard,
											OutputKey = "rite_count",
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
										},
										new AddTemporaryManaAction
										{
											Amount = 3,
											BonusAmountContextKey = "rite_count",
											TargetContextKey = ContextKeys.CastingPlayerId,
										}
									),
								},
							}
						),
					}
				),
			},
			CardFactory.Spell("Seething Song", manaCost: 3).WithAddMana(6).Build(),
			CardFactory
				.Creature("Hunted Dragon", manaCost: 10, power: 10, toughness: 10)
				.WithSubtype(DragonSubtype)
				.WithSubtype("Lizard")
				.WithFlying()
				.WithHaste()
				.Build(),
			CardFactory
				.Creature("Bogardan Hellkite", manaCost: 8, power: 7, toughness: 7)
				.WithSubtype(DragonSubtype)
				.WithFlying()
				.WithTriggeredAbility(
					"ETB Damage",
					TriggerConditions.OnSelfEntersBattlefield(),
					effect: eb =>
						eb.WithDamage(7).WithTarget(Random().OpponentOrOpponentCreatures())
				)
				.Build(),
			new()
			{
				Name = "Dragonstorm",
				ManaCost = 7,
				Components = ImmutableArray.Create<GameComponent>(
					new SpellComponent
					{
						HasStorm = true,
						Effects = ImmutableList.Create(
							new CardEffect
							{
								TargetingStrategy = TargetingStrategy.NoTarget(),
								ActionTemplate = new PipelineAction
								{
									Steps = ImmutableList.Create<GameAction>(
										new SelectCardFromLibraryAction
										{
											Subtype = DragonSubtype,
											OutputKey = "dragonstorm_target",
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
										},
										new PutIntoBattlefieldAction
										{
											CardIdContextKey = "dragonstorm_target",
										}
									),
								},
							}
						),
					}
				),
			},
			// ===== TRADITIONAL STORM DECK CARDS =====
			CardFactory
				.Spell("Tendrils of Agony", manaCost: 4)
				.WithStorm()
				.WithLoseLife(2)
				.WithTarget(Single().PlayersOrCreatures())
				.WithLifeGain(2)
				.Build(),
			CardFactory
				.Spell("Past in Flames", manaCost: 4)
				.WithFlashback(5)
				.WithAction(new GiveFlashbackAction(), AllValid().InstantOrSorceryInYourGraveyard())
				.Build(),
			// ===== LAND-ADJACENT CARDS =====
			new()
			{
				Name = "Rampant Growth",
				ManaCost = 2,
				Components = ImmutableArray.Create<GameComponent>(
					new SpellComponent
					{
						Effects = ImmutableList.Create(
							new CardEffect
							{
								TargetingStrategy = TargetingStrategy.NoTarget(),
								ActionTemplate = new PipelineAction
								{
									Steps = ImmutableList.Create<GameAction>(
										new SelectCardFromLibraryAction
										{
											Subtype = LandSubtype,
											OutputKey = "rampant_land",
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
										},
										new PutLandIntoPlayAction
										{
											CardIdContextKey = "rampant_land",
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
										}
									),
								},
							}
						),
					}
				),
			},
			new()
			{
				Name = "Primeval Titan",
				ManaCost = 6,
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 6, Toughness = 6 },
					new TriggeredAbilityComponent
					{
						Name = "ETB Fetch Lands",
						Condition = new EventTriggerCondition
						{
							EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
							Filter = new IsSourceCardSpecification(),
						},
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new SelectCardFromLibraryAction
									{
										Subtype = LandSubtype,
										OutputKey = "primeval_land_1",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new PutLandIntoPlayAction
									{
										CardIdContextKey = "primeval_land_1",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new SelectCardFromLibraryAction
									{
										Subtype = LandSubtype,
										OutputKey = "primeval_land_2",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new PutLandIntoPlayAction
									{
										CardIdContextKey = "primeval_land_2",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
						},
					}
				),
			},
			new()
			{
				Name = "Exploration",
				ManaCost = 1,
				Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Artifact"),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new ExtraLandPerTurnComponent()
				),
			},
			new()
			{
				Name = "Steppe Lynx",
				ManaCost = 0,
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 0, Toughness = 1 },
					new TriggeredAbilityComponent
					{
						Name = "Landfall",
						Condition = TriggerConditions.OnLandfall(),
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new AddModifierAction
							{
								PowerBonus = 2,
								ToughnessBonus = 2,
								Duration = ModifierDuration.UntilEndOfTurn,
								TargetContextKey = ContextKeys.SourceCardId,
							},
						},
					}
				),
			},
			new()
			{
				Name = "Terravore",
				ManaCost = 3,
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 0, Toughness = 0 },
					new LandsPlayedCountComponent { Duration = ModifierDuration.Permanent }
				),
			},
			// ===== REANIMATOR CARDS =====
			CardFactory
				.Spell("Reanimate", manaCost: 1)
				.WithAction(new PutIntoBattlefieldAction(), Single().CreatureInYourGraveyard())
				.Build(),
			new()
			{
				Name = "Bloodghast",
				ManaCost = 2,
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 2, Toughness = 1 },
					new TriggeredAbilityComponent
					{
						Name = "Landfall",
						ActiveInZone = ZoneType.Graveyard,
						Condition = TriggerConditions.OnLandfall(),
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new PutIntoBattlefieldAction
							{
								CardIdContextKey = ContextKeys.SourceCardId,
							},
						},
					}
				),
			},
			new()
			{
				Name = "Carnage Tyrant",
				ManaCost = 7,
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent
					{
						Power = 10,
						Toughness = 10,
						HasTrample = true,
						HasHexproof = true,
					}
				),
			},
			new()
			{
				Name = "Valakut, the Molten Pinnacle",
				ManaCost = 0,
				Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, LandSubtype),
				Components = ImmutableArray.Create<GameComponent>(
					new GrantEmblemComponent
					{
						Emblem = new Emblem
						{
							Name = "Valakut",
							Condition = new LandsPlayedCondition { Threshold = 7 },
							Effect = new CardEffect
							{
								TargetingStrategy = Random().OpponentOrOpponentCreatures(),
								ActionTemplate = new DealDamageAction { Amount = 3 },
							},
						},
					}
				),
			},
		};

	public static Card GetByName(string name) =>
		All.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
		?? throw new InvalidOperationException($"Card '{name}' not found in TestCardLibrary.");

	public static Card DoomBlade() => All.First(c => c.Name == "Doom Blade");

	public static Card GrizzlyBears() => All.First(c => c.Name == "Grizzly Bears");

	public static Card HillGiant() => All.First(c => c.Name == "Hill Giant");

	public static Card KalonianTusker() => All.First(c => c.Name == "Kalonian Tusker");

	public static Card IronGolem() => All.First(c => c.Name == "Iron Golem");

	public static Card MahamotiDjinn() => All.First(c => c.Name == "Mahamoti Djinn");

	public static Card CrawWurm() => All.First(c => c.Name == "Craw Wurm");

	public static Card AncestralRecall() => All.First(c => c.Name == "Ancestral Recall");

	public static Card RagingGoblin() => All.First(c => c.Name == "Raging Goblin");

	public static Card SavannahLions() =>
		new()
		{
			Name = "Savannah Lions",
			ManaCost = 1,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 2, Toughness = 1 }
			),
		};

	public static Card WrathOfGod() => All.First(c => c.Name == "Wrath of God");

	public static Card LightningBolt() => All.First(c => c.Name == "Lightning Bolt");

	public static Card Slagstorm() => All.First(c => c.Name == "Slagstorm");

	public static Card LightningHelix() => All.First(c => c.Name == "Lightning Helix");

	public static Card CarefulStudy() => All.First(c => c.Name == "Careful Study");

	public static Card TellingTime() => All.First(c => c.Name == "Telling Time");

	public static Card DarkConfidant() => All.First(c => c.Name == "Dark Confidant");

	public static Card ProdigalSorcerer() => All.First(c => c.Name == "Prodigal Sorcerer");

	public static Card ThroneOfBone() => All.First(c => c.Name == "Throne of Bone");

	public static Card GiantGrowth() => All.First(c => c.Name == "Giant Growth");

	public static Card UnholyStrength() => All.First(c => c.Name == "Unholy Strength");

	// ===== ARTIFACTS =====

	public static Card Mox() => All.First(c => c.Name == "Mox Pearl");

	public static Card SolRing() => All.First(c => c.Name == "Sol Ring");

	// ===== ENCHANTMENTS =====

	public static Card GloriousAnthem() => All.First(c => c.Name == "Glorious Anthem");

	public static Card PhyrexianArena() => All.First(c => c.Name == "Phyrexian Arena");

	// ===== EQUIPMENT =====

	public static Card Bonesplitter() => All.First(c => c.Name == "Bonesplitter");

	// ===== GOBLINS DECK CARDS =====

	public static Card GoblinToken() =>
		new()
		{
			Name = "Goblin",
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, GoblinSubtype),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 1, Toughness = 1 }
			),
		};

	public static Card GoblinGuide() => All.First(c => c.Name == "Goblin Guide");

	public static Card GoblinLackey() => All.First(c => c.Name == "Goblin Lackey");

	public static Card WarrenInstigator() => All.First(c => c.Name == "Warren Instigator");

	public static Card GoblinChieftain() => All.First(c => c.Name == "Goblin Chieftain");

	public static Card SiegeGangCommander() => All.First(c => c.Name == "Siege-Gang Commander");

	public static Card KrenkoMobBoss() => All.First(c => c.Name == "Krenko, Mob Boss");

	public static Card GoblinGrenade() => All.First(c => c.Name == "Goblin Grenade");

	// ===== ZOO DECK CARDS =====

	public static Card WildNacatl() => All.First(c => c.Name == "Wild Nacatl");

	public static Card KirdApe() => All.First(c => c.Name == "Kird Ape");

	public static Card Tarmogoyf() => All.First(c => c.Name == "Tarmogoyf");

	public static Card LlanowarElves() => All.First(c => c.Name == "Llanowar Elves");

	public static Card PathToExile() => All.First(c => c.Name == "Path to Exile");

	public static Card TribalFlames() => All.First(c => c.Name == "Tribal Flames");

	public static Card QasaliPridemage() => All.First(c => c.Name == "Qasali Pridemage");

	public static Card WallOfThorns() => All.First(c => c.Name == "Wall of Roots");

	public static Card GeistOfSaintTraft() => All.First(c => c.Name == "Geist of Saint Traft");

	public static Card LoamLion() => All.First(c => c.Name == "Loam Lion");

	// ===== DRAGONSTORM DECK CARDS =====

	public static Card SleightOfHand() => All.First(c => c.Name == "Sleight of Hand");

	public static Card LotusBoom() => All.First(c => c.Name == "Lotus Bloom");

	public static Card RiteOfFlame() => All.First(c => c.Name == "Rite of Flame");

	public static Card SeethingSong() => All.First(c => c.Name == "Seething Song");

	public static Card HuntedDragon() => All.First(c => c.Name == "Hunted Dragon");

	public static Card BogardanHellkite() => All.First(c => c.Name == "Bogardan Hellkite");

	public static Card Dragonstorm() => All.First(c => c.Name == "Dragonstorm");

	// ===== LAND =====

	public static Card Plains() =>
		new()
		{
			Name = "Plains",
			ManaCost = 0,
			Subtypes = ImmutableHashSet.Create(
				StringComparer.OrdinalIgnoreCase,
				LandSubtype,
				"Basic"
			),
			Components = ImmutableArray<GameComponent>.Empty,
		};

	public static Card Valakut() => GetByName("Valakut, the Molten Pinnacle");

	// ===== LAND-ADJACENT CARDS =====

	public static Card RampantGrowth() => All.First(c => c.Name == "Rampant Growth");

	public static Card PrimevalTitan() => All.First(c => c.Name == "Primeval Titan");

	public static Card Exploration() => All.First(c => c.Name == "Exploration");

	public static Card SteppeLynx() => All.First(c => c.Name == "Steppe Lynx");

	public static Card LandElemental() => All.First(c => c.Name == "Terravore");

	// ===== REANIMATOR CARDS =====

	public static Card Reanimate() => All.First(c => c.Name == "Reanimate");
}
