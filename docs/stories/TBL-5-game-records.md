---
id: TBL-5
title: Game records — export a played game, replay it, compare its choices with the AI's
status: in-progress
area: TBL
created: 2026-10-20
---

## Why

Shayne's first playtest (TBL-2, 2026-10-20) turned up AI plays worth a test — and no seed had been
saved, so none can be replayed. He wants a game log he can export (seed, the actions both players
chose, the states they led to) to store here and analyse: for AI test cases, and to see where his
own choices differ from the AI's.

## Requirements

- [x] A played game exports to a file: the seed, both decks, every decision of both players, enough
      to rebuild every state — saved without having to remember (every game, or one key)
- [x] A record replays exactly in the console and in tests, and says so when the engine has moved
      (as the position suite's file does)
- [x] A position from a record becomes an AI test case in a step (AI-8 needs exactly this)
- [x] At each of the human's decisions, what the AI would have chosen — and how its scores rank the
      human's pick — reported for the whole game, by BOTH the search (the uncapped default, not the
      table's 1 s cap — 2026-10-23) and the oracle (PositionSuite's deeper search)
- [x] Undo and the sandbox never break a replay: undone moves leave the record, sandbox
      placements are recorded as moves

## Decisions

- 2026-10-20, Shayne: noted now, built in a later session.
- 2026-10-20, Shayne: **every game is recorded automatically**, written as it goes.
- 2026-10-20, Shayne: the comparison asks **both** the search and the oracle.
- 2026-10-20, Claude (defaults, Shayne may overrule): states are **rebuilt** from seed + decks +
  moves, with a fingerprint per position and one of the end — PositionSuite's format and its
  stale check, not a second one.
- 2026-10-21, Claude: a record keeps **every transition**, not decisions with AutoPass between —
  the table also passes by your stops (settings that change mid-game) and a manual tap keeps
  priority without settling; replaying tokens alone reproduces all of it. Each move says its kind
  (you, ai, stop, rule, sandbox, auto), so the comparison (TBL-5.6) takes only yours. Records go to **`game_records/`** at the solution root,
  untracked like `sim_results/`, named by date and decks; a record worth keeping becomes a
  tracked AI test case (requirement 3).
- 2026-10-21, Claude: files are named by **date and seed** (`2026-10-21_143005_seed5.json`), not
  decks — the table's decks have no names (pasted, sealed); the record holds both lists, and its
  `About` says which seat was human and which AI played.
- 2026-10-21, Claude: the table makes every move **from its token**, not from the AI's or the
  click's own state — two copies of a card are one token, and the replay picks the copy the token
  does, so the table must too.
- 2026-10-21, Shayne: a permanent a cost takes is told apart by id in `MoveKey` (MagicCore), so a
  token keeps WHICH one you clicked — fixed in the engine, not worked around in the table.
- 2026-10-22, Shayne: kept records live in **`MagicCore.Tests/Records/`** (tracked, embedded in
  the test assembly), and the one step is the console — `--record=FILE --keep=NAME --at=N` copies
  the record in and prints a test to paste into `AiPositionTests`, its control included.
- 2026-10-23, Shayne: the comparison (TBL-5.6) ranks your pick by the **oracle** (it scores every
  option) and says whether the **uncapped default search** would have played it (repeatable) — not
  the table's 1 s search, whose answer depends on the machine's load.
- 2026-10-23, Shayne: **resume a recorded game** (TBL-5.7) — play, see something odd, the fix
  lands, continue the same game from before the odd play to check it, without losing the game.

## Tasks

- [x] TBL-5.1 Interview Shayne on the open questions (2026-10-20)
- [x] TBL-5.2 The record: `GameRecord` in MagicCore — decks as card lists, seed, move tokens
      (`MoveTokens`, moved out of PositionSuite), fingerprints; JSON round trip; `GameRecordTests`,
      4/4 revert-checks (2236f47, 2026-10-21)
- [x] TBL-5.3 The table records every game into `game_records/` (undo truncates, sandbox recorded):
      `MagicTableManager.Record`/`RecordTo`, one `Play` for every move, `Settle` as one loop; two
      tests (a table game with every kind of move and an undo, replayed from its file; an AI game
      recorded to its end), 3/3 revert-checks; a headless run wrote its file to the solution root
      (103d38f engine, ef94d68 table — 2026-10-21)
- [x] TBL-5.4 Replay a record in the console; a stale record says so: `GameRecord.Transcript`
      (forced passes only with `--passes`), `MagicConsole --record=FILE`; stale → names the move,
      exit 1; an unreadable file says so. A headless table game (seed 5, autoplay to 6) replayed
      move by move; a corrupted copy named move 21. Tests: the transcript; a deck card that no longer
      exists is stale, not a crash — both revert-checked (fbd7c6f, 2026-10-22)
