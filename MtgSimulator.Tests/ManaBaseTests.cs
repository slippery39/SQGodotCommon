using MtgCore;
using MtgCore.Cards.Builders;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// The manabase builder every deck path now shares. Its job is narrow — turn a pile of spells
/// into the right basics — but it is the difference between a drafted deck working and being
/// uncastable, and it is the only place that judgement lives.
/// </summary>
[TestFixture]
public class ManaBaseTests
{
	private static Card Spell(string name, ManaPool pips, int cost = 2) =>
		CardFactory.Creature(name, manaCost: cost, power: 2, toughness: 2).Build() with
		{
			ColorPips = pips,
		};

	private static Dictionary<string, int> Count(IEnumerable<Card> lands) =>
		lands.GroupBy(l => l.Name).ToDictionary(g => g.Key, g => g.Count());

	[Test]
	public void MonoColourDeck_GetsOnlyThatColour()
	{
		var spells = Enumerable
			.Range(0, 10)
			.Select(i => Spell($"Red {i}", new ManaPool { Red = 1 }))
			.ToList();

		var counts = Count(ManaBase.Build(spells, 17, ownerId: 1));

		Assert.That(counts, Has.Count.EqualTo(1));
		Assert.That(counts["Mountain"], Is.EqualTo(17));
	}

	[Test]
	public void TwoColourDeck_SplitsByDemand_NotByCardCount()
	{
		// Four white cards against two blue, but the white side is double-pipped, so it demands
		// far more than the 2:1 its card COUNT suggests.
		var spells = new List<Card>
		{
			Spell("WW a", new ManaPool { White = 2 }),
			Spell("WW b", new ManaPool { White = 2 }),
			Spell("W c", new ManaPool { White = 1 }),
			Spell("W d", new ManaPool { White = 1 }),
			Spell("U e", new ManaPool { Blue = 1 }),
			Spell("U f", new ManaPool { Blue = 1 }),
		};

		var counts = Count(ManaBase.Build(spells, 16, ownerId: 1));

		Assert.That(counts["Plains"], Is.GreaterThan(2 * counts["Island"]), "white demands more");
		Assert.That(counts["Plains"] + counts["Island"], Is.EqualTo(16));
	}

	[Test]
	public void ADoublePip_DemandsMoreSourcesThanTwoSinglePipCards()
	{
		// The measured table's central fact, read card-for-card: one WW card needs 18 sources of
		// White where one single-pip card needs 12 of its colour. If this ever reads equal, the
		// demand table has been flattened back into a plain pip count.
		//
		// Deliberately one card against one. Two single-pip cards out-demand one double-pip card
		// (12 + 12 against 18) and that is correct — demand sums over the deck.
		var spells = new List<Card>
		{
			Spell("WW", new ManaPool { White = 2 }),
			Spell("U", new ManaPool { Blue = 1 }),
		};

		var counts = Count(ManaBase.Build(spells, 24, ownerId: 1));

		Assert.That(counts["Plains"], Is.GreaterThan(counts["Island"]));
	}

	[Test]
	public void AnEarlyCard_DemandsMoreSourcesThanALateOne()
	{
		// A turn-two pip needs 12 sources, a turn-six pip needs 9. Cheap cards pull the manabase.
		var spells = new List<Card>
		{
			Spell("early", new ManaPool { Red = 1 }, cost: 1),
			Spell("late", new ManaPool { Green = 1 }, cost: 6),
		};

		var counts = Count(ManaBase.Build(spells, 24, ownerId: 1));

		Assert.That(counts["Mountain"], Is.GreaterThan(counts["Forest"]));
	}

	[Test]
	public void ACheapDoublePipCard_IsTreatedAsTheTurnItCanActuallyBeCast()
	{
		// A one-mana WW card cannot be cast on turn one: two White pips need two lands. Its demand
		// must be read at turn TWO, or the table is asked for a turn-one number that does not exist.
		var spells = new List<Card> { Spell("WW one-drop", new ManaPool { White = 2 }, cost: 1) };

		Assert.That(ManaBase.Build(spells, 10, ownerId: 1), Has.Count.EqualTo(10));
	}

	[Test]
	public void ASingleSplashedCard_StillGetsASource()
	{
		// One green card among nineteen red pips rounds to zero under pure proportion, and a
		// card you can never cast is worse than one fewer Mountain.
		var spells = Enumerable
			.Range(0, 19)
			.Select(i => Spell($"Red {i}", new ManaPool { Red = 1 }))
			.Append(Spell("Splash", new ManaPool { Green = 1 }))
			.ToList();

		var counts = Count(ManaBase.Build(spells, 17, ownerId: 1));

		Assert.That(counts.GetValueOrDefault("Forest"), Is.GreaterThanOrEqualTo(1));
	}

	[Test]
	public void AColourlessDeck_StillGetsLands()
	{
		// The preconstructed decks are still colourless; they must keep working untouched.
		var spells = Enumerable
			.Range(0, 10)
			.Select(i => Spell($"Artifact {i}", ManaPool.Empty))
			.ToList();

		var lands = ManaBase.Build(spells, 13, ownerId: 1);

		Assert.That(lands, Has.Count.EqualTo(13));
	}

	[TestCase(0)]
	[TestCase(1)]
	[TestCase(3)]
	[TestCase(17)]
	[TestCase(24)]
	public void TheDeckAlwaysGetsExactlyTheLandsItAskedFor(int landCount)
	{
		// Every colour present, so rounding has the most room to lose or invent a land.
		var spells = ManaPool
			.Colors.Select(c => Spell($"{c} card", ManaPool.Empty.Add(c, 1)))
			.ToList();

		Assert.That(ManaBase.Build(spells, landCount, ownerId: 1), Has.Count.EqualTo(landCount));
	}

