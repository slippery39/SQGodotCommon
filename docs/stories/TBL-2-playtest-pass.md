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
- An implausible AI play is AI-1's, not this story's.

## Tasks

- [ ] TBL-2.1 Emblems on screen: sandbox a planeswalker (`--put`), reach its ultimate, check the
      command zone on the player plate and in the zone viewer
- [ ] TBL-2.2 Shayne's first playtest session — reports become tasks below

## Next

Run the table with `Run-Godot.ps1` (`Commands.md`: flags `--seed --decks --autoplay --ai-vs-ai
--put`). For each report: reproduce it with the seed + undo, read `.claude/rules/magic-table.md`
for the table side or `MagicCore/CLAUDE.md` for the engine side, find the cause before the fix.

## Log

- 2026-10-09: TBL-1 done; playtesting is next.
- 2026-10-09/10: first reports fixed inside TBL-1 (combat declaration, multi-pick choices,
  wrapping, modal spells) — `7855e12`, `c72e55b`, `1ec29e1`.
