---
id: TBL-9
title: Table inputs the playtests asked for: an X spinner, reordering a surveil, conceding
status: ready
area: TBL
created: 2026-11-09
---

## Why

Shayne's playtest, 2026-11-09. Choosing X lists every possible X as a button (terrible past a few); a surveil lets you put cards in the graveyard but not reorder the ones kept on top, though the rules allow it.

## Requirements

- [ ] X is chosen with a spin-down (+ / -) that can also be typed into, never one button per X
- [ ] A surveil (and any "any order" choice) lets the player drag the kept cards into an order
- [ ] The player can concede a game: right-click the table → a context menu → Concede, confirmed first

## Decisions

- 2026-11-10, Claude (Shayne asked why surveil can't reorder): it is the engine's own shortcut, not a rule —
  `Surveil` (`ZoneEffects.cs`) asks only which cards go to the graveyard and keeps the rest in their order
  ("ponytail: reordering them is a second choice nobody needs at N=1"). Scry is NOT implemented: no FRA or
  TRK card scries. Spock's "bottom in any order" is random (`ZoneEffects.cs`, Shayne 2026-10-31). So
  TBL-9.2 needs an engine choice first; a new choice can make old game records stale (their offers
  fingerprint — TBL-2.16's trap), so check a record still replays.
- 2026-11-10, Claude (open to change): a concede is the table's, not an engine move — the game ends as a
  loss for you (a draft run counts it as one, `TableSetup.Finished`); the record is left as it stands.

## Tasks

- [ ] TBL-9.1 X picker: a spinner with +/- and direct typing, bounded by the legal X values (`ChoicePanel`/`Targeting`; today every X is listed)
- [ ] TBL-9.2 Surveil/"any order": drag to reorder the cards kept on top. The engine takes no order today (Decisions): add the order to the choice, check a record replays, then the table
- [ ] TBL-9.3 Concede: a right-click context menu on the table with Concede, a confirm, the game ended as a loss

## Next

Read this story, then `.claude/rules/magic-table.md` (table) or `MagicCore/CLAUDE.md` (engine).

## Log

- 2026-11-09: opened from Shayne's playtest notes.
- 2026-11-10: TBL-9.3 (concede) added (Shayne); TBL-9.2's engine side found (Decisions).
