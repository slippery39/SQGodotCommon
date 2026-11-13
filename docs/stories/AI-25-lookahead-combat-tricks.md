---
id: AI-25
title: The look-ahead's attacker uses its tricks after blocks — a block isn't free when the attacker can pump
status: backlog
area: AI
created: 2026-11-13
---

## Why

Shayne's first notes session on the table (TBL-10.3, 2026-11-13, seed 1 AI vs AI): "AI2 should not block the Edgar here.
Trading the 2/3 Greenhouse [Propagator] for a 1/1 rat does not seem like a positive ev play in isolation. This is not a
trade a reasonable person would make with no additional information." Edgar, Ancient Bloodlord (2/3) attacked; AI 2
blocked with Greenhouse Propagator (2/3); AI 1 paid {2} and sacrificed Rank Rat — "{2}, Sacrifice another creature or
planeswalker: Put a +1/+1 counter on Edgar" — Edgar became 3/4 and the Propagator died.

The search's own account (`MagicConsole --record=… --explain=175`, the full search, same choice): Block 0.95, Don't
block −1.19. In the block line the attacker does NOTHING after the block — no activation — so the block only saved 2
life. The look-ahead's attacker never uses an activated ability once blockers are declared, so Edgar's trick is
invisible to the defender, and any "pump after blocks" card makes a block look free.

## Requirements

- [ ] The kept position plays as Shayne says: the search does not block Edgar with the Propagator while the attacker
      has {2} and a creature to sacrifice — with a control (no mana, or nothing to sacrifice: the block stays)
- [ ] The look-ahead's attacking side may use its activated abilities (and instants, if AI-3.6's responses don't
      already) after blockers are declared, when it scores — behind a switch
- [ ] Measured the AI-6 way (the suite, then `--until-clear`), kept by AI-5's rule; cost per decision reported

## Decisions

- 2026-11-13, Shayne: write the notes' causes up as stories (this one is cause C of three; A is AI-13.13, B AI-9.9).

Open questions:
- Is it the rollout's opponent model (AI-3's `OpponentStep`) or the combat step inside the tree that skips the
  activation? Read before choosing where it goes.

## Tasks

- [ ] AI-25.1 The position as a test: `KeptRecords.Before("ShayneNotesSeed1", 175)` (MagicCore.Tests/Records, kept
      2026-11-13 — today's default search still blocks: the control holds), plus a hand-built control
- [ ] AI-25.2 Find where the attacker's after-block actions are skipped (tree combat or `Rollout`), then the change
      behind a switch, and the AI-6 measurement

## Next

AI-25.1. Read `MagicCore/Ai/Rollout.cs` (the combat step and `OpponentStep`) and AI-3.6 (full responses) first. The
explain command: `dotnet run -c Release --project MagicConsole -- --record=game_records/2026-11-13_131625_seed1.json
--explain=175` (the record is untracked; the kept copy is `MagicCore.Tests/Records/ShayneNotesSeed1.json`).

## Log

- 2026-11-13: created from Shayne's first notes session (TBL-10.3); the search's explanation read at #175.
