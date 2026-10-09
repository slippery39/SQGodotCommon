using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;

namespace MagicCore.Tests.Sets;

/// <summary>Uncommons (7e-2a): removal and spells — each ability fires.</summary>
public class FraUncommonTests : FraBoardTest
{
	static CardDefinition Colored(CardDefinition card, params string[] colors) =>
		card with
		{
			Data = card.Data with { Colors = colors },
		};

	IEnumerable<int> TargetsOf(int card) => Casts(card).Select(a => a.Targets[0]);

	[Test]
	public void RefuteDestiny_ExilesAGreenOrBlueCreature_ThenSurveils()
	{
		var green = Field(Colored(TestGames.Creature("Elf", "2/2"), "G"), them);
		var red = Field(Colored(TestGames.Creature("Goblin", "2/2"), "R"), them);
		var refute = Hand("Refute Destiny", me);
		Assert.That(TargetsOf(refute), Does.Contain(green).And.Not.Contain(red));

		Cast(refute, [], green);
		Assert.That(s.IsIn(green, them, ZoneType.Exile));
		Assert.That(s.GetPlayer(me).ThisTurn.Surveils, Is.EqualTo(1));
	}

	[Test]
	public void YourFateEndsHere_DestroysOnlyManaValueThreeOrGreater()
	{
		var big = Field(TestGames.CreatureCosting("{3}", "Big", "3/3"), them);
		var small = Field(TestGames.CreatureCosting("{2}", "Small", "2/2"), them);
		var fate = Hand("Your Fate Ends Here", me);
		Assert.That(TargetsOf(fate), Does.Contain(big).And.Not.Contain(small));

		Cast(fate, [], big);
		Assert.That(s.IsIn(big, them, ZoneType.Graveyard));
	}

	[Test]
	public void PreciseRedaction_CountersAWhiteOrBlackSpell()
	{
		var flashy = TheirSpellOnTheStack(
			Colored(TestGames.Creature("Flashy", "2/2", "Flash"), "B")
		);
		var redaction = Hand("Precise Redaction", me);
		Cast(redaction, [], flashy);
		Assert.That(s.IsIn(flashy, them, ZoneType.Graveyard));
	}

	[Test]
	public void PreciseRedaction_CannotTargetARedSpell()
	{
		TheirSpellOnTheStack(Colored(TestGames.Creature("Flashy", "2/2", "Flash"), "R"));
		var redaction = Hand("Precise Redaction", me);
		Assert.That(Casts(redaction), Is.Empty);
	}

	[Test]
	public void MultiplyByZero_ACreatureWithBaseZeroZeroDies()
	{
		var bear = Field(TestGames.Creature("Bear", "3/3"), them);
		Cast(Hand("Multiply by Zero", me), [], bear);
		Assert.That(s.IsIn(bear, them, ZoneType.Graveyard));
	}

	[Test]
	public void EssenceBurn_WhatWouldDieIsExiledInstead()
	{
		var elf = Field(Colored(TestGames.Creature("Elf", "2/2"), "G"), them);
		Cast(Hand("Essence Burn", me), [], elf);
		Assert.That(s.IsIn(elf, them, ZoneType.Exile));
	}

	[Test]
	public void EssenceBurn_ASurvivorDyingLaterThisTurnIsStillExiled()
	{
		var wurm = Field(Colored(TestGames.Creature("Wurm", "6/6"), "G"), them);
		Cast(Hand("Essence Burn", me), [], wurm);
		Assert.That(Alive(wurm), "5 damage doesn't kill a 6/6");

		var ping = Hand(
			TestGames.Inline(
				"Ping",
				"{R}",
				"Instant",
				null,
				new DealDamage(Ref.OpponentsPermanents(CardType.Creature), 1)
			),
			me
		);
		Cast(ping, []);
		Assert.That(s.IsIn(wurm, them, ZoneType.Exile));
	}

