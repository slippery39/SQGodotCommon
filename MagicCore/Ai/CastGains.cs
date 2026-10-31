using System.Text.Json;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Limited;
using MagicCore.Sets;

namespace MagicCore.Ai;

/// <summary>
/// The offers a held instant-speed card meets (story AI-13, Shayne: "the expected value of playing the
/// spell now vs holding for a better situation"). In self-play, each turn a player holds an instant or
/// a flash card, that turn's OFFER is the best its casting could gain — the evaluation after, settled,
/// less before, with the card's held price added back — or 0 if it couldn't be cast (a cast that ends
/// the game isn't one: the search takes a win as it comes). Optimal stopping
/// over these offers prices the card held: <see cref="ExpectedBest"/> of the turns left.
/// </summary>
public static class CastGains
{
	public const string TableFile = "FRA.castgains.json";

	public static string TablePath => Path.Combine("MagicCore", "Sets", TableFile);

	/// <summary>Each card's offers as twenty quantiles, 2.5% to 97.5% — ascending, equally likely.</summary>
	public static IReadOnlyDictionary<string, double[]> Fra => fra.Value;

	static readonly Lazy<IReadOnlyDictionary<string, double[]>> fra =
		new(
			() =>
				CardDataSet.ResourceText(TableFile) is { } text
					? JsonSerializer.Deserialize<Dictionary<string, double[]>>(text)!
					: new Dictionary<string, double[]>()
		);

	/// <summary>
	/// The expected best of <paramref name="k"/> offers drawn from <paramref name="quantiles"/> (equally
	/// likely, ascending): Σ qᵢ ((i/n)^k − ((i−1)/n)^k). One offer left: the mean; many: near the top.
	/// </summary>
	public static double ExpectedBest(IReadOnlyList<double> quantiles, int k)
	{
		var n = quantiles.Count;
		var sum = 0.0;
		for (var i = 1; i <= n; i++)
			sum +=
				quantiles[i - 1] * (Math.Pow((double)i / n, k) - Math.Pow((double)(i - 1) / n, k));
		return sum;
	}

	/// <summary>
	/// Self-play over the AI's sealed decks — pool i against pool 1000 + i, GreedyAi on both sides (fast,
	/// and it casts as it goes) — each card's offers, as quantiles.
	/// </summary>
	public static Dictionary<string, double[]> Measure(int games = 200)
	{
		var held = HeldValues.Fra(BeamSearchAi.Settings.HeldFraction);
		var offers = new List<(string Card, double Offer)>[games];
		// measured WITHOUT the option value: it's what this table prices, so with it on, each run would
		// measure the last one's table
		Evaluator.Without(
			Evaluator.Terms.HoldOption,
			() =>
				Parallel.For(
					0,
					games,
					new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
					g => offers[g] = Play(g + 1, held)
				)
		);
		return offers
			.SelectMany(o => o)
			.GroupBy(o => o.Card)
			.ToDictionary(
				x => x.Key,
				x =>
				{
					var sorted = x.Select(o => o.Offer).Order().ToArray();
					// twenty equal slices, each by its middle (2.5%, 7.5%, … 97.5%): one freak sample is
					// never a whole slice, which the best of several offers would lean on
					return Enumerable
						.Range(0, 20)
						.Select(q =>
							Math.Round(sorted[(int)Math.Floor((q + 0.5) / 20 * sorted.Length)], 2)
						)
						.ToArray();
				}
			);
	}

	static List<(string Card, double Offer)> Play(int seed, HeldValues held)
	{
		var decks = new[]
		{
			SealedBuilder.Opponent("FRA", seed),
			SealedBuilder.Opponent("FRA", 1000 + seed),
		};
		var s = MagicRules.NewGame(
			decks[0].Main.Select(CardRegistry.Get),
			decks[1].Main.Select(CardRegistry.Get),
			seed
		);
		var first = s.DecidingPlayerId();
		int[] players = [first, s.OpponentOf(first)];
		var ais = players.ToDictionary(p => p, p => new GreedyAi(p, held));
		var found = new List<(string, double)>();
		// this turn's best offer per (player, held card); flushed as the turn ends
		var turnOffers = new Dictionary<(int Player, string Card), double>();
		var turn = s.GetGame().TurnNumber;
		for (var step = 0; step < 5_000 && !s.IsOver(); step++)
		{
			if (s.GetGame().TurnNumber != turn)
			{
				found.AddRange(turnOffers.Select(kv => (kv.Key.Card, kv.Value)));
				turnOffers.Clear();
				turn = s.GetGame().TurnNumber;
			}
			var who = s.DecidingPlayerId();
			foreach (var p in players)
			foreach (
				var card in s.CardsIn(p, ZoneType.Hand).Where(c => Priority.AtInstantSpeed(s, c))
			)
				turnOffers.TryAdd((p, card.Name), 0);
			if (!s.IsWaitingForChoice && s.GetGame().Step != Step.PreGame)
				foreach (var (name, gain) in Offers(s, who, held))
					turnOffers[(who, name)] = Math.Max(
						turnOffers.GetValueOrDefault((who, name)),
						gain
					);
			s = ais[who].Play(s);
		}
		found.AddRange(turnOffers.Select(kv => (kv.Key.Card, kv.Value))); // the last turn's too
		return found;
	}

	/// <summary>What casting each instant-speed card in <paramref name="who"/>'s hand could gain right now, its best target.</summary>
	static IEnumerable<(string Card, double Gain)> Offers(GameState s, int who, HeldValues held)
	{
		var before = Evaluator.Evaluate(s, who, held);
		return s.DistinctMoves(s.LegalActions())
			.OfType<CastSpellAction>()
			.Select(c => (Card: s.GetCard(c.CardId), Cast: c))
			.Where(x => s.IsIn(x.Card.Id, who, ZoneType.Hand) && Priority.AtInstantSpeed(s, x.Card))
			.Select(x => (x.Card.Name, After: Lookahead.Settle(s.Act(x.Cast))))
			// a cast that ends the game is no offer to wait for: the search takes a win as it comes
			.Where(x => !x.After.IsOver())
			.GroupBy(x => x.Name)
			.Select(g =>
				(
					g.Key,
					g.Max(x => Evaluator.Evaluate(x.After, who, held)) - before + held.Of(g.Key)
				)
			);
	}

	public static string ToJson(Dictionary<string, double[]> table) =>
		JsonSerializer.Serialize(
			new SortedDictionary<string, double[]>(table),
			new JsonSerializerOptions { WriteIndented = true }
		);
}
