---
id: AI-14
title: See what the AI decides and why — a decision report now, an in-game inspector next
status: in-progress
area: AI
created: 2026-10-28
---

## Why

Shayne (2026-10-28, after AI-13.4's matches): "based on the previous plays I've seen the AI do, … it is
not playing cards correctly at all. Could we get some visibility into this?" A week of evaluator
changes was argued from match numbers and hand-built positions; MtgCore's game has an inspector (F6:
every ranked option, the chosen one's score by term) and the root `CLAUDE.md` says to use it before a
scoring change — MagicTable has none, and MagicCore's evaluator couldn't explain a score.

## Requirements

- [x] The evaluator explains a score term by term, summing to it — one implementation, never a copy
- [ ] A report of AI-vs-AI games Shayne can read: at each real decision, the position, the options
      ranked by the search's score, and what each top option changes, term by term — he flags what
      looks wrong, and each flag becomes a position or a task
- [x] Next sprint: an in-game inspector on MagicTable (as MtgGame's F6)

## Decisions

- 2026-10-28, Shayne: the decision report now; the inspector next sprint (TBL work).
- 2026-10-28, Claude (open to change): the report is Markdown in `ai_reports/` (untracked), read in
  VS Code's preview — no page to build until it proves useful.

## Tasks

- [x] AI-14.1 `Evaluator.Explain` (`Side` returns its terms as a struct `Evaluate` sums — no
      allocation on the search's path); `EvaluatorExplainTests` (sums to Evaluate at all 288 suite
      positions, both seats)
- [x] AI-14.2 `DecisionReport` / `MagicConsole --decisions [--games=N] [--seed=N]` — 2 games, 239
      decisions, ~20 s
- [ ] AI-14.3 Shayne reads a report and flags plays (five flags so far, game 1 seed 1 — keep going); each flag a position (AI-13's way) or a task.
      **First reading (2026-10-28, game 1, seed 1, White-Black vs Red-Green):**
      1. Turn 4, Red-Green casts Yoshimaru, Scrappy Stray with no other creature — its enters fight
         needs one, so it does nothing; "costs you a potential fight later on" → AI-13.5
      2. Turn 6, Yoshimaru (with a {6} +1/+1 ability) attacks into Rank Rat (its enters spent): "the AI
         did not properly value our 1/1 compared to theirs" — the evaluator prices a creature by
         stats and keywords only → AI-9.6
      3. Turn 9, no block, and a Heartstring Puller missing: the report's fault — White-Black's Vraska's
         Final Mercy killed it from a plan replayed, which the report didn't show. Fixed: every move is
         on the page
      4. Turn 15, White-Black casts Winter, Tormented Loner, declines its sacrifice, then sacrifices
         Winter to Edgar (a counter and menace) and attacks into a full board: Red-Green double-blocks
         with both Cadets — "we trade a winter and edgar for 1 2/2 … probably one of the worse lines
         possible" (Shayne). The search scored the attack 9017: it believed the line WON. Likely cause
         (from the evidence, not yet reproduced): the look-ahead answers blocks one creature at a time,
         each "alone" (`Rollout.Answer` → `BestAlone`), so it never double-blocks — a menace attacker is
         unblockable in the look-ahead. A fidelity bug, not a valuation: any attack a player answers with
         a gang block looks safe → AI-15
      5. Turn 16, Red-Green casts Ajani's Anguish (an X enchantment) for 3 at White-Black's face, its
         runner-up No Admittance (a sorcery, 3 damage) at the face too: "its better to just [hold] both
         spells … If you draw lands the anguish gets better anyways, and there is always a chance the
         opponent plays some creature you need to kill" (Shayne) — sorcery-speed cards aren't priced as
         options yet → AI-13.6
      **Re-read after AI-15 (2026-10-29, Claude, seed 1, `decisions-2026-10-28_211335`):** flag 1 still
      plays (turn 4, Yoshimaru cast 3.8 over Pass) — kept, `Records/YoshimaruWithNoFight.json` (#70);
      flag 2 is gone (turn 6 no attack; turn 7 Yoshimaru and Koth double-block Edgar); flag 5 is gone
      (turn 16 No Admittance kills Rampart Hunter), so AI-13.6 needs a new position.
      6. (Claude) Game 2 turn 13, Red-Green casts No Admittance at ITS OWN FACE (20 → 17), tied (0.0)
         with its own Jace token, White-Blue Jace at 9 — kept, `Records/NoAdmittanceAtOwnFace.json`
         (#321). Diagnosed and fixed → AI-16 (a won line counted only their life)
- [x] AI-14.5 The report as a REPLAY (Shayne: "it's a lot and is time consuming to read"; then "think
      of the log more like a replay, where I am just watching a game … the main thing is that I can
      follow what actually happened"): a section a turn; every move one line — a whole attack, a whole
      block one line each (a declaration is asked a creature at a time); under each, what it did, read
      from the game (who died, left or entered, life); the score against the next best only where the
      AI had a real choice; no "planned" labels (a replayed plan once hid a removal spell — flag 3);
      players named by their decks; close calls first. 2 games: 508 lines, from 3 205
- [x] AI-14.6 Each game saved as a record beside the report (`decisions-<time>-gameN.json`), so a flag
      can be kept as a test (`--record=FILE --keep=NAME --at=N`; `--record=FILE` numbers the moves) —
      added 2026-10-28 when flag 4's game no longer replayed (AI-15.1)
- [x] AI-14.4 The F6 inspector on MagicTable (next sprint)
      Built 2026-11-10 (overnight): F6 (or Tools → AI inspector) opens a panel on the AI's last SEARCHED decision —
      who chose what on which turn, every first move it read with its line's score, best first
      (`BeamSearchAi.Ranked`, named as `Explain` names them), then the lines themselves (`Explain`: each a game
      the search imagined, with its terms). The manager keeps the ranked list of every search
      (`MagicTableManager.LastSearch`); the lines only while that search is the AI's latest decision (the next
      clears them — the panel says to pause the AI from Tools). It follows each new search while open; Esc
      closes. `--inspector` opens it from the start (a capture can't press F6). Test
      `TheInspector_HasTheLastSearch_BestFirst_WithItsLines`; captured on an AI-vs-AI game (turn 8, Ajani's
      Anguish's five targets ranked, the lines under them). The Space-to-pause of MtgGame's isn't copied: Space
      passes priority here; Tools → Pause AI does it

## Next

**Keep a flagged position first** (AI-14.6): the engine moves on, and flag 4's game had gone within the
hour. Committed with AI-13.4 (the option value; AI-9.2's taper dropped). Run `dotnet run -c Release --project MagicConsole --
--decisions` from the solution root and open the file it names (VS Code's preview). Shayne's second
flag (turn 15, "why block with the Cadet") was a double block with both Cadets that killed Edgar — the
per-creature report hid it; the replay shows the block as one line. One reading already: casting a
removal spell for its cantrip mode shows "our hand −5.3" — AI-13.4's option value at work (the
removal option is spent), not a bug.

## Log

- 2026-10-28: created from Shayne's request; AI-14.1 and 14.2 built.
- 2026-10-28: first reading — three flags (two real, one a report gap, fixed); the report made readable.
- 2026-10-28: the report rebuilt as a replay (Shayne); 508 lines for 2 games.
- 2026-11-10: AI-14.4 — the F6 inspector on MagicTable (overnight).
