using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Is the archetype we assume exists actually good?** Builds the most theme-dense legal deck a
/// pool allows and plays it against a saved metagame — the decks evolution actually produced.
///
/// This exists because an assumption was wrong in a way no amount of reading decklists would have
/// settled. Evolution finished with a deck running 4x Goblin Chieftain supported by only 3x
/// Frenzied Goblin, which was read all session as the "half-built deck" failure the synergy
/// feature exists to fix. Measured, against the same eight decks:
///
/// | Deck | Win rate |
/// |---|---|
/// | Full goblins — 4x each of the 10 best goblins, 37 goblin cards | **24.4%** (39/160) |
/// | The half-goblin deck evolution actually built | **55.0%** |
///
/// **The AI was right and the assumption was wrong.** Committing to the tribe costs more than the
/// tribal payoff returns: playing your 10th-best goblin instead of the format's 10th-best card is
/// a losing trade, and Goblin Chieftain's +1/+1 does not cover it. CSC has 17 goblins, so this is
/// not a pool-size limit — the half package IS the optimum, not a failure to reach one.
///
/// **The general lesson: "the AI half-built an archetype" is a HYPOTHESIS, not an observation.**
/// Magic intuition about critical mass does not transfer to an engine with no colours, a small
/// pool, and mostly-weak tribe members. Test it here before designing anything around it.
///
/// It also vindicates one design decision worth keeping: features only GENERATE deck proposals and
/// the measured win rate JUDGES them. Had deck-composition scoring been allowed into the fitness
/// function, it would have driven decks toward exactly this losing configuration.
/// </summary>
[TestFixture]
public class ArchetypeChallenge
{
	/// A metagame produced by mode 6. Point this at a newer run to re-baseline.
	private const string DefaultField = "sim_results/metagame_csc_20260830_002412.json";

