---
id: MC-3
title: The 10 FRA Special Guests play
status: done
area: MC
created: 2026-10-20
---

## Why

One FRA play booster in 55 swaps a common for a Special Guest (set SPG, #159–168), so about one
sealed pool in nine holds one. Shayne (2026-10-20): script all 10 before LIM-1's pools contain them.

## Requirements

- [x] The 10 import into the card data: Austere Command, Consider, Consign to Memory, Eye of Ugin,
      Flesh Duplicate, Mind Twist, Necrodominance, Root Maze, Splinter Twin, Sublime Epiphany
- [x] Each is scripted and has a test that it FIRES (`/card-batch` tiers)

## Decisions

- 2026-10-20, Shayne: all 10, before pools contain them (LIM-1).
- 2026-10-23, Shayne (sprint planning): a priority — a real Arena pool can hold one, and the sealed
  practice needs the full experience. Sprint item 2, right after LIM-1.7.
- 2026-10-23, Claude: the guests live IN `FRA.json` (the importer's `GUESTS` query, after FRA's own
  cards), so every reader of the set — `FraCards`, the art, the sheet, the ratings — sees them with
  no second set to load. `Boosters.Guests` names them (the special-guest sheet).
- 2026-10-23, Claude: MC-3.2 split in two batches (the story allowed it). Shayne: the fresh-session
  rule is for very LARGE batches — batch 2 (four cards) ran in the same session.
- 2026-10-23, Claude: `Boosters.GuestsPlayable` deleted rather than set true — every guest plays, so
  there is nothing left to switch.

## Tasks

- [x] MC-3.1 Import SPG #159–168 beside FRA — `GUESTS` in `tools/import_scryfall_set.py` (read by
      `fetch_set_art.py` too); art fetched and imported
- [x] MC-3.2 Script and test them — batch 1 (Consider, Austere Command, Mind Twist, Eye of Ugin,
      Root Maze, Splinter Twin; `FraSpecialGuestTests`, 8/8 revert-checks); batch 2 is MC-3.4
- [x] MC-3.4 Batch 2: Consign to Memory, Sublime Epiphany, Flesh Duplicate, Necrodominance (14 tests
      in `FraSpecialGuestTests`, 17/17 revert-checks)
- [x] MC-3.3 Packs contain them, 1 pack in 55 (`SpecialGuests_AreOnePackIn55_AndPlay`, 20,000 packs);
      LIM-1.7's "left out" path now meets only non-FRA cards

## Next

Done — every guest plays and packs hold them. A fresh session needs nothing from here; the
mechanics' rules are in `.claude/rules/magic-mechanics.md`, the cards' tests in `FraSpecialGuestTests`.

## Log

- 2026-10-20: created from LIM-1's interview.
- 2026-10-23: moved up to the sprint's second item (Shayne); MC-3.3 added.
- 2026-10-23: MC-3.1 and batch 1 of MC-3.2 — 291/295 FRA cards play. New pieces: `GrantsActivatedAbility`
  (+ `s.ActivatedAbilitiesOf`), `PermanentsEnterTapped`, `SearchLibrary.Filter`, `Is.Colorless`,
  `DiscardAtRandom` by a `Count`, `CreateTokenCopy.ExileAtNextEndStep`. Goldens moved only where the
  new cards joined decks (30 every-card games, 195 of 284 card replays — the per-card decks rank 10
  others by a name hash); no value in FRA.values.json moved. AI tests no slower (bench: −2.9%).
- 2026-10-23: MC-3.4 and MC-3.3 — 295/295. New: `From.Ability` + `Is.TriggeredAbility` (abilities on
  the stack as targets), replicate, "choose one or more", `MayEnterAsCopy` + `EnteredAsCopy`, vanishing
  (`Card.TimeCounters`), and Necrodominance's four rules. A permanent spell now resolves in two steps
  (enter, then priority) — so Flesh Duplicate's choice beats the state check; no other golden game or AI
  replay moved for it. Goldens moved only where the new cards joined decks; AI tests no slower (−1.6%).
