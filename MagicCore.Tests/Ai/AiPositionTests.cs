using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Ai;

/// <summary>
/// Bad plays reported from real games, rebuilt as positions the search AI must handle (AI-2.6). Each
/// test names who reported it and when. The real cards are used — the report is about them.
/// Each position has a CONTROL: the AI that made the bad play still makes it there, so a position
/// that stops provoking it (a setup change, a card change) fails loudly instead of passing for nothing.
/// A play from a recorded table game needs no rebuilding: keep the record (<see cref="KeptRecords"/>)
/// and start from the test the console prints.
/// </summary>
public class AiPositionTests
{
	GameState s = null!;
	int me;
	int them;

	[SetUp]
	public void OurFirstMain_EmptyHands()
	{
		s = TestGames.EmptyHands(TestGames.InFirstMain());
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
	}

	// Shayne, 2026-10-09 (AI-1): the AI cast Prophesied End ("Destroy target creature. If it wasn't
	// attacking, its controller draws a card.") on a NON-attacking Rank Rat — premium removal on a
	// 1/1, and a card for the opponent. Rebuilt: two Plains and Prophesied End against their Rat.
	GameState RankRatPosition()
	{
		var state = s;
		for (var i = 0; i < 2; i++)
			(state, _) = TestGames.Put(state, me, TestGames.Card("Plains"), ZoneType.Battlefield);
		(state, _) = TestGames.Put(state, me, FraCards.Get("Prophesied End"), ZoneType.Hand);
		(state, _) = TestGames.Put(state, them, FraCards.Get("Rank Rat"), ZoneType.Battlefield);
		return state;
	}

	/// <summary>
	/// Plays our turn and theirs — Prophesied End is an instant, so waiting for their upkeep to kill
	/// the Rat is the same bad play — and says whether the Rat ever died while NOT attacking.
	/// </summary>
	bool KillsTheRatWhileItIsNotAttacking(IAi ai)
	{
		var bad = false;
		TestGames.PlayAis(
			RankRatPosition(),
			s.GetGame().TurnNumber + 1,
			ai,
			new GreedyAi(them),
			(before, after) =>
			{
				var rat = before
					.CardsIn(them, ZoneType.Battlefield)
					.FirstOrDefault(c => c.Name == "Rank Rat");
				var gone = !after
					.CardsIn(them, ZoneType.Battlefield)
					.Any(c => c.Name == "Rank Rat");
				bad |= rat is { AttackingId: 0 } && gone;
			}
		);
		return bad;
	}

	[Test]
	public void RankRat_Control_GreedyAiStillMakesTheBadPlay() =>
		Assert.That(KillsTheRatWhileItIsNotAttacking(new GreedyAi(me)), Is.True);

	// Holding must not become hoarding: against a 5/5 the removal is worth spending.
	[Test]
	public void ABigThreat_TheSearch_SpendsItsRemoval()
	{
		var state = RankRatPosition();
		(state, _) = TestGames.Put(
			state,
			them,
			TestGames.Creature("Big", "5/5"),
			ZoneType.Battlefield
		);
		var bigDied = false;
		TestGames.PlayAis(
			state,
			s.GetGame().TurnNumber + 1,
			new BeamSearchAi(me),
			new GreedyAi(them),
			(_, after) =>
				bigDied |= !after.CardsIn(them, ZoneType.Battlefield).Any(c => c.Name == "Big")
		);

		Assert.That(bigDied);
	}

	// The A/B control: the SAME search without held values still makes the bad play (AI-2.6) — so it is
	// the held values that keep the removal in hand below, nothing else.
	[Test]
	public void RankRat_Control_ThePlainSearchStillMakesTheBadPlay() =>
		Assert.That(
			KillsTheRatWhileItIsNotAttacking(
				new BeamSearchAi(me, new BeamSearchAi.Settings().WithoutHeld())
			),
			Is.True
		);

	// The search as it plays — held cards priced by their measured value (AI-1, the default since
	// AI-1.9) — keeps Prophesied End: worth more in hand than the Rat is dead.
	[Test]
	public void RankRat_TheSearch_HoldsItsRemoval() =>
		Assert.That(KillsTheRatWhileItIsNotAttacking(new BeamSearchAi(me)), Is.False);

	// Shayne, 2026-10-22 (AI-8.3, his sealed game — kept record, move #332): at 22 life the AI would not
	// trade its Divining Duelist (4/3) for an attacking 4/4 Beast. Two causes. A look-ahead bug (AI-8.3:
	// a double striker's damage counted once, so the simulated opponent threw its 7/5 into the Duelist —
	// RolloutTests.ALookAheadBlocker_BlocksOnlyWhatItSurvives). And the threat term (AI-8.4): it let
	// one 2/3 "stop" the opponent's 7/5 trampler, so dying to it later scored as no clock. The CONTROL:
	// without trample counted, the search still declines.
	[Test]
	public void DuelistWontTrade_AtTwentyTwoLife_TradesOnceTrampleCounts()
	{
		var record = KeptRecords.Get("DuelistWontTrade");
		var s = record.Before(332);
		string Choice(Evaluator.Terms without) =>
			MoveTokens.Token(
				s,
				new BeamSearchAi(s.DecidingPlayerId(), new() { Without = without }).Decide(s)
			);
		// Blocking either Beast: every option of the Duelist's block but "don't".
		var trades = s.GetPendingChoice()!
			.Options.Where(o => o.Id != global::MagicCore.Actions.CombatOption.None)
			.Select(o => $"c{o.Id}");

		Assert.That(
			MoveTokens.Same(Choice(Evaluator.Terms.Trample), record.Moves[331].Token),
			"the control: without trample, it declines"
		);
		Assert.That(trades, Does.Contain(Choice(Evaluator.Terms.None)), "it trades");
	}
}
