---
paths:
  - "SQGodotCommon/MagicTable/*.cs"
  - "SQGodotCommon.Tests/MagicTableTests/*.cs"
---

# MagicTable — the Godot front end for MagicCore

Arena's layout in the locked Arcane look (`MtgVisualDesign.md`), PC first. The interview answers
that shaped it, and what was built, are story TBL-1 (`docs/stories/`); playtesting is TBL-2.

```
MagicTable/
├── MagicTableManager.cs  THE bridge to MagicCore (plain C#, no Godot types): game, seed, replay,
│                         stops, one AI move per StepAi (the search capped at ~1 s, or GreedyAi),
│                         undo, log, the game record (every move via Play, from its token, written
│                         to game_records/ — story TBL-5); Resume: a record's moves made again, an
│                         undo step at each of yours, written as a new file (TBL-5.7). Tested in SQGodotCommon.Tests
├── DeckSelectScene.cs    the entry: both seats' decks (built-in or a pasted Arena export), seed, AI vs
│                         AI, search AI; the way into Sealed and Draft; Resume a game (a record from game_records/);
│                         TableSetup.Finished: a run's game reports its result once, then only the way back
├── DraftSession.cs       an FRA Quick Draft (plain C#, LIM-2.3): you in seat 0 and 7 bots (MagicCore's Draft),
│                         your picks as a SealedSession (Builder), the run — 7 wins or 3 losses — against
│                         each bot's own build in turn (BotDeck, NextBot, Record). Tested
├── DraftScene.cs         the pack as full cards (click takes one), your picks by colour in curve order and
│                         the curve; done → SealedScene.OpenDraft; --seed=N, --autopick (to the deck building)
├── SealedSession.cs      a sealed event (plain C#): your pool (a seed, or pasted from Arena), the deck
│                         from the pool, free basics, the suggested builds (two colours, splash,
│                         greedy — LIM-3; Suggest(i), Measured(i): castable on time, early landcycles; ClosePairs/ShowPair(i): a
│                         close pair's builds added — LIM-3.6; SimCommand: --compare for this pool — LIM-3.4),
│                         the table setup — a FRESH
│                         AI opponent per game, from the game's seed (OpponentFor). Tested
├── SealedScene.cs        Arena's deck builder: pool by colour (click in), deck list (click out), basics;
│                         Paste Arena pool (a dialog), what was left out named above the pool; a
│                         button per suggestion in a wrapping row, named by its colours; the close
│                         pairs in the left column (scrolling), Copy sim command above them; --seed=N
│                         opens that pool, --pool=FILE an export; a DRAFT's pool too (OpenDraft: the run
│                         in the title, Start plays the next bot, New draft)
├── MagicTableScene.cs    layout + redraw from State on every change; clicks → moves; AI timer;
│                         Tools menu (undo, sandbox, full control, pause / step / speed); the turn
│                         banner; each side's board half (gold on its turn); permanents scaled to the room
├── SandboxPanel.cs       search any playable FRA card, put it in a zone (MagicCore.Sandbox)
├── MagicHandView.cs      your hand: Common's Hand2D + Card2D fan (KIN's, TBL-3), Scryfall image as the
│                         whole face, glow as a plate behind it; click AND drag → ObjectClicked
├── CardViews.cs          permanent crops (tapped lies sideways, lands stack), card faces (zone viewer), thumbs;
│                         Hook: hover, click, hold-to-zoom for every card
├── PhaseBar.cs           the active player's steps (Stops shows both); each step is a stop toggle
├── BoardChoice.cs        is a pending choice answered ON the table? which objects light up (plain C#)
├── Targeting.cs          click a spell, then its picks — targets, then what a cost takes (plain C#)
├── CombatDeclaration.cs  attacks / blocks clicked on the table, submitted at once (plain C#)
├── ChoicePanel.cs        the choices that are not about the table: library, graveyard, modes, yes/no
├── PlayerPlate.cs        life, counts, zone buttons, emblems, floating mana; a click target
├── ZoneViewer.cs         a graveyard / exile / command zone as full cards, live
└── MagicArt.cs           Scryfall art crop / full card by image key — "Name|SET" (MagicArt.Key): the printing's
                          own art in Art/<set>/ first (a reprint), else the shared folder (tools/fetch_set_art.py)
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
- **A modal spell asks for its mode, always** (`Targeting.Modes`): every mode listed, one that can't
  be chosen greyed with why — Surgical Precision used to cast its only legal mode by itself.
- **The hand is a Card2D fan, and a fan card is clicked AND dragged** (`MagicHandView`). A Card2D
  drag starts on the PRESS, which clears the hover its click needs — so dragging is enabled only
  once the pointer moved 12 px from the PRESSED card (`Hand2D.DragEnabled`, read when the drag asks:
  it sees each motion before the scene's `Track`), and a still press and release is a click, on
  release; a 0.4 s hold zooms, as every card. Movement is measured from the EVENTS too: `--tap`'s
  synthetic input moves no real mouse, and a "drag" check that read the mouse was a click.
  A drop on the table goes to `ObjectClicked`; a refused one slides back and says why. A card being
  dragged is never freed by a redraw (Common's static drag state would stay set: no drag again).
  Hand2D's `_Input` (a debug "D draws a card") is switched off.
- **Card hover is physics picking**: the scene root and the hand's space ignore the mouse, and the
  overlays sit at `ZIndex` 300, above the fan's cards (Hand2D gives each one a ZIndex).
- **The fan is placed on the space's `ItemRectChanged`, not `Resized`** — the container MOVES the
  space after the first draw, and the fan stayed at the top of the screen, unseen.
- **A Scryfall face hides every Card2D layer but the frame** (`Decorate`, idempotent), with
  `FrameColor` white and `Holographic` off — the shared shader tinted the cards green. The glow is a
  plate behind the image: the shader's outline traces transparency, and a JPG has none.
- **Action-column buttons wrap** (`AddButton`): a long label once grew a button off the screen.
  Lists use the plain plate in the body font; only main actions use the big oxblood style.
- **Glows:** gold playable, blue clickable now, green picked, white what it is about, red/blue in
  combat — one style for every object (`CardViews.Outline`).
- **Everything stays in view** (Shayne, 2026-10-23): a screen that doesn't fit is reorganised or
  scrolls — never clipped off the edge. Capture every screen a change touches (TBL-2.5: a new button
  pushed Start off the deck select).
- **Stops live in MagicCore (`Stops`)**, tested there; the table only shows and toggles them.
- **A card's click fires on RELEASE** (`CardViews.Hook`): holding it 0.4 s zooms instead (a phone's
  only way to read a card), so a hold must never also play it. A new click target on a card goes
  through `Hook`, not its own `GuiInput` press handler.
- **A prepared copy is cast from its creature, never shown in the hand row** (Shayne, TBL-2.7):
  `_preparedCopies` maps each creature to its copy FROM THE LEGAL MOVES (any exile — a stolen
  creature's copy is its owner's), the creature glows and takes the click. Its image is the creature's
  (`MagicArt.ImageName`) — by its face's name it showed another card.
- **The permanents' scale comes from the SCREEN, never from the board's own size** — the board
  grows with its cards, and reading it back grew them until the hand left the screen (TBL-4.2).
