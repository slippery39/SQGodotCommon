using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Targeting;

namespace MagicCore.Tests.Ai;

/// <summary>
/// The card-value sandbox orders what it measures sensibly. Inline cards, so a balance pass on a real
/// card can't move these.
/// </summary>
public class CardValuesTests
{
	static readonly CardDefinition Kill =
		new(
			TestGames.Inline("Kill", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([new Destroy(Ref.Target())])
				{
					Targets = [TargetSpec.Creature],
				},
			]
		);

	static double ValueOf(CardDefinition card) => CardValues.Measure([card]).Single().Value;

	[Test]
	public void ABiggerCreature_IsWorthMore()
	{
		var values = CardValues.Measure(
			[TestGames.Creature("Small", "1/1"), TestGames.Creature("Big", "3/3")]
		);

		Assert.That(values[1].Value, Is.GreaterThan(values[0].Value));
		Assert.That(values[0].Value, Is.GreaterThan(0));
	}

	// Removal finds the ladder's biggest body worth killing: it must be worth more than a vanilla 1/1.
	[Test]
	public void Removal_IsWorthMoreThanASmallCreature() =>
		Assert.That(ValueOf(Kill), Is.GreaterThan(ValueOf(TestGames.Creature("Small", "1/1"))));

	// Measured as the turn ends, so what the end step takes away is gone: a 4/4 sacrificed "at the
	// beginning of the end step" (Frostbite Pyromental's clause) is worth nothing — measured, not skipped.
	[Test]
	public void ACreatureSacrificedAtTheEndStep_IsWorthNothing()
	{
		var doomed = TestGames.Creature("Doomed", "4/4") with
		{
			Components =
			[
				new TriggeredAbilityComponent(
					TriggerEvent.EachEndStep,
					[new Sacrifice(Ref.Source)]
				),
			],
		};

		var value = CardValues.Measure([doomed]).Single();

		Assert.That(value.NotMeasured, Is.Null);
		Assert.That(value.Value, Is.EqualTo(0));
	}

	// Our own choices are tried, not declined: "you may gain 5 life" is worth taking.
	[Test]
	public void AYouMay_IsTaken()
	{
		var maybe = new CardDefinition(
			TestGames.Inline("Maybe", "{1}", "Sorcery").Data,
			// "your library isn't empty": always so in the fixture
			[
				new SpellEffectsComponent(
					[
						new May(
							"Gain 5 life?",
							new Not(new YourLibraryIsEmpty()),
							[new GainLife(Ref.You, 5)]
						),
					]
				),
			]
		);

		Assert.That(ValueOf(maybe), Is.GreaterThan(0));
	}

	// The fixture has no colourless sources, so a {C} pip is the honest uncastable case.
	[Test]
	public void ALand_AndAnUncastableCard_AreNotMeasured()
	{
		var values = CardValues.Measure(
			[TestGames.Card("Plains"), TestGames.CreatureCosting("{C}", "Colourless", "2/2")]
		);

		Assert.That(values.Select(v => v.NotMeasured), Has.All.Not.Null);
		Assert.That(values.Select(v => v.Value), Has.All.EqualTo(0));
	}

	// The tracked table is what measuring gives NOW — a change to the evaluator, the engine or a card that
	// moves a value fails here until the table is regenerated, so it can't drift silently (~1 s).
	[Test]
	public void TheTrackedTable_IsCurrent()
	{
		foreach (var set in CardValues.TableSets)
			Assert.That(
				CardValues.TableText(set)?.Replace("\r\n", "\n"),
				Is.EqualTo(CardValues.ToJson(CardValues.MeasureSet(set))),
				$"{CardValues.TablePathFor(set)} is stale — run: dotnet run --project MagicConsole -- --card-values"
			);
	}

	// The table loads back as what was measured: every card, by name, measured or not alike.
	[Test]
	public void TheTable_LoadsBackWhatWasMeasured()
	{
		var measured = CardValues
			.TableSets.SelectMany(CardValues.MeasureSet)
			.DistinctBy(m => m.Name)
			.ToList();

		Assert.That(CardValues.Fra, Has.Count.EqualTo(measured.Count));
		Assert.That(
			measured.Select(m => (m.Name, m.NotMeasured)),
			Is.EqualTo(measured.Select(m => (m.Name, CardValues.Fra[m.Name].NotMeasured)))
		);
	}

	[Test]
	public void TheSameCards_MeasureTheSame()
	{
		CardDefinition[] cards = [Kill, TestGames.Creature("Big", "3/3")];

		Assert.That(CardValues.Measure(cards), Is.EqualTo(CardValues.Measure(cards)));
	}
}
