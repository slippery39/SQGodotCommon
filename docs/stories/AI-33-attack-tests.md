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
- [ ] AI-33.2 The positions as tests, run against today's search: which fail, at what cost each
- [x] AI-33.4 The look-ahead's half-block on the Cadet board fixed (the explicit test passing), before the Cadet's
      attack is read as a fault of AI-30.4e's fix - the two-attack clock; its match still to run
- [ ] AI-33.3 The attack speed changes judged on them (AI-30.4e first, then `shortlist2`, then AI-21's steps)

## Next

The two-attack clock's match stopped at 49.0% over 200 games (98-102), no difference - Shayne took it
(2026-11-20) and committed. Open from the review: `TurnsToKill` and the two-turn lethal check still read one
attack (DesignNotes) - each a suite and a match of its own. Then AI-33.2: write the tests like `RolloutTests`' whole-attack tests
(`TestGames.Put` the creatures, `PassUntil` the declare-attackers choice, `BeamSearchAi.Decide` each question) and
report `MagicRules.CountingSteps` beside each answer.

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
