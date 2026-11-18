using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4g8, LSVPower — spree: Three Steps Ahead, FIRING against its printed text through real casting and the real stack.
/// </summary>
public class LsvBatch2g8Tests : FraBoardTest
{
	static CardDefinition Boon =>
		TestGames.Inline("Boon", "{1}", "Instant", null, new GainLife(Ref.You, 1));

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	int Bears() => s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Bear");

	CastSpellAction CastOf(int card, params int[] modes) =>
		Casts(card).Single(c => c.Modes.SequenceEqual(modes));

	[Test]
	public void Spree_IsOneCastPerSetOfModes_ItsTargetsChosenAfter()
	{
		TheirSpellOnTheStack(Boon);
		for (var i = 0; i < 3; i++)
			Field(Bear, me);
		var steps = Hand(Lsv("Three Steps Ahead"), me);

		var casts = Casts(steps).ToList();

		Assert.That(
			(casts.Count, casts.Count(c => c.ChoosesTargets)),
			Nu.EqualTo((7, 6)),
			"one or more of three modes, never a cast per spell and bear; only 'draw two' has no target"
		);
	}

	[Test]
	public void Spree_AllThree_PaysEachModesCost_Counters_Copies_Loots()
	{
		var boon = TheirSpellOnTheStack(Boon);
		var bear = Field(Bear, me);
		var steps = Hand(Lsv("Three Steps Ahead"), me);
		var (lands, library, life) = (UntappedLands(), Library(me).Count, s.GetPlayer(them).Life);

		s = s.Act(CastOf(steps, 0, 1, 2));
		Resolve(c =>
			c switch
			{
				ChooseTargetChoice t => [t.Index == 0 ? boon : bear],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);

		Assert.That(
			(
				lands - UntappedLands(),
				InGraveyard(boon),
				s.GetPlayer(them).Life - life,
				Bears(),
				library - Library(me).Count
			),
			Nu.EqualTo((8, true, 0, 2, 2)),
			"{U} + {1}{U} + {3} + {2}; their spell countered; a token copy of the bear; two drawn"
		);
	}

	[Test]
	public void Spree_OneMode_PaysOnlyThatModesCost()
	{
		var steps = Hand(Lsv("Three Steps Ahead"), me);
		var lands = UntappedLands();

		s = s.Act(CastOf(steps, 2));
		Resolve();

		Assert.That(lands - UntappedLands(), Nu.EqualTo(3), "{U} + {2}");
	}

	[Test]
	public void Spree_AModeOutOfRange_IsInvalid_NeverAThrow()
	{
		var steps = Hand(Lsv("Three Steps Ahead"), me);
		var cast = CastOf(steps, 2) with { Modes = [5], LifeForPhyrexian = 1 };

		Assert.That(cast.ValidateAdd(s).IsValid, Nu.False);
	}

	[Test]
	public void Spree_DescribedByWhatEachModeDoes_NotItsCost()
	{
		var text = CardRegistry
			.Get("Three Steps Ahead", "LSV")
			.Components.OfType<SpellEffectsComponent>()
			.Single();

		Assert.That(
			Describe.Modes(text.Modes, [0, 2]),
			Nu.EqualTo("Counter target spell. + Draw two cards, then discard a card.")
		);
	}

	[Test]
	public void Spree_OnATriggerOrAbility_IsRefused_NothingWouldPayIt()
	{
		Mode[] three = [Scripting.M([]), Scripting.M([]), Scripting.M([])];

		Assert.That(
			() => Scripting.Printed("Three Steps Ahead", three),
			Throws.InstanceOf<InvalidOperationException>()
		);
	}
}
