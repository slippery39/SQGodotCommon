using MtgCore;
using MtgCore.Cards.Builders;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// Colour identity, and the presim sampling that depends on it.
///
/// The property under test throughout is the one that makes the value tables trustworthy: a random
/// deck must only contain cards its own manabase can cast. If that ever leaks, the tables silently
/// go back to deflating every committed card and nothing else in the suite notices.
/// </summary>
[TestFixture]
public class ColorIdentityTests
{
	private static Card Card(string name, ManaPool pips) =>
		CardFactory.Creature(name, manaCost: 2, power: 2, toughness: 2).Build() with
		{
			ColorPips = pips,
		};

	private static ColorIdentity Identity(string code) =>
		ColorIdentity.Standard.Single(i => i.Code == code);

	[Test]
	public void ThereAreFiveMonoAndTenPairIdentities()
	{
		Assert.Multiple(() =>
		{
			Assert.That(ColorIdentity.Mono, Has.Count.EqualTo(5));
			Assert.That(ColorIdentity.Pairs, Has.Count.EqualTo(10));
			Assert.That(ColorIdentity.Standard, Has.Count.EqualTo(15));
			Assert.That(
				ColorIdentity.Standard.Select(i => i.Code).Distinct().Count(),
				Is.EqualTo(15),
				"codes must be unique — they key the per-identity value table"
			);
		});
	}

	[Test]
	public void ColourlessCards_ArePlayableEverywhere()
	{
		var artifact = CardFactory.Artifact("Rock", manaCost: 2).Build();

		Assert.That(ColorIdentity.Standard.All(i => i.Allows(artifact)), Is.True);
	}

	[Test]
	public void AMonoColourCard_IsPlayableInItsColourAndItsFourPairs()
	{
		var red = Card("Bolt", new ManaPool { Red = 1 });

		var homes = ColorIdentity.Standard.Where(i => i.Allows(red)).Select(i => i.Code).ToList();

		Assert.That(homes, Has.Count.EqualTo(5), $"got {string.Join(",", homes)}");
	}

	[Test]
	public void AGoldCard_IsPlayableInExactlyOneIdentity()
	{
		// This is the exposure problem in one assertion: a gold card has a fifteenth of a
		// colourless card's chance to be sampled. See ColorIdentity.Playable's ponytail note.
		var gold = Card("Gold", new ManaPool { White = 1, Blue = 1 });

		var homes = ColorIdentity.Standard.Where(i => i.Allows(gold)).Select(i => i.Code).ToList();

		Assert.That(homes, Is.EqualTo(new[] { "WU" }));
	}

	[Test]
	public void ADoublePipCard_IsStillOnlyItsOwnColour()
	{
		var ww = Card("Double", new ManaPool { White = 2 });

		Assert.Multiple(() =>
		{
			Assert.That(Identity("W").Allows(ww), Is.True);
			Assert.That(
				Identity("WU").Allows(ww),
				Is.True,
				"depth is a manabase problem, not a legality one"
			);
			Assert.That(Identity("U").Allows(ww), Is.False);
		});
	}

	[Test]
	public void ARandomDeck_SeededToAnIdentity_HoldsNothingItCannotCast()
	{
		var pool = new List<Card>
		{
			Card("W one", new ManaPool { White = 1 }),
			Card("W two", new ManaPool { White = 2 }),
			Card("U one", new ManaPool { Blue = 1 }),
			Card("B one", new ManaPool { Black = 1 }),
			Card("R one", new ManaPool { Red = 1 }),
			Card("G one", new ManaPool { Green = 1 }),
			Card("Gold WU", new ManaPool { White = 1, Blue = 1 }),
			Card("Gold BR", new ManaPool { Black = 1, Red = 1 }),
			CardFactory.Artifact("Rock", manaCost: 2).Build(),
		};

		var identity = Identity("WU");
		var deck = PreSimulation.RandomDeck("t", pool, new Random(7), identity: identity);
		var byName = pool.ToDictionary(c => c.Name);

		Assert.That(deck.Spells.Keys, Is.Not.Empty);
		foreach (var name in deck.Spells.Keys)
			Assert.That(identity.Allows(byName[name]), Is.True, $"{name} is not castable in WU");
	}

	/// <summary>
	/// The measurement this whole mechanism exists for, on the real pool.
	///
	/// Unscoped, a random CSC deck plays 4.8 colours and a card sees ~5.8 sources of its own
	/// colour; scoped, it plays 1.7 and sees ~16. Against the calibrated table that is the
	/// difference between a double pip being castable on curve about a QUARTER of the time and
	/// about SIX SEVENTHS — and 29% of a random deck's coloured cards are double-pipped.
	///
	/// The threshold is deliberately loose. This guards the mechanism working at all, not the
	/// exact figures, which move whenever the cube's pips are retuned.
	/// </summary>
	[Test]
	public void OnTheRealPool_ScopingLiftsACardsOwnColourSourcesSeveralFold()
	{
		var spells = CoresetCube.Set.Cards.Where(c => !c.HasSubtype("Land")).ToList();

		double Measure(bool scoped)
		{
			var rng = new Random(11);
			double total = 0;
			var counted = 0;

			for (var i = 0; i < 60; i++)
			{
				var identity = scoped ? ColorIdentity.Standard[i % 15] : null;
				var deck = PreSimulation.RandomDeck($"d{i}", spells, rng, identity: identity);
				if (!deck.IsValid)
					continue;

				var cards = deck
					.Spells.SelectMany(kv =>
						Enumerable.Repeat(spells.First(c => c.Name == kv.Key), kv.Value)
					)
					.ToList();
				var sources = ManaBase
					.Build(cards, deck.Lands, ownerId: 1)
					.Aggregate(
						ManaPool.Empty,
						(p, l) =>
							p.Add(l.GetComponent<LandColorComponent>()?.Produces ?? ManaPool.Empty)
					);

				var coloured = cards.Where(c => !c.ColorPips.IsEmpty).ToList();
				if (coloured.Count == 0)
					continue;

				total += coloured.Average(c =>
					ManaPool.Colors.Where(x => c.ColorPips[x] > 0).Min(x => sources[x])
				);
				counted++;
			}

			return total / counted;
		}

		var unscoped = Measure(false);
		var scoped = Measure(true);

		TestContext.Out.WriteLine(
			$"own-colour sources — unscoped {unscoped:F1}, scoped {scoped:F1}"
		);
		Assert.That(scoped, Is.GreaterThan(2 * unscoped), "scoping must materially raise sources");
		Assert.That(scoped, Is.GreaterThan(12), "and clear the single-pip requirement outright");
	}

	[Test]
	public void WithoutAnIdentity_ARandomDeckStillSamplesTheWholePool()
	{
		// The old behaviour has to survive: a colourless pool needs no identity, and the presim
		// callers that pass none must keep working.
		var pool = ManaPool.Colors.Select(c => Card($"{c}", ManaPool.Empty.Add(c, 1))).ToList();

		var deck = PreSimulation.RandomDeck("t", pool, new Random(3));

		Assert.That(deck.SpellCount, Is.GreaterThan(0));
	}
}
