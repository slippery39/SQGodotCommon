---
id: DEV-4
title: Split magic-mechanics.md by subsystem, so a session loads only the rules it touches
status: in-progress
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

- [x] One rules file per subsystem (mana and costs, casting and the stack, combat, zones and moving cards,
      statics and type changes, triggers, counters, the AI's evaluation terms …), each with a `paths:` that
      matches only its own source files. Every rule lives in exactly one file; nothing is dropped.
- [x] Each file verified to LOAD for a file it claims and NOT for one it doesn't (root `CLAUDE.md`,
      "Maintaining these files": the quote test; LF frontmatter, no `**` in a glob).
- [ ] Measured before and after with `tools/usage_report.py` on comparable batches: the rules' share of the
      context, and the session total. Kept only if it saves.
- [x] The `/card-batch` skill and `MagicCore/CLAUDE.md` point at the new files (and `Cookbook.md`, `DesignNotes.md`).

## Decisions

- 2026-11-07, Shayne: noted as a story; done **before any new set is built** (with DEV-5).

- 2026-11-12, Claude (cube lane, overnight): **measured, a batch touches every subsystem** — the last ten
  `/card-batch` sessions all opened `Effects/`, card scripts and card tests, and most `Actions/`, `Turns/`, `Rules/`,
  `Mana/` (`docs/findings/card-batch-usage.md`). So the cut is by ENGINE subsystem, and a card script, card test or
  `Effects/` file loads all six mechanics files — a scripter needs every "phrase → piece" rule. The saving is in the
  sessions that never script a card: 22 of the last 58 loaded the whole file from an `Ai/`, `Limited/` or one engine
  file. For review: kept, since the measured cost to a batch is +5% of one load. **Shayne, 2026-11-12: kept as is**
  (not a grep-only phrase index).
- 2026-11-12, Claude: the seven files — `magic-casting` (casting, activating, the stack, modes, targets),
  `magic-triggers` (triggers, turns, "this turn"), `magic-combat`, `magic-mana` (mana and costs), `magic-zones`
  (moving cards, entering, tokens, hidden information, control), `magic-statics` (characteristics, counters,
  damage, life, copies, Auras), `magic-ai`. A rule that spans two went where its ONE place in the engine is.
  `Describe.cs`, `Stops.cs`, `Sandbox.cs` and the root test files (golden replays, the probe) load none.

## Tasks

- [x] DEV-4.1 Measure: which MagicCore files a recent batch opened, so the cuts follow real sessions
- [x] DEV-4.2 Split (158 rules, none dropped — checked line by line), verify each file loads where it should and
      nowhere else: a quote for each from its own engine file, NOT LOADED from the others', all six (no ai) from
      `Sets/Trk/TrkCards.cs`, none from `Limited/Draft.cs`
- [ ] DEV-4.3 Measure a batch after; record in `docs/findings/card-batch-usage.md`

## Next

DEV-4.3: the first card batch after the split (MC-4's, cube lane) logs its row in
`docs/findings/card-batch-usage.md`; compare its total to the TRK batches of a similar size. The AI lane's sessions
should show the larger saving — `python tools/usage_report.py --all` after a few of them.

## Log

- 2026-11-07: created — Shayne, after TRK's usage review; a prerequisite for the next set.
- 2026-11-12 (night, cube lane): DEV-4.1 measured, DEV-4.2 split into seven files, each checked to load.
