using System.Collections.Immutable;
using MtgCore;
using MtgCore.Cards.Builders;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// Whether a discovered archetype can actually be BUILT once colour is a constraint.
///
/// "Does this archetype assemble?" is what the engine report exists to ask. Colour adds a way for
/// the answer to be no that the report could not previously see: a payoff whose only enablers live
/// in three other colours posts a perfectly healthy lift and can never be played.
/// </summary>
[TestFixture]
public class DeckCoreColorTests
{
	private static Card Card(string name, ManaPool pips) =>
		CardFactory.Creature(name, manaCost: 2, power: 2, toughness: 2).Build() with
		{
			ColorPips = pips,
		};

	private static IReadOnlyDictionary<string, Card> Pool(params Card[] cards) =>
		cards.ToDictionary(c => c.Name, StringComparer.Ordinal);

	private static CoreSlot Slot(string role, int minCopies, params string[] cards) =>
		new(role, [.. cards], minCopies);

	[Test]
	public void ACoreWholeInOneColour_BuildsInThatColourAndItsPairs()
	{
		// Floors of 4, one playset each — an 8-copy slot holding ONE card is unfillable by
		// construction, which the two-playsets test below covers deliberately.
		var pool = Pool(
			Card("Payoff", new ManaPool { Red = 1 }),
			Card("Enabler", new ManaPool { Red = 1 })
		);
		var core = new DeckCore(
			"Mono",
			[Slot("Payoff", 4, "Payoff"), Slot("Enabler", 4, "Enabler")]
		);

		var identities = core.PlayableIdentities(pool).Select(i => i.Code).ToList();

		// Mono-red plus its four pairs.
		Assert.That(identities, Has.Count.EqualTo(5));
		Assert.That(identities, Does.Contain("R"));
	}

	[Test]
	public void ATwoColourCore_BuildsOnlyInThePairThatCoversIt()
	{
		var pool = Pool(
			Card("Payoff", new ManaPool { Red = 1 }),
			Card("Enabler", new ManaPool { White = 1 })
		);
		var core = new DeckCore(
			"Boros",
			[Slot("Payoff", 4, "Payoff"), Slot("Enabler", 4, "Enabler")]
		);

		Assert.That(
			core.PlayableIdentities(pool).Select(i => i.Code).ToList(),
			Is.EqualTo(new List<string> { "WR" })
		);
	}

	[Test]
	public void AThreeColourCore_IsUnbuildable()
	{
		// The finding this exists to surface. Nothing about the archetype is wrong except that no
		// legal manabase can cast it.
		var pool = Pool(
			Card("Payoff", new ManaPool { Red = 1 }),
			Card("EnablerA", new ManaPool { White = 1 }),
			Card("EnablerB", new ManaPool { Blue = 1 })
		);
		var core = new DeckCore(
			"Jeskai",
			[Slot("Payoff", 4, "Payoff"), Slot("A", 4, "EnablerA"), Slot("B", 4, "EnablerB")]
		);

		Assert.That(core.PlayableIdentities(pool), Is.Empty);
	}

	[Test]
	public void ASlotCostsTheCHEAPESTWayToFillIt_NotTheUnionOfItsMembers()
	{
		// A slot holds INTERCHANGEABLE cards by definition, so a Twin slot offering a red copier
		// and a blue one costs whichever the deck can cast. Taking the union would call almost
		// every core five-colour and unbuildable.
		var pool = Pool(
			Card("Payoff", new ManaPool { Red = 1 }),
			Card("RedCopier", new ManaPool { Red = 1 }),
			Card("BlueCopier", new ManaPool { Blue = 1 })
		);
		var core = new DeckCore(
			"Twin",
			[Slot("Payoff", 4, "Payoff"), Slot("Copier", 4, "RedCopier", "BlueCopier")]
		);

		Assert.That(core.PlayableIdentities(pool).Select(i => i.Code), Does.Contain("R"));
	}

	[Test]
	public void ASlotNeedingTwoPlaysetsNeedsTwoPlayableCards_NotOne()
	{
		// The floor is in COPIES and a card contributes at most MaxCopies, so an 8-copy slot with
		// one playable member cannot be filled however good that member is.
		var pool = Pool(
			Card("Payoff", new ManaPool { Red = 1 }),
			Card("RedEnabler", new ManaPool { Red = 1 }),
			Card("BlueEnabler", new ManaPool { Blue = 1 })
		);
		var core = new DeckCore(
			"Wide",
			[Slot("Payoff", 4, "Payoff"), Slot("Enablers", 8, "RedEnabler", "BlueEnabler")]
		);

		var mono = ColorIdentity.Standard.Single(i => i.Code == "R");
		var pair = ColorIdentity.Standard.Single(i => i.Code == "UR");

		Assert.Multiple(() =>
		{
			Assert.That(
				core.AssemblableIn(mono, pool),
				Is.False,
				"one playable member gives 4 of 8"
			);
			Assert.That(core.AssemblableIn(pair, pool), Is.True, "two members reach the floor");
		});
	}

	/// <summary>
	/// Null identities mean "not computed", empty means "computed, and unbuildable". Only the
	/// second may drop an engine.
	///
	/// This was a real bug for the few minutes it existed. Every engine in a report saved before
	/// the colour column deserialises with a null, so treating null as unbuildable emptied the
	/// entire engine field — and the run came back clean, having seeded nothing. Three existing
	/// tests caught it; this one states the rule directly.
	/// </summary>
	[Test]
	public void AnEngineReportWithoutTheColourColumn_IsNotTreatedAsUnbuildable()
	{
		EngineCandidate Candidate(IReadOnlyList<string>? identities) =>
			new(
				"Engine",
				new DeckCore("c", []),
				"c",
				"test",
				10,
				Decklist.Empty("c") with
				{
					Lands = Decklist.MinLands,
				},
				[],
				[],
				[],
				0.9,
				6.0,
				0.5,
				1.0,
				5.0,
				4.0,
				7.0,
				5,
				Identities: identities
			);

		Assert.Multiple(() =>
		{
			Assert.That(Candidate(null).IsBuildable, Is.True, "unknown is not a verdict");
			Assert.That(Candidate([]).IsBuildable, Is.False, "computed and empty IS a verdict");
			Assert.That(Candidate(["R"]).IsBuildable, Is.True);
		});
	}

	[Test]
	public void ColourlessCardsDoNotConstrainAnything()
	{
		var pool = Pool(
			CardFactory.Artifact("Rock", manaCost: 2).Build(),
			CardFactory.Artifact("Gear", manaCost: 3).Build()
		);
		var core = new DeckCore("Artifacts", [Slot("Rocks", 4, "Rock"), Slot("Gears", 4, "Gear")]);

		Assert.That(core.PlayableIdentities(pool), Has.Count.EqualTo(15));
	}

	[Test]
	public void ASlotWithNoFloorIsNeverTheReasonACoreIsRejected()
	{
		// TargetCopies-only slots are legal filler; they must not veto an identity.
		var pool = Pool(
			Card("Payoff", new ManaPool { Red = 1 }),
			Card("Filler", new ManaPool { Green = 1 })
		);
		var core = new DeckCore(
			"Loose",
			[Slot("Payoff", 4, "Payoff"), Slot("Filler", 0, "Filler")]
		);

		Assert.That(core.PlayableIdentities(pool), Is.Not.Empty);
	}
}
