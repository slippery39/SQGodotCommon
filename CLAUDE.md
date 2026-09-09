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
│   ├── Scenarios/                   # Saved positions: GameState↔JSON, scenario store, multi-strategy comparison
│   └── Evolution/                   # Constructed metagame evolution: Decklist, seeding/mutation, the loop
├── MtgSimulator.Console/            # Thin console entry point (Program.cs only — references MtgSimulator)
└── SQGodotCommon/                   # Godot project — reusable utilities (Common/, Project/)
    └── MtgGame/                     # MTG front end: board, deck select, draft + tournament
```

The MTG game is played in `SQGodotCommon/MtgGame/`. It talks to the engine only through
`MtgGameManager` (plain C#, no Godot types) — see `MtgSimulator/CLAUDE.md` for the draft path.

## Seeing what the AI is doing

Evaluation changes were argued rather than watched for two sessions, and several were wrong. There
is now tooling; use it before proposing a scoring change.

| | |
|---|---|
| **F6** in game | AI inspector overlay — every ranked action, and the chosen one's score split into evaluator terms |
| **F7** in game | Save the live position to `user://scenarios/` |
| Console **mode 5** | Load a scenario and have several strategies decide in it, side by side |
| Console **mode 6** | Evolve a constructed metagame — AI-built decks, matchup matrix, and a constructed-vs-limited card value diff |
| Console **mode 7** | Discover synergy engines in a pool — solitaire only, no battles. "Does this archetype assemble?", asked before "is it competitive?" |
| **Space** in game | Pause the AI — do this before F6 so you can click through candidates |

Full detail in `MtgSimulator/CLAUDE.md`. Three rules worth carrying: a scenario is **serialized
state**, not a `GameStateSnapshot` report; `StateEvaluator.Explain` is the implementation with
`Evaluate` as the wrapper — never write a second copy of the sum for display; and **goldfish speed
is measured to be the wrong fitness for a combo deck** — dismantling Storm makes it goldfish
*faster*. Use `EngineProbe`, which asks whether the payoff resolved with its support deployed.

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

**The set menu is 1=LEG 2=CSC 3=CMB 4=DES 5=ALL.** It has now shifted TWICE — CMB was inserted,
then HLM was retired — so any piped console command written against either older numbering runs a
different set silently. Read the menu.

**Combo Proving Ground (CMB)** is a test instrument, not a draftable set — see
`MtgCore/Sets/ComboProving/`. It plants combos with known answers so a run can distinguish "the
builder cannot find combos" from "this pool has none", which is the discriminator the handoff names
as the most valuable open item. It is a **supplement meant to be played inside DES**, never alone:
at ~60 cards `DeckCore.MinPoolForBreadth` (100) switches the breadth gate off entirely, so a run
over CMB by itself measures the fixture. Nothing in it is costed to a rate — read cohesion and
assembly, never win rate.

**Hollowmere (HLM) was RETIRED** along with its cards, tests and trained model. It served its
purpose but was designed around the engine having no colours — "ten overlapping themes stand in for
colours" — and its tribes do not map onto five colours evenly, so colouring it would have meant
redesigning it. A new set built WITH colours from the start is the intended successor.

