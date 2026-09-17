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

	/// <summary>The dooms are a schedule, not a roll, so the floor table shows one theme's.</summary>
	private const DoomTheme SampleTheme = DoomTheme.LongEmergency;

	public static void Execute()
	{
		Themes();
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
	/// <summary>
	/// The choice made once, at the start of a run. Each theme is a fixed escalation of dooms —
	/// six floors a band, then the boss floor's own.
	/// </summary>
	private static void Themes()
	{
		Head("THEMES", $"{ThemeLibrary.All.Length} — chosen once, at the start of a run");
		Console.WriteLine(
			$"  name                 floors 1-{ThemeLibrary.FloorsPerBand}  "
				+ $"{ThemeLibrary.FloorsPerBand + 1}-{ThemeLibrary.FloorsPerBand * 2}  "
				+ $"{ThemeLibrary.FloorsPerBand * 2 + 1}-{ThemeLibrary.FloorsPerBand * 3}  "
				+ $"floor {Run.ActLength}"
		);

		foreach (var t in ThemeLibrary.All)
			Console.WriteLine(
				$"  {t.Name, -18}  {string.Join("  ", t.Bands.Select(b => $"{b, -10}"))}  "
					+ $"{t.FinalDoom} (boss only)"
			);

		Console.WriteLine();
		Console.WriteLine("  The final doom is BATTLE scope by rule: a permanent one on the last");
		Console.WriteLine("  floor would rewrite a deck the run never draws again.");
	}

	private static void Floors()
	{
		Head(
			"THE ACT",
			$"{Run.ActLength} floors — {ThemeLibrary.Of(SampleTheme).Name}, Opponents for seed {SampleSeed}"
		);

		Console.WriteLine(
			"  floor  kind    Opponent                      foes  lead enemy          apocalypse"
		);

		for (var floor = 1; floor <= Run.ActLength; floor++)
		{
			if (StarterContent.FloorKindFor(floor) == FloorKind.Rest)
			{
				Console.WriteLine(
					$"  {floor, 5}  REST    heal {StarterContent.RestHealFor(StarterContent.StartingLife)} "
						+ "(30% of max), costs a floor"
				);
				continue;
			}

			var roster = EnemyLibrary.PlayableOn(floor);
			var lead = roster.MaxBy(e => e.MinFloor)!;
			var count = StarterContent.EnemiesFor(floor).Count;
			var doom = StarterContent.ScenarioFor(SampleTheme, floor);

			Console.WriteLine(
				$"  {floor, 5}  battle  {StarterContent.OpponentFor(floor, SampleSeed).Name, -28}  {count, 4}  "
					+ $"{lead.Name, -18}  {doom}"
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
			var what = ScenarioLibrary.EffectTextOf(d);

			Console.WriteLine(
				$"  {d.Scenario, -9}  f{d.MinFloor, -3}  {d.Scope, -9}  {d.Countdown, 5}  {what}"
			);
		}

		Console.WriteLine();
		Console.WriteLine(
			"  Battle scope changes this fight only. Permanent rewrites the run deck."
		);
	}

	private static void Cards()
	{
		Head("CARDS", "energy is 3 a turn, hand is 5, and the hand is discarded each turn");

		var starter = StarterContent.NewRun(seed: 1);
		var companion = starter.Companion;

		Console.WriteLine(
			$"  COMPANION — free, in the centre lane, every battle, untouchable by any doom"
		);
		// The ABILITY first, because it is the thing a deck is built around now — the marks are
		// growth, the ability is identity. Read off the companion rather than restated here, or
		// this becomes a second account of what Ash does and drifts from the real one.
		var ability = companion.Effects.IsEmpty
			? "no ability"
			: string.Join("; ", companion.Effects.Select(e => e.Text));

		Console.WriteLine(
			$"    {companion.Name, -18}  {companion.BasePower}/{companion.BaseToughness}   {ability}"
		);
		Console.WriteLine(
			$"    {"", -18}         and gains a mark from every apocalypse it survives"
		);

		Console.WriteLine();
		Console.WriteLine($"  STARTING DECK — {starter.Deck.Count} cards");
		Table(starter.Deck.GroupBy(c => c.Name).Select(g => (g.First(), g.Count())));

		Console.WriteLine();
		Console.WriteLine(
			$"  SHARED POOL — {StarterContent.SharedPool.Length} cards, offered in every act"
		);
		Console.WriteLine(
			"  NOT gated by floor — every card is offerable everywhere, rarity only weights the "
				+ "bag."
		);
		Console.WriteLine(
			$"  A rare is offered {StarterContent.WeightOf(DoomRarity.Common)
				/ StarterContent.WeightOf(DoomRarity.Rare)}x less often than a common, so an early "
				+ "rare is uncommon rather than impossible."
		);
		Table(StarterContent.SharedPool.Select(c => (c, 1)));

		foreach (var theme in ThemeLibrary.All)
		{
			var own = StarterContent
				.RewardPool(theme.Theme)
				.Where(c => c.Theme == theme.Theme)
				.ToList();

			Console.WriteLine();
			Console.WriteLine($"  {theme.Name.ToUpperInvariant()} — {own.Count} cards of its own");
			Table(own.Select(c => (c, 1)));
		}
	}

	private static void Table(IEnumerable<(RunCard Card, int Count)> cards)
	{
		Console.WriteLine("    n  cost  name                stats  rarity     does");

		foreach (var (card, count) in cards.OrderBy(c => c.Item1.Cost).ThenBy(c => c.Item1.Name))
		{
			var stats = card.IsUnit ? $"{card.Power}/{card.Toughness}" : "rite";
			var does = card.Effects.IsEmpty ? card.Description : Effects(card.Effects);

			Console.WriteLine(
				$"  {count, 3}  {card.Cost, 4}  {card.Name, -18}  {stats, 5}  "
					+ $"{card.Rarity, -9}  {does}"
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
