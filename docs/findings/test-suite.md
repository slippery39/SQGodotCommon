# The test suite's time (DEV-7)

## MagicCore.Tests, serial against parallel classes (2026-11-12, DEV-7.1)

A quiet machine (no other lane building; the graphify hook's rebuilds waited out), Debug build, built beforehand.

**Serial (as it was): 271 s wall**, the tests' own durations summing to 268 s. By folder: Ai 143 s (53%), Limited
83 s (31%), the root 37 s (14% — `RandomGameTests` almost all of it), Sets 3.7 s (1.4%, 827 tests), the rest under a
second. Slowest classes: `AiPositionTests` 57 s, `SealedSuggestionTests` 38, `RandomGameTests` 37, `RatingsMatchTests`
26, `PositionSuiteTests` 19, `GameRecordTests` 17, `EarlyStopTests` 12. Slowest tests: a ratings match 15.8 s, a sealed
opponent's suggestion 13.9, every kept record replaying 12.7, the position suite's check 11.9.

The 8–9 minutes measured earlier the same day were runs sharing the machine (a match, the cube lane) — a wall-clock
number means nothing without a quiet machine.

**Parallel classes (`[assembly: Parallelizable(ParallelScope.Fixtures)]`): 207, 189, 196 s wall over three runs** —
1,586 passed each time, 2 explicit skipped (one fewer than before: the Molten Tide timing check is explicit now and
counted with them). Only 1.4× — the AI tests already spread each search over the cores (`Parallel.For`), so the serial
run kept much of the machine busy. Per-test durations in a parallel run include waiting for a core (they summed to
1,783 s): read the wall clock only.

**Slices, parallel on:** engine and cards (everything but `Ai`, `Limited`, `RandomGameTests`, `GoldenReplay`) — 1,096
tests in **7 s**; the AI and sealed (`Ai` + `Limited`) — 342 tests in 173 s. The commands are in `Commands.md`.
