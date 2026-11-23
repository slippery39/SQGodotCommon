---
id: MC-9
title: The cube's cards in the AI's hands - the deferred AI items, watched and fixed
status: in-progress
area: MC
created: 2026-11-23
---

## Why

Every card of the cube plays (MC-4, 528 of 528), but each batch watched the search use its cards and left a list
"for the AI lane at the merge" (MC-4's Next, from l1 + l2 to m3; DesignNotes' "for the AI lane" entries): lands
sacrificed for life nobody needs, counters burned for mana nobody spends, abilities never used, searches that take
over a minute a turn. Shayne, 2026-11-23, after merging mtg-real in: fix them here, in the cube lane.

Most were seen on the probe's test board of TWENTY BASICS (`EmptyBoard()`), which distorts three of them: with 20
lands a land is worth little (Zuran Orb's trade may be near right), an X activation is 21 legal actions (Wizard's
Rockets' 82 s), and nothing makes {C} (Sowing Mycospawn's kicker is {1}{C}). Each is watched on a realistic board first.

## Requirements

- [ ] Every item is WATCHED before it is fixed (CLAUDE.md, "Seeing what the AI is doing"): the position built as a
      test, the search's ranked moves and the chosen one's evaluator terms printed (`BeamSearchAi.Ranked`, `Explain` —
      how MC-4 diagnosed Pentad Prism), or F7 → console mode 5. The verdict is written here, also when it is "the AI
      is right".
- [ ] Each fix has a test that fails without it (revert-checked), reading authored values.
- [ ] A fix keyed to a cube card's shape moves no FRA or TRK decision: the position suite 0 changed and the golden
      replays unchanged — or each move named and explained before it is regenerated.
- [ ] A change to an evaluator TERM (anything every position reads) gets a `--match` before it is the default, as the
      AI lane's changes do (any time, unattended); its results go to `docs/findings/magic-ai.md`.
- [ ] A search filter lives in `Evaluator.SearchMoves`, before validation; `LegalActions()` stays complete (magic-ai.md).

## Decisions

- 2026-11-23, Shayne: the plan below, with the four DesignNotes items (Phase 5) in it — "those items … should be in
  the plan". In the cube lane, under an MC- id (two-lanes.md: the cube lane takes ids only from MC-). The evaluator is
  where the lanes overlap: a fix the AI lane needs is merged to it at once.

The plan, by likely cause, cheapest and clearest first:

- **Phase 1 — the search tries what can't gain** (move filters and rule answers, cube shapes only): Fire Covenant's
  shares, Vivi at 0 power, Wizard's Rockets' X, Oko's −5.
- **Phase 2 — a spent resource priced at nothing**: Pentad Prism and Loot's exhaust (diagnosed in MC-4's Decisions,
  2026-11-22: the counters and floating mana score 0, the removal ties, and `BeamSearchAi.Potential`'s tie-break prefers
  it); Zuran Orb and Squandered Resources (a land sacrificed). Option A: price the store in `Evaluator.Other` (a
  permanent whose mana costs a counter or an exhaust is worth about a land's mana per use left). Option B: never offer
  a no-{T} mana move the turn can't spend. Claude leans to A for the Prism and Loot — a price, not a rule.
- **Phase 3 — abilities never or wrongly used** (watch, then decide): Reckoner Bankbuster, Sowing Mycospawn's kicker,
  Karn's −2 over his +1, Kaldra Compleat moved off its Germ, Psychic Frog's discard, the Figures' shrink.
- **Phase 4 — coverage**: the companion's {3} has never been offered to the search; the six m3 cards unwatched.
- **Phase 5 — the evaluator's blind spots** (DesignNotes; they touch what FRA play reads, so each is measured): a
  flashed Necromancy's creature priced as lasting; a modal DFC never counted as a land; a crewed Vehicle counted a
  creature next turn; energy and poison priced at nothing.

- 2026-11-23, Claude — **MC-9.1 watched** (six lands, Fire Covenant in hand, the search playing the turn): fast — every
  decision under a second against 12 creatures (the first decision of a run, ~18 s, is warm-up) — but WEAK: against
  12 creatures it cast X = 4 on one 4/4; against 6, X = 2 on two 1-toughness creatures. The cause: a target set of more
  than `BeamSearchAi.MaxPicks` (32) combinations is answered by `GreedyAi`, whose divided-spell answer was "one target
  takes all" (a ponytail since AI-19.25), so the shares were seldom even asked. Fixed: the shares by rule, and the
  target set's candidates are each single target plus two kill sets (most kills first; the biggest threats first),
  each played out (`Settle`). After: X = 4 kills three of six, four of twelve. How much life to pay stays the
  evaluator's. Only Pyrokinesis and Fire Covenant reach that answer (Fury's trigger divides its words by
  `Division.Words`; TRK's Pure Energy stays casts), so no FRA or TRK decision moves.

Open questions:
- Phase 2, option A or B — decided once the Prism is watched again on a realistic board (Shayne).
- Phase 5's energy and poison: the table's `PlayerPlate` shows neither — that is the TBL lane's; only the evaluator here.

## Tasks

Phase 1:
- [x] MC-9.1 (done 2026-11-23) Fire Covenant's shares (`Division.AskShares`, a `ChooseNumberChoice` per target)
      answered by rule, never branched, as delve's picks are: each target the damage that kills it, the last the rest
      (`ChooseNumberChoice.ShareOf`, `Lookahead.KillingShare`). Watched first: the COST was not the problem — the play
      was (Decisions, "MC-9.1 watched"); so the target set got kill sets too (`GreedyAi.KillSets`). A TRIGGER dividing
      X (none yet) would need the same.
- [ ] MC-9.2 Vivi Ornitier's mana move at 0 power (it makes nothing and uses the turn's activation) left out in
      `SearchMoves`: a mana move whose yield is 0.
