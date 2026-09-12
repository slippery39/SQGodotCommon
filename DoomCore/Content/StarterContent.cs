using System.Collections.Immutable;

namespace DoomCore;

/// <summary>
/// Placeholder content so the game is playable. Balance here is a guess, not a measurement —
/// every number is meant to be changed after the first real playtest.
/// </summary>
public static class StarterContent
{
	/// <summary>
	/// Countdown length per scenario. Varying it is free texture: each apocalypse feels different
	/// before the player has read a word of its text. Nuclear is short because clearing your board
	/// in two turns is genuinely hard; Flood is long because committing takes time.
	/// </summary>
	public static int CountdownFor(DoomScenario scenario) =>
		scenario switch
		{
			DoomScenario.Nuclear => 2,
			DoomScenario.Zombie => 3,
			DoomScenario.Rapture => 3,
			DoomScenario.Flood => 5,
			_ => 3,
		};

	/// <summary>
	/// Scenarios are TIERED by floor. Flood removes every unit you did not commit, which on a
	/// 10-card starter deck can end a run outright — the loss only becomes an interesting cost
	/// once there is a deck worth losing. Rapture is excluded everywhere: no transform yet.
	/// </summary>
	public const int FloodUnlocksAtFloor = 8;

	public static ImmutableArray<DoomScenario> PlayableOn(int floor) =>
		floor < FloodUnlocksAtFloor
			? [DoomScenario.Zombie, DoomScenario.Nuclear]
			: [DoomScenario.Zombie, DoomScenario.Nuclear, DoomScenario.Flood];

	private static RunCard Unit(string name, int cost, int power, int toughness, string text) =>
		new()
		{
			Name = name,
			Cost = cost,
			Description = text,
			IsUnit = true,
			Power = power,
			Toughness = toughness,
		};

	/// <summary>The starting companion. One for now; picking between several is a later job.</summary>
	public static Companion StarterCompanion =>
		new()
		{
			Name = "Ash",
			Description = "Followed you out of the first one. Has not left since.",
			BasePower = 1,
			BaseToughness = 3,
		};

	public static Run NewRun(int seed = 1) =>
		new Run
		{
			Life = 60,
			MaxLife = 60,
			RngSeed = seed,
			Companion = StarterCompanion,
		}.WithCards(
			[
				Unit("Scavenger", 1, 2, 2, "Takes what is left."),
				Unit("Scavenger", 1, 2, 2, "Takes what is left."),
				Unit("Scavenger", 1, 2, 2, "Takes what is left."),
				Unit("Scavenger", 1, 2, 2, "Takes what is left."),
				Unit("Bulwark", 1, 0, 4, "Stands in the way."),
				Unit("Bulwark", 1, 0, 4, "Stands in the way."),
				Unit("Bulwark", 1, 0, 4, "Stands in the way."),
				Unit("Ash Walker", 2, 3, 3, "Walked out of the last one."),
				Unit("Ash Walker", 2, 3, 3, "Walked out of the last one."),
				Unit("Lantern Bearer", 0, 1, 1, "Small light, long night."),
			]
		);

	/// <summary>One enemy per battle for now — the 6-enemy cap is designed for, not used yet.</summary>
	public static Enemy EnemyFor(int floor)
	{
		var health = 8 + floor * 4;
		var attack = 3 + floor;

		return new Enemy
		{
			Name = floor % 3 == 0 ? "Herald of the End" : "Wretch",
			Health = health,
			MaxHealth = health,
			Intent = IntentKind.Attack,
			IntentAmount = attack,
		};
	}

	/// <summary>
	/// Which apocalypse waits on a floor. Deterministic from the seed so a run is reproducible —
	/// the console prints the seed, which is what makes a bug report actionable.
	/// </summary>
	public static DoomScenario ScenarioFor(int seed, int floor)
	{
		var pool = PlayableOn(floor);
		return pool[new Random(seed * 7919 + floor).Next(pool.Length)];
	}
}
