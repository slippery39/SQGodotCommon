using MtgCore;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// **A core too small to fill a deck must not lock the slots it cannot reach.**
///
/// The pool lock admits only identity cards, which is right when the core can supply a whole deck
/// and fatal when it cannot: Splinter Twin's identity is FOUR cards, so 16 of its 45 spell slots
/// are reachable and the other 29 are whatever seeding put there, permanently. Measured on a
/// 21-deck DES run, the 2-, 3- and 4-card cores accepted nothing across six generations while
/// logging 5/5, 2/7 and 6/2 real/dry proposals — frozen rather than bad.
///
/// A frozen deck is worse than a drifting one: it is not searching. The core's own minimums still
/// carry the archetype, so what such a slot needs is what flex is for — find the support cards that
/// serve this engine.
/// </summary>
[TestFixture]
public class NarrowCoreFlexTests
{
	private static readonly CardSet Set = SetRegistry.Get("CMB");

	private static IReadOnlyList<Card> Spells =>
		[.. Set.Cards.Where(c => !c.HasSubtype("Land")).OrderBy(c => c.Name, StringComparer.Ordinal)];

	private static DeckCore CoreOf(int identityCards, int minCopies = 4) =>
		new(
			"core",
			[
				new CoreSlot(
					"Payoff",
					[.. Spells.Take(identityCards).Select(c => c.Name)],
					minCopies,
					IsIdentity: true
				),
			]
		);

	[Test]
	public void ACoreThatCannotFillADeck_DoesNotLockTheDeck()
	{
		// 40 spell slots against a 4-card identity: 16 reachable, 24 not.
		Assert.That(CoreOf(4).CanFillDeck(20), Is.False, "4 cards x 4 copies cannot reach 40 spells");

		// 12 cards x 4 = 48 >= 40, so this one genuinely can be built on-theme.
		Assert.That(CoreOf(12).CanFillDeck(20), Is.True);
	}

	/// <summary>
	/// The behavioural half: mutation of a narrow-core deck must be able to reach a card outside
	/// the identity, because that is where its 29 free slots live. Driven over many rolls because
	/// `Mutate` picks an operator at random and only some of them add cards.
	/// </summary>
	[Test]
	public void ANarrowCoreDeckCanMutateItsFlex()
	{
		var core = CoreOf(4);
		var identity = core.Identity.ToHashSet(StringComparer.Ordinal);

		// 20 lands, not 15: Decklist.MinLands defaults to 20, so a 15-land fixture is INVALID and
		// Mutate rejects every proposal — which reads exactly like the lock still being on.
		var deck = Decklist.Empty("Narrow") with { Lands = 20 };
		foreach (var n in identity)
			deck = deck.WithCopies(n, 4);
		foreach (var c in Spells.Where(c => !identity.Contains(c.Name)).Take(6))
			deck = deck.WithCopies(c.Name, 4);

		var values = new ConstructedValues(new CardStatAccumulator().ToData(), null);
		var rng = new Random(7);

		var proposals = 0;
		var reachedOutside = false;
		for (var i = 0; i < 200; i++)
		{
			var m = DeckBuilder.Mutate(deck, Set.Cards, values, rng, core: core);
			if (m is null)
				continue;
			proposals++;

			Assert.That(core.Holds(m), Is.True, "the core must hold whatever the flex does");
			if (m.Spells.Keys.Any(n => !identity.Contains(n) && m.CopiesOf(n) > deck.CopiesOf(n)))
				reachedOutside = true;
		}

		Assert.Multiple(() =>
		{
			Assert.That(proposals, Is.GreaterThan(0), "a narrow-core deck must get proposals at all");
			Assert.That(
				reachedOutside,
				Is.True,
				"its 29 unreachable slots are exactly what needs searching — locking them is what "
					+ "froze the 2-, 3- and 4-card cores for six generations"
			);
		});
	}

	/// <summary>
	/// **The control, and the reason the gate is a gate rather than a deletion.** A core wide
	/// enough to build a whole deck keeps the strict lock — that lock is measured to matter, a
	/// Dragonstorm deck went from 61% off-theme cards to 100% on-theme when it was introduced.
	/// </summary>
	[Test]
	public void AWideCoreIsStillLocked()
	{
		var core = CoreOf(12, minCopies: 4);
		var identity = core.Identity.ToHashSet(StringComparer.Ordinal);

		var deck = Decklist.Empty("Wide") with { Lands = 20 };
		foreach (var n in identity.Take(10))
			deck = deck.WithCopies(n, 4);

		var values = new ConstructedValues(new CardStatAccumulator().ToData(), null);
		var rng = new Random(11);

		for (var i = 0; i < 200; i++)
		{
			var m = DeckBuilder.Mutate(deck, Set.Cards, values, rng, core: core);
			if (m is null)
				continue;

			Assert.That(
				m.Spells.Keys,
				Is.SubsetOf(identity),
				"a core that can fill a deck must still admit nothing but its own cards"
			);
		}
	}
}
