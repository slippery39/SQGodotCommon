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
- **Phase 2 — a free ability used for nothing** (rewritten 2026-11-23 on Shayne's word, below): Pentad Prism and Loot's
  exhaust mana — a tie goes to the line making fewer mana moves; Zuran Orb and Squandered Resources — what they can
  still make is held, so spending it gains only what the spending triggers. (The first plan's options, a price on the
  store or a filter, are dropped: the cause was the tie, not the price.)
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
- 2026-11-23, Claude — **MC-9.2 watched** (six lands, Vivi Ornitier 0/3 on the battlefield, a 5-drop in hand): with a
  noncreature spell in hand (Lightning Bolt, Preordain) the order was RIGHT — the spell, Vivi grows to 1, her mana for
  {U}, the 5-drop. Without one, the search cast the 5-drop and then took her move at power 0 ("Vivi Ornitier:  for
  Blue", an empty amount): nothing made, the turn's activation used, a node the search pays for each turn. Left out of
  the search's moves; the rules still offer it.

- 2026-11-23, Shayne (reading MC-9.2's cause, "a mana move that adds no mana scored the same as not taking it"): **the
  same cause as the Prism, Zuran Orb and maybe Lightning Greaves — "All of these things are 0 mana abilities, and as
  such the opportunity cost to using them is non existent. Which means you don't need to use them right away."** Zuran
  Orb, unless a card makes the sacrifice pay (Titania, Baloth Prime, a life combo), is used as late as possible: it is
  free and at instant speed. Claude: in code, a tie went to ACTING (`Best`'s `PassBias` — a free draw off a Jace token
  is taken), so every free move that gains nothing was made; Phase 2 rewritten as one rule for the mana moves and the
  Orb's reserve. Greaves since MC-4.4l5 equips once a turn; not seen misused since — a board that shows it is a task.
- 2026-11-23, Claude — **MC-9.5 watched** (six lands; the search playing the turn, its ranked first moves printed):
  Pentad Prism with two counters removed both with nothing to cast (8.35 each way, an exact tie), removed both for a
  3-drop it then paid from lands, and both for a 7-drop that needed one; Loot, the Pathfinder exhausted for a 3-drop its
  lands paid, in combat — the mana emptied before the main phase. Three causes, each found in `Explain`: (1) the tie
  went to acting — and `Prune` (score, then potential) kept the Prism's removals over the line holding them, the
  floating mana paying part of the spell and keeping a land untapped; (2) the 7-drop's second counter: a line leaving a
  spell in hand gets the "a land drawn" rollout's credit (`LandMatters`: the hand costs more than our sources), and the
  floating {W} was not counted as mana, 40.75 against casting at once 40.24; (3) the ROLLOUT made mana moves on a tie too
  (its strict rule was for activations only): it exhausted Loot for White on its turn 3, tapping it out of the attack.
  Fixed: a line making more mana moves must BEAT the close lines making fewer (`BeamSearchAi.ManaMoves`, in `Best` and
  `Prune`); `LandMatters` counts the mana floating; the rollout takes a mana move only when it scores better. After: the
  Prism's counters held, one spent for the 7-drop; Loot's 3-drop paid from lands, Loot attacking, three drawn at their
  end step (its line 50.95 → 56.73). Zuran Orb on six lands HOLDS (Pass 8.35 against a sacrifice 7.85): the nine
  sacrificed in MC-4 were the twenty basics. A first fix also ranked mana moves last in `Narrow` — not caught by its
  revert-check once the rollout was fixed, so taken out.

Open questions:
- Phase 5's energy and poison: the table's `PlayerPlate` shows neither — that is the TBL lane's; only the evaluator here.

## Tasks

Phase 1:
- [x] MC-9.1 (done 2026-11-23) Fire Covenant's shares (`Division.AskShares`, a `ChooseNumberChoice` per target)
      answered by rule, never branched, as delve's picks are: each target the damage that kills it, the last the rest
      (`ChooseNumberChoice.ShareOf`, `Lookahead.KillingShare`). Watched first: the COST was not the problem — the play
      was (Decisions, "MC-9.1 watched"); so the target set got kill sets too (`GreedyAi.KillSets`). A TRIGGER dividing
      X (none yet) would need the same.
- [x] MC-9.2 (done 2026-11-23) Vivi Ornitier's mana move at 0 power (it makes nothing and uses the turn's activation)
      left out in `SearchMoves`: a mana move whose ability would add no mana (`Evaluator.MakesNoMana`). Watched first
      (Decisions, "MC-9.2 watched").
- [ ] MC-9.3 Wizard's Rockets: the turn timed on a 6–8 land board. Still slow: bound X in `SearchMoves` as Candelabra's
      is — up to the coloured pips the hand can't pay otherwise (DesignNotes, "{X} on an ACTIVATED ability").
- [ ] MC-9.4 Oko, Thief of Crowns' −5: what its two target choices cost the search, measured; left alone if small.

Phase 2:
- [x] MC-9.5 (done 2026-11-23) Pentad Prism and Loot, the Pathfinder's exhaust mana: a line making more mana moves
      must beat the close lines making fewer (`BeamSearchAi.ManaMoves`, `Best` and `Prune`); `LandMatters` counts the
      mana floating; the rollout's mana move must score better (`Rollout`, strict). Watched first (Decisions, "MC-9.5
      watched"). A tie-break, no evaluator term: the suite, not a `--match`.
- [ ] MC-9.6 Zuran Orb and Squandered Resources (Shayne, "wait for as long as possible to use its ability"): on six
      lands at 20 life the Orb already holds (MC-9.5's watch). Watch it (1) at low life, where 2 life may outscore a
      land, and (2) facing lethal — the search must still sacrifice in response (at declare blockers, or with burn on
      the stack), or holding it is worse than wasting it. If (1) sacrifices early: what the Orb can still gain counted
      as life held, so a sacrifice gains only what it triggers (Titania, Baloth Prime) — an evaluator term, `--match`.

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

Next is MC-9.6 (Zuran Orb at low life and facing lethal — MC-9.5's principle, Shayne 2026-11-23), then MC-9.3 (Wizard's
Rockets, timed on a realistic board). The tests are `MagicCore.Tests/Ai/CubeAiTests.cs`
(`SixLands()`: two each of Swamp, Island and Mountain; `SearchPlaysTheTurn()`: the search decides until the turn
changes, returning each decision's description). Read MC-4's Decisions from "4.4l1 + l2" (the free-ability note, Shayne 2026-11-21) and the Pentad Prism
diagnosis (2026-11-22), and magic-ai.md. To watch a position: build it in a test as `LsvBatch2l3Tests` does
(`EmptyBoard()`, `Play(land)` in `FraBoardTest` — but use a realistic land count) and print `BeamSearchAi.Ranked` /
`Evaluator.Explain`. After each fix: `python -u tools/test_changed.py --run`, the suite (`--suite=beam`), and the
golden replays in the normal tests.

## Log

- 2026-11-23: opened (Shayne) after the merge of mtg-real (DEV-6, `2f2028bb`): MC-4's lists for the AI lane, plus the
  four DesignNotes items, as one plan in five phases.
- 2026-11-23: MC-9.1 done — Fire Covenant's shares and target set by rule; watched, it had been killing one creature
  of twelve; now four. Two tests in `CubeAiTests`, both revert-checked.
- 2026-11-23: MC-9.2 done — Vivi's mana move at power 0 left out of the search's moves; two tests, revert-checked.
- 2026-11-23: Phase 2 rewritten on Shayne's word (free abilities have no opportunity cost); MC-9.5 done — a mana move
  must beat holding, in the search, its beam and its rollout; `LandMatters` counts floating mana. Five tests, the
  three pieces revert-checked.
