---
name: regen-card-values
description: Rebuild the measured card-value tables in sim_results/ after clearing them, or when tests fail with every card reading 0.00pp. Use when sim_results/ is missing or empty, when CardValueSweep / card-value tests fail, after changing card costs or the card pool, or when asked to regenerate, rebuild or refresh the measured tables. Covers the working-directory trap where a table loaded from the wrong place looks exactly like missing data and passes in isolation.
---

# Regenerating the measured tables

`sim_results/` is **untracked and disposable**, but four tests need the card-value table in it.

## Rebuild

From the repo root:

```
dotnet test MtgSimulator.Tests --filter "FullyQualifiedName~CardValueSweep.SweepTheCoreSetCube"
```

`[Explicit]`, roughly 8s for CSC's 408 cards. It writes the card-value JSON that the dependent tests
read.

## The trap this skill exists for

**`sim_results/` resolves against the WORKING DIRECTORY, which under `dotnet test` starts as the
test binary's folder, not the repository.** Anything reading or writing it must call
`TestPaths.ChdirToSolutionRoot()` first.

Getting this wrong **does not look like a path bug**. The table loads empty, every card reads
`0.00pp`, and the tests fail exactly as though the data were missing — while passing in isolation,
because when a test runs alone nothing else has moved the directory. So the symptom is a test that
fails in the suite and passes on its own, which reads like a test-ordering problem and isn't.

If you add or change anything that touches `sim_results/`, the first line of it calls
`TestPaths.ChdirToSolutionRoot()`.

The same class of trap one level up: `dotnet run` does not chdir into the project either, so running
the console from the repo root writes `./sim_results/` while running from inside
`MtgSimulator.Console/` writes `MtgSimulator.Console/sim_results/`. Two directories holding the same
filename is how a freshly trained model gets silently overwritten by a stale one. **Always run from
the repo root.**

## Verify it worked

Do not assume the file is good because the command exited 0. Check a value is non-zero:

```
python -c "import json;d=json.load(open('sim_results/card_values_csc.json'));print(len(d),list(d.items())[:3])"
```

If every entry reads 0.00, the working directory was wrong — not the data.

## Related tables

- **The identity presim table** is rebuilt by a full evolution run (console mode 6), or measured on
  its own with `PresimCalibrationHarness` (`[Explicit]`; `MTG_PRESIM_DECKS` / `MTG_PRESIM_OPPONENTS`
  size it).
- **The draft model** is console mode 4 — a different command with its own traps, in `Commands.md`.

## Never quote a rate from memory

**Never quote a card's win rate from memory or from a `CLAUDE.md` — compute it from
`sim_results/*.json` every time.** The tables are regenerated regularly and any figure written into
a document is stale the next time the sweep runs. That is why no card values are recorded in the
rules files.
