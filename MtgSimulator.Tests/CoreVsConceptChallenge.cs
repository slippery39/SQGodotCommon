using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Does a payoff-anchored `DeckCore` build a better deck than the demand-anchored `SeedConcept`
/// it replaced?** Same payoff, two builders, judged against the hand-built precons.
///
/// **The precons are the yardstick because a closed field has none.** A round-robin averages
/// exactly 50% by construction, so an evolved field cannot tell "my decks are good" from "my decks
/// are equally mediocre" — measured, an evolved field lost to plain Zoo by 16pp while every
/// internal metric reported health. `DeckRegistry` holds decks a human wrote and believes in, and
/// two of them (Dragonstorm, Traditional Storm) are hand-built versions of archetypes these cores
/// describe, which makes them a ceiling as well as an opponent.
///
/// **Why not A/B a whole mode 6 run:** that mode is chaotic. One different game outcome changes
/// which mutant is accepted and therefore the entire field, and the noise floor across seeds has
/// never been measured — so a single-run comparison is an anecdote and a readable one costs several
/// seeds per arm. This isolates the thing that actually changed, which is how a deck gets BUILT
/// from a concept, and leaves evolution out of the loop entirely.
///
/// Both arms play identical opponents on identical seeds — common random numbers, the same device
/// `MetagameEvolver.Seed` relies on — so shuffle and search variance cancel in the difference.
/// </summary>
[TestFixture]
public class CoreVsConceptChallenge
{
	private const int GamesPerOpponent = 20;

	/// <summary>
	/// Payoffs taken from the top of a mode 7 blank-first ranking, plus Atog as a control: it has a
	/// single demand, so both builders see the same problem and the arms SHOULD tie. Without a case
	/// like that, a difference everywhere is indistinguishable from a harness that favours one arm.
	/// </summary>
	private static readonly string[] Payoffs =
	[
		"Dragonstorm",
		"Zombie Apocalypse",
		"Goblin Lackey",
		"Angel of Second Rites",
		"Atog",
	];

