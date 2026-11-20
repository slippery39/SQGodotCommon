using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Targeting;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4h4, LSVPower — splice onto Arcane (Goryo's Vengeance): a card in hand adds its text to an Arcane spell as it
/// is cast (CastSpellAction.Splice, Spliced).
/// </summary>
public class LsvBatch2h4Tests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition Legend =>
		TestCards.Typed(TestGames.Creature("Legend", "3/3"), "Legendary Creature — Elf");

	/// <summary>A {1} Arcane instant with a target of its own: "target player gains 2 life".</summary>
	static CardDefinition Rite =>
		TestCards.Typed(
			TestCards.Targeted("Rite", TargetSpec.Player(), new GainLife(Ref.Target(), 2)),
			"Instant — Arcane"
		);

	void ToEndStep()
	{
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.End && !x.StackIsEmpty());
		Resolve();
	}

	[Test]
	public void GoryosVengeance_ALegendaryCreatureCard_ReturnsWithHaste_ExiledAtTheEndStep()
	{
		var legend = Put(Legend, me, ZoneType.Graveyard);
		Put(Bear, me, ZoneType.Graveyard);
		var goryo = Hand(Lsv("Goryo's Vengeance"), me);
		Assert.That(
			Casts(goryo).Select(c => c.Targets.Single()),
			Nu.EqualTo(new[] { legend }),
			"target legendary creature card — never the Bear"
		);
		Cast(goryo, [], legend);
		Assert.That(Alive(legend) && s.Has(s.GetCard(legend), "Haste"));
		ToEndStep();
		Assert.That(InExile(legend), "exiled at the beginning of the next end step");
	}

	[Test]
	public void Splice_OntoAnArcaneSpell_ItsEffectsAdded_WithTheirOwnTarget_TheCardStaysInHand()
	{
		var legend = Put(Legend, me, ZoneType.Graveyard);
		var goryo = Hand(Lsv("Goryo's Vengeance"), me);
		var rite = Hand(Rite, me);
		var spliced = Casts(rite).Where(c => !c.Splice.IsDefaultOrEmpty).ToList();
		Assert.That(
			spliced.Select(c => c.Targets[1]),
			Nu.EqualTo(new[] { legend, legend }),
			"one cast per target of Rite's own (two players), Goryo's word after it"
		);
		var cast = spliced.Single(c => c.Targets[0] == me);
		Assert.That(Describe.Action(s, cast), Does.Contain("(splicing Goryo's Vengeance)"));
		var life = Life(me);
		var untapped = UntappedLands();
		s = s.Act(cast);
		Assert.That(
			s.GetCard(goryo).KnownTo,
			Nu.EqualTo(Knowers.Everyone),
			"you may reveal this card from your hand"
		);
		Resolve();
		Assert.That(untapped - UntappedLands(), Nu.EqualTo(4), "{1}, and the splice cost {2}{B}");
		Assert.That(Life(me), Nu.EqualTo(life + 2), "Rite's own text, its own target");
		Assert.That(
			Alive(legend) && s.Has(s.GetCard(legend), "Haste"),
			"Goryo's text, Target(0) its own word"
		);
		Assert.That(s.IsIn(goryo, me, ZoneType.Hand), "the spliced card stays in the hand");
		Assert.That(
			InGraveyard(rite) && !s.GetCard(rite).HasComponent<Spliced>(),
			"off the stack it is Rite alone"
		);
		ToEndStep();
		Assert.That(InExile(legend));
	}

	[Test]
	public void Splice_TheSpellsOwnEachTarget_IsItsOwnWordsOnly()
	{
		var legend = Put(Legend, me, ZoneType.Graveyard);
		var bear = Field(Bear, them);
		Hand(Lsv("Goryo's Vengeance"), me);
		var banish = Hand(
			TestCards.Typed(
				TestCards.Targeted("Banish", TargetSpec.Creature, new Exile(Ref.EachTarget)),
				"Instant — Arcane"
			),
			me
		);
		s = s.Act(Casts(banish).Single(c => !c.Splice.IsDefaultOrEmpty));
		Resolve();
		Assert.That(InExile(bear), "Banish's own word");
		Assert.That(Alive(legend), "Goryo's card isn't one of Banish's targets");
	}

	[Test]
	public void Splice_NeverOntoANonArcaneSpell_NorWithoutATargetForItsText()
	{
		Hand(Lsv("Goryo's Vengeance"), me);
		var rite = Hand(Rite, me);
		Assert.That(
			Casts(rite).Where(c => !c.Splice.IsDefaultOrEmpty),
			Nu.Empty,
			"no legendary creature card in my graveyard: Goryo's target can't be chosen"
		);
		Put(Legend, me, ZoneType.Graveyard);
		var plain = Hand(
			TestCards.Targeted("Plain", TargetSpec.Player(), new GainLife(Ref.Target(), 2)),
			me
		);
		Assert.That(
			Casts(plain).Where(c => !c.Splice.IsDefaultOrEmpty),
			Nu.Empty,
			"onto Arcane only"
		);
		Assert.That(Casts(rite).Where(c => !c.Splice.IsDefaultOrEmpty), Nu.Not.Empty);
	}

	[Test]
	public void Splice_OntoWeSayTheeNay_CountersAndReturns()
	{
		var legend = Put(Legend, me, ZoneType.Graveyard);
		var goryo = Hand(Lsv("Goryo's Vengeance"), me);
		var nay = Hand(Lsv("We Say Thee Nay!"), me);
		var bolt = TheirSpellOnTheStack(TestGames.Inline("Zap", "{1}", "Instant"));
		s = s.Act(Casts(nay).Single(c => !c.Kicked && !c.Splice.IsDefaultOrEmpty));
		Resolve();
		Assert.That(InGraveyard(bolt), "We Say Thee Nay!'s own text"); // they may pay {2}: the test answers no
		Assert.That(Alive(legend), "Goryo's Vengeance's, added");
		Assert.That(s.IsIn(goryo, me, ZoneType.Hand));
	}

	[Test]
	public void Splice_ACopyOfASplicedSpell_HasTheSplicedText()
	{
		Put(Legend, me, ZoneType.Graveyard);
		Hand(Lsv("Goryo's Vengeance"), me);
		var rite = Hand(Rite, me);
		var twin = Hand(
			TestCards.Targeted("Twin", TargetSpec.Spell(), new CopySpell(Ref.Target())),
			me
		);
		s = s.Act(Casts(rite).First(c => !c.Splice.IsDefaultOrEmpty));
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = twin,
				Targets = [rite],
			}
		);
		var asked = ResolveAsking(c => c.Options.Take(Math.Max(1, c.MinChoices)).Select(o => o.Id));
		Assert.That(
			asked.OfType<ChooseTargetChoice>().Count(),
			Nu.EqualTo(2),
			"CR 707.10: the copy's target words are Rite's and Goryo's"
		);
	}
}
