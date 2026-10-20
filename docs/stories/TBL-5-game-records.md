---
id: TBL-5
title: Game records — export a played game, replay it, compare its choices with the AI's
status: backlog
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
      human's pick — reported for the whole game

## Decisions

- 2026-10-20, Shayne: noted now, built in a later session.

Open questions:
- "Resulting states": stored, or rebuilt from seed + moves (the engine is deterministic, and the
  position suite stores only fingerprints)?
- Where records live (a folder in the repo, `user://` copied over) and how they are named
- Which AI the comparison asks (the search as it plays, the oracle, both)

## Tasks

- [ ] TBL-5.1 Interview Shayne on the open questions; propose the format

## Next

Start from what exists: `MagicCore/Ai/PositionSuite.cs` already records games as decks + seed + move
tokens (an index among the distinct moves, or a choice's ids), with a fingerprint per position so a
stale file says so; `AiMatch` and the table both drive games decision by decision. Read
`.claude/rules/magic-table.md` for where the table's game loop lives.

## Log

- 2026-10-20: created from Shayne's first playtest session (TBL-2).
