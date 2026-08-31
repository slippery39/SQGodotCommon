using System.Collections.Immutable;
using MtgCore;
using MtgCore.Cards.Builders;

namespace MtgSimulator.Tests;

/// <summary>
/// Invariants of the constructed evolution mode.
///
/// Cards are defined inline rather than looked up from CardLibrary, so a balance tweak cannot
/// break these — the project convention.
/// </summary>
[TestFixture]
public class MetagameEvolutionTests
{
	private static Card Spell(string name, int cost) => new() { Name = name, ManaCost = cost };

	/// A pool wide enough that a 60-card deck respecting the 4-of cap is buildable.
	private static IReadOnlyList<Card> Pool(int size = 40) =>
		Enumerable.Range(0, size).Select(i => Spell($"Card{i:D2}", 1 + i % 6)).ToList();

	private static IReadOnlyDictionary<string, Card> Index(IReadOnlyList<Card> pool) =>
		ConstructedGameSetup.PoolIndex(pool);

	private static Decklist Build(int lands, params (string Name, int Count)[] spells) =>
		spells.Aggregate(
			Decklist.Empty("Test") with
			{
				Lands = lands,
			},
			(deck, s) => deck.WithCopies(s.Name, s.Count)
		);

	private static ConstructedValues NoData() => new(DraftTrainingData.Empty, null);

	// ===== Decklist invariants =====

	[Test]
	public void Materialize_ProducesExactlySixtyCards_StampedWithTheOwner()
	{
		var pool = Pool();
		var deck = DeckBuilder.Seed("D", pool, NoData(), new Random(1));

		var cards = deck.Materialize(ownerId: 7, Index(pool));

		Assert.That(cards, Has.Count.EqualTo(Decklist.DeckSize));
		Assert.That(cards.All(c => c.OwnerId == 7 && c.ControllerId == 7), Is.True);
	}

	[Test]
	public void Materialize_PadsWithLands_WhenTheDeckNamesACardThePoolLacks()
	{
		// A decklist saved against one set must still play 60 against another, rather than
		// throwing part-way through a run.
		var pool = Pool();
		var deck = Build(20, ("Card01", 4), ("Ghost", 4)) with
		{
			Spells = ImmutableSortedDictionary
				.Create<string, int>(StringComparer.Ordinal)
				.Add("Card01", 4)
				.Add("Ghost", 36),
		};

		var cards = deck.Materialize(1, Index(pool));

		Assert.That(cards, Has.Count.EqualTo(Decklist.DeckSize));
		Assert.That(deck.MissingFrom(Index(pool)), Does.Contain("Ghost"));
	}

	[Test]
	public void Validate_RejectsMoreThanFourCopies()
	{
		// Nothing in the ENGINE enforces this — ZooDeckFactory runs 4x Ancestral Recall — so
		// it has to hold here or a hill climb converges on 36 copies of the best card.
		var illegal = Build(20) with
		{
			Spells = ImmutableSortedDictionary
				.Create<string, int>(StringComparer.Ordinal)
				.Add("Card01", 40),
		};

		Assert.That(illegal.Validate(), Does.Contain("max 4"));
	}

	[Test]
	public void WithCopies_ClampsAtTheCap_AndRemovesAtZero()
	{
		var deck = Build(20, ("Card01", 2));

		Assert.That(
			deck.WithCopies("Card01", 9).CopiesOf("Card01"),
			Is.EqualTo(Decklist.MaxCopies)
		);
		Assert.That(deck.WithCopies("Card01", 0).Spells.ContainsKey("Card01"), Is.False);
	}

	[TestCase(19)]
	[TestCase(27)]
	public void Validate_RejectsLandCountsOutsideConstructedRange(int lands)
	{
		var deck = Build(lands, ("Card01", 4)) with { Lands = lands };
		Assert.That(deck.Validate(), Is.Not.Null);
	}

	// ===== Difference =====

	[Test]
	public void Difference_IdenticalDecks_IsZero()
	{
		var deck = Build(20, ("Card01", 4), ("Card02", 4));
		Assert.That(Decklist.Difference(deck, deck), Is.EqualTo(0).Within(1e-9));
	}

	[Test]
	public void Difference_DisjointDecks_IsOne()
	{
		var a = Build(20, ("Card01", 4));
		var b = Build(20, ("Card02", 4));
		Assert.That(Decklist.Difference(a, b), Is.EqualTo(1).Within(1e-9));
	}

	[Test]
	public void Difference_IgnoresLands()
	{
		// Every deck plays the same Plains. Counting them would report two totally different
		// decks as ~35% similar before a single spell was compared.
		var a = Build(20, ("Card01", 4));
		var b = Build(26, ("Card01", 4));

		Assert.That(Decklist.Difference(a, b), Is.EqualTo(0).Within(1e-9));
	}

	[Test]
	public void Difference_CountsCopies_NotJustNames()
	{
		var four = Build(20, ("Card01", 4));
		var one = Build(20, ("Card01", 1));

		var diff = Decklist.Difference(four, one);
		Assert.That(diff, Is.GreaterThan(0), "4x vs 1x is mostly a difference, not a match");
		Assert.That(diff, Is.LessThan(1));
	}

	// ===== Seeding =====

	[Test]
	public void Seed_IsDeterministicForAFixedSeed()
	{
		var pool = Pool();
		var a = DeckBuilder.Seed("D", pool, NoData(), new Random(42));
		var b = DeckBuilder.Seed("D", pool, NoData(), new Random(42));

		Assert.That(Decklist.Difference(a, b), Is.EqualTo(0).Within(1e-9));
		Assert.That(a.Lands, Is.EqualTo(b.Lands));
	}

	[Test]
	public void Seed_ProducesAValidDeck()
	{
		var pool = Pool();
		for (var s = 0; s < 25; s++)
		{
			var deck = DeckBuilder.Seed("D", pool, NoData(), new Random(s));
			Assert.That(deck.Validate(), Is.Null, $"seed {s}: {deck.Validate()}");
		}
	}

	[Test]
	public void SeedField_ProducesDistinctDecks()
	{
		var pool = Pool(60);
		var field = DeckBuilder.SeedField(8, pool, NoData(), new Random(3), minDifference: 0.35);

		Assert.That(field, Has.Count.EqualTo(8));
		for (var i = 0; i < field.Count; i++)
		{
			var others = field.Where((_, j) => j != i).ToList();
			Assert.That(
				DeckBuilder.MinDifference(field[i], others),
				Is.GreaterThanOrEqualTo(0.35),
				$"deck {i} is too close to another"
			);
		}
	}

	// ===== Mutation =====

	[Test]
	public void Mutate_AlwaysProducesAValidDeck_OrNothing()
	{
		var pool = Pool();
		var values = NoData();
		var deck = DeckBuilder.Seed("D", pool, values, new Random(11));
		var rng = new Random(11);

		var produced = 0;
		for (var i = 0; i < 300; i++)
		{
			var mutant = DeckBuilder.Mutate(deck, pool, values, rng);
			if (mutant is null)
				continue;
			produced++;
			Assert.That(mutant.Validate(), Is.Null, mutant.Validate());
			Assert.That(
				Decklist.Difference(deck, mutant),
				Is.GreaterThan(0),
				"a mutant identical to its parent is a wasted evaluation"
			);
		}

		Assert.That(produced, Is.GreaterThan(100), "most proposals should succeed");
	}

