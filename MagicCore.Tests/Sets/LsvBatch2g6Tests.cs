using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4g6, LSVPower — teamwork: We Say Thee Nay!, FIRING against its printed text through real casting and the
/// real stack.
/// </summary>
public class LsvBatch2g6Tests : FraBoardTest
{
	static CardDefinition Bird => TestGames.Creature("Bird", "1/1");

	static CardDefinition Wall => TestGames.Creature("Wall", "0/1");

	static CardDefinition Boon =>
		TestGames.Inline("Boon", "{1}", "Instant", null, new GainLife(Ref.You, 1));

	/// <summary>"{T}: Add {U}" on a creature of this size.</summary>
	static CardDefinition Dork(string size) =>
		TestGames.Creature("Dork", size) with
		{
			Components = [new ManaAbilityComponent(ManaColor.Blue)],
		};

	/// <summary>TEST SETUP: they keep only this many untapped lands — {2} they can pay, {4} not.</summary>
	void LeaveThemLands(int n)
	{
		foreach (
			var land in s.CardsIn(them, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Land) && !c.IsTapped)
				.Skip(n)
				.ToList()
		)
			TapIt(land.Id);
	}

	int Nay() => Hand(Lsv("We Say Thee Nay!"), me);

	static IEnumerable<int> PayIfAsked(ChoiceAction c) =>
		c switch
		{
			PayToKeepChoice => [1],
			_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
		};

	[Test]
	public void Teamwork_TapsCreaturesWithTotalPowerTwo_AndCountersUnlessTheyPayFour()
	{
		var first = Field(Bird, me);
		var second = Field(Bird, me);
		var wall = Field(Wall, me);
		var spell = TheirSpellOnTheStack(Boon);
		LeaveThemLands(3);
		var nay = Nay();
		var cast = Casts(nay).Single(c => c.Kicked);
		Assert.That(Describe.Action(s, cast), Does.Contain("(teamwork)"));

		s = s.Act(cast);
		var asked = ResolveAsking(PayIfAsked);

		Assert.That(
			asked.OfType<ChoosePermanentChoice>().Count(),
			Nu.EqualTo(2),
			"one creature at a time, until the power tapped is 2"
		);
		Assert.That(
			Offered<ChoosePermanentChoice>(asked),
			Nu.Not.Contains(wall),
			"no power, no help (a ceiling)"
		);
		Assert.That(
			(s.GetCard(first).IsTapped, s.GetCard(second).IsTapped, s.GetCard(wall).IsTapped),
			Nu.EqualTo((true, true, false))
		);
		Assert.That(
			(asked.OfType<PayToKeepChoice>().Any(), InGraveyard(spell), Life(them)),
			Nu.EqualTo((false, true, 20)),
			"{4} with three lands: not asked, countered"
		);
	}

	[Test]
	public void WithoutTeamwork_TheyMayPayTwo()
	{
		var bird = Field(Bird, me);
		Field(Bird, me);
		var spell = TheirSpellOnTheStack(Boon);
		LeaveThemLands(3);
		var cast = Casts(Nay()).Single(c => !c.Kicked);

		s = s.Act(cast);
		var asked = ResolveAsking(PayIfAsked);

		Assert.That(asked.OfType<PayToKeepChoice>().Any(), "{2} with three lands: asked");
		Assert.That(
			(InGraveyard(spell), Life(them), s.GetCard(bird).IsTapped),
			Nu.EqualTo((true, 21, false)),
			"paid: it resolved (an instant, to the graveyard); nothing tapped for teamwork"
		);
	}

	[Test]
	public void Teamwork_NotOfferedBelowTotalPowerTwo()
	{
		Field(Bird, me);
		Field(Wall, me);
		TheirSpellOnTheStack(Boon);
		var nay = Nay();
		Assert.That(Casts(nay).Select(c => c.Kicked), Nu.EquivalentTo(new[] { false }));
	}

	[Test]
	public void Teamwork_CreatureNeededForTheManaCantAlsoBeTapped()
	{
		TapMyLands();
		Field(Dork("2/2"), me); // the only power — and half the {1}{U}
		Field(Dork("0/1"), me);
		TheirSpellOnTheStack(Boon);
		var nay = Nay();
		Assert.That(
			Casts(nay).Select(c => c.Kicked),
			Nu.EquivalentTo(new[] { false }),
			"paying {1}{U} taps both: none left with power 2"
		);
	}
}
