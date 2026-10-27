---
id: AI-10
title: A winning AI still presses — ties among won lines no longer pick the passive move
status: in-progress
area: AI
created: 2026-10-23
---

## Why

Shayne's game of 2026-10-23 (`game_records/2026-10-23_230227_seed934349.json`, 835 moves; he won
on turn 33): the AI never attacked with Gallia, Tragic Host (2/1, menace) though only one creature
could block her each time, and he was at 5 life. "I'm pretty sure he would have won if he had."

**Cause, measured** (the search's `FirstMoveScores` at the two declarations, #545 turn 22 and #587
turn 24): attacking and not attacking both score **9025.0** = 10,000 × 0.95². The search's
simulated opponent plays its board only (`OpponentModel.BoardOnly`), so the AI's full attack next
turn is lethal either way; a won game scores a flat 10,000 (`Evaluator.Evaluate`), discounted per
TURN (`Rollout.TerminalDiscount`) — so two wins on the same turn tie, and a tie picks the first
option, "Don't attack". MagicCore's own rule names this: a cap makes more tie with less, and a tie
silently picks the passive move. The real Shayne cast things and stabilised; the 4 damage Gallia
never dealt mattered.

## Requirements

- [x] At #545 and #587 of that record the search attacks with Gallia — kept as AI test cases
      (`--record=… --keep=NAME --at=N`), each with its control
- [x] Among lines that all win, the one that presses now scores higher — a monotonic term, no tie
- [x] Measured the AI-6 way: `--suite=beam` (what changed) and `--match=…,beam --until-clear`
      before it becomes the default

## Decisions

- 2026-10-24, Shayne: **the tie-break is "progress this turn"** — a won line also counts the
  opponent's life lost before the rollout's first turn ends, so damage dealt NOW scores higher.
- 2026-10-27, Claude (AI-10.2, open to change): "the rollout's first turn" is read as **the search's
  own turn** (the root turn), so the moment is the same for every candidate — the rollout's own rule
  for its horizon. Their life is noted as that turn ends; a won line scores
  `10,000 × 0.95^turns − their life then` — a point a life, far under one turn's discount (~450), so
  a sooner win still beats a later one. Lost lines are unchanged. A switch,
  `Rollout.Settings.Progress`; the default since its match (`beam+noprogress` is the search before).

Open questions (Shayne):
- Which tie-break? Measured on the record, one position each (2026-10-23):
  - **Overkill** — a win scores 10,000 + how far below 0 the loser's life went. Gallia then attacks
    at both (9029.5 vs 9027.7), but Tinybones at #544 newly TIES and stays home: a blocked
    attack adds no overkill. Partial.
  - **Progress this turn** — a won line also counts the opponent's life lost before the rollout's
    first turn ends, so damage dealt NOW is worth more than the same win later. Not yet tried.
  - **Discount by step, not turn** — a win sooner within the turn scores higher. Not yet tried.
- Should the simulated opponent stop being so passive (`CastsFromHand` / `Responds`, AI-3)? That is
  why the search felt safe; AI-3 measured those models — read `docs/findings/magic-ai.md` first.
  **Decided here once for every story it touches** (2026-10-24): the same board-only opponent likely
  makes Unsummon look like a kill spell — a bounced creature is never recast in the look-ahead
  (AI-4's bounce cases, AI-10.3). AI-3 measured casting opponents no STRONGER overall at 5pp, but
  never these behaviours.

## Tasks

- [x] AI-10.1 Keep the two positions as AI test cases, each failing today (the control: the search
      plays "Don't attack")
- [x] AI-10.2 The tie-break Shayne picks; the kept cases pass; `--suite=beam`, then the early-stop match.
      Built: `Rollout.Settings.Progress` (measured as `beam+progress`, now `beam+noprogress` is off); `AiPositionTests.Gallia_AttacksWhenTheGameIsWon_ProgressThisTurn`
      (one kept record, `GalliaWontAttack`, at both moves; the control, without it, stays home). Suite: 5 decisions change, every one
      at the same oracle score (score given up 0.76 either way) — ties among won lines now press (an
      attack, Ajani's Anguish X=4 over a land, an ability). Oracle agreement 81.6 → 79.9% against the
      old oracle, whose own ties pick the first; re-read with the tie-break (`--suite=oracle`),
      83.3% against 81.6% without. The match: 51.4% ± 1.2pp over 288 games, no 5pp difference either way
      (findings) — made the default, the suite accepted (which also took in seed 3 move 67's drift,
      there at HEAD before this story)
- [x] AI-10.3 Evidence for the opponent-model question: in a position of the same record where the
      oracle casts Unsummon and Shayne held it (#547), its scores with `BoardOnly` against a casting
      opponent — does the bounced creature ever come back in its rollouts? Minutes, not a run.
      **Answered (2026-10-27):** board-only, the search casts Unsummon on Fateshaper Aspirant (−24.1
      against passing's −38.5); with `CastsFromHand` it PASSES, as Shayne did (−60.3; the best
      Unsummon −66.9). **Corrected the same day (AI-9.5's probe):** that casting opponent cast from
      its REAL hand (`Hidden.Hand.Real`, the default) — cards Shayne couldn't know. Honest
      (`casts+unseen`), the search still casts Unsummon, on Gallia (−40.0 against passing's −48.4).
      So a casting look-ahead alone does not explain his hold — and Shayne's reasons (2026-10-27) are
      patience, not the look-ahead: #547 is AI-4.2's holding position, AI-13's lesson 4
- [ ] AI-10.4 The look-ahead opponent casts, honestly (Shayne, 2026-10-27: "possibly"): `beam+casts+unseen`
      (seen cards kept, unseen redrawn) at #547 and on AI-4.2's bounce cases, then `--match=beam+casts+unseen,beam
      --until-clear`. AFTER AI-9.5 (a known card in their hand priced as itself), which may settle
      #547 alone — then this asks only whether the recast tempo still matters

## Next

AI-10.1–10.3 done; progress this turn is the default. **Left: AI-10.4** — the opponent-model
question, now split (Shayne, 2026-10-27): first AI-9.5 prices a KNOWN card in their hand as itself
(a bounced creature is not "a card"), then AI-10.4 tries an honest casting look-ahead opponent
(`beam+casts+unseen`). Close the story after AI-10.4; its answer covers AI-4's bounce cases and
AI-13's lesson 2.

## Log

- 2026-10-23: created from Shayne's game; the cause measured (above); one tie-break tried and reverted.
- 2026-10-24: the opponent-model question now also covers AI-4's bounce timing (AI-10.3 added).
- 2026-10-27: AI-10.1 kept (#545, #587); AI-10.2 built behind a switch, suite read; AI-10.3 answered
  (board-only bounces, casting passes).
- 2026-10-27: AI-10.2's match, 51.4% ± 1.2pp over 288 games (no 5pp difference); progress this
  turn is the default (`docs/findings/magic-ai.md`).
- 2026-10-27: Shayne — the opponent-model question becomes two tasks: AI-9.5 (a known card in their
  hand priced as itself) first, then AI-10.4 (an honest casting opponent).
