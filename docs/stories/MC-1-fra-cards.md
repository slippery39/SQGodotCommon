---
id: MC-1
title: A real-rules engine that plays every Reality Fracture card
status: done
area: MC
created: 2026-10-07
---

## Why

Shayne plays Magic (Arena directs, sealed) and lost money on decks that turned out wrong after one
game. He wants to playtest builds before queueing, explore sealed and draft builds, and run sims —
and building the engine is the fun part. MtgCore is deliberately NOT Magic (one phase, no priority,
no blocking, two-track mana, a guaranteed three-land hand); retrofitting it would break CSC and the
sims. So a new engine, MagicCore.

## Requirements

- [x] Real rules: steps, priority, the stack, London mulligan, blocking, lands that tap for mana
- [x] Every Reality Fracture card plays — 284 of 285; Extrapolate the Impossible moved to LIM-1
      (it needs a sideboard)
- [x] Every scripted card has a test that its ability FIRES, through real casting and the real stack
- [x] A console to play the AI: `dotnet run --project MagicConsole`

## Decisions

2026-10-07, Shayne (interview — do not re-ask):
- **Goal:** both the tools and the engine; building the engine is the fun part. Forge is a reference,
  not a replacement.
- **First set:** Reality Fracture (FRA, released 2026-10-02, 285 cards).
- **Rules:** Arena-accurate; comprehensive-rules corners wait until a card needs them. **Bo1 only**
  for now (London mulligan + Arena's hand smoother); Bo3 and sideboarding maybe later.
- **Cards:** hand-coded abilities on Scryfall data (name, cost, type, P/T, Oracle text imported).
  No Forge script import, no Oracle parser.
- **Structure:** branch `mtg-real` from `MTG-ImmutableObjects-PlayingAround`; new projects on
  ImmutableGameObjects; **copy and own** from MtgCore, which is never modified for this.
- **Order:** engine first; tests and console first, Godot once playtesting vs the AI is the goal.
- **Use cases, all wanted eventually:** sims of builds, playtest vs AI, sealed pool to builds, draft vs bots.

2026-10-08, Shayne: grep MtgCore FIRST for every mechanic and copy-adapt what fits.

## Tasks

- [x] MC-1.1 Scryfall import, the rules core, targeting, triggers, effects as data, `GreedyAi`, MagicConsole — `aaf1b20`..`12186f9`
- [x] MC-1.2 Planeswalkers + Empower Jace, dual lands, modal spells, sacrifice and dies triggers — `b0552f3`..`1da6187`
- [x] MC-1.3 Continuous effects, step triggers, flashback, prepare, graveyard abilities, hexproof — `a78291a`..`66decfd`
- [x] MC-1.4 Impulse draw, reflexive triggers, counterspells and ward, Auras and Equipment, all uncommons — `0ca382a`..`c99e23b`
- [x] MC-1.5 Loses all abilities, unit-based mana, copying a spell — `64eb5ef`..`c8309f6`
- [x] MC-1.6 Every rare and mythic — `8dccc23`..`c7c6c2b`
- [x] MC-1.7 The card-work kit: generated catalog and card sheet, `/card-batch`, tools — `0e796b3`

## Next

Done. For a new set: `/card-batch`, `MagicCore/Cookbook.md`, `MagicCore/AbilityCatalog.md`. What is
scripted and why: `MagicCore/FraCoverage.md`. Known simplifications: `DesignNotes.md` "MagicCore
known ceilings" — not bugs.

## Log

- 2026-10-09: 284/285 FRA cards play; MagicCore.Tests 740 green.
- 2026-10-10: a fresh session per card batch measured 37–40% cheaper than eight in one session
  (`tools/usage_report.py`) — hence `/card-batch`'s "one batch per session".
