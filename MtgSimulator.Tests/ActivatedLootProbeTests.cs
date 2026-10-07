using MtgCore;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgSimulator.Tests;

/// <summary>
/// **An activated loot fills a graveyard.** The card-profile probe activates a permanent's
/// abilities and reads what MOVED; an activated "discard, then draw" asks which card to discard,
/// and `ProcessAllActions` stops at that question. Unsettled, the discard never happened, so the
/// card read as moving nothing and was never a graveyard enabler. Measured on DES: settling it
/// raised the graveyard enablers from 32 to 34 (Chandra's Regulator among them) and Sun Titan
/// stopped reading as unbuildable.
///
/// Inline cards: the shape (an activated ability that asks) is what is under test.
/// </summary>
[TestFixture]
public class ActivatedLootProbeTests
{
	private static readonly Card Regulator = CardFactory
		.Artifact("Probe Regulator", manaCost: 2)
		.WithActivatedAbility(
			"Regulate",
			manaCost: 2,
			effect: eb => eb.WithDiscard(1).WithDraw(2).WithTarget(TargetingStrategy.Self())
		)
		.Build();

	private static readonly Card Reanimate = CardFactory
		.Sorcery("Probe Reanimate", manaCost: 1)
		.WithReanimate()
		.WithTarget(Single().CreatureInYourGraveyard())
		.Build();

	/// Creature-heavy like a real pool (DES is ~58% creatures): a mover that does not name its
	/// target is credited only when what it moves is LIKELY a creature, judged by pool density, so
	/// a pool of mostly non-creatures would hide the loot whether or not it settles.
	private static readonly Card[] Creatures =
	[
		.. Enumerable
			.Range(1, 4)
			.Select(i =>
				CardFactory
					.Creature($"Probe Creature {i}", manaCost: 3, power: 3, toughness: 3)
					.Build()
			),
	];

	[Test]
	public void AnActivatedLoot_CausesACreatureInTheGraveyard()
	{
		var features = PoolFeatures.Build([Regulator, Reanimate, .. Creatures]);

		var graveyard = features
			.DemandsOf(Reanimate.Name)
			.Where(d => features.Describe(d).Contains("Graveyard", StringComparison.Ordinal))
			.ToList();

		Assert.That(graveyard, Is.Not.Empty, "the reanimation demand itself");
		Assert.That(graveyard.SelectMany(features.CausalSuppliersOf), Does.Contain(Regulator.Name));
	}
}
