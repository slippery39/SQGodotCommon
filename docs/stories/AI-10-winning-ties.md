---
id: AI-10
title: A winning AI still presses — ties among won lines no longer pick the passive move
status: backlog
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

- [ ] At #545 and #587 of that record the search attacks with Gallia — kept as AI test cases
      (`--record=… --keep=NAME --at=N`), each with its control
- [ ] Among lines that all win, the one that presses now scores higher — a monotonic term, no tie
- [ ] Measured the AI-6 way: `--suite=beam` (what changed) and `--match=…,beam --until-clear`
      before it becomes the default

## Decisions

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

## Tasks

- [ ] AI-10.1 Keep the two positions as AI test cases, each failing today (the control: the search
      plays "Don't attack")
- [ ] AI-10.2 The tie-break Shayne picks; the kept cases pass; `--suite=beam`, then the early-stop match

## Next

Ask Shayne which tie-break (above). Read `MagicCore/Ai/Evaluator.cs` (`Evaluate`: the flat
±10,000), `Rollout.cs` (`TerminalDiscount`), and `docs/findings/magic-ai.md`. The experiment is
two lines in `Evaluate` — the record replays, so `GameRecord.Before(545)` is the position.

## Log

- 2026-10-23: created from Shayne's game; the cause measured (above); one tie-break tried and reverted.
