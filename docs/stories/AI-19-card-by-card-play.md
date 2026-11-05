---
id: AI-19
title: Card-by-card play and rulings — after every card plays
status: backlog
area: AI
created: 2026-11-05
---

## Why

TRK's cards are built to PLAY first (MC-5): each gets a correct engine piece and a simple, sensible AI answer,
and a rule simplified only where nothing reads the gap (`DesignNotes.md`, MagicCore known ceilings). Shayne,
2026-11-05, on Engineered Explosives: "Explosives should be used for a relevant X amount to kill the most
appropriate targets in play. But maybe just note that in the backlog. My main goal here is to get all the
cards implemented first. Then we can optimize usage and rulings per card."

## Requirements

- [ ] Each card listed below plays as a good player would play it, checked in a position (`/ai-inspect`).
- [ ] Each ruling listed below is exact, or its simplification is named and accepted again.

## Decisions

Open questions:
- Which cards first — by how often they are played (the TRK ratings, MC-5.4 / DATA-2), or by how wrong they look at the table?

## Tasks

- [ ] AI-19.1 Engineered Explosives: choose X for the most appropriate kills in play — today X is any the search
      tries, and the counters are the colours the payment can spend (MC-5.3 batch 6a). Also "the most colours"
      never spends a Treasure or Starfleet Academy for a colour — Shayne may want it to.

## Next

Not started. Add a card here whenever a batch leaves its play or a ruling simple on purpose; start once
MC-5 has every card playing.

## Log

- 2026-11-05: opened with Engineered Explosives (MC-5.3 batch 6a).
