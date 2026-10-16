---
id: TBL-3
title: MagicTable's cards use Common's Card2D, with drag-to-play
status: backlog
area: TBL
created: 2026-10-09
---

## Why

Building games on Common's shared features is the standing intent. MagicTable draws every card as
plain Controls (`CardViews`) and plays by click only — it skipped `Common/Cards/2D` (`Card2D`,
`Hand2D`, `DraggableNode2D`) and drag-to-play, which the other games share. Shayne called it a miss
on 2026-10-09, deferred so playtesting could start. Full write-up: `DesignNotes.md`, "MagicTable
does not use Common's Card2D or drag-to-play (deferred)".

## Requirements

- [ ] The hand is `Hand2D` + `Card2D`, showing the Scryfall image as the whole face (exact Oracle
      text is the point of the table — no frame or text layers)
- [ ] Drag a card onto the table to play it, routed through the same `ObjectClicked` path
- [ ] Clicking still works everywhere (Arena supports both); `IsMoveFor` and `Targeting` reused

## Decisions

- 2026-10-09, Shayne: deferred until after playtesting starts.

Open questions:
- **Hand only, or the battlefield too?** Hand only keeps the battlefield crops and is the cheap
  option. Battlefield too means permanents as `Card2D` laid out by hand (a Container won't lay out
  a Node2D), so the row code changes.

## Tasks

- [ ] TBL-3.1 Answer the open question

## Next

Read `.claude/rules/godot-frontend.md` "The shared card" first — `Card2D`'s 0.7 scale, ready-only
fill, hover and drag traps — then `.claude/rules/magic-table.md`.

## Log

- 2026-10-16: created from the DesignNotes entry.
