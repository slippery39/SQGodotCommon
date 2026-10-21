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

## Tasks

- [x] TBL-5.1 Interview Shayne on the open questions (2026-10-20)
- [x] TBL-5.2 The record: `GameRecord` in MagicCore — decks as card lists, seed, move tokens
      (`MoveTokens`, moved out of PositionSuite), fingerprints; JSON round trip; `GameRecordTests`,
      4/4 revert-checks (2236f47, 2026-10-21)
- [x] TBL-5.3 The table records every game into `game_records/` (undo truncates, sandbox recorded):
      `MagicTableManager.Record`/`RecordTo`, one `Play` for every move, `Settle` as one loop; two
      tests (a table game with every kind of move and an undo, replayed from its file; an AI game
      recorded to its end), 3/3 revert-checks; a headless run wrote its file to the solution root
      (uncommitted, 2026-10-21)
- [ ] TBL-5.4 Replay a record in the console; a stale record says so
- [ ] TBL-5.5 A position from a record → an AI test case in one step (for AI-8)
- [ ] TBL-5.6 The comparison report: the search's and the oracle's choice at each human decision

## Next

**TBL-5.4, replay a record in the console.** A `--record=<file>` flag in `MagicConsole/Program.cs`
(beside `--suite=`, `--match=`): `GameRecord.FromJson`, then `Replay()`; print each move as
`T<turn> <player> [<kind>] <MoveTokens.Describe(before, token)>`, skipping `rule`/`stop` passes
unless asked, and the end. A `StaleException` prints its message (it names the move) and exits
non-zero. A real record to try it on: play a few turns on the table — it writes
`game_records/<date>_seed<N>.json`. Update `RunningSimulations.md` if it lists MagicConsole's flags.

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
