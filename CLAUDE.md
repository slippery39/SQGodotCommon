# DOOMJAM

**This repo's active project is DOOMJAM, a Godot Wild Jam entry. It is NOT the MTG game.**

**The game is called *ENDLING* to a player; DOOMJAM is the working title and the code prefix.** Only
`MainMenu.Title` and `project.godot`'s `config/name` face outward — do not rename the solution.

A solitaire roguelike deckbuilder: you kill an Opponent across five lanes while a doomsday scenario
fires on a repeating clock, reshaping the board or permanently rewriting your deck. **Read
`DoomJam.md` first** — it is the design doc and the source of truth.

The second goal is a measurement: **how hard is it to build a completely different card game on
`ImmutableGameObjects`?** Anything we wish we could lift out of `MtgCore` is a finding — record it in
`DoomJam.md` under "Engine findings" *before* reimplementing it.

## Solution Map

```
SQGodotCommon/
├── ImmutableGameObjects/
│   ├── ImmutableGameObjects/        # THE ENGINE — GameState, GameAction, PipelineAction. Game-agnostic.
│   ├── ImmutableGameObjects.Tests/
│   └── ImmutableGameObjects.Benchmarks/
├── DoomCore/                        # DOOMJAM rules engine — lanes, combat, the doom clock
│   ├── Actions/                     # StartBattle/StartTurn/PlayCard/EndTurn/ResolveDoom
│   ├── Ai/                          # DoomBot (plays a turn), RunSimulator (plays a run + records)
│   ├── Cards/ Components/           # DoomCard; UnitComponent (Power/Toughness/Damage/Lane)
│   ├── Enemies/ Zones/              # Enemy + telegraphed Intent; Opponent + PendingSummon; Zone
│   ├── Content/                     # ThemeLibrary (the acts + floor→doom SCHEDULE), Enemy/Scenario,
│   │                                #   KeywordLibrary (the glossary, as DATA — console and UI share it)
│   ├── Run/                         # Run + RunCard (OUTSIDE GameState); ActMap; ShopOffer; DoomTransform (the
│   │                                #   permanent-doom language: a read × a verb), DoomTransforms,
│   │                                #   DoomBattleEffects (battle scope), FloorKind, DoomPreview
│   ├── DoomFiring.cs                # what ONE firing read, in run ids. Capture is shared by the
│   │                                #   real firing and the preview, so they cannot disagree
│   ├── DoomScope.cs                 # Battle vs Permanent — a fixed property of each scenario
│   └── DoomBattleFactory.cs         # one GameState per battle; DoomStateExtensions is the API
├── DoomCore.Tests/                  # NUnit; inline card definitions only
├── DoomConsole/                     # terminal front end — THE REMOTE SURFACE, needs no Godot
│   ├── SimCommand.cs                #   `sim N` — balance tables out of DoomCore/Ai. PER-ACT first
│   └── ContentCommand.cs            #   `content` — every act, doom, enemy and card, read from source
└── SQGodotCommon/                   # Godot project
    ├── Common/                      # reusable utilities — Cards/2D is game-agnostic, use it
    ├── Project/                     # GameManager, main menu
    └── DoomGame/                    # DOOMJAM front end — reads DoomCore, decides nothing
        ├── DoomBoard.cs             #   the battle screen; layout contract lives in DoomUI.md
        ├── DoomLaneCell.cs          #   one lane slot: art on a plinth + attack/life marks
        ├── DoomHandView.cs          #   the fan, on Common/Cards/2D; drag -> PlayCardAction
        ├── DoomCardFace.cs          #   THE CARD'S LOOK, in one place. Hand, preview and rewards
        │                            #   all draw through it — two copies would drift in a day
        ├── DoomCardPreview.cs       #   doom_card_preview.tscn — a card rack with no battle behind
        │                            #   it, loading the cards that BREAK the layout
        ├── DoomCardInspector.cs     #   the hover panel: full rules text, then keywords
        ├── DoomAnimator.cs          #   float/pop/flash/shake on one Speed dial; F4 cycles it
        ├── DoomClockDial.cs         #   the doom clock as a ring of segments
        ├── DoomIntermission.cs      #   between floors: what the doom took, and the card rewards
        ├── DoomShop.cs              #   a shop floor: buy, patch up, and REMOVE a card
        ├── DoomArt.cs               #   art by NAME convention from Art/, else a generated figure
        ├── DoomPalette.cs           #   the five colours; gold and red are reserved
        └── Art/                     #   52 authored SVGs + background.png + icons/ (CC BY — see
                                     #   CREDITS.md). New files need `--headless --import` to exist
```

**The MTG projects (`MtgCore`, `MtgCore.Tests`, `MtgConsole`, `MtgSimulator`, `MtgSimulator.Console`,
`MtgSimulator.Tests`, `SQGodotCommon/MtgGame/`) are still in the tree but are OFF LIMITS.** This
project must stay a clean no-op for them. Copy nothing from them. Their docs are listed below so
nothing is orphaned — you should not need any of it.

## Where the detail lives

**This file holds only what is true on every task.** Everything else loads on demand:

