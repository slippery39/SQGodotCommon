---
id: AI-31
title: The oracle re-examined — what the suite's "agrees with the oracle" measures, and whether it still should
status: backlog
area: AI
created: 2026-11-18
---

## Why

Shayne (notes, read at the sprint 5 review, 2026-11-18): "the oracle - you always mention the oracle, but the agreed
plays have been trending down as I have been fixing things. It makes me think we should take a second look at what the
oracle is actually doing."

The suite's agreement: 79.9% at AI-22.2 (2026-11-12), 76.0% after the sprint 5 merge (2026-11-18). The oracle is a
search looking four turns ahead, read once, with the tables and the model of its day — and it reads the real library
order (`Commands.md`, "Measure an AI change"), so as the AI got honest about hidden cards (AI-3.9, AI-20) it should agree
less while playing better.

## Requirements

Not agreed — to shape with Shayne.

Open questions:
- Regenerate the oracle under today's model (honest about hidden cards), or replace it with agreed answers — Shayne's
  note positions, each with the play he called right?
- Is a falling agreement still worth printing on every suite run until then?

## Tasks

- [ ] AI-31.1 Read what the oracle is and when it was read; the positions where today's default and the oracle disagree,
      sorted by why (hidden information, an old table, a real misplay)

## Next

Start at AI-31.1. Read `Commands.md`'s "Measure an AI change" and the oracle's code (`PositionSuite`).

## Log

- 2026-11-18: created at the sprint 5 review.
