---
id: TBL-2
title: Playtest MagicTable until the games look right
status: in-progress
area: TBL
created: 2026-10-09
---

## Why

MagicTable (TBL-1) exists so Shayne can see whether the cards play correctly. That only happens by
playing. Each game that looks wrong somewhere is a bug report — root CLAUDE.md: "It looks a bit off"
is a bug report, never about the look.

## Requirements

- [ ] Shayne plays games across the three FRA decks (and his own Arena exports) until a session
      turns up nothing new
- [ ] Every report is a task here (or its own story if it is big), fixed at the root cause and
      pinned by a test that fails without the fix
- [ ] Emblems seen on screen (never verified — TBL-1)

## Decisions

- 2026-10-09, Shayne: his "looks off" reports drive the fixes.
- A known engine simplification is not a bug: check `DesignNotes.md` "MagicCore known ceilings"
  before reporting or fixing (the legend rule and proliferate choose for you; blocker damage splits
  automatically; …).
- An implausible AI play is an AI story's, not this one's: rebuild it in
  `MagicCore.Tests/Ai/AiPositionTests.cs` (as AI-1 did the Rank Rat) and add it to the AI story it fits.
- 2026-10-16, Shayne: checkboxes get themed box icons (TBL-2.4), not plate toggles.

## Tasks

- [ ] TBL-2.1 Emblems on screen: sandbox a planeswalker (`--put`), reach its ultimate, check the
      command zone on the player plate and in the zone viewer
- [x] TBL-2.2 Shayne's first playtest session — reports become tasks below — 2026-10-20 against the
      search (W-B vs R-G): two AI plays → AI-8; the turn was unclear → TBL-4; no seed saved → TBL-5
- [ ] TBL-2.3 If the AI ever again spends Prophesied End (or any removal) on a non-attacking 1-drop:
      keep that game's record (`MagicConsole --record=FILE --keep=NAME --at=N`, TBL-5.5) as a real
      position beside AI-1's hand-built one (moved from AI-1.4)
- [x] TBL-2.4 An unticked checkbox draws no box ("AI plays both seats", "Full control"; "Search AI"
      once unticked); a ticked one is ~10 px beside 24 px text. Cause: Godot's DEFAULT CheckBox
      icons — `mtg_theme.tres` styles no CheckBox — a faint dark outline on the dark backdrop, not
      scaled with the font override. Seen in a capture of DeckSelectScene, 2026-10-16. A look
      decision, so ask first (`MtgVisualDesign.md`): themed box icons sized to the text, or toggles
      as `MtgUiStyles.StyleChoice` plates (parchment = on), as the deck list already shows a pick.
      Done: themed box icons in the project theme
- [x] TBL-2.5 The deck select ran off the screen (Start cut off; Shayne, 2026-10-23): TBL-5.7's
      Resume button made the one bottom row wider than 1920. The options and the buttons are two
      rows now; captured, and the sealed screen checked too. Rule (Shayne): everything stays in view
      — reorganise or scroll, never clip
