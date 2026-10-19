---
id: AI-7
title: A faster search — profiled, then optimised without changing a decision
status: in-progress
area: AI
created: 2026-10-19
---

## Why

Every measurement and every move on the table pays for the search's speed: ~250 ms a decision,
up to 200 rollouts at ~22 ms each. Measured so far (findings, AI-2.4): attack declarations are ~45%
of a rollout, our policy's checks ~35%, the engine's priority passes ~20% (~0.09 ms each). A faster
search shortens AI-6's runs and the table's thinking alike. Taken up together with AI-6 (Shayne,
2026-10-19).

## Requirements

- [x] Profiled first: where a decision's time goes — engine, rollout policy, evaluator, allocation —
      recorded in `docs/findings/magic-ai.md`, using AI-6's cost count
- [ ] **A pure optimisation changes no decision** — AI-6's position suite shows zero changed
      decisions against the committed AI (and `GoldenAiReplay` for GreedyAi); anything that does
      change one is a behaviour change, measured like one
- [ ] Each optimisation reports its speedup by the cost count and by wall clock

## Decisions

- 2026-10-19, Shayne: **profile first, decide later** whether optimising the shared
  ImmutableGameObjects engine is worth it (it would be a `CMN-` story, numbered on main and
  cherry-picked); until then MagicCore and its AI only.
- 2026-10-19, Shayne (AI-7.2, from the profile): **all four candidates are in** — the board cache,
  the turn structure, allocation, attack/block judging — **each discussed in detail before it is
  built.** The board cache was built first, as the measurement that found it.
- 2026-10-19, Shayne: **the table gets the server collector both ways** — `DOTNET_gcServer=1` in
  `Run-Godot.ps1` and `<ServerGarbageCollection>` in the Godot csproj (exported builds).

Open questions:
- Whether the shared ImmutableGameObjects engine is in (asked again if allocation leads there).

## Tasks

- [x] AI-7.1 Profile a decision and a rollout over real games; record where the time goes — the
      collector first (a weak-table cache), then the turn structure (half a step is the state-based check)
- [x] AI-7.2 Choose the optimisations with Shayne from the profile — all four, each discussed in
      detail first; the shared engine asked again if allocation leads there
- [x] AI-7.3a The board cache: one entry per thread, not a global weak table — suite 21.0 → 14.6 s
- [x] AI-7.3b The server collector for the table: `Run-Godot.ps1` and the Godot csproj
- [ ] AI-7.3c Allocation — next: `Characteristics` and `Card` reads (LINQ over `ImmutableArray`)
- [x] AI-7.3d The turn structure: trigger collection returns at once with no staged event (d1);
      `CardsIn` one iterator with cached zone keys (d2a); the check reads the battlefield once (d2b)
- [ ] AI-7.3e Attack/block judging: reuse the score of the line the previous creature's judgment
      already played (its "don't" / cheap answer)

Each AI-7.3 task is proven decision-neutral by the position suite (0 changed); the cost count says
what it saved — it may fall (fewer simulated steps), which is not a changed decision.

## Next

**AI-7.1 done; AI-7.2 chosen** from the profile (findings, "Where a decision's time goes"):

1. **The board cache** (7.3a) — `Characteristics.BoardOf`'s global `ConditionalWeakTable` replaced by one
   entry per thread: proven neutral (`--suite=beam` 0 changed, cost count identical, 975 tests; a
   stale cache fails 122). Suite 21.0 → 14.6 s; on the table's collector 56.3 → 22.0 s.
2. **Server GC for the table** (7.3b) — `Run-Godot.ps1` sets `DOTNET_gcServer=1` (checked: the table prints
   `IsServerGC` True through it, and the caller's shell is left as it was) and the Godot csproj sets
   it for exported builds — UNVERIFIED: there is no Windows export preset, only Android's (Mono,
   which likely ignores it). F5 from the editor still gets neither (Commands.md says so).

