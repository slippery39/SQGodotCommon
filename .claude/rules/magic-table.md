---
paths:
  - "SQGodotCommon/MagicTable/*.cs"
  - "SQGodotCommon.Tests/MagicTableTests/*.cs"
---

# MagicTable — the Godot front end for MagicCore

Arena's layout in the locked Arcane look (`MtgVisualDesign.md`), PC first. Interview answers that
shaped it are in the memory `project_mtg_real.md`; the build plan in `HANDOFF-MtgReal.md` §4.

```
MagicTable/
├── MagicTableManager.cs  THE bridge to MagicCore (plain C#, no Godot types): game, seed, replay,
│                         stops, one AI move per StepAi, undo, log. Tested in SQGodotCommon.Tests
├── MagicTableScene.cs    layout + redraw from State on every change; clicks → moves; AI timer
├── CardViews.cs          permanent crops (tapped lies sideways, lands stack), hand cards, thumbs
├── PhaseBar.cs           both players' steps; each step is a stop toggle
├── ChoicePanel.cs        every pending choice, generically (Describe.Question, Min..Max)
└── MagicArt.cs           Scryfall art crop / full card by name (tools/fetch_set_art.py)
```

- **The scene never decides a rule.** What is playable is `LegalActions` (via `DistinctMoves`);
  what a choice offers is its `Options`; text is `Describe`. A rule written here drifts from the
  engine — exactly what the AI/human parity rule in MagicCore forbids.
- **Redraw, don't patch.** `Refresh` rebuilds every row from the immutable state; there is no
  per-node bookkeeping to go stale.
- **`DistinctMoves` keeps ONE of identical cards**, so a click on the second Swamp in hand finds
  no move by id — ask `s.IsMoveFor(move, cardId)` (MagicCore, same identity as DistinctMoves).
- **A choice option's id is not always an object** — combat's "don't" is 0, modes number their own.
  Check `HasObject` before `GetObject`; an unchecked lookup crashed every attack declaration.
- **Several moves for one card go in the action column**, not a PopupMenu: a popup's rect is in
  window pixels, the table in design pixels.
- **Stops live in MagicCore (`Stops`)**, tested there; the table only shows and toggles them.
