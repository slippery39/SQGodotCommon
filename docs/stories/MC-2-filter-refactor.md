---
id: MC-2
title: One composable filter instead of six closed vocabularies
status: done
area: MC
created: 2026-10-10
---

## Why

Shayne's first manual code review of MagicCore found closed enums grown one card at a time —
RefKind (22), TriggerEvent (51), TargetSpec (~20 fields) and more. The audit is
`MagicCore/DesignAudit.md`; the plan, design and phase checklist are `MagicCore/FilterRefactorPlan.md`.

## Requirements

- [x] One `Filter` (`&`, `|`, `!`) read by targets, refs, statics, conditions, triggers and costs
- [x] Behaviour unchanged — proven by golden replays (`GoldenReplay.txt`, `GoldenCardReplay.txt`)

## Decisions

2026-10-10, Shayne:
- Lift MtgCore's `TargetSpecification` and repair it; MtgCore stays untouched.
- **Operator syntax** in card scripts: `Is.Creature & Is.Yours & !Is.Source`.
- Behaviour must not change; an intended change gets its own commit after the refactor.
- One phase per session.

2026-10-15: counters stay six fields for now; a keyed map when a set brings a new counter kind
(`DesignNotes.md`, "MagicCore known ceilings").

## Tasks

- [x] MC-2.1 Phase 0: design audit, plan, golden replays — `142fa34`
- [x] MC-2.2 Phase 1: the filter language and its migration table — `0b071e6`
- [x] MC-2.3 Phase 2: TargetSpec is a zone and a filter — `2a231ef`
- [x] MC-2.4 Phase 3: Ref is referents + `All(zone, filter)` — `8772546`
- [x] MC-2.5 Phase 4: statics and conditions read filters — `0fa7401`
- [x] MC-2.6 Phase 5: triggers are an event kind + subject filter + who — `ca80d8c`
- [x] MC-2.7 Phase 6: one cost list, one cost change — `9dd4e37`
- [x] MC-2.8 Phase 7: "this turn" effects live in the command zone — `c632191`
- [x] MC-2.9 Write-up: how to structure a card engine on ImmutableGameObjects — `47adebb`

## Next

Done. If Shayne's manual review continues, each new finding goes into `DesignAudit.md` and becomes
its own MC story.

## Log

- 2026-10-15: all phases committed.
