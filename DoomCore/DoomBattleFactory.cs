using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Builds a fresh GameState for ONE battle. No cards or enemies are placed — the run loads those.
///
/// A new GameState per battle is the point, not a limitation: the run (life, deck) lives above
/// GameState and is what actually persists (not built yet).
/// </summary>
public static class DoomBattleFactory
{
	public static GameState Create(
		DoomScenario scenario,
		int countdown,
		int life = 60,
		int maxLife = 60,
		int maxEnergy = 3,
		int rngSeed = 0,
		int opponentHealth = 40
	)
	{
		var state = new GameState { RngSeed = rngSeed };

		var (s1, battle) = state.AddObject(
			new DoomBattle
			{
				Name = $"{scenario}",
				Scenario = scenario,
				CountdownRemaining = countdown,
				CountdownTotal = countdown,
			}
		);

		// Enemies belong to the battle, not the player — they are the scenario's threat.
		var (s2, enemies) = s1.AddObject(
			new Zone { Name = "Enemies", ZoneType = ZoneType.Enemies },
			parentId: battle.Id
		);

		// The Opponent owns the enemy units and is the only way to WIN a battle. It belongs to the
		// battle rather than the enemy zone: the zone holds units, and it is not one.
		var (s2b, opponent) = s2.AddObject(
			new Opponent
			{
				Name = "The Opponent",
				Health = opponentHealth,
				MaxHealth = opponentHealth,
			},
			parentId: battle.Id
		);

		var (s3, player) = s2b.AddObject(
			new DoomPlayer
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

		return s7.RegisterWellKnownId(DoomObjectKeys.Battle, battle.Id)
			.RegisterWellKnownId(DoomObjectKeys.Enemies, enemies.Id)
			.RegisterWellKnownId(DoomObjectKeys.Opponent, opponent.Id)
			.RegisterWellKnownId(DoomObjectKeys.Player, player.Id)
			.RegisterWellKnownId(DoomObjectKeys.Draw, draw.Id)
			.RegisterWellKnownId(DoomObjectKeys.Hand, hand.Id)
			.RegisterWellKnownId(DoomObjectKeys.Discard, discard.Id)
			.RegisterWellKnownId(DoomObjectKeys.Field, field.Id);
	}
}
