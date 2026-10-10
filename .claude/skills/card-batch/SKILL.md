---
name: card-batch
description: Build a batch of cards for MagicCore (the real-rules engine on branch mtg-real) — survey the batch, tier each card, script, test by tier, revert-check, probe, regenerate the docs, /pre-commit, ask, commit. Use when implementing cards for a MagicCore set (Reality Fracture / FRA, or a new set), "the next batch", "build these cards", "add the rares", or when starting a set. Not for MtgCore cards (that is /add-card).
---

# A MagicCore card batch

Measured (2026-10-10, `tools/usage_report.py`): the FRA rare batches cost ~3M input-token equivalents
each, and **70% of it was re-reading the conversation** — every call re-read a ~440k-token context
because eight batches ran in one session. A batch in a FRESH session was simulated to cost 37–40% less.
So this skill is built to start cold and cheap: read the short generated docs, not the engine.

## 0. Start of the batch — fresh, and small

- **One batch per session.** After a commit, the next batch starts in a NEW session (or after `/clear`)
  — never carry a finished batch's context into the next one. The handoff and these docs make a cold
  start cheap; a 400k-token context makes every call expensive.
- **Read, in this order, and nothing more yet:** the handoff's "next" section, `MagicCore/AbilityCatalog.md`
  (what pieces exist — instead of the engine source), `MagicCore/Cookbook.md` (how scripts are written).
  `.claude/rules/magic-mechanics.md` loads itself when you open an engine file.
- **Card text:** `python tools/magic_set.py text "A" "B" …` — never inline Python over the JSON.
- Read engine source only where you will EDIT it, and only that region (grep the line first).

## 1. Survey and tier

For each card, find the pieces in the catalog and give it a tier:

| Tier | The card | It gets |
|---|---|---|
| 0 | Vanilla / keywords only | Nothing — no script |
| 1 | Only pieces AND options already tested | Script only; its card-sheet entry read against the printed text |
| 2 | A tested piece with a new option or combination | One test of the new option; revert-check that line |
| 3 | A new engine piece | Grep MtgCore first; full tests, revert-checks, probe |

**Grep MtgCore before building any tier-3 piece** (memory "Copy from MTG") — copy and adapt, never
modify MtgCore. Tell Shayne the tiers and the new pieces in one short list before building.

## 2. Build

- Scripts go in the set's Cards file (`FraCards.cs` `Scripts`), in a `// ===== batch =====` section.
- **Edits through `tools/edit.py`**: write a spec with the Write tool (`@@ path`, then `<<<< OLD` /
  `==== NEW` / `>>>> END` blocks) and apply it in one Bash call. All edits apply or none; each file
  keeps its own line endings (DesignNotes.md and Commands.md mix them); indentation differences
  still match; a miss prints the nearest lines — fix the spec, don't re-read the file.
- New engine pieces get a `///` summary naming the card and quoting its text — **the catalog is
  generated from those summaries**; a piece without one is invisible to the next session.

## 3. Check, by tier

- **Tiers 2–3: a test that the ability FIRES**, through real casting (`FraBoardTest`, `TestCards`).
  New test file: check `git status` first; never overwrite.
- **Revert-checks** (tiers 2–3): one spec (`@@ path | TestFilter` blocks) through
  `python -u tools/revert_check.py SPEC`. Every line must say `ok`; "NOT CAUGHT" means the test is
  vacuous or the revert hit the wrong line — fix the one that is wrong.
- **Fuzz probe** (if the batch has tier 3): `MAGIC_PROBE_CARDS="A;B;…" dotnet test MagicCore.Tests
  --filter "FullyQualifiedName~CardProbeTests"` — crashes fail it; read the per-card report, and look at
  any card marked NEVER USED.
- **Hot path touched** (MoveCard, Triggers.Collect, the state-based check, mana payment, the AI):
  `tools/bench_vs_head.sh`.
- **Tier 1 check:** `python tools/magic_docs.py`, then read the batch's entries in
  `MagicCore/FraCardSheet.md`: printed text against script — a missing clause, a wrong "whose", the wrong
  target kind. That replaces a test for a card made of tested pieces.
- The full suite once at the end: `dotnet test MagicCore.Tests`.

## 4. Docs, review, commit

- `python tools/magic_docs.py` (catalog + sheet), `python tools/magic_set.py coverage` → the coverage
  line in `MagicCore/FraCoverage.md`; new mechanics' rules in `.claude/rules/magic-mechanics.md`;
  deliberate simplifications in `DesignNotes.md` ("MagicCore known ceilings", append in binary, CRLF).
- `/pre-commit`. Point its code review at the ENGINE changes: tier 0–1 card scripts are checked by the
  card sheet, not line by line. Fix each finding with a test that fails without the fix.
- Update the handoff's "next" section, ask Shayne, commit only on a yes, never push.
- Optional now and then: `python tools/usage_report.py` — the batch's cost, to see the savings hold.
