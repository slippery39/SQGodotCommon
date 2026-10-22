using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Ai;

public class GreedyAiTests
{
	// The AI is measured by what it achieves, not by inspecting its picks — through the strength
	// harness (AiMatch), which is also its proof that it can SEE a difference. Every deck pairing,
	// seats swapped, so a win cannot come from one deck being stronger. 90% overall AND 6 of 10 per
	// pairing: the overall bar alone let one matchup collapse unseen. An unfinished game counts as
	// half a win, so it would hide a hang — every game must finish.
	[Test]
	public void BeatsRandomPlay_InNearlyEveryGame()
	{
		var result = AiMatch.Run("greedy", "random", games: 60);
		var byPairing = result
			.Games.GroupBy(g => $"{g.DeckOfA} vs {g.DeckOfB}")
			.Select(p => (Pairing: p.Key, Wins: p.Count(g => g.Winner == 1), Games: p.Count()))
			.ToList();
		var summary = string.Join("; ", byPairing.Select(r => $"{r.Pairing} {r.Wins}/{r.Games}"));

		Assert.That(result.Games.All(g => g.Finished), result.Report());
		Assert.That(result.WinRateA, Is.GreaterThanOrEqualTo(0.9), summary);
		Assert.That(byPairing.Where(r => r.Wins * 10 < r.Games * 6), Is.Empty, summary);
	}

	// A creature saved by a pump is alive; it was once scored as dead from its PRINTED toughness.
	[Test]
	public void Evaluate_ACreatureSavedByAPump_StillCounts()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		var before = Evaluator.Evaluate(s, me);
		int bear;
		(s, bear) = TestGames.Put(s, me, TestGames.Creature("Bear", "2/2"), ZoneType.Battlefield);
		var pumped = (Card)s.GetCard(bear).WithComponent(new UntilEndOfTurn(2, 2)) with
		{
			Damage = 3,
		};
		s = s.UpdateObject(bear, pumped);

