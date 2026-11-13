---
id: AI-23
title: Stationing priced — what the counters buy against the attack or block given up
status: backlog
area: AI
created: 2026-11-12
---

## Why

Borg Cube (AI-19.22) showed the AI pricing a Spacecraft badly: lessons (b) the rollout stations with a creature that
could attack, and (d) a threshold ability (12+: flying) unpriced until reached. Shayne (2026-11-12): "The Borg lesson is
really a SpaceCraft lesson … when you have spacecraft you are usually sacrificing your damage (by not attacking with
creatures that could attack) or sacrificing some of your life (by stationing with creatures that might have been able to
block instead) for charge counters that may not provide immediate impact … how do we price stationing so that it isn't
just automatically either stationing or not stationing?"

Today (`Evaluator.Other`): a Spacecraft below its threshold is 2 + (counters ÷ threshold) × (its creature value − 2) —
every counter worth the same, the threshold's abilities unread; the creature tapped for it priced only by what the
search's lines and the rollout happen to see.

## Requirements

- [ ] Tests first, each a position with its control, confirmed by Shayne before building: station with a summoning-sick
      creature; station with a creature whose attack does nothing; don't station with an attacker that deals real
      damage to a low opponent; don't station with the only good blocker facing a big attack; station when it reaches
      the threshold and the body turns the board; never past the threshold (`StationCap`, exists).
- [ ] The benefit: a counter worth how much sooner it brings the threshold (the creature and its threshold abilities),
      discounted by the turns still needed with the creatures that can be spared.
- [ ] The cost: the attack and the block given up, compared in the same score.
- [ ] Its own switch (`beam+…`), measured the AI-6 way; findings in `docs/findings/magic-ai.md`.

## Decisions

- 2026-11-12, Shayne: Borg Cube #343's Pass (attack, then D'Ghor's −1/−1 sweeps) is "a reasonable play to try to make"
  — not this story's position. The lesson is general: Spacecraft, not the Cube.

Open questions:
- The positions (Claude drafts them with each card's text and the board's numbers; Shayne confirms).
- Lessons (a) (the rollout's one spell a turn) and (c) (a card exiled "with" a permanent priced as your future gain) —
  here, or their own tasks in AI-19?

## Tasks

- [ ] AI-23.1 The positions, from TRK self-play records (the scan's S3c "station with an attacker", 20 in the rerun)
- [ ] AI-23.2 The price, behind a switch; the positions pass with their controls
- [ ] AI-23.3 Measured: suite, match, the scan's S3c count

## Next

AI-23.1 — after AI-20 in sprint 5's order. Read AI-19.22's lessons (AI-19's Tasks) and `Evaluator.Other`.

## Log

- 2026-11-12: created from Shayne's answer on the Borg Cube lessons.