| Kind | Location | Loads |
|---|---|---|
| Design doc — read first | `DoomJam.md` | read it |
| **The next thing to build — v3** | `DoomV3Plan.md` | read it before starting any v3 phase |
| Where the last session got to | `HANDOFF-DoomAndroidAndText.md` | read it when picking the work back up |
| Earlier handoff, superseded | `HANDOFF-DoomCombatV3.md` | read only for its scars (§4) |
| Earlier handoff, superseded | `HANDOFF-DoomVisualPass.md` | read only for its scars (§4) |
| Credits — **must ship** | `CREDITS.md` | before release, and when adding any third-party asset |
| Earlier handoff, superseded | `HANDOFF-DoomPacingAndRewards.md` | read only for its scars (§4) |
| Earlier handoff, superseded | `HANDOFF-DoomBalanceAndThemes.md` | read only for its scars (§4) |
| Earlier handoff, superseded | `HANDOFF-DoomFrontEndAndEffects.md` | read only for its scars (§4) |
| Earlier still, superseded | `HANDOFF-DoomBattleLoop.md` | read only for its scars (§4) |
| UI design — layout contract AND the visual language | `DoomUI.md` | read it before touching `SQGodotCommon/DoomGame/` |
| The mockup, and the backdrop prompt | `docs/mockups/` | when changing layout or generating art |
| Subsystem rules | `.claude/rules/*.md` | automatically, when you open a file the rule's `paths:` matches |
| Measured results | `docs/findings/*.md` | never — read when a change touches what a run measured |
| Balance, as measured | `docs/findings/doom-balance.md` | read before changing life, floors or rewards |
| Commands | `Commands.md` | read it |
| Deferred decisions | `DesignNotes.md` | read it |

Each project's `CLAUDE.md` is a map to its own rules — start there, not here.

**MTG-only docs, kept for the archive:** `docs/mtg/colours.md`, `docs/mtg/card-sets.md`,
`docs/mtg/ai-tooling.md`, `docs/mtg/measured-tables.md`, `MtgCore/CLAUDE.md`,
`MtgSimulator/CLAUDE.md`, and every `.claude/rules/mtg-*.md` and `sim-*.md`. All of those rule files
are path-gated to MTG directories, so none of them load on this project.

## Platform

C# on .NET. Windows.

## General Principles

- SOLID where applicable; no oversized files
- Functional or OOP as the problem dictates — not dogmatic about either
- **Game logic and UI logic stay completely separated; no game logic in presentation layers**
- Small, reviewable steps; never large all-at-once changes
- **Never assume on vague requirements — confirm before implementing**
- **Verify a primitive fires before relying on it.** An inert card throws no error; four silent no-op
  engine bugs were found only by testing the consequence, not the construction.
- **"It looks a bit off" is a bug report, and never about what it looks like.** Text that looked too
  small was empty; a cramped badge was showing the WRONG number; a panel whose title ran off screen
  was overflowing because of a label at its foot. Find the cause before fixing the resemblance.
- **Check `git status` before writing a file you believe is new.** A `cat >` over an existing
  `CompanionTests.cs` destroyed eight tests, and the only tell was the count going DOWN after tests
  were added. A wrong number looks exactly like a large one — read it, do not skim it.
- **Never report one balance number across more than one act.** Three acts at 58/13/1.5% averaged to
  24.2% against a 25% target — the aggregate reassured while two were unplayable. See
  `docs/findings/doom-balance.md`.
- **Player-facing text says the rule ONCE.** Cut any clause the player can derive from the clause
  before it — Rite said "not a body", "goes to the discard pile" AND "never holds a lane" for one
  fact. Keep a second clause only when it adds a rule nothing else states (Toughness' spillover
  does; Energy's "so it is wasted" does not). `ReminderTextStaysTight` holds the SHAPE — 20 words,
  2 sentences — because the derivable-clause rule itself is a judgement no test can make.
- **Tests read authored values, never restate them.** A literal copied out of content breaks on every
  balance pass while the code is right; twelve did at once.

## Serialization Rule

All objects stored in `GameState` must be fully serializable at all times. Delegates (`Func<>`,
`Action<>`), lambdas and expression trees are **forbidden** on any type that lives in `GameState`,
including all `GameObject` and `GameAction` subclasses and their data. If a delegate seems necessary,
make the case explicitly before implementing — there is almost always a data-oriented alternative.

This is not style. The MTG side enforced it with a JSON round-trip test that asserts a rebuilt state
scores identically and produces the same decisions; a **metadata value** of an untagged type throws
by design, naming the type and telling you to use a component instead. Whatever DOOMJAM needs to
serialize — and the run deck crossing battles means it will — the same constraint applies.

## Maintaining these files

When a change affects the architecture, patterns, rules or known issues described here — update the
relevant file **in the same step**. Source maps go stale fastest; keep them accurate as files are
added or removed.

**Route new content by what it is, not by where you happen to be:**

| What you are writing | Goes in |
|---|---|
| A rule that applies to every task | this file |
| Game design — mechanics, scenarios, scope | `DoomJam.md` |
| "here is where I left off, and what will bite you" | `HANDOFF-<Topic>.md` at the root |
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
