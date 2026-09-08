using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Does the production probe see a card that moves another card as a COST?**
///
/// `PoolFeatures` reads costs on the DEMAND side — `ProbeCostDemands` harvests
/// `DiscardAdditionalCost.Filter` and `SacrificeAdditionalCost.Filter`, which is how "discard a
/// land card" becomes a demand. The production side does not: `ProbeCardProfiles` resolves
/// `spell.Effects` for a non-permanent and puts a permanent onto the battlefield, and **no
/// `AdditionalCost` is ever paid**. So a card whose discard is a cost rather than an effect fills
/// a graveyard that nothing measures.
///
/// That is the asymmetry this file exists to size. It reports rather than asserting a threshold,
/// for the same reason `ArchetypeChallenge` does: a pass/fail bar would encode the answer to the
/// question being asked. Read the count — if a handful of cards are affected it is not worth
/// paying costs in the fixture; if every looter and sac outlet in the pool is on the list, those
/// are missing reanimator and aristocrats enablers.
/// </summary>
[TestFixture]
public class CostAsProductionTests
{
	/// <summary>
	/// A cost that MOVES a card out of a zone you control — the two that can fill a graveyard.
	/// Deliberately a closed list rather than "any `AdditionalCost`": paying life or removing a
	/// counter produces no card anywhere, so counting them would inflate the answer.
	/// </summary>
	private static bool MovesACard(AdditionalCost cost) =>
		cost is DiscardAdditionalCost or SacrificeAdditionalCost;

	private static IEnumerable<AdditionalCost> CostsOf(Card card)
	{
		foreach (var cost in card.AdditionalCastCosts)
			yield return cost;

		// Activated abilities carry their own, and a sacrifice outlet is usually written there
		// rather than on the cast.
		foreach (var ability in card.GetComponents<ActivatedAbilityComponent>())
		foreach (var cost in ability.AdditionalCosts)
			yield return cost;

		foreach (var flashback in card.GetComponents<FlashbackComponent>())
		foreach (var cost in flashback.AdditionalCosts)
			yield return cost;
	}

	[Test]
	[Explicit("Diagnostic — reports the size of a known gap, asserts nothing about it.")]
	public void HowManyCardsPayACostThatFillsAZone()
	{
		var spells = SetRegistry.Combined.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var features = PoolFeatures.Build(spells);

		// Every demand this card causes, across the whole pool. Zero means the movement pass never
		// saw it produce anything.
		bool HasCausalSupply(string name) =>
			Enumerable
				.Range(0, features.Demands.Count)
				.Any(d => features.CausalSupplyOf(d, name) > 0);

		var payers = spells.Where(c => CostsOf(c).Any(MovesACard)).ToList();
		var invisible = payers.Where(c => !HasCausalSupply(c.Name)).ToList();

		Console.WriteLine($"Pool: {spells.Count} spells, {features.Demands.Count} demands");
		Console.WriteLine(
			$"Cards paying a card-moving cost: {payers.Count}"
				+ $"  ({payers.Count(c => c.AdditionalCastCosts.Any(MovesACard))} on the cast,"
				+ $" {payers.Count - payers.Count(c => c.AdditionalCastCosts.Any(MovesACard))} on an ability)"
		);
		Console.WriteLine(
			$"  ...of which the movement pass credits with NOTHING: {invisible.Count}"
		);
		Console.WriteLine();

		foreach (var c in invisible.OrderBy(c => c.Name, StringComparer.Ordinal))
			Console.WriteLine(
				$"  {c.Name, -38} {string.Join(", ", CostsOf(c).Where(MovesACard).Select(x => x.GetType().Name))}"
			);

		// **The gap is wider than costs, and this is the number that says so.** `ProbeCardProfiles`
		// puts a permanent onto the battlefield and starts a turn — which fires upkeep triggers,
		// and is why mana dorks are found — but it never ACTIVATES anything. So for a sacrifice or
		// discard outlet written as an activated ability, both the cost and the effect are
		// unmeasured, and paying costs alone would not reach it.
		var activated = spells
			.Where(c => c.GetComponents<ActivatedAbilityComponent>().Any())
			.ToList();
		Console.WriteLine();
		Console.WriteLine(
			$"Cards with an activated ability: {activated.Count}"
				+ $"  ...with causal supply: {activated.Count(c => HasCausalSupply(c.Name))}"
		);

		// The control: if NO card in the pool has causal supply at all, the numbers above are
		// measuring a broken probe rather than a cost gap.
		var anyCausal = spells.Count(c => HasCausalSupply(c.Name));
		Console.WriteLine();
		Console.WriteLine($"CONTROL — cards with causal supply from any channel: {anyCausal}");
		Assert.That(
			anyCausal,
			Is.GreaterThan(0),
			"no card in the pool causes any demand — the movement pass is broken, and the "
				+ "count above says nothing about costs"
		);
	}
}
