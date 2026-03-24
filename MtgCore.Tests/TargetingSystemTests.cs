using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

// =====================================================================
// Test-only specifications — proof of concept for property-based filtering
// =====================================================================

/// <summary>
/// Matches creatures with power greater than or equal to the threshold.
/// Demonstrates that specifications can filter on any game object property.
/// </summary>
public record MinPowerSpecification : TargetSpecification
{
	public int MinPower { get; init; }

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		var obj = context.GameState.GetObject(candidateId);
		if (obj is not CreatureCard creature)
			return false;

		var zone = context.GameState.GetCardZone(candidateId);
		return zone.ZoneType == ZoneType.Battlefield && creature.Power >= MinPower;
	}
}

// =====================================================================
// Tests
// =====================================================================

[TestFixture]
public class TargetingSystemTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
	}

	// ===== SPECIFICATION COMPOSITION =====

	[Test]
	public void AndSpecification_RequiresBothConditions()
	{
		// Creature with low power on battlefield
		var (s1, weakCreature) = _state.AddObject(
			MakeCreature("Memnite", power: 1, toughness: 1, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);
		// Creature with high power on battlefield
		var (s2, strongCreature) = s1.AddObject(
			MakeCreature("Leatherback Baloth", power: 4, toughness: 4, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		// "Creature with power 3 or greater"
		var spec = new IsCreatureSpecification().And(new MinPowerSpecification { MinPower = 3 });

		var context = MakeContext(s2);

		Assert.That(
			spec.IsSatisfiedBy(strongCreature.Id, context),
			Is.True,
			"4-power creature should match"
		);
		Assert.That(
			spec.IsSatisfiedBy(weakCreature.Id, context),
			Is.False,
			"1-power creature should not match"
		);
		Assert.That(
			spec.IsSatisfiedBy(_ids.Player2Id, context),
			Is.False,
			"Player should not match"
		);
	}

	[Test]
	public void OrSpecification_AcceptsEitherCondition()
	{
		var (s1, creature) = _state.AddObject(
			MakeCreature("Bear", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		var spec = new IsPlayerSpecification().Or(new IsCreatureSpecification());
		var context = MakeContext(s1);

		Assert.That(
			spec.IsSatisfiedBy(_ids.Player1Id, context),
			Is.True,
			"Player should satisfy IsPlayer"
		);
		Assert.That(
			spec.IsSatisfiedBy(_ids.Player2Id, context),
			Is.True,
			"Player should satisfy IsPlayer"
		);
		Assert.That(
			spec.IsSatisfiedBy(creature.Id, context),
			Is.True,
			"Creature on battlefield should satisfy IsCreature"
		);
		Assert.That(
			spec.IsSatisfiedBy(_ids.StackId, context),
			Is.False,
			"A zone should not satisfy player or creature"
		);
	}

	[Test]
	public void NotSpecification_InvertsResult()
	{
		var (s1, creature) = _state.AddObject(
			MakeCreature("Bear", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		// "Not a creature" — should match players but not creatures
		var spec = new IsCreatureSpecification().Not();
		var context = MakeContext(s1);

		Assert.That(
			spec.IsSatisfiedBy(_ids.Player1Id, context),
			Is.True,
			"Player is not a creature so should match"
		);
		Assert.That(
			spec.IsSatisfiedBy(creature.Id, context),
			Is.False,
			"Creature should not satisfy Not(IsCreature)"
		);
	}

	[Test]
	public void MinPowerSpecification_FiltersCorrectly()
	{
		var (s1, smallCreature) = _state.AddObject(
			MakeCreature("Squire", power: 1, toughness: 2, ownerId: _ids.Player1Id),
			parentId: _ids.Player1BattlefieldId
		);
		var (s2, medCreature) = s1.AddObject(
			MakeCreature("Bear", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);
		var (s3, bigCreature) = s2.AddObject(
			MakeCreature("Titan", power: 6, toughness: 6, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		var spec = new MinPowerSpecification { MinPower = 3 };
		var context = MakeContext(s3);

		Assert.That(
			spec.IsSatisfiedBy(smallCreature.Id, context),
			Is.False,
			"Power 1 should not match"
		);
		Assert.That(
			spec.IsSatisfiedBy(medCreature.Id, context),
			Is.False,
			"Power 2 should not match"
		);
		Assert.That(spec.IsSatisfiedBy(bigCreature.Id, context), Is.True, "Power 6 should match");
	}

	[Test]
	public void IsControlledByOpponent_FiltersCorrectly()
	{
		var (s1, myCreature) = _state.AddObject(
			MakeCreature("My Bear", power: 2, toughness: 2, ownerId: _ids.Player1Id),
			parentId: _ids.Player1BattlefieldId
		);
		var (s2, theirCreature) = s1.AddObject(
			MakeCreature("Their Bear", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		var spec = new IsCreatureSpecification().And(new IsControlledByOpponentSpecification());
		var context = MakeContext(s2);

		Assert.That(
			spec.IsSatisfiedBy(theirCreature.Id, context),
			Is.True,
			"Opponent's creature should match"
		);
		Assert.That(
			spec.IsSatisfiedBy(myCreature.Id, context),
			Is.False,
			"My creature should not match"
		);
	}

	// ===== ALL VALID MODE (Pyroclasm-style) =====

	[Test]
	public void AllValid_HitsAllCreaturesOnBothBattlefields()
	{
		var (s1, p1Creature) = _state.AddObject(
			MakeCreature("My Bear", power: 2, toughness: 2, ownerId: _ids.Player1Id),
			parentId: _ids.Player1BattlefieldId
		);
		var (s2, p2Creature) = s1.AddObject(
			MakeCreature("Their Bear", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		var pyroclasm = MakeSpellCard(
			"Pyroclasm",
			_ids.Player1Id,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.AllValid(new IsCreatureSpecification()),
				ActionTemplate = new DealDamageAction { Amount = 2 },
			}
		);

		var (s3, pyroCard) = s2.AddObject(pyroclasm, parentId: _ids.Player1HandId);

		var (finalState, events) = s3.AddAction(MakeCastAction(pyroCard.Id, _ids.Player1Id))
			.ProcessAllActions();

		var p1CreatureZone = finalState.GetCardZone(p1Creature.Id);
		var p2CreatureZone = finalState.GetCardZone(p2Creature.Id);

		Assert.That(
			p1CreatureZone.ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Player 1's 2/2 should die to 2 damage"
		);
		Assert.That(
			p2CreatureZone.ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Player 2's 2/2 should die to 2 damage"
		);
		Assert.That(events.OfType<CreatureDestroyedEvent>().Count(), Is.EqualTo(2));
	}

	[Test]
	public void AllValid_DoesNotHitPlayers()
	{
		var (s1, creature) = _state.AddObject(
			MakeCreature("Bear", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		var pyroclasm = MakeSpellCard(
			"Pyroclasm",
			_ids.Player1Id,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.AllValid(new IsCreatureSpecification()),
				ActionTemplate = new DealDamageAction { Amount = 2 },
			}
		);

		var (s2, pyroCard) = s1.AddObject(pyroclasm, parentId: _ids.Player1HandId);

		var (finalState, events) = s2.AddAction(MakeCastAction(pyroCard.Id, _ids.Player1Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(20),
			"Player 1 should not take damage"
		);
		Assert.That(
			finalState.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(20),
			"Player 2 should not take damage"
		);
	}

	[Test]
	public void AllValid_WithNoValidTargets_SpawnsActionWithEmptyTargets()
	{
		// No creatures on the battlefield
		var pyroclasm = MakeSpellCard(
			"Pyroclasm",
			_ids.Player1Id,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.AllValid(new IsCreatureSpecification()),
				ActionTemplate = new DealDamageAction { Amount = 2 },
			}
		);

		var (s1, pyroCard) = _state.AddObject(pyroclasm, parentId: _ids.Player1HandId);

		var (finalState, events) = s1.AddAction(MakeCastAction(pyroCard.Id, _ids.Player1Id))
			.ProcessAllActions();

		Assert.That(finalState.HasPendingActions, Is.False);
		Assert.That(events.OfType<CreatureDestroyedEvent>(), Is.Empty);
		Assert.That(events.OfType<PlayerDamagedEvent>(), Is.Empty);
	}

	[Test]
	public void AllValid_WithPowerFilter_OnlyHitsMatchingCreatures()
	{
		var (s1, smallCreature) = _state.AddObject(
			MakeCreature("Squire", power: 1, toughness: 3, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);
		var (s2, bigCreature) = s1.AddObject(
			MakeCreature("Titan", power: 6, toughness: 6, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		// "Destroy all creatures with power 3 or greater"
		var spell = MakeSpellCard(
			"Forced March",
			_ids.Player1Id,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.AllValid(
					new IsCreatureSpecification().And(new MinPowerSpecification { MinPower = 3 })
				),
				ActionTemplate = new DealDamageAction { Amount = 999 },
			}
		);

		var (s3, spellCard) = s2.AddObject(spell, parentId: _ids.Player1HandId);

		var (finalState, events) = s3.AddAction(MakeCastAction(spellCard.Id, _ids.Player1Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetCardZone(bigCreature.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Big creature should be destroyed"
		);
		Assert.That(
			finalState.GetCardZone(smallCreature.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"Small creature should survive"
		);
	}

	// ===== MULTI TARGET USER SELECT =====

	[Test]
	public void MultiTarget_PlayerCanChooseUpToMaxTargets()
	{
		var (s1, creature1) = _state.AddObject(
			MakeCreature("Bear A", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);
		var (s2, creature2) = s1.AddObject(
			MakeCreature("Bear B", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		// "Deal 2 damage to up to 2 targets"
		var spell = MakeSpellCard(
			"Arc Lightning",
			_ids.Player1Id,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.MultiTarget(
					new IsPlayerSpecification().Or(new IsCreatureSpecification()),
					minTargets: 1,
					maxTargets: 2
				),
				ActionTemplate = new DealDamageAction { Amount = 2 },
			}
		);

		var (s3, spellCard) = s2.AddObject(spell, parentId: _ids.Player1HandId);

		var (finalState, events) = s3.AddAction(
				new CastSpellAction
				{
					CardId = spellCard.Id,
					CastingPlayerId = _ids.Player1Id,
					GameId = _ids.GameId,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
						0,
						ImmutableList.Create(creature1.Id, creature2.Id)
					),
				}
			)
			.ProcessAllActions();

		Assert.That(
			finalState.GetCardZone(creature1.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Bear A should die"
		);
		Assert.That(
			finalState.GetCardZone(creature2.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Bear B should die"
		);
		Assert.That(events.OfType<CreatureDestroyedEvent>().Count(), Is.EqualTo(2));
	}

	[Test]
	public void MultiTarget_ValidateAdd_FailsIfTooManyTargetsChosen()
	{
		var (s1, c1) = _state.AddObject(
			MakeCreature("Bear A", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);
		var (s2, c2) = s1.AddObject(
			MakeCreature("Bear B", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);
		var (s3, c3) = s2.AddObject(
			MakeCreature("Bear C", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		var spell = MakeSpellCard(
			"Arc Lightning",
			_ids.Player1Id,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.MultiTarget(
					new IsCreatureSpecification(),
					minTargets: 1,
					maxTargets: 2
				),
				ActionTemplate = new DealDamageAction { Amount = 2 },
			}
		);

		var (s4, spellCard) = s3.AddObject(spell, parentId: _ids.Player1HandId);

		// Try to choose 3 targets when max is 2
		var (_, success) = s4.TryAddAction(
			new CastSpellAction
			{
				CardId = spellCard.Id,
				CastingPlayerId = _ids.Player1Id,
				GameId = _ids.GameId,
				TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
					0,
					ImmutableList.Create(c1.Id, c2.Id, c3.Id)
				),
			}
		);

		Assert.That(success, Is.False, "Should reject more targets than MaxTargets allows");
	}

	[Test]
	public void MultiTarget_ValidateAdd_FailsIfTooFewTargetsChosen()
	{
		var spell = MakeSpellCard(
			"Arc Lightning",
			_ids.Player1Id,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.MultiTarget(
					new IsCreatureSpecification(),
					minTargets: 2,
					maxTargets: 2
				),
				ActionTemplate = new DealDamageAction { Amount = 2 },
			}
		);

		var (s1, spellCard) = _state.AddObject(spell, parentId: _ids.Player1HandId);

		// Only choose 1 target when min is 2
		var (s2, creature) = s1.AddObject(
			MakeCreature("Bear", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		var (_, success) = s2.TryAddAction(
			new CastSpellAction
			{
				CardId = spellCard.Id,
				CastingPlayerId = _ids.Player1Id,
				GameId = _ids.GameId,
				TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
					0,
					ImmutableList.Create(creature.Id)
				),
			}
		);

		Assert.That(success, Is.False, "Should reject fewer targets than MinTargets requires");
	}

	// ===== RANDOM TARGET MODE =====

	[Test]
	public void RandomTarget_HitsExactlyOneCreature()
	{
		var (s1, c1) = _state.AddObject(
			MakeCreature("Bear A", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);
		var (s2, c2) = s1.AddObject(
			MakeCreature("Bear B", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		var spell = MakeSpellCard(
			"Shock Bolt",
			_ids.Player1Id,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.RandomTarget(new IsCreatureSpecification()),
				ActionTemplate = new DealDamageAction { Amount = 2 },
			}
		);

		var (s3, spellCard) = s2.AddObject(spell, parentId: _ids.Player1HandId);

		var (finalState, events) = s3.AddAction(MakeCastAction(spellCard.Id, _ids.Player1Id))
			.ProcessAllActions();

		var destroyedCount = events.OfType<CreatureDestroyedEvent>().Count();
		Assert.That(destroyedCount, Is.EqualTo(1), "Exactly one creature should be destroyed");

		// One in graveyard, one still on battlefield
		var c1Zone = finalState.GetCardZone(c1.Id).ZoneType;
		var c2Zone = finalState.GetCardZone(c2.Id).ZoneType;
		var zones = new[] { c1Zone, c2Zone };
		Assert.That(zones.Count(z => z == ZoneType.Graveyard), Is.EqualTo(1));
		Assert.That(zones.Count(z => z == ZoneType.Battlefield), Is.EqualTo(1));
	}

	[Test]
	public void RandomTarget_WithNoValidTargets_DoesNothing()
	{
		var spell = MakeSpellCard(
			"Shock Bolt",
			_ids.Player1Id,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.RandomTarget(new IsCreatureSpecification()),
				ActionTemplate = new DealDamageAction { Amount = 2 },
			}
		);

		var (s1, spellCard) = _state.AddObject(spell, parentId: _ids.Player1HandId);

		var (finalState, events) = s1.AddAction(MakeCastAction(spellCard.Id, _ids.Player1Id))
			.ProcessAllActions();

		Assert.That(finalState.HasPendingActions, Is.False);
		Assert.That(events.OfType<CreatureDestroyedEvent>(), Is.Empty);
	}

	// ===== MULTI EFFECT CARD =====

	[Test]
	public void MultiEffectCard_EachEffectResolvesIndependently()
	{
		var (s1, creature) = _state.AddObject(
			MakeCreature("Bear", power: 2, toughness: 2, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		// A card with two effects: deal 1 damage to a creature, deal 2 damage to a player
		var spell = new InstantCard
		{
			Name = "Split Decision",
			ManaCost = 2,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Effects = ImmutableList.Create(
				new CardEffect
				{
					TargetingStrategy = TargetingStrategy.SingleTarget(
						new IsCreatureSpecification()
					),
					ActionTemplate = new DealDamageAction { Amount = 1 },
				},
				new CardEffect
				{
					TargetingStrategy = TargetingStrategy.SingleTarget(new IsPlayerSpecification()),
					ActionTemplate = new DealDamageAction { Amount = 2 },
				}
			),
		};

		var (s2, spellCard) = s1.AddObject(spell, parentId: _ids.Player1HandId);

		var (finalState, events) = s2.AddAction(
				new CastSpellAction
				{
					CardId = spellCard.Id,
					CastingPlayerId = _ids.Player1Id,
					GameId = _ids.GameId,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>
						.Empty.Add(0, ImmutableList.Create(creature.Id))
						.Add(1, ImmutableList.Create(_ids.Player2Id)),
				}
			)
			.ProcessAllActions();

		var updatedCreature = (CreatureCard)finalState.GetObject(creature.Id);
		Assert.That(updatedCreature.Damage, Is.EqualTo(1), "Creature should have 1 damage");
		Assert.That(
			finalState.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(18),
			"Player 2 should take 2 damage"
		);
	}

	// ===== HELPERS =====

	private TargetingContext MakeContext(GameState state) =>
		new()
		{
			GameState = state,
			SourceCardId = 0,
			CastingPlayerId = _ids.Player1Id,
		};

	private CreatureCard MakeCreature(string name, int power, int toughness, int ownerId) =>
		new()
		{
			Name = name,
			Power = power,
			Toughness = toughness,
			ManaCost = power,
			OwnerId = ownerId,
			ControllerId = ownerId,
		};

	private InstantCard MakeSpellCard(string name, int ownerId, params CardEffect[] effects) =>
		new()
		{
			Name = name,
			ManaCost = 1,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Effects = ImmutableList.Create(effects),
		};

	private CastSpellAction MakeCastAction(int cardId, int castingPlayerId) =>
		new()
		{
			CardId = cardId,
			CastingPlayerId = castingPlayerId,
			GameId = _ids.GameId,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
		};
}
