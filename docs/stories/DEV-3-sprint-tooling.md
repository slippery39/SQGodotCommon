---
id: DEV-3
title: Sprint tooling — a /sprint skill and the story script's mechanical checks
status: backlog
area: DEV
created: 2026-10-23
---

## Why

Sprint 2's planning (2026-10-23) took a whole session, most of it mechanical: grouping 58 commits
by story, cross-checking hashes, finding stories whose status no longer matched their tasks — about
30 tool calls by hand. That repeats every Thursday. The mechanical half belongs in a script and the
judgment half in a skill.

## Requirements

- [ ] `/sprint` (a skill in `.claude/skills/`, beside `/pre-commit`), two modes:
      - **close**: the sprint's commits by story, each plan item done or slipped, what
        `docs/findings/` and `DesignNotes.md` gained in the range, the Review section written —
        asking Shayne what share of the usage budget the sprint took (Claude can't see usage, and
        without it sizes never calibrate)
      - **plan**: candidates from open stories and recent DesignNotes entries; the questions that
        settled sprint 2 (deadlines and events, what needs Shayne at the table, what needs long
        runs, what slips first); the next sprint file written and the README's link moved — and
        it stops for Shayne's yes
- [ ] `tools/stories.py --sprint`: the current sprint's items, each task ticked or open — "what's
      next" at a session's start, and half the review
- [ ] `tools/stories.py --since DATE`: commits grouped by story ID, the untagged ones apart
- [ ] Drift warnings in `--check`: `ready`/`backlog` with ticked tasks (should be `in-progress`), a
      `done` story with open tasks, every task ticked but not `done`, "uncommitted" left in a story.
      Each in the self-test

## Decisions

- 2026-10-23, Shayne: noted now, implemented as a story in the future — not in sprint 2.
- 2026-10-23, Claude (proposed at sprint planning): **no `/story` skill** — DEV-1 deferred one until
  the manual steps repeat; what repeated was drift-checking, which the script covers. Creating a
  story or a task is the README's procedure, and a skill would be a second copy of it.

## Tasks

- [ ] DEV-3.1 The script's additions (`--sprint`, `--since`, drift warnings), self-tested — S
- [ ] DEV-3.2 The `/sprint` skill, used for one real close and plan — S

## Next

Read `docs/stories/README.md` ("Sprints"), `docs/sprints/` (sprint 2's plan is the shape to
produce), `tools/stories.py` and `.claude/skills/pre-commit/SKILL.md` (a skill's form here). The
sprint 1 review and sprint 2 plan, done by hand, are the reference for what each mode outputs.

## Log

- 2026-10-23: created at sprint 2's planning (Shayne: note it, a story for the future).
