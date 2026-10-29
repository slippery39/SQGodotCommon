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
		string Choice(Rollout.ProgressBy progress) =>
			MoveTokens.Token(
				s,
				new BeamSearchAi(
					s.DecidingPlayerId(),
					new(RolloutSettings: BeamSearchAi.DefaultRollout with { Progress = progress })
				).Decide(s)
			);

		Assert.That(
			MoveTokens.Same(Choice(Rollout.ProgressBy.None), record.Moves[move - 1].Token),
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
		string Choice(Rollout.ProgressBy progress) =>
			MoveTokens.Token(
				s,
				new BeamSearchAi(
					s.DecidingPlayerId(),
					new(RolloutSettings: BeamSearchAi.DefaultRollout with { Progress = progress })
				).Decide(s)
			);

		Assert.That(
			MoveTokens.Describe(s, selfBurn),
			Does.EndWith("→ " + s.GetPlayer(s.DecidingPlayerId()).Name),
			"the recorded move is the self-burn"
		);
		Assert.That(
			MoveTokens.Same(Choice(Rollout.ProgressBy.TheirLife), selfBurn),
			"the control: by their life alone, our own face"
		);
		Assert.That(MoveTokens.Same(Choice(Rollout.ProgressBy.TurnEnd), selfBurn), Is.False);
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
					new(RolloutSettings: BeamSearchAi.DefaultRollout with { LostLines = lostLines })
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
		// bounced card, which by itself makes the token the better bounce
		var boardOnly = BeamSearchAi.DefaultRollout with
		{
			Opponent = Rollout.OpponentModel.BoardOnly,
			KnownOnly = false,
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
		(s, _) = TestGames.Put(s, me, FraCards.Get("Ferocity of the Hunt"), ZoneType.Hand);
		int gasp;
		(s, gasp) = TestGames.Put(s, them, FraCards.Get("Last Gasp"), ZoneType.Hand);
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
		(s, _) = TestGames.Put(s, me, FraCards.Get("Unsummon"), ZoneType.Hand);
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
					FraCards.Get("Ferocity of the Hunt"),
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
					FraCards.Get("Fateshaper Aspirant"),
					ZoneType.Battlefield
				);
				(s, _) = TestGames.Put(
					s,
					them,
					FraCards.Get("Gallia, Tragic Host"),
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
		AiDecision Choice(Rollout.Settings rollout) =>
			new BeamSearchAi(s547.DecidingPlayerId(), new(RolloutSettings: rollout)).Decide(s547);
		var boardOnly = BeamSearchAi.DefaultRollout with
		{
			Opponent = Rollout.OpponentModel.BoardOnly,
			KnownOnly = false,
		};

		Assert.That(
			Choice(boardOnly).Action is global::MagicCore.Actions.CastSpellAction c
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
}
