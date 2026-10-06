# SQGodotCommon Solution

## Solution Map

```
SQGodotCommon/
├── ImmutableGameObjects/
│   ├── ImmutableGameObjects/        # Immutable game state library (GameState, GameAction, PipelineAction)
│   ├── ImmutableGameObjects.Tests/
│   └── ImmutableGameObjects.Benchmarks/
├── MtgCore/                         # MTG card game engine (actions, cards, turns, combat, targeting)
├── MtgCore.Tests/
├── MtgConsole/                      # Console presentation layer (ConsoleGameLoop, ConsoleRenderer)
├── MtgSimulator/                    # AI strategies, runners, deck factories (referenced by Godot + tests)
│   ├── Scenarios/                   # Saved positions: GameState↔JSON, scenario store, strategy comparison
│   └── Evolution/                   # Constructed metagame evolution + engine discovery
├── MtgSimulator.Console/            # Thin console entry point (Program.cs only)
└── SQGodotCommon/                   # Godot project — reusable utilities (Common/, Project/)
    └── MtgGame/                     # MTG front end: board, deck select, draft + tournament
```

The MTG game is played in `SQGodotCommon/MtgGame/`. It talks to the engine only through
`MtgGameManager` (plain C#, no Godot types).

## Where the detail lives

**This file holds only what is true on every task.** Everything else loads on demand:

| Kind | Location | Loads |
|---|---|---|
| Subsystem rules | `.claude/rules/*.md` | automatically, when you open a file the rule's `paths:` matches |
| Measured results | `docs/findings/*.md` | never — read when a change touches what a run measured |
| Procedures | skills (`/add-card`, `/ai-inspect`, `/regen-card-values`; art and look: `/match-mockup`, `/generate-art`, `/add-icon`) | when invoked or relevant |
| Commands — **run Godot scenes via `Run-Godot.ps1`** | `Commands.md` | read it |
| Deferred decisions | `DesignNotes.md` | read it |
| Lessons for a ROGUELIKE deckbuilder (from KIN) | `docs/roguelike-deckbuilder/` | before designing a game of that kind |

Each project's `CLAUDE.md` is a map to its own rules — start there, not here.

## Platform

C# on .NET. Windows.

## General Principles

- SOLID where applicable; no oversized files
- Functional or OOP as the problem dictates — not dogmatic about either
- **Game logic and UI logic stay completely separated; no game logic in presentation layers**
- Small, reviewable steps; never large all-at-once changes
- **Never assume on vague requirements — confirm before implementing**
- **Verify a primitive fires before relying on it.** An inert card throws no error; four silent no-op engine bugs were found only by testing the consequence.
- **"It looks a bit off" is a bug report, never about the look.** Text that looked too small was empty; a cramped badge showed the WRONG number. Find the cause before fixing the resemblance.
- **Check `git status` before writing a file you believe is new.** A `cat >` over an existing test file destroyed eight tests; the only tell was the count going DOWN after tests were added.
- **Tests read authored values, never restate them.** A literal copied out of content breaks on every balance pass while the code is right; twelve did at once.

## Seeing what the AI is doing

Evaluation changes were argued rather than watched for two sessions, and several were wrong. **Use
the tooling before proposing a scoring change.**

| | |
|---|---|
| **Space** in game | Pause the AI — do this *before* F6 so you can click through candidates |
| **F6** in game | AI inspector overlay — every ranked action, and the chosen one's score by evaluator term |
| **F7** in game | Save the live position to `user://scenarios/` |
| Console **mode 5** | Load a scenario, have several strategies decide in it side by side |
| Console **mode 6** | Evolve a constructed metagame |
| Console **mode 7** | Discover synergy engines in a pool — solitaire only. "Does this archetype assemble?", asked before "is it competitive?" |

Three rules worth carrying: a scenario is **serialized state**, not a `GameStateSnapshot` report;
`StateEvaluator.Explain` is the implementation with `Evaluate` as the wrapper — never write a second
copy of the sum for display; and **goldfish speed is measured to be the wrong fitness for a combo
deck** — dismantling Storm makes it goldfish *faster*. Use `EngineProbe`.

## Card Sets

**The set menu is 1=LEG 2=CSC 3=CMB 4=DES 5=ALL.** It has shifted TWICE — CMB was inserted, then HLM
retired — so any piped console command written against either older numbering runs a different set
silently. **Read the menu; never hardcode the index.**

One draftable set exists: **Core Set Cube (CSC)**, 335 cards, what the Godot draft mode plays. **CMB**
(Combo Proving Ground) is a registered test instrument with no trained model, meant to be played
inside DES rather than alone. **HLM is retired.** Per-set detail is in `.claude/rules/mtg-cards.md`.

## Colours

Colour is a **second, independent mana track**. A land grants 1 generic AND its colours; a cost of
"1W" spends 1 generic and 1 White. The two never substitute for each other, so payment is fully
determined — no ordering choice, no solver, no manual tapping. **Coloured mana DEPLETES and refills
each turn, exactly like generic** — it is not an Eternal-style permanent threshold. That is the
whole design: a five-colour manabase caps you at one single-pip spell per colour per turn, so greed
costs throughput while focus costs nothing.

**A double pip is effectively a mono-colour card, and that is where the whole colour constraint
lives.** Measured in this engine (`ManaBaseCalibrationTests`) — sources of one colour needed in a
60-card, 24-land deck for a 90% on-curve cast:

| pips | cost 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|
| 1 | 13 | 12 | 11 | 10 | 9 | 9 |
| 2 | – | 18 | 17 | 17 | 15 | 15 |
| 3 | – | – | 22 | 22 | 21 | 20 |

A two-colour deck split 12/12 casts a single pip **89% on turn one and 94% by turn three**, but a
double pip only **65% by turn three** — 17 of its 24 lands would have to be one colour. So
single-pip greed is barely taxed and double pips carry the constraint. **Treat the pip depth of a
card as its real colour commitment**, and expect `WW` cards to belong to mono decks.

**Paper Magic's manabase tables do not transfer here and must not be used.** The opening hand is
guaranteed to contain exactly three lands (`SetupGameAction.OpeningHandLandCount`) drawn uniformly
from the manabase, which makes early colour access far more reliable than a real seven-card draw; a
borrowed table systematically over-builds. Re-run `ManaBaseCalibrationTests` if the opening-hand,
land-drop or deck-size rules ever change.

`ManaBase.Build` is the only place a manabase is made. `CardValueSandbox` grants every colour at the
generic depth — a table handing out generic only would score coloured cards as uncastable and
rewrite the value tables into a report about colour screw. `MtgGameFactory.CreateForTesting` grants
99 of every colour: a test about a mechanic should not fail on colour, and a test about colour zeroes
it explicitly (`ManaColorTests`).

Engine detail is in `MtgCore/CLAUDE.md`. How colour shapes the evolver's field, the three win-rate
tables and the identity-scoped presim are in `.claude/rules/sim-evolution.md`.

## Regenerating the measured tables

`sim_results/` is **untracked and disposable**, but four tests need the card-value table in it:

```
dotnet test MtgSimulator.Tests --filter "FullyQualifiedName~CardValueSweep.SweepTheCoreSetCube"
```

**`sim_results/` resolves against the WORKING DIRECTORY, which under `dotnet test` starts as the test
binary's folder, not the repository.** Anything reading or writing it must call
`TestPaths.ChdirToSolutionRoot()` first. Getting this wrong does not look like a path bug: the table
loads empty, every card reads 0.00pp, and the tests fail exactly as though the data were missing —
while passing in isolation, because alone nothing has moved the directory.

**Never quote a card's win rate from memory or from a CLAUDE.md — compute it from `sim_results/*.json`
every time.**

## Serialization Rule

All objects stored in `GameState` must be fully serializable at all times. Delegates (`Func<>`,
`Action<>`), lambdas and expression trees are **forbidden** on any type that lives in `GameState`,
including all `GameObject` and `GameAction` subclasses and their data. If a delegate seems necessary,
make the case explicitly before implementing — there is almost always a data-oriented alternative.

**This is enforced, not aspirational.** `MtgSimulator/Scenarios/StateJson.cs` round-trips a whole
`GameState` through JSON and `StateJsonTests` asserts the result scores identically, offers the same
legal actions, and produces the same AI decision. Any new abstract type is picked up automatically by
reflection — but a **metadata value** of an untagged type throws by design, naming the type and
telling you to use a component instead.

## Maintaining these files

When a change affects the architecture, patterns, rules or known issues described here — update the
relevant file **in the same step**. Source maps go stale fastest; keep them accurate as files are
added or removed.

**Route new content by what it is, not by where you happen to be:**

| What you are writing | Goes in |
|---|---|
| A rule that applies to every task | this file |
| A rule about one subsystem | `.claude/rules/<subsystem>.md` |
| Numbers a run produced | `docs/findings/<subsystem>.md` — never a `CLAUDE.md` |
| A command and its traps | `Commands.md` |
| A repeatable procedure | a skill under `.claude/skills/` |
| "Fine now, revisit later", with costed options | `DesignNotes.md` |

**Budget: 200 lines per `CLAUDE.md`.** A `PostToolUse` hook (`.claude/check-docs.py`) warns when one
goes over. Over budget means content is in the wrong file, not that the budget is wrong — move it
down, don't delete it. When a rule's evidence moves to `docs/findings/`, leave the rule and a
one-line pointer behind: the rule without its evidence gets re-litigated, and the evidence without
its rule never gets read.

**A rule file fails SILENTLY — it simply never loads, with no error anywhere.** Two ways, both
measured on Claude Code 2.1.116 and both now caught by the same hook:

- **CRLF in the YAML frontmatter.** The `paths:` block does not parse and the rule is inert. `.gitattributes` pins these files to LF; keep it that way.
- **`**` in a glob matches nothing.** `MtgCore/Sets/**/*.cs` matched zero files while `MtgCore/Sets/*/*.cs` matched 34. Write the levels out explicitly.

Verify a new or edited rule actually loads rather than assuming — same instinct as testing that a
card's effect fires:

```
claude -p "Read <a file the rule claims>. Then WITHOUT opening any other file: is the text of
.claude/rules/<rule>.md already in your context? Reply exactly LOADED or NOT LOADED." --allowedTools Read
```

Then repeat with a file the rule should NOT match — a rule that answers LOADED to everything has
lost its `paths:` and is costing context in every session.
