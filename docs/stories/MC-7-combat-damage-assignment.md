---
id: MC-7
title: The attacker assigns combat damage among its blockers
status: backlog
area: MC
created: 2026-11-09
---

## Why

Shayne, 2026-11-09: when the opponent double-blocks, we don't get to decide how damage is dealt. CR 510.1c: the attacking player orders/divides damage among the blockers. It is a known simplification today (blocker damage splits automatically - DesignNotes "MagicCore known ceilings").

## Requirements

- [ ] With two or more blockers the attacker's controller divides the damage (lethal to the first before the next, CR 510.1c), on the table and for the AI
- [ ] The AI chooses sensibly (kill the most valuable it can); a record replays

## Decisions

- (none yet)

## Tasks

- [ ] MC-7.1 Read the ceiling in `DesignNotes.md` and `Combat.Assigns`; cost the options (a choice per attack vs a standing order) and ask Shayne
- [ ] MC-7.2 The choice in the engine, behind records' offer fingerprints (a new choice makes old records stale - check one replays)
- [ ] MC-7.3 The table: pick how much each blocker gets
- [ ] MC-7.4 The AI's choice, a position test (the double block that killed the wrong creature)

## Next

Read this story, then `.claude/rules/magic-table.md` (table) or `MagicCore/CLAUDE.md` (engine).

## Log

- 2026-11-09: opened from Shayne's playtest notes.
