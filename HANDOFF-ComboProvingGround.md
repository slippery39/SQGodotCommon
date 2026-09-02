# Handoff — Combo Proving Ground: a pool with known answers, and what it exposed

**Read this, then `MtgSimulator/CLAUDE.md` §"A combo deck can be built correctly, measured correctly,
and STILL not assemble" and §"The pre-search LETHAL check".**

Supersedes nothing in `HANDOFF-DeckIdentity.md` — that document is still correct on cores, identity
and optimisation. This one is about the **instrument** built to test them.

State at handoff: **MtgCore 830/830, MtgSimulator 310/310, SQGodotCommon 146/146.**

**The set menu changed: `1=LEG 2=HLM 3=CSC 4=CMB 5=DES 6=ALL`.** Any older piped command passing 4
for DES now runs CMB silently. Read the menu.

---

## 1. The headline

A 21-card set with combos planted by hand — `CMB`, Combo Proving Ground — turns "can the builder
find combos?" from an argument into a measurement, because the right answer is known by
construction.

**It worked, and the controlled negative worked too.** In an 18-deck, 12-generation run:

| combo | requirement expressible as a card filter? | outcome |
|---|---|---|
| **Twin** (untap + copy) | yes — the copier reads *"target Illusionist you control"* | found, seeded, **all 16 combo cards intact after 12 generations, 73.5%, 3rd of 18** |
| **Drain** (Sanguine Bond + Exquisite Blood) | no — the second half wants an event the OPPONENT produces | half-built; the partner appears **zero times in the entire field**, in two independent runs |

Same pool, same run, same machinery. The only difference is whether the requirement is a filter.
That is the pool-limitation-vs-builder-failure discriminator §7(a) of the last handoff asked for,
delivered as a fixture with a known answer rather than as a metric.

---

## 2. What shipped

### Engine primitives (MtgCore)

| Piece | Why |
|---|---|
| `UnexhaustCreatureAction` | Until this, **only `StartTurnAction` could clear `IsExhausted`** — every untap combo was structurally unwritable. Respects `CreatureComponent.IsFrozen` (shared with the untap step); does NOT clear `HasAttacked` — untapping is not vigilance |
| `CreateTokenCopyAction` | `CopyOnEnterComponent` copies the highest-power creature globally, so a combo could not name the card it runs on. This copies the RESOLVED TARGET, which is also what makes the archetype discoverable |
| `AbilityActivatedEvent` | Activating an ability named the card in **no event at all**, so any activated-ability engine was invisible to anything reading a game log. Three sites: record, `EventTypeNames`, `ExtractSubjectId` |
| `RemovePlusOneCounterAdditionalCost` | The existing counter cost spends CHARGE counters — wrong resource. As an EFFECT instead of a cost the Ballista would be free repeatable damage |
| `CountCardsWithSubtypeAction.CountedIdsOutputKey` | Mass targeting cannot reach inside a `PipelineAction`, so a Craterhoof needs number and target list from one scan |
| `CreatureComponent.IsFrozen` | One freeze rule, shared, so untap and the untap step cannot drift |

### Builder and discovery fixes (MtgSimulator)

1. **Causal supply density gate.** The movement rule credited any card moving anything into a
   demand's zone, so *"a Goblin in your hand"* had a **72-card** enabler slot — every draw spell.
   Now a blind mover needs `1 - (1-density)^moved >= 0.5`, or to be DIRECTED (its own data names the
   demand). Goblin Lackey's enabler slot vanished; reanimator's stayed **42, byte-identical**.
2. **`IsControlScoped`.** A battlefield spec reading *"target X **you control**"* was classified with
   Lightning Bolt and discarded, so the Twin combo harvested **zero** demands. Controlled-by-you is
   what un-shares the battlefield; decided empirically by placing the same card on both battlefields.
   Cost: demands 63→68, cores 77→82. **+5, because demands dedupe by value pool-wide.**
3. **`EngineDiscovery.StableHash`.** Seeding used `StringComparer.Ordinal.GetHashCode`, randomised
   **per process** — the second time this bug has appeared here. Mode 7 was not reproducible at a
   fixed seed: LIFT read +15.0 then 0.0 for one concept across two runs.
4. **`EngineProbe.Summarise` medianed depth over ALL games**, including non-assembling zeroes, so
   below 50% assembly `MedianDepth` — and therefore LIFT — was pinned to exactly 0. **22 of 22
   engines under 50% read depth 0; 0 of 20 above it did.** Engines at depth 0: 22/42 → **1/43**.
