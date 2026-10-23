---
id: TBL-3
title: MagicTable's cards use Common's Card2D, with drag-to-play
status: ready
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
- 2026-10-23, Shayne + Claude (sprint planning): **hand only**, copied from KIN — the battlefield
  keeps its crops for now (the "more rigid" cards). KIN already ships Common's fan with drag on PC
  and phone, so the misfit (the table rebuilds every row on each change; a `Card2D` is a living
  node with tweens) is solved there: `KinHandView.Sync` matches cards by game id, so a redraw adds
  and removes only what changed. Card2D, not Godot's own Control drag-and-drop: it is Common's, and
  proven on a phone.
- 2026-10-23, Shayne: phone is a nice-to-have for later; AI speed on a phone a slight concern, not
  a blocker.

## Tasks

- [x] TBL-3.1 Answer the open question — hand only, KIN's fan (2026-10-23)
- [ ] TBL-3.2 The hand as `Hand2D` + `Card2D` (sprint item 6): copy-adapt `KinHandView` (Sync by
      id; `CardDragEnd` → the same `ObjectClicked` router; a refused drop slides back and says the
      engine's reason) and `KinCardTap` (the first tap takes, on a phone too); the face is the
      Scryfall image through `Details.Decorate`, the shared scene untouched; the hover preview
      keeps working

## Next

**TBL-3.2** (sprint item 6). Read in this order:
1. `.claude/rules/godot-frontend.md`, "The shared card": `Card2D`'s 0.7 scale, its ready-only fill,
   the hover and drag traps. KIN's code was written around them.
2. `.claude/rules/magic-table.md`.
3. KIN's code, on the `kin-pivot` branch. Read it without switching branches:
   `git show kin-pivot:SQGodotCommon/KinGame/KinHandView.cs`, then `…/KinCardTap.cs`, then
   `kin-pivot:.claude/rules/kin-frontend.md` (Hand2D's ZIndex, `DrawCard`'s origin).

Common's `Cards/2D` is the same on both branches (a 12-line difference). KIN is standalone: copy from
it, never push to it.

## Log

- 2026-10-16: created from the DesignNotes entry.
- 2026-10-23: the open question answered (hand only, KIN's fan); status → ready; TBL-3.2 added.
