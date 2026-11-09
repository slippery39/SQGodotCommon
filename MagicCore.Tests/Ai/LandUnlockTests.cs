using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Sets;
using NUnit.Framework;

namespace MagicCore.Tests.Ai;

/// <summary>AI-9.1 (Shayne, 2026-11-09): a land is worth what it unlocks - a card we hold, or more cards by next turn.</summary>
public class LandUnlockTests
{
	GameState s = null!;
	int me;

	[SetUp]
	public void OurFirstMain_EmptyHands()
	{
		s = TestGames.EmptyHands(TestGames.InFirstMain());
		me = s.GetGame().StartingPlayerId;
	}

	void Put(string name, ZoneType zone, int times = 1)
	{
		for (var i = 0; i < times; i++)
			(s, _) = TestGames.Put(s, me, CardRegistry.Get(name), zone);
	}

	bool Unlocks(string land)
	{
		(s, var id) = TestGames.Put(s, me, CardRegistry.Get(land), ZoneType.Library);
		return LandUnlock.Unlocks(s, me, s.GetCard(id));
	}

	[Test]
	public void AFourthLand_UnlocksTheFourDropWeHold()
	{
		Put("Forest", ZoneType.Battlefield, 3);
		Put("Saurian Explorer", ZoneType.Hand); // {3}{G}

		Assert.That(Unlocks("Forest"), Is.True);
	}

	[Test]
	public void ASixthLand_WithNothingMoreToCast_IsSurplus()
	{
		Put("Forest", ZoneType.Battlefield, 5);
		Put("Saurian Explorer", ZoneType.Hand);

		Assert.That(Unlocks("Forest"), Is.False);
	}

	[Test]
	public void ALand_ThatLetsUsCastTwoCardsNextTurn_Unlocks()
	{
		Put("Forest", ZoneType.Battlefield, 2);
		Put("Moopsy", ZoneType.Hand, 3); // {G} each: two castable on two lands, three on three

		Assert.That(Unlocks("Forest"), Is.True);
	}

	[Test]
	public void TheColourWeLack_Unlocks_TheOneWeHave_DoesNot()
	{
		Put("Forest", ZoneType.Battlefield, 4);
		Put("Pet Sehlat", ZoneType.Hand); // {2}{W}

		Assert.That(Unlocks("Plains"), Is.True);
		Assert.That(Unlocks("Forest"), Is.False);
	}

	[Test]
	public void WithNoSpellInHand_ALandUnlocksNothing()
	{
		Put("Forest", ZoneType.Battlefield, 3);

		Assert.That(Unlocks("Forest"), Is.False);
	}

	// Review finding: on the opponent's turn only OUR next turn's land drop is left, so two held lands do not
	// together make the fourth land - the old count of two drops said they did.
	[Test]
	public void OnTheirTurn_OneLandDropIsLeft()
	{
		var them = s.OpponentOf(me); // not the active player in our first main
		for (var i = 0; i < 2; i++)
			(s, _) = TestGames.Put(s, them, CardRegistry.Get("Forest"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, CardRegistry.Get("Saurian Explorer"), ZoneType.Hand); // {3}{G}
		(s, _) = TestGames.Put(s, them, CardRegistry.Get("Forest"), ZoneType.Hand);
		(s, var id) = TestGames.Put(s, them, CardRegistry.Get("Forest"), ZoneType.Library);

		Assert.That(LandUnlock.Unlocks(s, them, s.GetCard(id)), Is.False);
	}

	// Review finding: with one drop and a Swamp held, a second land that makes the spell castable is the one to play.
	[Test]
	public void WithOneDrop_TheLandThatMakesTheSpellCastable_Unlocks()
	{
		var them = s.OpponentOf(me);
		for (var i = 0; i < 2; i++)
			(s, _) = TestGames.Put(s, them, CardRegistry.Get("Plains"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, CardRegistry.Get("Rank Rat"), ZoneType.Hand); // {1}{B}
		(s, _) = TestGames.Put(s, them, CardRegistry.Get("Plains"), ZoneType.Hand);
		(s, var id) = TestGames.Put(s, them, CardRegistry.Get("Swamp"), ZoneType.Library);

		Assert.That(LandUnlock.Unlocks(s, them, s.GetCard(id)), Is.True);
	}
}
