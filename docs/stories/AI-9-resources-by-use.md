---
id: AI-9
title: Resources priced by what they provide, not by how many there are
status: in-progress
area: AI
created: 2026-10-23
---

## Why

Shayne's principle (2026-10-22, `DesignNotes.md`: "MagicCore's evaluator values resources by count,
not by what they provide"): three lands put into a hand with ten in play are worth almost nothing,
yet score as three cards; loyalty only matters for what it buys when spent. AI-8 fixed the
instances its reports met (trample in the threat term, the opponent's walkers used in the look-ahead,
a walker priced by its best ability). This story is the rest.

## Requirements

- [ ] Each resource below priced by what it provides, each its own switch, measured as AI-6
      measures (the suite, then `--until-clear`) and kept by AI-5's rule
- [ ] Findings in `docs/findings/magic-ai.md`

## Decisions

- 2026-10-23, Shayne: a position built **by hand** may serve as the bad play AI-5's keep rule asks
  for (as the Rank Rat did) — but Shayne agrees the AI's current play there is wrong BEFORE the
  change, so a position can't be built to flatter it.
- 2026-10-27, Shayne (from AI-10.3): **a known card in the opponent's hand must not be worth the same
  as an unknown one** — a flaw in this evaluator AND MtgCore's (`MtgSimulator/WeightedStateEvaluator`
  counts each side's non-land hand cards at a flat 1.4, known or not). Try it first: AI-9.5.

Open questions:
- Which first — lands has a precedent to copy (below); life moves every attack decision.
- Is it wrong today in a way that shows? Search the 288 suite positions for decisions that turn on
  the price (discards, surveil, mulligans) before building.

## Tasks

- [ ] AI-9.1 Lands in hand by what they unlock — every land in our hand is a flat 0.3 today.
      MtgSimulator's precedent: "a land is worth 0 on its own; its value is entirely the difference
      it makes to everything else" (`.claude/rules/sim-ai.md`)
- [ ] AI-9.2 Life by what it buys — 1.0 a point up to 20, so 2 damage at 20 life outweighs a card
      (AI-8.1's second lever); the threat term already has the clock
- [ ] AI-9.3 Walker effects still unpriced (damage, mana, life, pumps, exile), and a 3-loyalty Jace
      cashed for a card still scores as a loss (AI-8.2 kept the price monotonic first); our own
      walkers' use
- [ ] AI-9.4 The opponent's unseen hand at a flat 2.71 a card — by what it could cast with the mana
      and colours shown
- [x] AI-9.5 A KNOWN card in the opponent's hand priced as what it is (Shayne, 2026-10-27): today
      `Evaluator.Side` prices every card in their hand blind (`HeldValues.Unknown`), so a creature
      just bounced is "a card", and Unsummon reads as a kill spell (AI-10.3, #547). `Card.KnownTo`
      already says who knows a hidden card (a card returned from play is known to everyone) — price
      one the searcher knows at its value (`HeldValues.Of`), the rest blind as now. Its own switch
      (an `Evaluator.Terms`); the case: `GalliaWontAttack` before #547, where Shayne held Unsummon
      and the board-only search casts it — with the switch it should pass, without it (the control)
      cast. Then the suite and `--until-clear`. MtgCore has the same flaw; port only if asked.
      **Built, not kept yet (2026-10-27):** `Evaluator.Terms.KnownHand` (`+blindhand` is off). It
      fires (after the bounce, Fateshaper Aspirant is known to Everyone; priced 6.50 held against 2.70
      unknown), but #547 doesn't flip: Unsummon on it −24.1 → −27.9, Pass −38.5. With an honest
      casting opponent (`casts+unseen`) the term changes nothing (the creature is recast inside the
      look-ahead) and the search still bounces Gallia (−40.0 against −48.4). So #547 is not this
      term's blunder: Shayne (2026-10-27) held for patience, not price — it is AI-4.2's holding
      position and AI-13's lesson 4. By AI-5's keep rule the term needs a match that measures better.
      Suite: 2 decisions change (one bounce now takes a Cadet TOKEN over Heartstring Puller — a bounced
      token is gone, Shayne's value case), oracle agreement 83.3 → 83.0% against an oracle without the
      term. The match (`--match=beam,beam+blindhand --until-clear`): beam 49.0% ± 1.0pp over 96 games
      (47–49), no 5pp difference either way. **Kept (Shayne, 2026-10-27), on the token bounce** at
      seed 4, move 53 (`AiPositionTests.ABounce_TakesTheToken_…`, its control without the term). Suite
      accepted, the oracle re-read: beam 82.6% agreement (given up 0.87) against beam+blindhand 83.0%
      (0.78) — slightly worse on the positions, recorded in the findings

## Next

AI-9.5 done (2026-10-27): a known card in their hand is priced at its held value; kept on the token
bounce, slightly worse on the suite's oracle, no difference in the match (findings). Next here:
AI-10.4 (an honest casting opponent) is AI-10's. The rest is not this sprint (`docs/stories/README.md`). Read the DesignNotes entry above, `Evaluator.cs`
(`Walker`, `Pressure`, held values) and AI-8's story. A related candidate, not yet a story: the
rollout plays ONE spell a turn (`Rollout.Settings.ActionsPerTurn`, never measured alone — AI-8.2
found it hiding Way of the Wildspeaker); one switch and one match.

## Log

- 2026-10-23: created at sprint planning from the DesignNotes entry and AI-8's leftovers.
- 2026-10-27: AI-9.5 added (Shayne: a known card in their hand is not an unknown one).
- 2026-10-27: AI-9.5 built behind a switch; it fires but doesn't flip #547 (numbers in the task).
- 2026-10-27: AI-9.5 kept (Shayne) on the token bounce; match no difference (findings).
