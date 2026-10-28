---
id: MC-4
title: MTGA's powered cube plays in MagicCore — surveyed, then built in batches
status: backlog
area: MC
created: 2026-10-28
---

## Why

Shayne plays powered cube on MTGA and wants to practise it here (LIM-2's Decisions, 2026-10-23). LIM-2
deferred the cube's CARDS to their own story, "planned in depth when its time comes": power brings
rules FRA never needed (extra turns, storm, fast mana and others), so the cube is surveyed before
anything is built. At sprint 3 planning (2026-10-28) Shayne named AI, drafting and the powered cube
as his three priorities.

## Requirements

- [ ] Every card on MTGA's powered cube list plays in MagicCore, each with a test that it FIRES
      (`/card-batch` tiers)
- [ ] The cube is a card list, drafted the same way as FRA's packs (LIM-2: the draft must not assume
      packs)

## Decisions

- 2026-10-28, Shayne: **MTGA's powered cube** (not MTGO's Vintage Cube). Sprint 3 covers the survey
  and the first large batch, not the whole cube.

Open questions:
- The list itself: Shayne exports it or gives a link (CubeCobra, or Wizards' announcement). Arena
  changes its cube between runs, so which run?
- Where its cards live: a list read by the Scryfall importer (`tools/import_scryfall_set.py` reads
  a set; MC-3's `GUESTS` query was a first list by number), and a file beside `FRA.json`.
- Arena's draft for this cube (packs, size, seats), for LIM-2.

## Tasks

- [ ] MC-4.1 The list from Shayne; imported by name, with art (`fetch_set_art.py`)
- [ ] MC-4.2 The survey: each card sorted by what it needs — scripts with today's primitives, needs a
      new primitive (named), or needs a new rule (extra turns, storm, ...) — and the batches planned
      in this story, the new rules first in each
- [ ] MC-4.3 The first batch, one large one in a fresh session (`/card-batch`)

## Next

Ask Shayne for the list (MC-4.1). Read `MagicCore/CLAUDE.md`, the `/card-batch` skill and MC-3 (the
last import beside FRA) before the survey; grep MtgCore for any rule the cube needs before writing
one (memory: copy from MTG).

## Log

- 2026-10-28: created at sprint 3 planning (Shayne): MTGA's powered cube; survey and first batch this
  sprint.
