---
id: AI-1
title: The AI plays plausibly — no wasted removal
status: backlog
area: AI
created: 2026-10-09
---

## Why

A playtest only means something against a plausible opponent. On 2026-10-09 the AI cast **Prophesied
End** ("Destroy target creature. If it wasn't attacking, its controller draws a card.") on a
non-attacking **Rank Rat**: premium removal on a 1-drop, AND a card for Shayne. `GreedyAi` simulates
each move one ply and scores the result — it cannot see that the card drawn is worth more than the
rat, or that the spell is better held for an attacker. Source: `DesignNotes.md`, "MagicCore's AI
plays implausibly — one-ply greedy is not enough (backlog)".

## Requirements

- [ ] In the captured Prophesied End position, the AI does not make that play
- [ ] "They draw a card" costs in the evaluation; a removal target's value is weighed against the spell's
- [ ] Every change measured over 25+ games per deck pairing and recorded in `docs/findings/magic-ai.md`
      — including when the result is noise (it was, twice)
- [ ] Evaluation terms stay monotonic (`MagicCore/CLAUDE.md` — a cap silently picks the passive move)

## Decisions

- 2026-10-16, Shayne: **AI-2 (the beam search port) goes first**; this story waits to see what the
  search fixes on its own. The Rank Rat position becomes one of AI-2's position tests (AI-2.6).

Why the search alone may not fix it (2026-10-16): every non-land card in hand scores a flat 1.2
(`Evaluator.Side`, `MagicCore/Ai/Evaluator.cs`), the Rat dying about 4.5 and the drawn card 1.2 — so casting nets ~+2 over passing,
and any removal is worth less held than almost any target. MtgCore's search has the same flat hand
value (1.4) and its default opponent model never casts, so no better target ever arrives in a
rollout. AI-3's richer opponent models are the other half of the cure.

## Tasks

- [ ] AI-1.1 Capture the position — MagicTable's seed + undo make it repeatable — as a test
- [ ] AI-1.2 After AI-2 and AI-3: does the search still waste removal? If so, value held cards

## Next

Read `MagicCore/Ai/GreedyAi.cs` and `docs/findings/magic-ai.md` first. Measure before claiming:
at 25 games a row moves ±2 by chance.

## Log

- 2026-10-16: created from the DesignNotes entry.
- 2026-10-16: open question answered — AI-2 first.
