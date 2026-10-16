---
id: AI-2
title: A search AI for MagicCore — port the multi-turn beam search
status: backlog
area: AI
created: 2026-10-09
---

## Why

MagicCore has only the one-ply `GreedyAi`: no turn planning, no holding up mana, a passive opponent
in its simulation, and it loses slow races to evasive fliers (`DesignNotes.md`, "MagicCore known
ceilings"). MtgCore's game already has a much stronger AI — `MultiTurnBeamSearchAiStrategy`
(`MtgSimulator/`, 1,455 lines; MtgCore's default, `MtgGameManager.cs:84`): a beam over the current
turn plus greedy rollouts of future turns, with choices branched. It was never ported.

## Requirements

- [ ] A search AI that plans the turn through MagicCore's priority, stack and choices
- [ ] Beats `GreedyAi` by a measured margin, 25+ games per deck pairing, recorded in `docs/findings/magic-ai.md`
- [ ] Fast enough to play against on MagicTable (budget to be set)

## Decisions

- 2026-10-09, Shayne: after the UI, his choice between a search AI and sealed/draft (LIM-1).
- Copy and own from MtgSimulator; MtgCore and MtgSimulator are not modified.

Open questions:
- Turn budget for a move on MagicTable?
- Do AI-1's evaluator changes come first, so the search scores with them?

## Tasks

- [ ] AI-2.1 Read `MultiTurnBeamSearchAiStrategy.cs` and bring a porting plan — before any code

## Next

`.claude/rules/sim-ai.md` loads with the MtgSimulator AI files and explains them. MagicCore's
boundary is `MagicRules` (`LegalActions` / `DistinctMoves`, `GetPendingChoice` / `Choose`, `Act`).

## Log

- 2026-10-16: created from the old handoff's "Later" list.
