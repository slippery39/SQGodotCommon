---
id: TBL-1
title: MagicTable — play MagicCore games in Godot, the Arena way
status: done
area: TBL
created: 2026-10-09
---

## Why

Tests prove each ability fires, not that the game plays right. Shayne needs to SEE games to know the
cards are programmed correctly — so the table must show everything the engine decides (the stack,
priority, every choice, counters, the command zone), not just a pretty board.

## Requirements

- [x] Deck select: built-in FRA decks or a pasted Arena export, for either seat; seed
- [x] Arena layout in the locked Dark Arcane look (`MtgVisualDesign.md`): land row, phase bar, stack, priority
- [x] Permanents as Arena-style crops (tapped rotates, identical lands stack, counter badges)
- [x] Click objects in place; targeting by clicks; combat declared the Arena way
- [x] Arena default stops, toggled on the phase bar, plus Full Control
- [x] Auto-pay, plus click a land to tap it manually
- [x] Test tools: sandbox spawner, undo, seed shown and replayable, AI vs AI with pause / step / speed
- [ ] Emblems seen on screen — moved to TBL-2 (it needs a planeswalker ultimate)

## Decisions

2026-10-09, Shayne (UI interview — do not re-ask):
- A **new scene**, `MagicTable` (`MagicGame` clashes with MagicCore's class); copy-and-own — never
  modify MtgGame or MtgCore.
- **Arena layout, Dark Arcane look; PC mouse first.**
- Stops: Arena defaults, toggleable, + Full Control. Mana: auto-pay + manual tapping.
- AI pacing ~0.5 s per action, highlighted, with a log line; adjustable.
- v1 test tools: sandbox, undo/rewind, seed replay, AI-vs-AI watch.
- Built with plain Controls rather than Common's `Card2D` — a miss, deferred to TBL-3.

## Tasks

- [x] TBL-1.1 Wiring and FRA art — `d413934`
- [x] TBL-1.2 Arena stops and the table's game loop — `992394a`
- [x] TBL-1.3 The first playable board — `1b3675e`
- [x] TBL-1.4 Click the objects themselves — `afdb447`
- [x] TBL-1.5 All the state on show — `16e2db6`
- [x] TBL-1.6 Deck select, Arena import, sandbox, AI controls — `9a2029d`
- [x] TBL-1.7 Follow-ups from the first games: combat declaration, multi-pick choices, wrapping,
      modal spells always ask — `7855e12`, `c72e55b`, `1ec29e1`

## Next

Done. Map and rules: `.claude/rules/magic-table.md`; run flags (`--seed --decks --autoplay
--ai-vs-ai --put`) in `Commands.md`. Playtesting continues in TBL-2.

## Log

- 2026-10-09: six-step plan done; MagicCore.Tests 762, SQGodotCommon.Tests 137.
