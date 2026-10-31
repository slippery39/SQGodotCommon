---
id: MC-5
title: Star Trek (TRK) plays in MagicCore — sealed and draft before Arena's day 1
status: ready
area: MC
created: 2026-10-30
---

## Why

Shayne, 2026-10-30: a Star Trek set comes out on Arena on **November 10, 2026** (prerelease November 6–12,
tabletop November 13) — "it might be worth implementing up front to get ahead of everything". Getting
ahead means: **"I can practice sealed and draft before Day 1 on arena, get a feel for all the cards and
stuff."**

Scryfall already lists the set (2026-10-30): code `trk`, 311 distinct cards (96 common, 110 uncommon,
80 rare, 25 mythic). New mechanics: Station (17 cards), Strange new worlds (13), Face a dilemma (8);
also Typecycling, Investigate and Kicker, which MagicCore may not have yet (grep MtgCore first). Most
keywords exist already (surveil, landcycling, cycling, treasure, mill, equip).

## Requirements

- [ ] TRK sealed and draft play on MagicTable before November 10 — the cards Shayne will open at the
      prerelease, scripted, as FRA's are (`/card-batch`)
- [ ] The draft bots pick sensibly without 17lands data (there is none before release): colour lean at
      least; DATA-2's engine ratings if there is time

## Decisions

- 2026-10-30, Shayne: TRK goes into the CURRENT sprint (sprint 3), ahead of the cube (MC-4); "get ahead"
  is practising sealed and draft before Arena's day 1. Not the Commander product (TRC) unless asked.
- 2026-10-30, Shayne: not started in the session that planned it — that session finishes the AI fixes
  (AI-17.6); the survey is the first task of a fresh session.

Open questions:
- How booster collation is known before release (FRA's came from MTGJSON — `tools/import_mtgjson_boosters.py`).

## Tasks

- [ ] MC-5.1 The survey (a fresh session): import TRK from Scryfall (`tools/import_scryfall_set.py`), tier
      every card by what the engine needs, the new mechanics' rules read in full, a batch plan sized to
      reach sealed and draft by November 10
- [ ] MC-5.2 The new mechanics (Station, Strange new worlds, Face a dilemma, and whatever the survey finds)
- [ ] MC-5.3 Commons and uncommons in large batches, then rares and mythics
- [ ] MC-5.4 TRK boosters, sealed and draft on the table, bots without 17lands

## Next

Start with MC-5.1 in a fresh session: read `/card-batch`, `MagicCore/CLAUDE.md` and FRA's story (MC-1) for
how a set was built; import the set; survey. Grep MtgCore for every mechanic before building it.

## Log

- 2026-10-30: created — Shayne moved TRK into sprint 3, ahead of the cube; release dates and Scryfall's
  card count checked the same day.