5. **`IsExecution` gained `AbilityActivatedEvent`**, so an activated-ability payoff is credited when
   the ability FIRES rather than when the creature lands.

### The AI fix that mattered most

**`MultiTurnBeamSearchAiStrategy.FindLethalAttacks`** — a pre-search pass: if effective power ≥
opponent life, simulate legal attacks until they die and commit the sequence if they do.

`FindWinner` could not cover it. It fires only when a node's ROLLOUT reached a win, and the rollout
completes our turn with `PlayGreedyTurn`, which plays a land, **exactly one** other action, then ends
the turn — so a kill needing six swings is never simulated. `SimulateOpponentTurn` LOOPS every attack
for the opponent, so the model gave them a whole turn and us one action.

| | before | after |
|---|---|---|
| Twin deck activations per game | **198** | 10–16 |
| Game end | `ActionLimitReached` (a draw) | `Damage`, **win turn 3–4** |
| Mode 7 `kill` | **99.0 (never)** | **3.5** |
| MtgSimulator suite runtime | 1m38s | **28s** |

`lethalCheck: false` restores the old behaviour for `EvaluatorStrengthTests`. **That measurement has
not been run** — the case for shipping is a fixed defect (a held win never taken), not a win rate.

### Card faces

`RemovePlusOneCounterAdditionalCost` rendered nothing, so the Ballista read **"Fling Spore (free):
Deal 1 damage"** — unlimited free damage on the face, the same bug `DescribeCost` already documents
one type earlier for Dragon's Hoard. And `LoseLifeAction` with a context amount printed **"loses 0
life"**, describing half of a two-card kill as doing nothing.

---

## 3. The set

`MtgCore/Sets/ComboProving/` — 21 cards, five packages, **a supplement meant to be played inside DES**
(under 100 cards the breadth gate switches off entirely). Nothing is costed to a rate.

| Package | Cards | Tests |
|---|---|---|
| Twin | 4 | 2-card infinite loop; `LoopDetector` finds it in 2 actions and reports it lethal |
| Elves | 5 | critical-mass mana engine, no loop (readiness is conserved, never created) |
| Reanimator | 4 | Entomb is Legacy-only, so DES had no library→graveyard tutor and no cheap reanimation |
| Counters | 5 | modular needed no new mechanic at modular 1 |
| Drain | 3 | the deliberate negative control |

**No CMB card reuses a name from another set** — `SetRegistry` is last-registered-wins and CMB
registers last, so a shared name silently replaces the other set's card. Two tests guard it.

---

## 4. Measured

**18 decks, 12 generations, ~91 000 games, 71 minutes, 0 excluded.**

```
Control-R                       82.4%
Engine-Champion of the Parish   75.6%
Engine-Kilnmother Vess          73.5%   <- the planted Twin combo
Midrange-Q                      70.9%
Engine-Wirewood Symbiont        61.2%   <- the elf engine
...
Engine-Ajani's Pridemate        26.2%   NON-VIABLE (drain, half-built)
Engine-Mere-Storm                8.2%   NON-VIABLE
```

Twin deck after 12 generations of hill climbing: **4x Mirevale Deceiver, 4x Tidebinder Sprite, 4x
Twinflame Artisan, 4x Kilnmother Vess** — every combo card, plus five on-theme flex and 16 lands.

**Mere-Storm is the sharpest standing result: top discovery numbers in the pool (lift +21.0, 56%
cover) and 8.2% / 0.0% across two runs.** Assembly and competitiveness are different axes and LIFT is
not a proxy for win rate.

---

## 5. Mistakes made this session — read this section

**One pattern, seven instances: I predicted behaviour and wrote the prediction down before measuring
it, and was wrong most times.** Each prediction was plausible and each was cheap to check.

1. Predicted the harvester could not see count-based Elf payoffs (a `Subtype` string is not a
   `TargetSpecification`). **Wrong** — `PoolFeatures` documents that a string property named
   `Subtype` IS lifted into one. All four Elf payoffs build cores.
2. Predicted "counters" could not be a demand because the fixture never has counters on anything.
   **Wrong** — `HasPermanentPowerBonusSpecification` matches any permanent P/T modifier, 28 cards.
3. Predicted neither drain half would harvest a demand. **Half wrong** — a trigger CONDITION is
   itself a demand, so the Covenant builds a core off 12 lifegain suppliers.
4. Claimed the Twin bottleneck was "the two halves rarely coincide". **Wrong** — they coincide on
   turns 2–4 routinely; the deck was looping itself into a draw.
