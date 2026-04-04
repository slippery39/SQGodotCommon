using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Defines the full pool of cards available for random deck generation.
///
/// Damage spells target opponents and opponent creatures only — the random AI
/// has no targeting intelligence so we restrict valid targets at the card level
/// rather than teaching the AI to distinguish friendly from hostile.
/// </summary>
public static class CardPool
{
	public static IReadOnlyList<Func<int, Card>> All { get; } =
		new List<Func<int, Card>>
		{
			// ===== CHEAP AGGRESSIVE CREATURES =====
			ownerId => MakeCreature("Goblin Raider", ownerId, cost: 1, power: 2, toughness: 1),
			ownerId => MakeCreature("Jackal Pup", ownerId, cost: 1, power: 2, toughness: 1),
			ownerId => MakeCreature("Savannah Lions", ownerId, cost: 1, power: 2, toughness: 1),
			ownerId => MakeCreature("Raging Goblin", ownerId, cost: 1, power: 1, toughness: 1),
			ownerId => MakeCreature("Llanowar Elves", ownerId, cost: 1, power: 1, toughness: 1),
			// ===== MIDRANGE CREATURES =====
			ownerId => MakeCreature("Grizzly Bears", ownerId, cost: 2, power: 2, toughness: 2),
			ownerId => MakeCreature("Runeclaw Bear", ownerId, cost: 2, power: 2, toughness: 2),
			ownerId => MakeCreature("Elvish Warrior", ownerId, cost: 2, power: 2, toughness: 3),
			ownerId => MakeCreature("Centaur Courser", ownerId, cost: 3, power: 3, toughness: 3),
			ownerId => MakeCreature("Hill Giant", ownerId, cost: 3, power: 3, toughness: 4),
			ownerId => MakeCreature("Bladetusk Boar", ownerId, cost: 4, power: 3, toughness: 3),
			ownerId => MakeCreature("Kalonian Tusker", ownerId, cost: 3, power: 3, toughness: 3),
			ownerId => MakeCreature("Wind Drake", ownerId, cost: 3, power: 2, toughness: 2),
			ownerId => MakeCreature("Wall of Stone", ownerId, cost: 3, power: 0, toughness: 8),
			ownerId => MakeCreature("Iron Golem", ownerId, cost: 4, power: 4, toughness: 4),
			// ===== BIG CREATURES =====
			ownerId => MakeCreature("Serra Angel", ownerId, cost: 5, power: 4, toughness: 4),
			ownerId => MakeCreature("Mahamoti Djinn", ownerId, cost: 6, power: 5, toughness: 6),
			ownerId => MakeCreature("Craw Wurm", ownerId, cost: 6, power: 6, toughness: 4),
			ownerId => MakeCreature("Ancient Ooze", ownerId, cost: 7, power: 6, toughness: 6),
			ownerId => MakeCreature("Leviathan", ownerId, cost: 9, power: 10, toughness: 10),
			// ===== REMOVAL SPELLS (damage to opponent or opponent's creatures) =====
			ownerId =>
				MakeSpell(
					"Lightning Bolt",
					ownerId,
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
					new DealDamageAction { Amount = 3 }
				),
			ownerId =>
				MakeSpell(
					"Shock",
					ownerId,
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
			ownerId =>
				MakeSpell(
					"Volcanic Hammer",
					ownerId,
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
			ownerId =>
				MakeSpell(
					"Searing Spear",
					ownerId,
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
			ownerId =>
				MakeSpell(
					"Incinerate",
					ownerId,
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
			// ===== BURN SPELLS (damage to opponent player only) =====
			ownerId =>
				MakeSpell(
					"Lightning Helix",
					ownerId,
					cost: 2,
					TargetingStrategy.SingleTarget(
						new IsPlayerSpecification().And(new IsControlledByOpponentSpecification())
					),
					new DealDamageAction { Amount = 3 }
				),
			ownerId =>
				MakeSpell(
					"Lava Spike",
					ownerId,
					cost: 1,
					TargetingStrategy.SingleTarget(
						new IsPlayerSpecification().And(new IsControlledByOpponentSpecification())
					),
					new DealDamageAction { Amount = 3 }
				),
			ownerId =>
				MakeSpell(
					"Rift Bolt",
					ownerId,
					cost: 2,
					TargetingStrategy.SingleTarget(
						new IsPlayerSpecification().And(new IsControlledByOpponentSpecification())
					),
					new DealDamageAction { Amount = 3 }
				),
			// ===== CREATURES WITH ACTIVATED ABILITIES =====

			// Prodigal Sorcerer — 3 mana 1/1. "1 mana: Deal 1 damage to any opponent target."
			ownerId =>
				MakeAbilityCreature(
					"Prodigal Sorcerer",
					ownerId,
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
			ownerId =>
				MakeAbilityCreature(
					"Drudge Skeletons",
					ownerId,
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
			ownerId =>
				MakeAbilityCreature(
					"Wizard Mentor",
					ownerId,
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
			ownerId =>
				MakeAbilityCreature(
					"Spikeshot Goblin",
					ownerId,
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
			// ===== UTILITY SPELLS =====
			ownerId =>
				MakeSpell(
					"Healing Salve",
					ownerId,
					cost: 1,
					TargetingStrategy.Self(),
					new GainLifeAction { Amount = 3 }
				),
			ownerId =>
				MakeSpell(
					"Revitalize",
					ownerId,
					cost: 2,
					TargetingStrategy.Self(),
					new GainLifeAction { Amount = 3 }
				),
			ownerId =>
				MakeSpell(
					"Inspiration",
					ownerId,
					cost: 4,
					TargetingStrategy.NoTarget(),
					new DrawCardsAction
					{
						Amount = 2,
						PlayerIdContextKey = ContextKeys.CastingPlayerId,
					}
				),
			ownerId =>
				MakeSpell(
					"Counsel of the Soratami",
					ownerId,
					cost: 3,
					TargetingStrategy.NoTarget(),
					new DrawCardsAction
					{
						Amount = 2,
						PlayerIdContextKey = ContextKeys.CastingPlayerId,
					}
				),
		};

	/// <summary>
	/// Builds a random 20-card deck for the given player by sampling
	/// without replacement from the full card pool.
	/// </summary>
	public static IReadOnlyList<Card> BuildRandomDeck(int ownerId, int deckSize = 20)
	{
		var rng = new Random();
		return All.OrderBy(_ => rng.Next())
			.Take(deckSize)
			.Select(factory => factory(ownerId))
			.ToList();
	}

	// ===== FACTORIES =====

	private static Card MakeCreature(
		string name,
		int ownerId,
		int cost,
		int power,
		int toughness
	) =>
		new Card
		{
			Name = name,
			ManaCost = cost,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = power, Toughness = toughness }
			),
		};

	private static Card MakeAbilityCreature(
		string name,
		int ownerId,
		int cost,
		int power,
		int toughness,
		params ActivatedAbilityComponent[] abilities
	) =>
		new Card
		{
			Name = name,
			ManaCost = cost,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList
				.Create<GameComponent>(
					new CreatureComponent { Power = power, Toughness = toughness }
				)
				.AddRange(abilities),
		};

	private static Card MakeSpell(
		string name,
		int ownerId,
		int cost,
		TargetingStrategy targeting,
		GameAction actionTemplate
	) =>
		new Card
		{
			Name = name,
			ManaCost = cost,
			OwnerId = ownerId,
			ControllerId = ownerId,
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
