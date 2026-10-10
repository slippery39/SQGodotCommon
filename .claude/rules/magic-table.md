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
├── DeckSelectScene.cs    the entry: both seats' decks (built-in or a pasted Arena export), seed, AI vs AI
├── MagicTableScene.cs    layout + redraw from State on every change; clicks → moves; AI timer
│                         (pause / step / speed)
├── SandboxPanel.cs       search any playable FRA card, put it in a zone (MagicCore.Sandbox)
├── CardViews.cs          permanent crops (tapped lies sideways, lands stack), hand cards, thumbs
├── PhaseBar.cs           both players' steps; each step is a stop toggle
├── BoardChoice.cs        is a pending choice answered ON the table? which objects light up (plain C#)
├── Targeting.cs          click a spell, then its picks — targets, then what a cost takes (plain C#)
├── CombatDeclaration.cs  attacks / blocks clicked on the table, submitted at once (plain C#)
├── ChoicePanel.cs        the choices that are not about the table: library, graveyard, modes, yes/no
├── PlayerPlate.cs        life, counts, zone buttons, emblems, floating mana; a click target
├── ZoneViewer.cs         a graveyard / exile / command zone as full cards, live
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
- **Combat is declared as a whole, as on Arena** — click creatures to attack (again to take back),
  or your creature then the attacker it blocks; one submit. The engine asks one creature at a time
  (`DeclareAttackerChoice`); `MagicTableManager.DeclareCombat` answers them all from the clicks.
  The first version showed those per-creature questions as buttons — Shayne attacked with nothing.
- **`GetPendingChoice()` builds a NEW choice object on every call** — `ReferenceEquals` on two
  calls is always false. "Still the same question?" compares the immutable `GameState`
  (`_shownState`). Comparing choices wiped every pick of a multi-pick choice on each redraw (Step 4
  shipped it) and every click of a combat declaration.
- **Every pick is clicked, even when only one is legal** (Shayne: a spell casting itself was a
  surprise; Arena and MTGO ask) — targets, then a cost's sacrifice / discard / behold / exile.
  "Sacrifice or pay" is chosen as a WAY first (`Targeting.Ways`), never listed per combination.
- **Action-column buttons wrap** (`AddButton`): a long label once grew a button off the screen.
  Lists use the plain plate in the body font; only main actions use the big oxblood style.
- **Glows:** gold playable, blue clickable now, green picked, white what it is about, red/blue in
  combat — one style for every object (`CardViews.Outline`).
- **Stops live in MagicCore (`Stops`)**, tested there; the table only shows and toggles them.