	private static void ChdirToSolutionRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && dir.GetFiles("*.sln").Length == 0)
			dir = dir.Parent;
		Assert.That(dir, Is.Not.Null, "could not find the solution root");
		Directory.SetCurrentDirectory(dir!.FullName);
	}

	[Test]
	[Explicit("Runs ~1600 constructed games against the precon gauntlet. Minutes.")]
	public void CoreBuiltDecksAgainstThePreconGauntlet()
	{
		ChdirToSolutionRoot();

		var set = SetRegistry.Combined;
		var spells = set.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var index = ConstructedGameSetup.PoolIndex(spells);
		var values = ConstructedValuesStore.Load(set.Code);
		var features = PoolFeatures.Build(spells);

		var opponents = DeckRegistry.All.Select(d => d.Name).ToList();
		var seError = Math.Sqrt(0.25 / (GamesPerOpponent * opponents.Count)) * 100;

		Console.WriteLine(
			$"MinLands {Decklist.MinLands}, {opponents.Count} precon opponents, "
				+ $"{GamesPerOpponent} games each = {GamesPerOpponent * opponents.Count} per arm. "
				+ $"1 SE = {seError:F1}pp — treat anything under {2 * seError:F1}pp as noise."
		);
		Console.WriteLine();
		Console.WriteLine($"{"payoff", -24}{"CORE", 10}{"CONCEPT", 10}{"delta", 9}");

		foreach (var payoff in Payoffs)
		{
			var core = DeckCore.For(features, payoff);
			if (core is null)
			{
				Console.WriteLine($"{payoff, -24}  no core");
				continue;
			}

			var coreDeck = BuildFromCore(core, spells, values);

			// The old path, given its BEST shot: the narrowest demand this payoff asks, which is the
			// most archetype-like single concept available to it. Anchoring on a broad one would be
			// rigging the comparison.
			var narrowest = features
				.DemandsOf(payoff)
				.Where(features.Informative)
				.OrderBy(features.SuppliersInPool)
				.Cast<int?>()
				.FirstOrDefault();

			var conceptDeck = narrowest is null
				? null
				: DeckBuilder.SeedConcept(
					$"Concept-{payoff}",
					spells,
					values,
					features,
					new Random(4242),
					narrowest.Value
				);

			var coreRate = Play(coreDeck, opponents, index);
			var conceptRate = conceptDeck is null
				? double.NaN
				: Play(conceptDeck, opponents, index);

			Console.WriteLine(
				$"{payoff, -24}{coreRate, 10:P1}{conceptRate, 10:P1}"
					+ $"{(coreRate - conceptRate) * 100, 9:+0.0;-0.0}"
			);
			Console.WriteLine($"      core:    {Describe(coreDeck)}");
			if (conceptDeck is not null)
				Console.WriteLine($"      concept: {Describe(conceptDeck)}");
		}

		// Reference ceiling: what a human-written deck scores against the same field.
		Console.WriteLine();
		foreach (var name in new[] { "Dragonstorm", "Traditional Storm", "Zoo" })
		{
			var others = opponents.Where(o => o != name).ToList();
			Console.WriteLine($"  PRECON {name, -22}{PlayPrecon(name, others), 8:P1}");
		}
	}

	/// <summary>
	/// Builds the same decks the gauntlet plays and prints how much of each is CORE against FLEX —
	/// no games, so it is seconds rather than minutes.
	///
	/// Written because the first report's decklist line sorted alphabetically and every card is a
	/// 4-of, so it showed the first six card names and said nothing about whether the archetype was
	/// present at all. A summary that cannot distinguish "the core is there and the archetype is
	/// weak" from "the core is missing" is not a diagnosis.
	/// </summary>
	[Test]
	[Explicit("Diagnostic — builds the gauntlet decks and prints core vs flex. No games.")]
	public void WhatDoesTheCoreBuilderActuallyBuild()
	{
		ChdirToSolutionRoot();

		var spells = SetRegistry.Combined.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var values = ConstructedValuesStore.Load(SetRegistry.Combined.Code);
		var features = PoolFeatures.Build(spells);

		foreach (var payoff in Payoffs)
		{
			var core = DeckCore.For(features, payoff);
			if (core is null)
				continue;

			var seed = Decklist.Empty($"Core-{payoff}") with
			{
				Lands = DeckBuilder.LandsForConcept(
					spells.Where(c => core.Slots.SelectMany(s => s.Cards).Contains(c.Name)),
					new Random(4242)
				),
			};

			foreach (
				var (label, deck) in new[]
				{
					("FORMAT-FILLED", BuildFromCore(core, spells, values)),
					("ARCHETYPE-FILLED", core.Complete(seed, spells, values, features)),
				}
			)
			{
				var coreCards = core
					.Slots.SelectMany(s => s.Cards)
					.ToHashSet(StringComparer.Ordinal);
				var onTheme = deck
					.Spells.Where(kv => coreCards.Contains(kv.Key))
					.Sum(kv => kv.Value);
				var dead = features.DeadCards(deck);

				Console.WriteLine(
					$"\n=== {payoff} [{label}]  {deck.Lands} lands, {deck.SpellCount} spells\n"
						+ $"    COHESION: {(double)onTheme / deck.SpellCount:P0} on-theme, "
						+ $"{dead.Count} dead card(s), payoff {deck.CopiesOf(payoff)}x, "
						+ $"holds {core.Holds(deck)}"
				);
				Console.WriteLine(
					"    OFF-THEME: "
						+ (
							deck.Spells.Where(kv => !coreCards.Contains(kv.Key)).Any()
								? string.Join(
									", ",
									deck.Spells.Where(kv => !coreCards.Contains(kv.Key))
										.Select(kv => $"{kv.Value}x {kv.Key}")
								)
								: "(none)"
						)
				);

				// The whole list, ordered by mana cost — the form a human reads a deck in.
				if (label.StartsWith("ARCHETYPE"))
					foreach (
						var kv in deck
							.Spells.OrderBy(kv => spells.First(c => c.Name == kv.Key).ManaCost)
							.ThenBy(kv => kv.Key, StringComparer.Ordinal)
					)
						Console.WriteLine(
							$"      {kv.Value}x [{spells.First(c => c.Name == kv.Key).ManaCost}] {kv.Key}"
						);
			}
		}
	}

	private static string Describe(Decklist deck) =>
		$"{deck.Lands} lands, "
		+ string.Join(
			", ",
			deck.Spells.OrderByDescending(kv => kv.Value)
				.Take(6)
				.Select(kv => $"{kv.Value}x {kv.Key}")
		);

	private static Decklist BuildFromCore(
		DeckCore core,
		IReadOnlyList<Card> spells,
		ConstructedValues values
	)
	{
		var coreCards = core.Slots.SelectMany(s => s.Cards).ToHashSet(StringComparer.Ordinal);
		var lands = DeckBuilder.LandsForConcept(
			spells.Where(c => coreCards.Contains(c.Name)),
			new Random(4242)
		);

		var deck = core.Satisfy(Decklist.Empty($"Core-{core.Name}") with { Lands = lands }, values);

		foreach (
			var card in spells
				.OrderByDescending(c => values.CardDelta(c.Name))
				.ThenBy(c => c.Name, StringComparer.Ordinal)
		)
		{
			var need = Decklist.DeckSize - deck.Lands - deck.SpellCount;
			if (need <= 0)
				break;
			var have = deck.CopiesOf(card.Name);
			if (have >= Decklist.MaxCopies)
				continue;
			deck = deck.WithCopies(card.Name, Math.Min(Decklist.MaxCopies, have + need));
		}

		return deck;
	}

	/// <summary>
	/// A seed that is the same in every process.
	///
	/// **`string.GetHashCode` is randomized per process in .NET**, so seeding from it made every run
	/// of this harness sample a different set of shuffles — the CONCEPT arm, whose code path did not
	/// change at all, moved 6.1% → 7.2% and 3.9% → 6.1% between two runs that should have been
	/// byte-identical. Same class as the wall-clock non-determinism that made mode 6 irreproducible:
	/// the number moved for a reason that had nothing to do with the thing being measured.
	/// </summary>
	private static int SeedFor(string opponent, int game)
	{
		var hash = 17;
		foreach (var c in opponent)
			hash = hash * 31 + c;
		return 61_000 + Math.Abs(hash % 1000) + game * 7;
	}

	private static double Play(
		Decklist deck,
		IReadOnlyList<string> opponents,
		IReadOnlyDictionary<string, Card> index
	)
	{
		if (deck.Validate() is { } invalid)
		{
			Console.WriteLine($"      INVALID: {invalid}");
			return double.NaN;
		}

		int wins = 0,
			games = 0;

		foreach (var opp in opponents)
			for (var i = 0; i < GamesPerOpponent; i++)
			{
				var onPlay = i % 2 == 0;
				// Seeded from the opponent and the game index only — NOT from which arm is playing —
				// so both builders meet identical shuffles.
				var seed = SeedFor(opp, i);

				var (state, ids, names) = GameSetup.FromDecks(
					owner =>
						onPlay ? deck.Materialize(owner, index) : DeckRegistry.Build(opp, owner),
					owner =>
						onPlay ? DeckRegistry.Build(opp, owner) : deck.Materialize(owner, index)
				);

				var runner = new GameRunner(
					new MultiTurnBeamSearchAiStrategy(
						ids,
						2,
						rng: new Random(seed + 1),
						cardValues: AiCardValues.Current
					),
					new MultiTurnBeamSearchAiStrategy(
						ids,
						2,
						rng: new Random(seed + 2),
						cardValues: AiCardValues.Current
					)
				);

				var (result, _) = runner.Run(state, ids, names, seed + 3, seed + 4);
				if (onPlay ? result.IsPlayer1Win : result.IsPlayer2Win)
					wins++;
				games++;
			}

		return (double)wins / games;
	}

	private static double PlayPrecon(string name, IReadOnlyList<string> opponents)
	{
		int wins = 0,
			games = 0;

		foreach (var opp in opponents)
			for (var i = 0; i < GamesPerOpponent; i++)
			{
				var onPlay = i % 2 == 0;
				var seed = SeedFor(opp, i);

				var (state, ids, names) = GameSetup.FromDecks(
					owner => DeckRegistry.Build(onPlay ? name : opp, owner),
					owner => DeckRegistry.Build(onPlay ? opp : name, owner)
				);

				var runner = new GameRunner(
					new MultiTurnBeamSearchAiStrategy(
						ids,
						2,
						rng: new Random(seed + 1),
						cardValues: AiCardValues.Current
					),
					new MultiTurnBeamSearchAiStrategy(
						ids,
						2,
						rng: new Random(seed + 2),
						cardValues: AiCardValues.Current
					)
				);

				var (result, _) = runner.Run(state, ids, names, seed + 3, seed + 4);
				if (onPlay ? result.IsPlayer1Win : result.IsPlayer2Win)
					wins++;
				games++;
			}

		return (double)wins / games;
	}
}
