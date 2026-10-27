---
id: AI-10
title: A winning AI still presses — ties among won lines no longer pick the passive move
status: done
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

- 2026-10-27, Shayne (on AI-10.4's no-difference match): try **board-only, except for cards we
  know about** — "if we know our opponent has a card in hand via a look at hand effect or unsummon
  or graveyard return type thing, then the opponent shouldn't just run into it." And: **"while win
  rate is a nice baseline, we also want to make sure the plays the opponents are making make
  somewhat decent sense, so even if this doesn't change win rate by a lot, it is still
  worthwhile."** — a change that measures no different is kept when it makes the plays sensible,
  shown by a position (this widens AI-5's keep rule: "fixes a recorded blunder").

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
- [x] AI-10.4 The look-ahead opponent casts, honestly (Shayne, 2026-10-27: "possibly"): `beam+casts+unseen`
      (seen cards kept, unseen redrawn) at #547 and on AI-4.2's bounce cases, then `--match=beam+casts+unseen,beam
      --until-clear`. AFTER AI-9.5 (a known card in their hand priced as itself), which may settle
      #547 alone — then this asks only whether the recast tempo still matters.
      **Run (2026-10-27), on today's defaults (honest libraries, progress, AI-9.5's known hand):** at
      #547 it still bounces Gallia (−40.0 against Pass −48.4) — Shayne's hold there is patience
      (AI-4.2, AI-13 lesson 4), not this. Suite: 70 of 288 decisions change (24.3%), cost 3 552 steps
      a position against 2 963 (+20%); oracle agreement 69.4% — the oracle searches with the
      board-only opponent, so it can't rank this one (AI-3.4's finding). The match: **50.3% ± 1.8pp,
      384 games (193–191), no 5pp difference either way, +18% cost** (findings). Not adopted.
      **Then Shayne's model (Decisions): board-only, except the cards we know** —
      `Rollout.Settings.KnownOnly` with `Responds`, now the DEFAULT (`beam+boardonly` is the old one;
      `casts`/`responds` the whole hand): 0 of 288 suite decisions change, 49.0% ± 1.0pp over 96 games,
      +1% cost; kept on the hand-built position he agreed (`AnAura_IsntCastIntoAKnownRemovalSpell`:
      Ferocity of the Hunt not cast into a known Last Gasp; with it unseen, the same as board-only —
      revert-checked). His note there became AI-13's lesson 5. Review fixes: the filter runs BEFORE
      DistinctMoves (one move per name — a known card behind an unknown one of its name was dropped;
      `TheDefaultOpponent_CastsOnlyTheCardWeKnow`, revert-checked — the suite unchanged); HiddenTests'
      casting opponents play the whole hand. Left (ponytail): no unknown land either, so a known card
      it can't yet afford stays uncast in the look-ahead, as board-only

## Next

**Done** (2026-10-27). A winning AI presses (progress this turn); the simulated opponent is
board-only plus the cards we know. The bounce and removal questions that remain are patience —
AI-13's lessons 1, 4 and 5, and AI-4's held spells.

## Log

- 2026-10-23: created from Shayne's game; the cause measured (above); one tie-break tried and reverted.
- 2026-10-24: the opponent-model question now also covers AI-4's bounce timing (AI-10.3 added).
- 2026-10-27: AI-10.1 kept (#545, #587); AI-10.2 built behind a switch, suite read; AI-10.3 answered
  (board-only bounces, casting passes).
- 2026-10-27: AI-10.2's match, 51.4% ± 1.2pp over 288 games (no 5pp difference); progress this
  turn is the default (`docs/findings/magic-ai.md`).
- 2026-10-27: Shayne — the opponent-model question becomes two tasks: AI-9.5 (a known card in their
  hand priced as itself) first, then AI-10.4 (an honest casting opponent).
- 2026-10-27: AI-10.4 — the honest casting opponent no different (+18% cost); Shayne's "board-only
  except the cards we know" made the default (no different, +1%, a position he agreed). Story done.
