namespace MagicCore.Tests;

/// <summary>
/// Whole games of random legal moves and random choice answers. Asserts only that the engine never
/// throws and every game ends — a net under everything the focused tests do not think of.
/// </summary>
public class RandomGameTests
{
	[Test]
	public void RandomPlay_AlwaysFinishes([Range(1, 30)] int seed)
	{
		var rng = new Random(seed);
		var s = TestGames.New(seed);

		for (var i = 0; i < 20_000 && !s.IsOver(); i++)
		{
			if (s.GetPendingChoice() is { } choice)
			{
				var picks = choice.Options.OrderBy(_ => rng.Next()).Take(choice.MinChoices);
				s = s.Choose(picks.Select(o => o.Id));
				continue;
			}
			var actions = s.LegalActions();
			// Mulligan rarely, or random play mulligans to nothing and never reaches combat.
			var pool = actions
				.Where(a => a is not Actions.MulliganAction || rng.Next(10) == 0)
				.ToList();
			s = s.Act(pool[rng.Next(pool.Count)]);
		}

		Assert.That(s.IsOver(), $"seed {seed} did not finish");
	}
}
