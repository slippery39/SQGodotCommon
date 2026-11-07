---
id: DATA-2
title: Rate a set's cards without 17lands — measured by the engine, checked against FRA's real rates
status: backlog
area: DATA
created: 2026-10-20
---

## Why

Shayne (2026-10-20): the sealed builder needs card ratings, and 17lands has none for a custom set or
one revealed but not yet playable online — "so we cannot fully rely on it… we also need to be able
to pre-measure cards". The engine's measured `CardValues` can't stand in: they count only the turn a
card is cast, so every FRA planeswalker reads last (2.7–3.75) and value climbs with cost (median
6-drop 16.5, 1-drop 4.5); FRA's median mythic measures BELOW its median common.

## Requirements

- [ ] A rating for every card of a set from the engine alone, on `CardRatings`' scale, so the
      sealed builder takes it unchanged
- [ ] Checked on FRA against 17lands' GIH WR (`Sets/FRA.17lands.json`): how well it ranks the cards,
      and which it gets most wrong

## Decisions

- 2026-10-20, Shayne: needed; FRA uses 17lands meanwhile (LIM-1).
- 2026-10-31, Shayne: needed NOW for TRK's draft bots (MC-5): "pre-simulating win rates of cards against
  bots" — a simulated win rate, not a `CardValues` fix; "not fully accurate to the true win rate … ok
  for now".

Open questions:
- The simulated win rate's exact measure (games in hand, as 17lands, or another) and the games it needs
- How many games that takes, and how long (ask before any run over ~15 minutes)

- 2026-11-07, Claude (MC-5.4.6, under Shayne's MC-5.4 permission: the rating run may run unattended — **Claude's
  defaults, for review**): the measure is **games in hand, as 17lands** — a card counts for a game if a copy left its
  library (read from the last state); the decks are AI sealed opponents' builds; fewer than 200 games pulled to the
  mean, as `CardRatings`. Checked on FRA against 17lands BEFORE any TRK run (MC-5.4's rule).

## Tasks

- [ ] DATA-2.1 Interview the open questions — answered by Claude's defaults (above) for MC-5.4; Shayne to review
- [x] DATA-2.2 `SimulatedRatings` + `MagicConsole --sim-ratings` (2026-11-07, MC-5.4.6) — checked on FRA: GreedyAi's
      self-play ranks FRA at ρ 0.20 against the interim table's 0.56 (`docs/findings/limited.md`); its first run
      found an unanswerable "Enter as a copy?" with nothing to copy (fixed in `EnterBattlefield`)

## Next

Read `MagicCore/Limited/CardRatings.cs` (the scale and the table shape), `MagicCore/Ai/CardValues.cs`
(what is measured today and its known limits), `Limited/SealedBuilder.cs` and `Boosters.cs` (pools
and decks for self-play).

## Log

- 2026-10-20: created from LIM-1.4.
- 2026-11-07: DATA-2.2 built for MC-5.4 (TRK's bots); greedy self-play checked on FRA, worse than the interim table.
