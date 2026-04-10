using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Tests for PowerToughnessModifier — temporary and permanent P/T modifications.
/// Covers modifier application, stacking, duration, and integration with combat.
/// </summary>
[TestFixture]
public class PowerToughnessModifierTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== EFFECTIVE VALUES =====

	[Test]
	public void GetEffectivePower_IncludesUntilEndOfTurnModifier()
	{
		var (s1, creature) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var state = ApplyModifier(
			s1,
			creature.Id,
			powerBonus: 3,
			toughnessBonus: 3,
			ModifierDuration.UntilEndOfTurn
		);

		Assert.That(state.GetEffectivePower(creature.Id), Is.EqualTo(5));
	}

	[Test]
	public void GetEffectiveToughness_IncludesUntilEndOfTurnModifier()
	{
		var (s1, creature) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var state = ApplyModifier(
			s1,
			creature.Id,
			powerBonus: 3,
			toughnessBonus: 3,
			ModifierDuration.UntilEndOfTurn
		);

		Assert.That(state.GetEffectiveToughness(creature.Id), Is.EqualTo(5));
	}

	[Test]
	public void GetEffectivePower_IncludesPermanentModifier()
	{
		var (s1, creature) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var state = ApplyModifier(
			s1,
			creature.Id,
			powerBonus: 2,
			toughnessBonus: 1,
			ModifierDuration.Permanent
		);

		Assert.That(state.GetEffectivePower(creature.Id), Is.EqualTo(4));
		Assert.That(state.GetEffectiveToughness(creature.Id), Is.EqualTo(3));
	}

	[Test]
	public void GetEffectivePower_StacksMultipleModifiers()
	{
		var (s1, creature) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var s2 = ApplyModifier(
			s1,
			creature.Id,
			powerBonus: 3,
			toughnessBonus: 3,
			ModifierDuration.UntilEndOfTurn
		);
		var s3 = ApplyModifier(
			s2,
			creature.Id,
			powerBonus: 2,
			toughnessBonus: 1,
			ModifierDuration.Permanent
		);

		Assert.That(s3.GetEffectivePower(creature.Id), Is.EqualTo(7));
		Assert.That(s3.GetEffectiveToughness(creature.Id), Is.EqualTo(6));
	}

	// ===== DURATION =====

	[Test]
	public void UntilEndOfTurn_Modifier_ClearedByStartTurnAction()
	{
		var (s1, creature) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var s2 = ApplyModifier(
			s1,
			creature.Id,
			powerBonus: 3,
			toughnessBonus: 3,
			ModifierDuration.UntilEndOfTurn
		);

		Assert.That(
			s2.GetEffectivePower(creature.Id),
			Is.EqualTo(5),
			"Modifier should be active before start of turn"
		);

		// Run StartTurnAction — should clear UntilEndOfTurn modifiers
		var battlefieldId = s2.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var startTurn = new StartTurnAction
		{
			ActivePlayerId = _ids.Player1Id,
			BattlefieldId = battlefieldId,
			SkipDraw = true,
		};
		var (finalState, _) = s2.AddAction(startTurn).ProcessAllActions();

		Assert.That(
			finalState.GetEffectivePower(creature.Id),
			Is.EqualTo(2),
			"UntilEndOfTurn modifier should be cleared after start of turn"
		);
	}

	[Test]
	public void Permanent_Modifier_NotClearedByStartTurnAction()
	{
		var (s1, creature) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var s2 = ApplyModifier(
			s1,
			creature.Id,
			powerBonus: 2,
			toughnessBonus: 1,
			ModifierDuration.Permanent
		);

		var battlefieldId = s2.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var startTurn = new StartTurnAction
		{
			ActivePlayerId = _ids.Player1Id,
			BattlefieldId = battlefieldId,
			SkipDraw = true,
		};
		var (finalState, _) = s2.AddAction(startTurn).ProcessAllActions();

		Assert.That(
			finalState.GetEffectivePower(creature.Id),
			Is.EqualTo(4),
			"Permanent modifier should not be cleared by start of turn"
		);
	}

	[Test]
	public void MixedModifiers_OnlyUntilEndOfTurn_ClearedByStartTurn()
	{
		var (s1, creature) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var s2 = ApplyModifier(
			s1,
			creature.Id,
			powerBonus: 3,
			toughnessBonus: 3,
			ModifierDuration.UntilEndOfTurn
		);
		var s3 = ApplyModifier(
			s2,
			creature.Id,
			powerBonus: 2,
			toughnessBonus: 1,
			ModifierDuration.Permanent
		);

		Assert.That(
			s3.GetEffectivePower(creature.Id),
			Is.EqualTo(7),
			"Both modifiers active before start of turn"
		);

		var battlefieldId = s3.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var startTurn = new StartTurnAction
		{
			ActivePlayerId = _ids.Player1Id,
			BattlefieldId = battlefieldId,
			SkipDraw = true,
		};
		var (finalState, _) = s3.AddAction(startTurn).ProcessAllActions();

		Assert.That(
			finalState.GetEffectivePower(creature.Id),
			Is.EqualTo(4),
			"Only permanent modifier remains after start of turn (+2 on base 2)"
		);
	}

	// ===== GIANT GROWTH SPELL =====

	[Test]
	public void GiantGrowth_IncreasesEffectivePowerAndToughness()
	{
		var (s1, creature) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var spell = CardLibrary.GiantGrowth() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s2, card) = s1.AddObject(spell, parentId: _ids.Player1HandId);

		var castAction = new CastSpellAction
		{
			CardId = card.Id,
			CastingPlayerId = _ids.Player1Id,
			GameId = _ids.GameId,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
				0,
				ImmutableList.Create(creature.Id)
			),
		};

		var (finalState, _) = s2.AddAction(castAction).ProcessAllActions();

		Assert.That(finalState.GetEffectivePower(creature.Id), Is.EqualTo(5));
		Assert.That(finalState.GetEffectiveToughness(creature.Id), Is.EqualTo(5));
	}

	[Test]
	public void GiantGrowth_AllowsCreatureToSurviveCombatItWouldOtherwiseLose()
	{
		// 2/2 Bear vs 3/3 — Bear would normally die
		// Giant Growth gives Bear +3/+3 making it effectively 5/5 — survives
		var (s1, bear) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, giant) = AddCreature(s1, "Giant", 3, 3, _ids.Player2Id);

		var spell = CardLibrary.GiantGrowth() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s3, card) = s2.AddObject(spell, parentId: _ids.Player1HandId);

		// Cast Giant Growth on the bear
		var castAction = new CastSpellAction
		{
			CardId = card.Id,
			CastingPlayerId = _ids.Player1Id,
			GameId = _ids.GameId,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
				0,
				ImmutableList.Create(bear.Id)
			),
		};
		var (s4, _) = s3.AddAction(castAction).ProcessAllActions();

		// Confirm modifier was applied before attacking
		Assert.That(
			s4.GetEffectivePower(bear.Id),
			Is.EqualTo(5),
			"Giant Growth should have been applied before attack"
		);
		Assert.That(
			s4.GetEffectiveToughness(bear.Id),
			Is.EqualTo(5),
			"Giant Growth should have been applied before attack"
		);

		// Now attack — Bear (effectively 5/5) vs Giant (3/3)
		var (finalState, _) = s4.AddAction(
				new AttackAction
				{
					AttackerId = bear.Id,
					TargetId = giant.Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		Assert.That(
			finalState.GetCardZone(bear.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"Bear should survive — effective toughness 5 > 3 damage"
		);
		Assert.That(
			finalState.GetCardZone(giant.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Giant should die — effective power of Bear (5) > Giant's toughness (3)"
		);
	}

	[Test]
	public void GiantGrowth_ModifierExpires_AfterStartOfNextTurn()
	{
		var (s1, creature) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var spell = CardLibrary.GiantGrowth() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s2, card) = s1.AddObject(spell, parentId: _ids.Player1HandId);

		var castAction = new CastSpellAction
		{
			CardId = card.Id,
			CastingPlayerId = _ids.Player1Id,
			GameId = _ids.GameId,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
				0,
				ImmutableList.Create(creature.Id)
			),
		};
		var (s3, _) = s2.AddAction(castAction).ProcessAllActions();

		Assert.That(
			s3.GetEffectivePower(creature.Id),
			Is.EqualTo(5),
			"Giant Growth should be active this turn"
		);

		// Simulate start of next turn
		var battlefieldId = s3.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var startTurn = new StartTurnAction
		{
			ActivePlayerId = _ids.Player1Id,
			BattlefieldId = battlefieldId,
			SkipDraw = true,
		};
		var (finalState, _) = s3.AddAction(startTurn).ProcessAllActions();

		Assert.That(
			finalState.GetEffectivePower(creature.Id),
			Is.EqualTo(2),
			"Giant Growth should have expired after start of turn"
		);
	}

	// ===== HELPERS =====

	private static GameState ApplyModifier(
		GameState state,
		int cardId,
		int powerBonus,
		int toughnessBonus,
		ModifierDuration duration
	)
	{
		var card = (Card)state.GetObject(cardId);
		var modifier = new PowerToughnessModifier
		{
			PowerBonus = powerBonus,
			ToughnessBonus = toughnessBonus,
			Duration = duration,
		};
		return state.UpdateObject(cardId, card with { Components = card.Components.Add(modifier) });
	}

	private (GameState, Card) AddCreature(
		GameState state,
		string name,
		int power,
		int toughness,
		int ownerId
	)
	{
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var creature = new Card
		{
			Name = name,
			ManaCost = 0,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent
				{
					Power = power,
					Toughness = toughness,
					HasSummoningSickness = false,
				}
			),
		};
		var (newState, added) = state.AddObject(creature, parentId: battlefieldId);
		return (newState, added);
	}
}
