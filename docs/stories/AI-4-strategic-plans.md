---
id: AI-4
title: Strategic plans — the AI picks a plan above single actions
status: backlog
area: AI
created: 2026-10-16
---

## Why

Shayne, 2026-10-16: searching afresh at every decision balloons time, and individual actions may be
the wrong level to choose at. The idea: the AI chooses a **plan** — "race them", "hold removal for
the flier", "set up a big turn" — made of actions, and leaves it only when the game changes the
plan's outcome a lot.

AI-2 ports the action-level half of this (MtgCore's committed chain: the beam's best line for the
turn is replayed while the position stays within a score threshold of what it predicted). This
story is the level above: a plan that spans turns and steers what the search looks for.

## Requirements

To be shaped — see the open questions.

## Decisions

- 2026-10-16, Shayne: worth its own story; AI-2's committed chain comes first.
- 2026-10-24, Shayne (from TBL-5.6's report on his sealed game, where the oracle cast Unsummon and
  Prophesied End at the first chance and he held them): **casting bounce or removal at the earliest
  opportunity "can't be correct at all. Usually you wait until those cards provide actual good
  value, or you use it in desperation."** When bounce (Unsummon) earns its card:
  - value — your own bomb returned to save it from a removal spell; their big expensive creature
    bounced to get in for a lot of damage and make them pay for it again (tempo); a creature in a
    double block bounced (a pseudo kill spell); an opponent's creature with an Aura or +1/+1
    counters (they're removed); a relevant token (it can't go to a hand, so it's gone); a creature
    attacking your planeswalker, when the planeswalker matters
  - desperation — a creature that would kill you, even if you die next turn anyway (a "fog")
- 2026-10-27, Shayne — **the first agreed holding position: #547 of his 10-23 game** (kept record
  `GalliaWontAttack`; he is at 5 life against 16, in the AI's attack: Tinybones 3/2 attacking,
  Fateshaper Aspirant 3/4 tapped by his stun, Gallia 2/1 menace home; Proctor 3/1 and Unsummon for
  him). **The right answer is Pass** (then block Tinybones with Proctor). In his words:
  "casting it on tinybones makes no sense because I can trade with it. Casting it on the fateshaper
  aspirant means that when they recast it they would just be able to get back a creature (maybe the
  tinybones) from their graveyard, and as well, I stunned it the previous turn anyways so it doesn't
  make sense, I can at the very latest wait a turn if I really need to. Casting it on galia makes no
  sense only because they did not attack with it, if they had attacked with it, I still might not
  have used it at that time. I'm at 5 life, I can still take a hit. And maybe they make a bad attack
  that allows me to unsummon something and trade with another one of thier creatures when they don't
  expect it. … there is literally no reason to play it right now."
  The search casts Unsummon there with every model tried (board-only: on Fateshaper; an honest
  casting opponent: on Gallia — AI-9.5's task has the scores).
- 2026-10-24, Shayne: **hard removal** is always a 1-for-1, so those cases don't apply; it needs a
  threat level that also weighs tempo (mana used efficiently so you aren't run over early) and
  aggression, balanced against holding it for a better target. The threat over time is AI-12.

Open questions:
- What is a plan, concretely: a goal that changes the evaluator's weights, a set of moves the search
  prefers, a target state to reach — or something else?
- Which plans: a fixed handful (race, control, combo setup), or found by the search?
- When does the AI re-plan: on score divergence like the chain, on new information (a card drawn, a
  threat landing), or every turn?
- How to measure it: AI-2.3's harness, plus positions where the plan is obviously right?
- A plan also prices held spells (AI-5): whether to Bolt their 2-power creature now depends on
  whether we mean to tap out for two turns or to block it with next turn's creature (Shayne,
  2026-10-16) — so plans and held-spell value may be one design, not two.
- Shayne's bounce and removal cases (Decisions, 2026-10-24) are the held-spell question's first
  concrete examples. Part of why the search casts Unsummon at once may be the look-ahead, not the
  price: its opponent never recasts what's bounced (AI-10.3). Fix what the search SEES first, then
  price the hold — card-agnostic either way, no rule per card. AI-13 tries the RULES route on the
  same cases (Shayne's lessons as general rules); both are measured on AI-4.2's positions.

## Tasks

- [ ] AI-4.1 Once AI-2 plays: watch where it goes wrong for lack of a strategy, then interview
- [ ] AI-4.2 Positions for Shayne's bounce and removal cases (Decisions, 2026-10-24), built by hand as
      AI-1 built the Rank Rat (`AiPositionTests`): one where holding is right, one per value case
      where casting is — Shayne agrees each answer before it is a target (AI-9's rule). **Holding:
      #547 of `GalliaWontAttack`, agreed (2026-10-27: Pass)** — a real game, not built by hand;
      the search passes there since AI-10.4 (`AiPositionTests.Unsummon_IsHeld_AtMove547`, 2026-10-28)

## Next

AI-2 and AI-3 are done, so AI-4.1 can start. Its inputs: the plays that looked wrong at the table
(AI-8 — what it fixed, and what it deferred to `DesignNotes.md`), and AI-8.2's finding that the
rollout casts **one spell a turn** (`Rollout.Settings.ActionsPerTurn` = 1, never measured alone) —
so a hand of several spells is worth only their held price inside the look-ahead. AI-5 deferred
held spells as options to this story; the open question on plans and held spells is where they
meet. TBL-5.6's comparison report, run on Shayne's games, is a further input: where his choices
and the search's differ.

## Log

- 2026-10-16: created from Shayne's plan idea during the AI-2 interview.
- 2026-10-23: Next pointed at its inputs (sprint housekeeping) — AI-2 and AI-3 done.
- 2026-10-24: Shayne's bounce and removal cases added (from AI-11, where they were first written);
  AI-4.2 added. AI-11's settled positions are how a fix here is measured. AI-13 created beside it.
- 2026-10-27: #547 agreed as AI-4.2's holding position (Shayne: Pass — his reasons in Decisions).