	[Test]
	public void Mutate_IsASmallStep_NotAReseed()
	{
		// A large rewrite is indistinguishable from a fresh seed: it destroys the hill being
		// climbed, and the paired comparison then measures two unrelated decks.
		var pool = Pool();
		var values = NoData();
		var deck = DeckBuilder.Seed("D", pool, values, new Random(5));
		var rng = new Random(5);

		for (var i = 0; i < 200; i++)
		{
			var mutant = DeckBuilder.Mutate(deck, pool, values, rng);
			if (mutant is null)
				continue;
			Assert.That(
				Decklist.Difference(deck, mutant),
				Is.LessThan(0.35),
				"one mutation should not rewrite a third of the deck"
			);
		}
	}

	// ===== The limited -> constructed blend =====

	[Test]
	public void WithNoConstructedData_ValuesAreExactlyTheDraftModel()
	{
		// The degenerate case the mode starts in every fresh run. If this drifts, generation 1
		// is seeding off something that is neither the draft model nor measured constructed
		// data.
		var draft = new DraftTrainingData(
			1000,
			500,
			[new CardStat("Good", 400, 280), new CardStat("Bad", 400, 120)],
			[]
		);
		var values = new ConstructedValues(DraftTrainingData.Empty, draft);

		var expectedGood = 100 * (DraftTrainingData.Shrink(280, 400, 0.5, 25) - 0.5);
		Assert.That(values.CardDelta("Good"), Is.EqualTo(expectedGood).Within(1e-9));
		Assert.That(values.CardDelta("Good"), Is.GreaterThan(0));
		Assert.That(values.CardDelta("Bad"), Is.LessThan(0));
	}

	[Test]
	public void WithNoModelAtAll_EveryCardScoresZero()
	{
		// The control arm: quality-blind seeding, so the run cannot inherit a limited
		// valuation it never read.
		var values = NoData();
		Assert.That(values.CardDelta("Anything"), Is.EqualTo(0).Within(1e-9));
		Assert.That(values.HasDraftPrior, Is.False);
	}

	[Test]
	public void ConstructedEvidence_OverridesTheDraftPrior()
	{
		// The whole point of the blend. A card the draft model loves, that loses constantly in
		// constructed, must end up valued negatively — otherwise the mode just rebuilds draft
		// decks.
		var draft = new DraftTrainingData(1000, 500, [new CardStat("LimitedBomb", 400, 320)], []);
		var constructed = new DraftTrainingData(
			20000,
			10000,
			[new CardStat("LimitedBomb", 8000, 2400)],
			[]
		);

		var early = new ConstructedValues(DraftTrainingData.Empty, draft);
		var late = new ConstructedValues(constructed, draft);

		Assert.That(early.CardDelta("LimitedBomb"), Is.GreaterThan(5), "draft says it is a bomb");
		Assert.That(
			late.CardDelta("LimitedBomb"),
			Is.LessThan(-5),
			"constructed evidence must win once there is enough of it"
		);
	}

	[Test]
	public void ASingleConstructedGame_BarelyMovesTheValuation()
	{
		// The other half of the blend: shrinkage, so one unlucky game cannot reprice a card.
		//
		// It does move it, and that is correct rather than a bug — at CardShrinkK = 25 a single
		// observation carries 1/26 of the weight by construction, which is the same sensitivity
		// DraftPickers has always had. What must hold is that the value stays near the draft
		// prior and nowhere near the raw 0% observed.
		var draft = new DraftTrainingData(1000, 500, [new CardStat("Card", 400, 280)], []);
		var oneLoss = new DraftTrainingData(2, 1, [new CardStat("Card", 1, 0)], []);

		var before = new ConstructedValues(DraftTrainingData.Empty, draft).CardDelta("Card");
		var after = new ConstructedValues(oneLoss, draft).CardDelta("Card");

		Assert.That(before, Is.GreaterThan(0));
		Assert.That(after, Is.LessThan(before), "one loss should nudge it down");
		Assert.That(
			after,
			Is.GreaterThan(before * 0.8),
			"but a single game must not erode most of a 400-game draft estimate"
		);
	}

	[Test]
	public void OneGenerationOfEvidence_MovesTheValuationSubstantially()
	{
		// The counterweight to the test above, and the one that would catch a shrink so heavy
		// the constructed table can never take over. A card in a deck accrues on the order of
		// 168 deck-games per generation, so the handover has to happen on that scale.
		var draft = new DraftTrainingData(1000, 500, [new CardStat("Card", 400, 280)], []);
		var oneGeneration = new DraftTrainingData(336, 168, [new CardStat("Card", 168, 34)], []);

		var before = new ConstructedValues(DraftTrainingData.Empty, draft).CardDelta("Card");
		var after = new ConstructedValues(oneGeneration, draft).CardDelta("Card");

		Assert.That(before, Is.GreaterThan(10), "draft rates it well");
		Assert.That(after, Is.LessThan(0), "a generation of losing must flip the sign");
	}

	[Test]
	public void Movers_ReportsTheCardsThatDisagreeBetweenFormats()
	{
		// This is the run's acceptance test for "did it escape the limited prior", so it has
		// to actually rank the disagreement.
		var draft = new DraftTrainingData(
			2000,
			1000,
			[new CardStat("Riser", 800, 360), new CardStat("Faller", 800, 440)],
			[]
		);
		var constructed = new DraftTrainingData(
			20000,
			10000,
			[new CardStat("Riser", 8000, 5600), new CardStat("Faller", 8000, 2400)],
			[]
		);

		var movers = new ConstructedValues(constructed, draft).Movers();

		Assert.That(movers.First().Name, Is.EqualTo("Riser"));
		Assert.That(movers.Last().Name, Is.EqualTo("Faller"));
		Assert.That(movers.First().Move, Is.GreaterThan(0));
		Assert.That(movers.Last().Move, Is.LessThan(0));
	}

	[Test]
	public void Movers_CentresOutTheCrossFormatOffset()
	{
		// Both tables are games-in-hand rates, and a card is only credited for a game in which
		// it was DRAWN. Constructed games end faster, so the two formats sit several points
		// apart even at identical priors — measured at 7.2pp on the first real run, which made
		// EVERY card read as "worse in constructed" by ~9pp.
		//
		// Here every card is shifted down by the same amount, so the honest answer is that no
		// card has been repriced RELATIVE to the others.
		var draft = new DraftTrainingData(
			4000,
			2000,
			[
				new CardStat("A", 1000, 600),
				new CardStat("B", 1000, 500),
				new CardStat("C", 1000, 400),
			],
			[]
		);
		// Uniformly worse: 10 points off every card.
		var constructed = new DraftTrainingData(
			40000,
			20000,
			[
				new CardStat("A", 10000, 5000),
				new CardStat("B", 10000, 4000),
				new CardStat("C", 10000, 3000),
			],
			[]
		);

		var movers = new ConstructedValues(constructed, draft).Movers();

		Assert.That(movers, Has.Count.EqualTo(3));
		Assert.That(
			movers.Select(m => m.Move),
			Is.All.EqualTo(0).Within(1.5),
			"a uniform shift is an offset, not a repricing"
		);
		Assert.That(
			movers.Select(m => m.RawMove),
			Is.All.LessThan(-5),
			"the raw offset must still be visible rather than silently discarded"
		);
	}

