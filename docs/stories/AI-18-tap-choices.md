---
id: AI-18
title: Tap choices without ballooning moves
status: backlog
area: AI
created: 2026-11-05
---

## Why

TRK taps creatures for costs everywhere: Station ("tap another creature you control", one legal action
per creature), Worf's "tap two" (one per pair, in both orders), and since MC-5.3 batch 5b "tap three" and
"tap seven" (Collation Trimatrix, Captain Jean-Luc Picard: one per SET, every combination —
`MagicRules.TapSets`). Each is a branch for the AI's search, and they multiply with targets and modes:
Picard with 9 Officers is 36 moves, with 12 it is 792. Shayne, 2026-11-05: "There is a lot of tapping
creatures in this set (station as well), which has ballooning choice combinations. Lets pick the easiest
to implement to get started, but we might want to have a new story which looks into this and any
potential optimizations." The shortcut is in `DesignNotes.md` (MagicCore known ceilings).

## Requirements

- [ ] Measured first: how many moves tap costs add in real TRK games (self-play, the position suite),
      and what they cost the search per decision.
- [ ] Fewer moves for the same choice, the player still picking every creature on the table.
- [ ] The AI's pick no worse, measured by a match.

## Decisions

Open questions:
- Which way: creatures that don't differ (two untapped vanilla Officer tokens) as one choice; a cost
  picked one creature at a time after activating (a pending choice, as blockers are); or the AI
  offered only the sets a heuristic ranks best (the weakest, the summoning sick) while the table
  offers them all.

## Tasks

- [ ] AI-18.1 Measure the moves tap costs add, and their cost to the search, on TRK self-play

## Next

Read `MagicRules.TapSets` and `Taps` (MoveKey), `ActivateAbilityAction.TapIds`, `TapUntappedCost`
(`Effects/AdditionalCosts.cs`) and the table's `Targeting.Narrow`. Measure before choosing (AI-18.1).

## Log

- 2026-11-05: created — Shayne, answering batch 5b's "how are the N chosen".
