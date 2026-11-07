---
id: DEV-5
title: One script for a card batch's end-of-batch checks, one summary out
status: backlog
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

- [ ] `tools/batch_checks.py` (name open): given the batch's new card names, runs in order — build; the
      golden replays with those cards' scripts taken out (and put back, always, as `revert_check.py`
      restores); the probe at 10 then 150; the bench; the full suite — and prints ONE short summary: each
      step's pass/fail, counts, which golden games moved and their trace paths, the probe's per-card lines.
- [ ] Stops at the first failure with the lines that matter, never a whole log.
- [ ] A hang is caught (a timeout per step, the hung test named) — batch 3b's seed 21 cost ~30 calls to find.
- [ ] `/card-batch` step 3 uses it; the long runs still ask Shayne first (memory: ask before long runs).
- [ ] Measured: calls and total of a batch before and after (`docs/findings/card-batch-usage.md`).

## Decisions

- 2026-11-07, Shayne: noted as a story; done **before any new set is built** (with DEV-4).

Open questions:
- Taking the new cards out: `#if false` around the batch's section of the Cards file (by marker comment), or
  a test-only switch that leaves named cards unscripted — the second needs no edit to restore.

## Tasks

- [ ] DEV-5.1 The script, golden-with-cards-out first (the step most often done by hand)
- [ ] DEV-5.2 The rest of the steps, the summary, the hang timeout
- [ ] DEV-5.3 `/card-batch` uses it; one batch measured before and after

## Next

Read the `/card-batch` skill's step 3, `tools/revert_check.py` (its restore and timeout), `tools/bench_vs_head.sh`,
Commands.md "Golden replays", and `MagicCore.Tests/CardProbeTests.cs`. DEV-5.1 first.

## Log

- 2026-11-07: created — Shayne, after TRK's usage review; a prerequisite for the next set.
