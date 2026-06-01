using System.Collections.Immutable;
using NUnit.Framework;

namespace ImmutableGameObjects.Tests;

// =====================================================================
// Context Keys
// =====================================================================

public static class ContextKeys
{
	public const string RevealedCardManaCost = "revealed_card_mana_cost";
	public const string RevealedCardId = "revealed_card_id";
	public const string ChosenCreatureId = "chosen_creature_id";
	public const string ChosenTargetId = "chosen_target_id";
}

// =====================================================================
// Minimal Game Objects
// =====================================================================

public record TestPlayer : GameObject
{
	public int Life { get; init; } = 20;
}

public record TestCreature : GameObject
{
	public int Power { get; init; }
	public int Toughness { get; init; }
	public int ManaCost { get; init; }
	public bool IsDestroyed { get; init; } = false;
}

public record TestCard : GameObject
{
	public int ManaCost { get; init; }
}

// =====================================================================
// Minimal Game Events
// =====================================================================

public record CreatureDestroyedEvent : GameEvent
{
	public int CreatureId { get; init; }
}

public record PlayerDamagedEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int Amount { get; init; }
}

public record PlayerGainedLifeEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int Amount { get; init; }
}

public record CardRevealedEvent : GameEvent
{
	public int CardId { get; init; }
	public int ManaCost { get; init; }
}

// =====================================================================
// Shared Actions
// =====================================================================

/// <summary>
/// Deals damage to a player. Reduces their life total.
/// Amount can be set directly or read from pipeline context via InputKey.
/// </summary>
public record DamagePlayerAction : GameAction
{
	public int PlayerId { get; init; }
	public int Amount { get; init; }
	public string InputKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var amount = string.IsNullOrEmpty(InputKey) ? Amount : GetInput<int>(InputKey, 0);

		if (amount == 0)
			return new ActionResult(gameState);

		var player = (TestPlayer)gameState.GetObject(PlayerId);
		var updatedPlayer = player with { Life = player.Life - amount };
		var newState = gameState.UpdateObject(PlayerId, updatedPlayer);

		return new ActionResult(newState).WithEvent(
			new PlayerDamagedEvent { PlayerId = PlayerId, Amount = amount }
		);
	}
}

/// <summary>
/// Restores life to a player.
/// Amount can be set directly or read from pipeline context via InputKey.
/// </summary>
public record GainLifeAction : GameAction
{
	public int PlayerId { get; init; }
	public int Amount { get; init; }
	public string InputKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var amount = string.IsNullOrEmpty(InputKey) ? Amount : GetInput<int>(InputKey, 0);

		if (amount == 0)
			return new ActionResult(gameState);

		var player = (TestPlayer)gameState.GetObject(PlayerId);
		var updatedPlayer = player with { Life = player.Life + amount };
		var newState = gameState.UpdateObject(PlayerId, updatedPlayer);

		return new ActionResult(newState).WithEvent(
			new PlayerGainedLifeEvent { PlayerId = PlayerId, Amount = amount }
		);
	}
}

/// <summary>
/// Destroys a single creature by ID.
/// </summary>
public record DestroyCreatureAction : GameAction
{
	public int CreatureId { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var creature = (TestCreature)gameState.GetObject(CreatureId);

		if (creature.IsDestroyed)
			return new ActionResult(gameState);

		var updatedCreature = creature with { IsDestroyed = true };
		var newState = gameState.UpdateObject(CreatureId, updatedCreature);

		return new ActionResult(newState).WithEvent(
			new CreatureDestroyedEvent { CreatureId = CreatureId }
		);
	}
}

/// <summary>
/// Deals damage to a creature. If damage meets or exceeds toughness, destroys it.
/// Amount can be set directly or read from pipeline context via InputKey.
/// </summary>
public record DamageCreatureAction : GameAction
{
	public int CreatureId { get; init; }
	public int Amount { get; init; }
	public string InputKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var amount = string.IsNullOrEmpty(InputKey) ? Amount : GetInput<int>(InputKey, 0);