	// ===== Synergy gating =====

	[Test]
	public void ThinPairs_ContributeExactlyZeroSynergy()
	{
		// The gate is load-bearing. MtgSimulator/CLAUDE.md records that summing many noisy pair
		// deltas cost win rate at every weight tested; this mode only survives that finding by
		// using a few well-evidenced pairs. Without the gate it becomes the disproved thing.
		var draft = new DraftTrainingData(
			1000,
			500,
			[new CardStat("A", 400, 200), new CardStat("B", 400, 200)],
			[new PairStat("A", "B", Games: 2, Wins: 2)]
		);

		var values = new ConstructedValues(DraftTrainingData.Empty, draft);

		Assert.That(values.SynergyDelta("A", "B"), Is.EqualTo(0).Within(1e-9));
	}

	[Test]
	public void WellEvidencedPairs_ReportRealSynergy()
	{
		// And the gate must not be so tight that nothing ever gets through — a test that only
		// asserted the zero above would pass on a synergy term that never fires.
		var draft = new DraftTrainingData(
			10000,
			5000,
			[new CardStat("A", 4000, 2000), new CardStat("B", 4000, 2000)],
			[new PairStat("A", "B", Games: 4000, Wins: 3200)]
		);

		var values = new ConstructedValues(DraftTrainingData.Empty, draft);

		Assert.That(values.SynergyDelta("A", "B"), Is.GreaterThan(5));
	}

	[Test]
	public void RealisticPairEvidence_ClearsTheGate()
	{
		// REGRESSION. The gate was 200 while the CSC draft table's BUSIEST pair had 166 games
		// (median 53), so SynergyDelta returned 0 for all 83 028 pairs: the seeder's kernel
		// never formed and cut scoring collapsed to card quality plus curve. The decks were
		// piles of good cards with visible anti-synergies.
		//
		// The old test used Games = 4000 and passed throughout, which is why this one uses a
		// volume the real data actually reaches. If MinPairGames is raised above ~60 again,
		// this fails instead of the mode silently going quiet.
		var draft = new DraftTrainingData(
			10000,
			5000,
			[new CardStat("A", 4000, 2000), new CardStat("B", 4000, 2000)],
			[new PairStat("A", "B", Games: 60, Wins: 48)]
		);

		var values = new ConstructedValues(DraftTrainingData.Empty, draft);

		Assert.That(
			values.SynergyDelta("A", "B"),
			Is.Not.EqualTo(0),
			"a pair with realistic draft-table volume must be able to influence the mode"
		);
	}

	[Test]
	public void TopPartners_FindsAKernel_AtRealisticEvidence()
	{
		// The seeding half of the same regression: an empty TopPartners means Seed degrades
		// from "anchor + kernel + curve" to "anchor + curve" with nothing reporting it.
		var pool = Pool(6);
		var draft = new DraftTrainingData(
			10000,
			5000,
			pool.Select(c => new CardStat(c.Name, 4000, 2000)).ToList(),
			[new PairStat("Card00", "Card01", 60, 48), new PairStat("Card00", "Card02", 60, 46)]
		);

		var partners = new ConstructedValues(DraftTrainingData.Empty, draft).TopPartners(
			"Card00",
			pool,
			4
		);

		Assert.That(partners, Is.Not.Empty, "the kernel must actually form");
		Assert.That(partners.Select(p => p.Name), Does.Contain("Card01"));
	}

	// ===== Per-deck history: synergy-aware cutting =====

	[Test]
	public void DeckHistory_ProtectsACardWhosePairsWin_AndExposesOneWhosePairsLose()
	{
		var deck = Build(24, ("Good", 4), ("Bad", 4), ("Partner", 4));

		// Same solo record for Good and Bad — only their PAIRS differ, which is the whole
		// point: a synergy piece with an unremarkable solo rate must not be cut first.
		var data = new DraftTrainingData(
			1000,
			500,
			[
				new CardStat("Good", 800, 400),
				new CardStat("Bad", 800, 400),
				new CardStat("Partner", 800, 400),
			],
			[
				new PairStat("Good", "Partner", Games: 400, Wins: 320),
				new PairStat("Bad", "Partner", Games: 400, Wins: 80),
			]
		);

		var history = new DeckHistory(data);

		Assert.That(history.CardDelta("Good"), Is.EqualTo(history.CardDelta("Bad")).Within(1e-9));
		Assert.That(
			history.KeepScore("Good", deck),
			Is.GreaterThan(history.KeepScore("Bad", deck)),
			"identical solo rates, so the pair record has to be what separates them"
		);
		Assert.That(history.PairDelta("Bad", deck), Is.LessThan(0));
	}

	[Test]
	public void DeckHistory_BiasesTowardTheCardWhosePairsAreBetter()
	{
		// This started as "bias toward the card in MORE pairs" and the imputation fix changed
		// it, deliberately. Once unmeasured pairs are imputed at the deck's mean, a card with
		// three average pairs and a card with one average pair plus two imputed ones score
		// IDENTICALLY — which is correct, because they are the same estimate.
		//
		// Breadth and tenure are the same quantity here: you cannot reward "many measured
		// pairs" without rewarding "has been in the deck longer", which is exactly the bug that
		// cut Sol Ring. So breadth now pays only to the extent the pairs are ABOVE average.
		var deck = Build(20, ("Hub", 4), ("Lone", 4), ("P1", 4), ("P2", 4), ("P3", 4));

		var data = new DraftTrainingData(
			1000,
			500,
			[
				new CardStat("Hub", 800, 400),
				new CardStat("Lone", 800, 400),
				new CardStat("P1", 800, 400),
				new CardStat("P2", 800, 400),
				new CardStat("P3", 800, 400),
			],
			[
				new PairStat("Hub", "P1", 400, 300),
				new PairStat("Hub", "P2", 400, 300),
				new PairStat("Hub", "P3", 400, 300),
				new PairStat("Lone", "P1", 400, 140),
			]
		);

		var history = new DeckHistory(data);

		Assert.That(history.SupportCount("Hub", deck), Is.EqualTo(3));
		Assert.That(history.SupportCount("Lone", deck), Is.EqualTo(1));
		Assert.That(
			history.KeepScore("Hub", deck),
			Is.GreaterThan(history.KeepScore("Lone", deck))
		);
	}

