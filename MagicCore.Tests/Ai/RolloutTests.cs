using System.Diagnostics;
using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Ai;

public class RolloutTests
{
	GameState s = null!;
	int me;
	int them;

	[SetUp]
	public void OurFirstMain_EmptyHands()
	{
		s = TestGames.InFirstMain();
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
		foreach (var p in new[] { me, them })
		foreach (var card in s.CardsIn(p, ZoneType.Hand).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(p, ZoneType.Library)); // TEST SETUP: nothing to cast
	}

	double Score(GameState state) =>
		Rollout.Score(state, me, Rollout.Horizon(state, Rollout.Default), Rollout.Default);

	GameState WithLife(GameState state, int player, int life) =>
		state.WithPlayer(state.GetPlayer(player) with { Life = life });

	[Test]
	public void ItAttacksForLethal_OnBoard()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Ours", "3/3"), ZoneType.Battlefield);
		s = WithLife(s, them, 3);

		Assert.That(Score(s), Is.GreaterThan(5_000));
	}

	// BoardOnly: the opponent attacks with its board — a rollout that never let it would see no danger.
	[Test]
	public void ItSeesTheOpponentAttackForLethal()
	{
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "4/4"), ZoneType.Battlefield);
		s = WithLife(s, me, 3);

		Assert.That(Score(s), Is.LessThan(-5_000));
	}

	// Blocks are judged by what they lead to: facing lethal, a chump block keeps us alive.
	[Test]
	public void FacingLethal_WeChumpBlock()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Chump", "1/1"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "4/4"), ZoneType.Battlefield);
		s = WithLife(s, me, 3);

		Assert.That(Score(s), Is.GreaterThan(-5_000));
	}

	// The discount counts from the SEARCH's root, which the horizon fixes — so two candidates that lose
	// on the same turn score the same, though one of them had already passed into the next turn.
	[Test]
	public void ALossOnTheSameTurn_ScoresTheSame_WhereverTheRolloutStarted()
	{
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "4/4"), ZoneType.Battlefield);
		s = WithLife(s, me, 3);
		var horizon = Rollout.Horizon(s, Rollout.Default);
		var thisTurn = s.GetGame().TurnNumber;
		var theirTurn = TestGames.PassUntil(s, x => x.GetGame().TurnNumber > thisTurn);

		var fromHere = Rollout.Score(s, me, horizon, Rollout.Default);
		var fromTheirTurn = Rollout.Score(theirTurn, me, horizon, Rollout.Default);

		Assert.That(fromHere, Is.LessThan(-5_000));
		Assert.That(fromTheirTurn, Is.EqualTo(fromHere));
	}

	// A sooner win scores more (the terminal discount), and any win far more than any board.
	[Test]
	public void ASoonerWin_ScoresHigher()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Ours", "3/3"), ZoneType.Battlefield);

		var now = Score(WithLife(s, them, 3));
		var nextTurn = Score(WithLife(s, them, 6));

		Assert.That(now, Is.GreaterThan(nextTurn));
		Assert.That(nextTurn, Is.GreaterThan(5_000));
	}

	[Test]
	public void TheSamePosition_ScoresTheSame()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Ours", "3/3"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "2/4"), ZoneType.Battlefield);

		Assert.That(Score(s), Is.EqualTo(Score(s)));
	}

	// What one rollout costs — the unit the search's budget is counted in. Measured at every one of
	// GreedyAi's decisions in real games; recorded in docs/findings/magic-ai.md.
	[Test, Explicit("measurement: prints the cost of a rollout")]
	public void Cost_OfOneRollout()
	{
		var times = new List<double>();
		var decks = FraDecks.All.Keys.ToList();
		foreach (
			var (first, second) in decks.SelectMany(a =>
				decks.Where(b => b != a).Select(b => (a, b))
			)
		)
		{
			var g = MagicRules.NewGame(FraDecks.All[first], FraDecks.All[second], seed: 1);
			var ais = new[] { g.Player1Id(), g.Player2Id() }.ToDictionary(
				p => p,
				p => new GreedyAi(p)
			);
			for (var i = 0; i < 5_000 && !g.IsOver(); i++)
			{
				g = g.AutoPass();
				if (g.IsOver())
					break;
				var who = g.DecidingPlayerId();
				if (g.GetGame().Step != Step.PreGame && !g.IsWaitingForChoice)
				{
					var start = Stopwatch.GetTimestamp();
					Rollout.Score(g, who, Rollout.Horizon(g, Rollout.Default), Rollout.Default);
					times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
				}
				g = ais[who].Decide(g).State;
			}
		}
		times.Sort();
		double At(double q) => times[(int)(q * (times.Count - 1))];
		TestContext.Out.WriteLine(
			$"{times.Count} rollouts: mean {times.Average():0.0} ms, p50 {At(0.5):0.0}, p90 {At(0.9):0.0}, "
				+ $"p99 {At(0.99):0.0}, max {times[^1]:0.0} ms"
		);
	}
}