		Assert.That(Evaluator.Evaluate(s, me), Is.GreaterThan(before));
	}

	// The threat term (AI-5.2) forecasts the next attack: a pump until end of turn is gone by then, so
	// it adds no threat — at 4 life, their pumped 2/2 scores as their plain 2/2.
	[Test]
	public void Threat_APumpUntilEndOfTurn_AddsNoThreat()
	{
		var s = TestGames.EmptyHands(TestGames.InFirstMain());
		var (me, them) = (s.GetGame().StartingPlayerId, s.OpponentOf(s.GetGame().StartingPlayerId));
		s = s.WithPlayer(s.GetPlayer(me) with { Life = 4 });
		int bear;
		(s, bear) = TestGames.Put(s, them, TestGames.Creature("Bear", "2/2"), ZoneType.Battlefield);
		var plain = Evaluator.Evaluate(s, me);

		s = s.UpdateObject(bear, s.GetCard(bear).WithComponent(new UntilEndOfTurn(3, 3)));

		Assert.That(Evaluator.Evaluate(s, me), Is.EqualTo(plain).Within(1e-9));
	}

	// A creature with defender never attacks: no threat. At 4 life their 3/3 defender costs us less
	// than their plain 3/3 — only its body, not the clock.
	[Test]
	public void Threat_ADefender_IsNoClock()
	{
		var s = TestGames.EmptyHands(TestGames.InFirstMain());
		var (me, them) = (s.GetGame().StartingPlayerId, s.OpponentOf(s.GetGame().StartingPlayerId));
		s = s.WithPlayer(s.GetPlayer(me) with { Life = 4 });
		var (wall, _) = TestGames.Put(
			s,
			them,
			TestGames.Creature("Wall", "3/3", "Defender"),
			ZoneType.Battlefield
		);
		var (bear, _) = TestGames.Put(
			s,
			them,
			TestGames.Creature("Bear", "3/3"),
			ZoneType.Battlefield
		);

		Assert.That(Evaluator.Evaluate(wall, me), Is.GreaterThan(Evaluator.Evaluate(bear, me) + 5));
	}

	// AI-8.4 (Shayne's sealed game, at AI-8.3's horizon): at 3 life, our 2/3 against their 7/5 — with
	// trample the excess gets past our blocker, a lethal clock; "one blocker stops one attacker" scored
	// it as none. Without trample the 2/3 does stop it, so trample counted changes nothing there — nor
	// does trample granted until end of turn, gone before the next attack.
	[TestCase("Trample", false, true)]
	[TestCase("", false, false)]
	[TestCase("", true, false)]
	public void Threat_ATramplersExcess_GetsPastItsBlocker(
		string keyword,
		bool trampleThisTurn,
		bool aClock
	)
	{
		var s = TestGames.EmptyHands(TestGames.InFirstMain());
		var (me, them) = (s.GetGame().StartingPlayerId, s.OpponentOf(s.GetGame().StartingPlayerId));
		s = s.WithPlayer(s.GetPlayer(me) with { Life = 3 });
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Blocker", "2/3"), ZoneType.Battlefield);
		string[] keywords = keyword == "" ? [] : [keyword];
		int crusher;
		(s, crusher) = TestGames.Put(
			s,
			them,
			TestGames.Creature("Crusher", "7/5", keywords),
			ZoneType.Battlefield
		);
		if (trampleThisTurn) // TEST SETUP
			s = s.UpdateObject(
				crusher,
				s.GetCard(crusher).WithComponent(new UntilEndOfTurn(Keyword: "Trample"))
			);

		var counted = Evaluator.Evaluate(s, me);
		var ignored = Evaluator.Without(Evaluator.Terms.Trample, () => Evaluator.Evaluate(s, me));

		if (aClock)
			Assert.That(counted, Is.LessThan(ignored - 5), "a lethal clock, at 3 life");
		else
			Assert.That(counted, Is.EqualTo(ignored).Within(1e-9));
	}

	// Keywords (AI-5.3): of two otherwise equal 2/2s and the mana for one, the one with haste — once
	// haste scored nothing, the two tied and GreedyAi cast whichever came first (the plain one, here).
	[Test]
	public void Keywords_OfTwoEqualCreatures_CastsTheOneWithHaste()
	{
		var s = TestGames.EmptyHands(TestGames.InFirstMain());
		var me = s.GetGame().StartingPlayerId;
		(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Plain", "2/2"), ZoneType.Hand);
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Hasty", "2/2", "Haste"), ZoneType.Hand);

		var (_, what) = new GreedyAi(me).Decide(s);

		Assert.That(what, Does.Contain("Hasty"), what);
	}

	// Haste counts only while the creature is new (AI-5.3); `MoreKeywords` switched off, it never does —
	// the switch +nokeywords measures by.
	[Test]
	public void Keywords_HasteCountsOnlyWhileNew_AndTheSwitchTurnsItOff()
	{
		var s = TestGames.EmptyHands(TestGames.InFirstMain());
		var me = s.GetGame().StartingPlayerId;
		var before = Evaluator.Evaluate(s, me);
		int hasty;
		(s, hasty) = TestGames.Put(
			s,
			me,
			TestGames.Creature("Hasty", "2/2", "Haste"),
			ZoneType.Battlefield
		);
		var veteran = s.UpdateObject(hasty, s.GetCard(hasty) with { ControlledSinceTurn = -5 });
		var newcomer = s.UpdateObject(
			hasty,
			s.GetCard(hasty) with
			{
				ControlledSinceTurn = s.GetGame().TurnNumber,
			}
		);

		Assert.That(
			Evaluator.Evaluate(newcomer, me) - Evaluator.Evaluate(veteran, me),
			Is.EqualTo(0.8).Within(1e-9)
		);
		Assert.That(
			Evaluator.Without(Evaluator.Terms.MoreKeywords, () => Evaluator.Evaluate(newcomer, me)),
			Is.EqualTo(Evaluator.Evaluate(veteran, me)).Within(1e-9)
		);
		Assert.That(Evaluator.Evaluate(newcomer, me), Is.GreaterThan(before));
	}

	// Lethal damage doesn't kill an indestructible creature, so the AI must not score it as dead.
	[Test]
	public void Evaluate_ADamagedIndestructibleCreature_StillCounts()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		var before = Evaluator.Evaluate(s, me);
		int bear;
		(s, bear) = TestGames.Put(s, me, TestGames.Creature("Bear", "2/2"), ZoneType.Battlefield);
		var indestructible = (Card)
			s.GetCard(bear).WithComponent(new UntilEndOfTurn(Keyword: "Indestructible")) with
		{
			Damage = 5,
		};
		s = s.UpdateObject(bear, indestructible);

		Assert.That(Evaluator.Evaluate(s, me), Is.GreaterThan(before));
	}

	// At 0.8 per loyalty counter the AI valued a counter at two thirds of a card, so it never spent
	// Jace's loyalty on anything — the Jace deck's engine sat idle in AI hands.
	[Test]
	public void SpendsJacesLoyalty_WhenItHasNothingBetterToDo()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		int jace;
		(s, jace) = TestGames.Put(
			s,
			me,
			MagicCore.Sets.Fra.EmpowerJace.JaceToken,
			ZoneType.Battlefield
		);
		s = s.UpdateObject(jace, s.GetCard(jace) with { Loyalty = 6, IsToken = true });
		var ai = new GreedyAi(me);

		for (var i = 0; i < 3 && s.GetCard(jace).Loyalty == 6; i++)
			s = s.AutoPass().DecidingPlayerId() == me ? ai.Decide(s).State : s;

		Assert.That(s.GetCard(jace).Loyalty, Is.LessThan(6));
	}

	// Life was capped at 20 in the evaluation: above it, damage scored zero, so the AI declined a
	// free attack into a board of tapped creatures (docs/findings/magic-ai.md).
	[Test]
	public void AttacksPastTappedBlockers_EvenWhenTheOpponentIsAboveTwenty()
	{
		var s = TestGames.InFirstMain();
		var (me, them) = (s.GetGame().StartingPlayerId, s.OpponentOf(s.GetGame().StartingPlayerId));
		int bear,
			wall;
		(s, bear) = TestGames.Put(s, me, TestGames.Creature("Bear", "2/2"), ZoneType.Battlefield);
		(s, wall) = TestGames.Put(s, them, TestGames.Creature("Wall", "1/4"), ZoneType.Battlefield);
		s = s.UpdateObject(wall, s.GetCard(wall) with { IsTapped = true }); // TEST SETUP: it attacked last turn
		s = s.WithPlayer(s.GetPlayer(them) with { Life = 29 });

		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		var (_, what) = new GreedyAi(me).Decide(s);

		Assert.That(what, Does.EndWith(s.GetPlayer(them).Name), what);
	}

	// The threat term (AI-5.2): at 3 life, its 3/3 facing their 3/3 — attacking taps its only blocker
	// and the swing back is lethal. Without the term the evaluator saw 3 damage and nothing else, and
	// GreedyAi went face (docs/findings/magic-ai.md).
	[Test]
	public void StaysHome_WhenAttackingLeavesItDeadToTheSwingBack()
	{
		var s = TestGames.EmptyHands(TestGames.InFirstMain());
		var (me, them) = (s.GetGame().StartingPlayerId, s.OpponentOf(s.GetGame().StartingPlayerId));
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Mine", "3/3"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "3/3"), ZoneType.Battlefield);
		s = s.WithPlayer(s.GetPlayer(me) with { Life = 3 });
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is Actions.DeclareAttackerChoice);

		var (_, what) = new GreedyAi(me).Decide(s);

		Assert.That(what, Does.EndWith("Don't attack"), what);
	}

	// Lands were capped at 7 in the evaluation, so an 8th scored less in play than in hand and the
	// AI hoarded lands — no landfall, no Koth, three or four dead cards at the end of every game.
	[Test]
	public void PlaysASurplusLand()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		for (var i = 0; i < 7; i++)
			(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
		foreach (var card in s.CardsIn(me, ZoneType.Hand).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(me, ZoneType.Library)); // TEST SETUP: a hand of one land
		(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Hand);

		var (_, what) = new GreedyAi(me).Decide(s);

		Assert.That(what, Is.EqualTo("Play Forest"));
	}
}
