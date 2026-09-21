using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// Builds a fresh GameState for ONE battle. No cards or enemies are placed — the run loads those.
///
/// A new GameState per battle is the point, not a limitation: the run (life, deck) lives above
/// GameState and is what actually persists (not built yet).
/// </summary>
public static class KinBattleFactory
{
	public static GameState Create(
		string name = "Battle",
		int life = 60,
		int maxLife = 60,
		int maxEnergy = 3,
		int rngSeed = 0,
		int opponentHealth = 40,
		OpponentDefinition? opponent = null
	)
	{
		var state = new GameState { RngSeed = rngSeed };

		var (s1, battle) = state.AddObject(
			new KinBattle { Name = name }
		);

		// Enemies belong to the battle, not the player — they are the battle's threat.
		var (s2, enemies) = s1.AddObject(
			new Zone { Name = "Enemies", ZoneType = ZoneType.Enemies },
			parentId: battle.Id
		);

		// The Opponent owns the enemy units and is the only way to WIN a battle. It belongs to the
		// battle rather than the enemy zone: the zone holds units, and it is not one.
		// A definition wins when given; `opponentHealth` remains for tests and for anything that
		// only cares how much there is to chew through.
		var health = opponent?.Health ?? opponentHealth;
		var (s2b, opponentObject) = s2.AddObject(
			new Opponent
			{
				Name = opponent?.Name ?? "The Opponent",
				Description = opponent?.Description ?? "",
				Health = health,
				MaxHealth = health,
				SummonInterval = opponent?.SummonInterval ?? 3,
				Reinforcement = opponent?.Reinforcement ?? EnemyLibrary.Revenant,
				Effects = opponent?.Effects ?? ImmutableList<KinEffect>.Empty,
			},
			parentId: battle.Id
		);

		var (s3, player) = s2b.AddObject(
			new KinPlayer
			{
				Name = "Player",
				Life = life,
				MaxLife = maxLife,
				Energy = maxEnergy,
				MaxEnergy = maxEnergy,
			},
			parentId: battle.Id
		);

		var (s4, draw) = s3.AddObject(
			new Zone { Name = "Draw", ZoneType = ZoneType.Draw },
			parentId: player.Id
		);
		var (s5, hand) = s4.AddObject(
			new Zone { Name = "Hand", ZoneType = ZoneType.Hand },
			parentId: player.Id
		);
		var (s6, discard) = s5.AddObject(
			new Zone { Name = "Discard", ZoneType = ZoneType.Discard },
			parentId: player.Id
		);
		var (s7, field) = s6.AddObject(
			new Zone { Name = "Field", ZoneType = ZoneType.Field },
			parentId: player.Id
		);
		var (s8, exhausted) = s7.AddObject(
			new Zone { Name = "Exhausted", ZoneType = ZoneType.Exhausted },
			parentId: player.Id
		);
		var (s9, taken) = s8.AddObject(
			new Zone { Name = "Taken", ZoneType = ZoneType.Taken },
			parentId: player.Id
		);

		return s9.RegisterWellKnownId(KinObjectKeys.Battle, battle.Id)
			.RegisterWellKnownId(KinObjectKeys.Enemies, enemies.Id)
			.RegisterWellKnownId(KinObjectKeys.Opponent, opponentObject.Id)
			.RegisterWellKnownId(KinObjectKeys.Player, player.Id)
			.RegisterWellKnownId(KinObjectKeys.Draw, draw.Id)
			.RegisterWellKnownId(KinObjectKeys.Hand, hand.Id)
			.RegisterWellKnownId(KinObjectKeys.Discard, discard.Id)
			.RegisterWellKnownId(KinObjectKeys.Field, field.Id)
			.RegisterWellKnownId(KinObjectKeys.Exhausted, exhausted.Id)
			.RegisterWellKnownId(KinObjectKeys.Taken, taken.Id);
	}
}