	[Test]
	public void DeckHistory_DoesNotCountAPairWhosePartnerHasBeenCut()
	{
		// A pair record about a card no longer in the deck is not evidence about the deck now.
		// It is not counted — but the slot it would have filled is imputed like any other
		// unmeasured pair rather than dropping to zero, which is the tenure fix.
		var withPartner = Build(24, ("Card", 4), ("Partner", 4));
		var without = Build(24, ("Card", 4), ("Other", 4));

		// A second, mediocre pair so the deck's mean sits BELOW the Card+Partner pair. With only
		// one pair in the data the mean is that pair, and measured and imputed coincide.
		var data = new DraftTrainingData(
			1000,
			500,
			[
				new CardStat("Card", 800, 400),
				new CardStat("Partner", 800, 400),
				new CardStat("Filler", 800, 400),
			],
			[new PairStat("Card", "Partner", 400, 320), new PairStat("Partner", "Filler", 400, 180)]
		);
		var history = new DeckHistory(data);

		Assert.That(history.SupportCount("Card", withPartner), Is.EqualTo(1));
		Assert.That(history.SupportCount("Card", without), Is.EqualTo(0));
		Assert.That(
			history.PairDelta("Card", without),
			Is.EqualTo(history.MeanPairDelta).Within(1e-9),
			"pure imputation — the cut partner's measured record contributes nothing"
		);
		Assert.That(
			history.PairDelta("Card", withPartner),
			Is.GreaterThan(history.PairDelta("Card", without)),
			"a measured good pair must still beat an imputed one"
		);
	}

	[Test]
	public void DeckHistory_DoesNotPunishACardMerelyForBeingNew()
	{
		// REGRESSION — the tenure bug. Summing only MEASURED pairs scored a card added this
		// generation at exactly 0 while an entrenched card in several measured pairs carried
		// +20, so every new card was the weakest thing in the deck on arrival and was cut
		// before it could prove anything.
		//
		// It threw away the two best cards in the format: Ancestral Recall (+14.91pp measured
		// in constructed) cut after 812 games and Sol Ring (+20.62pp, highest in the run) after
		// 2 209, while an +11.04pp card that arrived early accumulated 27 264.
		var deck = Build(24, ("Old", 4), ("P1", 4), ("P2", 4), ("P3", 4), ("Newcomer", 4));

		// Everything measured is positive; Newcomer simply has no pair record yet.
		var data = new DraftTrainingData(
			1000,
			500,
			[
				new CardStat("Old", 800, 400),
				new CardStat("P1", 800, 400),
				new CardStat("P2", 800, 400),
				new CardStat("P3", 800, 400),
			],
			[
				new PairStat("Old", "P1", 400, 250),
				new PairStat("Old", "P2", 400, 250),
				new PairStat("Old", "P3", 400, 250),
			]
		);

		var history = new DeckHistory(data);

		Assert.That(history.MeanPairDelta, Is.GreaterThan(0));
		Assert.That(
			history.PairDelta("Newcomer", deck),
			Is.GreaterThan(0),
			"an unmeasured pair is UNKNOWN, not bad — imputing zero is what cut Sol Ring"
		);
		Assert.That(
			history.KeepScore("Newcomer", deck),
			Is.GreaterThan(0.5 * history.KeepScore("Old", deck)),
			"a new card must be broadly comparable to an entrenched one, not automatically last"
		);
	}

	[Test]
	public void DeckHistory_StillCutsACardWhosePairsAreMeasuredBad()
	{
		// The counterweight: imputing at the mean must not make a genuinely bad card safe.
		// Without this, the fix above would silently disable synergy-aware cutting entirely.
		var deck = Build(24, ("Bad", 4), ("P1", 4), ("P2", 4), ("Newcomer", 4));

		var data = new DraftTrainingData(
			1000,
			500,
			[
				new CardStat("Bad", 800, 400),
				new CardStat("P1", 800, 400),
				new CardStat("P2", 800, 400),
			],
			[
				new PairStat("Bad", "P1", 400, 100),
				new PairStat("Bad", "P2", 400, 100),
				new PairStat("P1", "P2", 400, 300),
			]
		);

		var history = new DeckHistory(data);

		Assert.That(
			history.KeepScore("Bad", deck),
			Is.LessThan(history.KeepScore("Newcomer", deck)),
			"measured-bad must still lose to unknown"
		);
	}

	// ===== Package mutation =====

	[Test]
	public void Package_BringsInACardAndItsPartnersTogether()
	{
		// Single-card hill climbing cannot cross a synergy valley: each half of a combo is bad
		// alone, so every one-card step toward it is rejected and the combo is unreachable.
		// This asserts the operator can land both halves in one move.
		var pool = Pool(40);
		var draft = new DraftTrainingData(
			100000,
			50000,
			pool.Select(c => new CardStat(c.Name, 40000, 20000)).ToList(),
			[
				new PairStat("Card30", "Card31", 4000, 3400),
				new PairStat("Card30", "Card32", 4000, 3300),
			]
		);
		var values = new ConstructedValues(DraftTrainingData.Empty, draft);

		var deck = Build(
			24,
			("Card00", 4),
			("Card01", 4),
			("Card02", 4),
			("Card03", 4),
			("Card04", 4),
			("Card05", 4),
			("Card06", 4),
			("Card07", 4),
			("Card08", 4)
		);
		Assume.That(deck.Validate(), Is.Null, deck.Validate());

		// Sample many proposals; the operator fires on ~2 rolls in 10.
		var landedTogether = false;
		var rng = new Random(7);
		for (var i = 0; i < 400 && !landedTogether; i++)
		{
			var m = DeckBuilder.Mutate(deck, pool, values, rng);
			if (m is null)
				continue;
			Assert.That(m.Validate(), Is.Null, m.Validate());
			if (m.CopiesOf("Card30") > 0 && (m.CopiesOf("Card31") > 0 || m.CopiesOf("Card32") > 0))
				landedTogether = true;
		}

		Assert.That(
			landedTogether,
			Is.True,
			"a synergy package must be able to arrive as one move, or combos stay unreachable"
		);
	}

	[Test]
	public void AGloballyTerribleCard_IsAlwaysCuttable()
	{
		// REGRESSION. The "never cut a carrying card" filter scored candidates on LOCAL history
		// only, so a card's global value could not reach it: inside its own deck a card's win
		// rate sits near that deck's own rate, its local delta is ~0, and with the imputed pair
		// term it lands at or above the local mean — excluded from the cut candidates and
		// therefore uncuttable.
		//
		// Dragonstorm, the worst card in a 780-card pool at -22.86pp, survived 2 096 games that
		// way; Thoughtcast held slots in three decks with no artifacts; and Ancestral Recall
		// never got in at all because the slots were locked.
		var pool = Pool(12);
		var junk = "Card11";

		// Globally: everything is fine except the junk, which is catastrophic.
		var draft = new DraftTrainingData(
			100000,
			50000,
			pool.Select(c => new CardStat(c.Name, 40000, c.Name == junk ? 8000 : 22000)).ToList(),
			[]
		);
		var values = new ConstructedValues(DraftTrainingData.Empty, draft);
		Assume.That(values.CardDelta(junk), Is.LessThan(-10), "fixture must make it clearly bad");

		var deck = Build(
			24,
			("Card00", 4),
			("Card01", 4),
			("Card02", 4),
			("Card03", 4),
			("Card04", 4),
			("Card05", 4),
			("Card06", 4),
			("Card07", 4),
			(junk, 4)
		);
		Assume.That(deck.Validate(), Is.Null, deck.Validate());

		// Local history says nothing useful: every card looks like the deck average, which is
		// exactly the state that made the junk uncuttable.
		var flat = new DraftTrainingData(
			1000,
			500,
			deck.Spells.Keys.Select(n => new CardStat(n, 800, 400)).ToList(),
			[]
		);
		var history = new DeckHistory(flat);

		var rng = new Random(3);
		var cutJunk = 0;
		var proposals = 0;
		for (var i = 0; i < 400; i++)
		{
			var m = DeckBuilder.Mutate(deck, pool, values, rng, history);
			if (m is null)
				continue;
			proposals++;
			if (m.CopiesOf(junk) < deck.CopiesOf(junk))
				cutJunk++;
		}

		Assume.That(proposals, Is.GreaterThan(50));
		Assert.That(
			cutJunk,
			Is.GreaterThan(0),
			"the worst card in the format must be reachable by the cut, whatever its local record"
		);

		// Not vacuous: reachable is not enough, it must be PREFERRED. Nine distinct cards means
		// a blind cut hits the junk ~11% of the time; the global value has to do better than
		// chance or the filter is still swallowing it.
		Assert.That(
			(double)cutJunk / proposals,
			Is.GreaterThan(0.25),
			$"junk cut in only {cutJunk}/{proposals} proposals — barely better than a blind cut"
		);
	}

