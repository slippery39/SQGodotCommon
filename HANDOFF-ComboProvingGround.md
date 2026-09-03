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
| **Drain** (Sanguine Bond + Exquisite Blood) | no — the second half wants an event the OPPONENT produces | half-built; the partner appears **zero times in the entire field**, in two independent runs. **Since fixed — see §6(b); the pair is now joined by the event one produces and the other consumes.** The field measurement above has NOT been re-run. |

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

### (a) Prune dead engines — DONE

`MetagameEvolver(excludedEngines:)`, console prompt *"Engines to exclude?"*, `EngineExclusionTests`.
Excluded before the tier cut (filtering afterwards would let a dead archetype eat a tier place),
unmatched names warned about by name, and the run prints its own next exclusion list from any engine
slot that finished below the viability floor.

"Leave the slots as curve decks" needed no code: `SeedField` fills every slot with a curve deck and
engines overwrite, so an excluded engine simply leaves the curve deck standing.

**Cross-run persistence was deliberately not built** — the prompt plus the printed line is the whole
feature. Note the sampling window **scales with the request** (`top 18` at 6 engines, `top 48` at 16),
so an exclusion list changes which archetypes are reachable at every slot count.

### (b) Why the drain combo never appeared — FIXED, and the diagnosis below was wrong twice over

**Two fixes, and the sentence below is the recorded diagnosis, kept so the correction is legible.**

> `Sanguine Reciprocity` triggers on `PlayerLostLifeEvent` filtered to opponents. `ProbeTriggers`
> plays each pool card **solo**, and nothing a card does on its own makes the OPPONENT lose life in
> that fixture — so the demand has no suppliers, is dropped as uninformative, and the card is in no
> core's payoff or support slot.

1. **The demand was never harvested**, so "no suppliers" was measuring the wrong thing.
   `IsObjectReferential` recurses into a trigger's `Filter`, and `IsControlledByOpponentSpecification`
   was disqualifying. That rule is right for *targeting* — the placement fixture puts nothing on the
   opponent's battlefield, so every removal spell would read as a dead card — and does not transfer
   to a trigger, which is asked against real events. Split into
   `TargetingOnlyObjectReferentialSpecs`.
2. **Solo probing is real but separate.** `PoolFeatures.ProbeChainedTriggers` replays each card
   alongside a supplier of a demand it asks, subject first so the igniter's event lands while the
   subject is out, and credits only what fires that the igniter alone does not.

**Measured on DES — each fix alone changes nothing:**

| | demands | informative | cores |
|---|---|---|---|
| before | 71 | 52 | 91 |
| chained pass alone | **71** | **52** | **91** |
| both | 78 | 53 | 93 |

Reciprocity's demand ends with four suppliers; **only the chain finds Covenant of Thorns** (the other
three drain on their own). `ComboProvingDiscoveryTests.TheDrainPairIsNowJoinedByTheEventOneProducesAndTheOtherConsumes`
and `ChainedTriggerProbeTests` pin both halves.

**This is section 5's pattern again, one level worse**: the prediction was written down, then
reasoned FROM by two later sessions as an established mechanism. An unharvested demand and a demand
with zero suppliers look identical from outside, which is what let it stand — the dump that settled
it took one minute.

**CORRECTED after reading the code: the two "candidate fixes" below are the same fix, and the cheap
one is a trap.**

- ~~**Let the trigger probe hurt the opponent**~~ — stocking the fixture so opponent life can drop
  makes *every card in the pool* a supplier of that demand, which then trips the uninformative
  filter (`supply[d].Count < pool.Count * UninformativeShare`, `PoolFeatures.Build` step 4) and drops
  the demand anyway. Same outcome, more code.
- **The real gap is a two-step chain.** Covenant only emits `PlayerLostLifeEvent` *after* something
  gains you life, and `ProbeTriggers` plays each card solo. So the fix is a **second `ProbeTriggers`
  pass seeded from the first**: replay each card with a known supplier of an already-harvested demand
  in play, and record the newly-fired triggers. Depth 2 covers a two-card combo.

That second pass *is* the produce/consume-over-events edge, reached from the cheap end — it needs no
new graph, because `ProbeTriggers` already asks each condition's `IsSatisfiedBy` against each card's
real event log. `CLAUDE.md`'s "no graph over this vocabulary can find a combo" still stands and is
about membership predicates; events are a different vocabulary.

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