	/// <summary>
	/// **`sim_results/` is relative to the WORKING DIRECTORY**, and NUnit runs from the test
	/// binary's folder. Anchor on the SOLUTION FILE, not on `sim_results/` — stray sim_results
	/// directories exist wherever a process has been run from, including
	/// `MtgSimulator.Tests/bin/Debug/net10.0/`, which is the first hit walking up and holds only
	/// card_values_csc.json. Anchoring there loads an empty table and every card reads 0.00pp, so
	/// the failure looks like missing DATA rather than a wrong PATH.
	/// </summary>
	private static void ChdirToSolutionRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && dir.GetFiles("*.sln").Length == 0)
			dir = dir.Parent;
		Assert.That(dir, Is.Not.Null, "could not find the solution root");
		Directory.SetCurrentDirectory(dir!.FullName);
	}

	/// <summary>
	/// The most theme-dense legal deck the pool allows: 4-of every theme card, best measured card
	/// first, then topped up with the best non-theme cards.
	///
	/// Deliberately naive. The question is whether MAXIMUM density wins; a hand-tuned build would
	/// answer a different question, and the hand-tuning would smuggle in the assumption under test.
	/// </summary>
	public static (Decklist Deck, int ThemeCards) BuildMaxDensity(
		string name,
		IReadOnlyList<Card> pool,
		ConstructedValues values,
		Func<Card, bool> isTheme,
		int lands = 23
	)
	{
		var deck = Decklist.Empty(name) with { Lands = lands };

		void FillFrom(IEnumerable<Card> cards)
		{
			foreach (var c in cards.OrderByDescending(c => values.CardDelta(c.Name)))
			{
				var need = Decklist.DeckSize - deck.Lands - deck.SpellCount;
				if (need <= 0)
					return;
				if (deck.CopiesOf(c.Name) > 0)
					continue;
				deck = deck.WithCopies(c.Name, Math.Min(Decklist.MaxCopies, need));
			}
		}

		FillFrom(pool.Where(isTheme));
		var themeCards = deck.SpellCount;
		FillFrom(pool.Where(c => !isTheme(c)));
		return (deck, themeCards);
	}

	/// Plays a challenger against every deck in a field, alternating who is on the play.
	public static (
		double Rate,
		IReadOnlyList<(string Opponent, double Rate)> PerMatchup
	) PlayAgainstField(
		Decklist challenger,
		IReadOnlyList<Decklist> field,
		IReadOnlyDictionary<string, Card> index,
		int gamesPerMatchup = 20,
		int seed = 90_000
	)
	{
		var rows = new List<(string, double)>();
		int wins = 0,
			games = 0;

		foreach (var opponent in field)
		{
			var won = 0;
			for (var i = 0; i < gamesPerMatchup; i++)
			{
				var onPlay = i % 2 == 0;
				var (state, ids, names) = ConstructedGameSetup.Build(
					onPlay ? challenger : opponent,
					onPlay ? opponent : challenger,
					index
				);
				var s = seed + i * 7;
				var runner = new GameRunner(
					new MultiTurnBeamSearchAiStrategy(
						ids,
						2,
						rng: new Random(s + 1),
						cardValues: AiCardValues.Current
					),
					new MultiTurnBeamSearchAiStrategy(
						ids,
						2,
						rng: new Random(s + 2),
						cardValues: AiCardValues.Current
					)
				);
				var (result, _) = runner.Run(state, ids, names, s + 3, s + 4);
				if (onPlay ? result.IsPlayer1Win : result.IsPlayer2Win)
					won++;
			}
			rows.Add((opponent.Name, (double)won / gamesPerMatchup));
			wins += won;
			games += gamesPerMatchup;
		}

		return ((double)wins / games, rows);
	}

	/// <summary>
	/// **The sharpest version of the question: play the HAND-BUILT archetypes against what the AI
	/// produced.** `DeckRegistry` holds decks a human wrote and believes in — Traditional Storm,
	/// Reanimator, Affinity, Goblins, Dragonstorm — and the combined pool contains every card they
	/// use. If those decks beat the AI's good-cards piles, the deckbuilder is genuinely failing to
	/// reach them. If they lose, the piles are correct and the premise needs revisiting.
	///
	/// Nothing else in this project answers that. The evolution can only tell you which of ITS OWN
	/// decks is best; it cannot tell you whether the whole field is stuck in a local optimum.
	/// </summary>
	[TestCase("Traditional Storm")]
	[TestCase("Reanimator")]
	[TestCase("Affinity")]
	[TestCase("Goblins")]
	[TestCase("Dragonstorm")]
	[TestCase("Zoo")]
	[TestCase("Jund")]
	[Explicit("Plays a few hundred games per case against a saved metagame.")]
	public void HandBuiltArchetype_AgainstTheEvolvedField(string deckName)
	{
		ChdirToSolutionRoot();

		// Overridable so the same harness can score any run's field without an edit.
		var Field =
			Environment.GetEnvironmentVariable("MTG_FIELD")
			?? "sim_results/metagame_all_20260830_023326.json";
		if (!File.Exists(Field))
			Assert.Ignore($"no saved field at {Field} — run an ALL evolution first");

		var set = SetRegistry.Get("ALL");
		var index = ConstructedGameSetup.PoolIndex(
			set.Cards.Where(c => !c.HasSubtype("Land")).ToList()
		);
		var saved = DecklistStore.Load(Field)!;

		int wins = 0,
			games = 0;
		const int GamesPerMatchup = 20;

		Console.WriteLine();
		Console.WriteLine($"=== {deckName} vs the evolved ALL field ===");
		Console.WriteLine();

		foreach (var opponent in saved.Decks)
		{
			var won = 0;
			for (var i = 0; i < GamesPerMatchup; i++)
			{
				var onPlay = i % 2 == 0;
				// The precon is a card LIST, the field deck is a Decklist — FromDecks takes
				// builders precisely so both can be supplied per game with the right owner id.
				var (state, ids, names) = GameSetup.FromDecks(
					owner =>
						onPlay
							? DeckRegistry.Build(deckName, owner)
							: opponent.Materialize(owner, index),
					owner =>
						onPlay
							? opponent.Materialize(owner, index)
							: DeckRegistry.Build(deckName, owner)
				);
				var s = 70_000 + i * 7;
				var runner = new GameRunner(
					new MultiTurnBeamSearchAiStrategy(
						ids,
						2,
						rng: new Random(s + 1),
						cardValues: AiCardValues.Current
					),
					new MultiTurnBeamSearchAiStrategy(
						ids,
						2,
						rng: new Random(s + 2),
						cardValues: AiCardValues.Current
					)
				);
				var (result, _) = runner.Run(state, ids, names, s + 3, s + 4);
				if (onPlay ? result.IsPlayer1Win : result.IsPlayer2Win)
					won++;
			}
			Console.WriteLine($"  vs {opponent.Name, -14} {(double)won / GamesPerMatchup, 6:P0}");
			wins += won;
			games += GamesPerMatchup;
		}

		Console.WriteLine();
		Console.WriteLine(
			$"  {deckName.ToUpperInvariant()} overall: {(double)wins / games:P1} ({wins}/{games})"
				+ "   (50% = as good as the field's own average)"
		);
	}

	/// <summary>
	/// **Would Zoo be BETTER with the cards the AI insists on playing?**
	///
	/// The evolved field plays 4x Ancestral Recall and 4x Liliana of the Veil in nearly every deck
	/// and loses to Zoo 32.5-67.5. Two readings: the AI is right about those cards and wrong about
	/// everything else, or those cards are traps in a no-blocker engine where a fast clock beats
	/// card advantage. A hybrid settles it — take Zoo and swap its weakest spells for the two cards
	/// the AI will not give up.
	///
	/// Scored against the other eight precons, which is a real 23pp-spread metagame with genuine
	/// counter-play (Storm beats Zoo 65%, Goblins beats Storm 60%), rather than against the AI
	/// field which is eight variations of one deck.
	/// </summary>
	[TestCase("Zoo")]
	[TestCase("Goblins")]
	[Explicit("Plays a few hundred games.")]
	public void HybridWithTheCardsTheAiInsistsOn(string baseDeck)
	{
		ChdirToSolutionRoot();

		var set = SetRegistry.Get("ALL");
		var spells = set.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var index = ConstructedGameSetup.PoolIndex(spells);
		var values = ConstructedValuesStore.Load("ALL");

		// The precon as a Decklist, so cards can be swapped.
		var cards = DeckRegistry.Build(baseDeck, 1);
		var lands = cards.Count(c => c.HasSubtype("Land"));
		var counts = cards
			.Where(c => !c.HasSubtype("Land"))
			.GroupBy(c => c.Name, StringComparer.Ordinal)
			.ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

		// Precons predate the 4-of rule and some run 5+ of a card, so WithCopies clamps and drops
		// slots. Re-derive the mana base from what actually fits rather than trusting the original
		// land count — otherwise the deck is 59 cards and fails validation for a reason that has
		// nothing to do with the experiment.
		var built = counts.Aggregate(
			Decklist.Empty(baseDeck) with
			{
				Lands = lands,
			},
			(d, kv) => d.WithCopies(kv.Key, kv.Value)
		);
		var original = built with { Lands = Decklist.DeckSize - built.SpellCount };

		// Swap the 8 lowest-valued slots for the two cards the AI will not cut.
		var hybrid = original;
		var toCut = 8;
		foreach (
			var name in counts
				.Keys.OrderBy(n => values.CardDelta(n))
				.ThenBy(n => n, StringComparer.Ordinal)
		)
		{
			if (toCut <= 0)
				break;
			var take = Math.Min(toCut, hybrid.CopiesOf(name));
			hybrid = hybrid.WithCopies(name, hybrid.CopiesOf(name) - take);
			toCut -= take;
		}
		hybrid = hybrid.WithCopies("Ancestral Recall", 4).WithCopies("Liliana of the Veil", 4);
		hybrid = hybrid with
		{
			Name = $"{baseDeck}+Recall/Liliana",
			Lands = Decklist.DeckSize - hybrid.SpellCount,
		};

		Assert.That(original.Validate(), Is.Null, original.Validate());
		Assert.That(hybrid.Validate(), Is.Null, hybrid.Validate());

		Console.WriteLine();
		Console.WriteLine(hybrid.Format(index));

		// The other eight precons as the field — a real metagame with counter-play.
		var opponents = DeckRegistry
			.All.Where(d => !string.Equals(d.Name, baseDeck, StringComparison.Ordinal))
			.Select(d => d.Name)
			.ToList();

		foreach (var (deck, label) in new[] { (original, "ORIGINAL"), (hybrid, "HYBRID  ") })
		{
			int wins = 0,
				games = 0;
			foreach (var opp in opponents)
			{
				for (var i = 0; i < 20; i++)
				{
					var onPlay = i % 2 == 0;
					var s2 = 61_000 + i * 7;
					var (state, ids, names) = GameSetup.FromDecks(
						owner =>
							onPlay
								? deck.Materialize(owner, index)
								: DeckRegistry.Build(opp, owner),
						owner =>
							onPlay ? DeckRegistry.Build(opp, owner) : deck.Materialize(owner, index)
					);
					var runner = new GameRunner(
						new MultiTurnBeamSearchAiStrategy(
							ids,
							2,
							rng: new Random(s2 + 1),
							cardValues: AiCardValues.Current
						),
						new MultiTurnBeamSearchAiStrategy(
							ids,
							2,
							rng: new Random(s2 + 2),
							cardValues: AiCardValues.Current
						)
					);
					var (result, _) = runner.Run(state, ids, names, s2 + 3, s2 + 4);
					if (onPlay ? result.IsPlayer1Win : result.IsPlayer2Win)
						wins++;
					games++;
				}
			}
			Console.WriteLine(
				$"  {label} {deck.Name, -28} {(double)wins / games:P1} ({wins}/{games})"
			);
		}
	}

	/// <summary>
	/// One row per archetype hypothesis. Adding a new one is a single line — that is the point.
	///
	/// **Reports rather than asserting a threshold.** The output IS the finding, and a pass/fail
	/// bar would encode the very assumption being tested. 50% means "as good as the field's own
	/// average"; the goblin case came back at 24.4%.
	/// </summary>
	[TestCase("CSC", "Goblin")]
	[TestCase("CSC", "Artifact")]
	[TestCase("ALL", "Goblin")]
	[TestCase("ALL", "Artifact")]
	[TestCase("ALL", "Spirit")]
	[Explicit("Plays a few hundred games per case against a saved metagame.")]
	public void MaximumDensity_AgainstASavedField(string setCode, string subtype)
	{
		ChdirToSolutionRoot();

		if (!File.Exists(DefaultField))
			Assert.Ignore($"no saved field at {DefaultField} — run an evolution first");

		var set = SetRegistry.Get(setCode);
		var spells = set.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var index = ConstructedGameSetup.PoolIndex(spells);
		var values = ConstructedValuesStore.Load(set.Code);
		var saved = DecklistStore.Load(DefaultField)!;

		var (deck, themeCards) = BuildMaxDensity(
			$"{subtype}s",
			spells,
			values,
			c => c.HasSubtype(subtype)
		);

		var available = spells.Count(c => c.HasSubtype(subtype));
		Console.WriteLine(
			$"\n=== {setCode} / {subtype}: {available} in pool, {themeCards} theme cards in deck ===\n"
		);
		Console.WriteLine(deck.Format(index));

		var (rate, rows) = PlayAgainstField(deck, saved.Decks, index);
		Console.WriteLine();
		foreach (var (opponent, r) in rows)
			Console.WriteLine($"  vs {opponent, -14} {r, 6:P0}");

		Console.WriteLine(
			$"\n  MAX-DENSITY {subtype.ToUpperInvariant()} overall: {rate:P1}   "
				+ "(50% = as good as the field's own average)"
		);
	}
}
