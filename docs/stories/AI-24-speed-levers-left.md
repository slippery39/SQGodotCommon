---
id: AI-24
title: The speed levers left over from AI-22 — tried, costed or proposed, each to decide later
status: backlog
area: AI
created: 2026-11-13
---

## Why

AI-22 made a sealed run ~11% faster with no move changed (the engine shortcuts) and found more levers that either
change the AI's moves or need more work first. Shayne (2026-11-13) closed AI-22 to move on: "keep the shortcuts that
worked and didn't cause any regressions in behavior. For the rest of them keep it documented and have a story to go
over them later." Every number is in `docs/findings/magic-ai.md` ("AI-22.1", "AI-22.2" and "shortcuts").

## Requirements

- [ ] Each lever below decided: made the default, dropped, or built — a lever that changes moves only with Shayne's
      read of the plays it changes and a match.
- [ ] Measured the AI-7 / AI-22 way: the position suite (decisions changed), the full tests, the golden replays, a
      sealed pair on a quiet machine; a match for anything that changes moves.

## Decisions

- 2026-11-13, Shayne: the engine shortcuts that change no move are kept (on, no switch); everything else waits here.

## The levers

**Built, OFF — they change some moves (switches in `AiMatch`; `--builds … --ai=NAME` runs a sealed test on one):**
- `beam+shortlist2` — the rollout re-judges its best 2 attack plans against the real blocks, not 3. Sealed −18%;
  match 50.0% over 192; suite identical; 4 of 16 TRK games split (Amok Time cast instead of a pass, Sylvia Tilly
  attacking, a Klingon Warrior token kept home, Cantankerous Captain not cast). Its comment: "best 1" broke Shayne's
  Jace-token lethal (that test passes at 2).
- `beam+freepasses2` — at most 2 free passes in a row, not 4. Cost −5.7%; match 50.5%; suite identical; 4 of 16 games
  split, each acting where today's AI passed (Q, Plasma Cascade at the face, Photosynthetic Physician) — AI-13.12's
  first-stop bias partly back.
- `+shortlist1`, `+freepasses1` — cheaper still, but shortlist1 misses a win and walks into two losses on the suite.

**Not built:**
- **No possible blockers** — skip the rollout's real-block re-check when the defender has no untapped creature that
  can block (~1 h with its match; the real defender can still cast a trick, so not exact).
- **Per-card mana answers cached** while nothing on the board changes mana — the profile's ceiling ~4–5%; the risk a
  stale cache after a mana-changing effect (Molten Tide, Rok-Tahk, lost abilities).
- **Skipping the state-based check when no event was staged** — 58% of checks, up to ~13%; but 0.4–0.7% of those
  still act (toughness ≤ 0 from −X/−X, a walker at 0 loyalty, a token gone): an engine audit first, so every such
  change stages an event.
- **Twin attack plans** — tried and reverted: identical creatures don't score identically in the look-ahead (its ties
  fall to the lower id), so dropping a twin's plan changed a decision (`TheCerritos_BlocksTheBorg…`). AI-21's, once the
  settle treats twins alike.
- **Reusing combat answers within a decision** — needs a state fingerprint the engine doesn't have.
- **A game's cap in engine steps, not minutes** (was AI-22.5) — sealed runs, the scan and self-play would give the same
  answer on a busy machine (`.claude/rules/two-lanes.md`: quiet-machine tasks become any-time ones).
- **The rollout's attack plans** (`WholeAttacks.Best`, 50% of a sealed decision) — AI-21's subject; the biggest lever.

## Tasks

- [ ] AI-24.1 Shayne reads the split plays (above) and picks: shortlist2 / freepasses2 on, or dropped
- [ ] AI-24.2 The step cap (sealed runs as any-time tasks)
- [ ] AI-24.3 No possible blockers, with its match
- [ ] AI-24.4 The state-based audit (every state change stages an event), then the skip

## Next

Nothing until Shayne picks it up. Read AI-22 (done) and the findings sections named in Why.

## Log

- 2026-11-13: created when AI-22 closed (Shayne).
