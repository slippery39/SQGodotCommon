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

## Tasks

- [ ] AI-4.1 Once AI-2 plays: watch where it goes wrong for lack of a strategy, then interview

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
