---
id: AI-33
title: Attack tests — the right attack pinned in small positions, before combat is made cheaper
status: ready
area: AI
created: 2026-11-20
---

## Why

The attack plans are the biggest share of a sealed run's time (AI-30.1's profile: 37%), and every way to make them
cheaper — fewer plans re-judged (`beam+shortlist2`), a fixed menu on big boards, grouped tokens (AI-21) — changes
which attack the AI picks. A match is too noisy to say whether it still attacks well. Shayne (2026-11-20): "Like our
plan for the flash tests, instead of just playing a bunch of games, it would be better to have particular tests for
certain attack outcomes, so we can easier measure the performance and the quality of the attacks. While the matches
are good as a general check, there is too much noise there to actually measure if its working or not."

## Requirements

- [ ] A set of small attack positions, each with the right attack in Shayne's words, run against the search as it is
      — the ones it fails are bugs or the first notes, the ones it passes are what a speed change must keep.
- [ ] Every attack speed change (AI-30.4e, `shortlist2`, AI-21's steps) is judged on them before its match.
- [ ] Each position's cost (engine steps) reported too, so a big board's price is visible beside its answer.

## Decisions

- 2026-11-20, Shayne: tests for attack outcomes first; the matches stay as the general check. Tricks are not played
  around yet — "we need to get the AI making the basic decisions well before we even think of implementing a way to
  play around tricks".
- 2026-11-20, Shayne, on the Cadet board (we at 10, they at 12 with two 3/3 deathtouch and a 1/4 flier that drains 1):
  "You cannot race them. At this point you have to try and remove their creatures from the board. So the scoring
  saying its better to attack or its better just to chump makes no sense. In fact chumping here (1 on each 3/3) still
  kills you the next turn anyways." - "a pretty fundamental flaw ... lets try out some fixes here". The fix: the clock
  reads two attacks (`Evaluator.Terms.TwoAttacks`, `+oneattack` without), not a new block rule.

**Claude's draft positions (2026-11-20) — for Shayne to correct, add to and answer; nothing built.** Each is our turn,
our declare-attackers, both at 20 life unless said; creatures are vanilla unless said.

1. **Lethal on board.** We have a 3/3 and two 2/2s; they are at 4 with one untapped 1/1. Everyone attacks (one block
   stops at most 3).
2. **A bigger blocker.** Our 2/2 against their untapped 4/4. The 2/2 stays home.
3. **Nothing can block.** Our 2/2; they have no creatures. The 2/2 attacks.
4. **Nothing can block, and a blocker after** (from the suite, seed 2 #102). We are at 10 with a 2/2 Cadet token and a
   Jace token at 10 whose −2 makes another Cadet; they are at 12 with two 3/3 deathtouch and a 1/4 flier, all tapped,
   which attack us next turn (7 power; the flier's attack drains 1). The search (since AI-30.4e's fix) attacks with the
   Cadet for 2, then makes a new Cadet to block. **Shayne: keep the 2/2 back "to try and double block one of their
   3/3's the next turn"** - and "this seems more like a bug with the blocking logic than with the actual changes we
   made here. We should make this a test case and make sure that it is working properly before assuming that our AI
   change is the problem". It is (10 below): the look-ahead never double-blocks on this board.
5. **The walker.** Our 4/4, 3/3 and 2/2; their planeswalker at 4 loyalty, no blockers. The 4/4 at the walker, the rest
   at their face (Shayne's own example; the test `AWholeAttack_SendsTheFourPowerAtTheWalker_TheRestAtTheFace` exists).
6. **Evasion only.** Our 2/2 flier and 3/3; their untapped 4/4 without reach. The flier attacks, the 3/3 stays home.
7. **A deathtouch blocker.** Our 5/5 and 2/2; their untapped 1/1 deathtouch. **Shayne: the 2/2 attacks, the 5/5 stays
   home** - "You usually don't want to trade your 5/5 for their 1/1 deathtouch unless maybe you have multiple large
   creatures and are trying to push through damage. But on its own no."
8. **The Jace-token lethal** Shayne gave in AI-19.21 (the reason "best 1" was rejected) — reused as it is.
9. **A big board of copies.** Ten 1/1 tokens; they are at 8 with one untapped 2/2. Everyone attacks (nine get through),
   and the decision costs about what a board of three does — the measure for AI-21's grouping. **Shayne: "everyone
   attacks".**
10. **A double block** (a block test, from 4). Their 3/3 attacks our two 2/2s: both block it. Built
    (`RolloutTests.TwoTwoTwos_DoubleBlock_AnAttackingThreeThree`): the look-ahead's defender and the search both
    double-block - also with two attackers, and with a flier at 10 life; at 20 life against a deathtouch 3/3 neither
    blocks. **On the Cadet board's real cards** (two Rampart Hunters, a Screeching Soulbreaker; we at 10, they at 12;
    `TheCadetBoard_BothCadetsBlockTheSameHunter`) the search double-blocks a Hunter but **the look-ahead blocks it with
    one Cadet and leaves the other idle** - the explicit case, failing. Open: whether a double block that loses both
    Cadets to a deathtouch Hunter is the block Shayne wants (his answer to 4 says keep one back for it).

## Tasks

- [x] AI-33.1 Shayne corrects the draft (answers 4, 7 and 9 given 2026-11-20; more positions welcome)
- [x] AI-33.2 The positions as tests, run against today's search: which fail, at what cost each - `AttackTests`:
      1, 2, 3, 4, 6, 7, 9 (5 and 10 were already in `RolloutTests`); all pass; 8 not found as a board yet
- [x] AI-33.4 The look-ahead's half-block on the Cadet board fixed (the explicit test passing), before the Cadet's
      attack is read as a fault of AI-30.4e's fix - the two-attack clock; its match still to run
- [x] AI-33.5 The turns to kill read the same two attacks (`Evaluator.Terms.TwoAttackKill`, `beam+onekillclock` off) -
      Shayne, 2026-11-21: "Sure lets explore that"; the Cadet board's clock on us 10 turns -> 3
      (`TheCadetBoard_TheirClockReadsTwoAttacks`); counted up, not averaged (the review): suite 0 changed, match level, 49.0% ± 1.0pp
- [x] AI-33.6 The burn rule's two-turn kill reads the same two attacks (`Evaluator.Terms.TwoAttackBurn`,
      `beam+oneburnclock` off) - Shayne, 2026-11-21: "Sure" to tests on two boards over a match's noise
      (`TheBurnRulesKill_ReadsTwoAttacks`: a chump stops its attacker once, a trade for good); suite 0 changed; match
      identical (48-48, every game alike)
- [x] AI-33.7 A creature tapped for the next attack is back for the second - an experiment, OFF by default
      (`beam+tappedback`, switching off `Evaluator.Terms.TappedStay`); found by AI-33.6's review; oracle re-read;
      match 48.2% ± 1.2pp (600 games, undecided, leaning worse)
- [x] AI-33.8 The damage sent at our walker reads the two attacks (`Evaluator.Terms.TwoAttackWalker`,
      `beam+onewalkerclock` off): the first attack's at the first hit, the second's after (`AChumpBlocker_ShieldsTheWalkerOnce`);
      suite 2 changed, 4 golden games moved; match 51.4% ± 0.8pp (288 games, leaning better) - the default
- [ ] AI-33.3 The attack speed changes judged on them (AI-30.4e first, then `shortlist2`, then AI-21's steps) -
      `noblockers`, `shortlist2` and `shortlist1` keep every answer (each a fixture of `AttackTests`); AI-21's steps to come

## Next

The two-attack clock is in the pressure term (AI-33.4) and the turns to kill (AI-33.5). The burn rule's two-turn kill
(AI-33.6) reads it too. A blocker tapped now is still missing from the second attack (`TappedStay`, the default): the
fix, `beam+tappedback` (AI-33.7), leaned 1.8pp worse, its lost games showing no pattern - left off. The walker's damage a turn (`AtTheWalker`) reads the two attacks too (AI-33.8, the default: match 51.4% ± 0.8pp). Position 8 (Shayne's Jace-token lethal, AI-19.21) needs its
board found. More positions welcome - especially BIG boards, where the attack plans' cost is (these small ones save 2-17%
under the speed changes); a new AI-21 step joins as a `[TestFixture("beam+…")]`.

