using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator.Tests;

[TestFixture]
public class DraftTests
{
	// Cards are defined inline so card balance tweaks in CardLibrary can never break these.
	private static Card C(string name, int cost = 1, int power = 0, int toughness = 0) =>
		new()
		{
			Name = name,
			ManaCost = cost,
			Components =
				power > 0
					? ImmutableArray.Create<GameComponent>(
						new CreatureComponent { Power = power, Toughness = toughness }
					)
					: ImmutableArray<GameComponent>.Empty,
		};

	private static Card Land(string name) =>
		new()
		{
			Name = name,
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Land"),
		};

	/// A pool of distinctly-named cards so pools and packs can be compared by name.
	private static IReadOnlyList<Card> Pool(int count) =>
		Enumerable.Range(0, count).Select(i => C($"Card{i}", cost: i % 6 + 1)).ToList();

	private static DraftSeat Seat(params Card[] offer) =>
		new(
			offer.ToImmutableList(),
			ImmutableList<ImmutableList<Card>>.Empty,
			ImmutableList<Card>.Empty
		);

	private static IReadOnlyList<int> PickFirst(DraftState state) =>
		state.Seats.Select(_ => 0).ToList();

	private static List<string> Names(IEnumerable<Card> cards) =>
		cards.Select(c => c.Name).ToList();

	// ===== Booster rotation =====

	[Test]
	public void ApplyPicks_Booster_PassesRemainderToNextSeat()
	{
		// Round 0 passes "left": seat i receives what seat i-1 had left over.
		var state = new DraftState(
			DraftFormat.Booster,
			[Seat(C("A0"), C("B0")), Seat(C("A1"), C("B1")), Seat(C("A2"), C("B2"))],
			Round: 0
		);

		var next = Draft.ApplyPicks(state, PickFirst(state));

		Assert.That(Names(next.Seats[0].Pool), Is.EqualTo(new[] { "A0" }));
		Assert.That(Names(next.Seats[0].Offer), Is.EqualTo(new[] { "B2" }));
		Assert.That(Names(next.Seats[1].Offer), Is.EqualTo(new[] { "B0" }));
		Assert.That(Names(next.Seats[2].Offer), Is.EqualTo(new[] { "B1" }));
		Assert.That(next.Round, Is.EqualTo(0), "Round only advances when a new pack is opened.");
	}

	[Test]
	public void ApplyPicks_Booster_SecondPack_PassesInOppositeDirection()
	{
		// Round 1 is the second pack, so the pass direction flips: seat i receives from seat i+1.
		var state = new DraftState(
			DraftFormat.Booster,
			[Seat(C("A0"), C("B0")), Seat(C("A1"), C("B1")), Seat(C("A2"), C("B2"))],
			Round: 1
		);

		var next = Draft.ApplyPicks(state, PickFirst(state));

		Assert.That(Names(next.Seats[0].Offer), Is.EqualTo(new[] { "B1" }));
		Assert.That(Names(next.Seats[1].Offer), Is.EqualTo(new[] { "B2" }));
		Assert.That(Names(next.Seats[2].Offer), Is.EqualTo(new[] { "B0" }));
	}

	[Test]
	public void ApplyPicks_Booster_ExhaustedPack_OpensNextPack()
	{
		var nextPack = ImmutableList.Create(C("Next0"), C("Next1"));
		var seat = new DraftSeat([C("Last")], [nextPack], ImmutableList<Card>.Empty);
		var state = new DraftState(DraftFormat.Booster, [seat, seat], Round: 0);

		var next = Draft.ApplyPicks(state, PickFirst(state));

		Assert.That(Names(next.Seats[0].Offer), Is.EqualTo(new[] { "Next0", "Next1" }));
		Assert.That(next.Seats[0].Queue, Is.Empty);
		Assert.That(next.Round, Is.EqualTo(1));
		Assert.That(next.IsComplete, Is.False);
	}

	[Test]
	public void ApplyPicks_Digital_TakesOwnNextOffer_NotNeighboursRemainder()
	{
		// The whole point of Digital: seats never see each other's cards.
		var state = new DraftState(
			DraftFormat.Digital,
			[
				new DraftSeat(
					[C("A0"), C("B0")],
					[ImmutableList.Create(C("Own0"))],
					ImmutableList<Card>.Empty
				),
				new DraftSeat(
					[C("A1"), C("B1")],
					[ImmutableList.Create(C("Own1"))],
					ImmutableList<Card>.Empty
				),
			],
			Round: 0
		);

		var next = Draft.ApplyPicks(state, PickFirst(state));

		Assert.That(Names(next.Seats[0].Offer), Is.EqualTo(new[] { "Own0" }));
		Assert.That(Names(next.Seats[1].Offer), Is.EqualTo(new[] { "Own1" }));
		Assert.That(Names(next.Seats[0].Pool), Is.EqualTo(new[] { "A0" }));
	}