		var creature = (TestCreature)gameState.GetObject(CreatureId);

		if (creature.IsDestroyed || amount == 0)
			return new ActionResult(gameState);

		if (amount >= creature.Toughness)
		{
			var destroyed = creature with { IsDestroyed = true };
			var newState = gameState.UpdateObject(CreatureId, destroyed);
			return new ActionResult(newState).WithEvent(
				new CreatureDestroyedEvent { CreatureId = CreatureId }
			);
		}

		return new ActionResult(gameState);
	}
}

/// <summary>
/// Deals damage to all creatures currently in game state.
/// Spawns a DamageCreatureAction for each non-destroyed creature found.
/// Amount can be set directly or read from pipeline context via InputKey.
/// </summary>
public record DamageAllCreaturesAction : GameAction
{
	public int Amount { get; init; }
	public string InputKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var amount = string.IsNullOrEmpty(InputKey) ? Amount : GetInput<int>(InputKey, 0);

		if (amount == 0)
			return new ActionResult(gameState);

		var creatures = gameState
			.IdToGameObjectMap.Values.OfType<TestCreature>()
			.Where(c => !c.IsDestroyed)
			.ToImmutableList();

		var spawnedActions = creatures
			.Select(c =>
				(GameAction)new DamageCreatureAction { CreatureId = c.Id, Amount = amount }
			)
			.ToImmutableList();

		return new ActionResult(gameState.SpawnActions(spawnedActions));
	}
}

/// <summary>
/// Reveals the top card of a player's library (first child of the player object).
/// Outputs the card's ID and mana cost into pipeline context via ContextKeys.
/// </summary>
public record RevealTopCardAction : GameAction
{
	public int LibraryOwnerId { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var topCardId = gameState.GetChildrenIds(LibraryOwnerId).FirstOrDefault();

		if (topCardId == 0)
			return new ActionResult(gameState);

		var topCard = (TestCard)gameState.GetObject(topCardId);

		return new ActionResult(gameState)
			.WithOutput(ContextKeys.RevealedCardId, topCardId)
			.WithOutput(ContextKeys.RevealedCardManaCost, topCard.ManaCost)
			.WithEvent(new CardRevealedEvent { CardId = topCardId, ManaCost = topCard.ManaCost });
	}
}

/// <summary>
/// Deals damage to all creatures with mana cost at or below the threshold.
/// Spawns a DestroyCreatureAction for each matching creature.
/// </summary>
public record DestroyCreaturesByManaCostAction : GameAction
{
	public int MaxManaCost { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var targets = gameState
			.IdToGameObjectMap.Values.OfType<TestCreature>()
			.Where(c => !c.IsDestroyed && c.ManaCost <= MaxManaCost)
			.ToImmutableList();

		var spawnedActions = targets
			.Select(c => (GameAction)new DestroyCreatureAction { CreatureId = c.Id })
			.ToImmutableList();

		return new ActionResult(gameState.SpawnActions(spawnedActions));
	}
}

/// <summary>
/// Destroys a target creature only if its mana cost is at or below MaxManaCost.
/// Target is chosen upfront. Invalid targets are silently ignored.
/// </summary>
public record SmotherAction : GameAction
{
	public int TargetCreatureId { get; init; }
	public int MaxManaCost { get; init; } = 3;

	public override ActionResult Execute(GameState gameState)
	{
		var creature = (TestCreature)gameState.GetObject(TargetCreatureId);

		if (creature.IsDestroyed || creature.ManaCost > MaxManaCost)
			return new ActionResult(gameState);

		var destroyed = creature with { IsDestroyed = true };
		var newState = gameState.UpdateObject(TargetCreatureId, destroyed);

		return new ActionResult(newState).WithEvent(
			new CreatureDestroyedEvent { CreatureId = TargetCreatureId }
		);
	}
}

/// <summary>
/// Deals damage to a randomly selected creature.
/// Seed is fixed for deterministic tests.
/// </summary>
public record DamageRandomCreatureAction : GameAction
{
	public int Amount { get; init; }
	public int Seed { get; init; } = 0;

