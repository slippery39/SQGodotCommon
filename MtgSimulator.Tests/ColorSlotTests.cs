using MtgCore;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// Colour identity as a field slot: one slot per identity, enforced at seeding AND at mutation.
///
/// Mutation is the half worth testing hardest. A slot that seeds mono-red and then mutates freely
/// drifts out of its colours one swap at a time and the field silently stops covering the format —
/// the same failure the curve bands exist to prevent, in a different dimension.
/// </summary>
[TestFixture]
public class ColorSlotTests
{
	private static readonly IReadOnlyList<Card> Pool = CoresetCube
		.Set.Cards.Where(c => !c.HasSubtype("Land"))
		.ToList();

	private static ConstructedValues Values() => new(DraftTrainingData.Empty, null);

	private static IReadOnlyList<Card> CardsOf(Decklist deck) =>
		[.. deck.Spells.Keys.Select(n => Pool.First(c => c.Name == n))];

	[Test]
	public void TheFirstFifteenSlots_CoverEveryIdentityExactlyOnce()
	{
		var assigned = Enumerable
			.Range(0, 16)
			.Select(i => DeckBuilder.IdentityForSlot(i, 16, wildcard: i == 15))
			.ToList();

		Assert.Multiple(() =>
		{
			Assert.That(
				assigned.Take(15).Select(i => i!.Code).OrderBy(c => c),
				Is.EqualTo(ColorIdentity.Standard.Select(i => i.Code).OrderBy(c => c))
			);
			Assert.That(assigned[15], Is.Null, "the wildcard slot is deliberately unconstrained");
		});
	}

	[TestCase("R")]
	[TestCase("W")]
	[TestCase("WU")]
	[TestCase("BG")]
	public void ASeededDeck_HoldsOnlyCardsItsIdentityCanCast(string code)
	{
		var identity = ColorIdentity.Standard.Single(i => i.Code == code);

		var deck = DeckBuilder.Seed(code, Pool, Values(), new Random(5), identity: identity);

		Assert.That(deck.Validate(), Is.Null);
		foreach (var card in CardsOf(deck))
			Assert.That(identity.Allows(card), Is.True, $"{card.Name} is not castable in {code}");
	}

	[Test]
	public void MutationCannotDragADeckOutOfItsColours()
	{
		// The pool lock re-applied at mutation. Without it a slot leaks a card at a time and
		// nothing reports it — the deck stays legal, it just stops being the archetype it claims.
		var identity = ColorIdentity.Standard.Single(i => i.Code == "R");
		var values = Values();
		var rng = new Random(9);
		var deck = DeckBuilder.Seed("R", Pool, values, rng, identity: identity);

		var mutations = 0;
		for (var i = 0; i < 60; i++)
		{
			var mutant = DeckBuilder.Mutate(deck, Pool, values, rng, identity: identity);
			if (mutant == null)
				continue;

			deck = mutant;
			mutations++;
			foreach (var card in CardsOf(deck))
				Assert.That(
					identity.Allows(card),
					Is.True,
					$"{card.Name} leaked into a mono-red deck after {mutations} mutations"
				);
		}

		Assert.That(mutations, Is.GreaterThan(5), "the test must actually have mutated something");
	}

	[Test]
	public void TheWildcardSlot_MayPlayAnyColours()
	{
		// It is the control: if an unconstrained deck loses to the constrained slots, colour is
		// doing real work. It must therefore actually be unconstrained.
		var deck = DeckBuilder.Seed("Wildcard", Pool, Values(), new Random(4), identity: null);

		Assert.That(deck.Validate(), Is.Null);
		Assert.That(
			ManaPool.Colors.Count(c => CardsOf(deck).Any(card => card.ColorPips[c] > 0)),
			Is.GreaterThan(2),
			"an unconstrained seed should reach past two colours"
		);
	}

	[Test]
	public void AScopedValuesView_ConditionsEveryLookup_NotJustTheOneCallerRemembered()
	{
		// DeckBuilder reads CardDelta from seven places. If scoping had been threaded per call
		// site instead, one missed site would let fill add a card that cut then removes, forever.
		var pooled = new CardStatAccumulator();
		for (var i = 0; i < 200; i++)
			pooled.Add(["Card"], ["Card"], i < 100);

		var identity = new CardStatAccumulator();
		for (var i = 0; i < 400; i++)
			identity.Add(["Card"], ["Card"], i < 360);

		var values = new ConstructedValues(
			pooled.ToData(),
			null,
			new IdentityValues(
				new Dictionary<string, DraftTrainingData> { ["W"] = identity.ToData() }
			)
		);

		var scoped = values.For(ColorIdentity.Standard.Single(i => i.Code == "W"));

		Assert.Multiple(() =>
		{
			Assert.That(scoped.Scope, Is.EqualTo("W"));
			Assert.That(scoped.CardDelta("Card"), Is.GreaterThan(values.CardDelta("Card")));
			Assert.That(
				scoped.CardDelta("Card"),
				Is.EqualTo(values.CardDelta("Card", "W")).Within(0.01),
				"the scoped view and the explicit call must agree"
			);
		});
	}

	[Test]
	public void ScopingToNothing_LeavesTheValuesUntouched()
	{
		var values = Values();

		Assert.That(values.For(null), Is.SameAs(values));
	}
}