	// ===== Full drafts =====

	[Test]
	public void RunToCompletion_Booster_EveryDealtCardIsDraftedExactlyOnce()
	{
		const int seats = 4;
		const int packSize = 5;
		const int packCount = 3;
		var state = Draft.Create(
			DraftFormat.Booster,
			Pool(30),
			seed: 42,
			seatCount: seats,
			packSize: packSize,
			packCount: packCount
		);

		// Everything dealt to the table, before anyone picks.
		var dealt = state
			.Seats.SelectMany(s => s.Offer.Concat(s.Queue.SelectMany(p => p)))
			.Select(c => c.Name)
			.OrderBy(n => n)
			.ToList();

		var pickers = Enumerable.Repeat<DraftPicker>((offer, _) => 0, seats).ToList();
		var final = Draft.RunToCompletion(state, pickers);

		var drafted = final
			.Seats.SelectMany(s => s.Pool)
			.Select(c => c.Name)
			.OrderBy(n => n)
			.ToList();

		// Catches rotation losing or duplicating a card, not just miscounting.
		Assert.That(drafted, Is.EqualTo(dealt));
		Assert.That(final.Seats.Select(s => s.Pool.Count), Is.All.EqualTo(packSize * packCount));
	}

	[Test]
	public void RunToCompletion_Digital_EachSeatPoolReachesPoolSize()
	{
		const int poolSize = 7;
		var state = Draft.Create(
			DraftFormat.Digital,
			Pool(30),
			seed: 7,
			seatCount: 3,
			offerSize: 3,
			poolSize: poolSize
		);

		var pickers = Enumerable.Repeat<DraftPicker>((offer, _) => 0, 3).ToList();
		var final = Draft.RunToCompletion(state, pickers);

		Assert.That(final.Seats.Select(s => s.Pool.Count), Is.All.EqualTo(poolSize));
		Assert.That(final.IsComplete, Is.True);
	}

	// ===== Determinism and pack contents =====

	[Test]
	public void Create_SameSeed_ProducesIdenticalPacks()
	{
		var pool = Pool(30);
		var a = Draft.Create(DraftFormat.Booster, pool, seed: 123, seatCount: 4);
		var b = Draft.Create(DraftFormat.Booster, pool, seed: 123, seatCount: 4);
		var different = Draft.Create(DraftFormat.Booster, pool, seed: 124, seatCount: 4);

		Assert.That(Names(a.Seats[0].Offer), Is.EqualTo(Names(b.Seats[0].Offer)));
		Assert.That(Names(a.Seats[3].Offer), Is.EqualTo(Names(b.Seats[3].Offer)));
		Assert.That(Names(a.Seats[0].Offer), Is.Not.EqualTo(Names(different.Seats[0].Offer)));
	}

	[Test]
	public void Create_ExcludesLandsFromPacks()
	{
		// BuildDeck supplies the mana base, so lands must never take up a pack slot.
		var pool = Pool(10).Concat([Land("Plains"), Land("Island")]).ToList();
		var state = Draft.Create(DraftFormat.Booster, pool, seed: 1, seatCount: 2, packSize: 10);

		var everyCard = state.Seats.SelectMany(s => s.Offer.Concat(s.Queue.SelectMany(p => p)));
		Assert.That(everyCard.Any(c => c.HasSubtype("Land")), Is.False);
	}

	// ===== Deck building =====

	[Test]
	public void BuildDeck_ShortPool_PadsToDeckSize()
	{
		var deck = Draft.BuildDeck([C("A"), C("B"), C("C"), C("D"), C("E")], ownerId: 7);

		Assert.That(deck, Has.Count.EqualTo(40));
		Assert.That(deck.Count(c => c.HasSubtype("Land")), Is.EqualTo(35));
		Assert.That(deck.Select(c => c.OwnerId), Is.All.EqualTo(7));
		Assert.That(deck.Select(c => c.ControllerId), Is.All.EqualTo(7));
	}

	// ===== Pickers =====

	[Test]
	public void Curve_PrefersEfficientCreature_OverOverpricedOne()
	{
		var overpriced = C("Overpriced", cost: 6, power: 1, toughness: 1);
		var efficient = C("Efficient", cost: 1, power: 3, toughness: 3);
		var empty = new List<Card>();

		Assert.That(DraftPickers.Curve([overpriced, efficient], empty), Is.EqualTo(1));
		Assert.That(DraftPickers.Curve([efficient, overpriced], empty), Is.EqualTo(0));
	}
}