	[Test]
	public void DeckHistory_WithNoGames_IsInert()
	{
		// Generation 1: no deck has played anything yet, so cutting must fall back to card
		// quality without throwing or biasing.
		var deck = Build(24, ("Card01", 4));
		var history = new DeckHistory(DraftTrainingData.Empty);

		Assert.That(history.DeckGames, Is.EqualTo(0));
		Assert.That(history.KeepScore("Card01", deck), Is.EqualTo(0).Within(1e-9));
	}

	[Test]
	public void TwoTerribleCards_AreNotSelectedAsPartners_EvenWithHugeSynergy()
	{
		// REGRESSION, and the one that produced the storm-deck disaster. ExpectedPairRate
		// predicts a catastrophic rate for two individually-awful cards, so a pair that merely
		// performs badly reads as strong POSITIVE synergy. Measured on a real run: Dragonstorm +
		// Tendrils of Agony scored +8.91pp of synergy against a baseline expecting -35.4pp —
		// about -26pp in absolute terms, while looking like a discovery.
		//
		// The worst cards in a format are the EASIEST to show synergy for. Selection must use
		// absolute joint performance.
		var pool = Pool(10);
		var draft = new DraftTrainingData(
			100000,
			50000,
			[
				new CardStat("Junk1", 40000, 8000), // ~20% alone
				new CardStat("Junk2", 40000, 8000),
				.. Pool(10).Skip(2).Select(c => new CardStat(c.Name, 40000, 20000)),
			],
			// Together they reach ~30%: a big lift over the ~7% independence predicts, and
			// still far below the 50% base rate.
			[new PairStat("Junk1", "Junk2", 8000, 2400)]
		);
		var values = new ConstructedValues(DraftTrainingData.Empty, draft);

		Assert.That(
			values.SynergyDelta("Junk1", "Junk2"),
			Is.GreaterThan(0),
			"fixture check: the interaction really is positive"
		);
		Assert.That(
			values.JointDelta("Junk1", "Junk2"),
			Is.LessThan(0),
			"but together they still lose — which is what selection must see"
		);

		var junkPool = new List<Card> { Spell("Junk1", 2), Spell("Junk2", 2) };
		Assert.That(
			values.TopPartners("Junk1", junkPool, 4),
			Is.Empty,
			"a pair that loses together must never be proposed as a package"
		);
	}

	[Test]
	public void AGenuineEnabler_IsStillSelected_EvenIfWeakAlone()
	{
		// The counterweight. Absolute joint scoring must not simply become "only pair good
		// cards" — a discard outlet is card disadvantage alone and the whole point alongside a
		// madness payoff. Without this, the fix above would silently disable packages.
		var draft = new DraftTrainingData(
			100000,
			50000,
			[new CardStat("Enabler", 40000, 17000), new CardStat("Payoff", 40000, 22000)],
			[new PairStat("Enabler", "Payoff", 8000, 5600)] // 70% together
		);
		var values = new ConstructedValues(DraftTrainingData.Empty, draft);

		Assert.That(values.CardDelta("Enabler"), Is.LessThan(0), "weak on its own");
		Assert.That(values.JointDelta("Enabler", "Payoff"), Is.GreaterThan(0), "strong together");
		Assert.That(
			values.TopPartners("Payoff", [Spell("Enabler", 1)], 4).Select(p => p.Name),
			Does.Contain("Enabler")
		);
	}

	[Test]
	public void DeckFit_StaysCommensurateWithCardValue()
	{
		// The scaling bug. SynergyWithDeck SUMMED over every card weighted by copies, so a
		// 10-card, 40-copy deck produced scores near +290 against card values in the ±25 range
		// — card quality became rounding error, and Deck B rated Dragonstorm (+261 combined)
		// above Ancestral Recall (+240).
		var deck = Build(
			20,
			("Card00", 4),
			("Card01", 4),
			("Card02", 4),
			("Card03", 4),
			("Card04", 4),
			("Card05", 4),
			("Card06", 4),
			("Card07", 4),
			("Card08", 4),
			("Card09", 4)
		);
		Assume.That(deck.Validate(), Is.Null, deck.Validate());

		var cards = deck.Spells.Keys.Append("Newcomer").ToList();
		var pairs = deck
			.Spells.Keys.Select(n => new PairStat(
				string.CompareOrdinal("Newcomer", n) <= 0 ? "Newcomer" : n,
				string.CompareOrdinal("Newcomer", n) <= 0 ? n : "Newcomer",
				4000,
				2600
			))
			.ToList();

		var values = new ConstructedValues(
			DraftTrainingData.Empty,
			new DraftTrainingData(
				100000,
				50000,
				cards.Select(n => new CardStat(n, 40000, 20000)).ToList(),
				pairs
			)
		);

		var fit = values.DeckFit("Newcomer", deck);
		Assert.That(fit, Is.GreaterThan(0), "it does win alongside this deck");
		Assert.That(
			Math.Abs(fit),
			Is.LessThan(40),
			$"DeckFit {fit:F1} must stay in card-value units — summing gave ~290 and drowned quality"
		);
	}

	[Test]
	public void Mutate_SometimesAddsAPlainGoodCard_WithNoPairEvidenceAtAll()
	{
		// Pair data is the thinnest part of the model, so a mutator that only proposes packages
		// can never fill a slot with a plainly strong card that has no measured partners.
		var pool = Pool(30);
		// Card29 is clearly the best card and has no pair data whatsoever.
		var draft = new DraftTrainingData(
			100000,
			50000,
			pool.Select(c => new CardStat(c.Name, 40000, c.Name == "Card29" ? 30000 : 20000))
				.ToList(),
			[]
		);
		var values = new ConstructedValues(DraftTrainingData.Empty, draft);

		var deck = Build(
			24,
			("Card00", 4),
			("Card01", 4),
			("Card02", 4),
			("Card03", 4),
			("Card04", 4),
			("Card05", 4),
			("Card06", 4),
			("Card07", 4),
			("Card08", 4)
		);
		Assume.That(deck.Validate(), Is.Null, deck.Validate());

		var rng = new Random(11);
		var arrived = false;
		for (var i = 0; i < 400 && !arrived; i++)
		{
			var m = DeckBuilder.Mutate(deck, pool, values, rng);
			if (m is not null && m.CopiesOf("Card29") > 0)
				arrived = true;
		}

		Assert.That(
			arrived,
			Is.True,
			"the best card in the pool must be reachable without partners"
		);
	}

