---
id: LIM-5
title: An Arena-style deck builder — the deck as cards in columns, sorted and arranged, real mana symbols
status: done
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

- [x] The deck list shows costs as mana symbols (W U B R G C, generic numbers, X, hybrid), never `{B}` text
      — in the draft's picks list too (`DraftScene`)
- [x] Colourless filters alongside the colours: W + Colourless shows both; picking it never clears the colours
- [x] A visual deck view as Arena's: the deck as columns of overlapping cards, one per mana value
      (lands in their own column), the count per column; a toggle back to today's list view
- [x] Sort the deck view: by mana value (default), creatures / non-creatures (two rows, each by mana
      value), by colour
- [x] Move cards: click or drag pool → deck and deck → pool; drag a card between deck columns to arrange it
- [x] Search by name and rules text — LIM-4 (its own story, built alongside)
- [x] Everything stays in view at 1920×1080 and 1280×720 — reorganise or scroll, never clip (TBL-2.5's rule)

## Decisions

- 2026-11-10, Shayne: MTGA is the guide (the two screenshots above).
- 2026-11-10, Claude (open to change): the symbols are Scryfall's official symbol SVGs (the same source as
  the card art — "official art first"), fetched once into `MagicTable/Assets/Symbols/`; one helper turns a
  cost string into an inline row of icons, used by the deck list and the draft's picks.
- 2026-11-10, Claude (open to change): a hand arrangement (a card dragged to another column) lasts until a
  sort is picked again; it is not saved with the deck. Multi and Lands stay "kind" filters as today.

## Tasks

- [x] LIM-5.1 Mana symbols: fetch Scryfall's symbol SVGs, a cost → icons helper, used in the deck list and
      `DraftScene`'s picks — `tools/fetch_mana_symbols.py` (61 symbols: every one a cost can hold, {T}, {Q});
      `ManaSymbols.Bbcode`; one row for both lists, `SealedScene.CardRow`: the name, then the cost at the right as
      Arena's list. Tests: the BBCode, and every symbol FRA and TRK print has its file. Captured: the deck list
      (hybrids read) and the draft's picks. Elsewhere still `{B}` text (the plate's floating mana, the log)
- [x] LIM-5.2 Colourless combines with the colour filters (`SealedScene.PickFilter` / `Shows`) — a colour toggle now
      (`SealedSession.InColours`, tested; nonland, as before); captured with real clicks: W + Colourless shows both
- [x] LIM-5.3 The visual deck view: columns by mana value, a list/visual toggle; captured at both sizes
      `DeckColumns` (plain C#, tested: rows of columns, copies counted) and `DeckView` (Godot): under the pool, each
      column's cards overlapping by their name line, a header "key · count", ×N on copies; Cards / List toggle
      (Cards the default, kept between visits); a click takes a card out (a basic: one fewer). Both sizes are one
      layout (`canvas_items` stretch); captured with a suggested build, and in List mode
- [x] LIM-5.4 Sorts: mana value, creatures / non-creatures, colour — "Mana value" (0–6, 7+, Lands), "Creatures
      apart" (two rows; the view scrolls to the second), "Colour" (W U B R G Multi Colourless Lands); captured each
- [x] LIM-5.5 Moving cards: drag pool ↔ deck, drag between columns (copy the hand's 12 px drag threshold
      from `MagicHandView`; read `.claude/rules/godot-frontend.md`, "The shared card", first)
      Godot's own Control drag-and-drop (`DeckView.Wire`), not Card2D: these are plain TextureRects, and Godot's
      drag starts after its own threshold, so a still click stays a click. Every card takes the drop as well as
      the space around it (a card stops the mouse). Real input: pool → deck (23 → 24, into its column), deck →
      pool (23 → 22), a 1-drop onto the 6 column (1·2 → 1·1, 6·3 → 6·4). `--tap`'s drag now sends `Relative`:
      Godot's Control drag adds up motion, and a drag with none never started

## Next

Built overnight 2026-11-10, unseen by Shayne: his look decides what changes (card size, the strip shown,
Cards as the default). Code: `DeckColumns.cs` (layout, tested), `DeckView.cs` (drawing, drag-and-drop),
`SealedScene.cs` (the toggle and sorts). Capture with `-GameArgs '--seed=7','--tap=1043,235@12'` (a suggested
build), drags as `--tap=X,Y@F~12>X2,Y2`.

## Log

- 2026-11-10: created from Shayne's notes, with two MTGA screenshots as the guide.
- 2026-11-10: LIM-5.2 built (overnight).
- 2026-11-10: LIM-5.1 built (overnight).
- 2026-11-10: LIM-5.3–5.5 built (overnight); the story is done — Shayne's look at it may add tasks.
