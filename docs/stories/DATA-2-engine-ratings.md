---
id: DATA-2
title: Rate a set's cards without 17lands — measured by the engine, checked against FRA's real rates
status: in-progress
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

- 2026-11-10, Shayne: **TRK's opponents first, before the AI stories** — his aggressive sealed decks beat the AI's,
  which play slow, suspect cards; "I want to start playtesting against better decks at the very least now". **Two
  rounds**: round 1 rates on decks built from the interim table; round 2 rebuilds the decks on round 1's ratings and
  rates again (as MtgCore's presim, then the run). **The search on both sides** ("we should do the search ai … for
  sure"), **2,000 games a round** (~4 h each at ~8.5 games a minute). Which cards play well together: not now.
- 2026-11-10, Claude (for review): the measure of success is NOT the rank against 17lands — the rate includes how well
  this AI plays a card, which for an opponent's deck is what we want. It is whether decks built on the new ratings
  beat decks built on the interim table, the search on both sides (DATA-2.6).

## Tasks

- [ ] DATA-2.1 Interview the open questions — answered by Claude's defaults (above) for MC-5.4; Shayne to review
- [x] DATA-2.2 `SimulatedRatings` + `MagicConsole --sim-ratings` (2026-11-07, MC-5.4.6) — checked on FRA: GreedyAi's
      self-play ranks FRA at ρ 0.20 against the interim table's 0.56 (`docs/findings/limited.md`); its first run
      found an unanswerable "Enter as a copy?" with nothing to copy (fixed in `EnterBattlefield`)

- [x] DATA-2.3 The "in hand" read fixed (2026-11-10): a card counts when it was in its owner's hand at a decision
      from turn 1 on (`SimulatedRatings.InHandDuring`, fed by `AiMatch.Play`'s `seen`) — a milled or surveilled card
      isn't drawn, a mulliganed hand isn't kept. Test `InHand_IsWhatAPlayerHeldFromTurnOne_NotAMulliganNorAMilledCard`.
      `--card-games` keeps the old read (a replay finder). The run prints `RunProgress` (cards at 200+ games so far).
      `--built-on` (round 2) rates a card never in hand at the mean — the builder reads every pool card and threw; test
      `ACardNeverInHand_IsRatedAtTheMean_SoARound2CanBuildOnIt` also checks round 2's decks differ (the first version
      silently built round 2 on the interim table — caught by that check, revert-checked)
- [ ] DATA-2.4 Round 1: TRK, the search, 2,000 games in four chunks of 500 (`--first-seed=1,501,1001,1501`), the decks
      on the interim table and LIM-1.11's opponent; then round 2 the same, `--built-on=` the four chunk files
- [ ] DATA-2.5 Read both rounds: the cards they move most, the cheap-vs-expensive lean the interim table has, and how
      many cards reach 200 games
- [ ] DATA-2.6 Does it make better opponents: decks built on round 2's ratings against decks built on the interim
      table, same pools, the search on both sides (a harness to write — `SealedBuildMatch` plays builds against
      opponents, not two ratings tables). `--write` only if they win

## Next

**Now (2026-11-10): DATA-2.4, the two TRK rounds** — commands in its task; each chunk ~1 h, run detached or in chunks
(a background task stops at 2 h and a cut-off run writes nothing). AI-9.3, which this was blocked on, is done.

**Was (2026-11-07): blocked on AI-9.3 (Shayne: "our win rate data will never be good until we fix these issues").** The
self-play ratings rank FRA worse than the interim table (greedy ρ 0.20 against 0.56 over 10 000 games; the search 0.11
against 0.42 over 500 — `docs/findings/limited.md`), and the walker games show why for the walkers: priced in hand by
their cast turn alone, discarded over commons. **Before the next run, fix the proxy:** "in hand" is "a copy left the
library", so a MILLED or surveilled card counts as drawn (2 of 32 walker games) — count `DrewCardEvent` and the opening
hand instead. Then rerun on FRA (greedy, 10 000 games, ~17 min) and compare with the interim table's 0.56.

## Log

- 2026-10-20: created from LIM-1.4.
- 2026-11-07: DATA-2.2 built for MC-5.4 (TRK's bots); greedy self-play checked on FRA, worse than the interim table.
