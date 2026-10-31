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
- [ ] Every TRK card plays by Nov 10 — rares and mythics too (Shayne, 2026-10-31)
- [ ] A card's functionality is decoupled from its printings: a reprint is scripted once, its art,
      rarity and collector number are per set (Shayne, 2026-10-31 — sets and cubes alike)
- [ ] The draft bots pick by our own ratings: card win rates pre-simulated against bots (DATA-2), plus
      colour lean — there is no 17lands data before release (Shayne, 2026-10-31)

## Decisions

- 2026-10-30, Shayne: TRK goes into the CURRENT sprint (sprint 3), ahead of the cube (MC-4); "get ahead"
  is practising sealed and draft before Arena's day 1. Not the Commander product (TRC) unless asked.
- 2026-10-30, Shayne: not started in the session that planned it — that session finishes the AI fixes
  (AI-17.6); the survey is the first task of a fresh session.

- 2026-10-31, Shayne: survey first — Scryfall data, the new mechanics, how many cards today's pieces
  build — and the plan reviewed before anything is built. **Reprints are not re-implemented**: a card in
  two sets is one card (one script, one rules text); only its printing — art, rarity, collector number —
  differs by set.

- 2026-10-31, Shayne (MC-5.1's questions): **decouple a card's functionality from its printings, up
  front** — future sets and cubes face it too. **Every card by Nov 10, rares and mythics included** — the
  budget fits now that each batch is a fresh session (this set is the test of that). **Bot ratings are
  our own: card win rates pre-simulated against bots** (DATA-2's method), accepted as not fully accurate.

- 2026-10-31, Shayne: **Stardates (SDS) last, but still aimed at Nov 10** — 30 reprints (mythic,
  borderless, ~1 pack in 25 in place of a common), TRK's Special Guests. Until they play, that slot opens
  a common.

Open questions:
- Booster collation: MTGJSON has TRK's cards but NO booster data yet (checked 2026-10-31); a provisional
  collation from Wizards' published slot odds is proposed (Collecting Star Trek, magic.wizards.com).

## Tasks

- [x] MC-5.1 The survey (a fresh session): import TRK from Scryfall (`tools/import_scryfall_set.py`), tier
      every card by what the engine needs, the new mechanics' rules read in full, a batch plan sized to
      reach sealed and draft by November 10
- [ ] MC-5.2 The new mechanics (Station, Strange new worlds, Face a dilemma, and whatever the survey finds)
- [ ] MC-5.3 Commons and uncommons in large batches, then rares and mythics
- [ ] MC-5.4 TRK boosters, sealed and draft on the table, bots without 17lands
- [ ] MC-5.5 FIRST, before any card: a card's functionality decoupled from its printings — one registry
      by name across sets (one script per card; a test fails on a name scripted twice or a reprint whose
      rules text differs), printing data per set (art, rarity, collector number); `FraCards.Get`'s ~50
      call sites moved to it
- [ ] MC-5.6 LAST, aimed at Nov 10: the 30 Stardates (SDS) — imported as TRK's guests
      (`GUESTS` in `tools/import_scryfall_set.py`, as FRA's SPG), scripted once each, into the collation

## Next

**MC-5.1 is done (2026-10-31): the survey and the plan, agreed with Shayne.** Nothing is built.
**Every batch starts in a NEW session** (`/card-batch` step 0) and ends with its usage row
(`tools/usage_report.py --log`, `docs/findings/card-batch-usage.md`) — this set tests that workflow.

Read: this story, `MagicCore/TrkCoverage.md` (every card's tier, what it waits on, what to reuse —
the survey), `/card-batch`, `MagicCore/AbilityCatalog.md`, `MagicCore/Cookbook.md`. Card text:
`python tools/magic_set.py text --set TRK "Name"`. Rules for the new mechanics: Wizards' "Star Trek
Release Notes" (magic.wizards.com/en/news/feature/star-trek-release-notes) — Station's are Edge of
Eternities'. Booster odds: "Collecting Star Trek" on the same site. MTGJSON has no TRK boosters yet
(2026-10-31) — re-check before building the provisional collation.

The plan, one or more fresh sessions per row, every card by Nov 10:

| Dates | Work |
|---|---|
| Nov 1 | **MC-5.5** the card registry (functionality decoupled from printings); TrkCards; tokens; the 10 Planet duals |
| Nov 2–3 | Tier 0–1 commons, then uncommons (~118) |
| Nov 4–6 | Station; the small mechanics (beam me up, strange new worlds, dilemma, Federation, power 8, becomes tapped, counters); Sagas, Kicker → ~185 of 206 C/U |
| Nov 6–7 | Provisional boosters, TRK on Sealed and Draft (MC-5.4); DATA-2's rating method checked on FRA against 17lands |
| Nov 7–9 | Rares and mythics: ~49 tier 1–2, then ~56 tier-3 one-offs in batches of ~14 |
| Nov 9–10 | The TRK rating run (ask first: long), bots on it, a draft to check; the Stardates (MC-5.6) last |

Traps: the engine is single-set — ~50 call sites use `FraCards.Get(name)`, `MagicArt` finds art by
name slug only, and `tools/magic_set.py` finds a set's scripts by its folder name. MagicCore's
`CLAUDE.md` is at its 200-line budget: move content down rather than add.

## Log

- 2026-10-30: created — Shayne moved TRK into sprint 3, ahead of the cube; release dates and Scryfall's
  card count checked the same day.
- 2026-10-31: MC-5.1 survey — TRK imported (311), every card tiered, release-note rules read, MTGJSON
  checked (no TRK boosters yet), Stardates (SDS) found; plan put to Shayne. Counts are in Next.
- 2026-10-31: requirements changed (Shayne) — every card by Nov 10, not commons and uncommons first with
  rares if time; printings decoupled from functionality; bots on pre-simulated ratings, not colour lean
  alone.
- 2026-10-31: usage per batch is now logged — `docs/findings/card-batch-usage.md` (the survey: 1.9M).
