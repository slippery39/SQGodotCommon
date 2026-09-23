using KinCore;

namespace KinConsole;

/// <summary>
/// `dotnet run --project KinConsole -- content` — everything in the game, in one place.
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
	private const KinTheme SampleTheme = KinTheme.LongEmergency;

	public static void Execute()
	{
		Themes();
		Floors();
		Enemies();
		Opponents();
		Traits();

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
		Head("THEMES", $"{ThemeLibrary.All.Length} acts, walked in a fixed order");
		Console.WriteLine("  name                 boss");

		foreach (var t in ThemeLibrary.All)
			Console.WriteLine($"  {t.Name, -18}  {t.Boss.Name}");
	}

	private static void Floors()
	{
		Head(
			"THE ACT",
			$"{Run.ActLength} floors — {ThemeLibrary.Of(SampleTheme).Name}, Opponents for seed {SampleSeed}"
		);

		Console.WriteLine("  floor  kind    Opponent                      foes  lead enemy");

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

			Console.WriteLine(
				$"  {floor, 5}  battle  {StarterContent.OpponentFor(floor, SampleSeed).Name, -28}  {count, 4}  "
					+ $"{lead.Name, -18}"
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
				$"  {e.Name, -18}  f{e.MinFloor, -3}  {e.Health, 6}  {e.Attack, 6}  {Rules(KinRulesText.Lines(e))}"
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

	private static void Cards()
	{
		Head("CARDS", "energy is 3 a turn, hand is 5, and the hand is discarded each turn");

		var starter = StarterContent.NewRun(seed: 1);

		// **The whole roster, and what each one starts with** — a companion is an archetype now,
		// so which one you pick changes three cards of the opening deck. Read off the roster rather
		// than restated, or this becomes a second account of what each one does.
		Console.WriteLine(
			$"  COMPANIONS — {StarterContent.Roster.Length}, free in the centre lane every battle"
		);
		foreach (var companion in StarterContent.Roster)
		{
			Console.WriteLine(
				$"    {companion.Name, -10}  {companion.BasePower, 2}/{companion.BaseToughness, -3}  "
					+ Rules(companion.Effects.Select(e => e.Text))
			);
			Console.WriteLine(
				$"    {"", -10}  starts with "
					+ (
						companion.Starter.IsEmpty
							? "the generic three — no archetype yet"
							: string.Join(", ", companion.Starter.Select(c => c.Name))
					)
			);
		}

		Console.WriteLine();
		Console.WriteLine(
			$"  STARTING DECK — {starter.Deck.Count} cards, as {starter.Companion.Name}. Seven are the "
				+ "same for everyone; the companion decides the other three."
		);
		Table(starter.Deck.GroupBy(c => c.Name).Select(g => (g.First(), g.Count())));

		// **The design goal, as a number.** Every card should create a decision; a unit with no
		// rule at all creates none. Kept honest here so a content pass can see it move.
		var pool = StarterContent.RewardPool(ThemeLibrary.All[0].Theme);
		var vanilla = pool.Count(c => c.IsUnit && !KinRulesText.Lines(c).Any());
		Console.WriteLine();
		Console.WriteLine(
			$"  VANILLA — {vanilla} of the {pool.Length} cards you can be offered do nothing but stand there"
		);

		Console.WriteLine();
		Console.WriteLine(
			$"  SHARED POOL — {StarterContent.SharedPool.Length} cards, offered in every act"
		);
		Console.WriteLine(
			"  NOT gated by floor — every card is offerable everywhere, rarity only weights the "
				+ "bag."
		);
		Console.WriteLine(
			$"  A rare is offered {StarterContent.WeightOf(KinRarity.Common)
				/ StarterContent.WeightOf(KinRarity.Rare)}x less often than a common, so an early "
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
			Console.WriteLine(
				$"  {theme.Name.ToUpperInvariant()} — {own.Count} cards tagged to this act, "
					+ "offered in every act (act-exclusive pools are off)"
			);
			Table(own.Select(c => (c, 1)));
		}

		Console.WriteLine();
		Console.WriteLine(
			$"  BULWARK — Bramble's archetype, {StarterContent.BulwarkCards.Length} cards"
		);
		Table(StarterContent.BulwarkCards.Select(c => (c, 1)));

		Console.WriteLine();
		Console.WriteLine($"  FACE — Pike's archetype, {StarterContent.FaceCards.Length} cards");
		Table(StarterContent.FaceCards.Select(c => (c, 1)));
	}

	private static void Table(IEnumerable<(RunCard Card, int Count)> cards)
	{
		Console.WriteLine("    n  cost  name                stats  rarity     does");

		foreach (var (card, count) in cards.OrderBy(c => c.Item1.Cost).ThenBy(c => c.Item1.Name))
		{
			var stats = card.IsUnit ? $"{card.Power}/{card.Toughness}" : "rite";
			var rules = string.Join("; ", KinRulesText.Lines(card));
			var does = rules.Length == 0 ? card.Description : rules;

			Console.WriteLine(
				$"  {count, 3}  {card.Cost, 4}  {card.Name, -18}  {stats, 5}  "
					+ $"{card.Rarity, -9}  {does}"
			);
		}
	}

	private static string Rules(IEnumerable<string> lines)
	{
		var text = string.Join("; ", lines.Where(t => !string.IsNullOrEmpty(t)));
		return text.Length == 0 ? "-" : text;
	}

	private static string Effects(IEnumerable<KinEffect> effects)
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