	public override ActionResult Execute(GameState gameState)
	{
		var creatures = gameState
			.IdToGameObjectMap.Values.OfType<TestCreature>()
			.Where(c => !c.IsDestroyed)
			.ToImmutableList();

		if (creatures.IsEmpty)
			return new ActionResult(gameState);

		var rng = new Random(Seed);
		var target = creatures[rng.Next(creatures.Count)];

		var spawned = ImmutableList.Create<GameAction>(
			new DamageCreatureAction { CreatureId = target.Id, Amount = Amount }
		);

		return new ActionResult(gameState.SpawnActions(spawned));
	}
}

// =====================================================================
// Tests
// =====================================================================

[TestFixture]
public class CardProofOfConceptTests
{
	private GameState _initialState;
	private int _playerId;
	private int _opponentId;

	[SetUp]
	public void Setup()
	{
		var state = new GameState();
		var (s1, player) = state.AddObject(new TestPlayer { Name = "Player", Life = 20 });
		var (s2, opponent) = s1.AddObject(new TestPlayer { Name = "Opponent", Life = 20 });
		_initialState = s2;
		_playerId = player.Id;
		_opponentId = opponent.Id;
	}

	// ===== LIGHTNING BOLT =====

	[Test]
	public void LightningBolt_DealsThreeDamageToPlayer()
	{
		var (finalState, events) = _initialState
			.AddAction(new DamagePlayerAction { PlayerId = _opponentId, Amount = 3 })
			.ProcessAllActions();

		var opponent = (TestPlayer)finalState.GetObject(_opponentId);
		Assert.That(opponent.Life, Is.EqualTo(17));
		Assert.That(events.OfType<PlayerDamagedEvent>().Single().Amount, Is.EqualTo(3));
	}

	[Test]
	public void LightningBolt_DealsThreeDamageToCreature_AndDestroysIt()
	{
		var (s1, creature) = _initialState.AddObject(
			new TestCreature
			{
				Name = "Grizzly Bears",
				Power = 2,
				Toughness = 2,
				ManaCost = 2,
			}
		);

		var (finalState, events) = s1.AddAction(
				new DamageCreatureAction { CreatureId = creature.Id, Amount = 3 }
			)
			.ProcessAllActions();

		var updated = (TestCreature)finalState.GetObject(creature.Id);
		Assert.That(updated.IsDestroyed, Is.True);
		Assert.That(
			events.OfType<CreatureDestroyedEvent>().Single().CreatureId,
			Is.EqualTo(creature.Id)
		);
	}

	[Test]
	public void LightningBolt_DealsThreeDamageToCreature_DoesNotDestroyIfTough()
	{
		var (s1, creature) = _initialState.AddObject(
			new TestCreature
			{
				Name = "Hill Giant",
				Power = 3,
				Toughness = 4,
				ManaCost = 4,
			}
		);

		var (finalState, events) = s1.AddAction(
				new DamageCreatureAction { CreatureId = creature.Id, Amount = 3 }
			)
			.ProcessAllActions();

		var updated = (TestCreature)finalState.GetObject(creature.Id);
		Assert.That(updated.IsDestroyed, Is.False);
		Assert.That(events.OfType<CreatureDestroyedEvent>(), Is.Empty);
	}

	// ===== LIGHTNING HELIX =====

