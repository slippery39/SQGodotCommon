using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Two ENGINE decks are held to a lower distance floor than anything else.**
///
/// A curve deck has no identity to defend, so the distance rule is the only thing stopping the
/// field collapsing into one pile. Two engine decks are already held apart by something stronger:
/// each is pool-locked to its own `DeckCore`. Holding them to the same field-wide floor actively
/// fights that, because archetype pools OVERLAP — every reanimation core draws on the same
/// graveyard suppliers, so two genuinely different archetypes can sit inside 35% of each other
/// purely for sharing enablers, and the second gets rejected for resembling the first.
///
/// The asymmetry is the whole feature, so both directions are asserted. A change that simply
/// lowered the floor for everyone passes the first test and fails the other two.
/// </summary>
[TestFixture]
public class EngineDiversityTests
{
	private const double Field = 0.35;
	private const double Engines = 0.10;

	/// <summary>
	/// Two lists 20% apart: **inside the field floor and outside the engine floor**, which is the
	/// band the relaxation exists to open. 32 shared spells and 8 unique each side, so
	/// `1 - 2*32/80 = 0.20`.
	///
	/// The in-range assertion in the first test guards this arithmetic — a first draft sat at
	/// 6.98%, below BOTH floors, where every assertion here would have passed for the wrong reason.
	/// </summary>
	private static (Decklist A, Decklist B) NearIdentical()
	{
		var a = Decklist.Empty("A") with { Lands = Decklist.MinLands };
		var b = Decklist.Empty("B") with { Lands = Decklist.MinLands };

		for (var i = 0; i < 8; i++)
		{
			a = a.WithCopies($"Shared{i}", 4);
			b = b.WithCopies($"Shared{i}", 4);
		}

		for (var i = 0; i < 2; i++)
		{
			a = a.WithCopies($"OnlyInA{i}", 4);
			b = b.WithCopies($"OnlyInB{i}", 4);
		}

		return (a, b);
	}

	private static DeckCore Core(string name) => new(name, []);

	[Test]
	public void TwoEngineSlots_MayResembleEachOther()
	{
		var (a, b) = NearIdentical();
		var difference = Decklist.Difference(a, b);

		Assert.That(
			difference,
			Is.InRange(Engines, Field),
			"fixture is wrong: the pair must sit BETWEEN the two floors or nothing is under test"
		);

		Assert.That(
			MetagameEvolver.DiverseEnough(
				a,
				0,
				[a, b],
				[Core("engine-a"), Core("engine-b")],
				Field,
				Engines
			),
			Is.True,
			"two engine decks are held apart by their pool locks; the distance rule must not "
				+ "reject the second archetype for sharing enablers with the first"
		);
	}

	[Test]
	public void ACurveSlot_IsStillHeldToTheFieldFloor()
	{
		var (a, b) = NearIdentical();

		Assert.Multiple(() =>
		{
			// Neither is an engine — the ordinary case, unchanged.
			Assert.That(
				MetagameEvolver.DiverseEnough(a, 0, [a, b], [null, null], Field, Engines),
				Is.False,
				"two curve decks this similar must still be rejected"
			);

			// **The mixed pair is the one that matters.** A curve slot is the good-stuff control
			// the themed slots are read against, and a control that has drifted into a themed deck
			// is not a control — so an engine beside a curve deck gets the FIELD floor, not the
			// engine one, whichever of the two is being mutated.
			Assert.That(
				MetagameEvolver.DiverseEnough(
					a,
					0,
					[a, b],
					[Core("engine-a"), null],
					Field,
					Engines
				),
				Is.False,
				"an engine must still stand clear of a curve deck"
			);
			Assert.That(
				MetagameEvolver.DiverseEnough(
					a,
					0,
					[a, b],
					[null, Core("engine-b")],
					Field,
					Engines
				),
				Is.False,
				"a curve deck must still stand clear of an engine"
			);
		});
	}
}
