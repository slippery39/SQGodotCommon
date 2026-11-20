using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Targeting;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4h3, LSVPower — overload (Damn): an alternative cost whose cast has no target words, "target" read as "each".
/// </summary>
public class LsvBatch2h3Tests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	[Test]
	public void Damn_ForBB_DestroysTheTarget_Only()
	{
		var bear = Field(Bear, them);
		var other = Field(Bear, them);
		var damn = Hand(Lsv("Damn"), me);
		Cast(damn, [], bear);
		Assert.That((Alive(bear), Alive(other)), Nu.EqualTo((false, true)));
	}

	[Test]
	public void Damn_Overloaded_TargetsNothing_DestroysEachCreature()
	{
		var bear = Field(Bear, them);
		var hexproof = Field(TestGames.Creature("Shrouded", "2/2", "Hexproof"), them);
		var mine = Field(Bear, me);
		var damn = Hand(Lsv("Damn"), me);
		var overloaded = Casts(damn).Where(c => c.WithoutPayingManaCost).ToList();
		Assert.That(
			overloaded.Select(c => c.Targets.Count(id => id != 0)),
			Nu.EqualTo(new[] { 0 }),
			"one overloaded cast, choosing no target"
		);
		Assert.That(
			Casts(damn).Count(c => !c.WithoutPayingManaCost),
			Nu.EqualTo(2),
			"for {B}{B}: one per creature it may target — never the hexproof one"
		);
		Assert.That(Describe.Action(s, overloaded.Single()), Does.EndWith("(overload)"));
		s = s.Act(overloaded.Single());
		Resolve();
		Assert.That(
			(Alive(bear), Alive(hexproof), Alive(mine)),
			Nu.EqualTo((false, false, false)),
			"each creature — hexproof doesn't stop what doesn't target"
		);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.IsTapped),
			Nu.EqualTo(4),
			"{2}{W}{W} paid"
		);
	}

	[Test]
	public void Damn_ACopyOfItOverloaded_IsOverloaded_NoNewTargetAsked()
	{
		var bear = Field(Bear, them);
		var damn = Hand(Lsv("Damn"), me);
		var twin = Hand(
			TestCards.Targeted("Twin", TargetSpec.Spell(), new CopySpell(Ref.Target())),
			me
		);
		s = s.Act(Casts(damn).Single(c => c.WithoutPayingManaCost));
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = twin,
				Targets = [damn],
			}
		);
		var asked = ResolveAsking(c => c.Options.Take(c.MinChoices).Select(o => o.Id));
		Assert.That(
			asked.OfType<ChooseTargetChoice>(),
			Nu.Empty,
			"CR 707.10: the copy is overloaded too"
		);
		Assert.That(Alive(bear), Nu.False);
	}
}
