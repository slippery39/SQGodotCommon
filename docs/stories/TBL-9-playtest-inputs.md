---
id: TBL-9
title: Table inputs the playtests asked for: an X spinner and reordering a surveil
status: backlog
area: TBL
created: 2026-11-09
---

## Why

Shayne's playtest, 2026-11-09. Choosing X lists every possible X as a button (terrible past a few); a surveil lets you put cards in the graveyard but not reorder the ones kept on top, though the rules allow it.

## Requirements

- [ ] X is chosen with a spin-down (+ / -) that can also be typed into, never one button per X
- [ ] A surveil (and any "any order" choice) lets the player drag the kept cards into an order

## Decisions

- (none yet)

## Tasks

- [ ] TBL-9.1 X picker: a spinner with +/- and direct typing, bounded by the legal X values (`ChoicePanel`/`Targeting`; today every X is listed)
- [ ] TBL-9.2 Surveil/"any order": drag to reorder the cards kept on top. First check the engine takes an order at all (the choice may answer only graveyard-or-top)

## Next

Read this story, then `.claude/rules/magic-table.md` (table) or `MagicCore/CLAUDE.md` (engine).

## Log

- 2026-11-09: opened from Shayne's playtest notes.
