---
id: AI-7
title: A faster search — profiled, then optimised without changing a decision
status: done
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
- [x] **A pure optimisation changes no decision** — AI-6's position suite shows zero changed
      decisions against the committed AI (and `GoldenAiReplay` for GreedyAi); anything that does
      change one is a behaviour change, measured like one
- [x] Each optimisation reports its speedup by the cost count and by wall clock

## Decisions

- 2026-10-19, Shayne: **profile first, decide later** whether optimising the shared
  ImmutableGameObjects engine is worth it (it would be a `CMN-` story, numbered on main and
  cherry-picked); until then MagicCore and its AI only.
- 2026-10-19, Shayne (AI-7.2, from the profile): **all four candidates are in** — the board cache,
  the turn structure, allocation, attack/block judging — **each discussed in detail before it is
  built.** The board cache was built first, as the measurement that found it.
- 2026-10-19, Shayne: **the table gets the server collector both ways** — `DOTNET_gcServer=1` in
  `Run-Godot.ps1` and `<ServerGarbageCollection>` in the Godot csproj (exported builds).

- 2026-10-19, Shayne: **7.3e skipped, AI-7 closed** — measured, the attack/block reuse would skip
  16% of combat simulations (~6–7% of the search) for real complexity in its most delicate path. It,
  `LegalActions` and `ZoneId` are costed in DesignNotes.md ("MagicCore search: speedups not taken").
  The shared engine stayed out: nothing measured led there.

## Tasks

- [x] AI-7.1 Profile a decision and a rollout over real games; record where the time goes — the
      collector first (a weak-table cache), then the turn structure (half a step is the state-based check)
- [x] AI-7.2 Choose the optimisations with Shayne from the profile — all four, each discussed in
      detail first; the shared engine asked again if allocation leads there
- [x] AI-7.3a The board cache: one entry per thread, not a global weak table — suite 21.0 → 14.6 s
- [x] AI-7.3b The server collector for the table: `Run-Godot.ps1` and the Godot csproj
- [x] AI-7.3c Allocation: power, toughness and keywords as plain loops — 36.3 → 17.9 GB over AI-7
- [x] AI-7.3d The turn structure: trigger collection returns at once with no staged event (d1);
      `CardsIn` one iterator with cached zone keys (d2a); the check reads the battlefield once (d2b)
- [ ] ~~AI-7.3e Attack/block judging: reuse the score of the line the previous creature's judgment
      already played~~ — skipped (Shayne): 16% of combat simulations, ~6–7% of the search

Each AI-7.3 task is proven decision-neutral by the position suite (0 changed); the cost count says
what it saved — it may fall (fewer simulated steps), which is not a changed decision.

## Next

**Done (2026-10-19).** The search is profiled and three times leaner: allocation over the position
suite 36.3 → 17.9 GB, the table's case (workstation collector) 56 → ~17 s, 0 decisions changed at
every step (findings, "Where a decision's time goes" and "Decision-neutral speedups"). The table gets
the server collector through `Run-Godot.ps1` (F5 from the editor does not; Commands.md).

Not taken, costed in DesignNotes.md ("MagicCore search: speedups not taken"): attack/block score
reuse (7.3e), `LegalActions` (the largest allocator left), `ZoneId`'s string keys.

**For any later optimisation:** `--suite=beam` must show 0 changed; measure it against the tree
without it, minutes apart, on both collectors (`DOTNET_gcServer=1`/`0`), with the collector's
counters (`GC.GetTotalAllocatedBytes`, `GC.GetTotalPauseDuration`, CPU time) printed at exit by a
temporary line in MagicConsole/Program.cs — this machine's speed drifts ~2× in a day, so only pairs
compare, and allocated bytes are the steady measure. Time single operations with a .NET 10
file-based app (`#:project MagicCore`, `#:property PublishAot=false`, `#:property
TieredCompilation=false`) run from the solution root. `dotnet-trace`'s thread-time samples end in a
false `PollGC` leaf: read callers, not leaves. `HotPathAllocationTests` guards the characteristic reads.

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
- 2026-10-19: AI-7.3c — `Characteristics.Power`/`Toughness` sum the applying statics directly, `Has`
  compares the keyword before asking if a static applies (and computes no P/T a keyword question
  never reads); `Card.HasOwn`, `OwnPower`/`OwnToughness`, `CurrentBase` loop over `Components` (LINQ
  boxed the ImmutableArray). 0 changed; 21.0 → 17.9 GB; `IsIndestructible` 736 → 0 B a call. The
  evaluator as loops measured too (18.0 → 17.7 GB) and not kept; so was building each P/T static's
  context once per board (worse: 18.1 GB). `HotPathAllocationTests`: reading a creature allocates
  nothing (LINQ `HasOwn` back: 20 000 B over 100 reads, fails). 976 tests; revert-checked (no
  keyword granted: 95 fail; first base P/T, not latest: 1).
- 2026-10-19: AI-7.3e measured before building: rollouts made 18 155 combat judgments over 45 698
  options; 7 520 follow another of the same kind by the same player — each would skip one combat
  simulation, 16% of them. Shayne: skip it; AI-7 closed. Deferred candidates in DesignNotes.md.

