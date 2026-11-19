using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4g10, LSVPower — Classes: Does Machines, FIRING against its printed text through real casting and the real stack.
/// </summary>
public class LsvBatch2g10Tests : FraBoardTest
{
	static CardDefinition Trinket => TestGames.Inline("Trinket", "{1}", "Artifact");

	/// <summary>I cast Does Machines and it all resolves (the discard: the first two cards).</summary>
	int Machines()
	{
		var dm = Hand(Lsv("Does Machines"), me);
		Cast(dm, []);
		return dm;
	}

	/// <summary>The Class's one level ability on offer, activated, and the stack resolved as told.</summary>
	List<ChoiceAction> LevelUp(int dm, Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		s = s.Act(Activations(dm).Single());
		return ResolveAsking(answer ?? (c => c.Options.Take(c.MinChoices).Select(o => o.Id)));
	}

	/// <summary>Its level, as a script reads it: Count.ClassLevelOf.</summary>
	int Level(int dm) => Count.ClassLevelOf(Ref.Source).Value(s, Conditions.Of(s.GetCard(dm)));

	/// <summary>Through my beginning of combat (its triggers' targets: the first offered).</summary>
	void ThroughCombatStart() =>
		s = TestGames.PassUntil(s, x => x.GetGame().Step > Step.BeginCombat);

	[Test]
	public void Enters_MillTwo_DrawTwo_ThenDiscardTwo()
	{
		var library = Library(me).Count;
		var hand = HandCount(me);

		Machines();

		Assert.That(
			(library - Library(me).Count, HandCount(me), s.CardsIn(me, ZoneType.Graveyard).Count()),
			Nu.EqualTo((4, hand, 4)),
			"two milled and two drawn; two discarded: the hand as it was, less the Class"
		);
	}

	[Test]
	public void Level2_AsASorcery_OnlyFromLevel1_ReturnsUpToTwoArtifactCards()
	{
		var dm = Machines();
		var a = Put(Trinket, me, ZoneType.Graveyard);
		var b = Put(Trinket, me, ZoneType.Graveyard);
		Assert.That(Level(dm), Nu.EqualTo(1), "a Class enters at level 1");

		var lands = UntappedLands();
		LevelUp(
			dm,
			c =>
				c switch
				{
					ChooseTargetChoice t => [t.Index == 0 ? a : b],
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				}
		);

		Assert.That(
			(
				Level(dm),
				lands - UntappedLands(),
				s.IsIn(a, me, ZoneType.Hand),
				s.IsIn(b, me, ZoneType.Hand)
			),
			Nu.EqualTo((2, 2, true, true)),
			"{1}{U}: level 2 — and its trigger returned both"
		);
		Assert.That(
			Activations(dm).Select(x => x.AbilityIndex),
			Nu.EqualTo(new[] { 1 }),
			"now only Level 3 is offered"
		);

		var c = Put(Trinket, me, ZoneType.Graveyard);
		var asked = LevelUp(dm, On<ChooseTargetChoice>(c));
		Assert.That(
			(asked.OfType<ChooseTargetChoice>().Count(), InGraveyard(c)),
			Nu.EqualTo((0, true)),
			"becoming level 3 is not becoming level 2"
		);
	}

	[Test]
	public void Level2_NotAtInstantSpeed()
	{
		var dm = Machines();
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.BeginCombat);

		Assert.That(Activations(dm), Nu.Empty);
	}

	[Test]
	public void Level3_EachCombat_ThreeCounters_ANoncreatureArtifactBecomesA0_0Robot()
	{
		var dm = Machines();
		var trinket = Field(Trinket, me);
		ThroughCombatStart();
		Assert.That(
			s.GetCard(trinket).PlusOneCounters,
			Nu.EqualTo(0),
			"level 1: no combat trigger"
		);

		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);
		LevelUp(dm);
		LevelUp(dm);
		Assert.That(Level(dm), Nu.EqualTo(3));
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().ActivePlayerId == me
				&& x.GetGame().Step == Step.PreCombatMain
				&& x.GetGame().TurnNumber > 2
		);
		ThroughCombatStart();

		var card = s.GetCard(trinket);
		Assert.That(
			(
				card.Is(CardType.Creature),
				card.Is(CardType.Artifact),
				s.HasSubtype(card, "Robot"),
				s.Power(card),
				s.Toughness(card)
			),
			Nu.EqualTo((true, true, true, 3, 3)),
			"a 0/0 Robot artifact creature with three +1/+1 counters"
		);
	}

	[Test]
	public void ItsLevel_GoneAsItLeaves()
	{
		var dm = Machines();
		LevelUp(dm);
		s = s.MoveCard(dm, s.ZoneId(me, ZoneType.Hand)); // TEST SETUP: bounced

		Cast(dm, []);

		Assert.That(Level(dm), Nu.EqualTo(1), "a new object: level 1 again");
	}
}
