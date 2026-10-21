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

## Tasks

- [x] TBL-5.1 Interview Shayne on the open questions (2026-10-20)
- [x] TBL-5.2 The record: `GameRecord` in MagicCore — decks as card lists, seed, move tokens
      (`MoveTokens`, moved out of PositionSuite), fingerprints; JSON round trip; `GameRecordTests`,
      4/4 revert-checks (uncommitted, 2026-10-21)
- [ ] TBL-5.3 The table records every game into `game_records/` (undo truncates, sandbox recorded)
- [ ] TBL-5.4 Replay a record in the console; a stale record says so
- [ ] TBL-5.5 A position from a record → an AI test case in one step (for AI-8)
- [ ] TBL-5.6 The comparison report: the search's and the oracle's choice at each human decision

## Next

**TBL-5.3, the table records.** `GameRecord` (MagicCore/Ai) holds EVERY transition, so the manager
must record each one where it makes it: `StepAi` (kind `ai`, `MoveTokens.Token(State, decision)`),
`Act`/`Choose` (`you`; a manual tap is a `t` token), each `Choose` inside `DeclareCombat` (commit them
only if the declaration is accepted), `Sandbox` (`MoveTokens.Sandbox`), `AutoplayTo` (`auto`), and
`Settle` — rewrite it as one loop: a lone legal pass → `rule`; your seat not at a stop → `stop`
(`Stops.StopsAt`); else done. `Undo` truncates the record to the count saved with the undo step.
`GameRecordTests.PlayRecorded` is the model loop. Then write it after every decision to
`game_records/` at the solution root (gitignore it, like `sim_results/`).

## Log

- 2026-10-20: created from Shayne's first playtest session (TBL-2).
- 2026-10-20: interviewed; status → ready.
- 2026-10-21: TBL-5.2 — `MoveTokens` (tap and sandbox tokens added) and `GameRecord`; PositionSuite
  forwards to them and its suite still replays unchanged. Status → in-progress.
