---
id: MC-8
title: Every modal spell is one cast per choice of modes, its targets chosen after
status: backlog
area: MC
created: 2026-11-17
---

## Why

A modal spell with targets is still offered as one legal cast per choice of modes × every combination of its
targets, and its legality is checked against those combinations. MC-4.4e2f moved the "choose the same mode more
than once" spells (Fiery Confluence, Mystic Confluence) to one cast per choice of modes, with each target word
chosen as it is cast (`CastSpellAction.EachWord`). Two gaps are left:

- **Sublime Epiphany** (FRA, "choose one or more", five modes, four targeted) and every other modal spell with more
  than one mode still enumerate. Sublime Epiphany alone is 31 mode choices × spells × abilities × nonland permanents
  × your creatures × players.
- **A repeated mode never takes the same target twice** (`Modal.Compose` marks it `NotThePrevious`). That rule only
  kept the enumeration small. It is legal (CR 115.3) and sometimes the right play: Mystic Confluence countering ONE
  spell three times taxes it {3}, then {3}, then {3}. Today it can't (DesignNotes, "MagicCore known ceilings").

Shayne, 2026-11-17: "We don't want to have to test every possible combination every single time."

## Requirements

- [ ] Every modal spell (and modal activated ability) with targets is ONE legal action per choice of modes; its
      targets are chosen after, one word at a time. No action per combination.
- [ ] Legality never enumerates combinations: a short-circuit check, never `TargetSpec.Combinations`.
- [ ] A repeated mode may target what an earlier word targeted (CR 115.3): Mystic Confluence counters one spell three
      times. Fiery Confluence's test (`LsvBatch2c2Tests`) changes with the rule.
- [ ] A cast offered can always be finished: no word left with nothing to choose after earlier picks (today a
      ceiling — modes whose filters overlap; DesignNotes).
- [ ] The AI picks those targets well, measured: counters one spell three times when that is the play; bounces
      theirs, not its own.

## Decisions

- 2026-11-17, Shayne: backlog, not now. The fix belongs to every modal spell, not one card at a time.

Open questions:
- Sublime Epiphany is FRA: moving it changes the AI lane's recorded games (golden replays, perhaps
  `MagicCore.Tests/Records`). Is it done in the cube lane and merged, or in the AI lane?
- Modal activated abilities (`ActivateAbilityAction.Modes`, "one legal action per mode"): do any with targets
  enumerate on big boards? Count them first.
- The look-ahead answers the later words with the first legal target (`Lookahead.QuickAnswer`), as it does for a
  multi-target trigger. Is that good enough, or does each word need judging (`SettleDeep`)?

## Tasks

- [ ] MC-8.1 Count the legal actions of every modal spell and ability with targets (FRA, TRK, LSV) on a board of 12
      creatures, 12 artifacts and three spells on the stack. List the ones past a few dozen.
- [ ] MC-8.2 `EachWord` for every modal text with targets, not only `Repeats` — moving Sublime Epiphany and the rest
      found in MC-8.1.
- [ ] MC-8.3 Lift `NotThePrevious` from repeated modes in `Modal.Compose`; Fiery Confluence's test reads the rule.
- [ ] MC-8.4 A word's options never lead to a dead end (look ahead in `ChooseTargetChoice`'s options, or check the
      words together in `ChooseXTargetsChoice.CanChoose`).
- [ ] MC-8.5 The AI's picks, checked in positions (F7 → console mode 5): Mystic Confluence's triple counter, the bounces.

## Next

Start at MC-8.1: count before building (`/card-batch`'s "count the legal actions"). Read MC-4's Decisions, "4.4e2f's
plan" and "4.4e2f's notes" — the per-word choice is built there — and DesignNotes' two 4.4e2f ceilings. The code:
`CastSpellAction.EachWord` / `ChooseTargets` / `CastWithChosenTargetsAction.Words` (`Actions/StackActions.cs`),
`MagicRules.CastCandidates` (the `eachWord` branch), `Modal.Compose` (`Effects/Modes.cs`).

## Log

- 2026-11-17: opened from MC-4.4e2f (Shayne): Mystic Confluence's limit and the enumeration of the other modal
  spells, as one fix.