- [ ] MC-9.3 Wizard's Rockets: the turn timed on a 6–8 land board. Still slow: bound X in `SearchMoves` as Candelabra's
      is — up to the coloured pips the hand can't pay otherwise (DesignNotes, "{X} on an ACTIVATED ability").
- [ ] MC-9.4 Oko, Thief of Crowns' −5: what its two target choices cost the search, measured; left alone if small.

Phase 2:
- [ ] MC-9.5 Pentad Prism and Loot, the Pathfinder's exhaust mana: watched on a realistic board, then option A or B.
- [ ] MC-9.6 Zuran Orb and Squandered Resources on a 5–7 land board: if they still sacrifice, the land's price
      (land count, `LandUnlock`) is what is missing — an evaluator term, so a `--match`.

Phase 3:
- [ ] MC-9.7 Reckoner Bankbuster never activated ({2}, {T}: draw): does a card read worse than lands kept untapped
      (`Potential`)?
- [ ] MC-9.8 Sowing Mycospawn never kicked: first, is the kicked cast offered with a {C} source on the board?
- [ ] MC-9.9 Karn, Scion of Urza takes −2 (a Construct) over +1 (look at two, an opponent picks): how the walker's
      lifetime price (`Evaluator.Lifetime`) reads `LookAndTake { ChosenBy }`.
- [ ] MC-9.10 Kaldra Compleat re-equipped off its own Germ the turn it is cast; Psychic Frog discarding for a counter;
      a Figure's first ability on a bigger Figure — a verdict each (some may be right).

Phase 4:
- [ ] MC-9.11 The companion's {3} (`PutCompanionIntoHandAction`) in a position where the search should take it; the six
      m3 cards (Life // Death, Invert Polarity, Tamiyo, Lurrus, Lutri, Zirda) watched in the search's hands.

Phase 5:
- [ ] MC-9.12 A flashed Necromancy (DesignNotes, "Auras that return a creature"; Shayne, 2026-11-20): `MoveTo.
      LeavesAtTheEndStep` also true when an Aura on it sacrifices itself at the end step or cleanup and has a
      `SelfLeaves` `Sacrifice(Ref.EnchantedCreature)` — ~10 lines; a test both ways (flashed: priced 0; at sorcery
      speed: priced), revert-checked; the golden and AI replays unchanged.
- [ ] MC-9.13 A modal DFC in hand counts as a land (DesignNotes, "Modal double-faced cards"): the evaluator,
      `LandUnlock`, `SealedBuilder`, and `BlunderLint`'s "passed with a land to play"; `Rollout`'s land face only when no
      other land is offered (kept at the merge) re-read with it.
- [ ] MC-9.14 `Evaluator.NextTurn` undoes a crew stamp (`Animated.EndsAtCleanup`, DesignNotes "Crew"): a Vehicle crewed
      this turn is no creature next turn, its crew still are.
- [ ] MC-9.15 Energy and poison priced (DesignNotes, "Counters on a player"): energy as a resource held (the AI spends it
      all whenever the damage is worth anything), poison as a clock — a term in `Evaluator`, `--match`.

## Next

Next is MC-9.2 (Vivi at 0 power). The tests are `MagicCore.Tests/Ai/CubeAiTests.cs` (`SixLands()`: the realistic
board; a search-played turn is a loop of `BeamSearchAi.Decide` until the turn changes). Read MC-4's Decisions from "4.4l1 + l2" (the free-ability note, Shayne 2026-11-21) and the Pentad Prism
diagnosis (2026-11-22), and magic-ai.md. To watch a position: build it in a test as `LsvBatch2l3Tests` does
(`EmptyBoard()`, `Play(land)` in `FraBoardTest` — but use a realistic land count) and print `BeamSearchAi.Ranked` /
`Evaluator.Explain`. After each fix: `python -u tools/test_changed.py --run`, the suite (`--suite=beam`), and the
golden replays in the normal tests.

## Log

- 2026-11-23: opened (Shayne) after the merge of mtg-real (DEV-6, `2f2028bb`): MC-4's lists for the AI lane, plus the
  four DesignNotes items, as one plan in five phases.
- 2026-11-23: MC-9.1 done — Fire Covenant's shares and target set by rule; watched, it had been killing one creature
  of twelve; now four. Two tests in `CubeAiTests`, both revert-checked.
