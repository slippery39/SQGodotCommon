---
id: DEV-6
title: Two lanes at once — a second worktree for the cube, merged back often
status: ready
area: DEV
created: 2026-11-11
---

## Why

Shayne (2026-11-11): "I want to be able to work on two things at the same time in this project" — the AI, UI and
sealed testing on one side, new sets (the cube, MC-4) on the other — "kind of like working with multiple people in
typical software dev, but this is all happening on one PC", each in its own VS Code window and branch, merged when
done. He asked what could go wrong: conflicts, and Claude's memory not applying to the second folder.

## Requirements

- [ ] A second working folder on its own branch, opened in its own VS Code window, sharing this repository.
- [ ] Claude in the second folder has the same memory (feedback rules, project notes) as this one.
- [ ] Both folders' Claude usage is counted in the sprint's usage report.
- [ ] A short written rule for working in two lanes (below), where every session reads it.

## Decisions

- 2026-11-11, Shayne + Claude: **git worktrees, not clones** — one repository, a second folder
  (`git worktree add ..\SQGodotCommon-cube -b mtg-cube mtg-real`): both see each other's commits at once, merging is a
  plain `git merge`, and git refuses the same branch in two folders.
- 2026-11-11, Shayne: set it up **tomorrow** (2026-11-12), not during AI-19's scan.

Claude's notes on the risks, each with its mitigation (to become the written rule):
- **Memory is per folder path** (`~/.claude/projects/<path>/memory/`): a session in the new folder likely starts with
  none. Check first; if so, a directory junction (`mklink /J`) from its memory folder to this one's — both sessions
  then read and write one memory. What is in git (CLAUDE.md files, `.claude/rules`, skills, hooks) is shared already.
- **Generated files conflict** (`AbilityCatalog.md`, card sheets, coverage, `*.values.json`, `FRA.positions.txt`, the
  golden replays, the story table): never resolved by hand — take either side, then regenerate after the merge.
- **Story IDs:** each lane owns its areas — the cube lane `MC-`, this lane `AI-`, `TBL-`, `LIM-`, `DATA-` — so the
  "next free id" never clashes. The shared docs ("Next up", the sprint file, `CLAUDE.md`) merge as text.
- **The engine is the real overlap:** cube pieces and MC-6's generalising can move how FRA and TRK cards play, which
  AI measurements stand on. Merge often and small; the full tests and the position suite after every merge; an engine
  fix both lanes need is made in one and merged into the other at once.
- **Not in git, so per folder:** `sim_results/` (run `/regen-card-values` once — four tests need it), `ai_reports/`,
  `game_records/`, Godot's `.godot/` import cache (the first open reimports). Both folders share Godot's `user://`
  (same project name): F7 scenarios and settings are shared.
- **The machine:** sims use all 16 cores and a game's budget is wall-clock (`SimulatedRatings.GameBudget`), so a
  build or test run in the other window can stop more games and change a run's result — no long sims while the other
  lane builds heavily; a run's progress line counts the stopped games.
- **The weekly Claude budget is shared** between lanes; `tools/usage_report.py` reads only this folder's sessions (its
  `PROJECT` path) — it must read both.
- `Commands.md` mixes CRLF and LF: check `git diff --stat` after merging it.

Open questions:
- Does Claude Code already share auto-memory across worktrees of one repository? Test before adding the junction.
- Where the written rule lives: the root `CLAUDE.md` (every task) or `Commands.md`.

## Tasks

- [ ] DEV-6.1 The worktree `C:\SQGodotHelperApps\SQGodotCommon-cube` on a new branch `mtg-cube`, off `mtg-real`
- [ ] DEV-6.2 Memory in the new folder: test, then the junction if needed — checked by a session there quoting a
      memory rule
- [ ] DEV-6.3 `tools/usage_report.py` reads both folders' sessions
- [ ] DEV-6.4 The two-lane rule written (from Decisions above), and `/regen-card-values` run in the new folder

## Next

Start 2026-11-12 with DEV-6.1. Commit or stash this folder's work first: a worktree starts from a commit, so
uncommitted changes here don't appear there.

## Log

- 2026-11-11: created from Shayne's question about working on two things at once; set up tomorrow.
