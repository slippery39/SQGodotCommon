using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Which cards in the hand-built elf deck are actually pulling their weight?**
///
/// A sanity check on the deck the whole (c)/(f) argument rests on. `CMB Elves` beats the builder's
/// own elf slot 65-35 from the same pool, and that has been used all session as the known answer —
/// but "this deck is better" says nothing about WHICH cards make it better. Wirewood Conduit in
/// particular is asserted to matter on the strength of being in the winning list and nothing else:
/// `OutputProbe` rates it slightly BELOW its slot average, and the builder still never proposes it.
/// One of those readings is wrong and this is the cheapest instrument that can say which.
///
/// **Games-in-hand, the same measure `DraftTrainingData` uses**: a card is credited for a game only
/// if it was actually drawn, so a card sitting in the library does not collect the deck's results.
///
/// Two limits worth holding while reading it, both structural rather than fixable by more games:
///
/// - **Within ONE deck the spread is compressed.** Every card here is in every game's deck, so the
///   only variation is which were drawn — and a 4-of is drawn in most games. Read the ORDER, not
///   the magnitudes.
/// - **It cannot separate "good card" from "good in the games it happened to be drawn in".** That
///   is the same conditioning caveat this project already records for the constructed table.
/// </summary>
[TestFixture]
public class GauntletCardStatsTests
{
	private const int GamesPerOpponent = 20;

	[Test]
	[Explicit("Diagnostic — plays a few hundred games.")]
	public void WhichCardsCarryTheHandBuiltElfDeck()
	{
		Directory.SetCurrentDirectory(
			Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."))
		);

		var pool = SetRegistry
			.Designed.Cards.Where(c => !c.HasSubtype("Land"))
			.ToDictionary(c => c.Name, c => c, StringComparer.Ordinal);

		var field = DecklistStore
			.Load(Directory.GetFiles("sim_results", "metagame_des_*.json").OrderBy(f => f).Last())!
			.Decks;

		var deckName = DesignedGauntletDecks.Elves;
		var deckSpells = Gauntlet.SpellNames(deckName).ToList();

		var schedule = new List<(int Opponent, int Seed, bool OnPlay)>();
		for (var o = 0; o < field.Count; o++)
		for (var k = 0; k < GamesPerOpponent; k++)
			schedule.Add((o, 90_000 + (o * 1009 + k) * 7, k % 2 == 0));

		// Parallel into a pre-allocated array, folded sequentially — the project's standing rule so
		// that thread completion order cannot reach a result.
		var results = new (bool Won, IReadOnlyList<string> Drawn)[schedule.Count];
		Parallel.For(
			0,
			schedule.Count,
			i =>
			{
				var (o, seed, onPlay) = schedule[i];
				var buildElves = (int owner) => DeckRegistry.Build(deckName, owner);
				var buildOpp = (int owner) => field[o].Materialize(owner, pool);

				var (state, ids, cardNames) = GameSetup.FromDecks(
					onPlay ? buildElves : buildOpp,
					onPlay ? buildOpp : buildElves
				);
				var rng = new Random(seed + 4);
				var runner = new GameRunner(
					new MultiTurnBeamSearchAiStrategy(
						ids,
						2,
						rng: rng,
						cardValues: AiCardValues.Current
					),
					new MultiTurnBeamSearchAiStrategy(
						ids,
						2,
						rng: rng,
						cardValues: AiCardValues.Current
					)
				);
				var (r, _) = runner.Run(state, ids, cardNames, seed + 2, seed + 3);

				results[i] = (
					onPlay ? r.IsPlayer1Win : r.IsPlayer2Win,
					onPlay ? r.Player1DrawnCards : r.Player2DrawnCards
				);
			}
		);

		var acc = new CardStatAccumulator();
		foreach (var (won, drawn) in results)
			acc.Add(drawn, deckSpells, won);

		var data = acc.ToData();
		var prior = (double)data.Wins / data.Perspectives;

		TestContext.Out.WriteLine(
			$"  {deckName}: {data.Wins}/{data.Perspectives} = {prior:P1} base rate\n"
		);
		TestContext.Out.WriteLine($"  {"card", -26}{"GIH", 8}{"games", 8}{"delta", 9}");

		foreach (
			var c in data
				.Cards.Where(c => c.Games > 0)
				.OrderByDescending(c =>
					DraftTrainingData.Shrink(c.Wins, c.Games, prior, 25) - prior
				)
		)
			TestContext.Out.WriteLine(
				$"  {c.Name, -26}{(double)c.Wins / c.Games, 8:P0}{c.Games, 8}"
					+ $"{100 * (DraftTrainingData.Shrink(c.Wins, c.Games, prior, 25) - prior), 9:+0.0;-0.0}"
			);
	}
}
