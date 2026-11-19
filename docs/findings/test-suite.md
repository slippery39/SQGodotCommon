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

## The whole wait — every project, the build, and how sessions run it (2026-11-18, DEV-8.1)

A quiet machine (checked: no process above 1 s of CPU in 5 s), Debug, 16 cores, after the sprint 5 merge. One driver ran
every step in a row (21 min); the trx files give each class's span (first start to last end), which in a parallel run
includes waiting for a core: a class's span is read as its place on the critical path, and its true cost is timed alone.

**The build is not the wait.** The solution as found 43 s; with nothing changed 8 s; after an edit to one
`MagicCore/Ai` file 17 s (the solution) or 13 s (`MagicCore.Tests` only). A `dotnet test` call that runs no test at all
costs 5.5 s.

**Each project alone (`--no-build`), and all together:**

| Project | Tests | Alone | In the together run |
|---|---|---|---|
| MagicCore.Tests | 2,013 (+2 explicit skipped) | **245 s** | 302 s |
| MtgSimulator.Tests | 468 | 95 s | 241 s |
| SQGodotCommon.Tests | 197 | 54 s (MagicTable's 25: 39 s) | 194 s |
| MtgCore.Tests | 846 | 4 s | 2 s |
| ImmutableGameObjects.Tests | 111 | 2 s | 0.2 s |
| **The solution, `dotnet test --no-build`** | | 400 s one after another | **308 s wall** |

They do compete for cores — run together, MtgSimulator and SQGodotCommon each take 3–4× their alone time — but the
wall clock is only 63 s over MagicCore alone, so running them together still beats running them in a row. **The quiet
full suite is 5 m 8 s plus a build.** The 6 m 18 s / 5 m 14 s / 3 m 44 s read after the merge are the together-run's
stretched numbers on a busier machine.

**MagicCore.Tests by slice** (the `Commands.md` filters): engine and cards — 1,493 tests in **8 s**; that plus
`RandomGameTests` and the golden replays — 1,642 in 47 s; the AI and sealed (`Ai` + `Limited`) — 371 tests in
**237 s**, nearly the whole project (DEV-7: 173 s).

**The critical path is one class.** Classes run in parallel, the tests inside a class in order, so the longest class
sets the floor: **`AiPositionTests` spans the whole 242 s run; alone, its 61 tests take 148 s** (DEV-7: 57 s, serial).
Next by span (inflated by sharing the cores): `GameRecordTests` 150 s (its every-record replay 26 s alone), `HiddenTests`
141 s (the landcycler test 22 s alone, 117 s in the crowd), `BeamSearchAiTests` 139, `PositionSuiteTests` 128,
`BasicsTests` 119, `RandomGameTests` 102 (150 games), `SealedSuggestionTests` 98, `PricingPrincipleTests` 92.

**The golden replays:** `RandomGameTests` and `GreedyAiReplayTests` (12 games, 50 s span) are in the normal suite;
together with the engine slice they are the 47 s above. `CardReplayTests` (explicit) is **817 cards in 177 s — one
class, so in order** (`Commands.md` said ~90 s); its slowest card is 5 s, the rest well under 1.

**MtgSimulator.Tests** is three tests: `ColorSlotSurvivalTests` 45 s, `OutputProbeTests` 21 s,
`DiscardChoiceStabilityTests` 12 s; the other 465 take 15 s. **SQGodotCommon.Tests** is `MagicTableManagerTests`
(36 s, one test 24 s); the other 172 take 15 s.

**How often sessions run it** — Claude's own session logs, every `dotnet test` call and how long it blocked (call to
result). This lane, the nine sessions since DEV-7 (2026-11-12 to 18): **226 foreground calls, 5.2 h blocked — 35 min a
session** (4 to 94). Whole `MagicCore.Tests`: 33 calls, mean 4.5 min, **the slowest 6–9 min** (load: the same suite is
4 min quiet). Filtered `MagicCore.Tests`: 166 calls, median 29 s, but the 13 over 2 min (the AI slice on a busy machine,
up to 10 min) are 71 of their 150 min. SQGodotCommon.Tests: 25 calls, 16 min. The solution-wide `dotnet test`: 2.
MtgSimulator.Tests: 0. 20 more were run in the background.

So "20 minutes" is not one run: it is a step's chain — a slice, then the whole project (4 min quiet, 6–9 shared),
then a rerun after the fix — each a foreground wait.

**What a change touches.** In this lane's 253 commits since 2026-10-20, **none touched `MtgCore/`, `MtgSimulator/`,
`ImmutableGameObjects/`, `SQGodotCommon/MtgGame/` or `SQGodotCommon/Common/`**, so MtgSimulator.Tests, MtgCore.Tests,
ImmutableGameObjects.Tests and SQGodotCommon.Tests' MtgGame/Core halves could not fail from them. By the project
references: an AI change (`MagicCore/Ai`) reaches MagicCore.Tests and SQGodotCommon.Tests' MagicTable tests (the table
compiles against MagicCore and plays games with its AI); a table change (`SQGodotCommon/MagicTable`) reaches only the
MagicTable tests (39 s); a MagicCore card batch reaches the engine slice, the golden replays, those cards'
`CardReplayTests`, the values tables (`CardValuesTests`, in `Ai`) and the MagicTable tests.

## The in-order classes in parallel (2026-11-19, DEV-8.4)

`AiPositionTests` (each test its own instance), `CardReplayTests`, `RandomGameTests` and `GreedyAiReplayTests` run
their tests in parallel; `GoldenReplay` made safe for it. Quiet machine (the cube lane's batch done, checked idle),
three rounds, every run passing with the counts unchanged (MagicCore 2,013 + 2 skipped; card replays 817):

| | Before (DEV-8.1) | Rounds 1 / 2 / 3 | Median |
|---|---|---|---|
| MagicCore.Tests | 245 s | 187 / 192 / 246 | **192 s** |
| The AI and sealed slice | 237 s | 186 / 195 / 248 | **195 s** |
| An engine change (`test_changed.py --run`: engine + golden, then MagicTable) | 126 s | 77 / 87 / 85 | **85 s** |
| `CardReplayTests` | 177 s | 43 / 50 / 61 | **50 s** |

Round 3 ran slower across the board with nothing else visible on the machine (likely heat after 20 minutes at full
load) — read the medians. The card replays gained 3.5×; the suite only 1.3×, because the AI slice is still nearly the
whole suite: with `AiPositionTests` split, the next-longest AI class is the floor (unnamed yet — a trx run names it).