5. Wrote a test asserting the Ballista's face contained `"counter"`. It **passed while the bug was
   live**, matching the card's other line. A vacuous test, the fourth in this file's history.
6. Framed the lethal gap as "a counterexample to a design decision" rather than a bug. It was a bug.
7. Hand-rolled an inert seat returning `new EndTurnAction()`; every game threw. Take actions **from
   the generator**, never construct them.

**The rule: measure first, then write the sentence.** A prediction recorded in a source comment
becomes something the next reader reasons FROM, which is how the "documented mechanism that does not
exist" trap gets made.

---

## 6. Next steps — the user's notes, with what is already known

### (a) Prune dead engines across runs; do not fill slots that have no viable engine

A flag to start fresh or continue from previous data, with known-bad archetypes excluded. Mere-Storm
is the worked example: 0.0% and 8.2% in two runs, and every other deck's best matchup was "vs
Mere-Storm", which **inflates the entire field's numbers**. Viability = a very low win rate against
the non-gauntlet field. If fewer viable engines exist than slots requested, leave the slots as curve
decks rather than seeding a punching bag.

Note the sampling window **scales with the request** — it read `top 18` at 6 engines and `top 48` at
16 — so a prune list changes which archetypes are reachable at every slot count.

### (b) Why the drain combo never appeared — diagnosed, not yet fixed

`Sanguine Reciprocity` triggers on `PlayerLostLifeEvent` filtered to opponents. `ProbeTriggers` plays
each pool card **solo**, and nothing a card does on its own makes the OPPONENT lose life in that
fixture — so the demand has no suppliers, is dropped as uninformative, and the card is in no core's
payoff or support slot. It cannot be selected by anything.

Two candidate fixes, the second more interesting:

- **Let the trigger probe hurt the opponent** — stock the fixture so opponent life can drop. Narrow,
  cheap, fixes this family only.
- **A produce/consume graph over EVENTS.** `CLAUDE.md` records that the produce/consume graph over
  membership predicates cannot find a combo, and that is still true — but Covenant PRODUCES
  `PlayerLostLifeEvent` and Reciprocity CONSUMES it. That edge is real, mechanical, and needs no card
  filter. This is the one structural direction that would reach combos joined by an event rather than
  by a shared card type.

### (c) Flex slots converge to midrange piles

`DeckCore.Complete` fills from the archetype's own pool and then from the format by `CardDelta`.
The Twin core's archetype pool is **4 cards**, so 40 slots come from the format ranked by standalone
value — good stuff by construction. The fill needs a synergy notion (demand satisfaction, or the
per-deck pair history `DeckHistory` already computes for CUTTING) rather than raw value.

### (d) Which elf payoffs got played, and why — measured, and the answer is two different things

The elf deck (61.2%) holds `4x Wirewood Herald` — the draw engine **did** get played. The others:

| card | in the deck | why |
|---|---|---|
| Wirewood Herald | **4x** | chosen |
| Wirewood Conduit (mana) | 0 | **considered but not valued** — it IS an Elf, so it is in the core's support pool; the fill ranks by standalone value and a 1/1 mana dork loses to a 3-mana 2/3 that draws |
| Timberwatch Elder | 0 | same |
| Hoofthunder Colossus | 0 | **never considered** — a Beast that harvests no demand, so it is outside the Elf core's pool entirely and the pool lock forbids it by design |

Those are different failures needing different fixes: the first is (c), the second is that a
finisher which shares no filter with its archetype is unreachable under a pool lock.

**"Considered but not valued" is only HALF an answer, and the other half is untested.** It was read
off the fill rule — flex ranks by standalone `CardDelta`, and a 1/1 mana dork loses to a 3-mana 2/3
that draws. That explains a card never being ADDED. It does not rule out the card being added early,
performing badly, and being CUT: `PickWeakest` scores with `DeckHistory`, which is per-deck win-rate
feedback, so a creature that dies before it does anything gets marked weak and removed. **Those are
different mechanisms with different fixes** — one is a selection-heuristic problem, the other is a
survivability problem — and the evolved report only shows the FINAL deck, so nothing measured here
distinguishes them.

The cheap discriminator is a per-generation trace of one engine slot's decklist: if Conduit appears
at generation 1 and is gone by 4, it was cut, not overlooked.

**Survivability is worth checking directly, and the card data does NOT obviously implicate it.** All
four CMB Elves carry **Cover 10**, verified from the rendered faces — including both that went
unplayed:

