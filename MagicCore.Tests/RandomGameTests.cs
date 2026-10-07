using ImmutableGameObjects;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests;

/// <summary>
/// Whole games of random legal moves and random choice answers. Asserts only that the engine never
/// throws and every game ends — a net under everything the focused tests do not think of.
/// </summary>
public class RandomGameTests
{
	[Test]
	public void RandomPlay_VanillaFiller_AlwaysFinishes([Range(1, 30)] int seed) =>
		Assert.That(
			PlayRandomly(TestGames.New(seed), seed).IsOver(),
			$"seed {seed} did not finish"
		);

	[Test]
	public void RandomPlay_RealFraDecks_AlwaysFinishes([Range(1, 90)] int seed)
	{
		// Every ordered pairing of the decks, in turn — so each deck plays each other from both seats.
		var decks = FraDecks.All.Keys.ToList();
		var pairs = decks.SelectMany(a => decks.Where(b => b != a).Select(b => (a, b))).ToList();
		var (first, second) = pairs[seed % pairs.Count];

		var s = MagicRules.NewGame(FraDecks.All[first], FraDecks.All[second], seed);
		Assert.That(
			PlayRandomly(s, seed).IsOver(),
			$"seed {seed} ({first} vs {second}) did not finish"
		);
	}

	public static GameState PlayRandomly(GameState s, int seed)
	{
		var rng = new Random(seed);
		for (var i = 0; i < 20_000 && !s.IsOver(); i++)
		{
			if (s.GetPendingChoice() is { } choice)
			{
				var picks = choice.Options.OrderBy(_ => rng.Next()).Take(choice.MinChoices);
				s = s.Choose(picks.Select(o => o.Id));
				continue;
			}
			// Mulligan rarely, or random play mulligans to nothing and never reaches combat.
			var pool = s.LegalActions()
				.Where(a => a is not Actions.MulliganAction || rng.Next(10) == 0)
				.ToList();
			s = s.Act(pool[rng.Next(pool.Count)]);
		}
		return s;
	}
}
