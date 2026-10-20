---
id: DATA-1
title: Bring 17lands data into the engine's tools
status: backlog
area: DATA
created: 2026-10-07
---

## Why

Shayne already uses 17lands and said in the first interview (MC-1) that he will bring its data in
later. Real play data could inform card values, draft bots and deck analytics.

## Requirements

To be shaped — see the open questions.

## Decisions

- 2026-10-07, Shayne: engine first; 17lands later.
- 2026-10-20, 17lands' terms: **only the public datasets** (17lands.com/public_datasets) may be used
  outside the site — its API says "This data is only for use on 17Lands.com". The old
  `card_ratings/data` endpoint is also a frozen snapshot (any date range gives the same numbers; EOE
  shows zero games). `tools/import_17lands.py` computes from the public game data instead.
- 2026-10-20, Shayne (LIM-1): the sealed builder's card ratings come from 17lands for FRA — this
  story's first slice (`Sets/FRA.17lands.json`: GIH WR and games, Sealed and Premier Draft).

Open questions:
- What else for: draft-bot picks (LIM-2), deck analytics, checking our sims against real win rates
  (DATA-2 does that for card ratings)?

## Tasks

- [ ] DATA-1.1 Interview the open questions

## Next

Nothing to read yet — start with the interview.

## Log

- 2026-10-16: created from the first interview.
- 2026-10-20: first slice landed with LIM-1.4 — GIH WR per card from FRA's public game data
  (298k Premier Draft games, 61k Sealed; files of 2026-10-19).
