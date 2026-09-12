---
paths:
  - "MtgSimulator/Scenarios/*.cs"
  - "MtgSimulator/DebugSnapshot*.cs"
  - "MtgSimulator/FlaggedGameSaver.cs"
  - "MtgSimulator/GameStateSnapshot.cs"
  - "MtgSimulator.Tests/*StateJson*.cs"
  - "MtgSimulator.Tests/*Scenario*.cs"
  - "SQGodotCommon/MtgGame/*/*Inspector*.cs"
---

# MtgSimulator — scenarios, snapshots and AI inspection

Loaded when you touch saved positions, snapshots, or the inspector overlay.

## Flagged Game Snapshots

When a game is flagged, both runners call `FlaggedGameSaver.TrySave()` immediately after the game ends. Snapshots are written to `flagged_games/` (next to the binary) as indented JSON. Naming: `game_{number}_{reason}_{timestamp_ms}.json`. Saves are capped at `FlaggedGameSaver.MaxSaves` (25) per run — if a run produces more flagged games the first 25 are sufficient to diagnose the cause.

Each snapshot includes:
- Final board state: per-player life, mana, hand card names, library count, graveyard, battlefield creatures (with P/T, damage, sickness, attacked flags)
- Stack contents at termination
- Turn-by-turn event log (`TurnLogs`): one `TurnLog` per half-turn, each containing human-readable event strings (e.g. "Player 1 cast Lightning Bolt", "Goblin attacked", "Player 2 took 3 damage")
- Exception message and stack trace when `EndReason` is `UnhandledException`

`FlaggedGameSaver.TrySave` no longer takes `MtgGameIds` — it resolves all IDs it needs directly from `GameState` via `GetWellKnownId`.

## Interactive Debug Snapshots

`DebugSnapshotBuilder.BuildJson(history, aiDecisions, error)` is the Godot-side counterpart to
`FlaggedGameSaver` — it serialises the whole `MtgGameManager` history plus every AI decision.
`DebugSnapshot.Error` carries crash context; null means a manual export.

`MtgGameScene` writes these to `user://debug_snapshots/`:

| Prefix | Trigger |
|---|---|
| `debug_` | F5, manual |
| `crash_` | An exception in the AI turn loop, or the process-wide unhandled/unobserved handlers |
| `hang_` | The AI turn exceeded `MaxAiStepsPerTurn` without ending |

The AI turn loop is `async void`; an exception escaping it kills the process with nothing logged,
which is why it is wrapped and why `MtgGameManager.ForceEndAiTurn()` exists — it drains any
pending choice (an unresolved `ChoiceAction` blocks the action stack forever) and hands the turn
back rather than leaving the game wedged.

## AI Inspection Tooling

Three pieces over one data model. The point of all of them is the **term breakdown**, not the
score: "this action scores 4.52" is not a diagnosis, but `creatures +3.00, power +4.00,
race −3.20, hand −1.40, lands-in-hand +0.00` is — the land-pricing defect is visible on sight in
the second form and invisible in the first.

**`StateEvaluator.Explain` is the implementation and `Evaluate` is a one-line wrapper over its
`Total`.** That direction is deliberate. A second copy of the weighted sum written for display
would drift from the one the search uses, and a panel showing terms that do not sum to the real
score sends you hunting a discrepancy that exists only in the renderer. `EvaluationBreakdownTests`
pins them together. It returns a `readonly record struct`, so the hottest call in the engine
allocates nothing.

**Measured at ~0% over 300 games**, three interleaved rounds (57.75s vs 58.78s, the gap inside
first-run JIT warmup). Three earlier measurements said +11.8%, +8.0% and +8.8% and were all
measuring the win-detection bug above, not this. Do not re-split them on an unmeasured hunch.

**Summation order in `Explain` must not change.** Float addition is not associative and
`DeterminismTests` / `MachineIndependenceTests` compare exact results.

| Piece | Where | Trigger |
|---|---|---|
| Overlay | `SQGodotCommon/MtgGame/Board/AiInspectorPanel.cs` | **F6** in game |
| Scenario capture | `MtgGameScene.SaveScenario` | **F7** in game |
| Standalone viewer | `Scenarios/ScenarioConsole.cs` | console **mode 5** |

`AiDecision.StateBefore` and `AiActionCandidate.Breakdown` are populated only under
`_captureDecisions` — all three `SetLastDecision` call sites are already guarded — so the simulator
and trainer pay nothing for them.

**`AiActionCandidate.Score` is a ROLLOUT score, not the breakdown's total.** It is the evaluation
of a state two turns ahead; the breakdown is the immediate position. They will not agree, and the
gap between them is exactly what the lookahead contributed, which is why both are shown.

## Scenarios are serialized state, not snapshots

`GameStateSnapshot` renders a position for a human and **cannot be loaded back**. `StateJson` is
the other thing: a real `GameState` round-trip, so a saved position can be handed to
`SelectAction`.

Polymorphic types get a `$type` discriminator resolved by **reflection over every abstract type in
the game assemblies**, not `[JsonDerivedType]` attributes. The state holds 133 polymorphic types
across five hierarchies and the set grows with every new card mechanic; annotating each one means a
forgotten attribute silently breaks scenario loading. The first draft named four bases by hand and
the round-trip test immediately found a fifth — `TargetSpecification`, nested two levels inside a
card's effects.

Three things that would otherwise be silent bugs, each pinned by `StateJsonTests`:
- **`GameObject.Children` is dropped.** It is documented as view-only, `ParentToChildren` is the
  source of truth, and `LoadFrom` populates it recursively — so a hydrated state would serialize
  the object graph exponentially.
- **`ImmutableStack` enumerates top-first**, so a converter that pushes in read order inverts the
  action stack.
- **Metadata values carry their own type tag.** `GetMeta<T>` casts, so an `int` returning as a
  boxed `JsonElement` throws at some unrelated call far from the load.

Round-trip is asserted on **behaviour** — same evaluator score, same legal actions, same AI
decision. A state differing in a field the AI never reads is fine; one that scores differently is
a broken scenario.

**Godot writes `user://scenarios/` and the console reads `scenarios/` relative to the shell's cwd.**
They do not meet on their own — same trap as the draft model asset. F7's toast prints the absolute
path so the copy is one command.

`ScenarioConsole.Strategies` is the single place strategies are named. "Construct these AIs by name
and run them" is the same requirement for a one-position diff and for a head-to-head strength
harness; keep it one list.
