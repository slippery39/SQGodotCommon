---
id: LIM-1
title: Sealed and draft for Reality Fracture
status: backlog
area: LIM
created: 2026-10-07
---

## Why

Two of the use cases from the first interview (MC-1): **a sealed pool to builds** and **draft vs
bots** — the reason for the whole engine is building better limited decks before an Arena direct.
It also unlocks the last FRA card, **Extrapolate the Impossible**, whose "from outside the game"
needs a sideboard.

## Requirements

- [ ] Extrapolate the Impossible plays, with the sideboard as "outside the game"

To be shaped — see the open questions.

## Decisions

- 2026-10-07, Shayne: Bo1 only for now; Bo3 and sideboarding maybe later.
- 2026-10-09, Shayne: after the UI, his choice between this and a search AI (AI-2).

Open questions:
- Sealed first, draft first, or both?
- Real FRA booster collation (play boosters, rarity slots) or something simpler?
- What does "pool to builds" mean: suggested decks, a deck builder UI, sims ranking builds — or all three?
- Draft bots: how good, and from what — 17lands data (DATA-1), card values, or simple picks?
- Console first, or straight onto MagicTable?

## Tasks

- [ ] LIM-1.1 Interview the open questions, then split into stories

## Next

MtgSimulator already drafts (`MtgSimulator/Draft/`, rules in `.claude/rules/sim-draft.md`) and
MtgGame has a draft screen (`SQGodotCommon/MtgGame/Draft/`) — grep them first and copy-adapt.

## Log

- 2026-10-16: created from the interview's use cases and the old handoff's "Later" list.
