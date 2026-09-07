using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// **A card that cannot be CAST without its support is bare zero, not unmeasurable.**
///
/// The bare arm of `MeasureLeverage` plays the card into an empty fixture. A reanimation spell
/// targets a creature in your graveyard, so with an empty graveyard it has no legal target and the
/// arm fails — and marking the whole measurement unmeasured discarded a perfectly good SUPPLIED
/// number, dropping the card into tier 1 of `BlankFirstKey`.
///
/// That inverted the ranking the key exists to produce: `bare = 0` is the signature it promotes,
/// and a card that literally cannot be cast alone is the purest instance of it.
/// </summary>
[TestFixture]
public class LeverageMeasurementTests
{
	/// The literal is repeated here on purpose — the rule keys on this exact string, so a rename in
	/// the sandbox that forgot this call site would silently stop treating uncastable as bare zero.
	private const string NoLegalCast = "no legal cast action";

	[Test]
	public void AnUncastableBareArmIsBareZero_ButAFailedSuppliedArmIsStillFatal()
	{
		Assert.Multiple(() =>
		{
			Assert.That(
				CardValueSandbox.Unmeasured(null, null),
				Is.Null,
				"two clean arms measure"
			);
			Assert.That(
				CardValueSandbox.Unmeasured(NoLegalCast, null),
				Is.Null,
				"THE FIX: uncastable-without-support is the strongest possible bare zero"
			);

			// The asymmetry is the whole rule. Without a supplied number there is no leverage to
			// report, however well the bare arm went.
			Assert.That(
				CardValueSandbox.Unmeasured(null, "control threw"),
				Is.EqualTo("control threw"),
				"a failed SUPPLIED arm leaves nothing to measure"
			);
			Assert.That(
				CardValueSandbox.Unmeasured(NoLegalCast, "control threw"),
				Is.EqualTo("control threw")
			);

			// Only the uncastable case carries information. A crash is still a broken measurement.
			Assert.That(
				CardValueSandbox.Unmeasured("threw while generating casts", null),
				Is.EqualTo("threw while generating casts"),
				"a bare arm that THREW is broken, not informative — do not launder it to zero"
			);
		});
	}

	/// <summary>
	/// **A measured leverage may never be of terminal magnitude.**
	///
	/// The subject's score is checked for decisiveness and excluded by name, but the CONTROL it is
	/// subtracted from was not. A fixture that resolves itself inside the lookahead gave a baseline
	/// of ±9000, so every card measured against it returned `score - 9000` — and returned it as a
	/// clean result. Found while sweeping the rollout budget: Raise the Sunken came back at
	/// **-8018.06 with no error**, which reads as a real measurement of a catastrophic card.
	///
	/// Driven at a budget high enough to make the fixture decide itself, because that is the only
	/// state in which the bug appears. If the fixture ever stops deciding at this budget the test
	/// fails loudly rather than passing vacuously — the assertion is on the invariant, not on the
	/// count of failures.
	/// </summary>
	[Test]
	public void ADecidedControlIsReportedAsUnmeasured_NeverAsANumber()
	{
		var spells = SetRegistry.Get("DES").Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var features = PoolFeatures.Build(spells);
		var pool = spells
			.GroupBy(c => c.Name, StringComparer.Ordinal)
			.ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

		string[] probe = ["Kilnmother Vess", "Raise the Sunken", "Mere-Storm"];
		var rows = CardValueSandbox.MeasureLeverage(probe, features, pool, selfActionsPerTurn: 6);

		foreach (var r in rows)
			TestContext.Out.WriteLine(
				$"{r.Name,-20} lev {r.Leverage,10:F2} measured {r.WasMeasured}  {r.NotMeasured}"
			);

		Assert.That(
			rows.Where(r => r.WasMeasured).Select(r => r.Leverage),
			Has.All.Matches<float>(v => !StateEvaluator.IsDecisive(v)),
			"a leverage of terminal magnitude means the control decided the game — that is a "
				+ "failed measurement, not a card worth -8000"
		);
	}

	/// <summary>
	/// End to end on a pool where the answer is not in doubt: a one-mana reanimation spell cannot
	/// be cast with an empty graveyard, and must still come back measured with bare 0 and positive
	/// leverage — the combination that puts it in the blank tier.
	/// </summary>
	[Test]
	public void AReanimationSpellMeasures_WithBareZeroAndRealLeverage()
	{
		var raise = CardFactory
			.Spell("Raise", manaCost: 1)
			.WithReanimate()
			.WithTarget(TargetBuilder.Single().CreatureInYourGraveyard())
			.Build();

		List<Card> pool =
		[
			raise,
			CardFactory.Creature("Colossus", manaCost: 8, power: 7, toughness: 7).Build(),
			CardFactory.Creature("Ogre", manaCost: 3, power: 4, toughness: 4).Build(),
			// Ballast: PoolFeatures drops a demand answered by >99% of the pool, and a pool of
			// nothing but creatures makes "a creature in your graveyard" exactly that.
			CardFactory.Spell("Bolt", manaCost: 1).WithDamage(3).Build(),
			CardFactory.Spell("Growth", manaCost: 1).WithDraw(1).Build(),
		];

		var features = PoolFeatures.Build(pool);
		var index = pool.ToDictionary(c => c.Name, c => c, StringComparer.Ordinal);
		var row = CardValueSandbox.MeasureLeverage(["Raise"], features, index).Single();

		TestContext.Out.WriteLine(
			$"bare {row.Bare:F2}  supplied {row.Supplied:F2}  measured {row.WasMeasured}  {row.NotMeasured}"
		);

		Assert.Multiple(() =>
		{
			Assert.That(row.WasMeasured, Is.True, "the supplied arm succeeded, so this is a result");
			Assert.That(row.Bare, Is.Zero, "it cannot be cast at all without a creature to raise");
			Assert.That(
				row.Leverage,
				Is.GreaterThan(0f),
				"positive leverage plus bare zero is what puts a payoff in the blank tier — "
					+ "without both, BlankFirstKey demotes it"
			);
		});
	}
}
