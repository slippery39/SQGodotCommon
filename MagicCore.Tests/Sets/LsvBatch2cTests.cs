using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4c part 1, LSVPower — red's tier 1 made of tested pieces: each card's ability FIRES, against its PRINTED
/// text, through real casting and the real stack.
/// </summary>
public class LsvBatch2cTests : FraBoardTest
{
	static CardDefinition Lsv(string name) => CardRegistry.Get(name, "LSV");

	static CardDefinition Boon =>
		TestGames.Inline("Boon", "{1}", "Instant", null, new GainLife(Ref.You, 1));

	int Tokens(int player, string name) =>
		s.CardsIn(player, ZoneType.Battlefield).Count(c => c.IsToken && c.Name == name);

	bool InGraveyard(int card) =>
		s.CardsIn(s.GetCard(card).OwnerId, ZoneType.Graveyard).Any(c => c.Id == card);

	[Test]
	public void FlameSlash_FourToACreature()
	{
		var four = Field(TestGames.Creature("Four", "4/4"), them);
		var five = Field(TestGames.Creature("Five", "4/5"), them);
		Cast(Hand(Lsv("Flame Slash"), me), [], four);
		Cast(Hand(Lsv("Flame Slash"), me), [], five);
		Assert.That(Alive(four), Nu.False);
		Assert.That(s.GetCard(five).Damage, Nu.EqualTo(4));
	}

	[Test]
	public void Sear_FourToACreatureOrPlaneswalker()
	{
		var four = Field(TestGames.Creature("Four", "4/4"), them);
		var sear = Hand(Lsv("Sear"), me);
		Assert.That(Casts(sear).Any(c => c.Targets[0] == them), Nu.False, "not a player");
		Cast(sear, [], four);
		Assert.That(Alive(four), Nu.False);
	}

	[Test]
	public void FlametongueKavu_EntersDealingFourToACreature()
	{
		var four = Field(TestGames.Creature("Four", "4/4"), them);
		CastWithTrigger(Hand(Lsv("Flametongue Kavu"), me), [], four);
		Assert.That(Alive(four), Nu.False);
	}

	[Test]
	public void WheelOfFortune_EachPlayerDiscardsTheirHand_ThenDrawsSeven()
	{
		var mine = Hand(Boon, me);
		var theirs = Hand(Boon, them);
		Cast(Hand(Lsv("Wheel of Fortune"), me), []);
		Assert.That(InGraveyard(mine) && InGraveyard(theirs), Nu.True);
		Assert.That((HandCount(me), HandCount(them)), Nu.EqualTo((7, 7)));
	}

	[Test]
	public void FaithlessLooting_DrawTwoDiscardTwo_ThenFlashback()
	{
		var looting = Hand(Lsv("Faithless Looting"), me);
		Hand(Boon, me);
		var before = HandCount(me) - 1;
		Cast(looting, []);
		Assert.That(HandCount(me), Nu.EqualTo(before + 2 - 2));
		Assert.That(InGraveyard(looting), Nu.True);
		Assert.That(Casts(looting), Nu.Not.Empty, "flashback from the graveyard");
		Cast(looting, []);
		Assert.That(s.IsIn(looting, me, ZoneType.Exile), Nu.True, "exiled after flashback");
	}

	[Test]
	public void MineCollapse_OnYourTurn_SacrificeAMountainInstead()
	{
		var five = Field(TestGames.Creature("Five", "5/5"), them);
		var collapse = Hand(Lsv("Mine Collapse"), me);
		foreach (var land in s.CardsIn(me, ZoneType.Battlefield).ToList())
			s = s.Tap(land.Id);
		var free = Casts(collapse).Where(c => c.SacrificeId != 0).ToList();
		Assert.That(
			free.All(c => s.GetCard(c.SacrificeId).Subtypes.Contains("Mountain")),
			Nu.True,
			"only a Mountain"
		);
		var mountain = free.First(c => c.Targets[0] == five).SacrificeId;
		s = s.Act(free.First(c => c.Targets[0] == five));
		Resolve();
		Assert.That(Alive(five), Nu.False);
		Assert.That(Alive(mountain), Nu.False, "the Mountain sacrificed");
	}

