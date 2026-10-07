using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Tests.Turns;

/// <summary>Planeswalker rules (CR 306, 606) on an inline test walker — no real card's text involved.</summary>
public class PlaneswalkerTests
{
	static readonly CardDefinition Walker = TestGames.Planeswalker(
		"Walker",
		3,
		new LoyaltyAbilityComponent(1, [new GainLife(Ref.You, 1)]),
		new LoyaltyAbilityComponent(-3, [new DrawCards(Ref.You, 1)])
	);

	GameState s = null!;
	int me,
		them;

	[SetUp]
	public void Board()
	{
		s = TestGames.InFirstMain();
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
		for (var i = 0; i < 4; i++)
			(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
	}

	int CastWalker()
	{
		int id;
		(s, id) = TestGames.Put(s, me, Walker, ZoneType.Hand);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = id });
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = them });
		return id;
	}

	IEnumerable<ActivateLoyaltyAction> LoyaltyMoves(int walker) =>
		s.LegalActions().OfType<ActivateLoyaltyAction>().Where(a => a.CardId == walker);

	void Activate(int walker, int abilityIndex)
	{
		s = s.Act(LoyaltyMoves(walker).First(a => a.AbilityIndex == abilityIndex));
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = them });
	}

	[Test]
	public void EntersWithItsPrintedLoyalty()
	{
		var walker = CastWalker();
		Assert.That(s.GetCard(walker).Loyalty, Is.EqualTo(s.GetCard(walker).PrintedLoyalty));
	}

	[Test]
	public void PlusAbility_AddsLoyaltyAndResolves_OncePerTurn()
	{
		var walker = CastWalker();
		var (loyalty, life) = (s.GetCard(walker).Loyalty, s.GetPlayer(me).Life);

		Activate(walker, 0);

		Assert.That(s.GetCard(walker).Loyalty, Is.EqualTo(loyalty + 1));
		Assert.That(s.GetPlayer(me).Life, Is.EqualTo(life + 1));
		Assert.That(LoyaltyMoves(walker), Is.Empty, "once per turn");

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 3 && x.GetGame().Step == Step.PreCombatMain
		);
		Assert.That(LoyaltyMoves(walker), Is.Not.Empty, "again on your next turn");
	}

	[Test]
	public void SorcerySpeedOnly_NotInUpkeep_NotOnTheOpponentsTurn()
	{
		var walker = CastWalker();
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 2 && x.GetGame().PriorityPlayerId == me
		);
		Assert.That(LoyaltyMoves(walker), Is.Empty);
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 3 && x.GetGame().Step == Step.Upkeep
		);
		Assert.That(LoyaltyMoves(walker), Is.Empty);
	}

	[Test]
	public void AMinusCost_NeedsThatMuchLoyalty_AndAtZeroItDies_TheAbilityStillResolves()
	{
		var walker = CastWalker();
		var hand = s.CardsIn(me, ZoneType.Hand).Count();
		s = s.UpdateObject(walker, s.GetCard(walker) with { Loyalty = 2 }); // TEST SETUP
		Assert.That(LoyaltyMoves(walker).Any(a => a.AbilityIndex == 1), Is.False, "−3 from 2");

		s = s.UpdateObject(walker, s.GetCard(walker) with { Loyalty = 3 });
		Activate(walker, 1);

		Assert.That(s.IsIn(walker, me, ZoneType.Graveyard));
		Assert.That(s.CardsIn(me, ZoneType.Hand).Count(), Is.EqualTo(hand + 1));
	}

	[Test]
	public void AttackingAPlaneswalker_DamageRemovesLoyalty()
	{
		int walker,
			bear;
		(s, walker) = TestGames.Put(s, them, Walker, ZoneType.Hand);
		s = s.EnterBattlefield(walker, them); // TEST SETUP: theirs, at printed loyalty
		(s, bear) = TestGames.Put(s, me, TestGames.Creature("Bear", "2/2"), ZoneType.Battlefield);
		var loyalty = s.GetCard(walker).Loyalty;

		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		Assert.That(s.GetPendingChoice()!.Options.Select(o => o.Id), Does.Contain(walker));
		s = s.Choose([walker]);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);

		Assert.That(s.GetCard(walker).Loyalty, Is.EqualTo(loyalty - 2));
		Assert.That(
			s.GetPlayer(them).Life,
			Is.EqualTo(s.GetPlayer(me).Life),
			"the player took nothing"
		);
	}
}
