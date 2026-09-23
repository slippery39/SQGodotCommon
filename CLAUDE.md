# KIN

**This repo's active project is KIN. It is NOT the MTG game.** It began as DOOMJAM, a Godot Wild
Jam entry; the jam is over and the game pivoted.

**`Kin` is the CODE PREFIX and a codename, deliberately not the player-facing title.** The title is
still `ENDLING` in `MainMenu.Title` and `project.godot`'s `config/name`, and it is expected to
change with the re-theme — the prefix is chosen to survive that. Those two constants are the only
things that face outward.

A solitaire roguelike deckbuilder: you kill an Opponent across five lanes, with one COMPANION on the
board free every battle whose ability is what the deck is built around. **Read `KinJam.md` first** —
it is the design doc and the source of truth.

> **THE DOOM LAYER WAS DELETED (2026-09-21) and the top-down theme is being replaced.** Scenarios,
> transforms, the clock, the per-theme doom schedule and ~1,300 lines with them. The new setting
> is a GENERIC FANTASY substrate (`KinSettingSketches.md`); the grimy card names, enemies and 56
> SVGs are all still to be re-themed. **The design philosophy is at the top of `KinJam.md` — every
> card and enemy must create a decision; each companion is an archetype.**
>
> **The deleted dooms WERE the power curve. COMPANION UPGRADES replace them** — three offered every
> second cleared floor, take one, on `StarterContent.UpgradePool`. That took act completion from
> 0.0% back to 23.0% against a 25% target, with per-act clear rates of 68.5% / 60.6% / 55.4%.
> `StarterContent.FloorsPerUpgrade` is the dial and it is violently non-linear — every floor
> measured 82.5%, every third 1.5%. See `docs/findings/kin-balance.md`.

The second goal is a measurement: **how hard is it to build a completely different card game on
`ImmutableGameObjects`?** Anything we wish we could lift out of `MtgCore` is a finding — record it in
`KinJam.md` under "Engine findings" *before* reimplementing it.

## Solution Map

