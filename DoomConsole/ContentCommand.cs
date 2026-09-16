using DoomCore;

namespace DoomConsole;

/// <summary>
/// `dotnet run --project DoomConsole -- content` — everything in the game, in one place.
///
/// **Every number here is READ from the libraries, never restated.** A hand-written content table
/// is stale the moment a balance pass lands, and this repo has already lost a dozen tests to
/// exactly that. If a card is missing from this dump it is missing from the game.
/// </summary>
public static class ContentCommand
{
	/// <summary>Traits are rolled per run, so the floor table shows one example run.</summary>
	private const int SampleSeed = 1;

	public static void Execute()
	{
		Floors();
		Enemies();
		Opponents();
		Traits();
		Scenarios();
		Cards();
	}

	/// <summary>
	/// The act, floor by floor. Enemies and apocalypses are rolled per seed, so what a floor has is
	/// a POOL, not a fixture — except the lead enemy, which is always the newest tier the floor
	/// allows, and the Opponent, which is fixed.
	/// </summary>
	private static void Floors()
	{
		Head("THE ACT", $"{Run.ActLength} floors — Opponents shown for seed {SampleSeed}");

		Console.WriteLine(
			"  floor  kind    Opponent                      foes  lead enemy          apocalypse pool"
		);

		for (var floor = 1; floor <= Run.ActLength; floor++)
		{
			if (StarterContent.FloorKindFor(floor) == FloorKind.Rest)
			{
				Console.WriteLine(
					$"  {floor, 5}  REST    heal {StarterContent.RestHealFor(160)} "
						+ "(30% of max), costs a floor"
				);
				continue;
			}

			var roster = EnemyLibrary.PlayableOn(floor);
			var lead = roster.MaxBy(e => e.MinFloor)!;
			var count = StarterContent.EnemiesFor(floor).Count;
			var dooms = string.Join("/", StarterContent.PlayableOn(floor));

			Console.WriteLine(
				$"  {floor, 5}  battle  {StarterContent.OpponentFor(floor, SampleSeed).Name, -28}  {count, 4}  "
					+ $"{lead.Name, -18}  {dooms}"
			);
		}

		Console.WriteLine();
		Console.WriteLine(
			"  The lead enemy is always the newest tier a floor allows, so an unlock is felt the"
		);
		Console.WriteLine("  turn it happens. The rest of the lanes roll from the whole pool.");
		Console.WriteLine();
		Console.WriteLine(
			"  No Opponent is fought twice wearing the same trait — see TRAITS below."
		);
	}

	private static void Enemies()
	{
		Head("ENEMIES", $"{EnemyLibrary.All.Length} of them");
		Console.WriteLine("  name                from  health  attack  does");

		foreach (var e in EnemyLibrary.All.OrderBy(e => e.MinFloor).ThenBy(e => e.Health))
			Console.WriteLine(
				$"  {e.Name, -18}  f{e.MinFloor, -3}  {e.Health, 6}  {e.Attack, 6}  {Effects(e.Effects)}"
			);
	}

	private static void Opponents()
	{
		Head("OPPONENTS", "the only way to win a battle is to kill one");
		Console.WriteLine("  name              from  health  summons          every  does");

		foreach (var o in EnemyLibrary.AllOpponents.OrderBy(o => o.MinFloor))
			Console.WriteLine(
				$"  {o.Name, -16}  f{o.MinFloor, -3}  {o.Health, 6}  {o.Reinforcement.Name, -15}  "
					+ $"{o.SummonInterval, 5}  {Effects(o.Effects)}"
			);
	}

	/// <summary>
	/// The modifiers laid over an Opponent. Four Opponents crossed with these is what covers
	/// sixteen battles without a fight repeating.
	/// </summary>
	private static void Traits()
	{
		Head("OPPONENT TRAITS", $"{EnemyLibrary.Traits.Length}, crossed with every Opponent");
		Console.WriteLine("  name         does");

		foreach (var t in EnemyLibrary.Traits)
			Console.WriteLine(
				$"  {(t.Name.Length == 0 ? "(plain)" : t.Name), -11}  "
					+ $"{(t.Text.Length == 0 ? "nothing — the fight with no gimmick" : t.Text)}"
			);
	}

