---
id: AI-9
title: Resources priced by what they provide, not by how many there are
status: backlog
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

## Next

Not this sprint (`docs/stories/README.md`). Read the DesignNotes entry above, `Evaluator.cs`
(`Walker`, `Pressure`, held values) and AI-8's story. A related candidate, not yet a story: the
rollout plays ONE spell a turn (`Rollout.Settings.ActionsPerTurn`, never measured alone — AI-8.2
found it hiding Way of the Wildspeaker); one switch and one match.

## Log

- 2026-10-23: created at sprint planning from the DesignNotes entry and AI-8's leftovers.
