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
├── BoardChoice.cs        is a pending choice answered ON the table? which objects light up (plain C#)
├── Targeting.cs          click a spell, then its targets: plays, narrowing by target (plain C#)
├── ChoicePanel.cs        the choices that are not about the table: library, graveyard, modes, yes/no
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
- **Objects are clicked where they are, as on Arena.** A choice whose offered objects are ALL on
  the table (permanents, your hand, the stack, the players) is a `BoardChoice`; one offering
  anything hidden, or no objects (`MagicRules.OptionsAreObjects` says which), uses the panel. Casting with targets narrows by clicks
  (`Targeting`). One router, `ObjectClicked`, takes every click — card, player plate, stack row.
- **Glows:** gold playable, blue clickable now, green picked, white what it is about, red/blue in
  combat — one style for every object (`CardViews.Outline`).
- **Stops live in MagicCore (`Stops`)**, tested there; the table only shows and toggles them.
