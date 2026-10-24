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