ONE DRAFTABLE set exists (CMB is registered but has no trained model and is not meant to be
drafted), registered in `SetRegistry`:
- **Core Set Cube (CSC)** — built from an external cube list
  (https://cubecobra.com/cube/list/magiccoreset20xx). **All five colours are complete — 335
  cards.** The colourless (50) and multicolour (53) sections are what remain. See
  `MtgCore/Sets/CoresetCube/`.

  The cube is 450 cards: 67 per colour, 50 colourless, 53 multicolour. Verify a colour's card list
  by downloading `cubecobra.com/cube/download/csv/magiccoreset20xx` and filtering on the `Color`
  and `board` columns — the HTML page is a SPA and cannot be scraped.

  **CSC is the set the Godot draft mode plays** (`DraftScene.DraftedSet`). Making a set playable
  is three things, not one: the cards, the rules text that renders them, and a trained draft
  model. See `MtgSimulator/CLAUDE.md` — a set with correct cards and no rules text is
  undraftable, and the failure is invisible in a screenshot.

Sets sourced from a real cube exist to force new mechanics: the card list drives the engine rather
than the engine driving the list. When a card needs something the engine lacks, **build the
mechanic** — dropping the ability defeats the exercise. Only cut text when the concept is
structurally absent (no colours, no blocking, no planeswalkers), and comment the cut on the card.

## Colours

Colour is a **second, independent mana track**. A land grants 1 generic AND its colours; a cost of
"1W" spends 1 generic and 1 White. The two never substitute for each other, so payment is fully
determined — no ordering choice, no solver, no manual tapping. **Coloured mana DEPLETES and refills
each turn, exactly like generic** — it is not an Eternal-style permanent threshold. That is the
whole design: a five-colour manabase caps you at one single-pip spell per colour per turn, so greed
costs throughput while focus costs nothing.

Engine detail lives in `MtgCore/CLAUDE.md`. Three things worth carrying:

- **`ManaBase.Build` (MtgSimulator) is the only place a manabase is made.** Draft, random pools and
  `Decklist.Materialize` all route through it. Every one of them used to pad with Plains, which was
  fine with no colours and silently fatal with them. It allocates by MEASURED DEMAND — sources
  needed to cast a card on the turn it costs — not by raw pip count.

**A double pip is effectively a mono-colour card, and that is where the whole colour constraint
lives.** Measured in this engine (`ManaBaseCalibrationTests`), sources of one colour needed in a
60-card, 24-land deck for a 90% on-curve cast:

| pips | cost 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|
| 1 | 13 | 12 | 11 | 10 | 9 | 9 |
| 2 | – | 18 | 17 | 17 | 15 | 15 |
| 3 | – | – | 22 | 22 | 21 | 20 |

A two-colour deck split 12/12 casts a single pip **89% on turn one and 94% by turn three**, but a
double pip only **65% by turn three** — 17 of its 24 lands would have to be one colour. So
single-pip greed is barely taxed and double pips carry the constraint. Expect `WW` cards to belong
to mono decks, and treat the pip depth of a card as its real colour commitment.

**Paper Magic's manabase tables do not transfer here** and must not be used. The opening hand is
guaranteed to contain exactly three lands (`SetupGameAction.OpeningHandLandCount`) drawn uniformly
from the manabase, which makes early colour access far more reliable than a real seven-card draw;
a borrowed table systematically over-builds. Re-run `ManaBaseCalibrationTests` if the opening-hand,
land-drop or deck-size rules ever change.
- **`CardValueSandbox` grants every colour** at the generic depth. A measurement table that handed
  out generic only would score coloured cards as uncastable and rewrite the value tables into a
  report about colour screw.
- **`MtgGameFactory.CreateForTesting` grants 99 of every colour.** A test about a mechanic should
  not fail on colour; a test about colour zeroes it explicitly (see `ManaColorTests`).

**Presim random decks are seeded to a COLOUR IDENTITY** (`ColorIdentity`, 5 mono + 10 pairs) and
sample only cards that identity can cast. This is not a refinement — it repairs a bias colour
introduced. A deck sampled across the whole pool is a five-colour pile: measured on CSC it plays
**4.8 colours and gives a card 5.8 sources of its own colour**, against **1.7 and 16.2** when
scoped. Against the table above that is a double pip castable on curve ~23% of the time versus
~86%, and 29% of a random deck's coloured cards are double-pipped. Unscoped, the presim would
deflate every committed card in the format — precisely the failure `ConstructedValuesStore`
documents, reached from a different direction, and its note that *"deflation makes an archetype
unbuildable"* is what makes it serious rather than cosmetic.

So **"neutral" now means random WITHIN a manabase that can cast the card**, not random across the
pool. Three-colour identities are deliberately excluded from the pooled table: their manabases fail
often enough that the games would measure the mana rather than the card.

### Colour slots

The evolver's field is **16 slots: the fifteen identities (five mono, ten pairs) plus one
unconstrained wildcard**, assigned by `DeckBuilder.IdentityForSlot`. The identity is a POOL LOCK —
a colour a deck cannot cast never enters its candidate list — which is the same mechanism
`DeckCore` uses and for the reason recorded there: *"a budget for drift gets spent on drift."*

**Enforced at mutation as well as at seeding.** A slot that seeds mono-red and then mutates freely
drifts out of its colours one swap at a time, and the field silently stops covering the format.

`ConstructedValues.For(identity)` returns a SCOPED view whose every lookup answers in that
identity's terms. `DeckBuilder` reads `CardDelta` from seven places; conditioning at the source
rather than per call site is what keeps fill and cut agreeing — one missed site would let the
builder add a card its own cut scoring then wants gone, churning the slot forever.

**The wildcard slot is the control**, not a curiosity: if a deck allowed any colours consistently
loses to the constrained slots, colour is doing real work; if it wins, the manabase model is too
generous.

`minDifference` is relaxed to **0.15** because colour now separates the field structurally — with
one slot per identity it cannot converge. The old 0.35 would actively fight the slots, since
mono-red and red-white legitimately share most of their red cards. Cost is quadratic in deck count,
so a 16-slot run is ~4x an 8-slot one; the field is also a colour-pair tier list for the format.

### The three win-rate tables

Card value is looked up in three places, weighed against the feature scores (`DeckFit`,
`SupportScore`) which are theoretical rather than win-rate based:

| table | what it answers | scope |
|---|---|---|
| `ConstructedValues.CardDelta(name)` | worth in a random deck of its colours | persisted |
| `CardDelta(name, identity)` — `IdentityValues` | worth in mono-red as against red-white | persisted |
| `contextValue` from `OutputProbe` | worth in THIS deck | run-scoped, discarded |

**The identity rate is shrunk TOWARD the pooled rate, never added to it.** They are one population
viewed two ways — every game in an identity's table is also in the pooled table — so summing would
count the same evidence twice and inflate exactly the cards that already have the most data.
Shrinking makes it a delta by construction: no identity games means the pooled answer, unchanged.

**Both persisted tables come only from PRESIM random decks.** Nothing from an evolved deck may
enter either, or the builder's own output feeds back into the values steering it —
`ConstructedValuesStore.EvolvedPathFor` records what that cost when it happened (Goblin Chieftain
+13.43 against an unconfounded +3.81; Thoughtcast −7.18 against +0.92, and *"deflation makes an
archetype unbuildable"*). The run-scoped table is allowed to see built decks precisely because it
is thrown away.

`IdentityValues.IdentityShrinkK` is **provisional and must be calibrated**, not guessed —
`PreSimulation.Run` prints the median and p10 games-per-(card, identity) cell for that purpose.
Guessing a games constant by analogy is how `MinPairGames` came to be 200 against a busiest pair of
166, silently disabling every synergy path while the tests passed.

**Assignment status: every live set is assigned and verified** — CSC (`CoresetCubeColorTests`),
LEG and CMB (`LegacyAndComboColorTests`). HLM was retired rather than coloured.

Artifacts and lands stay colourless on purpose: an artifact's real colour IS colourless, and a
land's colour is what it PRODUCES (`LandColorComponent`), not what it costs. CMB's counters package
is artifact creatures and so is colourless too.

**Beware name-anchored bulk edits.** The first pass at assigning LEG and CSC anchored insertion on
any quoted occurrence of a card's name, and card names appear in other cards' doc comments — so
pips landed on whatever card was defined next. Llanowar Elves came out needing UUBRG and Lotus
Bloom, a colourless artifact, needed UU, and every existing test still passed. Anchor on the
FACTORY CALL, and assert an expected pip table.

## Serialization Rule

All objects stored in `GameState` must be fully serializable at all times. Delegates (`Func<>`, `Action<>`), lambdas, and expression trees are **forbidden** on any type that lives in `GameState`, including all `GameObject` and `GameAction` subclasses and their data. If a delegate seems necessary, make the case explicitly before implementing — there is almost always a data-oriented alternative.

**This rule is now enforced rather than aspirational.** `MtgSimulator/Scenarios/StateJson.cs`
round-trips a whole `GameState` through JSON and `StateJsonTests` asserts the result scores
identically, offers the same legal actions, and produces the same AI decision. A delegate on a
`GameState` type breaks those tests instead of being discovered years later, and any new abstract
type is picked up automatically by reflection — but a **metadata value** of an untagged type
throws by design, naming the type and telling you to use a component instead.
