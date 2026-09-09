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
	private static Card Spell(string name, ManaPool pips) =>
		CardFactory.Creature(name, manaCost: 2, power: 2, toughness: 2).Build() with
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
	public void TwoColourDeck_SplitsByPipDemand_NotCardCount()
	{
		// Six white pips against two blue: a 3:1 demand. Card COUNT is 4:2, which would give a
		// different and wrong answer — pips are what a land actually has to pay.
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

		Assert.Multiple(() =>
		{
			Assert.That(counts["Plains"], Is.EqualTo(12));
			Assert.That(counts["Island"], Is.EqualTo(4));
		});
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
}
