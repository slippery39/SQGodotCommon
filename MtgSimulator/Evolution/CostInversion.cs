using MtgCore;

namespace MtgSimulator;

/// <summary>
/// One card's mana arbitrage: what it costs to play, against the cost of the best thing it can put
/// onto the battlefield without paying for it.
/// </summary>
/// <param name="Paid">Mana actually spent to reach the cheat — see <see cref="CostInversion"/>.</param>
/// <param name="Cheated">The most expensive pool card the cheat's own targeting spec accepts.</param>
/// <param name="Gap"><c>Cheated - Paid</c>. The whole signal.</param>
/// <param name="Candidates">Pool cards the spec accepts at all.</param>
/// <param name="Big">How many of those cost 6 or more — the redundancy behind the headline gap.</param>
/// <param name="Via">Where the cheat lives: the spell itself, an activated ability, or a trigger.</param>
public sealed record CostInversionRow(
	string Card,
	int Paid,
	int Cheated,
	int Gap,
	int Candidates,
	int Big,
	string Via,
	string Demand
);

/// <summary>
/// **Which cards in this pool cheat on mana?**
///
/// A cheap card that puts an expensive one onto the battlefield is arbitrage, and arbitrage is the
/// fingerprint most broken combos leave. It is deliberately independent of the demand/supply model:
/// no lattice, no subsumption, no probe. It reads two numbers that are already on the cards.
///
/// **This exists because the demand model cannot express magnitude.** `Raise the Sunken` and
/// `Gravedigger` ask the identical question — <c>IsCreatureInOwnGraveyardSpecification</c> — so
/// `DeckCore` builds them the same core and `EngineDiscovery` dedupes them into one archetype named
/// after whichever sorts first alphabetically. One costs 1 and puts ANY creature onto the
/// battlefield; the other costs 4 and returns one to your hand, where you still have to pay for it.
/// Nothing in the demand model distinguishes them. The gap does, immediately.
///
/// **The discriminator is the ACTION TYPE, not the wording, and that is what makes this structural
/// rather than a mechanic-to-meaning table.** <c>PutIntoBattlefieldAction</c> is the single path a
/// card takes to the battlefield without being cast; <c>ReturnToHandAction</c> is not a cheat
/// because you still pay. Nothing here reads a card name, a subtype, or a zone by name.
///
/// Known scope, all deliberate for a first cut:
///
/// - **Only put-onto-the-battlefield cheats.** Cost reduction, alternative costs and "cast without
///   paying its mana cost" are the same idea through a different mechanism and are NOT read here.
/// - **X cards are skipped.** `ManaCost` is 0 for them because X lives on the cast action, so they
///   would every one read as a free spell. Same rule `ProbeCardProfiles` already applies: a cost
///   that cannot be known cannot be shown to be profitable.
/// - **A spec the harvest never indexed cannot be measured**, so its card is skipped rather than
///   guessed at. `IsObjectReferential` demands are the usual reason.
/// </summary>
public static class CostInversion
{
	/// Where a cheat can live. The mana you must spend to reach it differs per site, which is the
	/// only reason the distinction is kept.
	private const string ViaSpell = "spell";
	private const string ViaActivated = "activated";
	private const string ViaTriggered = "triggered";

	/// <summary>Cards whose cost is at or above this are what a cheat is FOR.</summary>
	public const int Big = 6;

	public static IReadOnlyList<CostInversionRow> Rank(
		IReadOnlyList<Card> pool,
		PoolFeatures features
	)
	{
		var costOf = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var c in pool)
			costOf[c.Name] = c.ManaCost;

		var rows = new List<CostInversionRow>();

		foreach (var card in pool)
		{
			// X lives on the cast action, never on the card, so every X spell reads manaCost 0.
			if (card.HasComponent<XCostComponent>())
				continue;

			foreach (var (paid, spec, via) in CheatSites(card))
			{
				var demand = IndexOf(features, spec);
				if (demand < 0)
					continue;

				var candidates = features.SuppliersOf(demand);
				if (candidates.Count == 0)
					continue;

				var costs = candidates
					.Where(costOf.ContainsKey)
					.Select(n => costOf[n])
					.DefaultIfEmpty(0)
					.ToList();

				// **The MAXIMUM, not the mean, and that is the honest statistic here.** You choose
				// what goes in your deck, so a cheat is worth what the biggest thing it can reach
				// is worth — the other 400 candidates are not a dilution of the effect, they are
				// choices you decline. `Big` carries the redundancy so a lone outlier is visible.
				var cheated = costs.Max();
				rows.Add(
					new CostInversionRow(
						card.Name,
						paid,
						cheated,
						cheated - paid,
						candidates.Count,
						costs.Count(c => c >= Big),
						via,
						features.Describe(demand)
					)
				);
			}
		}

		// One row per card: its best cheat. A card with two routes to the battlefield is one
		// build-around, not two.
		return
		[
			.. rows.GroupBy(r => r.Card, StringComparer.Ordinal)
				.Select(g => g.OrderByDescending(r => r.Gap).First())
				.OrderByDescending(r => r.Gap)
				.ThenBy(r => r.Card, StringComparer.Ordinal),
		];
	}

	/// <summary>
	/// Every place this card puts a card onto the battlefield, with the mana that route costs.
	///
	/// **An activated ability's cost is the card PLUS the activation.** Obsessive Stitcher is a
	/// 3-drop with a 4-mana reanimate ability, so its cheat costs 7 and it is genuinely a slower
	/// card than a 1-mana sorcery that does the same thing. A trigger costs only the card, because
	/// once it resolves the ability is free.
	/// </summary>
	private static IEnumerable<(int Paid, TargetSpecification Spec, string Via)> CheatSites(
		Card card
	)
	{
		foreach (var spell in card.GetComponents<SpellComponent>())
		foreach (var spec in SpecsPuttingIntoPlay(spell.Effects))
			yield return (card.ManaCost, spec, ViaSpell);

		foreach (var ability in card.GetComponents<ActivatedAbilityComponent>())
		foreach (var spec in SpecsPuttingIntoPlay(ability.Effects))
			yield return (card.ManaCost + ability.ManaCost, spec, ViaActivated);

		foreach (var trigger in card.GetComponents<TriggeredAbilityComponent>())
		foreach (var spec in SpecsPuttingIntoPlay(trigger.Effects))
			yield return (card.ManaCost, spec, ViaTriggered);
	}

	private static IEnumerable<TargetSpecification> SpecsPuttingIntoPlay(
		IEnumerable<CardEffect> effects
	) =>
		effects
			.Where(e => e.ActionTemplate is PutIntoBattlefieldAction)
			.Select(e => e.TargetingStrategy?.Specification)
			.OfType<TargetSpecification>();

	/// <summary>
	/// The harvest's index for this spec, or -1.
	///
	/// Specs are records and the harvest dedupes them by value, so equality is the right lookup and
	/// a spec that was never indexed genuinely has no measured supplier set.
	/// </summary>
	private static int IndexOf(PoolFeatures features, TargetSpecification spec)
	{
		for (var i = 0; i < features.Demands.Count; i++)
			if (Equals(features.Demands[i], spec))
				return i;
		return -1;
	}
}