- [x] TBL-2.6 A prepared copy sometimes showed another card's art (Shayne, 2026-10-23). Cause: art
      by NAME, and a copy is named after its spell face, which three creatures share each ("Seed
      Suture", five such faces) — the known ceiling in `MagicArt`. Now through the copy's link to its
      creature (`MagicArt.ImageName`): hand row, stack, zone viewer, choices; `MagicArtTests`
- [x] TBL-2.7 A prepared copy sat in the hand row ("from prepared copy") and confused Shayne at
      first, though Arena does much the same (2026-10-23). Shayne: **on the creature**, for now. The
      copy no longer joins the hand row; its creature glows playable and a click on it casts the
      copy (alone, the click IS on the copy, so a modal one still asks its mode). Captured: the hand
      without it, the creature's click straight into Seed Suture's targets
- [x] TBL-2.8 **A click away from a card sometimes plays a card** (Shayne, 2026-11-08, three times; the
      most destructive report). A hand click plays `CardUIManager.CurrentHoveredCard`
      (`MagicHandView.Track`), which is only refreshed once a frame from the mouse-over list
      (`CardUIManager._Process`). Suspect: a stale hover / the raised card's mouse area. Find the cause
      before fixing it; it is in Common, so KIN and MtgGame share it
      Fixed in `MagicHandView.Track`: a mouse event counts as on the hovered card only if its OWN
      position is inside the card's hit shape (`Over`), so a lagging hover can't turn a click on a land,
      a creature or empty board into a play. Cause (inferred, not reproduced): the window stalls while the
      AI searches, and a click delivered after the stall carried the hover from before the pointer moved.
      Captured: a click on a hand card plays it, a click beside it doesn't. Not pinned by a test (Godot input)
- [x] TBL-2.9 Auto-tapper tapped every black source with a black card in hand → TBL-8.1, fixed
- [x] TBL-2.10 With 10+ creatures the board's rows wrapped and pushed the whole UI down.
      `BoardScale` clamped the cards to at least 1×, so a row too wide for the board wrapped; it now
      shrinks them down to `MinBoardScale` (0.5×). Captured: 14 creatures in one row, the hand and phase
      bar where they were. Past ~20 permanents on a side the row still wraps (a `ponytail:` note). Not
      pinned by a test (Godot layout)
- [x] TBL-2.11 Cards in the hand are darker than they need to be (Shayne, 2026-11-08: "a slightly black
      tint", every card, in the hand only). Cause: the card's holographic shader sat on the face's
      container; even with `Holographic` off it drew the printed art at about half its brightness
      (a card's art area measured 41 on screen, 93 in the image — 93 again with the shader's material
      removed), while whites stayed white, so it read as a tint. `MagicHandView.Decorate` now drops
      the container's material for a printed face. Found by comparing a capture's pixels with the card
      image and switching the shader off — the algebra of the shader didn't show it. Not pinned by a test (Godot rendering)
- [x] TBL-2.12 "Double blocking didn't work" (Elim Garak, 021555 `game_records/2026-11-08_021555_seed146333.json`,
      T13, moves #302-#305). **Not an engine bug:** both Temporal Conquerors did block (`blk` on both); one
      died, but Dr. McCoy, Surly Surgeon's "remove +1/+1 counters instead of destroying" saved Garak (4/4 →
      3/3, damage healed) — exactly his card text. The table never said so
- [x] TBL-2.13 …so the log says when Dr. McCoy saved a creature: "Elim Garak, Simple Tailor would be
      destroyed: its +1/+1 counters are removed instead (Dr. McCoy, Surly Surgeon)" — a note from
      `MagicTableManager.Play`, read off the state like V'Ger's "seen in" (a ponytail: a creature that
      loses its counters another way with McCoy out is called saved too). Tested on the real game, kept
      as `MagicCore.Tests/Records/McCoySavesGarak.json` (`ACreatureSavedByDrMcCoy_SaysWhyItLived`). Which
      creatures blocked an attacker is TBL-4.3's
- [x] TBL-2.14 A choice whose options are cards in two or more graveyards (Raffi Musiker's "target card in a
      graveyard") shows a tab per player — "Your graveyard (2)" / "AI's graveyard (2)" — and one graveyard at a
      time; picks stay when you change tab, and the search box (24+ options) filters within the open one.
      `ChoicePanel`. Captured through the real flow: Raffi cast, the exile mode, the target panel, the second tab.
      Not pinned by a test (Godot UI). The ZoneViewer (a plate's button) already shows one zone at a time
- [x] TBL-2.15 (done as TBL-4.3) Blocks and attacks pair visibly (Arena's arrows, or the blocker moves beside its attacker);
      today two creatures with the same name can't be told apart → TBL-4
- [x] TBL-2.16 Casting from another card's permission (Shayne, 2026-11-08): Common Goal cast off The Caretaker shows a menu of
      every target COMBINATION. Cast it as from the hand instead - click the creatures on the board - and never get
      locked when there is no valid target: a Cancel button (don't cast it). Done: a pending `ChooseCastChoice`
      (CastNow's "how to cast it", one option per legal combination) runs the hand's `Targeting` flow over its casts
      (`MagicTableScene.StartCast`), the last pick answering the choice by index; **"Don't cast it (Esc)"** is always
      on screen - an empty answer, `ChooseCastChoice.MinChoices` 0 with the options untouched (a first version added a
      "Don't cast" OPTION and made every record with a cast choice stale: the offers fingerprint hashes the options,
      found when Shayne's own game would not replay). Checked on his game: `--resume-at=1021` of
      `2026-11-08_192946_seed1126905837`, a real click on the Saurian Explorer led to "choose target 2" with
      "No target". Test `CastNow_ThePlayerMayDeclineTheCast`. Known ceiling (`ExileTopMayCast`): Chandra's "if you
      don't" runs on a no to the up-front question only, not on backing out of the cast afterwards

## Next

**Reports so far live in their own stories:** AI plays in AI-8, the table's clarity in TBL-4, game
records in TBL-5. Every game is recorded to `game_records/`: for a report, note the turn and cards
involved; keep the position as an AI test case (`--record=FILE --keep=NAME --at=N`) or resume the
game on the table (Resume a game, TBL-5.7) to check a fix.

Run the table with `Run-Godot.ps1` (`Commands.md`: flags `--seed --decks --autoplay --ai-vs-ai
--greedy --put`). **The AI is the search since AI-2.8** (~1 s a move; the window stalls while it
thinks — DesignNotes); `--greedy` plays GreedyAi instead, and makes `--autoplay` fast. For each report: reproduce it from its record, read `.claude/rules/magic-table.md`
for the table side or `MagicCore/CLAUDE.md` for the engine side, find the cause before the fix.

## Log

- 2026-10-09: TBL-1 done; playtesting is next.
- 2026-10-09/10: first reports fixed inside TBL-1 (combat declaration, multi-pick choices,
  wrapping, modal spells) — `7855e12`, `c72e55b`, `1ec29e1`.
- 2026-10-16: TBL-2.4 added — unticked checkboxes are invisible (seen while AI-2.7 added "Search
  AI"); Next says the AI is now the search, and names `--greedy`.
- 2026-10-16: TBL-2.4 done — `check_off` / `check_on` (28 px: a leather plate with a brass rim, and
  the same with a gold tick) generated by `tools/make_mtg_ui.py` and set in `mtg_theme.tres`, so
  every CheckBox gets them. Seen in captures of DeckSelectScene (both states) and the table's "Full
  control". Not pinned by a test: SQGodotCommon.Tests can't load a Godot theme; the captures are the check.
- 2026-10-20: Shayne's first game against the search since AI-2.8 (and the honest library, AI-3.9;
  the threat term, AI-5): White-Black against the AI's Red-Green, seed not saved. Reports: (1) the
  AI's 2/2 didn't attack his 3-loyalty Jace token at a high life total, so Jace kept drawing — AI-8.1;
  (2) the AI surveiled Way of the Wildspeaker into its graveyard though it could cast it — AI-8.2;
  (3) he passed through his own turn thinking it was the opponent's, and the table "feels a little
  too busy" — TBL-4 (Arena-like first, the best of MTGO kept); (4) he wants exportable game records
  for analysis and AI test cases — TBL-5. Noted, not fixed (Shayne).
- 2026-10-22: Shayne's sealed session — three files in `game_records/`: a seed-5 game (124 moves,
  unfinished), the sealed game (seed 451259, 454 moves, played to the end) and a sealed start with no
  moves. AI-8.3's report (the Duelist that wouldn't trade) came from the sealed game, kept as
  `Records/DuelistWontTrade.json`; TBL-5.7 resumed it.
- 2026-10-23: Shayne's sealed game — the deck select off the screen (TBL-2.5), a prepared copy's
  wrong art (TBL-2.6), the prepared copy in the hand confusing (TBL-2.7). Status → in-progress.
- 2026-10-23: Shayne decided TBL-2.7 — the prepared copy on its creature, for now; built and
  captured the same day.