```
Wirewood Conduit    (Elf) 1/1   Cover 10       <- unplayed
Timberwatch Elder   (Elf) 1/1   Cover 10       <- unplayed
Wirewood Symbiont   (Elf) 1/1   Cover 10       <- played 4x
Wirewood Herald     (Elf) 1/1   Cover 10       <- played 4x
Hoofthunder Colossus (Beast) 5/5  no Cover     <- outside the pool entirely
```

Cover does not separate the played from the unplayed, so on this evidence it is not the cause. But
the CSC Elves that filled the rest of the deck (Elvish Mystic, Llanowar Elves, Dwynen's Elite) have
no Cover at all, and a mana engine whose supporting bodies die is a real failure mode that
`EngineProbe` cannot see — it reads assembly, not attrition. Two things worth testing:

- **Does Cover actually work in a contested game?** It burns down on its controller's turn and is
  spent the moment the creature attacks. In an aggressive matchup an Elf that ever swings loses it
  immediately. There is no test for Cover under real pressure.
- **Is the elf engine losing its board rather than failing to assemble?** The goldfish opponent is
  inert and never attacks, so mode 7 is structurally blind to this. Mode 6 is where it would show,
  and the instrument would be the deck's per-generation card history rather than any current report.

### (e) A gauntlet of hand-built expected decks

**Mode 6 already supports this** — the `gauntlet` prompt (games per reference deck, default 0 = off)
plays the field against `DeckRegistry` decks. Add hand-built Twin, Elves and Reanimator lists and
read the gap: gauntlet decks overperforming means the builder still has work; even or slightly
behind means it is doing its job. This is the cheapest high-value item on the list and needs no new
machinery, only decklists.

### (f) Better slot picking

Still open, and (c), (d) and (f) are arguably one problem: everything downstream of the core ranks by
standalone card value, which is exactly the pressure that produces good stuff.

### (g) Colours

Cards have no colour at all, so any card can go in any deck at no cost. A real deckbuilding
constraint would force decks apart and make an off-theme splash cost something. This is a large,
genuinely interesting change — it touches `Card`, the mana system, `Decklist` validity and every
existing measurement — and it would invalidate the trained draft models. Worth costing in
`DesignNotes.md` before anything else.

---

## 7. Run commands

```bash
# export it on its own line — `MTG_MIN_LANDS=12 printf … | dotnet run` sets it for printf.
export MTG_MIN_LANDS=12

# Set menu: 1=LEG 2=HLM 3=CSC 4=CMB 5=DES 6=ALL.  CMB was inserted; 4 is no longer DES.

# Mode 7 — discover cores.  mode, depth, set, games/engine, highlight, seed
printf '7\n\n5\n10\n10\nseedword\n' | dotnet run --project MtgSimulator.Console -c Release

# Mode 6 — evolve.  Count the prompts; this list has been wrong twice.
# mode, depth, set, decks, gens, mutants, games, finalGames, minDiff, presim,
# cull, draftPrior, conceptSlots, gauntlet, enginesPath, engineSlots, seed
printf '6\n\n5\n18\n12\n3\n6\n20\n0.35\n0\nY\nY\n0\n0\n<engines.json>\n16\nseedword\n' \
  | dotnet run --project MtgSimulator.Console -c Release

# ALWAYS regenerate the engine report after changing anything DeckCore writes.
# ALWAYS verify bin/ timestamps before trusting a run.
```

**Reference cost:** 18 decks × 12 generations ≈ 91 000 games ≈ 71 minutes. Round-robin scales with
decks², so 10 decks is ~19 minutes for the same generation count.

**Diversity at 18 decks read 27% for all 12 generations, below the 35% floor.** The floor gates
mutant ACCEPTANCE and does not constrain seeding, so 16 engines drawn from 43 cores pull in
overlapping archetypes (Echo of the Drowned has 232 cards in pool, Champion of the Parish 132). Read
field-level spread and diversity at high engine counts as an artifact of the request, not of deck
quality — the per-deck results are unaffected.

Diagnostics added this session, all `[Explicit]`:
`ComboProvingDiscoveryTests.DumpWhatTheHarvesterSeesOnTheComboPieces` (what each combo card
declares), `.DumpDemandAndCoreCounts` (the before/after instrument for any harvest-rule change),
`TwinComboGoldfishDiagnostic.DumpWhatHappensInARealGame` (turn-by-turn timeline of a deck
goldfishing), `ComboProvingRulesTextTests.DumpEveryCardFace`.
