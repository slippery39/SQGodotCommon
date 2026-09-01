using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Can a deck be optimised without stopping being itself?**
///
/// That is the whole question, and it is deliberately NOT "did the deck get better". Absolute win
/// rate is not the success criterion here — a Dragonstorm deck that loses to Zoo is still a
/// Dragonstorm deck, and an evolved pile that wins more while holding no archetype is the failure
/// this work exists to prevent. Win rate is the local gradient INSIDE an identity; it is not the
/// judge of whether the identity survived.
///
/// So the assertions are on cohesion and core-holding across generations, and the win rate is
/// printed as colour.
///
/// The hill climb is deliberately crude — accept a mutant that beats the parent against a fixed
/// opponent. `MetagameEvolver` is the real thing and is chaotic across seeds; reproducing its
/// accept rule here would import that chaos into a test whose claim has nothing to do with it.
/// </summary>
[TestFixture]
public class IdentityUnderOptimizationTests
{
	private const int Generations = 20;
	private const int MutantsPerGeneration = 3;
	private const int GamesPerEvaluation = 6;
	private const string Opponent = "Zoo";

	private static (
		PoolFeatures Features,
		IReadOnlyList<Card> Spells,
		ConstructedValues Values
	) Pool()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && dir.GetFiles("*.sln").Length == 0)
			dir = dir.Parent;
		if (dir is not null)
			Directory.SetCurrentDirectory(dir.FullName);

		var spells = SetRegistry.Combined.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		return (
			PoolFeatures.Build(spells),
			spells,
			ConstructedValuesStore.Load(SetRegistry.Combined.Code)
		);
	}

	[TestCase("Dragonstorm")]
	[TestCase("Tendrils of Agony")]
	[Explicit("Hill climbs a real deck over 20 generations. Minutes.")]
	public void TwentyGenerationsOfOptimisationDoNotDissolveTheIdentity(string payoff)
	{
		var (features, spells, values) = Pool();
		var index = ConstructedGameSetup.PoolIndex(spells);

		var request = DeckRequest.ForCards(payoff);
		var resolved = request.Resolve(features, spells, values, seed: 7);
		Assert.That(resolved.Deck, Is.Not.Null, string.Join("; ", resolved.Problems));

		var core = resolved.Core;
		var coreCards = core.Slots.SelectMany(s => s.Cards).ToHashSet(StringComparer.Ordinal);

		var deck = resolved.Deck!;
		var rng = new Random(99);
		var best = WinRate(deck, index, 0);

		double Cohesion(Decklist d) =>
			(double)d.Spells.Where(kv => coreCards.Contains(kv.Key)).Sum(kv => kv.Value)
			/ d.SpellCount;

		Console.WriteLine($"=== {payoff}: gen 0  {best:P1}  cohesion {Cohesion(deck):P0}");

		for (var gen = 1; gen <= Generations; gen++)
		{
			for (var m = 0; m < MutantsPerGeneration; m++)
			{
				var mutant = DeckBuilder.Mutate(
					deck,
					spells,
					values,
					rng,
					features: features,
					core: core
				);
				if (mutant is null)
					continue;

				// **Every proposal is checked, not just the accepted one.** A mutator that offers an
				// identity-breaking deck is broken whether or not the win rate happens to reject it.
				Assert.That(
					core.Holds(mutant),
					Is.True,
					$"gen {gen} proposed a deck missing its core: {string.Join(", ", core.Missing(mutant))}"
				);
				Assert.That(
					mutant.Spells.Keys.Where(n => !coreCards.Contains(n)),
					Is.Empty,
					$"gen {gen} proposed a card from outside the archetype"
				);

				var rate = WinRate(mutant, index, gen * 31 + m);
				if (rate > best)
					(deck, best) = (mutant, rate);
			}

			if (gen % 5 == 0)
				Console.WriteLine($"    gen {gen, 2}  {best:P1}  cohesion {Cohesion(deck):P0}");
		}

		Console.WriteLine($"    final: {deck.Lands} lands");
		foreach (
			var kv in deck
				.Spells.OrderBy(kv => spells.First(c => c.Name == kv.Key).ManaCost)
				.ThenBy(kv => kv.Key, StringComparer.Ordinal)
		)
			Console.WriteLine(
				$"      {kv.Value}x [{spells.First(c => c.Name == kv.Key).ManaCost}] {kv.Key}"
			);

		Assert.Multiple(() =>
		{
			Assert.That(core.Holds(deck), Is.True, string.Join(", ", core.Missing(deck)));
			Assert.That(
				deck.CopiesOf(payoff),
				Is.GreaterThan(0),
				"the named card was optimised out"
			);
			Assert.That(
				Cohesion(deck),
				Is.EqualTo(1.0).Within(0.001),
				"the deck drifted off-theme"
			);
			Assert.That(deck.Validate(), Is.Null);
		});
	}

	/// <summary>
	/// **The negative control: the same walk with no core must be ABLE to dissolve the identity.**
	///
	/// Without it the test above passes on a mutator too timid to change anything, and "the identity
	/// survived" is indistinguishable from "nothing happened".
	///
	/// **This accepts every mutant rather than hill climbing, and the first version did not — which
	/// produced a real finding.** Under a fitness-guided walk the unconstrained mutator kept the
	/// storm deck 100% on-theme for all 20 generations and never cut Dragonstorm. That is not the
	/// pool lock working, because the pool lock was off: it is `SupportScore` already paying
	/// `SupportBonus` for a card the deck answers and `DeadCardPenalty` against one it does not, so
	/// `Fill` prefers on-theme cards in a deck that is already on-theme. The soft pressure and the
	/// hard lock were doing the same job, and the fitness never had to choose.
	///
	/// So the honest claim is narrower than "unconstrained optimisation dissolves the deck": it is
	/// that unconstrained mutation CAN leave the archetype, which a random walk shows and a hill
	/// climb from a good starting deck does not. The stronger claim was mine and it was wrong.
	/// </summary>
	[Test]
	[Explicit("The control for the test above. Minutes.")]
	public void WithoutACoreTheSameWalkCanLeaveTheArchetype()
	{
		var (features, spells, values) = Pool();

		var resolved = DeckRequest
			.ForCards("Dragonstorm")
			.Resolve(features, spells, values, seed: 7);
		var coreCards = resolved
			.Core.Slots.SelectMany(s => s.Cards)
			.ToHashSet(StringComparer.Ordinal);

		var deck = resolved.Deck!;
		var rng = new Random(99);

		for (var gen = 1; gen <= Generations * MutantsPerGeneration; gen++)
			deck = DeckBuilder.Mutate(deck, spells, values, rng, features: features) ?? deck;

		var offTheme = deck.Spells.Where(kv => !coreCards.Contains(kv.Key)).Sum(kv => kv.Value);
		Console.WriteLine(
			$"unconstrained random walk: {offTheme}/{deck.SpellCount} off-theme, "
				+ $"Dragonstorm {deck.CopiesOf("Dragonstorm")}x"
		);

		Assert.That(
			offTheme,
			Is.GreaterThan(0),
			"unconstrained mutation never left the archetype, so the constrained run proves nothing"
		);
	}

	private static double WinRate(Decklist deck, IReadOnlyDictionary<string, Card> index, int seed)
	{
		int wins = 0,
			games = 0;

		for (var i = 0; i < GamesPerEvaluation; i++)
		{
			var onPlay = i % 2 == 0;
			// Seeded from the generation only, never from which candidate is playing, so a parent
			// and its mutants meet identical shuffles — the common-random-numbers device
			// `MetagameEvolver.Seed` depends on.
			var s = 90_000 + seed * 13 + i * 7;

			var (state, ids, names) = GameSetup.FromDecks(
				owner =>
					onPlay ? deck.Materialize(owner, index) : DeckRegistry.Build(Opponent, owner),
				owner =>
					onPlay ? DeckRegistry.Build(Opponent, owner) : deck.Materialize(owner, index)
			);

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
				wins++;
			games++;
		}

		return (double)wins / games;
	}
}
