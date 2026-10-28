---
id: AI-15
title: The look-ahead's defender gang-blocks — a menace attacker isn't unblockable
status: backlog
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

- [ ] The look-ahead's defender can block one attacker with two or more creatures when that's its best
      answer — card-agnostic (menace, a big attacker two blockers kill)
- [ ] The turn-15 position as a test: the attack (and the sacrifice for menace) no longer scores as a
      win; its control without the fix does
- [ ] Measured the AI-6 way: suite, then `--until-clear`

## Decisions

Open questions:
- How to search gang blocks without the cost exploding: only for an attacker no single blocker can
  stop (menace, or bigger than every blocker), pairs only, the defender's own best?

## Tasks

- [ ] AI-15.1 Reproduce: the turn-15 position from `MagicConsole --decisions` (game 1, seed 1), the
      attack's score with the look-ahead's blocks traced — confirm the cause before fixing
- [ ] AI-15.2 Gang blocks in the look-ahead's defender, behind a switch; the position test; measured

## Next

Start with AI-15.1 — the cause is inferred (the 9017 score, the per-creature block rule), not yet
reproduced. Read `Rollout.Answer`/`BestAlone`/`AllInOrAlone` (the attack side's all-in already exists,
measured WORSE in AI-3.8 — `docs/findings/magic-ai.md`), `Lookahead.QuickAnswer`.

## Log

- 2026-10-28: created from Shayne's fourth flag in AI-14's replay.
