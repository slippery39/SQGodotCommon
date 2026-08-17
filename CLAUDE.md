# SQGodotCommon Solution

## Solution Map

```
SQGodotCommon/
├── ImmutableGameObjects/
│   ├── ImmutableGameObjects/        # Immutable game state library (GameState, GameAction, PipelineAction)
│   ├── ImmutableGameObjects.Tests/  # Library unit tests
│   └── ImmutableGameObjects.Benchmarks/
├── MtgCore/                         # MTG card game engine (actions, cards, turns, combat, targeting)
├── MtgCore.Tests/                   # MTG engine unit tests
├── MtgConsole/                      # Console presentation layer (ConsoleGameLoop, ConsoleRenderer)
├── MtgSimulator/                    # Simulator library — AI strategies, runners, deck factories (referenced by Godot + tests)
├── MtgSimulator.Console/            # Thin console entry point (Program.cs only — references MtgSimulator)
└── SQGodotCommon/                   # Godot project — reusable utilities (Common/, Project/)
    └── MtgGame/                     # MTG front end: board, deck select, draft + tournament
```

The MTG game is played in `SQGodotCommon/MtgGame/`. It talks to the engine only through
`MtgGameManager` (plain C#, no Godot types) — see `MtgSimulator/CLAUDE.md` for the draft path.

## Platform

C# on .NET.

## General Principles

- SOLID where applicable; no oversized files
- Use functional or OOP as the problem dictates — not dogmatic about either
- Game Logic and UI Logic must be completely separated; no game logic in presentation layers
- Make changes in small, reviewable steps; never large all-at-once changes
- Never assume on vague requirements — confirm before implementing

## CLAUDE.md Maintenance

A CLAUDE.md file exists at the root and in each active project (`ImmutableGameObjects/ImmutableGameObjects/`, `MtgCore/`, `MtgSimulator/`). When code changes affect the architecture, patterns, rules, or known issues described in these files — update the relevant CLAUDE.md in the same step. Source maps in particular go stale quickly; keep them accurate as files are added or removed.

`DesignNotes.md` at the solution root is the companion watchlist: decisions that work for the
current scope but will need revisiting. Deliberate deferrals go there with their costed options,
not in CLAUDE.md.

## Card Sets

Two draftable sets exist, both registered in `SetRegistry`:
- **Hollowmere (HLM)** — an original graveyard-themed set. See `MtgCore/Sets/Hollowmere/`.
- **Core Set Cube (CSC)** — built from an external cube list
  (https://cubecobra.com/cube/list/magiccoreset20xx). **White is complete — all 67 cards.** Other
  colours are not started. See `MtgCore/Sets/CoresetCube/`.

Sets sourced from a real cube exist to force new mechanics: the card list drives the engine rather
than the engine driving the list. When a card needs something the engine lacks, **build the
mechanic** — dropping the ability defeats the exercise. Only cut text when the concept is
structurally absent (no colours, no blocking, no planeswalkers), and comment the cut on the card.

## Serialization Rule

All objects stored in `GameState` must be fully serializable at all times. Delegates (`Func<>`, `Action<>`), lambdas, and expression trees are **forbidden** on any type that lives in `GameState`, including all `GameObject` and `GameAction` subclasses and their data. If a delegate seems necessary, make the case explicitly before implementing — there is almost always a data-oriented alternative.
