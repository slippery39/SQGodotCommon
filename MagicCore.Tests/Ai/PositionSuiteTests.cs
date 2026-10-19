using MagicCore.Ai;

namespace MagicCore.Tests.Ai;

/// <summary>The quick check (story AI-6.4): recorded positions, replayed, decided again, compared.</summary>
public class PositionSuiteTests
{
	// A small suite of GreedyAi's own games: fast, and the same machinery as the search's.
	static readonly Lazy<PositionSuite.Suite> Greedy =
		new(() => PositionSuite.Generate("greedy", seeds: 1, perGame: 3));

	// The tracked suite still replays: an engine change that reorders legal moves, or changes what a
	// position offers, fails here — regenerate it (the message says how).
	[Test]
	public void TheTrackedSuite_StillReplays()
	{
		var tracked = PositionSuite.Tracked();

		Assert.That(
			tracked,
			Is.Not.Null,
			$"no {PositionSuite.FilePath} — {PositionSuite.GenerateCommand}"
		);
		Assert.That(PositionSuite.States(tracked!), Has.Count.EqualTo(tracked!.Positions.Count));
	}

	[Test]
	public void ASuite_ReadsBackAsWritten()
	{
		var text = PositionSuite.ToText(Greedy.Value);
		Assert.That(PositionSuite.ToText(PositionSuite.Parse(text)), Is.EqualTo(text));
		Assert.That(Greedy.Value.Positions, Has.Count.GreaterThan(0));
	}

	// The AI that answered changes nothing — the recording replays exactly — and another AI changes
	// some: a check that can't see a difference measures nothing.
	[Test]
	public void ItsOwnAnswers_ChangeNothing_AndAnotherAiChangesSome()
	{
		Assert.That(PositionSuite.Run(Greedy.Value, "greedy").Changed, Is.Empty);
		Assert.That(PositionSuite.Run(Greedy.Value, "random").Changed, Is.Not.Empty);
	}

	[Test]
	public void AMoveThatNoLongerApplies_IsStale()
	{
		var game = Greedy.Value.Games[0];
		var broken = Greedy.Value with
		{
			Games =
			[
				game with
				{
					Moves = ["a999", .. game.Moves.Skip(1)],
				},
				.. Greedy.Value.Games.Skip(1),
			],
		};
		Assert.Throws<PositionSuite.StaleException>(() => PositionSuite.States(broken));
	}

	[Test]
	public void APositionThatOffersSomethingElse_IsStale()
	{
		var p = Greedy.Value.Positions[0];
		var broken = Greedy.Value with
		{
			Positions = [p with { Offers = "00000000" }, .. Greedy.Value.Positions.Skip(1)],
		};
		Assert.Throws<PositionSuite.StaleException>(() => PositionSuite.States(broken));
	}

	// A divergence that leaves every recorded position's offer alike still changes how the game ends.
	[Test]
	public void AGameThatEndsDifferently_IsStale()
	{
		var game = Greedy.Value.Games[0];
		var broken = Greedy.Value with
		{
			Games = [game with { End = "00000000" }, .. Greedy.Value.Games.Skip(1)],
		};
		Assert.Throws<PositionSuite.StaleException>(() => PositionSuite.States(broken));
	}

	[Test]
	public void AGameWithNoMoves_ReadsBackWithNone()
	{
		var empty = new PositionSuite.Suite(
			"greedy",
			[new("White-Black", "Red-Green", 1, "x", [])],
			[]
		);
		Assert.That(PositionSuite.Parse(PositionSuite.ToText(empty)).Games[0].Moves, Is.Empty);
	}
}