	[Test]
	public void LandsAreStampedWithTheirOwner()
	{
		var spells = new[] { Spell("Black", new ManaPool { Black = 1 }) };

		var land = ManaBase.Build(spells, 1, ownerId: 42).Single();

		Assert.Multiple(() =>
		{
			Assert.That(land.OwnerId, Is.EqualTo(42));
			Assert.That(land.ControllerId, Is.EqualTo(42));
			Assert.That(land.GetComponent<LandColorComponent>()!.Produces.Black, Is.EqualTo(1));
		});
	}

	/// <summary>
	/// The claim the whole colour-feasibility layer rests on: a requirement is the MAX over the
	/// deck's cards, not the sum. Both these decks are entirely legal in UW by
	/// <see cref="ColorIdentity.Allows"/> — only depth separates them.
	/// </summary>
	[Test]
	public void DoublePips_InTwoColours_DoNotFit_ButSinglePipsDo()
	{
		var deep = new[]
		{
			Spell("WW", new ManaPool { White = 2 }),
			Spell("UU", new ManaPool { Blue = 2 }),
		};
		var shallow = new[]
		{
			Spell("W", new ManaPool { White = 1 }),
			Spell("U", new ManaPool { Blue = 1 }),
		};

		Assert.Multiple(() =>
		{
			Assert.That(ManaBase.Requirements(deep).Total, Is.EqualTo(36), "18 + 18");
			Assert.That(ManaBase.Shortfall(deep), Is.EqualTo(12));
			Assert.That(ManaBase.Requirements(shallow).Total, Is.EqualTo(24), "12 + 12");
			Assert.That(ManaBase.Shortfall(shallow), Is.Zero);
		});
	}

	/// A second copy of the same demand adds nothing; a deeper one raises the bar.
	[Test]
	public void Requirements_TakeTheMax_NotTheSum()
	{
		var one = new[] { Spell("W a", new ManaPool { White = 1 }) };
		var three = new[]
		{
			Spell("W a", new ManaPool { White = 1 }),
			Spell("W b", new ManaPool { White = 1 }),
			Spell("W c", new ManaPool { White = 1 }, cost: 5),
		};
		var deeper = three.Append(Spell("WW d", new ManaPool { White = 2 })).ToList();

		Assert.Multiple(() =>
		{
			Assert.That(ManaBase.Requirements(one).White, Is.EqualTo(12));
			Assert.That(
				ManaBase.Requirements(three).White,
				Is.EqualTo(12),
				"still one card's worth"
			);
			Assert.That(ManaBase.Requirements(deeper).White, Is.EqualTo(18), "the WW sets the bar");
		});
	}

	/// Colourless decks ask nothing, so they always fit — the artifact deck must not read broken.
	[Test]
	public void ColourlessDeck_HasNoRequirement()
	{
		var spells = new[] { Spell("Artifact", ManaPool.Empty, cost: 4) };

		Assert.That(ManaBase.Shortfall(spells), Is.Zero);
		Assert.That(ManaBase.Requirements(spells).IsEmpty, Is.True);
	}

	/// <summary>
	/// The splash is UNDER-SERVED, and this test pins that rather than wishing it away. Twenty white
	/// cards and one red one: the red card needs ten sources by the table and gets about two, because
	/// allocation is proportional to summed demand. See the comment on ManaBase.Allocate for why the
	/// fix does not belong there — a deck whose colours do not fit has no good allocation, so the
	/// splash must be priced by whoever builds the deck.
	///
	/// If this test starts failing because the splash gets MORE, that is the threshold allocator
	/// coming back; make sure a head-to-head justified it before updating the number.
	/// </summary>
	[Test]
	public void ASplashedCard_IsUnderServed_AndThatIsADeckbuildingProblem()
	{
		var spells = Enumerable
			.Range(0, 20)
			.Select(i => Spell($"W {i}", new ManaPool { White = 1 }))
			.Append(Spell("R splash", new ManaPool { Red = 1 }, cost: 4))
			.ToList();

		var counts = Count(ManaBase.Build(spells, 24, ownerId: 1));

		Assert.Multiple(() =>
		{
			Assert.That(ManaBase.Requirements(spells).Red, Is.EqualTo(10), "what it needs");
			Assert.That(counts["Mountain"], Is.LessThan(5), "what it gets");
			Assert.That(counts["Plains"] + counts["Mountain"], Is.EqualTo(24));
		});
	}

	/// <summary>
	/// When the colours do not fit, no split rescues the deck — Shortfall says so — and the lands
	/// are still split by demand. WW plus UU is 36 against 24, and the deck is the problem.
	/// </summary>
	[Test]
	public void UnmeetableColours_StillSplitByDemand()
	{
		var spells = new List<Card>
		{
			Spell("WW a", new ManaPool { White = 2 }),
			Spell("WW b", new ManaPool { White = 2 }),
			Spell("WW c", new ManaPool { White = 2 }),
			Spell("UU d", new ManaPool { Blue = 2 }),
		};

		var counts = Count(ManaBase.Build(spells, 24, ownerId: 1));

		Assert.Multiple(() =>
		{
			Assert.That(ManaBase.Shortfall(spells), Is.EqualTo(12), "not buildable at all");
			Assert.That(counts["Plains"], Is.GreaterThan(2 * counts["Island"]), "3:1 by demand");
			Assert.That(counts["Plains"] + counts["Island"], Is.EqualTo(24));
		});
	}
}
