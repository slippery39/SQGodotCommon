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

	// Shayne, 2026-10-20 (AI-8.1, TBL-2 — no seed saved, rebuilt by hand): the AI's 2/2 went face while
	// his Jace token at 3 loyalty, able to −3 and draw, sat unattacked. Its look-ahead never saw the
	// draw: the simulated opponent never used a planeswalker — the board-only one by its model, a
	// casting one because by the evaluator cashing the Jace for a card left it worse off. Now the
	// simulated opponent uses its best planeswalker ability every turn it can (Rollout.TheirLoyalty),
	// so a walker is judged by what it would do: worth attacking at 3 (a card denied), not at 1 or 2
	// where it can only surveil (Shayne: "attacking a Jace for 5 when it could only surveil next turn
	// anyways, probably not worth it"). The CONTROL: with its walkers idle (before AI-8.1), it goes face.
	// The two "not worth it" cases guard against over-attacking: they went face before AI-8.1 too.
	[TestCase("2/2", 3, true)]
	[TestCase("2/2", 1, false)]
	[TestCase("5/5", 2, false)]
	public void AJaceThatWouldDraw_IsAttacked_OneThatWouldOnlySurveil_IsNot(
		string attacker,
		int loyalty,
		bool attacksJace
	)
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Bear", attacker), ZoneType.Battlefield);
		int jace;
		(s, jace) = TestGames.Put(s, them, EmpowerJace.JaceToken, ZoneType.Battlefield);
		s = s.UpdateObject(jace, s.GetCard(jace) with { Loyalty = loyalty, IsToken = true }); // TEST SETUP
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is Actions.DeclareAttackerChoice);
		int Target(bool theirWalkersAct)
		{
			var settings = new BeamSearchAi.Settings(
				RolloutSettings: BeamSearchAi.DefaultRollout with
				{
					TheirWalkers = theirWalkersAct,
				}
			);
			return new BeamSearchAi(me, settings).Decide(s).Answer!.Single();
		}

		Assert.That(Target(true), Is.EqualTo(attacksJace ? jace : them));
		if (attacksJace)
			Assert.That(Target(false), Is.EqualTo(them), "the control: walkers idle, it goes face");
	}

	// Shayne, 2026-10-20 (AI-8.2, TBL-2 — no seed saved, rebuilt by hand): it surveiled Way of the
	// Wildspeaker away with the lands to cast it — "a 4/4 that draws a card": Jace +7, then −4 for a 4/4
	// Beast, then −3 to draw. Priced as loyalty alone (0.35 a counter), casting Way lost to a 2-mana
	// pump, so the look-ahead (one play a turn) never cast it, a Way on top was worth a random card,
	// and with cheap spells in hand it was binned by a hair. A walker is now priced by what its best
	// ability makes (Evaluator.Walker). The bin itself was too narrow to keep as a test (the new card
	// values alone flip it); the play that caused it is not. The CONTROL: priced by count, the pump.
	[Test]
	public void WayOfTheWildspeaker_IsCastOverAPump()
	{
		for (var i = 0; i < 5; i++)
			(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
		foreach (
			var card in new[] { "Way of the Wildspeaker", "Tam's Resistance", "No Admittance" }
		)
			(s, _) = TestGames.Put(s, me, FraCards.Get(card), ZoneType.Hand);
		(s, _) = TestGames.Put(s, me, FraCards.Get("Arcane Amphisbaena"), ZoneType.Battlefield);
		int jace;
		(s, jace) = TestGames.Put(s, me, EmpowerJace.JaceToken, ZoneType.Battlefield);
		s = s.UpdateObject(jace, s.GetCard(jace) with { Loyalty = 1, IsToken = true }); // TEST SETUP
		string Casts(Evaluator.Terms without) =>
			Evaluator.Without(
				without,
				() => new GreedyAi(me, BeamSearchAi.DefaultRollout.Held).Decide(s).What
			);

		Assert.That(
			Casts(Evaluator.Terms.WalkerUse),
			Does.StartWith("Cast Tam's Resistance"),
			"the control: priced by count, the pump"
		);
		Assert.That(Casts(Evaluator.Terms.None), Is.EqualTo("Cast Way of the Wildspeaker"));
	}
}
