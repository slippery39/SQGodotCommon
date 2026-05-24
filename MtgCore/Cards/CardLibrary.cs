using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Static definitions for all cards in the game.
/// Cards are pure data templates — OwnerId and ControllerId default to 0
/// and are stamped on at deck construction time via 'with'.
/// </summary>
public static class CardLibrary
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
					},
					new FlashbackComponent() { FlashbackManaCost = 3 }
				),
			},
			new()
			{
				Name = "Telling Time",
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
				Components = ImmutableList.Create<GameComponent>(
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
				.Spell("Wrath of God", manaCost: 4)
				.WithDestroy()
				.WithTarget(AllValid().Creatures())
				.Build(),
			new()
			{
				Name = "Mox",
				ManaCost = 0,
				Subtypes = ImmutableList.Create("Artifact"),
				Components = ImmutableList.Create<GameComponent>(
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
				Subtypes = ImmutableList.Create("Artifact"),
				Components = ImmutableList.Create<GameComponent>(
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
				Subtypes = ImmutableList.Create("Enchantment"),
				Components = ImmutableList.Create<GameComponent>(
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
				ManaCost = 2,
				Subtypes = ImmutableList.Create("Enchantment"),
				Components = ImmutableList.Create<GameComponent>(
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
				Subtypes = ImmutableList.Create("Artifact", "Equipment"),
				Components = ImmutableList.Create<GameComponent>(
					new PermanentComponent(),
					new EquipmentComponent { PowerBonus = 2, ToughnessBonus = 0 },
					new ActivatedAbilityComponent
					{
						Name = "Equip",
						ManaCost = 0,
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
				.Creature("Prodigal Sorcerer", manaCost: 2, power: 1, toughness: 1)
				.WithActivatedAbility("Ping", manaCost: 0, effect: eb => eb.WithDamage(1))
				.Build(),
			CardFactory
				.Creature("Throne of Bone", manaCost: 1, power: 1, toughness: 1)
				.WithActivatedAbility("Gain Life", manaCost: 0, effect: eb => eb.WithLifeGain(2))
				.WithActivatedAbility("Draw", manaCost: 1, effect: eb => eb.WithDraw(1))
				.Build(),
			CardFactory
				.Spell("Giant Growth", manaCost: 1)
				.WithBoost(power: 4, toughness: 4)
				.Build(),
			CardFactory
				.Spell("Unholy Strength", manaCost: 1)
				.WithBoost(power: 3, toughness: 2, ModifierDuration.Permanent)
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
				Subtypes = ImmutableList.Create(GoblinSubtype),
				Components = ImmutableList.Create<GameComponent>(
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
				Subtypes = ImmutableList.Create(GoblinSubtype, "Berserker"),
				Components = ImmutableList.Create<GameComponent>(
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
				ManaCost = 3,
				Subtypes = ImmutableList.Create(GoblinSubtype),
				Components = ImmutableList.Create<GameComponent>(
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
				Subtypes = ImmutableList.Create(GoblinSubtype, "Warrior"),
				Components = ImmutableList.Create<GameComponent>(
					new PermanentComponent(),
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
			CardFactory
				.Creature("Mogg Warmaster", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(GoblinSubtype)
				.WithEtbTrigger("ETB Token", eb => eb.WithCreateTokens(GoblinToken()))
				.WithComponent(
					new TriggeredAbilityComponent
					{
						Name = "Dies Token",
						Condition = TriggerConditions.OnSelfDies(),
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new CreateCardAction
							{
								CardTemplate = GoblinToken(),
								Count = 1,
							},
						},
						ActiveInZone = ZoneType.Graveyard,
					}
				)
				.Build(),
			CardFactory
				.Creature("Goblin Matron", manaCost: 2, power: 1, toughness: 1)
				.WithSubtype(GoblinSubtype)
				.WithEtbTrigger(
					"ETB Tutor",
					eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new SelectCardFromLibraryAction
									{
										Subtype = GoblinSubtype,
										OutputKey = "matron_target",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new MoveCardToHandAction
									{
										CardIdContextKey = "matron_target",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			CardFactory
				.Creature("Goblin Ringleader", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(GoblinSubtype)
				.WithEtbTrigger(
					"ETB Draw Goblins",
					eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new SelectCardFromLibraryAction
									{
										Subtype = GoblinSubtype,
										OutputKey = "ringleader_1",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new MoveCardToHandAction
									{
										CardIdContextKey = "ringleader_1",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new SelectCardFromLibraryAction
									{
										Subtype = GoblinSubtype,
										OutputKey = "ringleader_2",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new MoveCardToHandAction
									{
										CardIdContextKey = "ringleader_2",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new SelectCardFromLibraryAction
									{
										Subtype = GoblinSubtype,
										OutputKey = "ringleader_3",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new MoveCardToHandAction
									{
										CardIdContextKey = "ringleader_3",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// ===== ZOO DECK CARDS =====
			CardFactory
				.Creature("Wild Nacatl", manaCost: 1, power: 3, toughness: 3)
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
				Subtypes = ImmutableList.Create("Lhurgoyf"),
				Components = ImmutableList.Create<GameComponent>(
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
				Subtypes = ImmutableList.Create("Cat", "Wizard"),
				Components = ImmutableList.Create<GameComponent>(
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
				Subtypes = ImmutableList.Create("Spirit"),
				Components = ImmutableList.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 2, Toughness = 4 },
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
									Subtypes = ImmutableList.Create("Angel"),
									Components = ImmutableList.Create<GameComponent>(
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
				.Creature("Wall of Thorns", manaCost: 3, power: 2, toughness: 5)
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
				.Creature("Snapcaster Mage", manaCost: 1, power: 2, toughness: 1)
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
				Subtypes = ImmutableList.Create("Human", "Wizard"),
				Components = ImmutableList.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 1, Toughness = 3 },
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
						OtherFaceSubtypes = ImmutableList.Create("Insect"),
						OtherFaceComponents = ImmutableList.Create<GameComponent>(
							new PermanentComponent(),
							new CreatureComponent
							{
								Power = 4,
								Toughness = 3,
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
			CardFactory.Spell("Lotus Bloom", manaCost: 0).WithAddMana(2).Build(),
			new()
			{
				Name = "Rite of Flame",
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
										new CountCardsWithNameAction
										{
											CardName = "Rite of Flame",
											Zone = ZoneType.Graveyard,
											OutputKey = "rite_count",
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
										},
										new AddTemporaryManaAction
										{
											Amount = 2,
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
			CardFactory.Spell("Seething Song", manaCost: 3).WithAddMana(5).Build(),
			CardFactory
				.Creature("Hunted Dragon", manaCost: 7, power: 8, toughness: 8)
				.WithSubtype(DragonSubtype)
				.WithSubtype("Lizard")
				.WithFlying()
				.WithHaste()
				.Build(),
			CardFactory
				.Creature("Bogardan Hellkite", manaCost: 6, power: 6, toughness: 6)
				.WithSubtype(DragonSubtype)
				.WithFlying()
				.WithTriggeredAbility(
					"ETB Damage",
					TriggerConditions.OnSelfEntersBattlefield(),
					effect: eb =>
						eb.WithDamage(6).WithTarget(Random().OpponentOrOpponentCreatures())
				)
				.Build(),
			new()
			{
				Name = "Dragonstorm",
				ManaCost = 7,
				Components = ImmutableList.Create<GameComponent>(
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
				.Spell("Tendrils of Agony", manaCost: 5)
				.WithStorm()
				.WithLoseLife(2)
				.WithTarget(Single().PlayersOrCreatures())
				.WithLifeGain(2)
				.Build(),
			CardFactory
				.Spell("Past in Flames", manaCost: 5)
				.WithFlashback(7)
				.WithAction(new GiveFlashbackAction(), AllValid().InstantOrSorceryInYourGraveyard())
				.Build(),
			// ===== LAND-ADJACENT CARDS =====
			new()
			{
				Name = "Rampant Growth",
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
				Components = ImmutableList.Create<GameComponent>(
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
				ManaCost = 2,
				Subtypes = ImmutableList.Create("Artifact"),
				Components = ImmutableList.Create<GameComponent>(
					new PermanentComponent(),
					new ExtraLandPerTurnComponent(),
					new TriggeredAbilityComponent
					{
						Name = "ETB Draw",
						Condition = TriggerConditions.OnSelfEntersBattlefieldAsNonCreature(),
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new DrawCardsAction
							{
								Amount = 1,
								TargetContextKey = ContextKeys.CastingPlayerId,
							},
						},
					}
				),
			},
			new()
			{
				Name = "Steppe Lynx",
				ManaCost = 0,
				Components = ImmutableList.Create<GameComponent>(
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
								PowerBonus = 1,
								ToughnessBonus = 1,
								Duration = ModifierDuration.Permanent,
								TargetContextKey = ContextKeys.SourceCardId,
							},
						},
					},
					new TriggeredAbilityComponent
					{
						Name = "Landfall",
						Condition = TriggerConditions.OnLandfall(),
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new AddModifierAction
							{
								PowerBonus = 1,
								ToughnessBonus = 1,
								Duration = ModifierDuration.UntilEndOfTurn,
								TargetContextKey = ContextKeys.SourceCardId,
							},
						},
					}
				),
			},
			new()
			{
				Name = "Land Elemental",
				ManaCost = 3,
				Components = ImmutableList.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 0, Toughness = 0 },
					new LandsPlayedCountComponent { Duration = ModifierDuration.Permanent }
				),
			},
			new()
			{
				Name = "Cultivate",
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
										new SelectCardFromLibraryAction
										{
											Subtype = LandSubtype,
											OutputKey = "cultivate_play",
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
										},
										new PutLandIntoPlayAction
										{
											CardIdContextKey = "cultivate_play",
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
										},
										new SelectCardFromLibraryAction
										{
											Subtype = LandSubtype,
											OutputKey = "cultivate_hand",
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
										},
										new MoveCardToHandAction
										{
											CardIdContextKey = "cultivate_hand",
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
				Name = "Glimmervoid",
				ManaCost = 0,
				Subtypes = ImmutableList.Create(LandSubtype),
				Components = ImmutableList.Create<GameComponent>(
					new LandPlayEffectComponent
					{
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new GainLifeAction
							{
								Amount = 1,
								TargetContextKey = ContextKeys.CastingPlayerId,
							},
						},
					}
				),
			},
			new()
			{
				Name = "Field of the Dead",
				ManaCost = 0,
				Subtypes = ImmutableList.Create(LandSubtype),
				Components = ImmutableList.Create<GameComponent>(
					new GrantEmblemComponent
					{
						Emblem = new Emblem
						{
							Name = "Field of the Dead",
							Condition = new LandsPlayedCondition { Threshold = 9 },
							Effect = new CardEffect
							{
								TargetingStrategy = TargetingStrategy.NoTarget(),
								ActionTemplate = new CreateCardAction
								{
									CardTemplate = ZombieToken(),
									Count = 1,
								},
							},
						},
					}
				),
			},
			new()
			{
				Name = "Bounceland",
				ManaCost = 0,
				Subtypes = ImmutableList.Create(LandSubtype),
				Components = ImmutableList.Create<GameComponent>(
					new BonusManaLandComponent { ExtraMana = 0, Deferred = true },
					new LandPlayEffectComponent
					{
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new SelectCardFromZoneAction
									{
										Zone = ZoneType.Exile,
										Subtype = LandSubtype,
										OutputKey = "bounce_land",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										ExcludeSourceCard = true,
									},
									new MoveCardToHandAction
									{
										CardIdContextKey = "bounce_land",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
						},
					}
				),
			},
			// ===== REANIMATOR CARDS =====
			CardFactory
				.Spell("Reanimate", manaCost: 2)
				.WithAction(new PutIntoBattlefieldAction(), Single().CreatureInYourGraveyard())
				.Build(),
			new()
			{
				Name = "Bloodghast",
				ManaCost = 2,
				Components = ImmutableList.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 1, Toughness = 1 },
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
				Components = ImmutableList.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent
					{
						Power = 10,
						Toughness = 10,
						HasTrample = true,
						HasHexproof = true,
						HasTaunt = true,
					}
				),
			},
			// ===== JUND DECK CARDS =====
			CardFactory
				.Creature("Scavenging Ooze", manaCost: 2, power: 2, toughness: 2)
				.WithActivatedAbility(
					"Scavenge",
					manaCost: 0,
					effect: eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new SelectCardFromZoneAction
									{
										Zone = ZoneType.Graveyard,
										TargetOpponent = true,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "scooze_exile_1",
									},
									new MoveCardToExileAction
									{
										CardIdContextKey = "scooze_exile_1",
									},
									new SelectCardFromZoneAction
									{
										Zone = ZoneType.Graveyard,
										TargetOpponent = true,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "scooze_exile_2",
									},
									new MoveCardToExileAction
									{
										CardIdContextKey = "scooze_exile_2",
									},
									new GainLifeAction
									{
										Amount = 1,
										TargetContextKey = ContextKeys.CastingPlayerId,
									},
									new AddModifierAction
									{
										PowerBonus = 1,
										ToughnessBonus = 1,
										Duration = ModifierDuration.Permanent,
										TargetContextKey = ContextKeys.SourceCardId,
									}
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			new()
			{
				Name = "Thoughtseize",
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
										new SelectCardFromHandByManaCostAction
										{
											SelectLowest = false,
											TargetOpponent = true,
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
											OutputKey = "ts_target",
										},
										new DiscardCardsAction { TargetContextKey = "ts_target" }
									),
								},
							}
						),
					}
				),
			},
			new()
			{
				Name = "Inquisition of Kozilek",
				ManaCost = 0,
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
										new SelectCardFromHandByManaCostAction
										{
											SelectLowest = true,
											TargetOpponent = true,
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
											OutputKey = "iok_target",
										},
										new DiscardCardsAction { TargetContextKey = "iok_target" }
									),
								},
							}
						),
					}
				),
			},
			new()
			{
				Name = "Liliana of the Veil",
				ManaCost = 3,
				Components = ImmutableList.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 3, Toughness = 3 },
					new TriggeredAbilityComponent
					{
						Name = "Liliana ETB",
						Condition = TriggerConditions.OnSelfEntersBattlefield(),
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new SelectCreatureFromBattlefieldByManaCostAction
									{
										SelectLowest = true,
										TargetOpponent = true,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "lili_destroy_target",
									},
									new DestroyCreatureAction
									{
										TargetContextKey = "lili_destroy_target",
									}
								),
							},
						},
					},
					new TriggeredAbilityComponent
					{
						Name = "Liliana Upkeep",
						Condition = new EventTriggerCondition
						{
							EventTypeName = EventTypeNames.TurnStarted,
							Filter = new IsControlledByYouSpecification(),
						},
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new DiscardRandomCardAction
							{
								TargetOpponent = true,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
						},
					}
				),
			},
			new()
			{
				Name = "Siege Rhino",
				ManaCost = 4,
				Components = ImmutableList.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent
					{
						Power = 5,
						Toughness = 5,
						HasTrample = true,
						HasLifelink = true,
					},
					new TriggeredAbilityComponent
					{
						Name = "Siege Rhino ETB",
						Condition = TriggerConditions.OnSelfEntersBattlefield(),
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new DrainLifeAction
							{
								Amount = 3,
								TargetOpponent = true,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
						},
					}
				),
			},
			new()
			{
				Name = "Valakut, the Molten Pinnacle",
				ManaCost = 0,
				Subtypes = ImmutableList.Create(LandSubtype),
				Components = ImmutableList.Create<GameComponent>(
					new GrantEmblemComponent
					{
						Emblem = new Emblem
						{
							Name = "Valakut",
							Condition = new LandsPlayedCondition { Threshold = 8 },
							Effect = new CardEffect
							{
								TargetingStrategy = Random().OpponentOrOpponentCreatures(),
								ActionTemplate = new DealDamageAction { Amount = 2 },
							},
						},
					}
				),
			},
		};

	public static Card GetByName(string name) =>
		All.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
		?? throw new InvalidOperationException($"Card '{name}' not found in CardLibrary.");

	/*
	-static builders that can be accessed from anywhere for ease of use.
	-should we have a fluent builder type syntax? Or something else?
		Spell.WithManaCost(2).WithDestroyCreatureEffect().WithSingleTarget().OpponentCreatures()

		Another way this would allow us to chain multiple effects onto one card.
		Spell.WithManaCost(2).WithEffect(eb=> eb.WithDestroyCreatureEffect().WithSingleTarget().OpponentCreatures())

		Example for lightning bolt
		Spell.WithManaCost(1).WithEffect(ev=> eb.WithDamageEffect().WithSingleTarget().OpponentOrOpponentCreature());

		Or with Ancestrall Recall
		Spell.WithManaCost(1).WithEffect(eb=> eb.WithDrawCardsEffect(3).WithTargetingStrategy(TargetingStrategy.Self());

		This greatly simplifies the card creation process, should make it easier to design and modify cards.

	*/

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
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 2, Toughness = 1 }
			),
		};

	public static Card WrathOfGod() => All.First(c => c.Name == "Wrath of God");

	/// <summary>
	/// Lightning Bolt — 1 mana instant.
	/// "Lightning Bolt deals 3 damage to any target."
	/// </summary>
	public static Card LightningBolt() => All.First(c => c.Name == "Lightning Bolt");

	public static Card Slagstorm() => All.First(c => c.Name == "Slagstorm");

	/// <summary>
	/// Lightning Helix — 2 mana instant.
	/// "Lightning Helix deals 3 damage to any target and you gain 3 life."
	/// </summary>
	public static Card LightningHelix() => All.First(c => c.Name == "Lightning Helix");

	/// <summary>
	/// Careful Study — 1 mana instant.
	/// "Draw 2 cards, then discard 2 cards."
	/// </summary>
	public static Card CarefulStudy() => All.First(c => c.Name == "Careful Study");

	/// <summary>
	/// Telling Time — 2 mana instant.
	/// "Look at the top three cards of your library. Put one into your hand,
	///  one on top of your library, and one on the bottom of your library."
	/// </summary>
	public static Card TellingTime() => All.First(c => c.Name == "Telling Time");

	/// <summary>
	/// Dark Confidant — 2 mana creature (2/1).
	/// "At the beginning of your upkeep, reveal the top card of your library
	///  and put it into your hand. You lose life equal to its mana cost."
	/// Triggered ability not yet implemented — see TriggeredAbilityComponent (future).
	/// </summary>
	public static Card DarkConfidant() => All.First(c => c.Name == "Dark Confidant");

	/// <summary>
	/// Prodigal Sorcerer — 3 mana creature (1/1).
	/// Activated ability: "1 mana: Deal 1 damage to any target."
	/// Classic example of a simple damage ping ability.
	/// </summary>
	public static Card ProdigalSorcerer() => All.First(c => c.Name == "Prodigal Sorcerer");

	/// <summary>
	/// Throne of Bone — 1 mana artifact creature (1/1).
	/// Activated ability: "1 mana: Gain 2 life."
	/// Activated ability: "2 mana: Draw a card."
	/// Simple card with two abilities to exercise the multi-ability path.
	/// </summary>
	public static Card ThroneOfBone() => All.First(c => c.Name == "Throne of Bone");

	/// <summary>
	/// Giant Growth — 1 mana instant.
	/// "Target creature gets +3/+3 until end of turn."
	/// Classic combat trick — applies a UntilEndOfTurn PowerToughnessModifier.
	/// </summary>
	public static Card GiantGrowth() => All.First(c => c.Name == "Giant Growth");

	/// <summary>
	/// Unholy Strength — 1 mana instant.
	/// "Target creature gets +2/+1 permanently."
	/// Simplified enchantment-style permanent buff using a Permanent modifier.
	/// </summary>
	public static Card UnholyStrength() => All.First(c => c.Name == "Unholy Strength");

	// ===== ARTIFACTS =====

	/// <summary>
	/// Mox (generic) — 0 mana artifact.
	/// Once per turn: add 1 mana. Tap is proxied as HasActivated — no tap cost implemented yet.
	/// </summary>
	public static Card Mox() => All.First(c => c.Name == "Mox");

	/// <summary>
	/// Sol Ring — 1 mana artifact.
	/// Once per turn: add 2 mana. Tap is proxied as HasActivated — no tap cost implemented yet.
	/// </summary>
	public static Card SolRing() => All.First(c => c.Name == "Sol Ring");

	// ===== ENCHANTMENTS =====

	/// <summary>
	/// Glorious Anthem — 3 mana enchantment.
	/// "Creatures you control get +1/+1."
	/// Global static boost applied via StaticAbilityEngine push model.
	/// </summary>
	public static Card GloriousAnthem() => All.First(c => c.Name == "Glorious Anthem");

	/// <summary>
	/// Phyrexian Arena — 3 mana enchantment.
	/// "At the beginning of your upkeep, you draw a card and you lose 1 life."
	/// Triggered on TurnStarted; draws one card and costs one life each turn.
	/// </summary>
	public static Card PhyrexianArena() => All.First(c => c.Name == "Phyrexian Arena");

	// ===== EQUIPMENT =====

	public static Card Bonesplitter() => All.First(c => c.Name == "Bonesplitter");

	// ===== GOBLINS DECK CARDS =====

	/// <summary>
	/// Goblin Token — 1/1 creature token.
	/// Created by Siege-Gang Commander and Krenko, Mob Boss.
	/// OwnerId/ControllerId default to 0 and are stamped by CreateCardAction at runtime.
	/// </summary>
	public static Card GoblinToken() =>
		new()
		{
			Name = "Goblin",
			Subtypes = ImmutableList.Create(GoblinSubtype),
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 1, Toughness = 1 }
			),
		};

	public static Card ZombieToken() =>
		new()
		{
			Name = "Zombie",
			Subtypes = ImmutableList.Create("Zombie"),
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 2, Toughness = 2 }
			),
		};

	/// <summary>
	/// Goblin Guide — 1 mana creature (2/2, Haste).
	/// Goblin Scout. "Whenever Goblin Guide attacks, defending player reveals the
	/// top card of their library." — Reveal clause omitted; just a 2/2 haste for 1.
	/// </summary>
	public static Card GoblinGuide() => All.First(c => c.Name == "Goblin Guide");

	/// <summary>
	/// Goblin Lackey — 1 mana creature (1/1).
	/// "Whenever Goblin Lackey deals combat damage to a player, you may put a Goblin
	///  permanent card from your hand onto the battlefield."
	/// </summary>
	public static Card GoblinLackey() => All.First(c => c.Name == "Goblin Lackey");

	/// <summary>
	/// Warren Instigator — 2 mana creature (1/1, Double Strike).
	/// "Whenever Warren Instigator deals combat damage to a player, you may put a Goblin
	///  permanent card from your hand onto the battlefield." Fires twice (double strike).
	/// </summary>
	public static Card WarrenInstigator() => All.First(c => c.Name == "Warren Instigator");

	/// <summary>
	/// Goblin Chieftain — 3 mana creature (2/2, Haste).
	/// Lord effect ("other Goblins get +1/+1 and haste") deferred until static anthems
	/// are implemented (Step 2). For now: aggressive 2/2 haste body.
	/// </summary>
	public static Card GoblinChieftain() => All.First(c => c.Name == "Goblin Chieftain");

	/// <summary>
	/// Siege-Gang Commander — 5 mana creature (2/2).
	/// ETB: create three 1/1 Goblin creature tokens.
	/// Activated: 1 mana, sacrifice a Goblin → deal 2 damage to any target.
	/// </summary>
	public static Card SiegeGangCommander() => All.First(c => c.Name == "Siege-Gang Commander");

	/// <summary>
	/// Krenko, Mob Boss — 4 mana creature (3/3).
	/// Activated (tap proxy — no tap cost implemented): create X 1/1 Goblin tokens,
	/// where X is the number of Goblins you control. Uses pipeline to count at resolution.
	/// </summary>
	public static Card KrenkoMobBoss() => All.First(c => c.Name == "Krenko, Mob Boss");

	/// <summary>
	/// Goblin Grenade — 1 mana sorcery.
	/// Additional cost: sacrifice a Goblin.
	/// "Goblin Grenade deals 5 damage to any target."
	/// </summary>
	public static Card GoblinGrenade() => All.First(c => c.Name == "Goblin Grenade");

	/// <summary>
	/// Mogg Warmaster — 1 mana creature (1/1, Goblin).
	/// ETB: create a 1/1 Goblin token.
	/// When it dies: create a 1/1 Goblin token.
	/// </summary>
	public static Card MoggWarmaster() => All.First(c => c.Name == "Mogg Warmaster");

	/// <summary>
	/// Goblin Matron — 2 mana creature (1/1, Goblin).
	/// ETB: search library for a random Goblin and put it into your hand.
	/// </summary>
	public static Card GoblinMatron() => All.First(c => c.Name == "Goblin Matron");

	/// <summary>
	/// Goblin Ringleader — 3 mana creature (2/2, Goblin).
	/// ETB: find up to 3 random Goblins from library and put them into your hand.
	/// </summary>
	public static Card GoblinRingleader() => All.First(c => c.Name == "Goblin Ringleader");

	// ===== ZOO DECK CARDS =====

	/// <summary>
	/// Wild Nacatl — 1 mana creature (2/2).
	/// Cat Warrior. Simplified: no domain condition, just solid stats.
	/// </summary>
	public static Card WildNacatl() => All.First(c => c.Name == "Wild Nacatl");

	/// <summary>
	/// Kird Ape — 1 mana creature (2/3).
	/// Ape. Simplified: no Forest condition, just solid stats.
	/// </summary>
	public static Card KirdApe() => All.First(c => c.Name == "Kird Ape");

	/// <summary>
	/// Tarmogoyf — 2 mana creature (*/1+*).
	/// Power and toughness each scale with the total number of cards in all graveyards.
	/// Base Power = 0, Base Toughness = 1; GraveyardCountComponent adds the dynamic bonus.
	/// </summary>
	public static Card Tarmogoyf() => All.First(c => c.Name == "Tarmogoyf");

	public static Card LlanowarElves() => All.First(c => c.Name == "Llanowar Elves");

	/// <summary>
	/// Path to Exile — 1 mana instant.
	/// "Exile target creature."
	/// Simplified: no basic land search for the exiled creature's controller.
	/// </summary>
	public static Card PathToExile() => All.First(c => c.Name == "Path to Exile");

	/// <summary>
	/// Tribal Flames — 2 mana instant.
	/// "Tribal Flames deals 5 damage to any target."
	/// Simplified: fixed 5 damage, ignores domain condition.
	/// </summary>
	public static Card TribalFlames() => All.First(c => c.Name == "Tribal Flames");

	/// <summary>
	/// Qasali Pridemage — 2 mana creature (2/2).
	/// Cat Wizard. Activated ability: destroy target opponent's creature (proxy for
	/// the real card's sac-to-destroy-artifact/enchantment — no artifact type yet).
	/// </summary>
	public static Card QasaliPridemage() => All.First(c => c.Name == "Qasali Pridemage");

	public static Card WallOfThorns() => All.First(c => c.Name == "Wall of Thorns");

	public static Card GeistOfSaintTraft() => All.First(c => c.Name == "Geist of Saint Traft");

	/// <summary>
	/// Loam Lion — 1 mana creature (2/3).
	/// Cat. Simplified: no Forest condition, just good defensive stats.
	/// </summary>
	public static Card LoamLion() => All.First(c => c.Name == "Loam Lion");

	// ===== DRAGONSTORM DECK CARDS =====

	/// <summary>
	/// Sleight of Hand — 1 mana instant.
	/// "Look at the top two cards of your library. Put one into your hand
	///  and the other on the bottom of your library."
	/// </summary>
	public static Card SleightOfHand() => All.First(c => c.Name == "Sleight of Hand");

	/// <summary>
	/// Lotus Bloom — 0 mana sorcery (simplified from Suspend 3).
	/// "Add RRR." Suspend mechanic omitted — treated as a free mana spell.
	/// </summary>
	public static Card LotusBoom() => All.First(c => c.Name == "Lotus Bloom");

	/// <summary>
	/// Rite of Flame — 1 mana instant.
	/// "Add RR. Add an additional R for each card named Rite of Flame in your graveyard."
	/// </summary>
	public static Card RiteOfFlame() => All.First(c => c.Name == "Rite of Flame");

	/// <summary>
	/// Seething Song — 3 mana instant.
	/// "Add RRRRR." Net +2 mana at sorcery speed — fuels same-turn Dragonstorm.
	/// </summary>
	public static Card SeethingSong() => All.First(c => c.Name == "Seething Song");

	/// <summary>
	/// Hunted Dragon — 6 mana creature (6/6, Flying, Haste).
	/// Simplified: Knight token ETB omitted.
	/// </summary>
	public static Card HuntedDragon() => All.First(c => c.Name == "Hunted Dragon");

	/// <summary>
	/// Bogardan Hellkite — 8 mana creature (5/5, Flying).
	/// "When Bogardan Hellkite enters the battlefield, it deals 5 damage to target
	///  player or creature." Simplified: single random opponent target.
	/// </summary>
	public static Card BogardanHellkite() => All.First(c => c.Name == "Bogardan Hellkite");

	/// <summary>
	/// Dragonstorm — 9 mana sorcery with Storm.
	/// "Search your library for a Dragon permanent card and put it onto the battlefield.
	///  Storm — copy this spell for each spell cast before it this turn."
	/// HasStorm=true causes ResolveSpellAction to repeat the effect SpellsCastThisTurn times.
	/// Each copy: SelectCardFromLibraryAction finds the next Dragon, PutIntoBattlefieldAction deploys it.
	/// </summary>
	public static Card Dragonstorm() => All.First(c => c.Name == "Dragonstorm");

	// ===== LAND =====

	/// <summary>
	/// Plains — basic land.
	/// Playing a land from hand permanently increases MaxMana and CurrentMana by 1.
	/// Behavior is handled entirely by PlayLandAction; the card has no components.
	/// </summary>
	public static Card Plains() =>
		new()
		{
			Name = "Plains",
			ManaCost = 0,
			Subtypes = ImmutableList.Create(LandSubtype, "Basic"),
			Components = ImmutableList<GameComponent>.Empty,
		};

	/// <summary>
	/// Valakut, the Molten Pinnacle — special land.
	/// Grants the player a Valakut emblem when played. The emblem deals 3 damage
	/// to a random opponent or opponent creature each time the player plays a land
	/// while LandsPlayedTotal >= 7 (fires from the 7th land onward).
	/// </summary>
	public static Card Valakut() => GetByName("Valakut, the Molten Pinnacle");

	// ===== LAND-ADJACENT CARDS =====

	/// <summary>
	/// Rampant Growth — 2 mana sorcery.
	/// "Search your library for a basic land card and put it into play."
	/// Pipeline: SelectCardFromLibraryAction (subtype Land) → PutLandIntoPlayAction.
	/// </summary>
	public static Card RampantGrowth() => All.First(c => c.Name == "Rampant Growth");

	/// <summary>
	/// Primeval Titan — 6 mana 6/6.
	/// "When Primeval Titan enters the battlefield, search your library for up to two
	///  basic land cards and put them into play."
	/// ETB trigger: pipeline runs twice (SelectCardFromLibraryAction → PutLandIntoPlayAction).
	/// </summary>
	public static Card PrimevalTitan() => All.First(c => c.Name == "Primeval Titan");

	/// <summary>
	/// Exploration — 1 mana artifact.
	/// "You may play an additional land on each of your turns."
	/// ExtraLandPerTurnComponent on the battlefield grants +1 to the land-per-turn limit.
	/// </summary>
	public static Card Exploration() => All.First(c => c.Name == "Exploration");

	/// <summary>
	/// Steppe Lynx — 0 mana 0/1.
	/// "Landfall — Whenever a land enters play under your control, Steppe Lynx gets
	///  +2/+2 until end of turn."
	/// TargetContextKey = SourceCardId applies the modifier to the Lynx itself at resolution.
	/// </summary>
	public static Card SteppeLynx() => All.First(c => c.Name == "Steppe Lynx");

	/// <summary>
	/// Land Elemental — 3 mana creature (0/0 base).
	/// "Land Elemental's power and toughness are each equal to the number of lands
	///  you have played this game."
	/// LandsPlayedCountComponent reads the controller's LandsPlayedTotal dynamically.
	/// Duration = Permanent so StartTurnAction does not clear it.
	/// </summary>
	public static Card LandElemental() => All.First(c => c.Name == "Land Elemental");

	// ===== JUND DECK CARDS =====

	/// <summary>
	/// Scavenging Ooze — 2 mana creature (2/2).
	/// Activated (0 cost, once per turn): exile 2 random cards from opponent's graveyard,
	/// gain 1 life, and get +1/+1 permanently. No-ops gracefully when opponent graveyard is small.
	/// </summary>
	public static Card ScavengingOoze() => All.First(c => c.Name == "Scavenging Ooze");

	// ===== REANIMATOR CARDS =====

	/// <summary>
	/// Reanimate — 1 mana sorcery.
	/// "Return target creature card from your graveyard to the battlefield."
	/// </summary>
	public static Card Reanimate() => All.First(c => c.Name == "Reanimate");
}