## Log

- 2026-11-20: written from Shayne's ask at AI-30.1's profile; Claude's draft of nine positions, for him to correct.
- 2026-11-20: Shayne's answers to 4, 7, 9. The double-block test (10) passes on plain boards; on the Cadet board's real
  cards the look-ahead half-blocks (one Cadet on a Hunter, the other idle) where the search double-blocks - pinned as
  an explicit failing test. Likely cause, unconfirmed: each blocker judged alone, the first blocking on the expectation
  that the second joins (Lookahead.Joins in its settle), the second then judged alone and declining.
- 2026-11-20: AI-33.4 tried - a later blocker joins a block that dies only with it (`Rollout.Answer`, `Lookahead.Joins`):
  the Cadet board passes, and the suite's Cadet stays home (Shayne's answer). Measured why the look-ahead stayed
  out: Cadet A blocks Hunter 1 at −36.85 on the expectation that B joins; B judged alone scores "stay out" −36.61 against
  joining −36.85 - a live 2/2 (7.0 + 1.5 clock) just over a dead Rampart Hunter (8.3). But the rule also changed 4 other
  suite decisions, two of them +1/+1 counters put on the OPPONENT's creatures (Tam's Resistance on a Rampart Hunter,
  seed 1 #79; Yoshimaru's {6} on a Graft Surgeon, seed 1 #181), and broke the control of the two Chang sacrifice
  records (`ASacrifice_ThatOnlyBaitsTheSimulatedAttack_IsNotMade`). Not kept as it is - for Shayne. Also found: AI-30.4e's
  blocks fix breaks Shayne's Guidance Failure position (`AfterGuidanceFailure_TheAiAttacks_NotTheWorstOfBoth`) - there
  the Spanner Jockey's attack and no attack tie exactly (11.57; it trades with their 3/1 now or on their turn), the tie
  now to "no attack"; and `TheSearch_PlaysARealGame_FromItsSampledHands` (its two hand modes now play alike).
  `ReadingACreature_AllocatesNothing` fails at the last commit too (24 bytes) - not today's.
