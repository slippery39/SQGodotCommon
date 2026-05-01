using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Defines the simulator's built-in pool of cards available for random deck generation.
///
/// Damage spells target opponents and opponent creatures only — the random AI
/// has no targeting intelligence so we restrict valid targets at the card level
/// rather than teaching the AI to distinguish friendly from hostile.
/// </summary>
public static class CardPool
{
	public static IReadOnlyList<Card> All { get; } =
		new List<Card>
		{
			// ===== CHEAP AGGRESSIVE CREATURES =====
			MakeCreature("Goblin Raider", cost: 1, power: 2, toughness: 1),
			MakeCreature("Jackal Pup", cost: 1, power: 2, toughness: 1),
			MakeCreature("Savannah Lions", cost: 1, power: 2, toughness: 1),
			MakeCreature("Raging Goblin", cost: 1, power: 1, toughness: 1),
			MakeCreature("Llanowar Elves", cost: 1, power: 1, toughness: 1),
			// ===== MIDRANGE CREATURES =====
			MakeCreature("Grizzly Bears", cost: 2, power: 2, toughness: 2),
			MakeCreature("Runeclaw Bear", cost: 2, power: 2, toughness: 2),
			MakeCreature("Elvish Warrior", cost: 2, power: 2, toughness: 3),
			MakeCreature("Centaur Courser", cost: 3, power: 3, toughness: 3),
			MakeCreature("Hill Giant", cost: 3, power: 3, toughness: 4),
			MakeCreature("Bladetusk Boar", cost: 4, power: 3, toughness: 3),
			MakeCreature("Kalonian Tusker", cost: 3, power: 3, toughness: 3),
			MakeCreature("Wind Drake", cost: 3, power: 2, toughness: 2),
			MakeCreature("Wall of Stone", cost: 3, power: 0, toughness: 8),
			MakeCreature("Iron Golem", cost: 4, power: 4, toughness: 4),
			// ===== BIG CREATURES =====
			MakeCreature("Serra Angel", cost: 5, power: 4, toughness: 4),
			MakeCreature("Mahamoti Djinn", cost: 6, power: 5, toughness: 6),
			MakeCreature("Craw Wurm", cost: 6, power: 6, toughness: 4),
			MakeCreature("Ancient Ooze", cost: 7, power: 6, toughness: 6),
			MakeCreature("Leviathan", cost: 9, power: 10, toughness: 10),
			// ===== REMOVAL SPELLS =====
			MakeSpell(
				"Shock",
				cost: 1,
				TargetingStrategy.SingleTarget(
					new IsPlayerSpecification()
						.And(new IsControlledByOpponentSpecification())
						.Or(
							new IsCreatureSpecification().And(
								new IsControlledByOpponentSpecification()
							)
						)
				),
				new DealDamageAction { Amount = 2 }
			),
			MakeSpell(
				"Volcanic Hammer",
				cost: 2,
				TargetingStrategy.SingleTarget(
					new IsPlayerSpecification()
						.And(new IsControlledByOpponentSpecification())
						.Or(
							new IsCreatureSpecification().And(
								new IsControlledByOpponentSpecification()
							)
						)
				),
				new DealDamageAction { Amount = 3 }
			),
			MakeSpell(
				"Searing Spear",
				cost: 2,
				TargetingStrategy.SingleTarget(
					new IsPlayerSpecification()
						.And(new IsControlledByOpponentSpecification())
						.Or(
							new IsCreatureSpecification().And(
								new IsControlledByOpponentSpecification()
							)
						)
				),
				new DealDamageAction { Amount = 3 }
			),
			MakeSpell(
				"Incinerate",
				cost: 2,
				TargetingStrategy.SingleTarget(
					new IsPlayerSpecification()
						.And(new IsControlledByOpponentSpecification())
						.Or(
							new IsCreatureSpecification().And(
								new IsControlledByOpponentSpecification()
							)
						)
				),
				new DealDamageAction { Amount = 3 }
			),
			// ===== BURN SPELLS =====
			MakeSpell(
				"Lava Spike",
				cost: 1,
				TargetingStrategy.SingleTarget(
					new IsPlayerSpecification().And(new IsControlledByOpponentSpecification())
				),
				new DealDamageAction { Amount = 3 }
			),
			MakeSpell(
				"Rift Bolt",
				cost: 2,
				TargetingStrategy.SingleTarget(
					new IsPlayerSpecification().And(new IsControlledByOpponentSpecification())
				),
				new DealDamageAction { Amount = 3 }
			),
			// ===== UTILITY SPELLS =====
			MakeSpell(
				"Healing Salve",
				cost: 1,
				TargetingStrategy.Self(),
				new GainLifeAction { Amount = 3 }
			),
			MakeSpell(
				"Revitalize",
				cost: 2,
				TargetingStrategy.Self(),
				new GainLifeAction { Amount = 3 }
			),
			MakeSpell(
				"Inspiration",
				cost: 4,
				TargetingStrategy.NoTarget(),
				new DrawCardsAction { Amount = 2, PlayerIdContextKey = ContextKeys.CastingPlayerId }
			),
			MakeSpell(
				"Counsel of the Soratami",
				cost: 3,
				TargetingStrategy.NoTarget(),
				new DrawCardsAction { Amount = 2, PlayerIdContextKey = ContextKeys.CastingPlayerId }
			),
			MakeSpell(
				"Ancestral Recall",
				cost: 0,
				TargetingStrategy.NoTarget(),
				new DrawCardsAction { Amount = 3, PlayerIdContextKey = ContextKeys.CastingPlayerId }
			),
			// ===== CREATURES WITH ACTIVATED ABILITIES =====

			// Prodigal Sorcerer — 3 mana 1/1. "1 mana: Deal 1 damage to any opponent target."
			MakeAbilityCreature(
				"Prodigal Sorcerer",
				cost: 3,
				power: 1,
				toughness: 1,
				new ActivatedAbilityComponent
				{
					Name = "Ping",
					ManaCost = 1,
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.SingleTarget(
							new IsPlayerSpecification()
								.And(new IsControlledByOpponentSpecification())
								.Or(
									new IsCreatureSpecification().And(
										new IsControlledByOpponentSpecification()
									)
								)
						),
						ActionTemplate = new DealDamageAction { Amount = 1 },
					},
				}
			),
			// Drudge Skeletons — 2 mana 1/1. "1 mana: Gain 2 life."
			MakeAbilityCreature(
				"Drudge Skeletons",
				cost: 2,
				power: 1,
				toughness: 1,
				new ActivatedAbilityComponent
				{
					Name = "Drain Life",
					ManaCost = 1,
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.Self(),
						ActionTemplate = new GainLifeAction { Amount = 2 },
					},
				}
			),
			// Wizard Mentor — 3 mana 2/2. "2 mana: Draw a card."
			MakeAbilityCreature(
				"Wizard Mentor",
				cost: 3,
				power: 2,
				toughness: 2,
				new ActivatedAbilityComponent
				{
					Name = "Study",
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
			// Spikeshot Goblin — 3 mana 1/1. Two abilities: ping and drain.
			MakeAbilityCreature(
				"Spikeshot Goblin",
				cost: 3,
				power: 1,
				toughness: 1,
				new ActivatedAbilityComponent
				{
					Name = "Spike",
					ManaCost = 1,
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.SingleTarget(
							new IsPlayerSpecification()
								.And(new IsControlledByOpponentSpecification())
								.Or(
									new IsCreatureSpecification().And(
										new IsControlledByOpponentSpecification()
									)
								)
						),
						ActionTemplate = new DealDamageAction { Amount = 1 },
					},
				},
				new ActivatedAbilityComponent
				{
					Name = "Drain",
					ManaCost = 2,
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.Self(),
						ActionTemplate = new GainLifeAction { Amount = 2 },
					},
				}
			),
			// ===== CREATURES WITH TRIGGERED ABILITIES =====

			// Grim Initiate — 2 mana 2/1.
			// "When a creature you control dies, gain 1 life."
			MakeTriggerCreature(
				"Grim Initiate",
				cost: 2,
				power: 2,
				toughness: 1,
				new TriggeredAbilityComponent
				{
					Name = "Death Rites",
					Condition = new CreatureDiesCondition { OnlyYourCreatures = true },
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.Self(),
						ActionTemplate = new GainLifeAction { Amount = 1 },
					},
				}
			),
			// Blood Artist — 3 mana 0/1.
			// "When any creature dies, deal 1 damage to the opponent."
			MakeTriggerCreature(
				"Blood Artist",
				cost: 3,
				power: 0,
				toughness: 1,
				new TriggeredAbilityComponent
				{
					Name = "Blood Drain",
					Condition = new CreatureDiesCondition(),
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.AllValid(
							new IsPlayerSpecification().And(
								new IsControlledByOpponentSpecification()
							)
						),
						ActionTemplate = new DealDamageAction { Amount = 1 },
					},
				}
			),
			// Reconnaissance — 2 mana 1/2.
			// "Whenever a creature attacks, gain 1 life."
			MakeTriggerCreature(
				"Reconnaissance",
				cost: 2,
				power: 1,
				toughness: 2,
				new TriggeredAbilityComponent
				{
					Name = "Battle Cry",
					Condition = new CreatureAttacksCondition { OnlyYourCreatures = true },
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.Self(),
						ActionTemplate = new GainLifeAction { Amount = 1 },
					},
				}
			),
			// Mentor of the Meek — 4 mana 2/2.
			// "Whenever a creature you control enters the battlefield, draw a card."
			MakeTriggerCreature(
				"Mentor of the Meek",
				cost: 4,
				power: 2,
				toughness: 2,
				new TriggeredAbilityComponent
				{
					Name = "Tutelage",
					Condition = new CreatureEntersBattlefieldCondition { OnlyYourCreatures = true },
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
			// Vengeful Reaper — 3 mana 1/3.
			// "Whenever an opponent's creature dies, deal 1 damage to the opponent."
			MakeTriggerCreature(
				"Vengeful Reaper",
				cost: 3,
				power: 1,
				toughness: 3,
				new TriggeredAbilityComponent
				{
					Name = "Vengeance",
					Condition = new CreatureDiesCondition { OnlyOpponentCreatures = true },
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.AllValid(
							new IsPlayerSpecification().And(
								new IsControlledByOpponentSpecification()
							)
						),
						ActionTemplate = new DealDamageAction { Amount = 1 },
					},
				}
			),
			// ===== CREATURES WITH TRIGGERED ABILITIES (EventTriggerCondition) =====
			// These are equivalent to the cards above but use the new generic
			// EventTriggerCondition system instead of concrete condition classes.
			// Both systems work side by side — remove old versions once validated.

			// Grim Watcher — same as Grim Initiate using EventTriggerCondition
			// "When a creature you control dies, gain 1 life."
			MakeTriggerCreature(
				"Grim Watcher",
				cost: 2,
				power: 2,
				toughness: 1,
				new TriggeredAbilityComponent
				{
					Name = "Death Rites",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureDestroyed,
						Filter = new IsControlledByYouSpecification(),
					},
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.Self(),
						ActionTemplate = new GainLifeAction { Amount = 1 },
					},
				}
			),
			// Soul Harvester — same as Blood Artist using EventTriggerCondition
			// "When any creature dies, deal 1 damage to the opponent."
			MakeTriggerCreature(
				"Soul Harvester",
				cost: 2,
				power: 0,
				toughness: 1,
				new TriggeredAbilityComponent
				{
					Name = "Harvest",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureDestroyed,
					},
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.AllValid(
							new IsPlayerSpecification().And(
								new IsControlledByOpponentSpecification()
							)
						),
						ActionTemplate = new DealDamageAction { Amount = 1 },
					},
				}
			),
			// War Drummer — same as Reconnaissance using EventTriggerCondition
			// "Whenever your creature attacks, gain 1 life."
			MakeTriggerCreature(
				"War Drummer",
				cost: 2,
				power: 1,
				toughness: 2,
				new TriggeredAbilityComponent
				{
					Name = "Battle Cry",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureAttacked,
						Filter = new IsControlledByYouSpecification(),
					},
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.Self(),
						ActionTemplate = new GainLifeAction { Amount = 1 },
					},
				}
			),
			// Battlefield Scholar — same as Mentor of the Meek using EventTriggerCondition
			// "Whenever a creature you control enters the battlefield, draw a card."
			MakeTriggerCreature(
				"Battlefield Scholar",
				cost: 4,
				power: 2,
				toughness: 2,
				new TriggeredAbilityComponent
				{
					Name = "Tutelage",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreaturePlayed,
						Filter = new IsControlledByYouSpecification(),
					},
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
	/// Builds a random deck for the given player by sampling without replacement from the
	/// provided pool. Owner is stamped onto each card template at deck-build time.
	/// </summary>
	public static IReadOnlyList<Card> BuildRandomDeck(
		int ownerId,
		IReadOnlyList<Card> pool,
		int deckSize = 40,
		Random? rng = null
	)
	{
		rng ??= new Random();
		return pool.OrderBy(_ => rng.Next())
			.Take(deckSize)
			.Select(template => template with { OwnerId = ownerId, ControllerId = ownerId })
			.ToList();
	}

	// ===== FACTORIES =====

	private static Card MakeCreature(string name, int cost, int power, int toughness) =>
		new Card
		{
			Name = name,
			ManaCost = cost,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = power, Toughness = toughness }
			),
		};

	private static Card MakeAbilityCreature(
		string name,
		int cost,
		int power,
		int toughness,
		params ActivatedAbilityComponent[] abilities
	) =>
		new Card
		{
			Name = name,
			ManaCost = cost,
			Components = ImmutableList
				.Create<GameComponent>(
					new CreatureComponent { Power = power, Toughness = toughness }
				)
				.AddRange(abilities),
		};

	private static Card MakeTriggerCreature(
		string name,
		int cost,
		int power,
		int toughness,
		params TriggeredAbilityComponent[] triggers
	) =>
		new Card
		{
			Name = name,
			ManaCost = cost,
			Components = ImmutableList
				.Create<GameComponent>(
					new CreatureComponent { Power = power, Toughness = toughness }
				)
				.AddRange(triggers),
		};

	private static Card MakeSpell(
		string name,
		int cost,
		TargetingStrategy targeting,
		GameAction actionTemplate
	) =>
		new Card
		{
			Name = name,
			ManaCost = cost,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = targeting,
							ActionTemplate = actionTemplate,
						}
					),
				}
			),
		};
}
