using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// `EngineProbe.Read` walks a game's event log and answers "did the payoff go off with its support
/// already deployed". Fabricated logs rather than played games: a game reaching a specific
/// assembly is not reliably reproducible, and every one of these assertions is about the WALK.
///
/// **Each case here has a matching negative.** The trap this project has paid for three times is a
/// test that passes whether or not the thing under test works — an all-zeros reading looks
/// identical to a metric that never fires, so every "it counts" case is paired with a "and it does
/// not count this" case.
/// </summary>
[TestFixture]
public class EngineProbeTests
{
	private const int RitualA = 1;
	private const int RitualB = 2;
	private const int Cantrip = 3;
	private const int Payoff = 4;

	private static readonly Dictionary<int, string> Names =
		new()
		{
			[RitualA] = "Ritual",
			[RitualB] = "Ritual",
			[Cantrip] = "Cantrip",
			[Payoff] = "Tendrils",
		};

	private static EngineProbe Probe(string[]? payoffs = null, string[]? enablers = null) =>
		new(
			"spells cast this turn",
			(payoffs ?? ["Tendrils"]).ToImmutableHashSet(StringComparer.Ordinal),
			(enablers ?? ["Ritual", "Cantrip"]).ToImmutableHashSet(StringComparer.Ordinal)
		);

	private static GameEvent Resolved(int cardId) => new SpellResolvedEvent { CardId = cardId };

	[Test]
	public void ThreeEnablersThenThePayoff_ReadsDepthThree()
	{
		var reading = Probe()
			.Read(
				[
					new TurnStartedEvent { PlayerId = 1 },
					Resolved(RitualA),
					Resolved(RitualB),
					Resolved(Cantrip),
					Resolved(Payoff),
				],
				Names
			);

		Assert.Multiple(() =>
		{
			Assert.That(reading.Depth, Is.EqualTo(3), "three enablers were down when it resolved");
			Assert.That(reading.Payoffs, Is.EqualTo(1));
			Assert.That(reading.Assembled, Is.True);
			Assert.That(reading.Turn, Is.EqualTo(1));
		});
	}

	[Test]
	public void ThePayoffFirst_ReadsDepthZero_ButStillCountsAsCast()
	{
		var reading = Probe()
			.Read(
				[
					new TurnStartedEvent { PlayerId = 1 },
					Resolved(Payoff),
					Resolved(RitualA),
					Resolved(RitualB),
				],
				Names
			);

		// The distinction the goldfish could not make: the deck DID cast its payoff, into nothing.
		// A single number would report this identically to a deck that never found the card.
		Assert.Multiple(() =>
		{
			Assert.That(reading.Depth, Is.EqualTo(0));
			Assert.That(reading.Payoffs, Is.EqualTo(1));
			Assert.That(reading.Assembled, Is.False);
		});
	}

	[Test]
	public void APileWithNoPayoff_ReadsNothing()
	{
		var reading = Probe()
			.Read(
				[new TurnStartedEvent { PlayerId = 1 }, Resolved(RitualA), Resolved(Cantrip)],
				Names
			);

		Assert.Multiple(() =>
		{
			Assert.That(reading.Depth, Is.EqualTo(0));
			Assert.That(reading.Payoffs, Is.EqualTo(0));
		});
	}

	[Test]
	public void ADiscardedPayoff_DoesNotCountAsExecuted()
	{
		// The reason `IsExecution` cannot be the generic "any event naming this card" rule that
		// deployment uses. A storm deck pitching Tendrils to Faithless Looting must not score as
		// though it had won with it.
		var reading = Probe()
			.Read(
				[
					new TurnStartedEvent { PlayerId = 1 },
					Resolved(RitualA),
					Resolved(RitualB),
					Resolved(Cantrip),
					new CardDiscardedEvent { PlayerId = 1, CardId = Payoff },
					new CardEnteredGraveyardEvent { CardId = Payoff, OwnerId = 1 },
				],
				Names
			);

		Assert.That(reading.Payoffs, Is.EqualTo(0));
		Assert.That(reading.Depth, Is.EqualTo(0));
	}

	[Test]
	public void OneEnablerSeenThreeTimes_CountsOnce()
	{
		// Cast, resolve, then hit the graveyard — three events, one card. Counting events instead
		// of instances would triple every deck's depth and rank a slow deck above a fast one.
		var reading = Probe()
			.Read(
				[
					new TurnStartedEvent { PlayerId = 1 },
					new SpellCastEvent { CardId = RitualA, CastingPlayerId = 1 },
					Resolved(RitualA),
					new CardEnteredGraveyardEvent { CardId = RitualA, OwnerId = 1 },
					Resolved(Payoff),
				],
				Names
			);

		Assert.That(reading.Depth, Is.EqualTo(1));
	}

