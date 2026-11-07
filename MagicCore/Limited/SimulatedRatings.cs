using System.Collections.Concurrent;
using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Sets;

namespace MagicCore.Limited;

/// <summary>
/// A set's cards rated by self-play (story DATA-2, Shayne 2026-10-31: "pre-simulating win rates of cards against
/// bots"): two AI sealed decks a game — each a pool's suggested build (<see cref="SealedBuilder.Opponent"/>), as an
/// AI sealed opponent plays — and each card's GAMES-IN-HAND win rate, as 17lands' GIH WR: a game counts for a card
/// its player drew or kept in the opening hand, read as "a copy left the library" from the last state. ponytail: a
/// card searched out of the library counts as drawn; no card in TRK or FRA searches for a nonbasic of its own set.
/// </summary>
public static class SimulatedRatings
{
	/// <summary>One card's games in hand and the wins among them (a draw half a win).</summary>
	public sealed record Tally(int Games, double Wins)
	{
		public double WinRate => Games == 0 ? 0.5 : Wins / Games;
	}

	/// <summary>
	/// <paramref name="games"/> AI-vs-AI sealed games of <paramref name="set"/> — game i between the opponents of
	/// seeds 2i and 2i+1, seeded i, so a run replays — every card's games in hand tallied.
	/// </summary>
	public static Dictionary<string, Tally> Measure(
		string set,
		int games,
		string ai = "greedy",
		int firstSeed = 1,
		Action<int>? played = null
	)
	{
		var tallies = new ConcurrentDictionary<string, Tally>();
		var done = 0;
		Parallel.For(
			firstSeed,
			firstSeed + games,
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			seed =>
			{
				var (a, b) = (
					SealedBuilder.Opponent(set, 2 * seed),
					SealedBuilder.Opponent(set, 2 * seed + 1)
				);
				var start = MagicRules.NewGame(
					a.Main.Select(n => CardRegistry.Get(n, set)),
					b.Main.Select(n => CardRegistry.Get(n, set)),
					seed,
					sideboard1: SealedBuildMatch.Outside(a.Sideboard),
					sideboard2: SealedBuildMatch.Outside(b.Sideboard)
				);
				GameState? end = null;
				AiMatch.Game game;
				try
				{
					game = AiMatch.Play(
						ai,
						ai,
						start,
						"a",
						"b",
						seed,
						true,
						1,
						ended: s => end = s
					)!;
				}
				catch (Exception e) when (e is InvalidOperationException or ArgumentException)
				{
					throw new InvalidOperationException(
						$"{set} self-play game {seed} ({ai}): {e.Message}",
						e
					);
				}
				var scoreA = game.Winner switch
				{
					1 => 1.0,
					2 => 0.0,
					_ => 0.5,
				};
				foreach (
					var (deck, player, score) in new[]
					{
						(a.Main, start.Player1Id(), scoreA),
						(b.Main, start.Player2Id(), 1 - scoreA),
					}
				)
				foreach (var card in InHand(deck, end!, player))
					tallies.AddOrUpdate(
						card,
						new Tally(1, score),
						(_, t) => new Tally(t.Games + 1, t.Wins + score)
					);
				played?.Invoke(Interlocked.Increment(ref done));
			}
		);
		return new(tallies);
	}

	/// <summary>The deck's nonbasic names of which fewer copies are in the library at the end than the deck held.</summary>
	static IEnumerable<string> InHand(IReadOnlyList<string> deck, GameState end, int player)
	{
		var library = end.CardsIn(player, ZoneType.Library)
			.GroupBy(c => c.Name)
			.ToDictionary(g => g.Key, g => g.Count());
		return deck.Where(n => !SealedBuilder.Basics.Contains(n))
			.GroupBy(n => n)
			.Where(g => library.GetValueOrDefault(g.Key) < g.Count())
			.Select(g => g.Key);
	}

	/// <summary>
	/// The tallies as ratings on <see cref="CardRatings"/>' scale: each card's games-in-hand win rate, any games short
	/// of <see cref="CardRatings.MinGames"/> made up at the tallies' overall rate — 17lands' pull (CardRatings.From17Lands).
	/// </summary>
	public static Dictionary<string, double> Rate(IReadOnlyDictionary<string, Tally> tallies)
	{
		var mean = tallies.Values.Sum(t => t.Wins) / Math.Max(1, tallies.Values.Sum(t => t.Games));
		return tallies.ToDictionary(
			t => t.Key,
			t =>
				(t.Value.Wins + mean * Math.Max(0, CardRatings.MinGames - t.Value.Games))
				/ Math.Max(t.Value.Games, CardRatings.MinGames)
		);
	}
}
