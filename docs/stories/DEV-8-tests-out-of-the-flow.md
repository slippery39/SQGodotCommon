---
id: DEV-8
title: Tests out of the flow — the slice a change touches while working, the full suite in the background at a story's end
status: ready
area: DEV
created: 2026-11-18
---

## Why

Shayne, sprint 5 review (2026-11-18): "the amount of time we have to wait for simulation runs, and tests, and golden
replays. Although we sped it up, it still is just taking way too long. I do want to make sure we aren't adding in a
bunch of errors, but waiting 20 minutes for tests kind of breaks the flow."

DEV-7 parallelised MagicCore.Tests and wrote the slices into `Commands.md`, but the full `dotnet test` is still what
gets run. After the sprint 5 merge (2026-11-18) it read: MagicCore.Tests 2013 tests, 6 m 18 s; MtgSimulator.Tests
5 m 14 s; SQGodotCommon.Tests 3 m 44 s; MtgCore.Tests 1 s; ImmutableGameObjects.Tests 0.4 s — and most of MtgSimulator
and SQGodotCommon test the old MtgCore engine and the Godot layer, which this lane's AI work rarely touches.

## Requirements

- [ ] While working, the tests a change needs finish in under 2 minutes
- [ ] The full suite and the golden replays run once at a story's end (or before a merge), in the background, while
      work continues — never as a wait in the middle of a step
- [ ] A commit still runs the slice that covers what it changed (`/pre-commit` says which)

## Decisions

- 2026-11-18, Shayne (sprint 6 planning): the full suite and golden replays in the background at a story's end, not
  before every commit — "this makes more sense to me"; a commit runs its slice.

## Tasks

- [ ] DEV-8.1 Measure where today's wait goes: the build, each project, the slowest fixtures (DEV-7.1's method), the
      golden replays — and which projects a typical AI / table / card change actually touches
- [ ] DEV-8.2 The map from changed paths to the projects and slices to run (DEV-7.2's map, extended to every project);
      `/pre-commit` and `Commands.md` use it
- [ ] DEV-8.3 The end-of-story full run in the background (with `RunProgress`-style progress), its result read before
      the story closes or a merge
- [ ] DEV-8.4 The slowest slices made faster where the measurement points (fixtures, golden replays)

## Next

Start at DEV-8.1. Read DEV-7 (done) and `Commands.md`'s test section first. A timing is a quiet-machine measurement
(`.claude/rules/two-lanes.md`).

## Log

- 2026-11-18: created at the sprint 5 review.