3. **The turn structure** (7.3d) — trigger collection returns at once with no staged event (62% of
   calls); `CardsIn` is one iterator and zone keys are built once; the state-based check reads each
   battlefield once. Allocation 36.4 → 21.0 GB over the suite, 0 changed (findings, "Decision-neutral
   speedups").

1–3 are UNCOMMITTED in the working tree with the profile, awaiting Shayne's go to commit.

**Discussed with Shayne and agreed (2026-10-19): d1 → d2 → re-measure → 7.3c → 7.3e.** Next, 7.3c:
`Card.HasOwn`/`OwnToughness`/`CurrentBase` and `Characteristics.Statics` allocate on every read
(LINQ over `ImmutableArray` boxes it; `Has` builds a context and P/T values it never reads) —
`IsIndestructible` 736 B a call. Plain loops over `Components`, one computation each still. Then
7.3e: when creature k is judged, the option QuickAnswer would give it replays the line creature k−1's
chosen option already settled — reuse that score (keep the previous judgment's state and score in
the rollout); exactly equal, since the same state and computation give the same number. The cost
count falls with it (fewer settle steps); "0 changed" is the test.

Measured and NOT kept: `GivePriority` handing the check the same state when nothing is staged (so
the board cache hits) — 21.0 → 20.9 GB, no CPU change; a board is cheap to rebuild. A candidate for
later: `ZoneId` still goes through a string key, a tuple-keyed cache and a string-keyed dictionary
for an id fixed all game (ids on `MagicPlayer`, or a per-game array).

**To measure a change:** the A/B script approach — the suite without the change, then with it,
minutes apart, both collectors (`DOTNET_gcServer=1`/`0`), with the collector's counters printed at
exit by a temporary line in MagicConsole/Program.cs (`GC.GetTotalAllocatedBytes`,
`GC.GetTotalPauseDuration`, CPU time); allocated bytes are the steady measure.

Also open: whether the shared ImmutableGameObjects engine is in (Decisions) — nothing measured so far
points there; the allocation (7.3c) is the question that might.

**Measuring, learned in 7.1:** read the collector's own counters (`GC.GetTotalPauseDuration`,
`GC.GetTotalAllocatedBytes`) beside the suite; `dotnet-trace`'s thread-time samples end in a false
`PollGC` leaf (read callers, not leaves); time single operations with tiered compilation off. The
probe used was a .NET 10 file-based app (`#:project MagicCore`, `#:property PublishAot=false` — or
card data won't load —, `#:property TieredCompilation=false`), run from the solution root.

## Log

- 2026-10-19: created from an interview with Shayne after AI-3 was paused for slow runs.
- 2026-10-19: AI-7.1 — profiled (findings, "Where a decision's time goes"). MagicTable runs on the
  workstation collector (printed from the table: `IsServerGC` False; `DOTNET_gcServer=1` makes it
  True). The collector's pauses were 76% of the suite's wall clock on it, 27% on the server one —
  made slow by `BoardOf`'s weak table, not by the 36 GB allocated (one entry per thread: 42.8 → 8.6 s
  of pauses, 0 decisions changed). A step is ~77 µs, half of it the state-based check.
- 2026-10-19: AI-7.2 — Shayne took all four candidates, each to be discussed in detail first.
  AI-7.3a — `Characteristics.BoardOf` keeps one board per thread instead of a global weak table:
  `--suite=beam` 0 changed, the same cost count; suite 21.0 → 14.6 s (server collector), 56.3 →
  22.0 s (workstation); MagicCore.Tests 975 green in 1 m 5 s (was 1 m 52 s); revert-checked — a
  cache that never refreshes fails 122. AI-7.3b — `Run-Godot.ps1` sets `DOTNET_gcServer=1` (the table
  prints `IsServerGC` True through it) and the Godot csproj `<ServerGarbageCollection>` (exports —
  unverified: no Windows export preset exists).
- 2026-10-19: AI-7.3d — discussed with Shayne first (the plan: d1 → d2 → re-measure → c → e). d1:
  `Triggers.Collect` returns at once with no staged event — every trigger reads one. d2a: `CardsIn`
  one iterator; `MagicKeys.Zone` keys built once (a string, its enum formatted, on every call before).
  d2b: the state-based check reads each player's battlefield once, toughness once per creature, the
  legend grouping only with two legends or more. Each 0 changed on the suite, cost count identical;
  allocation 36.4 → 27.0 → 24.0 → 21.0 GB (findings). 975 tests; revert-checked (lethal off by one:
  120 fail; no token sweep: 158).