	[Test]
	public void SynergyIsSymmetric()
	{
		var draft = new DraftTrainingData(
			10000,
			5000,
			[new CardStat("A", 4000, 2000), new CardStat("B", 4000, 2000)],
			[new PairStat("A", "B", 4000, 3200)]
		);
		var values = new ConstructedValues(DraftTrainingData.Empty, draft);

		Assert.That(values.SynergyDelta("A", "B"), Is.EqualTo(values.SynergyDelta("B", "A")));
	}

	// ===== Self-check: can the fitness measurement see anything at all? =====

	/// <summary>
	/// **Run this before trusting any number this mode produces.**
	///
	/// This project has already shipped a comparison that silently measured nothing —
	/// `maxBranching` raised while `expandBranching` stayed capped — and the whole evolution
	/// loop rests on win rate over a few dozen games being able to separate two decks. A 50%
	/// result is indistinguishable from "no effect", which is the answer most of these runs
	/// hope for, so the instrument has to prove it can see a difference first.
	///
	/// A deck of 1/1s for five mana against a deck of 3/3s for two is the largest difference
	/// the card pool can express. If THIS does not land far below the viability floor, nothing
	/// smaller ever will and every matrix the mode prints is noise.
	///
	/// Explicit because it plays real games.
	/// </summary>
	[Test]
	[Explicit("Plays 40 real games — run manually before trusting an evolution run.")]
	public void SelfCheck_ADeliberatelyTerribleDeckLosesFarBelowTheViabilityFloor()
	{
		var pool = new List<Card>();
		for (var i = 0; i < 9; i++)
		{
			pool.Add(
				CardFactory.Creature($"Efficient{i}", manaCost: 2, power: 3, toughness: 3).Build()
			);
			pool.Add(
				CardFactory.Creature($"Awful{i}", manaCost: 5, power: 1, toughness: 1).Build()
			);
		}
		var index = ConstructedGameSetup.PoolIndex(pool);

		var good = Build(24, Enumerable.Range(0, 9).Select(i => ($"Efficient{i}", 4)).ToArray());
		var bad = Build(24, Enumerable.Range(0, 9).Select(i => ($"Awful{i}", 4)).ToArray());

		Assert.That(good.Validate(), Is.Null);
		Assert.That(bad.Validate(), Is.Null);

		var badWins = 0;
		var counted = 0;
		const int Games = 40;

		for (var k = 0; k < Games; k++)
		{
			// Alternate who is on the play, or the result measures seat order.
			var badOnPlay = k % 2 == 0;
			var (deck1, deck2) = badOnPlay ? (bad, good) : (good, bad);
			var seed = 5000 + k * 5;

			var (state, ids, cardNames) = ConstructedGameSetup.Build(deck1, deck2, index);
			var aiRng = new Random(seed + 4);
			var runner = new GameRunner(
				new MultiTurnBeamSearchAiStrategy(ids, 2, rng: aiRng),
				new MultiTurnBeamSearchAiStrategy(ids, 2, rng: aiRng)
			);
			var (result, _) = runner.Run(
				state,
				ids,
				cardNames,
				shuffleSeed: seed + 2,
				gameRngSeed: seed + 3
			);

			if (
				result.EndReason
				is GameEndReason.TimeLimitReached
					or GameEndReason.UnhandledException
			)
				continue;

			counted++;
			if (badOnPlay ? result.IsPlayer1Win : result.IsPlayer2Win)
				badWins++;
		}

		Assert.That(counted, Is.GreaterThan(Games / 2), "too many games were excluded to conclude");

		var rate = (double)badWins / counted;
		TestContext.Out.WriteLine($"Terrible deck won {badWins}/{counted} = {rate:P1}");

		Assert.That(
			rate,
			Is.LessThan(0.40),
			"the fitness measurement cannot separate the worst deck expressible from a good "
				+ "one, so no result from this mode is trustworthy"
		);
	}

	// ===== Combined pool =====

	[Test]
	public void CombinedPool_HasNoDuplicateNames()
	{
		// A duplicate name would break every name-keyed table in the project — Decklist,
		// CardStat, CardValue and the runner's cardNames map.
		var names = SetRegistry.Combined.Cards.Select(c => c.Name).ToList();

		Assert.That(
			names.Count,
			Is.EqualTo(names.Distinct(StringComparer.OrdinalIgnoreCase).Count())
		);
	}

