---
id: AI-13
title: Shayne's lessons as general rules for the search — card-agnostic, switchable, measured
status: backlog
area: AI
created: 2026-10-24
---

## Why

Shayne (2026-10-24): Magic is complex enough that years of play teach lessons a simulated game
state won't find on its own — when to hold Unsummon (AI-4's cases), when the right time to play a
card comes. Rather than only hoping the look-ahead finds the line, try coding the lessons in. "I
think it might be a good thing to test out."

The look-ahead can't learn these by itself: it sees two turns, its simulated opponent plays the
board only (AI-10), and our side casts one spell a turn (AI-8.2). A lesson that comes from
thousands of games is exactly what a two-turn search won't rediscover.

## Requirements

- [ ] A **lessons ledger** in this story: each lesson in Shayne's words, the general rule it
      becomes, and its status (proposed, built, kept, dropped — with the measurement)
- [ ] Each lesson is a **general rule, never a card's own script** — it reads what a card does
      (an instant, a bounce, a token, an Aura), never its name — so it holds for every card and set
- [ ] Each is its **own switch** (as `Evaluator.Terms` and the rollout models are), so it can be
      measured on and off
- [ ] Each is pinned by positions where Shayne agrees the right answer (AI-4.2's for the bounce
      cases), and **kept only if it doesn't lose games** — the AI-6 way: `--suite`, then
      `--match … --until-clear`; a rule that wins its example but loses overall doesn't ship

## Decisions

- 2026-10-24, Shayne: create the story; worth testing.
- 2026-10-24, Claude (proposed, open to change): **general rules, not per-card logic.** Forge, the
  open-source Magic AI, writes "when to play me" per card: it plays decently, but every new card
  needs new code and the rules quietly contradict each other. This repo already refuses a special
  case per card phrase (MtgCore's lesson). A lesson becomes a rule that steers the search — a
  penalty, a bonus, or a move it doesn't consider — and the search still decides.
- 2026-10-24, Claude (proposed): **how this splits with AI-4.** AI-4 COMPUTES the price of holding
  a spell (plans, held spells as options); AI-13 tries the RULES route on the same problems. Both
  are measured on the same positions, and a rule that turns out to be a cheap stand-in for AI-4's
  computed answer is fine until AI-4 lands.

Open questions:
- How a rule steers: a score adjustment the search adds, a move left out of the search (pruning),
  or an ordering hint (searched first)? Pruning is the cheapest and the most dangerous — a pruned
  move can never be chosen, even in the exception.
- Where rules live: one file of named rules, each a switch, beside the evaluator?
- Which lesson first — Shayne's pick.

## The lessons ledger

| # | Lesson (Shayne) | General rule (proposed) | Status |
|---|---|---|---|
| 1 | Bounce and removal: wait until they give real value, or use them in desperation (AI-4, Decisions 2026-10-24: his value and desperation cases) | **Patience with instants:** don't cast an instant now if casting it later does as much or more — unless the alternative is losing (desperation) | proposed |
| 2 | Unsummon's value cases: a token, an Aura or counters, a creature in a double block, your bomb under removal, an attacker at a planeswalker that matters, a big creature for tempo (AI-4) | **What a bounce costs them:** a bounce is worth what is lost when the creature leaves play — a token is gone, Auras and counters are gone, the mana is paid again, a combat is won — computed for any card | proposed |
| 4 | At #547 (AI-4.2's holding position, 2026-10-27): don't bounce what you can trade with in combat; a recast gives them its "enters" again (Fateshaper returning a creature from their graveyard); a creature already stunned or tapped needs no answer now — "at the very latest wait a turn"; at 5 life "I can still take a hit"; hold for their bad attack, when Unsummon plus a trade wins two for one "when they don't expect it" | Part of lesson 1, sharpened: **an instant cast now must beat holding it** — beat what combat already does (a trade), not hand them an "enters" again, and not answer what can't hurt you this turn | proposed |
| 3 | Hard removal: a 1-for-1 — weigh the threat against tempo and against holding for a better target (AI-4) | needs a target's threat over time (AI-12) | waiting on AI-12 |

New lessons go here as Shayne thinks of them — in his words first, the rule after.

## Tasks

- [ ] AI-13.1 Shayne picks the first lesson; agree how a rule steers (open questions)
- [ ] AI-13.2 The first rule behind a switch, its positions (AI-4.2), measured; kept or dropped in
      the ledger

## Next

Not started. Read AI-4 (the bounce and removal cases, AI-4.2's positions), AI-10 (the passive
simulated opponent — rule 2 may be what an honest look-ahead gives for free), `Evaluator.cs`
(`Terms`: how a term is a switch), `BeamSearchAi.cs` (where a move is scored), and AI-6's
measuring steps (`RunningSimulations.md` doesn't cover MagicCore — `Commands.md` does).

## Log

- 2026-10-24: created — Shayne's idea; machine learning for the AI noted as a later option in
  `DesignNotes.md` ("MagicCore AI: machine learning, later").
- 2026-10-27: lesson 4 from Shayne's reasons at #547 (AI-4.2's first agreed holding position).
