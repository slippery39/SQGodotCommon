using MagicCore.Cards;
using MagicCore.Limited;

namespace MagicCore.Tests.Limited;

/// <summary>
/// FRA's Quick Draft (story LIM-2.2): packs pass as Arena passes them, lose cards only by picks — so the
/// echoed pairs leave their signal — and a bot picks by rating with MtgSimulator's measured colour lean.
/// </summary>
public class DraftTests
{
	static List<List<IReadOnlyList<PackCard>>> Deal(int seed)
	{
		var rng = new Random(seed);
		return
		[
			.. Enumerable
				.Range(0, Draft.SeatCount)
				.Select(_ =>
					Enumerable
						.Range(0, Draft.PackCount)
						.Select(_ => Boosters.Pack("FRA", rng))
						.ToList()
				),
		];
	}

	static DraftState Create(List<List<IReadOnlyList<PackCard>>> dealt) =>
		Draft.Create(
			[
				.. dealt.Select(seat =>
					(IReadOnlyList<IReadOnlyList<string>>)
						[.. seat.Select(p => p.Select(c => c.Card).ToList())]
				),
			]
		);

	[Test]
	public void AnFraDraft_IsEightSeats_ThreePacksEach_AndEverySeatEndsWithEveryPick()
	{
		var start = Draft.New("FRA", seed: 1);
		Assert.That(start.Seats, Has.Count.EqualTo(Draft.SeatCount));
		Assert.That(start.Seats.All(s => s.Unopened.Count == Draft.PackCount - 1));
		var packSize = start.Seats[0].Pack.Count;
		Assert.That(packSize, Is.EqualTo(14), "Wizards: 14 cards a play booster");

		var end = Draft.RunBots(start, CardRatings.Of("FRA"));
		Assert.That(end.IsComplete);
		Assert.That(end.Seats.All(s => s.Picks.Count == packSize * Draft.PackCount));
		Assert.That(
			end.Seats.SelectMany(s => s.Picks).Order(),
			Is.EqualTo(
				start.Seats.SelectMany(s => s.Pack.Concat(s.Unopened.SelectMany(p => p))).Order()
			),
			"every card dealt is picked once — none added, none lost"
		);
	}

	[Test]
	public void ADraft_IsTheSameForTheSameSeed()
	{
		var a = Draft.RunBots(Draft.New("FRA", seed: 4), CardRatings.Of("FRA"));
		var b = Draft.RunBots(Draft.New("FRA", seed: 4), CardRatings.Of("FRA"));
		Assert.That(
			a.Seats.Select(s => string.Join("|", s.Picks)),
			Is.EqualTo(b.Seats.Select(s => string.Join("|", s.Picks)))
		);
	}

	[Test]
	public void PacksPassLeft_ThenRight_ThenLeft()
	{
		var state = Draft.New("FRA", seed: 2);
		for (var round = 0; round < Draft.PackCount; round++)
		{
			var before = state;
			state = Draft.Next(state, CardRatings.Of("FRA"));
			var from = round % 2 == 0 ? Draft.SeatCount - 1 : 1; // seat 0 receives seat 7's pack, then seat 1's
			var expected = before
				.Seats[from]
				.Pack.RemoveAt(before.Seats[from].Pack.IndexOf(state.Seats[from].Picks[^1]));
			Assert.That(state.Seats[0].Pack, Is.EquivalentTo(expected), $"round {round}");
			while (state.Round == round && !state.IsComplete)
				state = Draft.Next(state, CardRatings.Of("FRA"));
		}
	}

	// LIM-2's requirement: "a pack with two pair cards from different pairs means the partner was taken".
	[Test]
	public void TakingAnEchoedPairCard_LeavesItsPartnerBesideTheThird_FromAnotherPair()
	{
		var dealt = Deal(seed: 3);
		var state = Create(dealt);
		var pack = dealt[0][0];
		var taken = pack.First(c => c.Sheet.StartsWith("pair"));
		var partner = pack.Single(c => c.Sheet == taken.Sheet && c != taken).Card;
		var third = pack.Single(c => c.Sheet.StartsWith("thirdNot")).Card;

		state = Draft.Next(
			state,
			CardRatings.Of("FRA"),
			seat0: state.Seats[0].Pack.IndexOf(taken.Card)
		);

		var seen = state.Seats[1].Pack; // seat 0's pack, passed left
		Assert.That(seen, Does.Contain(partner).And.Contain(third));
		Assert.That(
			seen,
			Does.Not.Contain(taken.Card),
			"two pairs on show — one card each: a partner is gone"
		);
	}

