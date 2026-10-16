---
id: DEV-2
title: mtg-real exists somewhere besides this machine
status: done
area: DEV
created: 2026-10-16
---

## Why

`mtg-real` has 59 commits on top of `f112255` (2026-10-16) — the whole real-rules engine, every FRA card and
MagicTable — and none of it has ever been pushed. `origin` (github.com/slippery39/SQGodotCommon)
has `main` and `kin-pivot` but no `mtg-real` (checked 2026-10-16 with `git ls-remote`). A lost disk
loses all of it.

## Requirements

- [x] `origin/mtg-real` exists and matches the local branch

## Decisions

- Standing rule: Shayne pushes; Claude never pushes unasked.

## Tasks

- [x] DEV-2.1 Shayne: `git push -u origin mtg-real` — pushed at `4ec90ac`

## Next

Done. The branch tracks `origin/mtg-real`.

## Log

- 2026-10-16: created from the "never pushed" note in the old handoff.
- 2026-10-16: Shayne pushed; verified `origin/mtg-real` = HEAD `4ec90ac`, tracking set.