	[Test]
	public void ACardThatIsBothPayoffAndEnabler_DoesNotCountItself()
	{
		// A Goblin lord asks for Goblins and is one. The first copy must read depth 0; a second
		// copy landing after it reads depth 1, because four Chieftains do support each other.
		var lords = Probe(payoffs: ["Ritual"], enablers: ["Ritual"]);

		var first = lords.Read([new TurnStartedEvent { PlayerId = 1 }, Resolved(RitualA)], Names);
		var second = lords.Read(
			[new TurnStartedEvent { PlayerId = 1 }, Resolved(RitualA), Resolved(RitualB)],
			Names
		);

		Assert.Multiple(() =>
		{
			Assert.That(first.Depth, Is.EqualTo(0));
			Assert.That(second.Depth, Is.EqualTo(1));
		});
	}

	[Test]
	public void EnteringPlayCountsAsExecution_ForCreaturesAndForPermanents()
	{
		// A reanimated fatty and an affinity artifact never emit SpellResolvedEvent; they arrive.
		var creature = Probe()
			.Read(
				[
					new TurnStartedEvent { PlayerId = 1 },
					Resolved(RitualA),
					new CreatureEnteredBattlefieldEvent { CardId = Payoff, PlayerId = 1 },
				],
				Names
			);

		var permanent = Probe()
			.Read(
				[
					new TurnStartedEvent { PlayerId = 1 },
					Resolved(RitualA),
					new PermanentEnteredBattlefieldEvent { CardId = Payoff, PlayerId = 1 },
				],
				Names
			);

		Assert.Multiple(() =>
		{
			Assert.That(creature.Depth, Is.EqualTo(1));
			Assert.That(permanent.Depth, Is.EqualTo(1));
		});
	}

	[Test]
	public void TheBestAssemblyWins_NotTheLast()
	{
		// Two payoff resolutions, the good one first. A deck is judged on what it managed, not on
		// what it happened to do last before the game ended.
		var reading = Probe()
			.Read(
				[
					new TurnStartedEvent { PlayerId = 1 },
					Resolved(RitualA),
					Resolved(RitualB),
					Resolved(Cantrip),
					Resolved(Payoff),
					new TurnStartedEvent { PlayerId = 2 },
					new TurnStartedEvent { PlayerId = 1 },
					Resolved(Payoff),
				],
				Names
			);

		Assert.Multiple(() =>
		{
			Assert.That(reading.Depth, Is.EqualTo(3));
			Assert.That(reading.Payoffs, Is.EqualTo(2));
			Assert.That(reading.Turn, Is.EqualTo(1), "the turn of the BEST assembly");
		});
	}

	[Test]
	public void TurnsAreRounds_TwoTurnStartsPerTurn()
	{
		var reading = Probe()
			.Read(
				[
					new TurnStartedEvent { PlayerId = 1 },
					new TurnStartedEvent { PlayerId = 2 },
					new TurnStartedEvent { PlayerId = 1 },
					Resolved(RitualA),
					Resolved(Payoff),
				],
				Names
			);

		Assert.That(reading.Turn, Is.EqualTo(2));
	}

	/// <summary>
	/// **Both depth and turn ignore games that never assembled; the RATE is what reports those.**
	///
	/// Depth used to be medianed over every game, and this test asserted that (`1.0`, "median of
	/// 0,0,2,4"). The consequence was arithmetic: below 50% assembly the median falls on a zero, so
	/// `MedianDepth` — and therefore `Lift` — was pinned to exactly 0 for every such engine.
	/// Measured across 42 real engines, 22 of 22 under 50% read depth 0 and 0 of 20 above it did.
	///
	/// That silently made LIFT unable to score a COMBO deck, which assembles rarely by nature and
	/// is the thing mode 7 exists to find.
	/// </summary>
	[Test]
	public void SummariseIgnoresFailedGamesInBothDepthAndTurn()
	{
		var (depth, turn, rate) = EngineProbe.Summarise(
			[new(4, 3, 1), new(0, 0, 0), new(2, 5, 1), new(0, 0, 2)]
		);

		Assert.Multiple(() =>
		{
			Assert.That(rate, Is.EqualTo(0.5).Within(1e-9), "rate still counts every game");
			Assert.That(
				depth,
				Is.EqualTo(3.0).Within(1e-9),
				"median of the 4 and 2 that assembled — not of 0,0,2,4"
			);
			// Turn 0 from a game that never assembled would drag this to 2.5 and make a deck that
			// half-works look faster than one that always works.
			Assert.That(turn, Is.EqualTo(4.0).Within(1e-9), "median of the 3 and 5 that assembled");
		});
	}

	/// <summary>
	/// The paired negative: an engine that NEVER assembles must still report 0, not a median over
	/// an empty set. Without this the fix above would throw or return garbage on the commonest
	/// case in any real report.
	/// </summary>
	[Test]
	public void SummariseReportsZeroWhenNothingAssembled()
	{
		var (depth, turn, rate) = EngineProbe.Summarise([new(0, 0, 0), new(0, 0, 0)]);

		Assert.Multiple(() =>
		{
			Assert.That(rate, Is.EqualTo(0.0).Within(1e-9));
			Assert.That(depth, Is.EqualTo(0.0).Within(1e-9));
			Assert.That(turn, Is.EqualTo(0.0).Within(1e-9));
		});
	}
}
