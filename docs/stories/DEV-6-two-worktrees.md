---
id: DEV-6
title: Two lanes at once — a second worktree for the cube, merged back often
status: in-progress
area: DEV
created: 2026-11-11
---

## Why

Shayne (2026-11-11): "I want to be able to work on two things at the same time in this project" — the AI, UI and
sealed testing on one side, new sets (the cube, MC-4) on the other — "kind of like working with multiple people in
typical software dev, but this is all happening on one PC", each in its own VS Code window and branch, merged when
done. He asked what could go wrong: conflicts, and Claude's memory not applying to the second folder.

## Requirements

- [x] A second working folder on its own branch, opened in its own VS Code window, sharing this repository.
- [x] Claude in the second folder has the same memory (feedback rules, project notes) as this one.
- [x] Both folders' Claude usage is counted in the sprint's usage report.
- [x] A short written rule for working in two lanes (below), where every session reads it.

## Decisions

- 2026-11-11, Shayne + Claude: **git worktrees, not clones** — one repository, a second folder
  (`git worktree add ..\SQGodotCommon-cube -b mtg-cube mtg-real`): both see each other's commits at once, merging is a
  plain `git merge`, and git refuses the same branch in two folders.
- 2026-11-11, Shayne: set it up **tomorrow** (2026-11-12), not during AI-19's scan — then the same evening: **now**,
  and **on D:** (C: is 98% full): `D:\GameDev\SQGodotCommon-cube`, beside his other game projects. Builds, `.godot/`,
  `sim_results/` and records grow there; only `.git` (one, 249 MB) and Claude's logs stay on C:.
- 2026-11-11, Shayne: **uncommitted data files are not shared** for now — `sim_results/` is a measurement of one
  branch's engine, so sharing it would hand one lane numbers from the other's code. A junction later, per folder, only
  for one that is not regenerated.
- 2026-11-11: the rule lives in `.claude/rules/two-lanes.md` with no `paths:` (loads every session; checked by a
  quote) — the root `CLAUDE.md` is at its 200-line budget.

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

- **Losing work:** `git worktree add` touches nothing here. Work is lost only by `git worktree remove --force` (or
  deleting the folder) with uncommitted changes, or `git branch -D` on an unmerged branch; and both folders share one
  `.git`, so neither is a backup of the other — push both branches. Moving this folder breaks the D: one until
  `git worktree repair`.

## Tasks

- [x] DEV-6.1 The worktree `D:\GameDev\SQGodotCommon-cube` on a new branch `mtg-cube`, off `mtg-real` (pushed first, e3746597)
- [x] DEV-6.2 Memory in the new folder: **shared already, no junction** — a session there quoted the
      MEMORY.md line word for word, and Claude made no memory folder of its own for it (session logs ARE per folder)
- [x] DEV-6.3 `tools/usage_report.py` reads every worktree's sessions (`git worktree list`): `--all` tags each lane; the default and `--log` stay this folder's
- [x] DEV-6.4 The two-lane rule written: `.claude/rules/two-lanes.md`
- [ ] DEV-6.5 The first session in the cube folder: `git merge mtg-real` (picks up the rule once committed), a
      build, the full tests, `/regen-card-values`

## Next

Commit this step (the rule, `usage_report.py`, this story) on `mtg-real`. Then DEV-6.5 in a VS Code window on
`D:\GameDev\SQGodotCommon-cube`: `git merge mtg-real` there (a fast-forward), build, full tests, `/regen-card-values`.
The story is done when that passes.

## Log

- 2026-11-11: created from Shayne's question about working on two things at once; set up tomorrow.
- 2026-11-11 (evening): set up now instead — `mtg-real` pushed, the worktree on D:, memory found shared, the usage
  report reads both lanes, the rule written and checked to load.
