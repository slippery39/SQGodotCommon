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
- 2026-11-10, Shayne (11:00, after two restarts): **every round halved** — 1,000 games a round, the match 300 — and
  **long games stopped sooner, their boards noted**: `GameBudget` 5 min (~3x a typical game; Claude's number), each
  abandoned game logged with `SimulatedRatings.Board` (turn, life, each side's permanents, tokens marked).
- 2026-11-10, Shayne: **the limited loop, as a person learns a format.** A stand-in rating first (AI-9.8's
  cost-adjusted value — "evaluating cards for a prerelease without actually playing the games yet"), then games
  (self-play), then the two compared; where a card we rated good doesn't win, find WHY before adjusting: "is the format
  too fast? too slow? is a color in general just bad which brings win rates of other cards in that color down? Is a
  color too good which brings win rates of other cards in that color up?" → DATA-2.7.
- 2026-11-10, Shayne: **run DATA-2.5 and 2.6 automatically overnight.** Claude's rule for the swap (for review): round
  2's ratings become TRK's table only if their decks score above 50% by at least 2 standard errors in the 600-game
  match; otherwise the interim table stays and the result is written up.
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
- [x] DATA-2.4 Round 1 and 2 done 2026-11-10 (halved by Shayne: 1,000 games each; two restarts — stuck games, an
      engine crash — and one runtime fault rerun; findings, limited.md). Was: Round 1: TRK, the search, 2,000 games in four chunks of 500 (`--first-seed=1,501,1001,1501`), the decks
      on the interim table and LIM-1.11's opponent; then round 2 the same, `--built-on=` the four chunk files
- [x] DATA-2.5 Read (2026-11-10, findings): the interim table rises with mana value (ρ +0.64), self-play doesn't
      (−0.13); cheap commons top round 1. Was: Read both rounds: the cards they move most, the cheap-vs-expensive lean the interim table has, and how
      many cards reach 200 games
- [x] DATA-2.6 **No (2026-11-10): 49.6% ± 2.9pp over 141 pairs** — TRK keeps the interim table (findings). Was: Does it make better opponents: decks built on round 2's ratings against decks built on the interim
      table, the search on both sides. HARNESS BUILT (2026-11-10): `RatingsMatch` / `--ratings-match=trk
      --built-on=FILES [--games=600] [--write]` — pools in pairs, each built on both tables (X new v Y old, X old v Y
      new: each pool and seat plays both), pairs whose builds don't change skipped; tests `RatingsMatchTests`.
      `--write` writes the summed ratings as `TRK.ratings.json` only when `NewWon` (implemented nonlands; lands read
      the median in draft).
      To run: 600 games, ~1 h, started by the overnight script once round 2 is done

- [ ] DATA-2.7 The format report — why a card's played rate differs from its stand-in (Shayne, Decisions): per colour
      and colour pair, the decks' win rate (a strong colour lifts its cards, a weak one sinks them — a card's rate read
      against its colour's); the format's speed (turn a game ends, by deck speed — is it fast or slow?); and the
      biggest mismatches, stand-in against played, each with its likely reason (colour, cost, speed, the AI's skill
      with it — AI-14's replays or `--card-games` for the ones that don't explain). Needs `Measure` to tally the deck
      colours and the turn each game ends (not yet recorded)

## Next

**Now (2026-11-10, 14:30): 2.4–2.6 done — self-play ratings don't build better decks yet (49.6% ± 2.9).** Next is
Shayne's call among: AI-9.8's stand-in (cost-adjusted) table, matched the same way (needs `--ratings-match` to take a
ratings file, not only tallies); a curve in the builder; more games. DATA-2.7 (the format report) explains mismatches.

**To run the rounds again** (Shayne, 2026-11-10: more games, "possibly overnight", for "enough of a feel on win rates"):
chunks of 500 (a chunk writes nothing until it ends, and a background task stops at 2 h — run the chain detached, e.g.
`Start-Process pwsh -File <script>`, and skip a chunk whose file exists so a restart loses only the chunk running):
round 1 `--sim-ratings=trk --ai=beam --games=500 --first-seed=1` (then 501, 1001, …) → `sim_results/sim_ratings_trk_beam[_N].json`;
round 2 the same with `"--built-on=a.json;b.json"` (QUOTED — an unquoted `;` ends the command in PowerShell and bash, and only the first file arrives) → `..._round2.json`; then `--ratings-match=trk
--built-on=<round 2's files> --games=300 --write` (writes `TRK.ratings.json` only if its decks win by 2 SE). A game
past `GameBudget` (5 min) is abandoned with its board logged; one that throws is logged and dropped. At ~10 games a
minute: 2,000 games a round ≈ 3.5 h. The 2026-11-10 run (1,000 a round) and its two stalls are in the Log.

**Was (2026-11-07): blocked on AI-9.3 (Shayne: "our win rate data will never be good until we fix these issues").** The
self-play ratings rank FRA worse than the interim table (greedy ρ 0.20 against 0.56 over 10 000 games; the search 0.11
against 0.42 over 500 — `docs/findings/limited.md`), and the walker games show why for the walkers: priced in hand by
their cast turn alone, discarded over commons. **Before the next run, fix the proxy:** "in hand" is "a copy left the
library", so a MILLED or surveilled card counts as drawn (2 of 32 walker games) — count `DrewCardEvent` and the opening
hand instead. Then rerun on FRA (greedy, 10 000 games, ~17 min) and compare with the interim table's 0.56.

## Log

- 2026-11-10: the first overnight start (05:42) stalled at 467/500 of its first chunk: three search games on boards of
  many tokens ran 90+ minutes each (sampled: `WholeAttacks.Best` in the rollout, settling two attacks per creature),
  and Parallel.For's range partitioning held the 33 games queued behind them. Fixed in the harness, not the AI: a
  game past `SimulatedRatings.GameBudget` (20 min; a typical game ~1.6 min) is abandoned between decisions, tallies
  nothing and is logged by seed (`RatingsMatch` drops its pair). The dropped games lean to token decks — watch the
  "abandoned" count. Restarted 08:15.
- 2026-11-10: round 1's chunk 1 then CRASHED at game 392 (chunk 501 finished: 500 games, 3 abandoned, 83 min). The
  cause, replayed: Malfunctioning Holodeck had exiled De-evolve; Eject the Warp Core kicked by sacrificing the Holodeck
  brought De-evolve back mid-cast, the state check binned it before its host question came up, and the question had
  nothing to offer — `Lookahead.Chosen`'s `MaxBy` over no picks returned null (the table would hang on it). Fixed in
  the engine: the question is judged when asked (`Attachments.AskHostAction`); test
  `AnAuraReturnedByACost_GoneBeforeItsQuestion_IsAskedNothing` (failed before). The Aura still goes to the graveyard
  instead of attaching — a known ceiling (DesignNotes). And the harnesses no longer die on one game: with a `failed`
  callback (the console's) a throwing game is logged with its seed and stack and dropped. Restarted 10:49.

- 2026-10-20: created from LIM-1.4.
- 2026-11-07: DATA-2.2 built for MC-5.4 (TRK's bots); greedy self-play checked on FRA, worse than the interim table.
