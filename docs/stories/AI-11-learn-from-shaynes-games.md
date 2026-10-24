---
id: AI-11
title: Learn from Shayne's games — settle where he and the oracle differ, keep what he got right
status: backlog
area: AI
created: 2026-10-24
---

## Why

TBL-5.6's comparison on Shayne's sealed game of 2026-10-23 (`game_records/2026-10-23_230227_seed934349.json`,
he won on turn 33; `docs/findings/magic-ai.md`) agreed with the oracle on 38% of his decisions (63%
counting ties). The oracle can't say who was RIGHT: it grades by its own scores, so it never rates a
pick above its own. Shayne wants his play and the oracle's set against each other to find out which
was better, and what's learned used to make the AI better.

This story is the loop: settle the disputes, keep the positions he got right as AI test cases,
diagnose each. **The fixes live in the stories that own them** — when to cast bounce and removal in
AI-4 (held spells as options; Shayne's cases are there), the passive simulated opponent in AI-10,
resource prices in AI-9, a card's threat over time in AI-12. This story's tests are how those fixes
are measured.

## Requirements

- [ ] A disputed position (from `--compare`) is SETTLED: Shayne's move and the oracle's each played
      to the game's end, the search playing both sides, over many samples paired by shuffle; the
      verdict is his move, the oracle's, or no clear difference
- [ ] Positions where his move wins clearly become AI test cases (`--keep`), each with its evidence
      and a diagnosis handed to the story whose fix it is

## Decisions

- 2026-10-24, Shayne: create the story; don't build yet.
- 2026-10-24, Shayne: **settling keeps both hands REAL** — the opponent played its cards from the
  hand it held; another hand would have played out differently. **Libraries are shuffled only where
  unknown**: a card whose position is known (surveilled to the top, put on the bottom) stays where
  it is — what `Hidden.Library.ShuffleUnknown` already does.
- 2026-10-24, Shayne: his plays aren't always correct — the settle decides, not either side's say.
- 2026-10-24, Shayne: the bounce and removal timing goes to AI-4, the passive opponent to AI-10 —
  this story keeps the settle and the learning loop.

Open questions:
- The playout player for settling: the default search on both sides (Claude's recommendation), or
  the oracle (stronger, ~10× slower)?
- How many samples and which positions: Shayne's 9 big calls (~8 min) or every real dispute (~37,
  ~30 min) at 20 a move — a tested difference then shows at ~20–25pp
- A playout is only as good as the search playing it: a held Unsummon the search then misuses
  favours the moves it already handles. "No clear difference" is the honest verdict there.

## Tasks

- [ ] AI-11.1 The settle tool: `--record=FILE --settle`, his 9 big calls first (ask before a run
      over ~15 min)
- [ ] AI-11.2 Keep what he won as tests; diagnose each, and hand it to its story

## Next

**Not to be built until Shayne says.** Read first: `RecordComparison` (TBL-5.6 — the disputes),
`PositionSuite` (`Read`, `Judge`), `Hidden.cs` (`ShuffleUnknown`, `Sample`), `AiMatch` (games played
to the end in parallel), `SealedBuildMatch` (paired games, LIM-1.8).

## Log

- 2026-10-24: created from the TBL-5.6 report on Shayne's sealed game.
- 2026-10-24: restructured the same day — Shayne's bounce and removal cases moved to AI-4, the
  passive-opponent cause to AI-10; this story keeps the settle and the loop.
