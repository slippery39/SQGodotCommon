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
			LightningBolt(),
			LightningHelix(),
			CarefulStudy(),
			TellingTime(),
			DarkConfidant(),
			LlanowarElves(),
			DoomBlade(),
			WrathOfGod(),
			Mox(),
			SolRing(),
			GloriousAnthem(),
			PhyrexianArena(),
			Bonesplitter(),
			ProdigalSorcerer(),
			ThroneOfBone(),
			GiantGrowth(),
			UnholyStrength(),
			GoblinGuide(),
			GoblinLackey(),
			WarrenInstigator(),
			GoblinChieftain(),
			SiegeGangCommander(),
			KrenkoMobBoss(),
			GoblinGrenade(),
			WildNacatl(),
			KirdApe(),
			Tarmogoyf(),
			PathToExile(),
			TribalFlames(),
			QasaliPridemage(),
			LoamLion(),
			Slagstorm(),
			GeistOfSaintTraft(),
			WallOfThorns(),
			RagingGoblin(),
			HillGiant(),
			GrizzlyBears(),
			KalonianTusker(),
			IronGolem(),
			CrawWurm(),
			AncestralRecall(),
			MahamotiDjinn(),
			// ===== DRAGONSTORM DECK CARDS =====
			SleightOfHand(),
			LotusBoom(),
			RiteOfFlame(),
			SeethingSong(),
			HuntedDragon(),
			BogardanHellkite(),
			Dragonstorm(),
			// ===== LAND-ADJACENT CARDS =====
			RampantGrowth(),
			PrimevalTitan(),
			Exploration(),
			SteppeLynx(),
			LandElemental(),
		};

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
	public static Card DoomBlade() =>
		new()
		{
			Name = "Doom Blade",
			ManaCost = 2,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.SingleTarget(
								TargetSpecification.OpponentCreatures()
							),
							ActionTemplate = new DestroyCreatureAction(),
						}
					),
				}
			),
		};

	public static Card GrizzlyBears() =>
		new()
		{
			Name = "Grizzly Bears",
			ManaCost = 2,
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 2, Toughness = 2 }
			),
		};

	public static Card HillGiant() =>
		new()
		{
			Name = "Hill Giant",
			ManaCost = 3,
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 3, Toughness = 4 }
			),
		};

	public static Card KalonianTusker() =>
		new()
		{
			Name = "Kalonian Tusker",
			ManaCost = 2,
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 3, Toughness = 3 }
			),
		};

	public static Card IronGolem() =>
		new()
		{
			Name = "Iron Golem",
			ManaCost = 4,
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 5, Toughness = 5 }
			),
			Subtypes = ImmutableList.Create("Golem"),
		};

	public static Card MahamotiDjinn() =>
		new()
		{
			Name = "Mahamoti Djinn",
			ManaCost = 6,
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = 6,
					Toughness = 7,
					HasFlying = true,
				}
			),
			Subtypes = ImmutableList.Create("Djinn"),
		};

	public static Card CrawWurm() =>
		new()
		{
			Name = "Craw Wurm",
			ManaCost = 6,
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 8, Toughness = 4 }
			),
			Subtypes = ImmutableList.Create("Wurm"),
		};

	public static Card AncestralRecall() =>
		new()
		{
			Name = "Ancestral Recall",
			ManaCost = 1,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.Self(),
							ActionTemplate = new DrawCardsAction { Amount = 3 },
						}
					),
				}
			),
		};

	public static Card RagingGoblin() =>
		new()
		{
			Name = "Raging Goblin",
			ManaCost = 1,
			Subtypes = ImmutableList.Create(GoblinSubtype),
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = 1,
					Toughness = 1,
					HasHaste = true,
				}
			),
		};

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

	public static Card WrathOfGod() =>
		new()
		{
			Name = "Wrath of God",
			ManaCost = 3,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.AllValid(
								TargetSpecification.Creatures()
							),
							ActionTemplate = new DestroyCreatureAction(),
						}
					),
				}
			),
		};

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
								TargetSpecification.PlayersOrCreatures()
							),
							ActionTemplate = new DealDamageAction { Amount = 3 },
						}
					),
				}
			),
		};

	public static Card Slagstorm() =>
		new()
		{
			Name = "Slagstorm",
			ManaCost = 3,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.AllValid(
								TargetSpecification.PlayersOrCreatures()
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
								TargetSpecification.PlayersOrCreatures()
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
										TargetContextKey = ContextKeys.CastingPlayerId,
									},
									//Selecting cards and discarding could created from some sort of factory method or builder which would
									//abstract these details of creating this specific type of effect. And we could also, make a factory
									//method for Draw and Discard specifically which would combine all these effects in one for easy use.
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
				new PermanentComponent(),
				new CreatureComponent { Power = 1, Toughness = 1 },
				new ActivatedAbilityComponent
				{
					Name = "Ping",
					ManaCost = 1,
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.SingleTarget(
							TargetSpecification.PlayersOrCreatures()
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
				new PermanentComponent(),
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
						TargetingStrategy = TargetingStrategy.Self(),
						ActionTemplate = new DrawCardsAction { Amount = 1 },
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
								TargetSpecification.CreatureControlledByYou()
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

	// ===== ARTIFACTS =====

	/// <summary>
	/// Mox (generic) — 0 mana artifact.
	/// Once per turn: add 1 mana. Tap is proxied as HasActivated — no tap cost implemented yet.
	/// </summary>
	public static Card Mox() =>
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
		};

	/// <summary>
	/// Sol Ring — 1 mana artifact.
	/// Once per turn: add 2 mana. Tap is proxied as HasActivated — no tap cost implemented yet.
	/// </summary>
	public static Card SolRing() =>
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
		};

	// ===== ENCHANTMENTS =====

	/// <summary>
	/// Glorious Anthem — 3 mana enchantment.
	/// "Creatures you control get +1/+1."
	/// Global static boost applied via StaticAbilityEngine push model.
	/// </summary>
	public static Card GloriousAnthem() =>
		new()
		{
			Name = "Glorious Anthem",
			ManaCost = 3,
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
		};

	/// <summary>
	/// Phyrexian Arena — 3 mana enchantment.
	/// "At the beginning of your upkeep, you draw a card and you lose 1 life."
	/// Triggered on TurnStarted; draws one card and costs one life each turn.
	/// </summary>
	public static Card PhyrexianArena() =>
		new()
		{
			Name = "Phyrexian Arena",
			ManaCost = 3,
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
		};

	// ===== EQUIPMENT =====

	public static Card Bonesplitter() =>
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
		};

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
				new PermanentComponent(),
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
				new PermanentComponent(),
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
						ActionTemplate = new CreateCardAction
						{
							CardTemplate = GoblinToken(),
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
							TargetSpecification.PlayersOrCreatures()
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
								TargetSpecification.PlayersOrCreatures()
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
				new PermanentComponent(),
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
				new PermanentComponent(),
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
				new PermanentComponent(),
				new CreatureComponent { Power = 0, Toughness = 1 },
				new GraveyardCountComponent { Duration = ModifierDuration.Permanent }
			),
		};

	public static Card LlanowarElves() =>
		new()
		{
			Name = "Llanowar Elves",
			ManaCost = 1,
			Subtypes = ImmutableList.Create("Elf", "Druid"),
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 1, Toughness = 1 },
				new ActivatedAbilityComponent
				{
					Name = "Mana Ramp",
					ManaCost = 0,
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.Self(),
						ActionTemplate = new AddTemporaryManaAction { Amount = 1 },
					},
				}
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
								TargetSpecification.OpponentCreatures()
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
								TargetSpecification.PlayersOrCreatures()
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
		};

	public static Card WallOfThorns() =>
		new()
		{
			Name = "Wall of Thorns",
			ManaCost = 3,
			Subtypes = ImmutableList.Create("Plant"),
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = 2,
					Toughness = 5,
					HasTaunt = true,
				}
			),
		};

	public static Card GeistOfSaintTraft() =>
		new()
		{
			Name = "Geist of Saint Traft",
			ManaCost = 3,
			Subtypes = ImmutableList.Create("Spirit"),
			Components = ImmutableList.Create<GameComponent>(
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
				new PermanentComponent(),
				new CreatureComponent { Power = 2, Toughness = 3 }
			),
		};

	// ===== DRAGONSTORM DECK CARDS =====

	/// <summary>
	/// Sleight of Hand — 1 mana instant.
	/// "Look at the top two cards of your library. Put one into your hand
	///  and the other on the bottom of your library."
	/// </summary>
	public static Card SleightOfHand() =>
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
										ExcludeContextKeys = ImmutableList.Create("soh_hand_pick"),
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
	/// Lotus Bloom — 0 mana sorcery (simplified from Suspend 3).
	/// "Add RRR." Suspend mechanic omitted — treated as a free mana spell.
	/// </summary>
	public static Card LotusBoom() =>
		new()
		{
			Name = "Lotus Bloom",
			ManaCost = 0,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.Self(),
							ActionTemplate = new AddTemporaryManaAction { Amount = 3 },
						}
					),
				}
			),
		};

	/// <summary>
	/// Rite of Flame — 1 mana instant.
	/// "Add RR. Add an additional R for each card named Rite of Flame in your graveyard."
	/// </summary>
	public static Card RiteOfFlame() =>
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
		};

	/// <summary>
	/// Seething Song — 3 mana instant.
	/// "Add RRRRR." Net +2 mana at sorcery speed — fuels same-turn Dragonstorm.
	/// </summary>
	public static Card SeethingSong() =>
		new()
		{
			Name = "Seething Song",
			ManaCost = 3,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.Self(),
							ActionTemplate = new AddTemporaryManaAction { Amount = 6 },
						}
					),
				}
			),
		};

	/// <summary>
	/// Hunted Dragon — 6 mana creature (6/6, Flying, Haste).
	/// Simplified: Knight token ETB omitted.
	/// </summary>
	public static Card HuntedDragon() =>
		new()
		{
			Name = "Hunted Dragon",
			ManaCost = 10,
			Subtypes = ImmutableList.Create(DragonSubtype, "Lizard"),
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = 10,
					Toughness = 10,
					HasFlying = true,
					HasHaste = true,
				}
			),
		};

	/// <summary>
	/// Bogardan Hellkite — 8 mana creature (5/5, Flying).
	/// "When Bogardan Hellkite enters the battlefield, it deals 5 damage to target
	///  player or creature." Simplified: single random opponent target.
	/// </summary>
	public static Card BogardanHellkite() =>
		new()
		{
			Name = "Bogardan Hellkite",
			ManaCost = 8,
			Subtypes = ImmutableList.Create(DragonSubtype),
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = 5,
					Toughness = 5,
					HasFlying = true,
				},
				new TriggeredAbilityComponent
				{
					Name = "ETB Damage",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsSourceCardSpecification(),
					},
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.RandomTarget(
							TargetSpecification.OpponentOrOpponentCreatures()
						),
						ActionTemplate = new DealDamageAction { Amount = 5 },
					},
				}
			),
		};

	/// <summary>
	/// Dragonstorm — 9 mana sorcery with Storm.
	/// "Search your library for a Dragon permanent card and put it onto the battlefield.
	///  Storm — copy this spell for each spell cast before it this turn."
	/// HasStorm=true causes ResolveSpellAction to repeat the effect SpellsCastThisTurn times.
	/// Each copy: SelectCardFromLibraryAction finds the next Dragon, PutIntoBattlefieldAction deploys it.
	/// </summary>
	public static Card Dragonstorm() =>
		new()
		{
			Name = "Dragonstorm",
			ManaCost = 9,
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
		};

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

	// ===== LAND-ADJACENT CARDS =====

	/// <summary>
	/// Rampant Growth — 2 mana sorcery.
	/// "Search your library for a basic land card and put it into play."
	/// Pipeline: SelectCardFromLibraryAction (subtype Land) → PutLandIntoPlayAction.
	/// </summary>
	public static Card RampantGrowth() =>
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
		};

	/// <summary>
	/// Primeval Titan — 6 mana 6/6.
	/// "When Primeval Titan enters the battlefield, search your library for up to two
	///  basic land cards and put them into play."
	/// ETB trigger: pipeline runs twice (SelectCardFromLibraryAction → PutLandIntoPlayAction).
	/// </summary>
	public static Card PrimevalTitan() =>
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
		};

	/// <summary>
	/// Exploration — 1 mana artifact.
	/// "You may play an additional land on each of your turns."
	/// ExtraLandPerTurnComponent on the battlefield grants +1 to the land-per-turn limit.
	/// </summary>
	public static Card Exploration() =>
		new()
		{
			Name = "Exploration",
			ManaCost = 1,
			Subtypes = ImmutableList.Create("Artifact"),
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new ExtraLandPerTurnComponent()
			),
		};

	/// <summary>
	/// Steppe Lynx — 0 mana 0/1.
	/// "Landfall — Whenever a land enters play under your control, Steppe Lynx gets
	///  +2/+2 until end of turn."
	/// TargetContextKey = SourceCardId applies the modifier to the Lynx itself at resolution.
	/// </summary>
	public static Card SteppeLynx() =>
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
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.LandPlayed,
						Filter = new IsControlledByYouSpecification(),
					},
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
		};

	/// <summary>
	/// Land Elemental — 3 mana creature (0/0 base).
	/// "Land Elemental's power and toughness are each equal to the number of lands
	///  you have played this game."
	/// LandsPlayedCountComponent reads the controller's LandsPlayedTotal dynamically.
	/// Duration = Permanent so StartTurnAction does not clear it.
	/// </summary>
	public static Card LandElemental() =>
		new()
		{
			Name = "Land Elemental",
			ManaCost = 3,
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 0, Toughness = 0 },
				new LandsPlayedCountComponent { Duration = ModifierDuration.Permanent }
			),
		};
}
