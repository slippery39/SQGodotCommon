using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4h1, LSVPower — black's tested pieces, FIRING against their printed text through real casting and the
/// real stack.
/// </summary>
public class LsvBatch2hTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	/// <summary>A {3} sorcery: mana value 3.</summary>
	static CardDefinition Dear => TestGames.Inline("Dear", "{3}", "Sorcery");

	static CardDefinition Land => TestGames.Card("Swamp");

	/// <summary>I cast it with these targets, and the stack resolves, each choice answered as told.</summary>
	List<ChoiceAction> CastAt(int card, int target, Func<ChoiceAction, IEnumerable<int>> answer)
	{
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = card,
				Targets = [target],
			}
		);
		return ResolveAsking(answer);
	}

	[Test]
	public void DarkConfidant_Upkeep_RevealsTheTopIntoHand_LosesItsManaValue()
	{
		Field(Lsv("Dark Confidant"), me);
		var top = OnTop(Dear);
		var life = Life(me);
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().ActivePlayerId == me
				&& x.GetGame().TurnNumber > 1
				&& x.GetGame().Step == Step.Draw
		);
		Assert.That(s.IsIn(top, me, ZoneType.Hand));
		Assert.That(
			s.GetCard(top).KnownTo & Knowers.Opponent,
			Nu.EqualTo(Knowers.Opponent),
			"reveal"
		);
		Assert.That(Life(me), Nu.EqualTo(life - 3));
	}

	[Test]
	public void IronShieldElf_DiscardACard_IndestructibleAndTapped()
	{
		var elf = Field(Lsv("Iron-Shield Elf"), me);
		var fodder = Hand(TestCards.Fodder, me);
		s = s.Act(Activations(elf).Single(a => a.DiscardId == fodder));
		Resolve();
		Assert.That(InGraveyard(fodder));
		Assert.That(s.GetCard(elf).IsTapped);
		Cast(Hand(TestCards.Kill(), me), [], elf);
		Assert.That(Alive(elf), "indestructible until end of turn");
	}

	[Test]
	public void SunsetSaboteur_Attacks_ACounterOnTheirCreature()
	{
		var saboteur = Field(Lsv("Sunset Saboteur"), me);
		var bear = Field(Bear, them);
		AttackWith(Targeting(bear, false), saboteur);
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(1));
		Assert.That(s.GetCard(saboteur).PlusOneCounters, Nu.EqualTo(0));
	}

	[Test]
	public void HarvesterOfMisery_Enters_OtherCreaturesGetMinusTwo()
	{
		var mine = Field(Bear, me);
		var theirs = Field(Bear, them);
		var harvester = Hand(Lsv("Harvester of Misery"), me);
		Cast(harvester, []);
		Assert.That(Alive(harvester), "other creatures");
		Assert.That(Alive(mine), Nu.False);
		Assert.That(Alive(theirs), Nu.False);
	}

	[Test]
	public void HarvesterOfMisery_DiscardedFromHand_TargetGetsMinusTwo()
	{
		var bear = Field(Bear, them);
		var harvester = Hand(Lsv("Harvester of Misery"), me);
		s = s.Act(Activations(harvester).Single(a => a.Targets.Contains(bear)));
		Resolve();
		Assert.That(InGraveyard(harvester));
		Assert.That(Alive(bear), Nu.False);
	}

	[Test]
	public void ArchonOfCruelty_Enters_TheyLoseACreatureACardAndThree_IDrawAndGainThree()
	{
		var bear = Field(Bear, them);
		Hand(TestCards.Fodder, them);
		var (theirHand, theirLife, myLife) = (HandCount(them), Life(them), Life(me));
		var archon = Hand(Lsv("Archon of Cruelty"), me);
		var myHand = HandCount(me);
		Cast(archon, []);
		Assert.That(Alive(bear), Nu.False);
		Assert.That(HandCount(them), Nu.EqualTo(theirHand - 1));
		Assert.That(Life(them), Nu.EqualTo(theirLife - 3));
		Assert.That(HandCount(me), Nu.EqualTo(myHand - 1 + 1), "the Archon cast, a card drawn");
		Assert.That(Life(me), Nu.EqualTo(myLife + 3));
	}

	[Test]
	public void ArchonOfCruelty_Attacks_TriggersAgain()
	{
		var archon = Field(Lsv("Archon of Cruelty"), me);
		var theirLife = Life(them);
		AttackWith(c => c.Options.Take(c.MinChoices).Select(o => o.Id), archon);
		Assert.That(Life(them), Nu.EqualTo(theirLife - 3 - 6), "3 lost, 6 from combat");
	}

	[Test]
	public void Griselbrand_PaySevenLife_DrawSeven()
	{
		var gris = Field(Lsv("Griselbrand"), me);
		var (hand, life) = (HandCount(me), Life(me));
		s = s.Act(Activations(gris).Single());
		Resolve();
		Assert.That(HandCount(me), Nu.EqualTo(hand + 7));
		Assert.That(Life(me), Nu.EqualTo(life - 7));
	}

	[Test]
	public void Grief_Enters_IChooseTheirNonlandCard()
	{
		var spell = Hand(TestCards.Fodder, them);
		var land = Hand(Land, them);
		var asked = CastAsking(Hand(Lsv("Grief"), me), On<ChooseCardsFromHandChoice>(spell));
		Assert.That(Offered<ChooseCardsFromHandChoice>(asked), Nu.Not.Contains(land), "nonland");
		Assert.That(InGraveyard(spell));
	}

	[Test]
	public void Entomb_ACardIntoMyGraveyard()
	{
		var card = Put(Dear, me, ZoneType.Library);
		CastAsking(Hand(Lsv("Entomb"), me), On<ChooseFromLibraryChoice>(card));
		Assert.That(InGraveyard(card));
	}

	[Test]
	public void DemonicTutor_ACardIntoMyHand()
	{
		var card = Put(Dear, me, ZoneType.Library);
		CastAsking(Hand(Lsv("Demonic Tutor"), me), On<ChooseFromLibraryChoice>(card));
		Assert.That(s.IsIn(card, me, ZoneType.Hand));
	}

	[TestCase("Vampiric Tutor")]
	[TestCase("Imperial Seal")]
	public void Seal_ACardOnTop_LoseTwo(string name)
	{
		var card = Put(Dear, me, ZoneType.Library);
		var life = Life(me);
		CastAsking(Hand(Lsv(name), me), On<ChooseFromLibraryChoice>(card));
		Assert.That(Library(me).First(), Nu.EqualTo(card));
		Assert.That(Life(me), Nu.EqualTo(life - 2));
	}

	[TestCase(0, false)] // a nontoken creature
	[TestCase(1, true)] // a creature token: the Bear isn't one
	public void SheoldredsEdict_EachOpponentSacrifices(int mode, bool survives)
	{
		var bear = Field(Bear, them);
		Cast(Hand(Lsv("Sheoldred's Edict"), me), [mode]);
		Assert.That(Alive(bear), Nu.EqualTo(survives));
	}

	[Test]
	public void Dismember_MinusFive()
	{
		var big = Field(TestGames.Creature("Big", "5/5"), them);
		Cast(Hand(Lsv("Dismember"), me), [], big);
		Assert.That(Alive(big), Nu.False);
	}

	[Test]
	public void Thoughtseize_TheirNonlandCard_LoseTwo()
	{
		var spell = Hand(TestCards.Fodder, them);
		var land = Hand(Land, them);
		var life = Life(me);
		var asked = CastAt(
			Hand(Lsv("Thoughtseize"), me),
			them,
			On<ChooseCardsFromHandChoice>(spell)
		);
		Assert.That(Offered<ChooseCardsFromHandChoice>(asked), Nu.Not.Contains(land), "nonland");
		Assert.That(InGraveyard(spell));
		Assert.That(Life(me), Nu.EqualTo(life - 2));
	}

	[Test]
	public void HymnToTourach_TwoAtRandom()
	{
		for (var i = 0; i < 3; i++)
			Hand(TestCards.Fodder, them);
		var hand = HandCount(them);
		Cast(Hand(Lsv("Hymn to Tourach"), me), [], them);
		Assert.That(HandCount(them), Nu.EqualTo(hand - 2));
	}

	[Test]
	public void Reanimate_TheirCreatureMine_LoseItsManaValue()
	{
		var dead = Put(TestGames.CreatureCosting("{3}", "Dead", "3/3"), them, ZoneType.Graveyard);
		var life = Life(me);
		Cast(Hand(Lsv("Reanimate"), me), [], dead);
		Assert.That(s.IsOnBattlefield(dead));
		Assert.That(s.GetCard(dead).ControllerId, Nu.EqualTo(me), "under your control");
		Assert.That(Life(me), Nu.EqualTo(life - 3));
	}

	[Test]
	public void Unearth_ManaValueThreeOrLess_AndCycling()
	{
		var small = Put(TestGames.CreatureCosting("{3}", "Small", "3/3"), me, ZoneType.Graveyard);
		var big = Put(TestGames.CreatureCosting("{4}", "Big", "4/4"), me, ZoneType.Graveyard);
		var unearth = Hand(Lsv("Unearth"), me);
		Assert.That(Casts(unearth).Any(c => c.Targets.Contains(big)), Nu.False, "3 or less");
		Assert.That(Activations(unearth).Count(), Nu.EqualTo(1), "cycling {2}");
		Cast(unearth, [], small);
		Assert.That(Alive(small));
	}
}
