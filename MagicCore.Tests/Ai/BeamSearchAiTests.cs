using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Ai;

public class BeamSearchAiTests
{
	GameState s = null!;
	int me;
	int them;

	[SetUp]
	public void OurFirstMain_EmptyHands()
	{
		s = TestGames.EmptyHands(TestGames.InFirstMain());
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
	}

	GameState WithLife(GameState state, int player, int life) =>
		state.WithPlayer(state.GetPlayer(player) with { Life = life });

	/// <summary>The search decides for us, <paramref name="opponent"/> for them, until the turn passes <paramref name="lastTurn"/>.</summary>
	GameState PlayThrough(GameState state, int lastTurn, IAi opponent) =>
		TestGames.PlayAis(state, lastTurn, new BeamSearchAi(me), opponent);

	[Test]
	public void ItTakesLethal_OnBoard()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Ours", "3/3"), ZoneType.Battlefield);
		s = WithLife(s, them, 3);

		s = PlayThrough(s, s.GetGame().TurnNumber, new GreedyAi(them));

		Assert.That(s.WinnerId(), Is.EqualTo(me));
	}

	// Their 4/4 attacks us at 3 life: the search must see that blocking with the 1/1 is survival.
	[Test]
	public void FacingLethal_ItChumpBlocks()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Chump", "1/1"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "4/4"), ZoneType.Battlefield);
		s = WithLife(s, me, 3);

		s = PlayThrough(s, s.GetGame().TurnNumber + 1, new GreedyAi(them));

		Assert.That(s.GetPlayer(me).HasLost, Is.False);
	}

	// The cost count (AI-6) reaches every thread a decision's work runs on: on every core or all on the
	// deciding thread, the same decision costs the same — a worker the count missed would make them differ.
	[Test]
	public void TheSameDecision_CostsTheSame_HoweverTheThreadsRun()
	{
		var game = MagicRules.NewGame(
			FraDecks.All["Red-Green"],
			FraDecks.All["White-Black"],
			seed: 2
		);
		var greedy = new[] { game.Player1Id(), game.Player2Id() }.ToDictionary(
			p => p,
			p => new GreedyAi(p)
		);
		(long Steps, long Rollouts) Cost(GameState at, int? threads = null)
		{
			var search = new BeamSearchAi(
				at.DecidingPlayerId(),
				new BeamSearchAi.Settings(CommitPlans: false, Threads: threads)
			);
			var (_, steps) = MagicRules.CountingSteps(() => search.Decide(at));
			return (steps, search.Rollouts);
		}
		// The first decision from turn 4 that searches widely enough to spread over threads.
		var first = (Steps: 0L, Rollouts: 0L);
		for (var i = 0; i < 2_000 && !game.IsOver(); i++)
		{
			game = game.AutoPass();
			if (game.GetGame().TurnNumber >= 4 && (first = Cost(game)).Rollouts >= 20)
				break;
			game = greedy[game.DecidingPlayerId()].Decide(game).State;
		}

		var costs = Enumerable
			.Range(0, 4)
			.Select(_ => Cost(game))
			.Prepend(first)
			.Append(Cost(game, threads: 1))
			.ToList();

		Assert.That(costs.Distinct(), Has.Exactly(1).Items, string.Join(", ", costs));
		Assert.That(first.Rollouts, Is.GreaterThanOrEqualTo(20), "a decision that really searched");
		Assert.That(
			first.Steps,
			Is.GreaterThan(first.Rollouts * 10),
			"every rollout's steps counted"
		);
	}

	[Test]
	public void TheSamePosition_GetsTheSameDecision()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Ours", "3/3"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "2/4"), ZoneType.Battlefield);

		Assert.That(
			new BeamSearchAi(me).Decide(s).What,
			Is.EqualTo(new BeamSearchAi(me).Decide(s).What)
		);
	}

	// The table's time cap gates the levels below the root, decision by decision over a real game:
	// with no time at all only the root is searched (a one-level search's decisions); with time to
	// spare, every level is (an uncapped search's) — an estimate that always overshoots fails this.
	[TestCase(0, true, 25)]
	[TestCase(3600, false, 12)]
	public void TheTimeCap_GatesTheLevelsBelowTheRoot(int seconds, bool rootOnly, int decisions)
	{
		var game = MagicRules.NewGame(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], 1);
		var p = game.Player1Id();
		var capped = new BeamSearchAi(
			p,
			new BeamSearchAi.Settings(TimeLimit: TimeSpan.FromSeconds(seconds))
		);
		var uncapped = new BeamSearchAi(
			p,
			rootOnly ? new BeamSearchAi.Settings(Depth: 1) : new BeamSearchAi.Settings()
		);
		var other = new GreedyAi(game.Player2Id());

		for (var searched = 0; searched < decisions && !game.IsOver(); )
		{
			game = game.AutoPass();
			if (game.IsOver())
				break;
			if (game.DecidingPlayerId() != p)
			{
				game = other.Decide(game).State;
				continue;
			}
			var (next, what) = uncapped.Decide(game);
			Assert.That(capped.Decide(game).What, Is.EqualTo(what), $"decision {searched}");
			(game, searched) = (next, searched + 1);
		}
	}

	// A planned move is re-found by MoveKey, so everything that tells two legal moves apart must be in
	// it — MtgSimulator replayed a planned X=4 as X=0 because X was not.
	[Test]
	public void MoveKey_TellsXApart()
	{
		int blast;
		(s, blast) = TestGames.Put(
			s,
			me,
			TestGames.Inline("Blast", "{X}{R}", "Sorcery"),
			ZoneType.Hand
		);

		var x1 = new CastSpellAction
		{
			PlayerId = me,
			CardId = blast,
			X = 1,
		};
		var x4 = new CastSpellAction
		{
			PlayerId = me,
			CardId = blast,
			X = 4,
		};

		Assert.That(s.MoveKey(x1), Is.Not.EqualTo(s.MoveKey(x4)));
		Assert.That(s.MoveKey(x1), Is.EqualTo(s.MoveKey(x1 with { })));
	}

	// A loyalty move's targets are an array, which a record compares by reference — a planned +1 or
	// −X was never re-found until MoveKey spelled it out.
	[Test]
	public void MoveKey_ReFindsALoyaltyMove_AndTellsXApart()
	{
		var planned = new ActivateLoyaltyAction
		{
			PlayerId = me,
			CardId = 7,
			AbilityIndex = 1,
			X = 2,
			Targets = [5],
		};
		var regenerated = planned with { Targets = [5] };

		Assert.That(s.MoveKey(regenerated), Is.EqualTo(s.MoveKey(planned)));
		Assert.That(s.MoveKey(planned with { X = 3 }), Is.Not.EqualTo(s.MoveKey(planned)));
	}

	// Deterministic, and a mirror pair is one game with the names swapped: exactly half.
	[Test, Explicit("plays 12 games of the search against itself")]
	public void TheSearchAgainstItself_ScoresExactlyHalf()
	{
		var result = AiMatch.Run("beam", "beam", games: 12);
		TestContext.Out.WriteLine(result.Report());

		Assert.That(result.Games.All(g => g.Finished), result.Report());
		Assert.That(result.WinRateA, Is.EqualTo(0.5), result.Report());
	}
}