	[Test]
	public void LightningHelix_DealsDamageAndGainsLife()
	{
		var lightningHelix = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new DamagePlayerAction { PlayerId = _opponentId, Amount = 3 },
				new GainLifeAction { PlayerId = _playerId, Amount = 3 }
			),
		};

		var (finalState, events) = _initialState.AddAction(lightningHelix).ProcessAllActions();

		var player = (TestPlayer)finalState.GetObject(_playerId);
		var opponent = (TestPlayer)finalState.GetObject(_opponentId);

		Assert.That(opponent.Life, Is.EqualTo(17), "Opponent should take 3 damage");
		Assert.That(player.Life, Is.EqualTo(23), "Player should gain 3 life");
		Assert.That(events.OfType<PlayerDamagedEvent>().Single().Amount, Is.EqualTo(3));
		Assert.That(events.OfType<PlayerGainedLifeEvent>().Single().Amount, Is.EqualTo(3));
	}

	[Test]
	public void LightningHelix_BothEffectsAreIndependent()
	{
		var (s1, lowLifeOpponent) = _initialState.AddObject(
			new TestPlayer { Name = "LowLifeOpponent", Life = 3 }
		);

		var lightningHelix = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new DamagePlayerAction { PlayerId = lowLifeOpponent.Id, Amount = 3 },
				new GainLifeAction { PlayerId = _playerId, Amount = 3 }
			),
		};

		var (finalState, _) = s1.AddAction(lightningHelix).ProcessAllActions();

		var player = (TestPlayer)finalState.GetObject(_playerId);
		Assert.That(player.Life, Is.EqualTo(23), "Player should still gain life");
	}

	// ===== PYROCLASM =====

	[Test]
	public void Pyroclasm_DealsTwoDamageToAllCreatures()
	{
		var (s1, bearA) = _initialState.AddObject(
			new TestCreature
			{
				Name = "Bear A",
				Power = 2,
				Toughness = 2,
				ManaCost = 2,
			}
		);
		var (s2, bearB) = s1.AddObject(
			new TestCreature
			{
				Name = "Bear B",
				Power = 2,
				Toughness = 2,
				ManaCost = 2,
			}
		);
		var (s3, giant) = s2.AddObject(
			new TestCreature
			{
				Name = "Hill Giant",
				Power = 3,
				Toughness = 4,
				ManaCost = 4,
			}
		);

		var (finalState, events) = s3.AddAction(new DamageAllCreaturesAction { Amount = 2 })
			.ProcessAllActions();

		var updatedBearA = (TestCreature)finalState.GetObject(bearA.Id);
		var updatedBearB = (TestCreature)finalState.GetObject(bearB.Id);
		var updatedGiant = (TestCreature)finalState.GetObject(giant.Id);

		Assert.That(updatedBearA.IsDestroyed, Is.True, "Bear A should be destroyed");
		Assert.That(updatedBearB.IsDestroyed, Is.True, "Bear B should be destroyed");
		Assert.That(updatedGiant.IsDestroyed, Is.False, "Giant should survive");
		Assert.That(events.OfType<CreatureDestroyedEvent>().Count(), Is.EqualTo(2));
	}

	[Test]
	public void Pyroclasm_WithNoCreatures_DoesNothing()
	{
		var (finalState, events) = _initialState
			.AddAction(new DamageAllCreaturesAction { Amount = 2 })
			.ProcessAllActions();

		Assert.That(finalState.HasPendingActions, Is.False);
		Assert.That(events, Is.Empty);
	}

	// ===== DARK CONFIDANT =====

	[Test]
	public void DarkConfidant_RevealsTopCard_AndPlayerLosesLifeEqualToManaCost()
	{
		var (s1, card) = _initialState.AddObject(
			new TestCard { Name = "Shock", ManaCost = 1 },
			parentId: _playerId
		);

		// Note: LoseLifeAction reads mana cost from context via InputKey
		var darkConfidantTrigger = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new RevealTopCardAction { LibraryOwnerId = _playerId },
				new DamagePlayerAction
				{
					PlayerId = _playerId,
					InputKey = ContextKeys.RevealedCardManaCost,
				}
			),
		};

		var (finalState, events) = s1.AddAction(darkConfidantTrigger).ProcessAllActions();

		var player = (TestPlayer)finalState.GetObject(_playerId);
		Assert.That(player.Life, Is.EqualTo(19), "Player should lose 1 life (Shock costs 1)");

		var revealEvent = events.OfType<CardRevealedEvent>().Single();
		Assert.That(revealEvent.CardId, Is.EqualTo(card.Id));
		Assert.That(revealEvent.ManaCost, Is.EqualTo(1));

		var damageEvent = events.OfType<PlayerDamagedEvent>().Single();
		Assert.That(damageEvent.Amount, Is.EqualTo(1));
	}

	[Test]
	public void DarkConfidant_HighManaCostCard_PlayerLosesMoreLife()
	{
		var (s1, _) = _initialState.AddObject(
			new TestCard { Name = "Nicol Bolas", ManaCost = 8 },
			parentId: _playerId
		);

		var darkConfidantTrigger = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new RevealTopCardAction { LibraryOwnerId = _playerId },
				new DamagePlayerAction
				{
					PlayerId = _playerId,
					InputKey = ContextKeys.RevealedCardManaCost,
				}
			),
		};

		var (finalState, _) = s1.AddAction(darkConfidantTrigger).ProcessAllActions();

		var player = (TestPlayer)finalState.GetObject(_playerId);
		Assert.That(player.Life, Is.EqualTo(12), "Player should lose 8 life");
	}

	[Test]
	public void DarkConfidant_EmptyLibrary_NothingHappens()
	{
		var darkConfidantTrigger = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new RevealTopCardAction { LibraryOwnerId = _playerId },
				new DamagePlayerAction
				{
					PlayerId = _playerId,
					InputKey = ContextKeys.RevealedCardManaCost,
				}
			),
		};

		var (finalState, events) = _initialState
			.AddAction(darkConfidantTrigger)
			.ProcessAllActions();

		var player = (TestPlayer)finalState.GetObject(_playerId);
		Assert.That(player.Life, Is.EqualTo(20), "Player should not lose life with empty library");
		Assert.That(events, Is.Empty);
	}

	// ===== RANDOM TARGET =====

	[Test]
	public void RandomDamage_HitsExactlyOneCreature()
	{
		var (s1, _) = _initialState.AddObject(
			new TestCreature
			{
				Name = "Bear A",
				Power = 2,
				Toughness = 2,
				ManaCost = 2,
			}
		);
		var (s2, _) = s1.AddObject(
			new TestCreature
			{
				Name = "Bear B",
				Power = 2,
				Toughness = 2,
				ManaCost = 2,
			}
		);

		var (finalState, events) = s2.AddAction(
				new DamageRandomCreatureAction { Amount = 2, Seed = 42 }
			)
			.ProcessAllActions();

		var allCreatures = finalState.IdToGameObjectMap.Values.OfType<TestCreature>().ToList();

		Assert.That(
			allCreatures.Count(c => c.IsDestroyed),
			Is.EqualTo(1),
			"Exactly one creature should be destroyed"
		);
		Assert.That(events.OfType<CreatureDestroyedEvent>().Count(), Is.EqualTo(1));
	}

	[Test]
	public void RandomDamage_WithNoCreatures_DoesNothing()
	{
		var (finalState, events) = _initialState
			.AddAction(new DamageRandomCreatureAction { Amount = 2, Seed = 0 })
			.ProcessAllActions();

		Assert.That(finalState.HasPendingActions, Is.False);
		Assert.That(events, Is.Empty);
	}

	// ===== SMOTHER =====

	[Test]
	public void Smother_DestroysCreatureWithManaCostThreeOrLess()
	{
		var (s1, creature) = _initialState.AddObject(
			new TestCreature
			{
				Name = "Grizzly Bears",
				Power = 2,
				Toughness = 2,
				ManaCost = 2,
			}
		);

		var (finalState, events) = s1.AddAction(
				new SmotherAction { TargetCreatureId = creature.Id }
			)
			.ProcessAllActions();

		var updated = (TestCreature)finalState.GetObject(creature.Id);
		Assert.That(updated.IsDestroyed, Is.True);
		Assert.That(
			events.OfType<CreatureDestroyedEvent>().Single().CreatureId,
			Is.EqualTo(creature.Id)
		);
	}

	[Test]
	public void Smother_CannotDestroyCreatureWithManaCostFourOrMore()
	{
		var (s1, creature) = _initialState.AddObject(
			new TestCreature
			{
				Name = "Hill Giant",
				Power = 3,
				Toughness = 4,
				ManaCost = 4,
			}
		);

		var (finalState, events) = s1.AddAction(
				new SmotherAction { TargetCreatureId = creature.Id }
			)
			.ProcessAllActions();

		var updated = (TestCreature)finalState.GetObject(creature.Id);
		Assert.That(updated.IsDestroyed, Is.False, "Giant costs 4, Smother only hits 3 or less");
		Assert.That(events.OfType<CreatureDestroyedEvent>(), Is.Empty);
	}

	[Test]
	public void Smother_AtExactlyThreeMana_StillDestroys()
	{
		var (s1, creature) = _initialState.AddObject(
			new TestCreature
			{
				Name = "Phyrexian Rager",
				Power = 2,
				Toughness = 2,
				ManaCost = 3,
			}
		);

		var (finalState, _) = s1.AddAction(new SmotherAction { TargetCreatureId = creature.Id })
			.ProcessAllActions();

		var updated = (TestCreature)finalState.GetObject(creature.Id);
		Assert.That(
			updated.IsDestroyed,
			Is.True,
			"Creature at exactly 3 mana cost should be destroyed"
		);
	}

	// ===== FORCED MARCH =====

	[Test]
	public void ForcedMarch_DestroysAllCreaturesWithManaCostTwoOrLess()
	{
		var (s1, bearA) = _initialState.AddObject(
			new TestCreature
			{
				Name = "Bear A",
				Power = 2,
				Toughness = 2,
				ManaCost = 2,
			}
		);
		var (s2, bearB) = s1.AddObject(
			new TestCreature
			{
				Name = "Bear B",
				Power = 2,
				Toughness = 2,
				ManaCost = 2,
			}
		);
		var (s3, giant) = s2.AddObject(
			new TestCreature
			{
				Name = "Hill Giant",
				Power = 3,
				Toughness = 4,
				ManaCost = 4,
			}
		);

		var (finalState, events) = s3.AddAction(
				new DestroyCreaturesByManaCostAction { MaxManaCost = 2 }
			)
			.ProcessAllActions();

		var updatedBearA = (TestCreature)finalState.GetObject(bearA.Id);
		var updatedBearB = (TestCreature)finalState.GetObject(bearB.Id);
		var updatedGiant = (TestCreature)finalState.GetObject(giant.Id);

		Assert.That(updatedBearA.IsDestroyed, Is.True, "Bear A costs 2, should be destroyed");
		Assert.That(updatedBearB.IsDestroyed, Is.True, "Bear B costs 2, should be destroyed");
		Assert.That(updatedGiant.IsDestroyed, Is.False, "Giant costs 4, should survive");
		Assert.That(events.OfType<CreatureDestroyedEvent>().Count(), Is.EqualTo(2));
	}

	[Test]
	public void ForcedMarch_WithNoMatchingCreatures_DoesNothing()
	{
		var (s1, giant) = _initialState.AddObject(
			new TestCreature
			{
				Name = "Hill Giant",
				Power = 3,
				Toughness = 4,
				ManaCost = 4,
			}
		);

		var (finalState, events) = s1.AddAction(
				new DestroyCreaturesByManaCostAction { MaxManaCost = 2 }
			)
			.ProcessAllActions();

		var updated = (TestCreature)finalState.GetObject(giant.Id);
		Assert.That(updated.IsDestroyed, Is.False);
		Assert.That(events, Is.Empty);
	}

	[Test]
	public void ForcedMarch_DoesNotDestroyAlreadyDestroyedCreatures()
	{
		var (s1, _) = _initialState.AddObject(
			new TestCreature
			{
				Name = "Dead Bear",
				Power = 2,
				Toughness = 2,
				ManaCost = 2,
				IsDestroyed = true,
			}
		);

		var (_, events) = s1.AddAction(new DestroyCreaturesByManaCostAction { MaxManaCost = 2 })
			.ProcessAllActions();

		Assert.That(
			events.OfType<CreatureDestroyedEvent>(),
			Is.Empty,
			"Already destroyed creatures should not trigger destroy events"
		);
	}
}
