---
id: LIM-5
title: An Arena-style deck builder — the deck as cards in columns, sorted and arranged, real mana symbols
status: in-progress
area: LIM
created: 2026-11-10
---

## Why

Shayne, 2026-11-10: the deck builder (`SealedScene`, sealed and draft) shows the deck only as a text list
at the side, its costs as `{B}` text. He wants MTGA's deck builder: the deck visible as cards, sorted by
curve or by creatures / non-creatures, cards moved around, and a colourless filter that works with the
colours. His two references (MTGA screenshots, 2026-11-10):
- **Deck view:** the pool as a card grid on top (filters and search above it); the deck below as columns
  of overlapping cards, one per mana value; the commander-style "deck box" and count at the left.
- **List view:** the pool as a larger card grid, the deck as a list at the right — each row the count,
  the name and the cost as real mana symbols.

## Requirements

- [ ] The deck list shows costs as mana symbols (W U B R G C, generic numbers, X, hybrid), never `{B}` text
      — in the draft's picks list too (`DraftScene`)
- [x] Colourless filters alongside the colours: W + Colourless shows both; picking it never clears the colours
- [ ] A visual deck view as Arena's: the deck as columns of overlapping cards, one per mana value
      (lands in their own column), the count per column; a toggle back to today's list view
- [ ] Sort the deck view: by mana value (default), creatures / non-creatures (two rows, each by mana
      value), by colour
- [ ] Move cards: click or drag pool → deck and deck → pool; drag a card between deck columns to arrange it
- [ ] Search by name and rules text — LIM-4 (its own story, built alongside)
- [ ] Everything stays in view at 1920×1080 and 1280×720 — reorganise or scroll, never clip (TBL-2.5's rule)

## Decisions

- 2026-11-10, Shayne: MTGA is the guide (the two screenshots above).
- 2026-11-10, Claude (open to change): the symbols are Scryfall's official symbol SVGs (the same source as
  the card art — "official art first"), fetched once into `MagicTable/Assets/Symbols/`; one helper turns a
  cost string into an inline row of icons, used by the deck list and the draft's picks.
- 2026-11-10, Claude (open to change): a hand arrangement (a card dragged to another column) lasts until a
  sort is picked again; it is not saved with the deck. Multi and Lands stay "kind" filters as today.

## Tasks

- [ ] LIM-5.1 Mana symbols: fetch Scryfall's symbol SVGs, a cost → icons helper, used in the deck list and
      `DraftScene`'s picks
- [x] LIM-5.2 Colourless combines with the colour filters (`SealedScene.PickFilter` / `Shows`) — a colour toggle now
      (`SealedSession.InColours`, tested; nonland, as before); captured with real clicks: W + Colourless shows both
- [ ] LIM-5.3 The visual deck view: columns by mana value, a list/visual toggle; captured at both sizes
- [ ] LIM-5.4 Sorts: mana value, creatures / non-creatures, colour
- [ ] LIM-5.5 Moving cards: drag pool ↔ deck, drag between columns (copy the hand's 12 px drag threshold
      from `MagicHandView`; read `.claude/rules/godot-frontend.md`, "The shared card", first)

## Next

Read `SealedScene.cs` (617 lines — the deck view should be its own file, not grow this one),
`.claude/rules/magic-table.md`, and capture the screen before changing it
(`./Run-Godot.ps1 MagicTable/SealedScene.tscn -Capture`, Commands.md). LIM-4's search goes in the same
filter row.

## Log

- 2026-11-10: created from Shayne's notes, with two MTGA screenshots as the guide.
- 2026-11-10: LIM-5.2 built (overnight).
