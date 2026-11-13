---
id: DEV-5
title: One script for a card batch's end-of-batch checks, one summary out
status: in-progress
area: DEV
created: 2026-11-07
---

## Why

A session's cost is roughly calls × context: in MC-5.6 batch 3b, 77% was cache reads (203 calls, median
context 251k) and output only 12%. Every card batch ends with the same checks, each run and read by hand:
the golden replays with the new cards taken out (`#if false`, two runs, restore), regenerated with them in,
the revert-checks, the fuzz probe at 10 then 150 games, the AI bench against HEAD, the full suite, the docs
check — ~15–25 calls a batch, each re-reading a ~250k context (~0.4–0.6M a batch). The one-of-a-kind TRK
cards made the checks cost about the same for 3 cards as for 30: 0.14M a card in the 28+ card batches, 0.53M
in the rest. Shayne, 2026-11-07: a prerequisite before any new set.

## Requirements

- [x] `tools/batch_checks.py` (name open): given the batch's new card names, runs in order — build; the
      golden replays with those cards' scripts taken out (and put back, always, as `revert_check.py`
      restores); the probe at 10 then 150; the bench; the full suite — and prints ONE short summary: each
      step's pass/fail, counts, which golden games moved and their trace paths, the probe's per-card lines.
- [x] Stops at the first failure with the lines that matter, never a whole log.
- [x] A hang is caught (a timeout per step, the hung test named) — batch 3b's seed 21 cost ~30 calls to find.
- [x] `/card-batch` step 3 uses it; the long runs still ask Shayne first (memory: ask before long runs).
- [ ] Measured: calls and total of a batch before and after (`docs/findings/card-batch-usage.md`).

## Decisions

- 2026-11-07, Shayne: noted as a story; done **before any new set is built** (with DEV-4).

- 2026-11-12, Claude (cube lane, overnight; for review): **the cards are taken out by a switch, not `#if false`** —
  `MAGIC_UNSCRIPTED="A;B"`, read once where `CardRegistry` merges the sets' scripts: nothing to restore, so a
  killed run can't leave a file half-reverted, and it works whichever file a set's scripts are in. A name that
  isn't scripted throws (a typo would leave the card in and read as an engine change). The one env var MagicCore
  reads; nothing outside the tests sets it. **Shayne, 2026-11-12: taken.** The run (~15 min) is asked once per batch,
  then run.
- 2026-11-12, Claude: a hang is named by `dotnet test --blame-hang` ("The test running when the crash
  occurred"), the step's timeout kills the tree; a probe hung at 10 games times each card alone. The bench runs
  only with `--bench` (the skill's "hot path touched"); `--from STEP` resumes after a fix.
- 2026-11-12, Claude: `CardReplayTests` is `[Explicit]` — an OR filter with the other golden fixtures doesn't
  select it (the run counted 162, not ~470), so it is a run of its own in both golden steps.

## Tasks

- [x] DEV-5.1 The script, golden-with-cards-out first (the step most often done by hand) — checked failing:
      two TRK cards out moved 29 recorded games, each named with its window and trace file
- [x] DEV-5.2 The rest of the steps, the summary, the hang timeout — checked passing on two TRK cards: probe 10
      and 150 with their per-card lines, the full suite (1586), the docs check, 4.8 min. Not yet run for real:
      golden-in, revert, bench, and golden-out PASSING (needs a batch's new cards)
- [ ] DEV-5.3 `/card-batch` uses it; one batch measured before and after

## Next

DEV-5.3: the first card batch after this (MC-4's first LSVPower batch, cube lane) runs
`python -u tools/batch_checks.py "<its cards>" [--revert SPEC]` instead of the steps by hand — its golden-in,
revert and a passing golden-out are the parts not yet run for real; fix what they show. Then log the batch's
calls and total in `docs/findings/card-batch-usage.md` beside the TRK rows (with DEV-4.3).

## Log

- 2026-11-07: created — Shayne, after TRK's usage review; a prerequisite for the next set.
- 2026-11-12 (night, cube lane): DEV-5.1–5.2 — the script, the switch, the skill and Commands.md; checked on TRK cards.
