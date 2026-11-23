---
id: DEV-8
title: Tests out of the flow — the slice a change touches while working, the full suite in the background at a story's end
status: done
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

- [x] While working, the tests a change needs finish in under 2 minutes
- [x] The full suite and the golden replays run once at a story's end (or before a merge), in the background, while
      work continues — never as a wait in the middle of a step
- [x] A commit still runs the slice that covers what it changed (`/pre-commit` says which)

## Decisions

- 2026-11-18, Shayne (sprint 6 planning): the full suite and golden replays in the background at a story's end, not
  before every commit — "this makes more sense to me"; a commit runs its slice.
- 2026-11-19, Shayne approved (after DEV-8.1): the path-to-slices map as proposed, and a script for it rather than a
  table — `tools/test_changed.py`. While working, an AI change runs the test classes named after its files and the AI's
  golden replay; at a commit, the whole AI slice. MtgSimulator.Tests, the old engine's tests and `CardReplayTests` run
  only in the full suite at a story's end. A card batch keeps its own checks (`tools/batch_checks.py`).

## Tasks

- [x] DEV-8.1 Measure where today's wait goes: the build, each project, the slowest fixtures (DEV-7.1's method), the
      golden replays — and which projects a typical AI / table / card change actually touches
- [x] DEV-8.2 The map from changed paths to the projects and slices to run (DEV-7.2's map, extended to every project);
      `/pre-commit` and `Commands.md` use it
- [x] DEV-8.3 The end-of-story full run in the background (with `RunProgress`-style progress), its result read before
      the story closes or a merge
- [x] DEV-8.4 The slowest slices made faster where the measurement points (fixtures, golden replays)

## Next

Done. While working: `python -u tools/test_changed.py --run` (an engine change 85 s, a table or AI change ~1 min); at a
commit `/pre-commit` adds `--commit`; at a story's end `--full` in the background (5.9 min). If the AI slice (~3 min,
nearly the whole suite) should shrink next: a trx run names its new longest class (DEV-7.1's method).

## Log

- 2026-11-18: created at the sprint 5 review.
- 2026-11-18: DEV-8.1 done (`docs/findings/test-suite.md`): quiet, the solution's tests are 308 s wall (MagicCore 245 s
  alone, MtgSimulator 95, SQGodotCommon 54), the build 8-17 s; the AI slice 237 s with `AiPositionTests` (148 s, in
  order) its floor; `CardReplayTests` 177 s. Sessions since DEV-7 blocked 35 min each on tests (226 calls; whole-project
  runs 6-9 min when shared) - the "20 minutes" is a chain of foreground runs, not one. No commit since 2026-10-20 touched
  the old engine's projects.
- 2026-11-19: DEV-8.2 done: `tools/test_changed.py` (`--selftest`; `--paths` to ask about any file), `/pre-commit` runs
  it with `--commit`, `Commands.md`'s two filter lines replaced by it; `batch_checks.dotnet_test` takes a project. Timed:
  an AI change (two files) 1.1 min, a table change 1.1 min, an engine change 2.1 min. The MagicTable tests share
  SQGodotCommon.Tests' one namespace, so the slice lists the folder's classes by name.
- 2026-11-19: DEV-8.3 done: `python -u tools/test_changed.py --full` — the solution built, its five test projects
  together with each result printed as it lands, then `CardReplayTests`; ends `FULL: all passed | FAILED`. First run:
  all passed, 9.8 min (the together run 6.4 min, card replays 3.3). The stories README (a story is `done` only after it
  passes), `two-lanes.md` (after a merge), `/pre-commit` and `Commands.md` name it.
- 2026-11-19: DEV-8.4 built, timing pending: `AiPositionTests` (each test its own instance, `FixtureLifeCycle`),
  `CardReplayTests`, `RandomGameTests` and `GreedyAiReplayTests` run their tests in parallel (`ParallelScope.All` - a
  class's own attribute replaces the assembly's `Fixtures`, so `Children` alone would serialise the class itself);
  `GoldenReplay` reads its file under a lock and collects fresh lines in a `ConcurrentDictionary`. Checked with the
  cube lane's batch running (correctness only): MagicCore 2,013 passed, 2 skipped, as before; all three golden files
  regenerated in update mode, byte-identical. Indicative only (shared machine): `CardReplayTests` 80 s (177 s quiet
  before), the suite 4 m 17 s.
- 2026-11-19: DEV-8.4 timed on a quiet machine, three rounds (`docs/findings/test-suite.md`): MagicCore.Tests 245 ->
  192 s, the AI slice 237 -> 195 s, an engine change 126 -> 85 s (under the 2 minutes), `CardReplayTests` 177 -> 50 s;
  counts unchanged. `--full` all passed in 5.9 min (9.8 before). DEV-8 closed.
- 2026-11-19: the pre-commit review's fixes to `test_changed.py`: an engine change also runs the AI's golden replay and builds MagicConsole; the kept records run every class that replays them; the shared test helpers run all of MagicCore.Tests; the engine and AI slices together run as one call (the classes folded into the engine's filter likewise); Windows paths. `GoldenReplay` reads its file through `Lazy`.
