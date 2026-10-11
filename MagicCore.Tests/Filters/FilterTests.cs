using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Filters;
using Is = MagicCore.Filters.Is; // the filter vocabulary, over NUnit's Is
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Filters;

/// <summary>Each leaf, the operators, and last-known objects — on <see cref="FilterBoard"/>, in every zone.</summary>
public class FilterTests
{
	static readonly FilterBoard B = FilterBoard.Build();

	static IEnumerable<TestCaseData> Leaves()
	{
		TestCaseData Leaf(string name, Filter f, string expected, string source = "Src") =>
			new TestCaseData(f, source, expected).SetName($"Leaf_{name}");
		yield return Leaf(
			"Creature",
			Is.Creature,
			"Src Sage Thopter Wall Brute Raider Spirit Bear Golem Stalker Golem_Spell Titan Bones Elf"
		);
		yield return Leaf("Planeswalker", Is.Planeswalker, "Jace Vow");
		yield return Leaf("Land", Is.Land, "Grove Grove_2 Meadow Isle Marsh");
		yield return Leaf(
			"TypeAnyOf",
			Is.Type(CardType.Artifact | CardType.Enchantment),
			"Thopter Ward Shrine Golem Sword Golem_Spell"
		);
		yield return Leaf("Legendary", Is.Legendary, "Sage Jace Duress Titan Lore");
		yield return Leaf("Subtype", Is.Subtype("Wizard"), "Sage Thopter");
		yield return Leaf("Color", Is.Color(ManaColor.Red), "Thopter Raider Shock Blast");
		yield return Leaf("With", Is.With("Flying"), "Sage Spirit");
		yield return Leaf(
			"Yours",
			Is.Yours,
			"me Src Sage Thopter Jace Grove Grove_2 Meadow Ward Shrine Bear Golem Sword Shock Golem_Spell Echo Bones Marsh Lore"
		);
		yield return Leaf(
			"Opponents",
			Is.Opponents,
			"them Wall Brute Raider Spirit Isle Vow Stalker Duress Titan Elf Blast"
		);
		yield return Leaf("Player", Is.Player, "me them");
		yield return Leaf("Source", Is.Source, "Src");
		yield return Leaf("AttachedToSource", Is.AttachedToSource, "Bear", source: "Sword");
		yield return Leaf("Token", Is.Token, "Thopter Spirit");
		yield return Leaf("Copy", Is.Copy, "Brute");
		yield return Leaf("Prepared", Is.Prepared, "Raider");
		yield return Leaf("PreparedCopy", Is.PreparedCopy, "Echo");
		yield return Leaf("WithPlusOneCounter", Is.WithPlusOneCounter, "Sage");
		yield return Leaf("EnteredThisTurn", Is.EnteredThisTurn, "Bear");
		yield return Leaf("AttackedThisTurn", Is.AttackedThisTurn, "Bear Golem");
		yield return Leaf("Attacking", Is.Attacking, "Bear Golem Raider Stalker");
		yield return Leaf("AttackingAPlayer", Is.AttackingAPlayer, "Bear Raider");
		yield return Leaf("AttackingYou", Is.AttackingYou, "Raider Stalker");
		yield return Leaf("Blocking", Is.Blocking, "Wall");
		yield return Leaf("InCombat", Is.InCombat, "Bear Golem Raider Stalker Wall");
		// Sage is 4/5 with a +1/+1 counter; Titan is a 6/6 spell on the stack.
		yield return Leaf("PowerAtLeast", Is.PowerAtLeast(5), "Sage Brute Titan");
		yield return Leaf("ToughnessAtLeast", Is.ToughnessAtLeast(5), "Sage Brute Titan");
		yield return Leaf("ManaValueAtLeast", Is.ManaValueAtLeast(4), "Titan Lore Blast");
		yield return Leaf(
			"Operators",
			Is.Creature & Is.Yours & !Is.Source,
			"Sage Thopter Bear Golem Golem_Spell Bones"
		);
		yield return Leaf("Or", Is.Token | Is.Copy, "Thopter Spirit Brute");
	}

	[TestCaseSource(nameof(Leaves))]
	public void MatchesExactly(Filter filter, string source, string expected) =>
		Assert.That(
			Matching(filter, B.Context(source)),
			Nu.EquivalentTo(expected.Split(' ').Select(l => l.Replace('_', ' ')))
		);

	[Test]
	public void EventCard_IsTheContextsEventCard() =>
		Assert.That(
			Matching(Is.EventCard, B.Context(eventCard: "Bear")),
			Nu.EqualTo(new[] { "Bear" })
		);

	[Test]
	public void Filters_AreValues_SoComponentsCompareEqual() =>
		Assert.That(
			Is.Creature & Is.Yours,
			Nu.EqualTo(
				new AllOf(new OfType(CardType.Creature), new OfController(Targeting.Whose.You))
			)
		);

	[Test]
	public void NoneAsAType_Throws_RatherThanMatchingNothing()
	{
		// The old vocabularies read None as "any"; a mechanical port would make an inert card.
		Assert.Throws<ArgumentException>(() => Is.Type(CardType.None));
		Assert.Throws<ArgumentException>(() => Is.Supertype(Supertype.None));
	}

	[Test]
	public void ALastKnownObject_IsFilteredAsItWas()
	{
		var snapshot = B.Card("Sage"); // a 5/6 with a +1/+1 counter
		var s = B.S.MoveCard(snapshot.Id, B.S.ZoneId(B.Me, ZoneType.Graveyard));
		var now = s.GetCard(snapshot.Id); // a new object: no counter
		var f = Is.Creature & Is.Yours & Is.WithPlusOneCounter & Is.PowerAtLeast(5);

		Assert.That(f.Matches(s, snapshot, B.Context()), Nu.True);
		Assert.That(f.Matches(s, now, B.Context()), Nu.False);
		Assert.That((Is.Creature & Is.Yours).Matches(s, now, B.Context()), Nu.True); // any zone
	}

	static IEnumerable<string> Matching(Filter f, FilterContext c) =>
		B.Everything().Where(o => f.Matches(B.S, o, c)).Select(o => B.LabelOf(o.Id));
}