- 2026-11-20: AI-33.4 - the cause was the CLOCK, not the blocks: "their clock on us" paired each untapped blocker with
  one attacker for good, so the Cadet kept home "stopped" a Hunter and both blocks left the same clock (4 through).
  Shayne: "You cannot race them". Now the clock averages two attacks (`Evaluator.Attacks`): a blocker that died
  blocking is gone for the second, an attacker that died to its blocker too (half-block 4 then 7, double block 4 then
  4). The join rule stays out. The Cadet board's look-ahead double-blocks (one attack, the control: half-blocks).
  Suite: 14 changed (4.9%), all read on their boards - mostly reorders (a card cast after combat, Jace's draw first),
  two blockers cast at low life (4 life: Repurposed Enforcer; 9: Heartstring Puller), no counter on an opponent's
  creature; AI-30.4e's two changes (the Cadet's attack at seed 2 #102, the Mountain at seed 4 #43) are back to the
  committed answers - the Cadet stays home, Shayne's answer to 4. Score given up 0.66 -> 0.57 a position, oracle
  agreement 71.9% both; 12,014 -> 11,934 steps a position. Tests: Guidance Failure and the sampled-hands test pass
  again; three controls (HandSize, Trample, the Chang sacrifices) now name `TwoAttacks` too - the new clock fixes
  their old mistakes as well; the two hand-mode tests moved to seed 2 (seed 3's modes now play alike); the AI golden
  replay regenerated (8 of 12 games moved).
- 2026-11-20: the match, stopped by hand: 49.0% over 200 games (98-102), 53 min. Seed 3 #16 (the Oculus) to AI-34.
- 2026-11-20: pre-commit: the table's log showed an AI's damage split as its prompt ("how much damage to …? (3 left to
  divide)") - a game the new clock changed found it; now "Damage: Edgar, Ancient Bloodlord deals 0 to Arcane Amphisbaena"
  (`MagicTableManager.Declared`). `TableChangesTests` moved to seed 2 (seed 1 no longer marks damage on a survivor).
- 2026-11-21: AI-33.2 - `AttackTests`: seven positions (1 lethal on board, 2 a bigger blocker, 3 nothing can block, 4 the
  Cadet board on its real cards, 6 evasion only, 7 a deathtouch blocker, 9 ten 1/1s), each printing its cost; every one
  run under `beam`, `beam+shortlist2`, `beam+shortlist1` and `beam+noblockers`. All 32 pass; the Cadet board's control
  (`+oneattack`) attacks under each. Engine steps, beam / shortlist2 / shortlist1 / noblockers: 1 lethal 1 165 / 1 100 /
  985 / 1 009; 2 331 / 331 / 331 / 291; 3 240 / 240 / 240 / 220; 4 7 198 / 6 637 / 5 974 / 7 030; 6 855 / 828 / 820 /
  767; 7 723 / 704 / 623 / 693; 9 4 945 / 4 879 / 4 813 / 4 691.
- 2026-11-21: AI-33.5 - the turns to kill (`Evaluator.TurnsToKill`: how long a held card stays useful, the share of
  their attack sent at our walker, the option price's turns ahead) read one attack; now the two the clock reads. On the
  Cadet board their clock on us was 10 turns (the Cadets "stop" both Hunters for good, the flier's 1 through) and is 3
  (1, then 7). Suite: 2 changed (0.7%), both seed 4's Red-Green against the Jace deck, 9 431 -> 9 500 steps a position:
  #56 (we at 10, 5 mana; their Jace at 22 making Cadets): Ajani's Anguish -> Eardrum Rattler makes Samut unblockable,
  Heartstring Puller (its Cadet; Arni 6/5 trample), Samut, Arni and the hasty Puller attack; #109 (we at 1, they at 12,
  their Way of the Healer making Cadets): Ajani's Anguish for 2 -> Budding Insurgent sacrificed to destroy Way of the
  Healer (a card drawn), then Ajani's Anguish for 2 on a Cadet. Shayne, on both: "Oh wow yeah those plays do look better". The match next.
- 2026-11-21: AI-33.5's match, `beam` vs `beam+onekillclock` (--until-clear, 192 games, 28 min): **51.0% ± 1.0pp**
  (98-94), no 5pp difference either way; 4 969 engine steps a decision against 5 054 (−1.7%); spells and lands alike.
- 2026-11-21: pre-commit review: dividing life by the two attacks' AVERAGE misreads low life - the Cadet board at 3
  life read 1 turn (the first attack does 1), and a lethal first attack followed by a blocked second read 2. Now counted
  up: the first attack, then the second's damage for every attack after (cases at 10, 3 and 1 life). With that, **the
  suite's two better plays go back to Ajani's Anguish** - the suite is the committed one again (0 changed), 9 281 steps a
  position - and 2 of the 12 golden games still move. The two plays came from the average's miscount, not a better read
  of the race; the match (51.0%) was of the averaged version. Shayne: "Sure" - the counted version's match, and the two
  boards read with the search's own lines (a temporary probe, deleted). Both were TIES, not a wrong price: #56 two
  winning lines, both won on turn 14 in the look-ahead, 9023.22 (Ajani's Anguish) against 9023.20 (Rattler) - though the
  Rattler line won at 4 life and Ajani's at 1, a win's margin moving its score by 0.02; #109 Ajani's Anguish and Budding
  Insurgent exactly level at the root (−7.69 each, neither line sacrificing it). After the Insurgent, sacrificing it on
  Way of the Healer scores −17.32 against −7.69 for a land: in that look-ahead Arni stays home (4 damage not dealt) and
  their Jace, with no Cadets to make, uses its −3 twice (two cards in their hand, −5.3) - our drawn card counted
  (+2.7). The averaged clock only broke the ties the other way.
- 2026-11-21: the counted version's match, `beam` vs `beam+onekillclock` (--until-clear, 192 games, 41 min): **49.0% ±
  1.0pp** (94-98), no 5pp difference either way; 4 949 engine steps a decision against 5 005. Both versions level - the
  clock is now right on the Cadet board at any life, at no cost in strength. Committed.
- 2026-11-21: AI-33.6 - the burn rule's two-turn kill (`BeamSearchAi.KillsWithinTwoTurns`, which lets burn go at the
  face) read one attack twice against their blockers as they stand. Now the two attacks together (`Evaluator.TwoThrough`,
  shared with the turns to kill) while we can still attack this turn. Shayne chose tests over a match's noise - the rule
  is rare: they at 10 with a 2/2 against our two 3/3s and a burn for 3 now kills (3 + 6 + 3; read twice 9); they at 6
  with a 4/4 against our 3/3 and 2/2 does not (2 + 0 + 3; read twice 7). Suite 0 changed, golden replay unchanged.
- 2026-11-21: AI-33.6's match, `beam` vs `beam+oneburnclock` (--until-clear, 96 games, 21 min): **48-48, ± 0.0pp, the
  same 5 141 engine steps a decision** - every game played alike: in FRA's three decks the two readings never disagreed
  where a burn could go at the face. Its tests are what pin it.
- 2026-11-21: AI-33.6's review found a bug in the two-attack clock itself (since AI-33.4): when the attack is this
  turn's, the defender's creatures tapped now were left out of the SECOND attack too - every creature that attacked the
  turn before, untapped by then. It overstated our second attack on our turn and theirs on theirs (the pressure term,
  the turns to kill, the burn rule). AI-33.7: they come back for the second (`SecondUntaps`, `+tappedstay` without);
  the burn test gains the review's board (they at 8 with a tapped 4/4, our 3/3 and a burn for 3: no kill, 3 + 0 + 3).
  Suite: **16 changed (5.6%)**, score given up 0.57 -> 0.32 a position, oracle agreement 72.2% -> 71.5%, misses a win
  the oracle found 2 -> 5; 9 281 -> 9 388 steps a position. The changes: Jace's abilities used more (5 of 16), attacks at
  a walker or held back, removal on a walker over a creature. AI-33.6 itself: tests passed (AI 441, table 78).
- 2026-11-21: the three "new missed wins" were the STORED oracle's, read before the fix. Re-searched with each line
  shown, all three win: on two the oracle now picks the new answer (Jace −2 for a Cadet, then the Enforcer after
  combat; the second Rampart Hunter over Final Mercy on Koth), on the third (Samut attacks beside Koth as a decoy: the
  3/3 blocks Samut, Koth's 3 goes through) it still prefers Koth alone, 8 170 to 8 163. Shayne's "why no 4/4 on turn
  9?": Red-Green holds Jace's −4 for its next precombat main - the Beast entering gives Arni +4/+0 (5/5 trample), and
  with Koth's landfall ping it is lethal from 5; a line that wins only because the simulated opponent attacks and
  keeps no blockers. Oracle re-read (`--suite=oracle`, 3.0 min, the fixed evaluator - it leans to the fix): **the fix
  94.1% agreement, 1 missed win, 0.58 given up a position; without it (`+tappedstay`) 92.4%, 2, 0.37.**
- 2026-11-21: AI-33.7's match, `beam` (the fix) vs `beam+tappedstay` (--until-clear, 600 games, 69 min): **fix 48.2%
  ± 1.2pp (289-311), UNDECIDED at the cap**, leaning worse (log-likelihood worse -2.36 of ±2.94); 4 782 vs 4 817 engine
  steps a decision. beam's record by deck: Red-Green 131-69, White-Black 99-101, White-Blue Jace 59-141.
- 2026-11-21: Decided (Shayne): commit both, the fix OFF by default - the old reading kept as the default term
  `TappedStay` (was `SecondUntaps`), the fix `beam+tappedback` (was the default, `+tappedstay` the old reading). The
  oracle re-read again on the DEFAULT's evaluator (3.8 min): the default 93.1%, 0.46 given up, 1 win missed; the fix 91.0%,
  0.77, 4 - each oracle favours the evaluator it was read with, so for an evaluator change the match is the verdict
  (findings: magic-ai.md, AI-33.7). Suite 0 changed for the default.
- 2026-11-22: Which games the fix loses (a temporary probe, deleted: each game played twice from one seed, the fix on
  one seat vs `beam`, and `beam` vs `beam` - alike up to the fix's first changed decision; 16 seeds, 192 pairs, 50 min):
  **the fix 92-100, the mirror 96-96; 159 diverged, 32 results flipped - 18 lost, 14 won** (as likely as not by
  chance). The first changed decisions spread over everything - which spell first, a land before a spell, Jace's
  abilities, X sizes, attacks, blocks (3 of the 18 losses a block the default didn't make, against 1 of the 14 wins).
  No one kind of play leans on the overstated second attack: the fix moves the clock's small terms everywhere, close
  calls flip, and the games after go either way. Left OFF; no further digging unless a board shows the misread.
  Shayne on the one block flagged (we at 15 with a Jace at 2: our 1/4 Screeching Soulbreaker chumping their 4/4
  Repurposed Enforcer): "seems wrong" - held for later (White-Blue Jace vs White-Black seed 11, move 221, the fix's seat).
- 2026-11-22: AI-33.8 built: `AtTheWalker` returns the first attack's hit and the later ones' (TwoThrough − Through),
  the lifetime reading the first at the walker's first attack and the later at each after. Test: their 3/3 against
  our walker (+1 Surveil, −3 draw, at 4), our 2/2 in front - priced lower than one attack read every turn; without the
  2/2 the two agree. Suite **2 changed**: Mindseeker Oculus cast BEFORE combat (was: pass) - Repurposed Enforcer's
  attack trigger then counts one creature more for Jace; Yoshimaru's end-step counter on the Cadet 2/2 over Heartstring
  Puller 3/1 trample. 4 of the greedy golden games moved.
  Shayne: the Oculus "makes sense"; the counter belongs on the Puller - "a 4/2 so we have a clean blocker for their
  5/4" (Divining Duelist: a 4/2 kills it blocking, a 3/3 doesn't).
  Why (`Explain` under both readings): the walker term on the end board barely moves (their Jace −10.22 vs −10.47);
  what flipped is the ROLLOUT in the Cadet line. There, both readings put the next counter on the Puller a turn later;
  the old one then held back and lost Rattler and Yoshimaru double-blocking their Duelist (3.72), the new one attacks
  the 4/2 Puller into it and trades - the trade that clears their only blocker now counts for the Jace damage after
  (8.74, over the Puller line's 7.95, which casts Arcane Amphisbaena instead). Their turn between - where Shayne's 4/2
  is the clean blocker - the rollout's opponent attacks in neither line (at 8 life facing our 10 power), so the clean
  blocker never shows. Asked Shayne whether that 5/4 attacks there: "not sure", not to focus on - the match decides.
  Match `beam` vs `beam+onewalkerclock`: 51.4% ± 0.8pp, 148-140 over 288 games, stopped clear of 5pp - on by default.
  Review: `AtTheWalker` reads its two attacks once and passes them to `TurnsToKill(life, first, second)` (was four
  passes; suite and golden games unchanged); the test also prices a walker used this turn - there the sooner second
  attack's soak outweighs its lost turns, so only that the chump counts is asserted.
