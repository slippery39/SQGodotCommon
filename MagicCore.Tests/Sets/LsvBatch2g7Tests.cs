using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Targeting;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4g7, LSVPower — storm: Brain Freeze, FIRING against its printed text through real casting and the real stack.
/// </summary>
public class LsvBatch2g7Tests : FraBoardTest
{
	static CardDefinition Boon =>
		TestGames.Inline("Boon", "{1}", "Instant", null, new GainLife(Ref.You, 1));

	static CardDefinition Nope =>
		new(
			TestGames.Inline("Nope", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([new Counter(Ref.Target())])
				{
					Targets = [TargetSpec.Spell()],
				},
			]
		);

	int CastFreezeAt(int target)
	{
		var freeze = Hand(Lsv("Brain Freeze"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = freeze,
				Targets = [target],
			}
		);
		return freeze;
	}

	[Test]
	public void Storm_CopiesItForEverySpellCastBeforeIt_TheirsToo()
	{
		TheirSpellOnTheStack(Boon);
		Resolve();
		Cast(Hand(Boon, me), []);
		var library = Library(them).Count;

		CastFreezeAt(them);
		Resolve(Targeting(them, false));

		Assert.That(
			library - Library(them).Count,
			Nu.EqualTo(9),
			"their spell and mine before it: two copies, three mills of three"
		);
	}

	[Test]
	public void Storm_CountedAsItIsCast_AndCountered_ItIsStillCopied()
	{
		Cast(Hand(Boon, me), []);
		var library = Library(them).Count;

		var freeze = CastFreezeAt(them);
		Assert.That(
			s.GetChildren(s.GetWellKnownId(MagicKeys.Stack)).First(),
			Nu.InstanceOf<StackAbility>(),
			"the storm trigger, above the spell, before anyone responds"
		);
		var nope = Hand(Nope, me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = nope,
				Targets = [freeze],
			}
		);
		Resolve(Targeting(them, false));

		Assert.That(
			(InGraveyard(freeze), library - Library(them).Count),
			Nu.EqualTo((true, 3)),
			"one copy: the counter, cast after it, isn't counted, and the countered original is still copied"
		);
	}
}
