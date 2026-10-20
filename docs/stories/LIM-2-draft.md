---
id: LIM-2
title: Draft Reality Fracture against bots
status: backlog
area: LIM
created: 2026-10-20
---

## Why

The second limited use case from the first interview (MC-1): draft vs bots. Split from LIM-1 on
2026-10-20, when Shayne chose sealed first.

## Requirements

- [ ] Draft 3 packs of 14 with 7 bots, the packs LIM-1's generator makes
- [ ] The echoed-pair block reads as a signal (a pack with two pair cards from different pairs
      means the partner was taken) — the bots' picks must leave that trace honestly

## Decisions

- 2026-10-20, Shayne: after sealed (LIM-1); same Arena packs and collation rules.

Open questions:
- Draft bots: how good, and from what — 17lands data (DATA-1), card values, or simple picks?
- Arena's draft (Quick: bots; Premier: humans) — which does it model?

## Tasks

- [ ] LIM-2.1 Interview the open questions

## Next

Needs LIM-1's pack generator and AI builder. MtgSimulator already drafts (`MtgSimulator/Draft/`,
rules in `.claude/rules/sim-draft.md`) and MtgGame has a draft screen (`SQGodotCommon/MtgGame/
Draft/`) — grep them first and copy-adapt.

## Log

- 2026-10-20: split from LIM-1.
