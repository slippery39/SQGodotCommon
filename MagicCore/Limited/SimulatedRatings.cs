using System.Collections.Concurrent;
using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Sets;

namespace MagicCore.Limited;

/// <summary>
/// A set's cards rated by self-play (story DATA-2, Shayne 2026-10-31: "pre-simulating win rates of cards against
/// bots"): two AI sealed decks a game — each a pool's suggested build (<see cref="SealedBuilder.Opponent"/>), as an
/// AI sealed opponent plays — and each card's GAMES-IN-HAND win rate, as 17lands' GIH WR: a game counts for a card
/// its player drew or kept in the opening hand — read as "it was in its owner's hand at a decision from turn 1 on"
/// (<see cref="InHandDuring"/>), so a milled or surveilled card is not drawn and a mulliganed hand is not kept.
/// ponytail: a card searched or returned to the hand counts as drawn, as 17lands' "in hand" would; a card drawn and
/// discarded inside one resolution is missed — a card id on <c>DrewCardEvent</c>, watched, would catch both.
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
	/// seeds 2i and 2i+1, seeded i, so a run replays — every card's games in hand tallied. The decks are built on
	/// <paramref name="ratings"/> (the set's own table if null): a second round builds on the first round's ratings.
	/// </summary>
	public static Dictionary<string, Tally> Measure(
		string set,
		int games,
		string ai = "greedy",
		int firstSeed = 1,
		IReadOnlyDictionary<string, double>? ratings = null,
		RunProgress? progress = null
	)
	{
		var tallies = new ConcurrentDictionary<string, Tally>();
		Parallel.For(
			firstSeed,
			firstSeed + games,
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			seed =>
			{
				var (a, b) = Decks(set, seed, ratings);
				var start = MagicRules.NewGame(
					a.Main.Select(n => CardRegistry.Get(n, set)),
					b.Main.Select(n => CardRegistry.Get(n, set)),
					seed,
					sideboard1: SealedBuildMatch.Outside(a.Sideboard),
					sideboard2: SealedBuildMatch.Outside(b.Sideboard)
				);
				var hands = new InHandDuring(start);
				AiMatch.Game game;
				try
				{
					game = AiMatch.Play(ai, ai, start, "a", "b", seed, true, 1, seen: hands.See)!;
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
				foreach (var card in hands.Of(player).Where(deck.Contains))
					tallies.AddOrUpdate(
						card,
						new Tally(1, score),
						(_, t) => new Tally(t.Games + 1, t.Wins + score)
					);
				progress?.Tick(
					() =>
						$"{tallies.Count(t => t.Value.Games >= CardRatings.MinGames)} of {tallies.Count} cards at {CardRatings.MinGames}+ games in hand"
				);
			}
		);
		return new(tallies);
	}

	/// <summary>Game <paramref name="seed"/>'s two AI sealed decks, player 1's first — <c>--card-games</c> finds a card's games by them.</summary>
	public static (SealedDeck A, SealedDeck B) Decks(
		string set,
		int seed,
		IReadOnlyDictionary<string, double>? ratings = null
	) =>
		(
			SealedBuilder.Opponent(set, 2 * seed, ratings),
			SealedBuilder.Opponent(set, 2 * seed + 1, ratings)
		);

	/// <summary>
	/// The nonbasic names each player held at a decision once turn 1 began — the opening hand kept and every card
	/// drawn (DATA-2). Hand <see cref="See"/> to <see cref="AiMatch.Play"/>'s <c>seen</c>; one game only, one thread.
	/// </summary>
	public sealed class InHandDuring(GameState start)
	{
		readonly Dictionary<int, HashSet<string>> held =
			new() { [start.Player1Id()] = [], [start.Player2Id()] = [] };

		public void See(GameState s)
		{
			if (s.GetGame().TurnNumber < 1)
				return; // a mulligan's hand isn't kept
			foreach (var (player, names) in held)
			foreach (var card in s.CardsIn(player, ZoneType.Hand))
				if (!SealedBuilder.Basics.Contains(card.Name))
					names.Add(card.Name);
		}

		public IReadOnlySet<string> Of(int player) => held[player];
	}

	/// <summary>
	/// The deck's nonbasic names of which fewer copies are in the library at the end than the deck held — the rough
	/// read <c>--card-games</c> still uses (a milled card counts); the ratings use <see cref="InHandDuring"/>.
	/// </summary>
	public static IEnumerable<string> InHand(IReadOnlyList<string> deck, GameState end, int player)
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
	/// Each of <paramref name="every"/> never in hand is at that rate too, as a card of 0 games: the builder reads a
	/// rating for every card of a pool, and a round's decks leave some out — the ones the table they were built on
	/// shunned, so the mean also lets the next round TRY them (Claude, 2026-11-10, DATA-2.4). <see cref="Rateable"/>.
	/// </summary>
	public static Dictionary<string, double> Rate(
		IReadOnlyDictionary<string, Tally> tallies,
		IEnumerable<string>? every = null
	)
	{
		var mean = tallies.Values.Sum(t => t.Wins) / Math.Max(1, tallies.Values.Sum(t => t.Games));
		var rated = tallies.ToDictionary(
			t => t.Key,
			t =>
				(t.Value.Wins + mean * Math.Max(0, CardRatings.MinGames - t.Value.Games))
				/ Math.Max(t.Value.Games, CardRatings.MinGames)
		);
		foreach (var name in every ?? [])
			rated.TryAdd(name, mean);
		return rated;
	}

	/// <summary>The cards a set's ratings need: its implemented nonland printings (a basic at the mean would move <see cref="SealedBuilder.TopDecile"/>).</summary>
	public static IEnumerable<string> Rateable(string set) =>
		CardRegistry
			.Printings(set)
			.Where(p =>
				!p.Card.TypeLine.Contains("Land") && CardRegistry.IsImplemented(p.Card.Name)
			)
			.Select(p => p.Card.Name)
			.Distinct();
}
