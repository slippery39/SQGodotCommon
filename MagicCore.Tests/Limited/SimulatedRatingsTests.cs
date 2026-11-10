using ImmutableGameObjects;
using MagicCore.Limited;

namespace MagicCore.Tests.Limited;

/// <summary>DATA-2: a set's cards rated by their games-in-hand win rate in AI-vs-AI sealed games.</summary>
public class SimulatedRatingsTests
{
	[Test]
	public void ACardShortOfMinGames_IsPulledToTheMean_OneWithEnough_IsItsOwnRate()
	{
		var n = CardRatings.MinGames;
		var rated = SimulatedRatings.Rate(
			new Dictionary<string, SimulatedRatings.Tally>
			{
				["seen often"] = new(n, 0.7 * n),
				["seen once"] = new(1, 1),
				["the rest"] = new(3 * n, 1.2 * n),
			}
		);
		var mean = (0.7 * n + 1 + 1.2 * n) / (4 * n + 1);
		Assert.That(rated["seen often"], Is.EqualTo(0.7).Within(1e-12));
		Assert.That(rated["seen once"], Is.EqualTo((1 + mean * (n - 1)) / n).Within(1e-12));
	}

	// Round 2 builds on round 1's ratings, and the builder reads a rating for every pool card (it threw).
	[Test]
	public void ACardNeverInHand_IsRatedAtTheMean_SoARound2CanBuildOnIt()
	{
		var tallies = new Dictionary<string, SimulatedRatings.Tally> { ["seen"] = new(10, 7) };
		var rated = SimulatedRatings.Rate(tallies, ["seen", "never"]);
		Assert.That(rated["never"], Is.EqualTo(0.7).Within(1e-12));
		var r1 = SimulatedRatings.Measure("TRK", 2, firstSeed: 90001);
		var round1 = SimulatedRatings.Rate(r1, SimulatedRatings.Rateable("TRK"));
		var r2 = SimulatedRatings.Measure("TRK", 2, firstSeed: 90001, ratings: round1);
		Assert.That(
			r2.Keys,
			Is.Not.EquivalentTo(r1.Keys),
			"round 2's decks are built on round 1's ratings"
		);
	}

	// The old read ("a copy left the library") counted a milled or surveilled card as drawn (DATA-2's Next).
	[Test]
	public void InHand_IsWhatAPlayerHeldFromTurnOne_NotAMulliganNorAMilledCard()
	{
		var mulligan = TestGames.New();
		var hands = new SimulatedRatings.InHandDuring(mulligan);
		var me = mulligan.Player1Id();
		hands.See(mulligan);
		Assert.That(hands.Of(me), Is.Empty, "the hand at the mulligan isn't kept yet");

		var s = TestGames.EmptyHands(TestGames.Started());
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Held", "2/2"), ZoneType.Hand);
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Milled", "2/2"), ZoneType.Graveyard);
		(s, _) = TestGames.Put(
			s,
			s.Player2Id(),
			TestGames.Creature("Theirs", "2/2"),
			ZoneType.Hand
		);
		hands.See(s);
		Assert.That(hands.Of(me), Is.EquivalentTo(new[] { "Held" }));
		Assert.That(hands.Of(s.Player2Id()), Is.EquivalentTo(new[] { "Theirs" }));
	}

	[Test]
	public void AFewGames_TallyTheCardsDrawn_TwoPlayersAGame()
	{
		const int games = 4;
		var tallies = SimulatedRatings.Measure("FRA", games);
		Assert.That(tallies, Is.Not.Empty);
		Assert.That(tallies.Values.Select(t => t.Games), Is.All.InRange(1, 2 * games));
		// every player sees at least their opening hand's nonbasics: well over one card a game
		Assert.That(tallies.Values.Sum(t => t.Games), Is.GreaterThan(2 * games));
		Assert.That(
			tallies.Values.Sum(t => t.Wins),
			Is.LessThanOrEqualTo(tallies.Values.Sum(t => t.Games))
		);
	}
}
