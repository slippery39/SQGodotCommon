---
name: ai-inspect
description: Watch what the MTG AI actually decided before changing how it scores. Captures a live position with F7, loads it into console mode 5, and compares several strategies in the same position with their evaluator terms broken out. Use when the AI "makes a bad play", "misvalues" something, when proposing a StateEvaluator or scoring weight change, tuning beam search, or investigating a flagged game. Also use when a change to evaluation is being argued from reasoning rather than from a position.
---

# Inspecting an AI decision

**Evaluation changes were argued rather than watched for two sessions, and several were wrong.**
This skill is the "watch it first" loop. Do not propose a scoring change without a position.

Reference is `.claude/rules/sim-scenarios.md` (loads on `MtgSimulator/Scenarios/`) and
`.claude/rules/sim-ai.md` (loads on strategies and evaluators). Measured strength results are
`docs/findings/ai-strength.md`.

## Before you start: read the pattern

Four evaluator changes have been measured and **all four came back neutral**. The reason is likely
structural — the engine already evaluates by playout, so `ScoreAfterCompletingTurn` observes much of
what a hand-written term is trying to predict. Read `docs/findings/ai-strength.md` before proposing
a fifth. If your change is worth making, it should be visible as a *wrong decision in a specific
position*, not as a plausible argument about weights.

## Capturing a position

In the Godot game:

1. **Space** — pause the AI. Do this **first**. Unpaused, the AI moves on while you read the
   overlay and you inspect a position that no longer exists.
2. **F6** — the inspector overlay. Every ranked action, and the chosen one's score split into
   evaluator terms.
3. **F7** — save the live position. Writes to `user://scenarios/`; the toast prints the absolute
   path.

**The term breakdown is the point, not the score.** "This action scores 4.52" is not a diagnosis.
`creatures +3.00, power +4.00, race −3.20, hand −1.40, lands-in-hand +0.00` is — a land-pricing
defect is visible on sight in the second form.

## Comparing strategies in that position

The game writes to `user://scenarios/`; the console reads `scenarios/` relative to the **shell's**
working directory. Copy it across:

```
cp "<path from the F7 toast>" scenarios/
printf '5\n1\n\n' | dotnet run --project MtgSimulator.Console -c Release
```

Mode 5 loads the scenario and has several strategies decide in the same position, printing each
one's chosen action and the evaluator terms it moved. Run it from the repo root.

## Two traps

**A scenario is serialized state, not a snapshot.** `GameStateSnapshot` renders a position for a
human and **cannot be loaded back**. `StateJson` is the real `GameState` round-trip — that is what a
scenario is. If you find yourself trying to load a snapshot, you have the wrong type.

**Verify the console's `bin/` timestamps before trusting a run.** `dotnet build -c Release` can
report success while leaving a stale `MtgCore.dll` / `MtgSimulator.dll` in
`MtgSimulator.Console/bin/Release/net10.0/`. The tell is maddening: unit tests pass because the test
projects rebuild correctly, while the run disagrees — which reads exactly like a real bug in your
fix. This cost two full training runs and three wrong conclusions in one session.

```
ls -la MtgSimulator.Console/bin/Release/net10.0/MtgCore.dll   # must be newer than your edit
```

**When a run contradicts a passing unit test, suspect the binary before the diagnosis.**

## Changing the evaluator

`StateEvaluator.Explain` is the implementation and `Evaluate` is the wrapper around it. **Never
write a second copy of the sum for display** — the display and the decision must come from the same
code or the inspector lies to you.

If you do make a change, measure it with `StrengthHarness` (`EvaluatorStrengthTests`, `[Explicit]`).
**Every strength claim either comes from there or is a guess.** Note that a head-to-head is the
wrong instrument for a per-card bug — a fix can be correct and still measure 50.2% ± 1.5pp.

## Done when

- [ ] The bad decision is reproduced in a saved scenario, not described
- [ ] The term breakdown names which term is wrong
- [ ] `bin/` timestamps checked if a console run informed the diagnosis
- [ ] Any change measured through `StrengthHarness`, or explicitly labelled unmeasured
