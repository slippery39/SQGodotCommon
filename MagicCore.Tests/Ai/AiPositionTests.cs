using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Sets;
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

	// Shayne, 2026-11-08 (AI-17.10): turn 3, the AI blocked his Astute Terraformer with its own; he pumped
	// his ({6} less one per differently named land: three lands, {3}) and killed theirs for free. The
	// search imagined the trade, never the pump: the simulated opponent had no priority in its OWN combat.
	// The CONTROL: without TheirCombatTricks the AI still blocks (c15).
	[Test]
	public void ABlock_ThatLosesToTheirOpenManaPump_IsNotMade()
	{
		var s = KeptRecords.Before("TerraformerBlock", 92);
		string Choice(Evaluator.Terms without) =>
			MoveTokens.Token(
				s,
				new BeamSearchAi(s.DecidingPlayerId(), new(Without: without)).Decide(s)
			);

		Assert.That(MoveTokens.Same(Choice(Evaluator.Terms.TheirCombatTricks), "c15"), "control");
		Assert.That(MoveTokens.Same(Choice(Evaluator.Terms.None), "c15"), Is.False);
	}

	// Shayne, 2026-11-08 (AI-19.21): General Chang ("{1}, Sacrifice a creature: a +1/+1 counter on each
	// Klingon"). The AI sacrificed a 4/4 and a 2/1 to make his 1/2 a 3/4 at sorcery speed, nothing dying, and
	// at #458 Chang himself. Not the evaluator: it charged the creatures (-15 at once). The simulated HUMAN
	// judged his attack against the look-ahead's weak defender, then met the real blocks and threw four
	// creatures away - and a sacrifice that baited that attack scored +25 (Terms.TrueDefender judges it against
	// the blocks the rollout plays). The CONTROL: without it the AI still sacrifices.
	[TestCase("ChangSac305", 305, "a5")]
	[TestCase("ChangSac309", 309, "a3", true)] // flat prices: priced (AI-17.15) Pass and the sacrifice tie exactly
	[TestCase("ChangSac458", 458, "a3")]
	public void ASacrifice_ThatOnlyBaitsTheSimulatedAttack_IsNotMade(
		string record,
		int at,
		string token,
		bool flat = false
	)
	{
		var s = KeptRecords.Before(record, at);
		string Choice(Evaluator.Terms without) =>
			MoveTokens.Token(
				s,
				new BeamSearchAi(
					s.DecidingPlayerId(),
					flat
						? new BeamSearchAi.Settings(Without: without).WithoutHeld()
						: new(Without: without)
				).Decide(s)
			);

		Assert.That(MoveTokens.Same(Choice(Evaluator.Terms.TrueDefender), token), "control");
		Assert.That(MoveTokens.Same(Choice(Evaluator.Terms.None), token), Is.False);
	}

	// Shayne, 2026-11-08 (AI-17.11): Dominion Assault Force ("choose one - exile target card from a graveyard;
	// surveil 1"). The AI exiled its OWN Infinite Diversity and Shayne's Tal Shiar Operative: "surveiling here is
	// 100% the correct play". Not a judgement - a TIE: the rollout's one sampled top card is kept, so surveil gained
	// nothing, exiling a card with no graveyard value cost nothing, and the first listed mode won. A surveil on the
	// stack now counts a third of a card (Terms.SurveilPrice). The CONTROL: without it the AI still exiles.
	[TestCase("DominionExilesOwnCard", 127)]
	[TestCase("DominionExilesTheirCard", 294)]
	public void DominionAssaultForce_Surveils_RatherThanExilingACardWorthNothing(
		string record,
		int at
	)
	{
		var s = KeptRecords.Before(record, at);
		string Chose(Evaluator.Terms without) =>
			new BeamSearchAi(s.DecidingPlayerId(), new(Without: without)).Decide(s).What;

		Assert.That(Chose(Evaluator.Terms.SurveilPrice), Does.Contain("Exile"), "control");
		Assert.That(Chose(Evaluator.Terms.None), Does.Contain("Surveil"));
	}

	// Shayne, 2026-11-08 (AI-17.14): Amok Time ("target creature you control fights up to one target creature you
	// don't control") with his 4/4 out against 3/1s, a 2/2 and a 3/2 - and the second target chosen as NONE, a
	// free kill left. The simulated human, attacking, met the real blocks only when his attack triggered no choice:
	// the Tal Shiar Operative's "choose one" left a pending choice and the true defender (AI-19.21) silently fell
	// back to the weak one, so he looked to win a bloodbath - and a stall after a kill scored below it. Blocked now
	// drives the combat through such choices. The CONTROL: with TrueDefender off the AI still chooses none.
	[Test]
	public void AmokTime_FightsSomething_RatherThanNothing()
	{
		var s = KeptRecords.Before("AmokTimeNoSecondTarget", 380);
		string Chose(Evaluator.Terms without) =>
			new BeamSearchAi(s.DecidingPlayerId(), new(Without: without)).Decide(s).What;

		Assert.That(Chose(Evaluator.Terms.TrueDefender), Does.EndWith("none"), "control");
		Assert.That(Chose(Evaluator.Terms.None), Does.Not.EndWith("none"));
	}

	// Shayne, 2026-11-08 (AI-13.7): the AI's first turn, three Forests in hand - and it passed, then discarded to
	// hand size. Pass -15.78 against Play Forest -21.63: the rollout scored the discarded Photosynthetic
	// Physician's return from the graveyard (an Officer by turn 4) above the land's flat price. Terms.LandDrop: the
	// search never passes with a land to play. The CONTROL: without it, it still passes.
	[Test]
	public void WithALandToPlay_TheSearchPlaysIt_NotPass()
	{
		var s = KeptRecords.Before("AiSkipsLandDrop", 27);
		string Chose(Evaluator.Terms without, bool flat = false) =>
			new BeamSearchAi(
				s.DecidingPlayerId(),
				flat
					? new BeamSearchAi.Settings(Without: without).WithoutHeld()
					: new(Without: without)
			)
				.Decide(s)
				.What;

		// the position is a Star Trek game: the control is in the flat prices it was played with (AI-17.15 priced TRK)
		Assert.That(Chose(Evaluator.Terms.LandDrop, flat: true), Is.EqualTo("Pass"), "control");
		Assert.That(Chose(Evaluator.Terms.None), Does.StartWith("Play"));
		Assert.That(Chose(Evaluator.Terms.None, flat: true), Does.StartWith("Play"));
	}

	// Shayne, 2026-11-09 (AI-17.21): on 2 lands, no land in hand, Free Borg Revolutionaries ({3}{W}{W}, basic
	// landcycling {2}) held - the AI passed its own turn 5, landcycled in his upkeep and played the land a turn late.
	// The 5-drop held was priced as castable (6.9) against a land; Terms.ManaShort discounts it per land short past the first. "Its not
	// worth keeping over a land when you have only 2 lands in play." The AI plays its turn out: a land is played in it.
	// The CONTROL: without the term the turn ends with no land played.
	[Test]
	public void ALandcycler_OnTwoLands_IsCycledAndTheLandPlayed_ThisTurn()
	{
		var start = KeptRecords.Before("BorgLandcycleHeld", 88);
		var ai = start.DecidingPlayerId();
		int LandsPlayed(Evaluator.Terms without)
		{
			var (s, search) = (start, new BeamSearchAi(ai, new(Without: without)));
			for (var i = 0; i < 200 && s.GetGame().TurnNumber == 5; i++)
				s = s.DecidingPlayerId() == ai ? search.Decide(s).State : TestGames.PassOrChoose(s);
			return s.GetGame().TurnNumber == 5
				? throw new InvalidOperationException("turn 5 never ended")
				: s.GetPlayer(ai).LastTurn.LandsPlayed;
		}

		Assert.That(LandsPlayed(Evaluator.Terms.ManaShort), Is.EqualTo(0), "control");
		Assert.That(LandsPlayed(Evaluator.Terms.None), Is.EqualTo(1));
	}

	// Shayne, 2026-11-09 (AI-17.22): "trying to race me the whole game. He never kept his guy back to block. My 2/1
	// eventually killed him." Turn 12, the AI at 4 and Shayne at 7 with a 2/1 and Artificial Anomaly in play ("{3}{B},
	// {T}, Sacrifice this land: Each opponent loses 2 life") - the AI attacked with its Planetary Patrol and died to
	// the 2/1 plus the drain. The look-ahead's opponent never used an ability in its own turn, so the AI counted a
	// turn it didn't have. Terms.TheirAbilities: it does. The CONTROL: without the term the AI still attacks.
	[Test]
	public void AtFourLife_AgainstAVisibleDrain_ThePatrolStaysHomeToBlock()
	{
		var s = KeptRecords.Before("PatrolRacesIntoDrain", 287);
		string Chose(Evaluator.Terms without) =>
			new BeamSearchAi(s.DecidingPlayerId(), new(Without: without)).Decide(s).What;

		Assert.That(Chose(Evaluator.Terms.TheirAbilities), Does.EndWith("Attack You"), "control");
		Assert.That(Chose(Evaluator.Terms.None), Does.EndWith("Don't attack"));
	}

	// Shayne, 2026-11-08 (AI-17.12): turn 13, the AI attacked its Officer 1/1 token into Shayne's untapped Saurian
	// Explorer 4/3 with no trick, let it die, then cast a 3/3. The look-ahead's defender never blocked it, so the
	// attack looked free; judged against the blocks the rollout plays (Terms.TrueDefender, AI-19.21) the Explorer
	// blocks and the Officer stays home. Fixed by that term, found by switching it off. The CONTROL: without it the
	// AI still attacks.
	[Test]
	public void AOneOneToken_DoesNotAttackIntoAnUntappedFourThree()
	{
		var s = KeptRecords.Before("OfficerAttacksIntoExplorer", 264);
		string Chose(Evaluator.Terms without) =>
			new BeamSearchAi(s.DecidingPlayerId(), new(Without: without)).Decide(s).What;

		Assert.That(Chose(Evaluator.Terms.TrueDefender), Does.EndWith("Attack You"), "control");
		Assert.That(Chose(Evaluator.Terms.None), Does.EndWith("Don't attack"));
	}

	// Shayne, 2026-11-09 (AI-17.17): the AI put 17 charge counters on its Sarcophagus, Ship of the Dead, which animates at
	// 10. A Spacecraft's value grew with charge / threshold without end, so tapping a creature for one more counter looked
	// like value. Terms.StationCap counts it only up to the threshold. The CONTROL: without it the AI still stations.
	[Test]
	public void ASpacecraftPastItsThreshold_IsNotChargedFurther()
	{
		var s = KeptRecords.Before("SarcophagusOvercharge", 445);
		string Chose(Evaluator.Terms without) =>
			new BeamSearchAi(s.DecidingPlayerId(), new(Without: without)).Decide(s).What;

		Assert.That(
			Chose(Evaluator.Terms.StationCap),
			Does.StartWith("Activate Sarcophagus"),
			"control"
		);
		Assert.That(Chose(Evaluator.Terms.None), Does.Not.StartWith("Activate Sarcophagus"));
	}

	// Shayne, 2026-11-09 (AI-17.16): turn 6, three lands, the land already played - the AI attacked its Tasha Yar 2/1
	// ("when it dies, exile the top card, you may play it") into his untapped 1/3, then exiled her from the graveyard for a
	// copy that exiles another: a 2/1 and three mana for cards it could not use. The search treats its ONE sampled library
	// as certain, so an exile looks good whenever the sample is kind (attack in 10 of 30 shuffles, mean margin -1.7).
	// Shayne: "it shouldn't matter what the exact top card is" - Terms.ImpulseValue credits a random card (0.8, fading) and
	// never plays the sample's. Over 30 shuffles: 10 attack without it, 1 with it (mean margin -6.2).
	[Test]
	public void TheTashaAttack_DoesNotHangOnTheSampledTopCard()
	{
		var s = KeptRecords.Before("TashaAttacksIntoCrew", 114);
		int Attacks(Evaluator.Terms without)
		{
			var attacked = 0;
			Parallel.For(
				1,
				31,
				k =>
				{
					var ai = new BeamSearchAi(
						s.DecidingPlayerId(),
						new(Without: without, Threads: 1)
					);
					if (ai.Decide(s with { RngSeed = k * 7919 }).What.EndsWith("Attack You"))
						Interlocked.Increment(ref attacked);
				}
			);
			return attacked;
		}

		Assert.That(
			Attacks(Evaluator.Terms.ImpulseValue),
			Is.GreaterThanOrEqualTo(6),
			"control: a kind sample attacks"
		);
		Assert.That(Attacks(Evaluator.Terms.None), Is.LessThanOrEqualTo(3));
	}

	// Shayne, 2026-11-09 (AI-17.20): turn 8, Emergency Holographic Crew's surveil 2 over a Mountain and an Energize, with
	// four lands in play and a hand of expensive spells - and the AI put nothing in the graveyard, as it never does
	// (the four answers tied or ranked the land ahead: the rollout keeps one sampled top card). Shayne: keep a land on
	// top only if it unlocks a card in hand or lets us do more next turn; otherwise bin it. Terms.SurveilLands
	// (`LandUnlock.Surveil`). The CONTROL: without it the AI still answers none.
	[Test]
	public void ASurveil_BinsALandThatUnlocksNothing()
	{
		var s = KeptRecords.Before("SurveilNone", 173);
		string Chose(Evaluator.Terms without, bool flat = false) =>
			new BeamSearchAi(
				s.DecidingPlayerId(),
				flat
					? new BeamSearchAi.Settings(Without: without).WithoutHeld()
					: new(Without: without)
			)
				.Decide(s)
				.What;

		// a Star Trek position: the control is in the flat prices it was played with (AI-17.15 priced TRK, and the
		// priced Energize alone now moves the answer; the rule decides it either way)
		Assert.That(
			Chose(Evaluator.Terms.SurveilLands, flat: true),
			Does.EndWith("none"),
			"control"
		);
		Assert.That(Chose(Evaluator.Terms.None), Does.Contain("Mountain"));
		Assert.That(Chose(Evaluator.Terms.None, flat: true), Does.Contain("Mountain"));
	}

	// Shayne, 2026-11-08 (AI-17.13): the AI cast Guidance Failure main phase on Raffi Musiker and then did not
	// attack with its Spanner Jockey and Dahar Master - "the worst play possible": cast it to be aggressive, or
	// don't. NOT reproduced: the search of that day, today's, with plans kept or not, and caps from 10 ms to
	// 1 s all attack (the record's `Don't attack`, #187-188, came from the live game). Kept as it plays now.
	[Test]
	public void AfterGuidanceFailure_TheAiAttacks_NotTheWorstOfBoth()
	{
		var s = KeptRecords.Before("GuidanceNoAttack", 187);
		var chose = new BeamSearchAi(s.DecidingPlayerId(), BeamSearchAi.Settings.Interactive)
			.Decide(s)
			.What;

		Assert.That(chose, Does.StartWith("Attack with").And.EndsWith("Attack You"));
	}

	// Shayne, 2026-10-09 (AI-1): the AI cast Prophesied End ("Destroy target creature. If it wasn't
	// attacking, its controller draws a card.") on a NON-attacking Rank Rat — premium removal on a
	// 1/1, and a card for the opponent. Rebuilt: two Plains and Prophesied End against their Rat.
	GameState RankRatPosition()
	{
		var state = s;
		for (var i = 0; i < 2; i++)
			(state, _) = TestGames.Put(state, me, TestGames.Card("Plains"), ZoneType.Battlefield);
		(state, _) = TestGames.Put(state, me, CardRegistry.Get("Prophesied End"), ZoneType.Hand);
		(state, _) = TestGames.Put(state, them, CardRegistry.Get("Rank Rat"), ZoneType.Battlefield);
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

	// Shayne, 2026-10-23 (AI-10, his sealed game — kept record, moves #545 turn 22 and #587 turn 24): the
	// AI never attacked with Gallia, Tragic Host (2/1 menace) though one creature at most could block
	// her and he was at 5 life. Attacking and staying home both scored 10,000 × 0.95²: the board-only
	// look-ahead's full attack next turn wins either way, and the tie picked "Don't attack". Now a won
	// line counts the opponent's life lost this turn (Rollout.Settings.Progress, Shayne's tie-break —
	// since AI-16 inside the turn's whole evaluation, ProgressBy.TurnEnd; she attacks under both).
	// The CONTROL: without it, the search stays home, as it did in the game.
	[TestCase(545)]
	[TestCase(587)]
	public void Gallia_AttacksWhenTheGameIsWon_ProgressThisTurn(int move)
	{
		var record = KeptRecords.Get("GalliaWontAttack");
		var s = record.Before(move);
		string Choice(Rollout.ProgressBy progress, bool wholeAttacks = true) =>
			MoveTokens.Token(
				s,
				new BeamSearchAi(
					s.DecidingPlayerId(),
					new(
						RolloutSettings: BeamSearchAi.DefaultRollout with
						{
							Progress = progress,
						},
						WholeAttacks: wholeAttacks
					)
				).Decide(s)
			);

		Assert.That(
			// whole attacks (AI-17.3) attack here too — "everyone" leads a tie — so the search as it was
			MoveTokens.Same(
				Choice(Rollout.ProgressBy.None, wholeAttacks: false),
				record.Moves[move - 1].Token
			),
			"the control: it stays home"
		);
		// Gallia's declaration is the pending choice: every option but "don't" attacks with her.
		var attacks = s.GetPendingChoice()!
			.Options.Where(o => o.Id != global::MagicCore.Actions.CombatOption.None)
			.Select(o => $"c{o.Id}");
		Assert.That(attacks, Does.Contain(Choice(Rollout.ProgressBy.TurnEnd)), "she attacks");
		Assert.That(
			attacks,
			Does.Contain(Choice(Rollout.ProgressBy.TheirLife)),
			"AI-10's own rule"
		);
	}

	// Claude, 2026-10-29 (AI-14's replay, game 2 seed 1, flag 6; Shayne: "a huge bug"): Red-Green, White-Blue
	// at 9 and dead next turn on every line, cast No Admittance at ITS OWN FACE (20 → 17). Every line that
	// burns a face or a Jace wins on turn 15, and a won line counted only their life as the turn ended:
	// burned at their face they blocked everything and stayed at 6, burned at ours they took 6 (9 → 3) —
	// so the self-burn "pressed harder", and our 3 life counted for nothing. Now a won line counts the
	// turn's whole evaluation (Rollout.ProgressBy.TurnEnd, AI-16). The CONTROL: by their life alone, the
	// recorded self-burn.
	[Test]
	public void AWonLine_DoesntBurnOurOwnFace()
	{
		var record = KeptRecords.Get("NoAdmittanceAtOwnFace");
		var s = record.Before(321);
		var selfBurn = record.Moves[320].Token;
		string Choice(
			Rollout.ProgressBy progress,
			Evaluator.Terms without = Evaluator.Terms.None
		) =>
			MoveTokens.Token(
				s,
				new BeamSearchAi(
					s.DecidingPlayerId(),
					new(
						RolloutSettings: BeamSearchAi.DefaultRollout with
						{
							Progress = progress,
							// the control is the look-ahead as it was: whole attacks there (AI-17.6) avoid it too
							Attacks =
								without == Evaluator.Terms.None
									? Rollout.Attacks.AsOne
									: Rollout.Attacks.EachAlone,
						},
						Without: without,
						// the control is the search as it was: whole attacks (AI-17.3) avoid it too
						WholeAttacks: without == Evaluator.Terms.None
					)
				).Decide(s)
			);

		Assert.That(
			MoveTokens.Describe(s, selfBurn),
			Does.EndWith("→ " + s.GetPlayer(s.DecidingPlayerId()).Name),
			"the recorded move is the self-burn"
		);
		Assert.That(
			// the look-ahead's defender chump-blocking (AI-17.3) and a finished game's board (AI-17.6) avoid
			// it too, so the search as it was
			MoveTokens.Same(
				Choice(
					Rollout.ProgressBy.TheirLife,
					Evaluator.Terms.ChumpBlocks
						| Evaluator.Terms.EndBoard
						| Evaluator.Terms.LowLifeRisk
				),
				selfBurn
			),
			"the control: by their life alone, our own face"
		);
		Assert.That(MoveTokens.Same(Choice(Rollout.ProgressBy.TurnEnd), selfBurn), Is.False);
	}

	// Shayne, 2026-10-29 (AI-17.3, his draft game, seed 636312, turn 21): at 3 life with one blocker, the
	// AI attacked with Chandra's Emberling alone and kept Tether Technician home — both attacking was
	// lethal. Both root lines won by turn 23, and any win stopped the search: two rollouts, a tie, "don't
	// attack". Three fixes, each enough here alone: only a win this turn stops the search early
	// (BeamSearchAi.Settings.WinNowOnly), so it reaches the Emberling's declaration; the look-ahead's
	// defender chumps when facing lethal (Evaluator.Terms.ChumpBlocks), so the Technician alone no longer
	// looks lethal; and the attack is chosen whole (BeamSearchAi.Settings.WholeAttacks), "everyone" among
	// its candidates.
	// The CONTROL: none — the Technician stays home, as recorded.
	[TestCase(true, true, true)]
	[TestCase(true, false, false)]
	[TestCase(false, true, false)]
	[TestCase(false, false, true)]
	public void ALethalAttack_IsMade_NotPostponedForALaterWin(
		bool winNowOnly,
		bool chumps,
		bool asOne
	)
	{
		var record = KeptRecords.Get("TechnicianSkipsLethal");
		var s = record.Before(494);
		var me = s.DecidingPlayerId();
		BeamSearchAi Ai(bool fixes) =>
			new(
				me,
				fixes
					? new(
						WinNowOnly: winNowOnly,
						Without: chumps ? Evaluator.Terms.None : Evaluator.Terms.ChumpBlocks,
						WholeAttacks: asOne
					)
					: new(
						WinNowOnly: false,
						Without: Evaluator.Terms.ChumpBlocks,
						WholeAttacks: false
					)
			);

		Assert.That(
			MoveTokens.Same(
				MoveTokens.Token(s, Ai(fixes: false).Decide(s)),
				record.Moves[493].Token
			),
			"the control: it stays home"
		);
		var ai = Ai(fixes: true); // one AI for the declaration, as the table plays: a whole attack is answered whole
		var technician = ai.Decide(s);
		Assert.That(technician.What, Does.EndWith("Attack You"));
		var emberling = ai.Decide(technician.State);
		Assert.That(emberling.What, Does.EndWith("Attack You"), "the Emberling attacks too");
		Assert.That(
			Lookahead.Settle(emberling.State).WinnerId(),
			Is.EqualTo(me),
			"and it is lethal"
		);
	}

	// The same game (AI-17.3): the look-ahead's cheap defender never chump-blocked, so a lone attacker at a
	// player on 3 life looked lethal and a second added nothing. Both attacking, Shayne's Skilled
	// Battlecarver now chumps the bigger one, Tether Technician (Evaluator.Terms.ChumpBlocks). The CONTROL:
	// without it, no block.
	[Test]
	public void TheLookAheadsDefender_Chumps_WhenFacingLethal()
	{
		var record = KeptRecords.Get("TechnicianSkipsLethal");
		var s = record.Before(494);
		static GameState AttackWith(GameState at) =>
			at.Choose([at.GetPendingChoice()!.Options.First(o => o.Id != CombatOption.None).Id]);
		var block = AttackWith(AttackWith(s)).AutoPass();
		while (block.GetPendingChoice() is not DeclareBlockerChoice)
			block = block
				.Act(new PassPriorityAction { PlayerId = block.DecidingPlayerId() })
				.AutoPass();
		var choice = block.GetPendingChoice()!;
		var technician = global::MagicCore
			.Turns.Combat.Attackers(block)
			.Single(a => a.Name == "Tether Technician")
			.Id;

		Assert.That(
			Evaluator.Without(
				Evaluator.Terms.ChumpBlocks,
				() => Lookahead.QuickAnswer(block, choice)
			),
			Is.EqualTo(new[] { CombatOption.None }),
			"the control: no chump"
		);
		Assert.That(Lookahead.QuickAnswer(block, choice), Is.EqualTo(new[] { technician }));
	}

	// Shayne, 2026-10-29 (AI-17.2, his draft game, seed 535739, turn 11): the AI made a Jace with Way of
	// the Deathbringer and drew a card (−3) where "−2: you may sacrifice a creature. If you do, create a
	// 4/4" with Blessed Ghoul out "would (theoretically have stopped all my creatures)". The look-ahead
	// answered every "you may" with nothing, so the −2 was "−2 loyalty for nothing" — and the plan made as
	// Way of the Deathbringer was cast (#257) drew, replayed at #262 without a search. Now a "you may" is
	// answered with its chooser's best (Rollout.Settings.Mays). Played as the table plays — one AI from
	// #257, its plan and all. Shayne's whole answer (2026-10-30)
	// is the 4/4 from Blessed Ghoul — the test after this one.
	[Test]
	public void AYouMay_IsTakenInTheLookAhead_TheDeathbringersBeast()
	{
		var record = KeptRecords.Get("DeathbringerDrawsNotABeast");
		var (before, _) = record.Replay();
		var me = before[256].DecidingPlayerId();
		Assert.That(before[261].DecidingPlayerId(), Is.EqualTo(me), "#262 is the AI's");
		// Its CONTROL ("you may" declined, it drew at #262) was dropped 2026-11-07 (AI-9.3, Shayne): with walkers priced
		// over their lifetime, that old look-ahead no longer casts Way of the Deathbringer at #257 at all.
		// Since AI-17.6 it goes to combat at #262, attacks with Tinybones, then −2 — the 4/4 made this turn.
		Assert.That(
			DeathbringerTurn(record, before, me)
				.CardsIn(me, ZoneType.Battlefield)
				.Select(c => c.Name),
			Does.Contain("Beast"),
			"the 4/4"
		);
	}

	/// <summary>The Deathbringer's turn played as the table plays it — one AI from #257 — then on from #262 against GreedyAi.</summary>
	static GameState DeathbringerTurn(
		GameRecord record,
		IReadOnlyList<GameState> before,
		int me,
		BeamSearchAi.Settings? settings = null
	)
	{
		var ai = new BeamSearchAi(me, settings);
		var s = before[256];
		for (var n = 257; n <= 262; n++)
			if ((s = before[n - 1]).DecidingPlayerId() == me)
				s = ai.Decide(s).State;
		return TestGames.PlayAis(s, s.GetGame().TurnNumber, ai, new GreedyAi(s.OpponentOf(me)));
	}

	// Shayne, 2026-10-30: "the idea is that we can recur the blessed ghoul from the graveyard, so sacrificing
	// the ghoul means we can just get the ghoul back next turn and potentially do it again" — and "having the
	// 2/1 tinybones and a 4/4 beast is better than having a 1/1 that you can recur from the graveyard and a
	// beast token". Right after the −2 the evaluator agreed (Ghoul −23.38, Tinybones −25.23); the imagined
	// turns flipped it: keeping the Ghoul, the AI at 6 swung its Beast at him on 15 — and the look-ahead
	// stopped at the start of his turn, before his swing back took it to 1 (the next one killing it). His
	// rule: "always account for a counter attack from an opponent whenever we are choosing attackers
	// ourselves" (Rollout.Settings.CounterAttack). The CONTROL: the look-ahead stopping after our turn.
	[TestCase(true, ExpectedResult = true)]
	[TestCase(false, ExpectedResult = false)]
	public bool TheDeathbringersBeast_IsMadeFromBlessedGhoul_WhichComesBack(bool counterAttack)
	{
		var record = KeptRecords.Get("DeathbringerDrawsNotABeast");
		var (before, _) = record.Replay();
		var me = before[256].DecidingPlayerId();

		return DeathbringerTurn(
				record,
				before,
				me,
				new()
				{
					// the control also turns off TrueDefender (AI-19.21), which makes the Beast unaided
					Without = counterAttack ? Evaluator.Terms.None : Evaluator.Terms.TrueDefender,
					RolloutSettings = BeamSearchAi.DefaultRollout with
					{
						CounterAttack = counterAttack,
					},
				}
			)
				is var after
			&& after.CardsIn(me, ZoneType.Battlefield).Any(c => c.Name == "Beast")
			&& after.CardsIn(me, ZoneType.Battlefield).Any(c => c.Name.StartsWith("Tinybones"))
			&& after.CardsIn(me, ZoneType.Graveyard).Any(c => c.Name == "Blessed Ghoul");
	}

	// Shayne, 2026-10-29 (AI-16.4): "If the AI can only find losing lines, the AI should still try to
	// maximize their board score" — a later loss first ("it gives your opponent the chance to make a
	// mistake"), then the best board as the searched turn ends. Every lost line on one turn used to tie.
	// Kept from an AI game played WITH the rule (seed 11's game 8, #548, turn 21: Red-Green, lost on
	// every line, passed). The CONTROL: lost lines tied (Rollout.Settings.LostLines off), it casts
	// Ajani's Anguish for X = 0 — a card for nothing.
	[Test]
	public void ALostLine_KeepsTheBoard_NotACardThrownAway()
	{
		var record = KeptRecords.Get("LostLineAnguishForNothing");
		var s = record.Before(548);
		string Choice(bool lostLines) =>
			MoveTokens.Token(
				s,
				new BeamSearchAi(
					s.DecidingPlayerId(),
					new(
						RolloutSettings: BeamSearchAi.DefaultRollout with
						{
							LostLines = lostLines,
						},
						// the control is the search as it was: a finished game's own board (AI-17.6) keeps it too
						Without: lostLines ? Evaluator.Terms.None : Evaluator.Terms.EndBoard
					)
				).Decide(s)
			);

		Assert.That(
			MoveTokens.Describe(s, Choice(false)),
			Does.StartWith("Cast Ajani's Anguish"),
			"the control: lost lines tied, the card thrown away"
		);
		Assert.That(
			MoveTokens.Describe(s, record.Moves[547].Token),
			Is.EqualTo("Pass"),
			"the record"
		);
		Assert.That(MoveTokens.Same(Choice(true), record.Moves[547].Token), "it keeps the card");
	}

	// Shayne, 2026-10-27 (AI-9.5): "an unknown card in hand vs a known card in hand should not have the
	// same value". Every card in the opponent's hand was priced as an unknown card, so a bounced
	// creature (known to everyone) was "a card" and a bounced TOKEN — gone, it can't go to a hand —
	// scored about the same. Now a known card is priced as itself (Evaluator.Terms.KnownHand). The
	// case Shayne agreed (the position suite, White-Blue Jace vs Red-Green seed 4, move 53): the bounce
	// takes the token. The CONTROL: priced blind, it takes the card.
	[Test]
	public void ABounce_TakesTheToken_OnceAKnownCardInTheirHandIsPricedAsItself()
	{
		var suite = PositionSuite.Tracked()!;
		var (_, s) = PositionSuite
			.States(suite)
			.Single(x =>
				suite.Games[x.Position.GameIndex]
					is { Deck1: "White-Blue Jace", Deck2: "Red-Green", Seed: 4 }
				&& x.Position.Move == 53
			);
		var me = s.DecidingPlayerId();
		// under the board-only look-ahead the term was measured in: the default (AI-10.4) recasts the
		// bounced card, which by itself makes the token the better bounce — and stopping after our turn,
		// as it did then (the default since AI-17.6 plays their swing back, and still bounces the token)
		var boardOnly = BeamSearchAi.DefaultRollout with
		{
			Opponent = Rollout.OpponentModel.BoardOnly,
			KnownOnly = false,
			CounterAttack = false,
		};
		global::MagicCore.Cards.Card Target(Evaluator.Terms without)
		{
			var cast = new BeamSearchAi(
				me,
				new() { Without = without, RolloutSettings = boardOnly }
			)
				.Decide(s)
				.Action;
			Assert.That(
				cast,
				Is.InstanceOf<global::MagicCore.Actions.CastSpellAction>(),
				"the bounce is cast — if not, the suite moved: find the token bounce again"
			);
			return s.GetCard(((global::MagicCore.Actions.CastSpellAction)cast!).Targets.Last());
		}
		Assume.That(
			s.CardsIn(s.OpponentOf(me), ZoneType.Battlefield).Count(c => c.IsToken),
			Is.Positive,
			"the suite was regenerated: find the token bounce again"
		);

		Assert.That(
			Target(Evaluator.Terms.KnownHand).IsToken,
			Is.False,
			"the control: priced blind, a card"
		);
		Assert.That(Target(Evaluator.Terms.None).IsToken, "the token");
		var today =
			new BeamSearchAi(me).Decide(s).Action as global::MagicCore.Actions.CastSpellAction;
		Assert.That(
			today is not null && s.GetCard(today.Targets.Last()).IsToken,
			"and the default search, their swing back seen, bounces the token too"
		);
	}

	/// <summary>Our 2/2 and Ferocity of the Hunt (an Aura, with flash); their 1/4 and Last Gasp, two Swamps each side.</summary>
	void FerocityAgainstLastGasp(bool known)
	{
		for (var i = 0; i < 2; i++)
		{
			(s, _) = TestGames.Put(s, me, TestGames.Card("Swamp"), ZoneType.Battlefield);
			(s, _) = TestGames.Put(s, them, TestGames.Card("Swamp"), ZoneType.Battlefield);
		}
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Bear", "2/2"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Wall", "1/4"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, me, CardRegistry.Get("Ferocity of the Hunt"), ZoneType.Hand);
		int gasp;
		(s, gasp) = TestGames.Put(s, them, CardRegistry.Get("Last Gasp"), ZoneType.Hand);
		if (known)
			s = s.UpdateObject(
				gasp,
				s.GetCard(gasp) with
				{
					KnownTo = global::MagicCore.Cards.Knowers.Everyone,
				}
			); // TEST SETUP
	}

	/// <summary>
	/// They attack us (20 life) with one creature — <paramref name="kind"/> — and we have Unsummon and two
	/// Islands, no creatures (AI-13.3/13.4, positions Shayne agreed 2026-10-28). Returns the attacker.
	/// </summary>
	int TheyAttackWith(string kind)
	{
		for (var i = 0; i < 2; i++)
			(s, _) = TestGames.Put(s, me, TestGames.Card("Island"), ZoneType.Battlefield);
		for (var i = 0; i < 6; i++)
			(s, _) = TestGames.Put(s, them, TestGames.Card("Plains"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, me, CardRegistry.Get("Unsummon"), ZoneType.Hand);
		int attacker;
		switch (kind)
		{
			case "token":
				(s, attacker) = TestGames.Put(
					s,
					them,
					TestGames.Creature("Spirit", "3/3"),
					ZoneType.Battlefield
				);
				s = s.UpdateObject(attacker, s.GetCard(attacker) with { IsToken = true }); // TEST SETUP
				break;
			case "aura":
				(s, attacker) = TestGames.Put(
					s,
					them,
					TestGames.Creature("Beast", "2/2"),
					ZoneType.Battlefield
				);
				int aura;
				(s, aura) = TestGames.Put(
					s,
					them,
					CardRegistry.Get("Ferocity of the Hunt"),
					ZoneType.Battlefield
				);
				s = s.UpdateObject(aura, s.GetCard(aura) with { AttachedToId = attacker }); // TEST SETUP
				break;
			case "counters":
				(s, attacker) = TestGames.Put(
					s,
					them,
					TestGames.Creature("Beast", "2/2"),
					ZoneType.Battlefield
				);
				s = s.UpdateObject(attacker, s.GetCard(attacker) with { PlusOneCounters = 3 }); // TEST SETUP
				break;
			case "plain":
				(s, attacker) = TestGames.Put(
					s,
					them,
					TestGames.Creature("Spirit", "3/3"),
					ZoneType.Battlefield
				);
				break;
			case "vanilla":
				(s, attacker) = TestGames.Put(
					s,
					them,
					TestGames.CreatureCosting("{4}{W}", "Rhino", "3/4"),
					ZoneType.Battlefield
				);
				break;
			default: // "enters"
				(s, attacker) = TestGames.Put(
					s,
					them,
					CardRegistry.Get("Fateshaper Aspirant"),
					ZoneType.Battlefield
				);
				(s, _) = TestGames.Put(
					s,
					them,
					CardRegistry.Get("Gallia, Tragic Host"),
					ZoneType.Graveyard
				);
				break;
		}
		s = TestGames.PassUntil(
			s,
			x => x.GetPendingChoice() is Actions.DeclareAttackerChoice c && c.PlayerId == them
		);
		while (s.GetPendingChoice() is Actions.DeclareAttackerChoice c)
			s = s.Choose([c.CreatureId == attacker ? me : Actions.CombatOption.None]);
		s = TestGames.PassUntil(s, x => !x.IsWaitingForChoice && x.DecidingPlayerId() == me);
		return attacker;
	}

	bool BouncesIt(int attacker, Evaluator.Terms without = Evaluator.Terms.None) =>
		new BeamSearchAi(me, new() { Without = without }).Decide(s).Action
			is Actions.CastSpellAction c
		&& c.Targets.Contains(attacker);

	// Shayne, 2026-10-28 (AI-13.3, lesson 2 — "wait until those cards provide actual good value"): a bounce
	// earns its card on a token (gone), an Aura'd creature (the Aura falls off) or a countered one (they
	// fall off) — and never gives back a creature whose "enters" a recast repeats (Fateshaper Aspirant,
	// Gallia in their graveyard; lesson 4). Guards: the option value (AI-13.4) must not hold these.
	[TestCase("token", true)]
	[TestCase("aura", true)]
	[TestCase("counters", true)]
	[TestCase("enters", false)]
	public void Unsummon_EarnsItsCard_OnAValueTarget(string kind, bool bounces) =>
		Assert.That(BouncesIt(TheyAttackWith(kind)), Is.EqualTo(bounces));

	// Shayne, 2026-10-28 (AI-13.4): at 20 life, a plain 3/3 or a vanilla 5-mana 3/4 attacking isn't worth
	// Unsummon — "I'm at 5 life, I can still take a hit" (#547). A held instant is now priced as an option
	// (Evaluator.Terms.HoldOption: the expected best of its offers over the turns left), and a creature we
	// bounced is priced as itself less its recast — so a bounce gains tempo, not the creature. The CONTROL:
	// without it, the search bounces both.
	[TestCase("plain")]
	[TestCase("vanilla")]
	public void Unsummon_IsHeld_AgainstAPlainAttacker(string kind)
	{
		var attacker = TheyAttackWith(kind);

		// The control holds for the vanilla 3/4 only since AI-9.3 (2026-11-07): the walkers' lifetime raised the blind
		// price of an unseen card, and priced blind the plain 3/3 is now held too (Shayne: no point rebuilding it).
		if (kind == "vanilla")
			Assert.That(
				BouncesIt(attacker, Evaluator.Terms.KnownHand),
				"the control: a bounced creature priced as half a card, it bounces"
			);
		Assert.That(BouncesIt(attacker), Is.False, "it holds");
	}

	// AI-4.2's holding position, agreed with Shayne (2026-10-27): #547 of his game, Unsummon held at 5 life
	// ("there is literally no reason to play it right now"). The search holds it since the simulated
	// opponent recasts a creature we know it holds (AI-10.4) — a guard, so a later rule can't undo it.
	// (AI-13's patience can't be what holds: #547 is in their turn, never sorcery timing.) The CONTROL:
	// the board-only look-ahead casts Unsummon.
	[Test]
	public void Unsummon_IsHeld_AtMove547()
	{
		var record = KeptRecords.Get("GalliaWontAttack");
		var s547 = record.Before(547);
		AiDecision Choice(
			Rollout.Settings rollout,
			Evaluator.Terms without = Evaluator.Terms.None
		) =>
			new BeamSearchAi(
				s547.DecidingPlayerId(),
				new(RolloutSettings: rollout, Without: without)
			).Decide(s547);
		// the control is the look-ahead as it was: Settle's choices (AI-17.6) hold too
		var boardOnly = BeamSearchAi.DefaultRollout with
		{
			Opponent = Rollout.OpponentModel.BoardOnly,
			KnownOnly = false,
		};

		Assert.That(
			Choice(boardOnly, Evaluator.Terms.SettleChoices).Action
				is global::MagicCore.Actions.CastSpellAction c
				&& s547.GetCard(c.CardId).Name == "Unsummon",
			"the control: board-only, it casts Unsummon"
		);
		Assert.That(
			MoveTokens.Same(
				MoveTokens.Token(s547, Choice(BeamSearchAi.DefaultRollout)),
				record.Moves[546].Token
			),
			"it holds, as Shayne did"
		);
	}

	// Shayne, 2026-10-27 (AI-10.4, built by hand, the answer agreed): "if we know our opponent has a card
	// in hand via a look at hand effect or unsummon or graveyard return type thing, then the opponent
	// shouldn't just run into it." Our 2/2 and Ferocity of the Hunt (an Aura); their Last Gasp, KNOWN, with
	// two Swamps up. Enchanting the 2/2 runs into it — two cards for their one. The simulated opponent
	// now plays the cards we know (Rollout.Settings.KnownOnly), so the search holds. The CONTROL: the
	// board-only look-ahead casts it. And a card we DON'T know changes nothing: the same decision as
	// board-only. (Casting it at the first chance at all is AI-13's lesson 5: AFlashAura_IsHeld_NotCastAtTheFirstChance.)
	[TestCase(true)]
	[TestCase(false)]
	public void AnAura_IsntCastIntoAKnownRemovalSpell(bool known)
	{
		FerocityAgainstLastGasp(known);
		bool CastsTheAura(bool knownOnly) => new BeamSearchAi(
				me,
				new(
					RolloutSettings: knownOnly
						? BeamSearchAi.DefaultRollout with
						{
							Opponent = Rollout.OpponentModel.Responds,
							KnownOnly = true,
						}
						: BeamSearchAi.DefaultRollout with
						{
							Opponent = Rollout.OpponentModel.BoardOnly,
							KnownOnly = false,
						}
				)
				{
					// the opponent model alone: AI-13's option value would hold the Aura either way
					Without = Evaluator.Terms.HoldOption,
				}
			).Decide(s).Action is global::MagicCore.Actions.CastSpellAction;

		if (known)
		{
			Assert.That(CastsTheAura(knownOnly: false), "the control: board-only, it runs into it");
			Assert.That(CastsTheAura(knownOnly: true), Is.False, "it holds");
		}
		else
			Assert.That(
				CastsTheAura(knownOnly: true),
				Is.EqualTo(CastsTheAura(knownOnly: false)),
				"an unseen card is not used"
			);
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
			(s, _) = TestGames.Put(s, me, CardRegistry.Get(card), ZoneType.Hand);
		(s, _) = TestGames.Put(s, me, CardRegistry.Get("Arcane Amphisbaena"), ZoneType.Battlefield);
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

	// Shayne, 2026-10-28 (AI-15, from flag 4 of AI-14's replay — the game itself no longer replayed, so
	// rebuilt): our 3/3 menace, their two untapped 2/2s, them at 3 life. The look-ahead's defender never
	// gang-blocked, so the attack scored as a WIN; their real search double-blocks and the 3/3 dies for a
	// 2/2. His answer: don't attack. The CONTROL: without gang blocks in the look-ahead, it attacks.
	[TestCase(false, ExpectedResult = false)]
	[TestCase(true, ExpectedResult = true)]
	public bool AMenaceAttack_IntoTwoBlockersThatKillIt_IsntMade(bool noGang)
	{
		(s, var brute) = TestGames.Put(
			s,
			me,
			TestGames.Creature("Brute", "3/3", "Menace"),
			ZoneType.Battlefield
		);
		for (var i = 0; i < 2; i++)
			(s, _) = TestGames.Put(
				s,
				them,
				TestGames.Creature("Cadet", "2/2"),
				ZoneType.Battlefield
			);
		s = s.WithPlayer(s.GetPlayer(them) with { Life = 3 }); // TEST SETUP
		var ai = new BeamSearchAi(
			me,
			new() { Without = noGang ? Evaluator.Terms.GangBlocks : Evaluator.Terms.None }
		);
		// our turn up to the end of combat: did the Brute attack?
		var attacked = false;
		for (var i = 0; i < 200 && s.GetGame().Step <= Step.EndCombat && !s.IsOver(); i++)
		{
			s = s.DecidingPlayerId() == me ? ai.Decide(s).State : TestGames.PassOrChoose(s);
			attacked |= s.GetCard(brute).AttackingId != 0;
		}
		return attacked;
	}

	// Shayne, 2026-11-07 (AI-9.3, from MC-5.4's planeswalker self-play games, seed 98): Rank Rat makes Seat 2 discard
	// on turn 4, two lands in play. "Keeping the island is better, keeping protege's awakening, hapatra the desert frost
	// over jace is clearly not right. That Jace literally has the potential to win the game on its own." The search
	// discards The Theorist, Jace Beleren — its held price (3.3, the card-value sandbox: the cast turn alone) ranks it
	// below every other spell in hand. Fixed by AI-9.3: priced over its lifetime (a 1/1 a turn, a card each opponent's
	// draw step), the table puts it at its measured value, read from the table, never restated here.
	[Test]
	public void TheoristDiscardedOverCommons_KeepsTheWalker()
	{
		var s = KeptRecords.Before("TheoristDiscardedOverCommons", 72);
		var choice = MoveTokens.Token(s, new BeamSearchAi(s.DecidingPlayerId()).Decide(s));
		Assert.That(MoveTokens.Describe(s, choice), Does.Not.Contain("The Theorist"));
	}

	/// <summary>
	/// The control, the whole price reverted (`+walkernow`): the search without it AND the hand's cards measured
	/// without it — the held price comes from the table, which the lifetime moves — makes the recorded play.
	/// </summary>
	[Test]
	public void TheoristDiscardedOverCommons_Control_TheSearchDiscardsHim()
	{
		var s = KeptRecords.Before("TheoristDiscardedOverCommons", 72);
		var me = s.DecidingPlayerId();
		var same = Evaluator.Without(
			Evaluator.Terms.WalkerLifetime,
			() =>
			{
				var values = CardValues.Fra.ToDictionary();
				foreach (
					var e in CardValues.Measure(
						[
							.. s.CardsIn(me, ZoneType.Hand)
								.Select(c => c.Name)
								.Distinct()
								.Select(CardRegistry.Get),
						]
					)
				)
					values[e.Name] = e;
				var held = new HeldValues(values, BeamSearchAi.Settings.HeldFraction);
				var ai = new BeamSearchAi(
					me,
					new BeamSearchAi.Settings(
						RolloutSettings: BeamSearchAi.DefaultRollout with
						{
							Held = held,
						}
					)
				);
				return MoveTokens.Same(
					MoveTokens.Token(s, ai.Decide(s)),
					KeptRecords.Get("TheoristDiscardedOverCommons").Moves[71].Token
				);
			}
		);
		Assert.That(same);
	}

	// Shayne, 2026-11-07 (AI-9.3): "Usually we will always want to draw a card here. Loyalty = Life should not be a
	// 1-1 thing … in 99.9% of cases, you are drawing a card of a jace that allows you to (assuming you can't use the
	// loyalty for something else)". The position suite, White-Black vs White-Blue Jace seed 1, move 108: our second
	// main phase at 4 life, a Jace token ("−1: Surveil 1", "−3: Draw a card") at 6 loyalty, our 3/2 against their 3/3
	// deathtouch and a tapped 1/4. With each turn of the lifetime at 0.7 of the one before, the second −3 kept for
	// later outweighed the card drawn now (the sampled draw a Plains) and the search passed; at 0.6 it draws.
	// Revert-checked by hand (LifetimeDecay 0.7: Pass). AI-8.2's price (`+walkernow`) draws too.
	[Test]
	public void AJaceToken_DrawsWithItsMinusThree_RatherThanBankingTheLoyalty()
	{
		var suite = PositionSuite.Tracked()!;
		var (_, s) = PositionSuite
			.States(suite)
			.Single(x =>
				suite.Games[x.Position.GameIndex]
					is { Deck1: "White-Black", Deck2: "White-Blue Jace", Seed: 1 }
				&& x.Position.Move == 108
			);
		var choice = MoveTokens.Token(s, new BeamSearchAi(s.DecidingPlayerId()).Decide(s));
		Assert.That(MoveTokens.Describe(s, choice), Is.EqualTo("Jace [−3]"));
	}
}
