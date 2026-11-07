---
id: DEV-4
title: Split magic-mechanics.md by subsystem, so a session loads only the rules it touches
status: backlog
area: DEV
created: 2026-11-07
---

## Why

`.claude/rules/magic-mechanics.md` loads for every MagicCore file and stays in the context for the rest of
the session, re-read on every call. TRK grew it from 24 KB to 66 KB (~6k → ~16k tokens) over 22 commits, each
batch adding its mechanics' rules there. At ~200 calls a session and cache reads at 0.1, that is ~0.3M a
session (~6% of a 5M batch) — and growing with every set. Shayne, 2026-11-07, after TRK's usage review
(`docs/findings/card-batch-usage.md`: 108M for 335 cards): a prerequisite before any new set.

## Requirements

- [ ] One rules file per subsystem (mana and costs, casting and the stack, combat, zones and moving cards,
      statics and type changes, triggers, counters, the AI's evaluation terms …), each with a `paths:` that
      matches only its own source files. Every rule lives in exactly one file; nothing is dropped.
- [ ] Each file verified to LOAD for a file it claims and NOT for one it doesn't (root `CLAUDE.md`,
      "Maintaining these files": the quote test; LF frontmatter, no `**` in a glob).
- [ ] Measured before and after with `tools/usage_report.py` on comparable batches: the rules' share of the
      context, and the session total. Kept only if it saves.
- [ ] The `/card-batch` skill and `MagicCore/CLAUDE.md` point at the new files.

## Decisions

- 2026-11-07, Shayne: noted as a story; done **before any new set is built** (with DEV-5).

Open questions:
- Whether a card-batch session ends up touching most subsystems anyway (a tier-3 batch edits mana, casting
  and zones in one go) — measure how many of the split files one batch loads before choosing the cuts.

## Tasks

- [ ] DEV-4.1 Measure: which MagicCore files a recent batch opened, so the cuts follow real sessions
- [ ] DEV-4.2 Split, verify each file loads where it should and nowhere else
- [ ] DEV-4.3 Measure a batch after; record in `docs/findings/card-batch-usage.md`

## Next

Read `.claude/rules/magic-mechanics.md`'s headings and its `paths:`, the root `CLAUDE.md` "Maintaining these
files" (the two silent failures), and the last few sessions' file reads (`tools/usage_report.py`). DEV-4.1
first.

## Log

- 2026-11-07: created — Shayne, after TRK's usage review; a prerequisite for the next set.