**ANSWERED: never proposed — and NOT because of the pool lock.**

`MutationLog` (`sim_results/mutations_<set>_<stamp>.csv`) logs every proposal. Run: 10 decks x 12
generations on DES, 12 204 games, 6 engine slots from a fresh mode 7 report, Mere-Storm excluded,
`MTG_MIN_LANDS=12`, seed 7.

Wirewood Conduit appears in **zero rows**, and it **is** one of the 24 cards in the Wirewood Herald
core's pool — so the thing that excludes it is selection, not the lock. The elf slot got **7
proposals across 12 generations**, touching five distinct cards (Fauna Shaman, Elvish Archdruid,
Elvish Visionary, Llanowar Visionary, Yeva's Forcemage) out of 24 available.

So **"considered but not valued" was wrong as stated** — it was never considered, and survivability
is not implicated because the card was never in a deck to die. Both selection points rank by
standalone value, and ~7 proposals against a 24-card pool is not enough exploration for a 1/1 mana
dork to ever surface.

**The bigger finding came out of the same run, and it was not what anyone was looking for.**

| slot | core pool | real proposals | final |
|---|---|---|---|
| Engine-Watcher of the Spheres | 129 | **32** | 47.2% |
| Engine-Drogskol Captain | 54 | **33** | 46.1% |
| Engine-Master of the Wild Hunt | **2** | **3** | **26.1% NON-VIABLE** |
| Engine-Sanguine Reciprocity | **5** | **2** | **32.2% NON-VIABLE** |

`MutantsFor` gives a deck at or below 45% the FULL budget, and both non-viable slots sat there all
run — so each was offered ~36 attempts and used 3 and 2. **They were frozen at their seeded list for
twelve generations and then reported as non-viable archetypes.**

**RE-BASELINED after the fix — the verdicts held.** Same configuration re-run: the two frozen slots
got 22 and 26 real proposals and moved +0.6pp and +2.8pp, finishing NON-VIABLE again at 26.7% and
35.0%. They are genuinely weak in this field, not merely un-optimised, so excluding them is now a
supported decision. **The general rule still stands — read the `dry` column before excluding** — but
a slot with dry near zero has had its chance.

Cost of the fix: 12 204 games in 19.3 minutes became 16 956 in 31.2. More real proposals means more
games, which is what the budget always meant to buy.

**And (d) survived the better search.** The elf slot went from 7 proposals to 18, touching 9 distinct
cards instead of 5 — Wirewood Conduit still appears in **zero** rows. Not budget starvation: the
selection heuristic, which is items (c) and (f).

**Standing result worth carrying: the best deck in the field is a plain curve slot.** `Midrange-H`
finished at **77.2%**, clear of every discovered engine (next best 67.2%), on 4 real proposals — it
sat above `StableRate` almost throughout and was left alone by design.

**FOUND AND FIXED — and core pool size was not the only cause.**

`MutantsFor` returns "how many mutants to EVALUATE"; the loop spent it as "how many times to call a
function that often fails". `Mutate` rolls ONE operator and returns null when that operator cannot
produce a legal, distinct, core-holding, profile-respecting list, and the slot was consumed either
way. No retry.

Null rate per single call, measured (`MutationYieldTests`, 600 mutations per deck):

| condition | null rate |
|---|---|
| no core, profile `Any` | 3–9% |
| **`Control` profile, deck below its band** | **34%** |
| engine core, 508 cards in pool | 18% |
| engine core, **5** cards in pool | **95%** |

The curve profile is the cause nobody suspected: a deck below its band may only move toward it, so
roughly every curve-lowering mutation is discarded — an 11x multiplier with no pool lock involved.
That is what put `Control-C` at 0 real / 6 dry and made "narrow core" look like the whole story.

`TryMutate` re-rolls up to 20 times. Same short configuration before and after:

| slot | before | after |
|---|---|---|
| Engine-Sanguine Reciprocity | 0 real / 6 dry | 3 / 4 |
| Control-C | 0 real / 6 dry | **5 / 1** |

**It does not make a narrow core searchable** — a five-card pool genuinely has few legal lists, and
Sanguine Reciprocity still reads 4 dry. It stops the waste. And **runs across this change are not
comparable at a fixed seed**: a re-roll consumes more draws, so everything downstream shifts.

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

**CORRECTED: this is NOT "only decklists", and a DES run currently gets no gauntlet at all.**

`Gauntlet.For` returns `[]` for every set except ALL and LEG, and DES is defined as every set
*except* LEG — so the `gauntlet` prompt on a DES+CMB run produces a zero-deck gauntlet and the
evolver prints its "no gauntlet" warning. The `DeckRegistry` decks are LEG-pool lists; `Gauntlet`'s
own comment already records they are 0–5 of 13 cards on a non-LEG pool.

Real cost: three deck factories built from DES/CMB cards (Twin, Elves, Reanimator), a `DeckRegistry`
entry each, and one clause in `Gauntlet.For` returning them for DES. Still worth doing — it is the
only external yardstick the mode has — but price it as half a session, not as a decklist paste.

**BUILT AND MEASURED — the field loses 32.7%.** `DesignedGauntletDecks` (CMB Twin, CMB Elves, CMB
Reanimator), `Gauntlet.For` split by pool, and `FinalGauntlet`/`PrintGauntlet` — the last because
the gauntlet was counted in FITNESS while the report still printed a closed round-robin, so the gap
it exists to produce was invisible.

10 decks x 12 generations, 21 846 games, 20 games per reference:

| | field wins | reference wins |
|---|---|---|
| CMB Twin | 25.0% | **75.0%** |
| CMB Elves | 29.5% | **70.5%** |
| CMB Reanimator | 43.5% | 56.5% |
| **overall** | **32.7%** | |

Internal metrics read healthy throughout: 8/10 viable, 47.8pp spread, 44% diversity against a 35%
floor. Same blindness recorded for ALL, now measured on DES.

**The elf row is fully controlled and it is a SEARCH failure.** `Engine-Wirewood Herald` was SEEDED
with the elf core, pool-locked, and evolved twelve generations; the hand-built list beat it
**65-35** at the same 17 lands from the same 24-card pool. What it built:

```
4x Dwynen's Elite      4x Llanowar Elves        4x Reclamation Sage
3x Dwynen, Gilt-Leaf   4x Nissa, Vastwood Seer  4x Sylvan Ranger
3x Elvish Archdruid    4x Poison-Tip Archer     4x Wirewood Herald
4x Elvish Mystic       4x Radha, Heart of Keld
1x Elvish Visionary
```

**Zero Wirewood Symbiont, zero Wirewood Conduit, zero Timberwatch Elder** — three of the four engine
pieces of the archetype it was handed. The one it kept is the anchor, locked by `ProtectedIn`. In
their place four Reclamation Sage, near-vanilla here, and a 1x singleton.

**CAUSE FOUND, and it is a DATA problem, not a search one.** Every CMB card was **absent** from
`constructed_values_des_presim.json` — 711 cards measured before the set existed — so all 21 scored
exactly **0.00** while the HLM/CSC elves beside them read up to **+17.17**:

```
Nissa, Vastwood Seer   +17.17      Wirewood Conduit     0.00   <- never played
Sylvan Ranger          +15.55      Timberwatch Elder    0.00   <- never played
Radha, Heart of Keld   +14.33      Wirewood Symbiont    0.00   <- never played
```

The run used **`presim 0`**, so nothing measured them either. A zero-valued card loses every fill
comparison; `ExplorationBonus * Unmeasured` exists for this and is nowhere near enough against +17.

**Same self-reinforcing blind spot `MtgSimulator/CLAUDE.md` documents for draft bootstrapping**, in a
third place. **Operational rule: after adding cards to a pool, run mode 6 with `presim > 0` at least
once, or the new cards are unplayable by construction.**

What stands: the builder's elf deck is weak, from the same pool at the same land count, and its
bodies were BIGGER (Radha 3/3, Poison-Tip Archer 2/3) with a close curve (2.40 vs 2.16) — so neither
curve nor stats explain it. What does NOT stand: that this shows value-ranked filling is wrong.
Items (c)/(f) are untested by this run, not confirmed.

Read the gap: gauntlet decks overperforming means the builder still has work; even or slightly
behind means it is doing its job.

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
# cull, draftPrior, conceptSlots, gauntlet, enginesPath, engineSlots, EXCLUDED, seed
#
# `engineSlots` and `EXCLUDED` are asked ONLY when enginesPath is non-blank.  EXCLUDED is
# comma-separated concept names; the previous run prints the line ready to paste.
printf '6\n\n5\n18\n12\n3\n6\n20\n0.35\n0\nY\nY\n0\n0\n<engines.json>\n16\nMere-Storm\nseedword\n' \
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
