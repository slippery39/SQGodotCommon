---
id: DEV-7
title: Faster tests while working — run what a change touches, the full suite at a story's end
status: ready
area: DEV
created: 2026-11-12
---

## Why

The full MagicCore suite takes 8–9 minutes (1,589 tests, measured 2026-11-12), and every step of a story ran it.
Shayne (2026-11-12): "Is there any way to split this up so that we can section certain parts of it off, so that we only
run tests which affect things that we touched? … And then run the full tests at the end of a story. If not possible or
if this might cause other issues then thats ok."

## Requirements

- [ ] One command runs only the tests a working-tree change can affect, and says which slices it picked and why.
- [ ] The full suite still runs at a story's end and before every commit (`/pre-commit` unchanged on that).
- [ ] The slowest tests are known (a timing list), so the slices are chosen on evidence, not guessed.
- [ ] A change to shared code (the engine, the evaluator) picks the slices that depend on it — never fewer tests than
      the change can break.

## Decisions

- 2026-11-12, Shayne: wanted, with the full suite kept for the end of a story; dropped if it causes other issues.
- 2026-11-12, Claude (read, not yet measured — the cube lane was building): **the suite runs one test at a time.**
  `MagicCore.Tests` has no `[Parallelizable]` anywhere, so NUnit runs all 1,589 tests serially on a 16-core machine.
  The engine is already safe for concurrent games (`AiMatch` runs games in `Parallel.For`; the evaluator's switches are
  `AsyncLocal`, the rollout's and mana's scratch `[ThreadStatic]`), so running FIXTURES in parallel
  (`[assembly: Parallelizable(ParallelScope.Fixtures)]`) is likely the biggest win and smaller than any slicing script.
  Risks found: one wall-clock assertion (`FraEngineTests`: a mana search under 500 ms) — split off 2026-11-12 (Shayne) as an
  explicit `Performance` test, the answer check left in the suite; the
  golden replays write per-key files to the work directory (no clash) and their table only when updating; fixtures
  that already run games in parallel inside would compete for cores (fine, or `[NonParallelizable]`). No test changes
  the working directory (that trap is MtgSimulator.Tests').
- 2026-11-12, Claude: **the slices, if still needed after parallel runs** — one assembly, so `--filter` by namespace:

  | Changed | Run |
  |---|---|
  | the engine (`MagicCore/*.cs`, `Rules`, `Effects`, `Actions`, `Cards`, `Filters`, `Mana`, `Targeting`, `Turns`, `Zones`) | everything — every test stands on it |
  | a set's card scripts (`MagicCore/Sets/Fra/`, `Sets/Trk/`, a cube's) | that set's `Sets` tests, `Ai` (values tables, positions), the golden replays |
  | `MagicCore/Ai/` | `Ai`, `Limited`, the root tests (golden replays, probes), and 15 `Sets` files that call the AI |
  | `MagicCore/Limited/` | `Limited`, `Ai` |
  | `MagicConsole/`, `tools/`, docs | none of MagicCore.Tests |

  An AI change still runs most of the cost if the AI tests are the slow part — DEV-7.1's timings decide.

Open questions:
- None before DEV-7.1's timings: parallel first, slices only if a story's loop is still slow.

## Tasks

- [ ] DEV-7.1 Time the suite per fixture and namespace (`dotnet test --logger trx`), the slowest 20 listed — on a QUIET
      machine; then the same suite with fixtures in parallel (the counts identical, every test passing, three runs)
- [ ] DEV-7.2 The slices (NUnit `--filter` by namespace or `[Category]`) and the map from changed paths to slices
- [ ] DEV-7.3 `tools/test_changed.py` (or a `Commands.md` line if a filter is enough); `/pre-commit` and the stories
      README say when the full suite runs

## Next

DEV-7.1 on a quiet machine (not while the cube lane builds): the serial timings, then fixtures in parallel (Decisions).
Tests per folder: Sets 827, Ai 272, Limited 63, Turns 55, Mana 17, Filters 17, Cards 12, root 6.

## Log

- 2026-11-12: created from Shayne's sprint-5 notes.
- 2026-11-12: the static half read (Decisions): the suite runs serially; the folder-to-code map written.
- 2026-11-12: the Molten Tide timing check split off as `[Explicit]`, `Category("Performance")` (Shayne); `Commands.md` has the filter.
