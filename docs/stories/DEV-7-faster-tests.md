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

Open questions:
- None before DEV-7.1's timings.

## Tasks

- [ ] DEV-7.1 Time the suite per fixture and namespace (`dotnet test --logger trx`), the slowest 20 listed
- [ ] DEV-7.2 The slices (NUnit `--filter` by namespace or `[Category]`) and the map from changed paths to slices
- [ ] DEV-7.3 `tools/test_changed.py` (or a `Commands.md` line if a filter is enough); `/pre-commit` and the stories
      README say when the full suite runs

## Next

DEV-7.1: time the suite. `MagicCore.Tests` folders: `Ai/`, `Limited/`, the card tests by set. A slow few (the
position suite, random play over every card) may be most of the 8 minutes — then the answer is slices, not a script.

## Log

- 2026-11-12: created from Shayne's sprint-5 notes.