- [x] TBL-5.5 A position from a record → an AI test case in one step (for AI-8): the transcript
      numbers its moves (`#110`), `GameRecord.Before(at)`, `KeptRecords` (tests), `--keep=NAME
      --at=N` (refuses an existing name, a name that isn't an identifier, a move that isn't a
      decision, the wrong folder). The control is the AI that MADE the play — GreedyAi for the
      stand-in or a greedy table, else the default search (the table's 1 s cap isn't repeatable) —
      and says NOT A CONTROL when it no longer makes it (review). Checked end to end on a headless
      table game: the printed test pasted verbatim compiled and passed, failed with a wrong control;
      a corrupted kept record failed `EveryKeptRecord_StillReplays` by name. `Records/Sample.json`
      (that game, move #110) stays, so the embedding is tested — revert-checked by breaking the glob
      (267d330, 2026-10-22)
- [x] TBL-5.6 The comparison report: the oracle's ranking of your pick and the default search's
      choice at each of your decisions (sprint item 8) — built: `RecordComparison` (MagicCore/Ai),
      `MagicConsole --record=FILE --compare`; the oracle's reading of one position is
      `PositionSuite.Read`, the summary its `Judge`. A pick the oracle scores the same as its own is
      "a tie", counted both ways. `RecordComparisonTests`: a seat that plays as the oracle agrees at
      every decision and ranks first — revert-checked (the position after the move; a rank counting
      ties against you). Run on Shayne's sealed game of 2026-10-23 (findings)
- [x] TBL-5.7 Resume a record on the table from a chosen move (sprint item 4): a new
      `MagicTableManager` from the record's seed, decks and sideboards, its moves replayed through
      `Play` so the new record starts as a copy (written as a NEW file; the old one is kept). The
      cheapest form: replay the whole record with the undo history filled, so the existing Undo
      steps back to before the odd play. A fix to the AI replays exactly (moves are tokens, not
      re-decided — the fixed AI decides from there); a fix to the ENGINE can make an earlier
      position stale (`MoveTokens.StaleException`) — offer a lenient replay that keeps applying
      moves while they are legal and says where the first one differs. Entry: the deck select —
      built: `MagicTableManager.Resume`, "Resume a game" on the deck select (a file dialog on
      `game_records/`), `--resume=FILE`; `Resume_IsTheSameGame_WithTheSameUndoSteps` (revert-checked:
      a declaration as one step per creature fails it), `Resume_AStaleRecord_SaysWhereItDiffersAndStops`

## Next

**Every requirement is met** (TBL-5.1–5.7). `--record=FILE --compare` sets your choices beside the
AI's — ~20 s a game on 16 cores, so it needs no asking.

Open, for Shayne to decide (not tasks yet):
- **The oracle reads the opponent's REAL hand** (`Hidden.Hand.Real`, the search's default), so it
  judges a human knowing what they couldn't — "walks into a loss" may rest on a card you never saw.
  An honest reading is one setting (`Hand = Hidden.Hand.ShuffleUnseen` or `GuessedFromColors`, on
  both searches in `RecordComparison.Compare`); it changes what the search would do, so it is a
  decision, not a fix.
- Story done? Status stays `in-progress` until Shayne has read a report on one of his games.

## Log

- 2026-10-20: created from Shayne's first playtest session (TBL-2).
- 2026-10-20: interviewed; status → ready.
- 2026-10-21: TBL-5.2 — `MoveTokens` (tap and sandbox tokens added) and `GameRecord`; PositionSuite
  forwards to them and its suite still replays unchanged. Status → in-progress.
- 2026-10-21: TBL-5.3 — the table records every game. Its first test never attacked (the lazy
  human casts no creatures before turn 9 on seed 5) until a precondition asked; checked, not assumed.
  Review: `MoveKey` keyed a cost's fodder by NAME, so a token couldn't say WHICH of two same-named
  permanents you clicked, and the table (playing from tokens) paid with the copy `DistinctMoves`
  kept. Shayne: fix it in MagicCore — `Fodder` now uses `Identity` (a permanent by id, elsewhere by
  name); `FraSacrificeTests.DistinctMoves_KeepsEachPermanentACostCanTake`, revert-checked. The
  position suite was regenerated and re-read by the oracle (`docs/findings/magic-ai.md`) — 103d38f;
  the table's recording is the commit after it.
- 2026-10-22: TBL-5.4 — the console replays a record. A sandbox put now reads "Put X into Hand (AI)"
  — whose zone, since either seat can be the target (review); the table's own log line is unchanged.
- 2026-10-22: TBL-5.5 — a recorded position becomes an AI test case in one console step.
- 2026-10-23: sprint planning — TBL-5.7 (resume a record) added; TBL-5.6's searches decided.
- 2026-10-23: TBL-5.7 — resume a record. Shayne's sealed game of 2026-10-22 (seed 451259, 454
  moves) resumed on the table: every move replayed, the same win, Undo enabled.
- 2026-10-23: TBL-5.6 — the comparison report; requirement 4 reworded to the decided search (the
  uncapped default) and ticked. Shayne's sealed game (seed 934349): agreed with the oracle 38%, 63%
  counting ties; the search with the oracle 85% — `docs/findings/magic-ai.md`.
- 2026-10-23: requirements 1, 2, 3, 5 ticked (sprint housekeeping) — met by TBL-5.3, 5.4, 5.5, 5.3;
  the comparison (4) is TBL-5.6.
