---
id: AI-3
title: Swappable simulation models in the search, measured for strength against time
status: backlog
area: AI
created: 2026-10-16
---

## Why

What the search imagines the opponent doing decides what it sees. MtgCore's default, `BoardOnly`,
never casts from hand — so a bigger threat never arrives inside a rollout, and holding removal for
one never pays (AI-1's Rank Rat). A richer model sees more and costs more, and the one that sees
the opponent's real hand cheats. Which is best is a measurement, not a guess.

## Requirements

- [ ] Three opponent models behind AI-2's seam, chosen by a constructor parameter:
  - `BoardOnly` — attacks and blocks with its board, never casts (ships in AI-2)
  - casts from hand — plays threats in its own turn
  - full responses — also responds at instant speed (counters, tricks) inside our turn
- [ ] Each measured: head-to-head against the others and against `GreedyAi`, AND time per decision —
      recorded side by side in `docs/findings/magic-ai.md`
- [ ] Our own side's rollout policy is swappable through the same seam (cheap greedy vs `GreedyAi`)
- [ ] Hidden information has a second mode beside AI-2's "real order": **shuffle only what isn't
      known**. Needs the engine to record which library positions a player knows — surveil and scry
      keeps, "put on the top / bottom", mulligan bottoms — cleared when the library is shuffled
- [ ] Every combination worth running is measured the same way: strength AND time per decision

## Decisions

- 2026-10-16, Shayne: don't commit to one model — be able to try all three and measure play
  ability against simulation time.
- 2026-10-16, Shayne: hidden information is the same kind of question — two methods, swappable and
  measured. A pure library shuffle is wrong, because some positions are known.

Open questions:
- "Casts from hand": its REAL hand (sees hidden information) or a guessed one (sampled from what
  its deck could hold)? The real hand is cheap and cheats; a guess is honest and needs a deck model.

## Tasks

- [ ] AI-3.1 After AI-2: settle the open question, then build the second model

## Next

Starts once AI-2's seam exists. MtgSimulator's `OpponentSimulationMode` (`PassTurn`, `Greedy`,
`Random`, `BoardOnly`) is the precedent; `.claude/rules/sim-ai.md` records how `PassTurn` misprices
every defensive card.

## Log

- 2026-10-16: split out of AI-2 so the port stays bounded.
- 2026-10-16: widened from opponent models to every simulation model — our rollout policy and hidden information too.
