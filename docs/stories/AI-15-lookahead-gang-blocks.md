---
id: AI-15
title: The look-ahead's defender gang-blocks — a menace attacker isn't unblockable
status: done
area: AI
created: 2026-10-28
---

## Why

AI-14's replay, game 1 seed 1, turn 15 (Shayne): White-Black sacrificed Winter, Tormented Loner to
give Edgar menace and attacked into five untapped creatures; Red-Green double-blocked with two Cadets
and Edgar died — "probably one of the worse lines possible". The search had scored the attack 9017, a
WIN: its look-ahead answers each blocker alone (`Rollout.Answer` → `BestAlone`), so it never forms a
double block, and a menace attacker can't be blocked by one creature. Any attack a real player meets
with a gang block looks safe to it.

## Requirements

- [x] The look-ahead's defender can block one attacker with two or more creatures when that's its best
      answer — card-agnostic (menace, a big attacker two blockers kill)
- [x] The turn-15 position as a test: the attack (and the sacrifice for menace) no longer scores as a
      win; its control without the fix does
- [x] Measured the AI-6 way: suite, then `--until-clear`

## Decisions

- 2026-10-28, Shayne: the position's answer — **don't attack** (the double block trades our 3/3 for
  a 2/2). The approach — **join a block** (the proposal below), over a gang on each attacker or all
  pairs.

Open questions (answered):
- How to search gang blocks without the cost exploding: only for an attacker no single blocker can
  stop (menace, or bigger than every blocker), pairs only, the defender's own best? **Proposed
  (Claude, 2026-10-28):** `QuickAnswer`'s blocker JOINS a block already declared on an attacker when,
  with it, the blockers kill that attacker (or menace needs a second) — no extra settles. The rollout's
  defender already tries "block X" for each blocker (`BestAlone`); with the join, that line becomes the
  gang block. The declarations so far are in the pending pipeline's context (as an attack limit reads
  them, `.claude/rules/magic-mechanics.md`).

## Tasks

- [x] AI-15.1 Reproduce: the turn-15 position from `MagicConsole --decisions` (game 1, seed 1), the
      attack's score with the look-ahead's blocks traced — confirm the cause before fixing.
      **Confirmed, on a hand-built position** (2026-10-28): the game no longer reaches turn 15 — the
      report was written before AI-13.4's review fixes, and game 1 diverges from move one. Our 3/3
      menace against their two untapped 2/2s, them at 3 life: the search scores the attack **10000, a
      win** (not attacking 9022) and attacks; their search double-blocks (block −10.09, no block −10000)
      and the 3/3 dies for one 2/2. The cause, read in the code: the rollout's defender judges each
      blocker alone (`Rollout.BestAlone`), settling the rest with `Lookahead.QuickAnswer`, which blocks
      only what it survives — so no second blocker ever joins, and `FinishBlockersAction` drops a lone
      block on menace. The top-level search gang-blocks (each declaration is its own node); only the
      look-ahead can't. At 20 life the same attack isn't made (−5.40 against −3.90: the swing back), so
      the bug shows where a lethal-looking attack meets a gang block
- [x] AI-15.2 Gang blocks in the look-ahead's defender, behind a switch; the position test; measured.
      **Built (2026-10-28):** `Lookahead.Joins` — a blocker that survives nothing joins a block already
      declared when, with it, the blockers kill the attacker, or menace needs a second;
      `Evaluator.Terms.GangBlocks` (`+nogang` is the old look-ahead). Fires: the position's attack
      10000 → 8.69 (holding 10.80), so it holds. Tests: `AMenaceAttack_IntoTwoBlockersThatKillIt_IsntMade`
      (its control, `+nogang`, attacks), `ALookAheadBlocker_JoinsABlock_TheTwoMake` (six cases). Suite:
      6 of 288 decisions change, cost +1.9% (3 097 steps against 3 040); oracle agreement 81.3% against
      82.6% — that oracle reads with the old look-ahead, so it can't rank this; re-read, 82.6% (accepted).
      Match: **51.2% ± 1.2pp over 576 games, no difference, worse rejected**, +2.1% cost — kept.
      GreedyAi's golden games regenerated (4 of 12 moved; gang blocks alone — checked with the join
      forced off). The review: a menace join no longer chumps (it joins when the two kill the attacker
      or it survives it) and `Joins` runs only when no safe block exists — 0 of 288 suite decisions and
      the golden games unchanged, so the match stands. `docs/findings/magic-ai.md`

## Next

Done. Left open, should they bite (`Lookahead.Joins`' ponytail): first strike, the attacker's damage
split and its deathtouch aren't counted in a gang block. The look-ahead still never STARTS a gang
block outside the rollout's defender (plain `Settle` blocks only what it survives). AI-13's
`--cast-gains` table was measured with the old look-ahead — rerun it (AI-13's Next). Read `Rollout.Answer`/`BestAlone`/`AllInOrAlone` (the attack side's all-in already exists,
measured WORSE in AI-3.8 — `docs/findings/magic-ai.md`), `Lookahead.QuickAnswer`.

## Log

- 2026-10-28: created from Shayne's fourth flag in AI-14's replay.
- 2026-10-28: AI-15.1 — reproduced by hand (the game itself had moved): a menace attack the look-ahead
  scores as a win, met by a double block. Status → in-progress (sprint 3, item 1).
- 2026-10-28: AI-15.2 built, measured (51.2%, no difference) and kept — Shayne's position fixed. Done.
