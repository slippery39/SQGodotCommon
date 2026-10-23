---
id: MC-3
title: The 10 FRA Special Guests play
status: in-progress
area: MC
created: 2026-10-20
---

## Why

One FRA play booster in 55 swaps a common for a Special Guest (set SPG, #159–168), so about one
sealed pool in nine holds one. Shayne (2026-10-20): script all 10 before LIM-1's pools contain them.

## Requirements

- [x] The 10 import into the card data: Austere Command, Consider, Consign to Memory, Eye of Ugin,
      Flesh Duplicate, Mind Twist, Necrodominance, Root Maze, Splinter Twin, Sublime Epiphany
- [ ] Each is scripted and has a test that it FIRES (`/card-batch` tiers)

## Decisions

- 2026-10-20, Shayne: all 10, before pools contain them (LIM-1).
- 2026-10-23, Shayne (sprint planning): a priority — a real Arena pool can hold one, and the sealed
  practice needs the full experience. Sprint item 2, right after LIM-1.7.
- 2026-10-23, Claude: the guests live IN `FRA.json` (the importer's `GUESTS` query, after FRA's own
  cards), so every reader of the set — `FraCards`, the art, the sheet, the ratings — sees them with
  no second set to load. `Boosters.Guests` names them (the special-guest sheet).
- 2026-10-23, Claude: MC-3.2 split in two batches (the story allowed it): the four with the most
  engine work wait for a fresh session.

## Tasks

- [x] MC-3.1 Import SPG #159–168 beside FRA — `GUESTS` in `tools/import_scryfall_set.py` (read by
      `fetch_set_art.py` too); art fetched and imported
- [ ] MC-3.2 Script and test them — batch 1 DONE (Consider, Austere Command, Mind Twist, Eye of Ugin,
      Root Maze, Splinter Twin; `FraSpecialGuestTests`, 8/8 revert-checks); batch 2 is MC-3.4
- [ ] MC-3.4 Batch 2: Consign to Memory, Sublime Epiphany, Flesh Duplicate, Necrodominance
- [ ] MC-3.3 Packs contain them: `Boosters.GuestsPlayable` → true (1 pack in 55), the rate test
      checks it; LIM-1.7's "not in the engine" path then only meets non-FRA cards

## Next

**MC-3.4, batch 2** — `/card-batch` in a FRESH session. All four are tier 3; grep MtgCore first:
- **Consign to Memory + Sublime Epiphany:** targeting an ability ON THE STACK (`StackAbility` is not a
  card; `TargetSpec` `From.Stack` only takes cards today) — "triggered ability or colorless spell",
  "activated or triggered ability". Countering an ability already works (`StackResolution.Counter`).
  Replicate {1} (a count paid at cast, then that many copies — `CopySpell` exists). "Choose one or
  more" (`SpellEffectsComponent.Choose` is an exact count; `ModeChoices` sets Min = Max).
  `Is.Colorless` exists.
- **Flesh Duplicate:** "enter as a copy of any creature" (a choice as it enters;
  `Card.WithCopiableValuesOf`) and vanishing 3 (time counters, an upkeep trigger, sacrifice).
- **Necrodominance:** skip your draw step; at your end step pay any amount of life, draw that many (a
  number choice); maximum hand size five; your cards and tokens go to exile instead of your graveyard
  (`MoveCard` already has exile-instead cases).
Then **MC-3.3**: `GuestsPlayable` → true; `EveryFraCard_IsPlayable` stops excluding the guests by itself.

## Log

- 2026-10-20: created from LIM-1's interview.
- 2026-10-23: moved up to the sprint's second item (Shayne); MC-3.3 added.
- 2026-10-23: MC-3.1 and batch 1 of MC-3.2 — 291/295 FRA cards play. New pieces: `GrantsActivatedAbility`
  (+ `s.ActivatedAbilitiesOf`), `PermanentsEnterTapped`, `SearchLibrary.Filter`, `Is.Colorless`,
  `DiscardAtRandom` by a `Count`, `CreateTokenCopy.ExileAtNextEndStep`. Goldens moved only where the
  new cards joined decks (30 every-card games, 195 of 284 card replays — the per-card decks rank 10
  others by a name hash); no value in FRA.values.json moved. AI tests no slower (bench: −2.9%).
