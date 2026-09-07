using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// **Does the payoff plus its support assemble an unbounded engine?**
///
/// The question leverage structurally cannot answer: `PlayGreedyTurn` takes one action per
/// simulated turn, so an infinite loop prices as one activation a turn. Splinter Twin measured
/// `bare 9.00, leverage 0.00` and ranked 31st of 44, while the hand-built Twin deck beats the
/// evolved field. Raising the rollout budget was measured and rejected — the fixture decides itself
/// and the whole report goes unmeasured.
/// </summary>
[TestFixture]
public class ComboProbeTests
{
	private static Card Card(string name) => ComboProving.Cards.Single(c => c.Name == name);

	/// The copier is the payoff; the Illusionist untapper is the support it cannot loop without.
	[Test]
	public void TheTwinPairAssemblesALoop()
	{
		var found = ComboProbe.WithSupport(Card("Twinflame Artisan"), [Card("Mirevale Deceiver")]);

		Assert.That(found, Is.Not.Null, "the shipped two-card untap/copy loop was not assembled");
		TestContext.Out.WriteLine(
			$"x{found!.Iterations}  gain {found.Gain}\n  {string.Join(" -> ", found.Line)}"
		);
	}

	/// <summary>
	/// **The loop must NEED the support**, or "assembles a loop" is a claim about one card.
	///
	/// Both halves of this are load-bearing. Without support there is nothing to assemble; with
	/// support that does not complete the combo, the copier still cannot go off — and a rule that
	/// simply returned "yes" whenever a copier was present would pass the test above on its own.
	/// </summary>
	[Test]
	public void TheCopierAloneDoesNot_AndNeitherDoesItWithIrrelevantSupport()
	{
		var vanilla = CardFactory.Creature("Bear", manaCost: 2, power: 2, toughness: 2).Build();

		Assert.Multiple(() =>
		{
			Assert.That(
				ComboProbe.WithSupport(Card("Twinflame Artisan"), []),
				Is.Null,
				"no support means nothing to assemble"
			);
			Assert.That(
				ComboProbe.WithSupport(Card("Twinflame Artisan"), [vanilla]),
				Is.Null,
				"a copier with no Illusionist to copy has no loop, so support must be the RIGHT support"
			);
		});
	}

	/// <summary>
	/// **A card that loops on its own is a balance bug, not an archetype.** Crediting it here would
	/// rank a broken card as the pool's best engine, and `NoSingleCardInThePoolLoops` is the sweep
	/// that exists to find such cards instead.
	/// </summary>
	[Test]
	public void ASelfLoopingPayoffIsRejected()
	{
		var engine = CardFactory
			.Creature("Loop Engine", manaCost: 0, power: 1, toughness: 1)
			.WithActivatedAbility(
				"Free Life",
				manaCost: 0,
				effect: e => e.WithLifeGain(1),
				requiresTap: false,
				maxPerTurn: 99
			)
			.Build();

		var vanilla = CardFactory.Creature("Bear", manaCost: 2, power: 2, toughness: 2).Build();

		Assert.That(
			ComboProbe.WithSupport(engine, [vanilla]),
			Is.Null,
			"it loops without the support, so the support is not what makes it an engine"
		);
	}

	/// <summary>
	/// The ranking consequence, asserted directly: a loop outranks a card that beats it on every
	/// measured column. Without this the probe could be correct and change nothing.
	/// </summary>
	[Test]
	public void ALoopOutranksABetterMeasuredCard()
	{
		var core = new DeckCore("x", [new CoreSlot("Payoff", ["x"], 4, IsIdentity: true)]);
		var empty = new Decklist("d", System.Collections.Immutable.ImmutableSortedDictionary<string, int>.Empty, 20);

		EngineCandidate Candidate(string name, float bare, float supplied, bool loops) =>
			new(name, core, name, "test", 0, empty, [], [], [], 0, 0, 0, 0, 0, 0, 0, 0, bare, supplied, true, loops);

		// The non-looper wins bare (0 is the blank signature) and wins leverage by a mile.
		var looper = Candidate("Twin", bare: 9f, supplied: 9f, loops: true);
		var measured = Candidate("Zombie Apocalypse", bare: 0f, supplied: 51f, loops: false);

		Assert.That(
			looper.BlankFirstKey.CompareTo(measured.BlankFirstKey),
			Is.LessThan(0),
			"an assembled loop is the strongest 'blank until assembled' there is, and must sort "
				+ "ahead of a card that merely measures well"
		);
	}
}
