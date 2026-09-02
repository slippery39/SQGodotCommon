using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Some triggers cannot fire alone, and the single-card probe reports them as demanding nothing
/// anyone supplies.**
///
/// CMB's drain pair is the worked example and the reason the chained pass exists. Sanguine
/// Reciprocity fires on an OPPONENT losing life; the only card in the set that makes that happen is
/// Covenant of Thorns, which itself only fires once you gain life. `ProbeTriggers` plays each card
/// solo, so nothing reaches the second step — Reciprocity's demand had no suppliers, was not
/// `Informative`, and the card was in no core's payoff or support slot. It could not be selected by
/// anything.
///
/// The pool here is the drain package plus a vanilla, not the whole set: these three cards are
/// chosen for the SHAPE of the chain, none of which is a balance number, so a costing pass cannot
/// break these tests.
/// </summary>
[TestFixture]
public class ChainedTriggerProbeTests
{
	private const string Producer = "Covenant of Thorns";
	private const string Consumer = "Sanguine Reciprocity";
	private const string Igniter = "Almsgiver Acolyte";

	private static Card Cmb(string name) =>
		ComboProving.Set.Cards.Single(c => string.Equals(c.Name, name, StringComparison.Ordinal));

	private static PoolFeatures Pool(params string[] names) =>
		PoolFeatures.Build([.. names.Select(Cmb)]);

	/// The demand a card asks that any other pool card supplies, or -1.
	private static int SuppliedDemandOf(PoolFeatures features, string card) =>
		features.DemandsOf(card).FirstOrDefault(d => features.SuppliersInPool(d) > 0, -1);

	[Test]
	[Explicit("Diagnostic — what the chained fixture actually emits.")]
	public void DumpWhatTheChainEmits()
	{
		var (fixture, ids) = MtgGameFactory.CreateForTesting();
		var events = new List<GameEvent>();
		var state = fixture;

		foreach (var name in new[] { Producer, Consumer, Igniter })
		{
			var (next, fired) = state
				.AddActions(
					[
						new PutIntoBattlefieldAction
						{
							CardTemplate = Cmb(name) with
							{
								OwnerId = ids.Player1Id,
								ControllerId = ids.Player1Id,
							},
						},
					]
				)
				.ProcessAllActions();
			state = next;
			events.AddRange(fired);
			TestContext.Out.WriteLine($"after {name}:");
			foreach (var e in fired)
				TestContext.Out.WriteLine($"    {e.GetType().Name}  {e}");
		}

		TestContext.Out.WriteLine(
			$"  P1 life {((MtgPlayer)state.GetObject(ids.Player1Id)).Life}, "
				+ $"P2 life {((MtgPlayer)state.GetObject(ids.Player2Id)).Life}"
		);

		var features = Pool(Producer, Consumer, Igniter);
		foreach (var d in Enumerable.Range(0, features.Demands.Count))
			TestContext.Out.WriteLine(
				$"  [{d}] {features.Describe(d)}  suppliers=[{string.Join(", ", features.SuppliersOf(d))}]  "
					+ $"askers=[{string.Join(", ", features.AskersOf(d))}]"
			);
		foreach (var f in features.Failures)
			TestContext.Out.WriteLine($"  FAILURE {f}");
	}

	/// <summary>
	/// The headline. Reciprocity consumes what Covenant produces, and no card filter relates the
	/// two — this edge exists only because the chained probe played them together.
	/// </summary>
	[Test]
	public void ATriggerFedByAnotherCardsEvent_FindsThatCardAsItsSupplier()
	{
		var features = Pool(Producer, Consumer, Igniter);

		var demand = SuppliedDemandOf(features, Consumer);
		Assert.That(
			demand,
			Is.GreaterThanOrEqualTo(0),
			$"{Consumer} still supplies-nothing demands"
		);

		Assert.Multiple(() =>
		{
			Assert.That(features.SupplyOf(demand, Producer), Is.GreaterThan(0));
			Assert.That(features.Informative(demand), Is.True);
		});
	}

	/// <summary>
	/// **The vacuity guard, and it is the one that matters here.** The failure mode this pass had
	/// to avoid is crediting EVERYTHING — stocking the fixture so opponent life can drop would make
	/// every card in the pool a supplier, which is why attribution is a diff against the igniter
	/// alone. Remove the producer and the demand must go back to having no suppliers; a pass that
	/// credited the igniter, or the subject's own presence, would still find one.
	/// </summary>
	[Test]
	public void WithoutTheProducingCard_TheDemandStillHasNoSuppliers()
	{
		var features = Pool(Consumer, Igniter);

		foreach (var d in features.DemandsOf(Consumer))
			Assert.That(
				features.SuppliersOf(d),
				Is.Empty,
				$"'{features.Describe(d)}' gained a supplier with nothing in the pool that produces it"
			);
	}

	/// <summary>
	/// The chain is directional, and the pass must not invent the reverse edge. Covenant asks for
	/// life gain and Reciprocity gains life, so this one is real — but it has to come from the
	/// Acolyte too, not only from its combo partner, or the pass is keying on the pair rather than
	/// on the event.
	/// </summary>
	[Test]
	public void TheOrdinaryHalfOfThePairIsStillSuppliedByAPlainLifegainCard()
	{
		var features = Pool(Producer, Consumer, Igniter);

		var demand = SuppliedDemandOf(features, Producer);
		Assert.That(demand, Is.GreaterThanOrEqualTo(0));
		Assert.That(features.SupplyOf(demand, Igniter), Is.GreaterThan(0));
	}
}
