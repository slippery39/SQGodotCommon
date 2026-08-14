using MtgCore;

namespace MtgSimulator.Tests;

[TestFixture]
public class DraftTournamentTests
{
	// Pools are never played in these tests — only the schedule and the tally are exercised,
	// and playing real games would make this a benchmark, not a unit test.
	private static DraftTournament Build(int seatCount = 8, int humanSeat = 0) =>
		new(
			Enumerable.Range(0, seatCount).Select(_ => (IReadOnlyList<Card>)[]).ToList(),
			seed: 1,
			humanSeat: humanSeat
		);

	// ===== Schedule =====

	[Test]
	public void Pairings_CoverEverySeatExactlyOncePerRound()
	{
		const int seats = 8;
		for (var round = 0; round < seats - 1; round++)
		{
			var seated = DraftTournament
				.Pairings(seats, round)
				.SelectMany(p => new[] { p.A, p.B })
				.OrderBy(s => s)
				.ToList();
			Assert.That(seated, Is.EqualTo(Enumerable.Range(0, seats)), $"round {round}");
		}
	}

	[Test]
	public void Pairings_CoverEveryPairExactlyOnceAcrossAllRounds()
	{
		const int seats = 8;
		var all = Enumerable
			.Range(0, seats - 1)
			.SelectMany(r => DraftTournament.Pairings(seats, r))
			.Select(p => (Math.Min(p.A, p.B), Math.Max(p.A, p.B)))
			.ToList();

		Assert.That(all, Has.Count.EqualTo(seats * (seats - 1) / 2));
		Assert.That(all.Distinct().Count(), Is.EqualTo(all.Count), "a pair was scheduled twice");
	}

	[Test]
	public void HumanPairing_MatchesTheSchedule()
	{
		var t = Build(humanSeat: 3);
		for (var round = 0; round < t.RoundCount; round++)
		{
			var (opponent, humanIsSeatA) = t.HumanPairing(round);
			var expected = humanIsSeatA ? (3, opponent) : (opponent, 3);
			Assert.That(DraftTournament.Pairings(8, round), Does.Contain(expected));
		}
	}

	// ===== Tally =====

	[Test]
	public void ApplyResults_CreditsWinnerAndLoser()
	{
		var t = Build();
		t.ApplyResults([new RoundResult(2, 5, WinnerSeat: 5)]);

		var standings = t.Standings().ToDictionary(s => s.Seat);
		Assert.Multiple(() =>
		{
			Assert.That(standings[5].Wins, Is.EqualTo(1));
			Assert.That(standings[5].Losses, Is.Zero);
			Assert.That(standings[2].Losses, Is.EqualTo(1));
			Assert.That(standings[2].Wins, Is.Zero);
		});
	}

	[Test]
	public void ApplyResults_DrawCreditsBothSeats()
	{
		var t = Build();
		t.ApplyResults([new RoundResult(1, 4, WinnerSeat: -1)]);

		var standings = t.Standings().ToDictionary(s => s.Seat);
		Assert.Multiple(() =>
		{
			Assert.That(standings[1].Draws, Is.EqualTo(1));
			Assert.That(standings[4].Draws, Is.EqualTo(1));
			Assert.That(standings[1].Wins + standings[1].Losses, Is.Zero);
		});
	}

	[Test]
	public void RecordHumanResult_CreditsTheScheduledOpponent()
	{
		var t = Build(humanSeat: 0);
		var (opponent, _) = t.HumanPairing(0);
		t.RecordHumanResult(round: 0, humanWon: true, isDraw: false);

		var standings = t.Standings().ToDictionary(s => s.Seat);
		Assert.Multiple(() =>
		{
			Assert.That(standings[0].Wins, Is.EqualTo(1));
			Assert.That(standings[opponent].Losses, Is.EqualTo(1));
		});
	}

	/// A full event must leave every seat having played every round, with wins and losses
	/// balanced — this is the invariant that catches a seat being scored twice or skipped.
	[Test]
	public void FullTournament_BalancesWinsAgainstLossesAndGamesPlayed()
	{
		var t = Build();
		for (var round = 0; round < t.RoundCount; round++)
		{
			// Lower seat wins every game — arbitrary but deterministic.
			t.ApplyResults(
				DraftTournament
					.Pairings(t.SeatCount, round)
					.Select(p => new RoundResult(p.A, p.B, Math.Min(p.A, p.B)))
			);
			t.AdvanceRound();
		}

		var standings = t.Standings();
		Assert.Multiple(() =>
		{
			Assert.That(t.IsComplete, Is.True);
			Assert.That(standings.Sum(s => s.Wins), Is.EqualTo(standings.Sum(s => s.Losses)));
			Assert.That(standings.Sum(s => s.Played), Is.EqualTo(2 * 28));
			Assert.That(standings, Has.All.Property("Played").EqualTo(t.RoundCount));
			// Seat 0 never faces a lower seat, so it wins out and tops the table.
			Assert.That(standings[0].Seat, Is.Zero);
			Assert.That(standings[0].Wins, Is.EqualTo(7));
		});
	}

	[Test]
	public void Standings_RankByWinsThenLosses()
	{
		var t = Build();
		t.ApplyResults(
			[
				new RoundResult(1, 2, WinnerSeat: 1), // seat 1: 1-0
				new RoundResult(3, 4, WinnerSeat: -1), // seat 3: 0-0-1
			]
		);

		var order = t.Standings().Select(s => s.Seat).ToList();
		Assert.Multiple(() =>
		{
			Assert.That(order[0], Is.EqualTo(1), "a win should lead");
			Assert.That(order[1], Is.EqualTo(3), "a draw should beat an untouched seat");
			Assert.That(order[^1], Is.EqualTo(2), "the only loss should be last");
		});
	}

	// ===== Round advance =====

	[Test]
	public void IsComplete_OnlyAfterAdvancingThroughTheFinalRound()
	{
		var t = Build();
		for (var i = 0; i < t.RoundCount; i++)
		{
			Assert.That(t.IsComplete, Is.False, $"complete too early at round {i}");
			t.AdvanceRound();
		}
		Assert.That(t.IsComplete, Is.True);
	}

	[Test]
	public void AdvanceRound_DoesNotRunPastTheLastRound()
	{
		var t = Build();
		for (var i = 0; i < t.RoundCount + 5; i++)
			t.AdvanceRound();
		Assert.That(t.Round, Is.EqualTo(t.RoundCount));
	}

	[Test]
	public void Constructor_RejectsAnOddSeatCount()
	{
		Assert.Throws<ArgumentException>(
			() =>
				_ = new DraftTournament(
					Enumerable.Range(0, 7).Select(_ => (IReadOnlyList<Card>)[]).ToList(),
					seed: 1
				)
		);
	}
}
