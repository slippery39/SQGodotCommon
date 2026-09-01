using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// A `DeckCore` states what a deck must CONTAIN, so the search can answer "the best deck that
/// includes this" rather than "the best deck", which reliably answers "a pile of good cards".
///
/// **The predecessor was a 60%-of-spells pool quota and it failed in a way worth pinning
/// against.** A real run spent the remaining 40% on Steppe Lynx, Gravecrawler, Liliana of the
/// Veil and Zombie Horde Leader inside what was nominally a storm deck — legal throughout,
/// because a resemblance score has slack and slack gets spent. These tests assert the constraint
/// is a fact about the list, and — separately — that anything is still free to move.
/// </summary>
[TestFixture]
public class DeckCoreTests
{
	private static (IReadOnlyList<Card> Spells, ConstructedValues Values) Pool()
	{
		var set = SetRegistry.Get("LEG");
		return (
			set.Cards.Where(c => !c.HasSubtype("Land")).ToList(),
			new ConstructedValues(DraftTrainingData.Empty, null)
		);
	}

	private static Decklist Deck(int lands, params (string Name, int Copies)[] spells)
	{
		var deck = Decklist.Empty("t") with { Lands = lands };
		foreach (var (name, copies) in spells)
			deck = deck.WithCopies(name, copies);
		return deck;
	}

	private static CoreSlot Slot(string role, int min, params string[] cards) =>
		new(role, cards.ToImmutableHashSet(StringComparer.Ordinal), min);

	// ===== The constraint itself =====

	[Test]
	public void ASlotIsSatisfiedByANYMixOfItsCards()
	{
		// Redundancy is the point: Splinter Twin and Kiki-Jiki are one role, and four of either
		// or two of each satisfies it identically. A core that named specific cards would be a
		// frozen decklist, which is the thing that scored 24.4%.
		var core = new DeckCore("twin", [Slot("Twin effect", 4, "Splinter Twin", "Kiki-Jiki")]);

		Assert.Multiple(() =>
		{
			Assert.That(core.Holds(Deck(24, ("Splinter Twin", 4))), Is.True);
			Assert.That(core.Holds(Deck(24, ("Kiki-Jiki", 4))), Is.True);
			Assert.That(core.Holds(Deck(24, ("Splinter Twin", 2), ("Kiki-Jiki", 2))), Is.True);
			Assert.That(core.Holds(Deck(24, ("Splinter Twin", 3))), Is.False);
		});
	}

	[Test]
	public void EverySlotMustHold_AndMissingSaysWhichDidNot()
	{
		var core = new DeckCore(
			"storm",
			[Slot("Mana", 8, "Ritual", "Mox"), Slot("Payoff", 4, "Tendrils")]
		);
		var half = Deck(16, ("Ritual", 4), ("Mox", 4), ("Tendrils", 2));

		Assert.Multiple(() =>
		{
			Assert.That(core.Holds(half), Is.False, "a mana engine with no payoff is not the deck");
			Assert.That(core.Missing(half), Is.EqualTo(new[] { "Payoff 2/4" }));
		});
	}

	// ===== Protection: the enforcement, and its limits =====

	[Test]
	public void AtTheFloor_TheSlotsCardsAreProtected()
	{
		var core = new DeckCore("goblins", [Slot("Goblins", 8, "Lackey", "Piledriver", "Matron")]);
		var atFloor = Deck(24, ("Lackey", 4), ("Piledriver", 4), ("Filler", 4));

		Assert.That(
			core.ProtectedIn(atFloor),
			Is.EquivalentTo(new[] { "Lackey", "Piledriver" }),
			"Matron is in the slot but not in the deck, so there is nothing to protect"
		);
	}

	[Test]
	public void AboveTheFloor_NothingIsProtected()
	{
		// **The difference between a constraint and a freeze.** With slack, evolution may still
		// discover a storm deck wants fewer rituals and cut them one at a time — it just cannot
		// cut past the floor.
		var core = new DeckCore("goblins", [Slot("Goblins", 8, "Lackey", "Piledriver", "Matron")]);
		var surplus = Deck(24, ("Lackey", 4), ("Piledriver", 4), ("Matron", 4));

		Assert.Multiple(() =>
		{
			Assert.That(core.Slots[0].CountIn(surplus), Is.EqualTo(12), "12 against a floor of 8");
			Assert.That(core.ProtectedIn(surplus), Is.Empty);
		});
	}

	// ===== Through Mutate, which is what actually matters =====

	[Test]
	public void SixtyChainedMutations_NeverBreakTheCore()
	{
		var (spells, values) = Pool();
		var themed = spells.Take(6).Select(c => c.Name).ToArray();
		var core = new DeckCore("test", [Slot("Theme", 12, themed)]);

		var deck = core.Satisfy(Decklist.Empty("t") with { Lands = 24 }, values);
		deck = Fill(deck, spells);
		Assert.That(core.Holds(deck), Is.True, "the fixture must start satisfied");

		for (var gen = 0; gen < 60; gen++)
		{
			var next = DeckBuilder.Mutate(deck, spells, values, new Random(gen), core: core);
			if (next is null)
				continue;
			deck = next;
			Assert.That(
				core.Holds(deck),
				Is.True,
				$"generation {gen} broke the core: {string.Join(", ", core.Missing(deck))}"
			);
		}
	}

	[Test]
	public void TheSameMutationsDoChangeTheRestOfTheDeck()
	{
		// **The control.** Without it the test above passes on a mutator that proposes nothing —
		// the vacuous-test trap this project has paid for repeatedly. A core must constrain the
		// core and leave everything else free.
		var (spells, values) = Pool();
		var themed = spells.Take(6).Select(c => c.Name).ToArray();
		var core = new DeckCore("test", [Slot("Theme", 12, themed)]);

		var start = Fill(core.Satisfy(Decklist.Empty("t") with { Lands = 24 }, values), spells);
		var deck = start;
		for (var gen = 0; gen < 60; gen++)
			deck = DeckBuilder.Mutate(deck, spells, values, new Random(gen), core: core) ?? deck;

		Assert.That(
			Decklist.Difference(start, deck),
			Is.GreaterThan(0.1),
			"the non-core slots must actually move"
		);
	}

	[Test]
	public void WithoutACore_MutationWillHappilyCutTheThemeAway()
	{
		// The negative control for the constraint as a whole: the same 60 mutations, unconstrained,
		// must be able to drop below what the core would have demanded. If they cannot, the test
		// above proves nothing about the core.
		var (spells, values) = Pool();
		var themed = spells.Take(6).Select(c => c.Name).ToArray();
		var core = new DeckCore("test", [Slot("Theme", 12, themed)]);

		var deck = Fill(core.Satisfy(Decklist.Empty("t") with { Lands = 24 }, values), spells);
		var dropped = false;
		for (var gen = 0; gen < 60 && !dropped; gen++)
		{
			deck = DeckBuilder.Mutate(deck, spells, values, new Random(gen)) ?? deck;
			dropped = !core.Holds(deck);
		}

		Assert.That(dropped, Is.True, "unconstrained mutation must erode the theme");
	}

	private static Decklist Fill(Decklist deck, IReadOnlyList<Card> spells)
	{
		foreach (var card in spells)
		{
			var need = Decklist.DeckSize - deck.Lands - deck.SpellCount;
			if (need <= 0)
				break;
			if (deck.CopiesOf(card.Name) == 0)
				deck = deck.WithCopies(card.Name, Math.Min(Decklist.MaxCopies, need));
		}
		return deck;
	}
}
