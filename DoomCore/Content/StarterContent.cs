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

	/// <summary>Rapture is excluded — it has no transform yet and would throw on resolution.</summary>
	public static readonly ImmutableArray<DoomScenario> Playable =
	[
		DoomScenario.Zombie,
		DoomScenario.Nuclear,
		DoomScenario.Flood,
	];

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

	public static Run NewRun(int seed = 1) =>
		new Run
		{
			Life = 60,
			MaxLife = 60,
			RngSeed = seed,
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
	public static DoomScenario ScenarioFor(int seed, int floor) =>
		Playable[new Random(seed * 7919 + floor).Next(Playable.Length)];
}