	// The lean, on inline ratings: a seat in Red and Green, a Blue card rated a point above a Red one.
	static readonly CardData[] Fra =
	[
		.. CardDataSet.Load("FRA").Select(p => p.Card).OrderBy(c => c.Name),
	];

	static string MonoColored(string letter) =>
		Fra.First(c =>
			c.Colors is [var only] && only == letter && !c.TypeLine.Contains("Land")
		).Name;

	static (Dictionary<string, double> Ratings, string[] Picks, string Red, string Blue) Lean()
	{
		var reds = Fra.Where(c => c.Colors is ["R"]).Select(c => c.Name).Take(6).ToArray();
		var greens = Fra.Where(c => c.Colors is ["G"]).Select(c => c.Name).Take(5).ToArray();
		var ratings = Fra.ToDictionary(c => c.Name, _ => 0.55); // the median
		string[] picks = [.. reds[1..], .. greens];
		foreach (var p in picks)
			ratings[p] = 0.60;
		var (red, blue) = (reds[0], MonoColored("U"));
		(ratings[red], ratings[blue]) = (0.60, 0.61);
		return (ratings, picks, red, blue);
	}

	[Test]
	public void ABot_LeansIntoItsColours_OnceItHasPicks()
	{
		var (ratings, picks, red, blue) = Lean();
		Assert.That(
			Draft.Lane(picks, c => 100 * (ratings[c] - 0.55)),
			Is.EquivalentTo(new[] { ManaColor.Red, ManaColor.Green })
		);

		Assert.That(
			Draft.BotPick([blue, red], picks, ratings),
			Is.EqualTo(1),
			"a point less, but in its colours"
		);
		Assert.That(
			Draft.BotPick([blue, red], [], ratings),
			Is.EqualTo(0),
			"the control: no picks, no lean"
		);
	}

	[Test]
	public void ABotInOneColour_LeavesItsSecondOpen()
	{
		var (ratings, picks, red, blue) = Lean();
		string[] reds = [.. picks.Where(p => Draft.ColorsOf(p) is [ManaColor.Red])];
		var redOnly = Enumerable.Repeat(reds[0], 10).ToArray();
		Assert.That(Draft.Lane(redOnly, ratings), Is.EqualTo(new[] { ManaColor.Red }));
		// one new colour fits the open second slot: Blue's point beats Red, as with no lean
		Assert.That(Draft.BotPick([blue, red], redOnly, ratings), Is.EqualTo(0));
		var gold = Fra.First(c => c.Colors is ["G", "U"] || c.Colors is ["U", "G"]).Name;
		ratings[gold] = 0.61; // two new colours don't: the lean takes Red
		Assert.That(Draft.BotPick([gold, red], redOnly, ratings), Is.EqualTo(1));
	}

	[Test]
	public void ABot_StillTakesABomb_OutsideItsColours()
	{
		var (ratings, picks, red, blue) = Lean();
		ratings[blue] = 0.60 + (2 * Draft.LaneWeight + 1) / 100; // beats the lean both ways
		Assert.That(Draft.BotPick([blue, red], picks, ratings), Is.EqualTo(0));
	}

	[Test]
	public void ABot_NeverTakesABasicLand_OverAnotherCard()
	{
		var (ratings, picks, red, _) = Lean();
		var forest = Fra.First(c => c.TypeLine.StartsWith("Basic")).Name;
		(ratings[forest], ratings[red]) = (0.99, 0.30);
		Assert.That(Draft.BotPick([forest, red], picks, ratings), Is.EqualTo(1));
		Assert.That(
			Draft.BotPick([forest, forest], picks, ratings),
			Is.EqualTo(0),
			"all basics: one anyway"
		);
	}

	[Test]
	public void ACardWithNoRating_ScoresAtTheMedian()
	{
		var (ratings, _, red, blue) = Lean();
		ratings[red] = 0.50; // below the median
		ratings.Remove(blue);
		Assert.That(Draft.BotPick([red, blue], [], ratings), Is.EqualTo(1));
	}
}