	private static void Scenarios()
	{
		Head("APOCALYPSES", "they fire on a clock, over and over, and never end a battle");
		Console.WriteLine("  name       from  scope      clock  does");

		foreach (var d in ScenarioLibrary.All.OrderBy(d => d.MinFloor))
		{
			var what =
				d.Scope == DoomScope.Battle ? Effects(d.BattleEffects) : Permanent(d.Scenario);

			Console.WriteLine(
				$"  {d.Scenario, -9}  f{d.MinFloor, -3}  {d.Scope, -9}  {d.Countdown, 5}  {what}"
			);
		}

		Console.WriteLine();
		Console.WriteLine(
			"  Battle scope changes this fight only. Permanent rewrites the run deck."
		);
	}

	/// <summary>
	/// What a permanent scenario does, named here because it is CODE — a permanent transform
	/// rewrites the run, which lives outside GameState, so it cannot be data like a battle one.
	/// This is the one place in this dump that is not read from a library, and it is the same
	/// asymmetry `ScenarioDefinition` documents.
	/// </summary>
	private static string Permanent(DoomScenario scenario) =>
		scenario switch
		{
			DoomScenario.Zombie => "every unit that DIED returns to the deck as a 2/2 Zombie",
			DoomScenario.Nuclear =>
				$"every unit LEFT STANDING gets +{DoomTransforms.IrradiatedBuff}/+{DoomTransforms.IrradiatedBuff} "
					+ $"and costs {DoomTransforms.IrradiatedDrawCost} life to draw",
			DoomScenario.Rapture => "NOT IMPLEMENTED — needs sacrifice; never offered",
			_ => "",
		};

	private static void Cards()
	{
		Head("CARDS", "energy is 3 a turn, hand is 5, and the hand is discarded each turn");

		var starter = StarterContent.NewRun(seed: 1);
		var companion = starter.Companion;

		Console.WriteLine(
			$"  COMPANION — free, in the centre lane, every battle, untouchable by any doom"
		);
		Console.WriteLine(
			$"    {companion.Name, -18}  {companion.BasePower}/{companion.BaseToughness}   "
				+ "gains a mark from every apocalypse it survives"
		);

		Console.WriteLine();
		Console.WriteLine($"  STARTING DECK — {starter.Deck.Count} cards");
		Table(starter.Deck.GroupBy(c => c.Name).Select(g => (g.First(), g.Count())));

		Console.WriteLine();
		Console.WriteLine(
			$"  REWARD POOL — {StarterContent.RewardPool.Length} cards, 3 offered after each"
		);
		Console.WriteLine("  battle, FLAT (floor 20 offers what floor 1 does)");
		Table(StarterContent.RewardPool.Select(c => (c, 1)));
	}

	private static void Table(IEnumerable<(RunCard Card, int Count)> cards)
	{
		Console.WriteLine("    n  cost  name                stats  does");

		foreach (var (card, count) in cards.OrderBy(c => c.Item1.Cost).ThenBy(c => c.Item1.Name))
		{
			var stats = card.IsUnit ? $"{card.Power}/{card.Toughness}" : "rite";
			var does = card.Effects.IsEmpty ? card.Description : Effects(card.Effects);

			Console.WriteLine(
				$"  {count, 3}  {card.Cost, 4}  {card.Name, -18}  {stats, 5}  {does}"
			);
		}
	}

	private static string Effects(IEnumerable<DoomEffect> effects)
	{
		var text = string.Join(
			"; ",
			effects.Select(e => e.Text).Where(t => !string.IsNullOrEmpty(t))
		);
		return text.Length == 0 ? "-" : text;
	}

	private static void Head(string title, string note)
	{
		Console.WriteLine();
		Console.WriteLine($"  === {title} — {note}");
		Console.WriteLine();
	}
}
