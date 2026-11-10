---
id: LIM-4
title: Deck builder search: find cards by name and keyword
status: done
area: LIM
created: 2026-11-09
---

## Why

Shayne, 2026-11-09: the deck builder (SealedScene) needs a search box that finds cards by keyword and rules text, as a pool of 90+ cards is slow to scan.

## Requirements

- [x] A search field filters the pool by name and rules text/keywords, live
- [x] It works with the colour grouping (matches shown within their colour) and never hides a card from the deck list

## Decisions

- (none yet)

## Tasks

- [x] LIM-4.1 Search box in `SealedScene` (the sandbox panel and `ChoicePanel` already have one at 24+ options - reuse)
      Built: a box in the rarity row filters the POOL only, live, ANDed with the colour/kind/rarity filters; every
      word typed must appear in the name, type line, rules text (either face) or keywords
      (`SealedSession.Matches`, tested); All clears it too. Captured (sealed, seed 7); typing not driven by a
      capture (`--tap` is mouse only) — the match is the test's

## Next

Read this story, then `.claude/rules/magic-table.md` (table) or `MagicCore/CLAUDE.md` (engine).

## Log

- 2026-11-09: opened from Shayne's playtest notes.
- 2026-11-10: LIM-4.1 built overnight (unattended); LIM-5 builds the rest of the Arena deck builder around it.
