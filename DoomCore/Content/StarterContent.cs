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
	/// What a scenario is allowed to change. **A fixed property of its design**, and the thing that
	/// decides which hook implements it — see <see cref="DoomScope"/>.
	/// </summary>
	public static DoomScope ScopeOf(DoomScenario scenario) =>
		scenario switch
		{
			DoomScenario.Flood => DoomScope.Battle,
			_ => DoomScope.Permanent,
		};

	/// <summary>
	/// Which scenarios a floor may roll. **Scope is the difficulty curve**: early floors get
	/// battle-only apocalypses you merely navigate, later floors get ones that leave marks.
	///
	/// Flood used to be gated to floor 8+ because it removed cards from the run deck, which meant
	/// it did not exist at all for the first seven floors. It is a battle-scope board wash now, so
	/// it leads instead — it teaches the player what an apocalypse feels like before one can cost
	/// them anything. Rapture is excluded everywhere: no transform yet.
	/// </summary>
	public static ImmutableArray<DoomScenario> PlayableOn(int floor) =>
		floor < 3
			? [DoomScenario.Flood]
			: [DoomScenario.Flood, DoomScenario.Zombie, DoomScenario.Nuclear];

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

	/// <summary>
	/// The Opponent's HP for a floor. This is the battle's real length dial: you win by cutting it
	/// down through lanes nothing is contesting, so it prices how long you must hold the board.
	///
	/// Provisional — it wants tuning against real play, not reasoning. See DoomJam.md.
	/// </summary>
	public static int OpponentHealthFor(int floor) => 20 + floor * 6;

	/// <summary>
	/// What the Opponent puts back into a lane, scaled by how long the battle has already run.
	///
	/// Scaling on the TURN rather than the floor is what stops a stalled battle being safe: the
	/// longer you fail to break through, the worse the bodies you have to break through. It is the
	/// pressure that replaces the old countdown ending the fight.
	///
	/// Provisional — wants tuning against real play, not reasoning.
	/// </summary>
	public static PendingSummon SummonFor(int turnNumber, int lane) =>
		new()
		{
			Name = "Revenant",
			Health = 4 + turnNumber / 2,
			Attack = 1 + turnNumber / 4,
			Lane = lane,
		};

	/// <summary>
	/// The enemies for a floor, already placed in lanes.
	///
	/// **Lanes need more than one enemy to be a decision.** One enemy across five lanes is covered
	/// by a single unit and the battle is over as a threat; the count is what makes "which lanes do
	/// I contest" cost something. Fixed at battle start — nothing arrives mid-battle.
	///
	/// Enemies are spread from the outside in, so the companion's centre lane is the LAST one
	/// contested. A free blocker that happened to be pre-matched with the only enemy would make the
	/// opening turn decide itself.
	/// </summary>
	public static IReadOnlyList<Enemy> EnemiesFor(int floor)
	{
		var count = Math.Min(2 + floor / 3, DoomBattle.LaneCount);

		// Health is per-enemy, so it must fall as the count rises or floor 8 is unkillable.
		var health = 5 + floor * 2;
		var attack = 2 + floor / 2;

		int[] order = [0, 4, 1, 3, 2];

		return Enumerable
			.Range(0, count)
			.Select(i => new Enemy
			{
				Name = floor % 3 == 0 && i == 0 ? "Herald of the End" : "Wretch",
				Health = health,
				MaxHealth = health,
				Intent = IntentKind.Attack,
				IntentAmount = attack,
				Lane = order[i],
			})
			.ToList();
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
