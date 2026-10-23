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

- [ ] A played game exports to a file: the seed, both decks, every decision of both players, enough
      to rebuild every state — saved without having to remember (every game, or one key)
- [ ] A record replays exactly in the console and in tests, and says so when the engine has moved
      (as the position suite's file does)
- [ ] A position from a record becomes an AI test case in a step (AI-8 needs exactly this)
- [ ] At each of the human's decisions, what the AI would have chosen — and how its scores rank the
      human's pick — reported for the whole game, by BOTH the search (as the table plays it) and
      the oracle (PositionSuite's deeper search)
- [ ] Undo and the sandbox never break a replay: undone moves leave the record, sandbox
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
- [ ] TBL-5.6 The comparison report: the oracle's ranking of your pick and the default search's
      choice at each of your decisions (sprint item 8, if budget is left)
- [ ] TBL-5.7 Resume a record on the table from a chosen move (sprint item 4): a new
      `MagicTableManager` from the record's seed, decks and sideboards, its moves replayed through
      `Play` so the new record starts as a copy (written as a NEW file; the old one is kept). The
      cheapest form: replay the whole record with the undo history filled, so the existing Undo
      steps back to before the odd play. A fix to the AI replays exactly (moves are tokens, not
      re-decided — the fixed AI decides from there); a fix to the ENGINE can make an earlier
      position stale (`MoveTokens.StaleException`) — offer a lenient replay that keeps applying
      moves while they are legal and says where the first one differs. Entry: the deck select

## Next

**TBL-5.7 first** (sprint item 4, `docs/stories/README.md`), then TBL-5.6. For TBL-5.7 read
`MagicTableManager.cs` (the constructor, `Play`, `Undo`, `Replay`) and `MagicCore/Ai/GameRecord.cs`
(`Replay`, `Before`, the stale check).

**TBL-5.6, the comparison report** (requirement 4). For each `you` move of a record (`Kind ==
"you"`; a combat declaration is several — one per creature): the position (`Before`), what the
search plays there and what the oracle plays, and where YOUR pick ranks among the oracle's scores.
The scoring exists: `PositionSuite.WithOracle` runs `new BeamSearchAi(p, PositionSuite.OracleSettings)`
and reads `FirstMoveScores` (every first move searched — the default `beam` does not search them
all, so rank only by the oracle; for the search, say same/different). Parallel over positions
like `WithOracle`. A console flag beside `--keep` (`--record=FILE --compare`): one line per
decision where either differs, then a summary (agreed with search %, with oracle %, score given
up). The search is the uncapped default, not the table's (decided 2026-10-23).

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
