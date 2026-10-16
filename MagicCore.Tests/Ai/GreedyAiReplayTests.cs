using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Ai;

/// <summary>
/// GreedyAi against itself over seeded games, every decision fingerprinted (<see cref="GoldenReplay"/>):
/// who decided, how many options there were, and what was picked — so an option offered or dropped
/// shows even when the pick stays the same. The random-play replays never ask the AI anything, so they
/// cannot see an AI change: a refactor of the AI proves HERE that it changed no decision. Regenerate
/// only for an intended change in how the AI plays, with the diff reviewed. Passes AutoPass takes (the
/// only legal move) are not decisions and are not recorded.
/// </summary>
public class GreedyAiReplayTests
{
	static readonly GoldenReplay Replays = new("GoldenAiReplay.txt");

	[OneTimeTearDown]
	public void SaveReplays() => Replays.WriteIfUpdating();

	// Every ordered pairing of the FRA decks, twice — so each deck meets each other from both seats,
	// and a new deck is fingerprinted the moment it is added.
	static IEnumerable<TestCaseData> Games() =>
		from first in FraDecks.All.Keys
		from second in FraDecks.All.Keys
		where first != second
		from seed in new[] { 1, 2 }
		select new TestCaseData(first, second, seed);

	[TestCaseSource(nameof(Games))]
	public void GreedyAi_PlaysAsRecorded(string first, string second, int seed)
	{
		var s = MagicRules.NewGame(FraDecks.All[first], FraDecks.All[second], seed);
		var ais = new[] { s.Player1Id(), s.Player2Id() }.ToDictionary(p => p, p => new GreedyAi(p));

		var trace = new GoldenReplay.Trace();
		for (var i = 0; i < 5_000 && !s.IsOver(); i++)
		{
			s = s.AutoPass();
			if (s.IsOver())
				break;
			var seat = s.DecidingPlayerId() == s.Player1Id() ? "P1" : "P2";
			var options = s.GetPendingChoice() is { } choice
				? choice.Options.Count
				: s.LegalActions().Count;
			(s, var what) = ais[s.DecidingPlayerId()].Decide(s);
			trace.Step($"{seat} {options} {what}");
		}

		Assert.That(s.IsOver(), $"{first} vs {second}, seed {seed} did not finish");
		Replays.Check($"{first} vs {second}", seed, trace, s);
	}
}