	[Test]
	public void ViolentEchoes_ExcessDamageEmpowersJace()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		Cast(Hand("Violent Echoes", me), [], bear);
		Assert.That(Jace(me)?.Loyalty, Is.EqualTo(6 - 2));
	}

	[Test]
	public void ViolentEchoes_ExcessOverAPlaneswalkersLoyalty()
	{
		var walker = Field(TestGames.Planeswalker("Walker", 3), them);
		s = s.UpdateObject(walker, s.GetCard(walker) with { Loyalty = 3 }); // TEST SETUP: Put skips entering
		Cast(Hand("Violent Echoes", me), [], walker);
		Assert.That(Jace(me)?.Loyalty, Is.EqualTo(6 - 3));
	}

	[Test]
	public void ViolentEchoes_NoExcess_NoJace()
	{
		var wurm = Field(TestGames.Creature("Wurm", "6/6"), them);
		Cast(Hand("Violent Echoes", me), [], wurm);
		Assert.That(Jace(me), Is.Null);
	}

	[Test]
	public void ClashOfElements_TheOwnerPutsItOnTop_AndTakesTwo()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var life = Life(them);
		var asked = 0;
		var clash = Hand("Clash of Elements", me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = clash,
				Targets = [bear],
			}
		);
		Resolve(choice =>
		{
			if (choice is YesNoChoice { PlayerId: var who } && who == them)
				asked++;
			return choice switch
			{
				YesNoChoice => [1],
				_ => [],
			};
		});

		Assert.That(asked, Is.EqualTo(1), "its OWNER chooses");
		Assert.That(s.GetChildrenIds(s.ZoneId(them, ZoneType.Library)).First(), Is.EqualTo(bear));
		Assert.That(Life(them), Is.EqualTo(life - 2));
	}

	[Test]
	public void ClashOfElements_OtherwiseTheBottom_AndNoDamage()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var life = Life(them);
		Cast(Hand("Clash of Elements", me), [], bear); // the default answer is "no"

		Assert.That(s.GetChildrenIds(s.ZoneId(them, ZoneType.Library)).Last(), Is.EqualTo(bear));
		Assert.That(Life(them), Is.EqualTo(life));
	}

	[Test]
	public void PrimalWitchstalker_MillsFour_ThenReturnsAMilledLandTapped()
	{
		int forest;
		(s, forest) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Library);
		s = s.MoveObjectToFront(forest, s.ZoneId(me, ZoneType.Library)); // TEST SETUP: the top
		var library = s.CardsIn(me, ZoneType.Library).Count();

		var witchstalker = Hand("Primal Witchstalker", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = witchstalker });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [forest],
				_ => [],
			}
		);

		Assert.That(s.CardsIn(me, ZoneType.Library).Count(), Is.EqualTo(library - 4));
		Assert.That((Alive(forest), s.GetCard(forest).IsTapped), Is.EqualTo((true, true)));
	}

	[Test]
	public void PrimalWitchstalker_AnEmptyLibraryMillsNothing_SoNoLandReturns()
	{
		int forest;
		(s, forest) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Graveyard);
		foreach (var card in s.CardsIn(me, ZoneType.Library).ToList())
			s = s.MoveObject(card.Id, s.ZoneId(me, ZoneType.Exile)); // TEST SETUP: an empty library

		var witchstalker = Hand("Primal Witchstalker", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = witchstalker });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [forest],
				_ => [],
			}
		);
		Assert.That(
			s.IsIn(forest, me, ZoneType.Graveyard),
			"nothing milled, so no \"when you do\""
		);
	}

	[Test]
	public void TheorixCharm_CountersANoncreatureSpellTheyDontPayFor()
	{
		var trick = TheirSpellOnTheStack(
			TestGames.Inline("Trick", "{R}", "Instant", null, new GainLife(Ref.You, 1))
		);
		var life = Life(them);
		Cast(Hand("Theorix Charm", me), [0], trick);
		Assert.That(
			(s.IsIn(trick, them, ZoneType.Graveyard), Life(them)),
			Is.EqualTo((true, life))
		);
	}

	[Test]
	public void TheorixCharm_MinusTwo_OrMillThreeThenDraw()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		Cast(Hand("Theorix Charm", me), [1], bear);
		Assert.That(s.IsIn(bear, them, ZoneType.Graveyard));

		var charm = Hand("Theorix Charm", me);
		var (library, hand) = (s.CardsIn(me, ZoneType.Library).Count(), HandCount(me));
		Cast(charm, [2]);
		Assert.That(
			(s.CardsIn(me, ZoneType.Library).Count(), HandCount(me)),
			Is.EqualTo((library - 4, hand))
		);
	}

	[Test]
	public void TwistedFates_DestroysAPermanent_AndCountersOnlyTheTargetPlayersCreatures()
	{
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		var other = Field(TestGames.Creature("Other", "2/2"), them);
		var mine = Field(TestGames.Creature("Mine", "2/2"), me);
		Cast(Hand("Twisted Fates", me), [], theirs, me);

		Assert.That(s.IsIn(theirs, them, ZoneType.Graveyard));
		Assert.That(
			(s.GetCard(mine).PlusOneCounters, s.GetCard(other).PlusOneCounters),
			Is.EqualTo((1, 0))
		);
	}

	[Test]
	public void VigorbloomCharm_HexproofAndIndestructible()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(Hand("Vigorbloom Charm", me), [0], bear);
		var card = s.GetCard(bear);
		Assert.That((s.Has(card, "Hexproof"), s.IsIndestructible(card)), Is.EqualTo((true, true)));
	}

	[Test]
	public void VigorbloomCharm_DrawAndGainThree()
	{
		var charm = Hand("Vigorbloom Charm", me);
		var (hand, life) = (HandCount(me), Life(me));
		Cast(charm, [1]);
		Assert.That((HandCount(me), Life(me)), Is.EqualTo((hand, life + 3)));
	}

	[Test]
	public void VigorbloomCharm_TheCounterLandsBeforeTheFight()
	{
		var mine = Field(TestGames.Creature("Mine", "2/2"), me);
		var theirs = Field(TestGames.Creature("Theirs", "2/3"), them);
		Cast(Hand("Vigorbloom Charm", me), [2], mine, theirs);
		Assert.That((Alive(mine), Alive(theirs)), Is.EqualTo((true, false)));
	}
}