```
SQGodotCommon/
├── ImmutableGameObjects/
│   ├── ImmutableGameObjects/        # THE ENGINE — GameState, GameAction, PipelineAction. Game-agnostic.
│   ├── ImmutableGameObjects.Tests/
│   └── ImmutableGameObjects.Benchmarks/
├── KinCore/                         # KIN rules engine — lanes, combat, the run
│   ├── Actions/                     # StartBattle/StartTurn/PlayCard/EndTurn/WithdrawUnits
│   ├── Ai/                          # KinBot (plays a turn), RunSimulator (plays a run + records)
│   ├── Cards/ Components/           # KinCard; UnitComponent (Power/Toughness/Damage/Lane)
│   ├── Enemies/ Zones/              # Enemy + telegraphed Intent; Opponent + PendingSummon; Zone
│   ├── Content/                     # ThemeLibrary (the three acts and their bosses), EnemyLibrary,
│   │                                #   StarterContent (cards + the COMPANION ROSTER; its
│   │                                #   .Archetypes.cs holds each companion's archetype cards),
│   │                                #   KeywordLibrary (the glossary, as DATA — console and UI share it)
│   ├── Run/                         # Run + RunCard (OUTSIDE GameState); Companion; ActMap;
│   │                                #   ShopOffer; FloorKind
│   └── KinBattleFactory.cs          # one GameState per battle; KinStateExtensions is the API
├── KinCore.Tests/                  # NUnit; inline card definitions only
├── KinConsole/                     # terminal front end — THE REMOTE SURFACE, needs no Godot
│   ├── SimCommand.cs                #   `sim N` — balance tables out of KinCore/Ai. PER-ACT first
│   └── ContentCommand.cs            #   `content` — every act, enemy and card, read from source
└── SQGodotCommon/                   # Godot project
    ├── Common/                      # reusable utilities — Cards/2D is game-agnostic, use it
    ├── Project/                     # GameManager, main menu
    └── KinGame/                    # DOOMJAM front end — reads KinCore, decides nothing
        ├── KinBoard.cs             #   the battle screen; layout contract lives in KinUI.md
        ├── KinLaneCell.cs          #   one lane slot: art, attack/life marks, TRAIT strip
        ├── KinHandView.cs          #   the fan, on Common/Cards/2D; drag -> PlayCardAction
        ├── KinCardFace.cs          #   THE CARD'S LOOK, in one place. Hand, preview and rewards
        │                            #   all draw through it — two copies would drift in a day
        ├── KinCardPreview.cs       #   kin_card_preview.tscn — a card rack with no battle behind
        │                            #   it, loading the cards that BREAK the layout
        ├── KinCardInspector.cs     #   the hover panel: full rules text, then keywords
        ├── KinAnimator.cs          #   float/pop/flash/shake on one Speed dial; F4 cycles it
        ├── KinCompanionSelect.cs    #   RUN START — pick the companion. The only build declaration
        ├── KinIntermission.cs       #   between floors: the deck diff, and the card rewards
        ├── KinShop.cs              #   a shop floor: buy, patch up, and REMOVE a card
        ├── KinArt.cs               #   art by NAME convention from Art/, else a generated figure
        ├── KinPalette.cs           #   the five colours; gold and red are reserved
        └── Art/                     #   56 authored SVGs + background.png + icons/ (CC BY — see
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
| Design doc — read first | `KinJam.md` | read it |
| **The setting — GENERIC FANTASY substrate; themes swappable on top** | `KinSettingSketches.md` | read it before naming any content |
| **The next thing to build — v3** | `KinV3Plan.md` | read it before starting any v3 phase |
| Where the last session got to | `HANDOFF-KinPivot.md` | read it when picking the work back up |
| Earlier handoff, superseded | `HANDOFF-KinCardsAndFlood.md` | read only for its scars (§4) |
| Earlier handoff, superseded | `HANDOFF-KinAndroidAndText.md` | read only for its scars (§3) |
| Earlier handoff, superseded | `HANDOFF-KinCombatV3.md` | read only for its scars (§4) |
| Earlier handoff, superseded | `HANDOFF-KinVisualPass.md` | read only for its scars (§4) |
| Credits — **must ship** | `CREDITS.md` | before release, and when adding any third-party asset |
| Earlier handoff, superseded | `HANDOFF-KinPacingAndRewards.md` | read only for its scars (§4) |
| Earlier handoff, superseded | `HANDOFF-KinBalanceAndThemes.md` | read only for its scars (§4) |
| Earlier handoff, superseded | `HANDOFF-KinFrontEndAndEffects.md` | read only for its scars (§4) |
| Earlier still, superseded | `HANDOFF-KinBattleLoop.md` | read only for its scars (§4) |
| UI design — layout contract AND the visual language | `KinUI.md` | read it before touching `SQGodotCommon/KinGame/` |
| The mockup, and the backdrop prompt | `docs/mockups/` | when changing layout or generating art |
| Subsystem rules | `.claude/rules/*.md` | automatically, when you open a file the rule's `paths:` matches |
| Measured results | `docs/findings/*.md` | never — read when a change touches what a run measured |
| Balance, as measured | `docs/findings/doom-balance.md` | read before changing life, floors or rewards |
| Commands — **run scenes via `Run-Godot.ps1`** | `Commands.md` | read it |
| Deferred decisions | `DesignNotes.md` | read it |

Each project's `CLAUDE.md` is a map to its own rules — start there, not here.

**MTG-only docs are inventoried in `docs/mtg/README.md`.** All are path-gated to MTG directories,
so none load here.

## Platform

C# on .NET. Windows.

## THE GAME IS IN AN EXPLORATORY PHASE — this outranks every design note below

**No mechanic is settled. Until Shayne says otherwise, every design rule here is a NOTE FROM A PAST
SESSION, not a constraint on the next idea.** The doom was "the core hook" and is deleted; marks
were "the record of your run" and are cut. Both were written as firmly as anything still standing.

**Do not answer a design idea with a quotation.** Say what it would COST — which code it touches,
which measurement it invalidates, what it trades away — then help build it if that is the call.
"`KinJam.md` says lane choice is the only decision" is not an argument; "three companions leaves two
open lanes, and here is what that does to the reward screen" is. A past playtest note is evidence,
never a veto. **How the companion should work is the most open question in the project.**

The card pass's own standing rule, promoted here from a superseded handoff: *we are exploring what
is fun, not enforcing what is written. Breaking a rule on purpose is a design decision, not an
error.*

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
| Game design — mechanics, scenarios, scope | `KinJam.md` |
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

**A rule file fails SILENTLY when its frontmatter or globs are wrong** — it simply never loads,
with no error anywhere. Both known causes, and the way to VERIFY a rule loads rather than assume it,
are in `docs/authoring-rules.md`. Read it before adding or editing anything in `.claude/rules/`.
