---
id: LIM-2
title: Draft Reality Fracture against bots
status: ready
area: LIM
created: 2026-10-20
---

## Why

The second limited use case from the first interview (MC-1): draft vs bots. Split from LIM-1 on
2026-10-20, when Shayne chose sealed first.

## Requirements

- [ ] Draft 3 packs of 14 with 7 bots, the packs LIM-1's generator makes
- [ ] Arena's **Quick Draft**: Shayne and 7 bots; the bots pick the highest-rated card, leaning into
      the colours they have already picked
- [ ] The drafted pool goes to LIM-1's deck builder and is played on the table, as sealed is
- [ ] The echoed-pair block reads as a signal (a pack with two pair cards from different pairs
      means the partner was taken) — the bots' picks must leave that trace honestly

## Decisions

- 2026-10-20, Shayne: after sealed (LIM-1); same Arena packs and collation rules.
- 2026-10-23, Shayne (sprint planning): drafting is **wanted** — FRA first. He plays powered cube
  on MTGA (maybe MTGO later) and wants to practise it here one day, so the draft must not assume
  packs: a cube is a card list. The powered cube's CARDS are deferred — their complexity is known
  to be high (a separate story, planned in depth when its time comes). Draft stats and pick
  tracking are NOT built here: an existing tracker does it (DATA-1).

- 2026-10-28, Shayne (LIM-2.1, sprint 3 planning): **Quick Draft** (him and 7 bots), not Premier.
  **Bots pick by rating plus a colour lean** — FRA's rating is the 17lands GIH WR the sealed builder
  already uses; a cube, which has no 17lands data, uses the engine's card values (DATA-2 later). Not
  a trained picker (MtgSimulator's `DraftTrainer`) and not rating alone. Built this sprint, after
  AI-15.

## Tasks

- [x] LIM-2.1 Interview the open questions — Quick Draft; rating plus colour lean (Decisions)
- [ ] LIM-2.2 The draft: 3 FRA packs of 14 from LIM-1's generator, Shayne and 7 bots, passing left
      then right then left; a bot picks by rating with a colour lean; the cards as a list, so a cube
      drafts the same way later. A test that the bots' picks leave the echoed pairs' trace
- [ ] LIM-2.3 On the table: a draft screen (pick from the pack, the picks so far), then the pool to
      LIM-1's deck builder and a game against an AI opponent's drafted deck

## Next

Needs LIM-1's pack generator and AI builder. MtgSimulator already drafts (`MtgSimulator/Draft/`,
rules in `.claude/rules/sim-draft.md`) and MtgGame has a draft screen (`SQGodotCommon/MtgGame/
Draft/`) — grep them first and copy-adapt.

## Log

- 2026-10-20: split from LIM-1.
- 2026-10-23: wanted, FRA first, a cube later; provisionally next sprint (interview first).
- 2026-10-28: LIM-2.1 interviewed (Quick Draft, rating plus colour lean); status → ready; sprint 3.
