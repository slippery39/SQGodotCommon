---
id: MC-3
title: The 10 FRA Special Guests play
status: ready
area: MC
created: 2026-10-20
---

## Why

One FRA play booster in 55 swaps a common for a Special Guest (set SPG, #159–168), so about one
sealed pool in nine holds one. Shayne (2026-10-20): script all 10 before LIM-1's pools contain them.

## Requirements

- [ ] The 10 import into the card data: Austere Command, Consider, Consign to Memory, Eye of Ugin,
      Flesh Duplicate, Mind Twist, Necrodominance, Root Maze, Splinter Twin, Sublime Epiphany
- [ ] Each is scripted and has a test that it FIRES (`/card-batch` tiers)

## Decisions

- 2026-10-20, Shayne: all 10, before pools contain them (LIM-1).
- 2026-10-23, Shayne (sprint planning): a priority — a real Arena pool can hold one, and the sealed
  practice needs the full experience. Sprint item 2, right after LIM-1.7.

## Tasks

- [ ] MC-3.1 Import SPG #159–168 beside FRA (`tools/import_scryfall_set.py` takes one set code)
- [ ] MC-3.2 Script and test them — one `/card-batch` (two sessions if the engine work runs long)
- [ ] MC-3.3 Packs contain them: `Boosters.GuestsPlayable` → true (1 pack in 55), the rate test
      checks it; LIM-1.7's "not in the engine" path then only meets non-FRA cards

## Next

**Sprint item 2**, after LIM-1.7 (`docs/stories/README.md`). Run it as `/card-batch`, in a fresh
session. The guests must join the card data `FraCards.Get` reads: today the sealed builder
(`Limited/SealedBuilder.cs`) would throw on a guest in a pool. Read each card's full Oracle text first. Easy: Consider, Mind Twist, Root Maze, Austere Command,
Sublime Epiphany, Consign to Memory. Engine work likely: Splinter Twin (a granted ability making a
token copy), Flesh Duplicate (clone), Eye of Ugin (Eldrazi cost reduction + a library search),
Necrodominance (skip draw, end-step pay life, hand size). Grep MtgCore first for each.

## Log

- 2026-10-20: created from LIM-1's interview.
- 2026-10-23: moved up to the sprint's second item (Shayne); MC-3.3 added.
