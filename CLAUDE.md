# KIN

**This repo's active project is KIN. It is NOT the MTG game.** It began as DOOMJAM, a Godot Wild
Jam entry; the jam is over and the game pivoted.

**`Kin` is the CODE PREFIX and a codename, deliberately not the player-facing title.** The title is
still `ENDLING` in `MainMenu.Title` and `project.godot`'s `config/name`, and it is expected to
change with the re-theme — the prefix is chosen to survive that. Those two constants are the only
things that face outward.

**A monster-companion deckbuilder (2026-09-23): up to 3 monsters ARE the board; the deck is the
TRAINER's. ONE FAMILY PER RUN, chosen at the start. Monsters act ONLY through cards (a first-attack
bonus each); no catching. NEW SHAPE (2026-10-02, not built): 3 random monsters from the start, bosses
EVOLVE one.** A run is playable (`kin_party.tscn`, `KinCore/Party/`), built BESIDE the lane game. **Read `KinJam.md` first** — its newest section, at the top, is the truth; the
design philosophy is there too. The setting is a GENERIC FANTASY substrate (`KinSettingSketches.md`).
The lane game's history (the deleted doom layer, companion upgrades) is in `docs/findings/kin-balance.md`.

The second goal is a measurement: **how hard is it to build a completely different card game on
`ImmutableGameObjects`?** Anything lifted or adapted out of `MtgCore` is a finding — record it in
`KinJam.md` under "Engine findings" as it is done.

## Solution Map

```
SQGodotCommon/
├── ImmutableGameObjects/
│   ├── ImmutableGameObjects/        # THE ENGINE — GameState, GameAction, PipelineAction. Game-agnostic.
│   ├── ImmutableGameObjects.Tests/
│   └── ImmutableGameObjects.Benchmarks/
├── KinCore/                         # KIN rules engine — lanes, combat, the run
│   ├── Actions/                     # StartBattle/StartTurn/PlayCard/EndTurn/WithdrawUnits/MoveCompanion
│   ├── Ai/                          # KinBot (plays a turn), RunSimulator (plays a run + records)
│   ├── Cards/ Components/           # KinCard; UnitComponent (Power/Toughness/Damage/Lane)
│   ├── Enemies/ Zones/              # Enemy + telegraphed Intent; Opponent + PendingSummon; Zone
│   ├── Content/                     # ThemeLibrary (the three acts and their bosses), EnemyLibrary,
│   │                                #   StarterContent (cards + the COMPANION ROSTER; its
│   │                                #   .Archetypes.cs holds each companion's archetype cards),
│   │                                #   KeywordLibrary (the glossary, as DATA — console and UI share it)
│   ├── Run/                         # Run + RunCard (OUTSIDE GameState); Companion; ActMap; ShopOffer
│   ├── Party/                       # THE COMPANION GAME — PartyState API; PartyWorld.Tiers; PartyMonsters; PartyExams + PartyBosses; PartyRelics; PartyCards + Discard/Spells/Surge/Summon/Triggers; PartyEmber + EmberCards; PartyGrove + GroveCards
│   └── KinBattleFactory.cs          # one GameState per battle; KinStateExtensions is the API
├── KinCore.Tests/                  # NUnit; inline card definitions only
├── KinConsole/                     # terminal front end — THE REMOTE SURFACE, needs no Godot
│   ├── SimCommand.cs                #   `sim N` — balance tables out of KinCore/Ai. PER-ACT first
│   └── ContentCommand.cs            #   `content` — every act, enemy and card, read from source
└── SQGodotCommon/                   # Godot project
    ├── Common/                      # reusable utilities — Cards/2D is game-agnostic, use it
    ├── Project/                     # GameManager, main menu
    └── KinGame/                    # DOOMJAM front end — reads KinCore, decides nothing
        ├── KinPartyBoard.cs        #   THE COMPANION GAME — kin_party.tscn; + Cell, Inspector, RunScreens(.Map)
        ├── KinBoard.cs             #   the battle screen; layout contract lives in KinUI.md
        ├── KinLaneCell.cs          #   one lane slot: art, attack/life marks, TRAIT strip
        ├── KinHandView.cs          #   the fan, on Common/Cards/2D; drag -> PlayCardAction
        ├── KinCardFace.cs          #   THE CARD'S LOOK, in one place. Hand, preview and rewards
        │                            #   all draw through it — two copies would drift in a day
        ├── KinCardPreview.cs       #   kin_card_preview.tscn — the cards that BREAK the layout
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
`MtgSimulator.Tests`, `SQGodotCommon/MtgGame/`) are a TESTED LIBRARY TO REUSE — look there FIRST
(Shayne, 2026-09-24/25).** This repo branched off MTG precisely so KIN could reuse what MTG already
built and tested. **Copying MTG code into KIN, the engine or `Common/` is EXPECTED, not a finding to
avoid:** triggers, choices, discard, effects, targeting, AI search, card and hand UI. Before building
any mechanic, grep MTG for it; copy or adapt the tested version rather than writing a new one, and
say which MTG file it came from. The one limit: **do not MODIFY the MTG projects** — they must still
build and behave the same, and names lifted into the engine must not collide with theirs
(`Trigger`, not `TriggeredAbilityComponent`). A 2026-09-12 rule said "copy nothing"; it was wrong,
and it cost this project weeks of re-implementing tested code. Any older doc that says otherwise is
superseded by this paragraph.

## Where the detail lives

**This file holds only what is true on every task.** Everything else loads on demand:

| Kind | Location | Loads |
|---|---|---|
| Design doc — read first; lessons for ANY deckbuilder | `KinJam.md`; `docs/design-principles.md` | read both |
| **The setting — GENERIC FANTASY substrate; themes swappable on top** | `KinSettingSketches.md` | read it before naming any content |
| **The next design — FAMILIES: monsters as engines, cards as fuel (interview 2026-09-27)** | `KinFamiliesPlan.md` | read it before designing any monster or card; the Relay's plan is `KinRelayPlan.md` |
| **The ENEMIES — the curve, region rosters, junk cards, debuffs, the foe ideas (2026-10-02)** | `KinEnemiesPlan.md` | read it before designing or building any foe |
| **The next loop — towns and wild routes as interactive MAPS (planned 2026-09-26)** | `KinMapPlan.md` | read it before touching the run's structure or its screens |
| Where the last session got to | `HANDOFF-KinFamiliesBuilt.md` | read it when picking the work back up |
| Earlier handoffs, superseded — every other `HANDOFF-Kin*.md` | root | read only for their scars section |
| Credits — **must ship** | `CREDITS.md` | before release, and when adding any third-party asset |
| UI design — layout contract AND the visual language | `KinUI.md` | read it before touching `SQGodotCommon/KinGame/` |
| **The look — STYLE D chosen (2026-09-26): requirements, mockups, the sprite recipe** | `KinVisualDesign.md`; mockups and prompts in `docs/mockups/` | read it before any visual work; it overrides `KinUI.md`'s style |
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

**EXPLORING or TUNING — name which one a task is, and spend accordingly (Shayne, 2026-09-22).**
- **Exploring** (now): design and build to find what is fun. Tests prove a mechanic FIRES — always,
  they are cheap and never go stale. **No sims**: every design change invalidates the last one, and
  a session spent 50 minutes of sim on numbers the next change deleted. Broken numbers are fine.
- **Tuning** (later, when Shayne calls it): the design holds still, and `sim` decides numbers per act.
- Judge by principle instead. **Repeatable healing is too strong** in a game about keeping life
  across a run: *does a longer battle pay this ability more?* If yes, it is a stall engine.

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