	[Test]
	public void Suplex_ThreeDamage_AndExiledIfItWouldDie()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		Cast(Hand(Lsv("Suplex"), me), [0], bear);
		Assert.That(s.IsIn(bear, them, ZoneType.Exile), Nu.True);
	}

	[Test]
	public void Suplex_OrExileAnArtifact()
	{
		var trinket = Field(TestGames.Inline("Trinket", "{1}", "Artifact"), them);
		Cast(Hand(Lsv("Suplex"), me), [1], trinket);
		Assert.That(s.IsIn(trinket, them, ZoneType.Exile), Nu.True);
	}

	[Test]
	public void LegionExtruder_TwoDamage_ThenAGolemForAnotherArtifact()
	{
		var life = Life(them);
		var extruder = Hand(Lsv("Legion Extruder"), me);
		CastWithTrigger(extruder, [], them);
		Assert.That(Life(them), Nu.EqualTo(life - 2));
		var trinket = Field(TestGames.Inline("Trinket", "{1}", "Artifact"), me);
		Assert.That(Activations(extruder).All(a => a.SacrificeId != extruder), Nu.True, "another");
		s = s.Act(Activations(extruder).Single(a => a.SacrificeId == trinket));
		Resolve();
		Assert.That(Tokens(me, "Golem"), Nu.EqualTo(1));
		Assert.That(Alive(trinket), Nu.False);
	}

	[Test]
	public void SeasonedPyromancer_DiscardsTwoFirst_AnElementalPerNonland()
	{
		foreach (var held in s.CardsIn(me, ZoneType.Hand).ToList()) // a hand of exactly these two: no choice
			s = s.MoveCard(held.Id, s.ZoneId(me, ZoneType.Library));
		var pyromancer = Hand(Lsv("Seasoned Pyromancer"), me);
		var nonland = Hand(Boon, me);
		var land = Hand(TestGames.Card("Forest"), me);
		Cast(pyromancer, []);
		Assert.That(InGraveyard(nonland) && InGraveyard(land), Nu.True);
		Assert.That(HandCount(me), Nu.EqualTo(2));
		Assert.That(Tokens(me, "Elemental"), Nu.EqualTo(1), "one nonland discarded");
	}

	[Test]
	public void SeasonedPyromancer_FromTheGraveyard_TwoElementals()
	{
		int pyromancer;
		(s, pyromancer) = TestGames.Put(s, me, Lsv("Seasoned Pyromancer"), ZoneType.Graveyard);
		s = s.Act(Activations(pyromancer).Single());
		Resolve();
		Assert.That(Tokens(me, "Elemental"), Nu.EqualTo(2));
		Assert.That(s.IsIn(pyromancer, me, ZoneType.Exile), Nu.True);
	}

	[Test]
	public void Pyrogoyf_EntersDealingItsPower_CardTypesInAllGraveyards()
	{
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Bear", "2/2"), ZoneType.Graveyard);
		(s, _) = TestGames.Put(s, me, Boon, ZoneType.Graveyard);
		var life = Life(them);
		var goyf = Hand(Lsv("Pyrogoyf"), me);
		CastWithTrigger(goyf, [], them);
		Assert.That(s.Power(s.GetCard(goyf)), Nu.EqualTo(2), "creature and instant");
		Assert.That(s.Toughness(s.GetCard(goyf)), Nu.EqualTo(3));
		Assert.That(Life(them), Nu.EqualTo(life - 2));
	}

	[Test]
	public void Pyrogoyf_AnotherLhurgoyfEntering_DealsITSPower()
	{
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Bear", "2/2"), ZoneType.Graveyard);
		Field(Lsv("Pyrogoyf"), me);
		var lhurgoyf = Hand(
			new CardDefinition(
				new CardData(
					"Big Lhurgoyf",
					"normal",
					"{1}",
					1,
					"Creature — Lhurgoyf",
					null,
					"5",
					"5",
					null,
					null,
					[],
					null
				),
				[]
			),
			me
		);
		var life = Life(them);
		CastWithTrigger(lhurgoyf, [], them);
		Assert.That(Life(them), Nu.EqualTo(life - 5));
	}

	[Test]
	public void GenerousPlunderer_UpkeepTreasures_AndAttackDamagePerTheirArtifact()
	{
		var plunderer = Field(Lsv("Generous Plunderer"), me);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is YesNoChoice y && y.PlayerId == me);
		Resolve(On<YesNoChoice>(1));
		Assert.That(Tokens(me, "Treasure"), Nu.EqualTo(1));
		var theirs = s.CardsIn(them, ZoneType.Battlefield).Single(c => c.Name == "Treasure");
		Assert.That(theirs.IsTapped, Nu.True, "they create a TAPPED Treasure");
		Field(TestGames.Inline("Trinket", "{1}", "Artifact"), them);
		var life = Life(them);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice d)
			s = s.Choose([d.CreatureId == plunderer ? them : 0]);
		Resolve();
		Assert.That(Life(them), Nu.EqualTo(life - 2), "two artifacts they control");
	}
}
