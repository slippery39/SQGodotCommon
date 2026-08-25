using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;
using MtgSimulator.Scenarios;

namespace MtgSimulator.Tests;

/// <summary>
/// End-to-end for the standalone viewer: capture a position, write it, read it back from disk,
/// and have several strategies decide in it.
///
/// StateJsonTests proves the state survives the trip. This proves the trip is actually useful —
/// that a file on disk produces a comparison, which is the thing the viewer exists to show.
/// </summary>
[TestFixture]
public class ScenarioViewerTests
{
	private string _dir = null!;

	[SetUp]
	public void Setup()
	{
		_dir = Path.Combine(Path.GetTempPath(), "mtg_scenarios_" + Guid.NewGuid().ToString("N"));
	}

	[TearDown]
	public void TearDown()
	{
		if (Directory.Exists(_dir))
			Directory.Delete(_dir, recursive: true);
	}

	/// <summary>
	/// NOTE: BeginGame shuffles, and nothing here seeds that shuffle, so the board reached varies
	/// between runs even at a fixed seed. That non-determinism is what surfaced the
	/// ImmutableList&lt;int&gt; serialization gap — as an intermittent failure in this fixture,
	/// which is a bad way to learn about it. StateJsonTests.ManyPlayedGames_AllRoundTrip now sweeps
	/// seeds deliberately instead of relying on this to stumble into coverage.
	/// </summary>
	private static (GameState State, MtgGameIds Ids) MidGame(int seed, int actions)
	{
		var (state, ids) = MtgGameFactory.Create();
		var rng = new Random(seed);

		foreach (
			var (playerId, libraryId, deckSeed) in new[]
			{
				(ids.Player1Id, ids.Player1LibraryId, seed),
				(ids.Player2Id, ids.Player2LibraryId, seed + 1),
			}
		)
		foreach (
			var card in CardPool.BuildRandomDeck(
				playerId,
				CardLibrary.All,
				rng: new Random(deckSeed)
			)
		)
			(state, _) = state.AddObject(card, parentId: libraryId);

		(state, _) = state.BeginGame(ids.GameId, ids.Player1Id, ids.Player2Id);

		for (var i = 0; i < actions; i++)
		{
			var game = state.TryGetGame();
			if (game == null || state.GetPlayer(ids.Player1Id).HasLost)
				break;
			if (state.IsWaitingForChoice)
			{
				var choice = state.GetPendingChoice();
				if (choice == null || choice.Options.Count == 0)
					break;
				(state, _) = state.ResolveChoice(
					choice
						.Options.Take(Math.Max(1, choice.MinChoices))
						.Select(o => o.Id)
						.ToImmutableList()
				);
				continue;
			}
			var legal = MtgActionGenerator.GetLegalActions(state, ids, game.ActivePlayerId);
			if (legal.Count == 0)
				break;
			(state, _) = state.AddAction(legal[rng.Next(legal.Count)]).ProcessAllActions();
		}

		return (state, ids);
	}

	[Test]
	public void AScenario_SurvivesTheDiskRoundTrip()
	{
		var (state, ids) = MidGame(seed: 21, actions: 24);
		var scenario = Scenario.Capture(state, ids.Player1Id, "land-vent", "AI vents a land here");

		var path = ScenarioStore.Save(scenario, _dir);
		var loaded = ScenarioStore.Load(path);

		Assert.Multiple(() =>
		{
			Assert.That(loaded.Name, Is.EqualTo("land-vent"));
			Assert.That(loaded.Note, Is.EqualTo("AI vents a land here"));
			Assert.That(loaded.PlayerToMove, Is.EqualTo(ids.Player1Id));
			Assert.That(
				StateEvaluator.Evaluate(loaded.State, ids, ids.Player1Id),
				Is.EqualTo(StateEvaluator.Evaluate(state, ids, ids.Player1Id)),
				"a scenario that scores differently is a different position"
			);
		});
	}

	[Test]
	public void TheIdsAreRecoverable_FromTheStateAlone()
	{
		// A scenario file does not store MtgGameIds; the viewer rebuilds them from the state's own
		// well-known registry. If that drifts, every loaded scenario reads the wrong zones and the
		// numbers are quietly about someone else's board.
		var (state, ids) = MidGame(seed: 4, actions: 12);

		var rebuilt = ScenarioComparer.MtgGameIdsFor(
			ScenarioStore
				.Load(ScenarioStore.Save(Scenario.Capture(state, ids.Player1Id, "ids", ""), _dir))
				.State
		);

		Assert.That(rebuilt, Is.EqualTo(ids));
	}

	[Test]
	public void SeveralStrategies_DecideInTheSamePosition()
	{
		var (state, ids) = MidGame(seed: 33, actions: 26);
		var scenario = Scenario.Capture(state, ids.Player1Id, "compare", "several AIs, one board");

		var loaded = ScenarioStore.Load(ScenarioStore.Save(scenario, _dir));
		var results = ScenarioComparer.Compare(loaded, ScenarioConsole.Strategies, ids);

		Assert.Multiple(() =>
		{
			Assert.That(results, Has.Count.EqualTo(ScenarioConsole.Strategies.Count));
			Assert.That(
				results.Where(r => r.Error != null).Select(r => $"{r.StrategyName}: {r.Error}"),
				Is.Empty,
				"no strategy should throw on a legally reachable position"
			);
			Assert.That(
				results.Select(r => r.ChosenAction),
				Has.All.Not.Empty,
				"every strategy must pick something"
			);
		});

		// Not an assertion — the table is the deliverable, so print it and let a failing run show
		// what the viewer would have displayed.
		TestContext.Out.WriteLine(ScenarioComparer.Format(loaded, results));
	}

	[Test]
	public void TheComparison_RendersEveryEvaluatorTerm()
	{
		var (state, ids) = MidGame(seed: 8, actions: 20);
		var scenario = Scenario.Capture(state, ids.Player1Id, "terms", "");

		var results = ScenarioComparer.Compare(scenario, ScenarioConsole.Strategies, ids);
		var table = ScenarioComparer.Format(scenario, results);

		// The term rows are the point of the viewer; a table that silently loses them is the
		// "score 4.2 tells you nothing" failure with extra steps.
		Assert.Multiple(() =>
		{
			foreach (var term in new[] { "life", "creatures", "power", "hand", "mana", "race" })
				Assert.That(table, Does.Contain(term), $"the '{term}' row must render");
			Assert.That(table, Does.Contain("total"));
			foreach (var (name, _) in ScenarioConsole.Strategies)
				Assert.That(table, Does.Contain(name));
		});
	}
}
