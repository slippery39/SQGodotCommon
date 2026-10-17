---
id: TBL-2
title: Playtest MagicTable until the games look right
status: ready
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

## Tasks

- [ ] TBL-2.1 Emblems on screen: sandbox a planeswalker (`--put`), reach its ultimate, check the
      command zone on the player plate and in the zone viewer
- [ ] TBL-2.2 Shayne's first playtest session — reports become tasks below
- [ ] TBL-2.3 If the AI ever again spends Prophesied End (or any removal) on a non-attacking 1-drop:
      capture that game's seed as a real position beside AI-1's hand-built one (moved from AI-1.4)
- [ ] TBL-2.4 An unticked checkbox draws no box ("AI plays both seats", "Full control"; "Search AI"
      once unticked); a ticked one is ~10 px beside 24 px text. Cause: Godot's DEFAULT CheckBox
      icons — `mtg_theme.tres` styles no CheckBox — a faint dark outline on the dark backdrop, not
      scaled with the font override. Seen in a capture of DeckSelectScene, 2026-10-16. A look
      decision, so ask first (`MtgVisualDesign.md`): themed box icons sized to the text, or toggles
      as `MtgUiStyles.StyleChoice` plates (parchment = on), as the deck list already shows a pick

## Next

Run the table with `Run-Godot.ps1` (`Commands.md`: flags `--seed --decks --autoplay --ai-vs-ai
--greedy --put`). **The AI is the search since AI-2.8** (~1 s a move; the window stalls while it
thinks — DesignNotes); `--greedy` plays GreedyAi instead, and makes `--autoplay` fast. For each report: reproduce it with the seed + undo, read `.claude/rules/magic-table.md`
for the table side or `MagicCore/CLAUDE.md` for the engine side, find the cause before the fix.

## Log

- 2026-10-09: TBL-1 done; playtesting is next.
- 2026-10-09/10: first reports fixed inside TBL-1 (combat declaration, multi-pick choices,
  wrapping, modal spells) — `7855e12`, `c72e55b`, `1ec29e1`.
- 2026-10-16: TBL-2.4 added — unticked checkboxes are invisible (seen while AI-2.7 added "Search
  AI"); Next says the AI is now the search, and names `--greedy`.
