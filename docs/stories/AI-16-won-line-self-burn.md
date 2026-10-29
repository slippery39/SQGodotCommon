---
id: AI-16
title: A won line still counts what it spends — the AI no longer burns its own face
status: done
area: AI
created: 2026-10-29
---

## Why

AI-14's replay after AI-15 (2026-10-29, game 2 seed 1, turn 13 — AI-14.3's flag 6): Red-Green cast No
Admittance at ITS OWN FACE (20 → 17) with White-Blue Jace at 9. Shayne: "a huge bug … treat it as a
bug from the last sprint that should be fixed before we continue."

The cause (the position's first-move scores): every line that burns a face or a Jace WINS on turn 15
(it did — the game ended there), so each scored `10,000 × 0.95² − their life as our turn ends` — AI-10's
progress tie-break. Burned at their face, they blocked everything and stayed at 6 (9019); burned at
ours, they took 6 in the attack, 9 → 3 (9022). The self-burn "pressed harder", and in a won line our
own life and cards counted for nothing. Counting our life too doesn't fix it: both net 3, a tie, and a
tie takes the first move — the self-burn.

## Requirements

- [x] A won line counts what the turn spent on both sides, not their life alone — card-agnostic
- [x] A sooner win still beats a later one
- [x] The position as a test (`AiPositionTests.AWonLine_DoesntBurnOurOwnFace`), its control the old
      tie-break; AI-10's Gallia positions still attack
- [x] Measured the AI-6 way: suite, then `--until-clear` against `beam+lifeprogress`
- [x] When every line loses (Shayne, 2026-10-29): a later loss still beats a sooner one, and among
      losses on the same turn the best board as our turn ends wins

## Decisions

- 2026-10-29, Shayne: fix it before the sprint continues — a bug from sprint 2 (AI-10), not a lesson.
- 2026-10-29, Shayne (on the diagnosis): "why would winning with a lower life total beat out winning
  with a higher life total? … I'm not sure I agree with that" — the same win at a higher life must
  score higher (the old rule never compared our life at all). The match approved.
- 2026-10-29, Claude (open to change): a won line's progress is **our evaluation as the search's own
  turn ends** (`Rollout.ProgressBy.TurnEnd`) — it holds their life (AI-10's rule) and everything else
  the turn did. Squashed (`cap × tanh(e / cap)`) under `TurnEndCap`, under half the last turn's discount
  gap (214 at the defaults), so a sooner win still beats a later one — the review: a plain ±200 clamp
  tied every line above it again, and a fixed cap didn't follow `LookaheadTurns`. AI-10's rule stays as `beam+lifeprogress`, the old ties as `beam+noprogress`.

- 2026-10-29, Shayne (on lost lines still tying): "If the AI can only find losing lines, the AI should
  still try to maximize their board score as best as possible … pick the one which has the best board
  score". Asked which comes first: **lose later first** — "it gives your opponent the chance to make a
  mistake that might allow you back into the game" — then the best board score, read **as our turn
  ends** (the moment a won line uses). Committed after AI-16.3, built as AI-16.4.

- 2026-10-29, Shayne: a line lost INSIDE the searched turn (blocks when every block dies) is **not**
  extended to "the board just before the loss" for now — wait until it shows in a game.

## Tasks

- [x] AI-16.1 Diagnosed on the kept position (`Records/NoAdmittanceAtOwnFace.json`, #321) — above
- [x] AI-16.2 `Rollout.Settings.Progress` an enum (`None`, `TheirLife`, `TurnEnd`); `TurnEnd` the
      default. Fires: their face 9083, our own face 9052 (was 9019 against 9022). Tests: the position
      and its control, Gallia both ways; MagicCore.Tests 1 134 pass. Suite: 6 of 288 decisions change
      (edicts cast, Yoshimaru's fight activated, Jace −3 over Memory Trap, two passes), cost unchanged
      (3 097 steps); oracle agreement 80.6% against 82.6% — that oracle reads with AI-10's tie-break,
      so it can't rank this (as AI-15)
- [x] AI-16.3 The match (Shayne approved): `--match=beam,beam+lifeprogress --until-clear` — **51.6% ±
      0.9pp over 192 games (99–93), no difference, worse rejected**, −1.5% cost, 678 s. Kept. Suite
      accepted, the oracle re-read: 82.6% (`docs/findings/magic-ai.md`). The review's squash in place of
      the clamp keeps every lower line's order, so the match stands: the suite 0 changed after it
- [x] AI-16.4 Lost lines get the same tie-break (Shayne's Decision) — **built** (2026-10-29):
      `Rollout.Settings.LostLines` (`beam+nolostlines` is off): a line lost after our turn adds the
      turn-end board, squashed under `TurnEndCap`, so a later loss still comes first; a loss inside our
      own turn has no turn-end board and is unchanged. Fires: at the default look-ahead only 2 of 288
      suite positions lose on every line (the oracle's four-turn look sees 13), and neither changes
      (#119 now ranks Pass over spending Academic Ascent, −9505 against −9557: it passed already) — the
      suite 0 changed. In 8 AI games (`--decisions --games=8 --seed=11`) lost positions are common and
      the rule moves many: it keeps Academic Ascent or Ajani's Anguish rather than cast them for
      nothing, draws with Jace, kills a Cadet. Position: `ALostLine_KeepsTheBoard_NotACardThrownAway`
      (`Records/LostLineAnguishForNothing.json` #548: Ajani's Anguish for X = 0 while lost; its
      control casts it). Match (Shayne approved): `--match=beam,beam+nolostlines --until-clear` — **50.0% over
      96 games (48–48), no difference**, +0.2% cost. Kept (sensible plays, AI-10.4's rule)

## Next

Done. Side note found on the way, not this bug: the
look-ahead answers a TRIGGER's target with the first legal one (`Targets.FirstLegal`, measured no
different in AI-3.8), so a simulated Ajani's Anguish may hit our own face; only the real decision
searches every target. A line lost inside our own turn still ties (DesignNotes, MagicCore known ceilings).
`Records/YoshimaruWithNoFight.json` (#70) is kept for AI-13.5, not tested here.

## Log

- 2026-10-29: created from AI-14.3's flag 6; diagnosed and fixed behind a switch the same day.
- 2026-10-29: measured (51.6% over 192 games, no difference, worse rejected) and kept.
- 2026-10-29: AI-16.4 — lost lines keep the best board (Shayne); 50.0% over 96 games, kept. Done.