	[Test]
	public void CombinedPool_ResolvesDuplicatesToTheLastPrinting()
	{
		var replaced = SetRegistry.CombinedReplacements;
		Assume.That(replaced, Is.Not.Empty, "no colliding names to check");

		foreach (var (name, winningSet) in replaced)
		{
			var expected = SetRegistry
				.Get(winningSet)
				.Cards.First(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
			var actual = SetRegistry.Combined.Cards.First(c =>
				string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
			);

			Assert.That(actual.ManaCost, Is.EqualTo(expected.ManaCost), name);
		}
	}

	[Test]
	public void CombinedPool_ContainsEveryUniqueCardFromEverySet()
	{
		var expected = SetRegistry
			.All.SelectMany(s => s.Cards)
			.Select(c => c.Name)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Count();

		Assert.That(SetRegistry.Combined.Cards, Has.Count.EqualTo(expected));
	}

	// ===== Feature-driven support (PoolFeatures in DeckBuilder) =====

	/// A tribe member: supplies the lord's demand and asks for nothing itself.
	private static Card Tribesman(string name, string tribe = "Orc") =>
		CardFactory.Creature(name, manaCost: 2, power: 2, toughness: 2).WithSubtype(tribe).Build();

	/// A lord: demands its tribe and is worth nothing without it.
	private static Card Lord(string name, string tribe = "Orc") =>
		CardFactory
			.Creature(name, manaCost: 3, power: 2, toughness: 2)
			.WithSubtype(tribe)
			.WithComponent(
				new StaticPTBoostAbility
				{
					PowerBonus = 1,
					ToughnessBonus = 1,
					Filter = new IsSubtypeSpecification { Subtype = tribe }.And(
						new IsNotSelfSpecification()
					),
				}
			)
			.Build();

	/// <summary>
	/// Two independent archetypes, for the distinct-concept rule. A one-concept pool cannot test
	/// it: the second slot correctly finds nothing left and falls back, which looks identical to
	/// the rule not working.
	/// </summary>
	/// <summary>
	/// An anthem wanting ANY creature you control. Broad on purpose: it is the shape that broke
	/// concept selection on CSC, where "creatures you control" answers 237 of 408 cards.
	/// </summary>
	private static Card BroadAnthem() =>
		CardFactory
			.Creature("Anthem", manaCost: 3, power: 2, toughness: 2)
			.WithComponent(
				new StaticPTBoostAbility
				{
					PowerBonus = 1,
					ToughnessBonus = 1,
					Filter = new IsCreatureSpecification().And(
						new IsControlledByYouSpecification()
					),
				}
			)
			.Build();

	private static IReadOnlyList<Card> TwoTribePool() =>
		[
			Lord("OrcLord"),
			.. Enumerable.Range(0, 8).Select(i => Tribesman($"Orc{i}")),
			Lord("ElfLord", "Elf"),
			.. Enumerable.Range(0, 8).Select(i => Tribesman($"Elf{i}", "Elf")),
			BroadAnthem(),
			.. Enumerable.Range(0, 30).Select(i => Spell($"Filler{i}", 2)),
		];

	/// <summary>
	/// Deliberately filler-heavy: 4 tribe members against 30 neutrals.
	///
	/// **A narrow pool makes this measure nothing.** At 4 orcs against 8 fillers a random fill
	/// clusters the tribe by accident — the A/B below read 9.30 against 10.20 and would have
	/// passed on a term that barely worked. The pool has to be wide enough that support is
	/// evidence of the term rather than of the fixture.
	///
	/// Eight tribe members rather than four, because `DeckBuilder.MinConceptSuppliers` is 6: a
	/// demand four cards answer is an interaction, not an archetype, and a fixture below the
	/// threshold silently falls back to ordinary seeding and tests nothing.
	/// </summary>
	private static IReadOnlyList<Card> TribalPool() =>
		[
			Lord("Lord"),
			.. Enumerable.Range(0, 8).Select(i => Tribesman($"Orc{i}")),
			.. Enumerable.Range(0, 30).Select(i => Spell($"Filler{i}", 2)),
		];

	[Test]
	public void ACardTheDeckCannotSupportAtAll_IsCutFarMoreOftenThanChance()
	{
		// The Dragonstorm case: a lord in a deck with none of its tribe does nothing at all, and
		// before this nothing in the mode could tell that from a card that was merely unlucky.
		var pool = TribalPool();
		var features = PoolFeatures.Build(pool);
		(string Name, int Count)[] spells =
		[
			("Lord", 4),
			.. Enumerable.Range(0, 8).Select(i => (Name: $"Filler{i}", Count: 4)),
		];
		var deck = Build(24, spells);
		Assume.That(deck.Validate(), Is.Null, deck.Validate());
		Assume.That(
			features.Satisfaction("Lord", deck),
			Is.Zero,
			"fixture must leave the lord genuinely unsupported"
		);

		var rng = new Random(7);
		int proposals = 0,
			cutLord = 0;
		for (var i = 0; i < 400; i++)
		{
			var m = DeckBuilder.Mutate(deck, pool, NoData(), rng, features: features);
			if (m is null)
				continue;
			proposals++;
			if (m.CopiesOf("Lord") < deck.CopiesOf("Lord"))
				cutLord++;
		}

		Assume.That(proposals, Is.GreaterThan(50));

		// Nine distinct cards, so a blind cut hits the lord ~11% of the time.
		Assert.That(
			(double)cutLord / proposals,
			Is.GreaterThan(0.25),
			$"unsupported card cut in only {cutLord}/{proposals} proposals — no better than blind"
		);
	}

	[Test]
	public void AWinningCardIsNotCutJustBecauseItIsUnsupported()
	{
		// **The counterweight, and the reason this term RANKS instead of deleting.**
		// Satisfaction == 0 cannot tell a card that does nothing from a fine card carrying an
		// irrelevant rider — a 2/2 for 2 that would gain 1 life if a tribe member entered reads
		// exactly the same as a 7-mana spell with no targets in the deck. Without this test the
		// penalty could be raised until it deletes good cards and every other test would pass.
		var pool = TribalPool();
		var features = PoolFeatures.Build(pool);
		(string Name, int Count)[] spells =
		[
			("Lord", 4),
			.. Enumerable.Range(0, 8).Select(i => (Name: $"Filler{i}", Count: 4)),
		];
		var deck = Build(24, spells);

		// The lord is unsupported AND the best card in the format.
		var draft = new DraftTrainingData(
			100000,
			50000,
			pool.Select(c => new CardStat(c.Name, 40000, c.Name == "Lord" ? 30000 : 20000))
				.ToList(),
			[]
		);
		var values = new ConstructedValues(DraftTrainingData.Empty, draft);
		Assume.That(
			values.CardDelta("Lord"),
			Is.GreaterThan(10),
			"fixture must make it clearly good"
		);
		Assume.That(features.Satisfaction("Lord", deck), Is.Zero);

		var rng = new Random(11);
		int proposals = 0,
			cutLord = 0;
		for (var i = 0; i < 400; i++)
		{
			var m = DeckBuilder.Mutate(deck, pool, values, rng, features: features);
			if (m is null)
				continue;
			proposals++;
			if (m.CopiesOf("Lord") < deck.CopiesOf("Lord"))
				cutLord++;
		}

		Assume.That(proposals, Is.GreaterThan(50));
		Assert.That(
			(double)cutLord / proposals,
			Is.LessThan(0.11),
			$"a card winning by 10pp was cut in {cutLord}/{proposals} proposals — the support term "
				+ "is overriding the measured win rate instead of breaking ties"
		);
	}

	[Test]
	public void SeedingWithFeatures_BuildsBetterSupportedDecks()
	{
		// The other direction, and the one single-card hill climbing cannot travel: Fill scores
		// against the PARTIALLY built deck, so a tribe member already placed makes the next one
		// and the lord more attractive. Measured as an A/B on the same seeds, because the claim
		// is about the term and not about one lucky deck.
		var pool = TribalPool();
		var features = PoolFeatures.Build(pool);

		double MeanSupport(bool on)
		{
			var total = 0.0;
			for (var seed = 0; seed < 40; seed++)
			{
				var deck = DeckBuilder.Seed(
					"D",
					pool,
					NoData(),
					new Random(seed),
					features: on ? features : null
				);
				var support = features.Satisfaction("Lord", deck);
				total += deck.CopiesOf("Lord") > 0 && !double.IsNaN(support) ? support : 0;
			}
			return total / 40;
		}

		var withFeatures = MeanSupport(true);
		var without = MeanSupport(false);

		Assert.That(
			withFeatures,
			Is.GreaterThan(without),
			$"support {without:F2} -> {withFeatures:F2}: seeding is not clustering a concept, which "
				+ "is the whole reason the term exists"
		);
	}

	[Test]
	public void SeedConcept_JamsTheWholeArchetype_NotAPackageOfTwo()
	{
		// The operator the mode was missing. `Package` brings an anchor plus up to two partners;
		// a concept needs its critical mass at once, because every intermediate step toward it is
		// worse than the pile it came from and gets rejected by accept-if-better.
		var pool = TribalPool();
		var features = PoolFeatures.Build(pool);

		var deck = DeckBuilder.SeedConcept("Synergy-1", pool, NoData(), features, new Random(5));

		Assert.That(deck, Is.Not.Null);
		Assert.That(deck!.Validate(), Is.Null, deck.Validate());

		var tribe = deck
			.Spells.Where(kv => kv.Key.StartsWith("Orc") || kv.Key == "Lord")
			.Sum(kv => kv.Value);

		Assert.That(
			tribe,
			Is.GreaterThanOrEqualTo(16),
			$"only {tribe} on-concept cards — a concept seed that does not reach critical mass is "
				+ "just a differently-shaped pile"
		);
	}

	[Test]
	public void SeedConcept_BeatsOrdinarySeeding_AtBuildingTheArchetype()
	{
		// The A/B that says the operator, not the scoring term, is doing the work. Same pool,
		// same seeds, same features — only the seeding route differs.
		var pool = TribalPool();
		var features = PoolFeatures.Build(pool);

		double MeanTribe(bool concept)
		{
			var total = 0.0;
			for (var seed = 0; seed < 30; seed++)
			{
				var deck = concept
					? DeckBuilder.SeedConcept("C", pool, NoData(), features, new Random(seed))
					: DeckBuilder.Seed("D", pool, NoData(), new Random(seed), features: features);
				if (deck is null)
					continue;
				total += deck
					.Spells.Where(kv => kv.Key.StartsWith("Orc") || kv.Key == "Lord")
					.Sum(kv => kv.Value);
			}
			return total / 30;
		}

		var withConcept = MeanTribe(true);
		var ordinary = MeanTribe(false);

		Assert.That(
			withConcept,
			Is.GreaterThan(ordinary * 3),
			$"on-concept cards {ordinary:F1} -> {withConcept:F1}: committing to a concept must "
				+ "build a visibly different deck, not a slightly nudged one"
		);
	}

	[Test]
	public void SeedField_LabelsSlotsAndGivesEachSynergySlotItsOwnConcept()
	{
		var pool = TwoTribePool();
		var features = PoolFeatures.Build(pool);

		var field = DeckBuilder.SeedField(
			5,
			pool,
			NoData(),
			new Random(9),
			minDifference: 0.3,
			includeWildcard: true,
			features: features,
			conceptSlots: 2
		);

		Assert.That(field, Has.Count.EqualTo(5));
		Assert.Multiple(() =>
		{
			Assert.That(field[0].Name, Is.EqualTo("Synergy-1"));
			Assert.That(field[1].Name, Is.EqualTo("Synergy-2"));
			Assert.That(field[4].Name, Is.EqualTo("Wildcard"));
			Assert.That(
				field.Select(d => d.Name),
				Is.Unique,
				"every slot must be identifiable in the report"
			);
		});

		foreach (var deck in field)
			Assert.That(deck.Validate(), Is.Null, deck.Validate());
	}

	private static int Orcs(Decklist d) =>
		d.Spells.Where(kv => kv.Key.StartsWith("Orc")).Sum(kv => kv.Value);

	private static int Elves(Decklist d) =>
		d.Spells.Where(kv => kv.Key.StartsWith("Elf")).Sum(kv => kv.Value);

	/// <summary>
	/// A labelled slot has to mean what it says. These were named "Midrange-A" while drawing a
	/// curve target uniformly from 2.0-4.5, so a "midrange" slot could come out with an aggro or
	/// control curve and the report would still call it midrange — a label that lies is worse than
	/// no label, because it gets read as evidence.
	/// </summary>
	[Test]
	public void ProfiledSlots_ActuallyBuildToTheirCurveBand()
	{
		// A pool spanning the whole curve, so a band is a real constraint rather than the only
		// thing available.
		IReadOnlyList<Card> pool =
		[
			.. Enumerable.Range(0, 10).Select(i => Spell($"One{i}", 1)),
			.. Enumerable.Range(0, 10).Select(i => Spell($"Two{i}", 2)),
			.. Enumerable.Range(0, 10).Select(i => Spell($"Four{i}", 4)),
			.. Enumerable.Range(0, 10).Select(i => Spell($"Six{i}", 6)),
		];
		var index = Index(pool);

		double MeanCost(DeckBuilder.DeckProfile profile)
		{
			var total = 0.0;
			for (var seed = 0; seed < 25; seed++)
				total += DeckBuilder
					.Seed("D", pool, NoData(), new Random(seed), profile: profile)
					.AverageCost(index);
			return total / 25;
		}

		var aggro = MeanCost(DeckBuilder.DeckProfile.Aggro);
		var midrange = MeanCost(DeckBuilder.DeckProfile.Midrange);
		var control = MeanCost(DeckBuilder.DeckProfile.Control);

		Assert.Multiple(() =>
		{
			Assert.That(
				aggro,
				Is.LessThan(midrange),
				$"aggro {aggro:F2} is not cheaper than midrange {midrange:F2}"
			);
			Assert.That(
				midrange,
				Is.LessThan(control),
				$"midrange {midrange:F2} is not cheaper than control {control:F2}"
			);
		});

		// Land count follows the curve through LandsForCurve, so the profile has to move the mana
		// base too or "aggro" is only a spell mix.
		var aggroLands = DeckBuilder
			.Seed("A", pool, NoData(), new Random(3), profile: DeckBuilder.DeckProfile.Aggro)
			.Lands;
		var controlLands = DeckBuilder
			.Seed("C", pool, NoData(), new Random(3), profile: DeckBuilder.DeckProfile.Control)
			.Lands;
		Assert.That(
			aggroLands,
			Is.LessThan(controlLands),
			$"aggro ran {aggroLands} lands against control's {controlLands}"
		);
	}

	/// A deck committed to ONE tribe rather than a mixed creature pile.
	private static bool IsTribal(Decklist d) =>
		Math.Max(Orcs(d), Elves(d)) > 0
		&& Math.Max(Orcs(d), Elves(d)) >= 2 * Math.Min(Orcs(d), Elves(d));

	/// <summary>
	/// **REGRESSION, found by a real 25-generation A/B rather than by reasoning.**
	/// <c>PickConcept</c> weighted by supplier count, so it drew the BROADEST demand available.
	/// This pool's Anthem wants any creature at all (19 of 19 creatures); the equivalent on CSC
	/// is "creatures you control" at 237 of 408. A deck built on that is a creature pile, not an
	/// archetype, so three slots drawing such demands produced three similar decks — the field
	/// started at 59% diversity against a 69% control and collapsed to 48% in one generation.
	///
	/// **Measured as a RATE over many seeds, because this changes a probability distribution and
	/// one fixed seed cannot see it.** The first version of this test used a single seed and
	/// passed under BOTH weightings — a test that measured nothing, which is the trap this file
	/// keeps rediscovering.
	/// </summary>
	[Test]
	public void ConceptChoice_PrefersDistinctiveDemands_OverBroadOnes()
	{
		var pool = TwoTribePool();
		var features = PoolFeatures.Build(pool);

		var tribal = 0;
		const int Seeds = 60;
		for (var seed = 0; seed < Seeds; seed++)
		{
			var deck = DeckBuilder.SeedConcept("C", pool, NoData(), features, new Random(seed));
			if (deck is not null && IsTribal(deck))
				tribal++;
		}

		// Three viable demands: Orc (9 suppliers), Elf (9), any-creature (19). Weighted by breadth
		// the broad one takes 19/37 of draws; weighted by 1/n it takes 5/24.
		Assert.That(
			(double)tribal / Seeds,
			Is.GreaterThan(0.7),
			$"only {tribal}/{Seeds} concept seeds committed to a tribe — concept choice is still "
				+ "drawing the broad demand, which builds piles"
		);
	}
}
