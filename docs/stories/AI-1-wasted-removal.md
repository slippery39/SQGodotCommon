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

Open questions:
- **Cheap fix first, or straight to AI-2 (beam search)?** The cheap fix: value cards in hand in the
  evaluator, and weigh a removal target against the spell. It may be enough for playtests; AI-2 is
  the real cure for one-ply blindness and is far bigger.

## Tasks

- [ ] AI-1.1 Capture the position — MagicTable's seed + undo make it repeatable — as a test
- [ ] AI-1.2 Answer the open question

## Next

Read `MagicCore/Ai/GreedyAi.cs` and `docs/findings/magic-ai.md` first. Measure before claiming:
at 25 games a row moves ±2 by chance.

## Log

- 2026-10-16: created from the DesignNotes entry.
