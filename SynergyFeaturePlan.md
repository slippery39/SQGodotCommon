# Feature-Based Synergy for Constructed Evolution — Handoff

**Status:** phase 1 (demand extraction) is **built and measured** — see §9. The rest of "The
proposal" is not started, and options 1/2/4 below are superseded by §9's approach.

---

## 1. What exists

Console **mode 6** evolves a field of constructed decks. See `MtgSimulator/CLAUDE.md`
§Constructed Metagame Evolution for the full description; this section is only what a new
session needs to orient.

| File | Purpose |
|---|---|
| `MtgSimulator/Evolution/Decklist.cs` | `Decklist` (name → copies + lands), invariants, `Difference`, `Materialize` |
| `MtgSimulator/Evolution/ConstructedValues.cs` | Card and pair values; `CardDelta`, `JointDelta`, `DeckFit`, `TopPartners`, `Movers` |
| `MtgSimulator/Evolution/DeckBuilder.cs` | Seeding (anchor + kernel + curve) and the four mutation operators |
| `MtgSimulator/Evolution/DeckHistory.cs` | Per-deck-slot card/pair record; drives synergy-aware cutting |
| `MtgSimulator/Evolution/PreSimulation.cs` | Random-deck measurement of the whole pool before evolution |
| `MtgSimulator/Evolution/MetagameEvolver.cs` | The loop, paired evaluation, culling, report |
| `MtgSimulator/CardStatAccumulator.cs` | Games-in-hand counting, shared with `DraftTrainer` |
| `MtgSimulator/GameSetup.cs` | Two built decks → pre-begin `GameState`; shared by draft and constructed |
| `MtgSimulator.Tests/MetagameEvolutionTests.cs` | 42 tests, including several regressions named below |

Run it from the **repo root**:

```
printf '6\n\n3\n8\n25\n3\n6\n20\n0.45\n800\nn\nY\n<seed>\n' | dotnet run --project MtgSimulator.Console -c Release
```

Fields: mode, AI depth (blank = 2), set index (**read the menu — it changes**), decks,
generations, mutants, games/matchup, final games/matchup, min diversity, presim decks,
cull (Y/n), seed-from-draft-model (Y/n), seed.

---

## 2. Measured state

Best result to date — **`csc1`**, which met the original acceptance spec:

| | csc1 | csc2 (after the scoring rewrite) |
|---|---|---|
| Field spread | 17.9pp | **14.3pp** |
| Viable (≥40%) | 8/8 | 8/8 |
| Every deck has a counter | yes | 7/8 (Wildcard's best is exactly 50%) |
| Distinct cards in final decks | **55** | 38 |
| Min diversity | **53%** | 45% — pinned at the floor |
| Spearman vs draft model | **0.701** | 0.609 |
| Cost | 30.6 min | 41.2 min |

The scoring rewrite bought cleaner decks and a tighter field at the cost of **pool coverage**.
csc2's decks are coherent aggro/midrange with no junk; they also look like each other.

Combined-pool (`ALL`, 785 cards) runs are consistently worse than CSC: best was 37.9pp spread,
worst 61.4pp with a deck collapsed to 8.6%. **More time made ALL worse, not better** — an 80
generation / 3 000-presim-deck run (270 min) produced a worse field than the 30-minute CSC run.

---

## 3. The problem this plan exists to solve

**Pairwise, name-keyed synergy cannot work at this pool size, and three separate defects all
traced back to it.** All three are now fixed; the underlying limitation is not.

### 3.1 The data is too sparse, structurally

| Pool | Possible pairs | Reality |
|---|---|---|
| CSC 408 | 83 028 | draft table median **53** games, **max 166** |
| ALL 785 | ~308 000 | **58% are cross-set** — sets are drafted separately, so those pairs have never co-occurred and never will |

Presimulation with uniformly random decks **cannot fix this**, and that is arithmetic rather than
budget. A 60-card deck holds ~15 distinct cards = 105 pairs. Measured across three presim sizes:

| Presim games | Pool | Pairs clearing the 50-game gate |
|---|---|---|
| 9 596 | CSC | 12 |
| 11 992 | ALL | 1 |
| **35 982** | ALL | **75** of 198 684 (**0.04%**) |

Coverage scales linearly with games while the space is quadratic in pool size. Card values
bootstrap beautifully (780/780 cards at median 709 games); pairs do not.

### 3.2 Deviation-from-baseline systematically selects junk

`ExpectedPairRate` predicts a catastrophic rate for two individually-terrible cards, so a pair
that merely performs badly reads as strong **positive** synergy:

| Pair | Baseline expects | "Synergy" | Absolute |
|---|---|---|---|
| Dragonstorm + Tendrils of Agony | −35.4pp | **+8.91pp** | **−26.11pp** |
| Gitaxian Probe + Tendrils | −29.5pp | +10.88pp | −18.38pp |
| Thoughtcast + Dragonstorm | −33.7pp | +5.08pp | −28.26pp |

**The worst cards in a format are the easiest to show synergy for.** This built a 4x-Dragonstorm
storm deck that finished at 8.6% and sat near-dead for 30 generations.

Fixed by `ConstructedValues.JointDelta` — absolute joint performance against the base rate.
Pinned by `TwoTerribleCards_AreNotSelectedAsPartners_EvenWithHugeSynergy` and its counterweight
`AGenuineEnabler_IsStillSelected_EvenIfWeakAlone`.

### 3.3 Pairs cannot express a threshold, and this is the part still unsolved

Three real cases, none representable as a pair at any sample size:

| Card | Needs | Why a pair cannot say it |
|---|---|---|
| Atog | ~15 artifacts | wants a *count*, not a partner |
| Dragonstorm | dragons in the deck | conditional on deck contents, not on drawing one card |
| Storm (Tendrils, Past in Flames) | ~8 cheap spells *before* it | a sequencing threshold |

This is what makes decks read as "halfway to a real deck" — a goblin package with two Mogg War
Marshals, Thoughtcast with no artifacts.

---

## 4. What the codebase already gives you for free

**The cards are data.** Effects are classes, composed by builders; nothing is hardcoded. A
feature extractor needs no hand-labelling — the components *are* the labels, and they cannot
drift from behaviour because they define it.

| Source | Example |
|---|---|
| Component types | `AffinityComponent`, `FlashbackComponent`, `ThresholdComponent`, `PlusOneCounterComponent`, `EquipmentComponent` |
| Action types inside effects | `MillAction`, `DestroyCreatureAction`, `DrawCardsAction`, `CreateCardAction`, `DrainLifeAction` |
| Additional costs **and their `Filter`** | `SacrificeAdditionalCost { Filter = IsSubtypeSpecification { Subtype = "Artifact" } }` — this *is* "Atog wants artifacts" |
| Trigger conditions | `EventTriggerCondition.EventTypeName`, `ActiveInZone = Graveyard` |
| Targeting | `AllValid(Creatures())` — this *is* "sweeper" |
| Scalars | `ManaCost`, P/T, `Subtypes`, `CardType` flags, `CreatureComponent` keywords |

Two worked examples, verified in source:

- `Day of Judgment` = `DestroyCreatureAction` + `TargetingStrategy.AllValid(TargetSpecification.Creatures())`
  (`MtgCore/Sets/CoresetCube/CoresetCubeWhiteSpells.cs:158`)
- `Atog` = `ActivatedAbilityComponent` with `SacrificeAdditionalCost` filtered to `"Artifact"`
  (`MtgCore/Cards/CardLibrary.cs:1650`)

Rough sizing: ~60–100 binary/bucketed features → a few thousand feature-pairs, against 308 000
card-pairs. **Two orders of magnitude less data needed**, and every game updates many
feature-pairs at once.

`MtgSimulator/Scenarios/StateJson.cs` already walks every abstract type in the game assemblies by
reflection — copy that pattern rather than inventing one.

---

## 5. The proposal

### Option 1 — Feature-pair synergy table *(recommended first)*

Accumulate `PairStat` keyed by **feature pair** instead of card pair. A never-played card gets a
synergy estimate from its mechanics alone, which is the only thing that generalises to unplayed
cards and to cross-set pairs where measured data can never exist.

*Reuses:* `CardStatAccumulator`, `DraftTrainingData.Shrink`, the whole scoring path.
*New:* a `CardFeatures` extractor (~200 lines) and a feature-keyed accumulator.
*Risk:* features too fine-grained collapse back into card identity. **Bucket numeric values**
(damage 1–2 / 3–4 / 5+), never use exact amounts.

### Option 2 — Deck-vector regression

Describe the *deck* by aggregate features (creature count, removal count, discard outlets,
graveyard payoffs, artifact count, curve shape) and learn which vectors win. **The only option
that expresses critical mass**, i.e. the §3.3 cases.

*Cost:* significantly higher — needs a regression and far more deck-outcome data.
*Do not start here.* Option 1 may make it unnecessary, since a feature-pair table already knows
"sacrifice-artifact-cost" pairs well with "is-artifact".

### Option 3 — Synergy sandbox (direct causal measurement)

Extend `CardValueSandbox`: measure A alone, B alone, then A+B in the same fixture.
Synergy = value(A+B) − value(A) − value(B). **No statistics, no sample-size problem.**

*Feasibility, recomputed:* `CardValueSweep` does 408 cards in ~8s (~20ms each). Even at 2× for a
pair, CSC's 83k pairs is under an hour and ALL is one overnight run. Restricting to plausible
candidates cuts that 10×.
*Caveats:* the fixture is a symmetric board, so sweepers price near zero, and it measures
immediate value rather than long-game inevitability. Madness measures cleanly; a control shell
does not.

### Option 4 — Matrix factorization

Learn an 8–16 dim embedding per card so dot products predict pair synergy. ~100 lines of
hand-rolled SGD, no dependency. The data-efficient form of "use ML", and the textbook answer to
sparse pairwise data.

*Ranked last* because it must *infer* structure that options 1 and 3 read directly off the cards,
and its output is uninterpretable — you cannot look at an embedding and tell whether it found
madness or noise.

### Recommended sequence

**1 → 3 → measure → only then consider 2.** They compose: features *propose* cheaply and
generalise to unplayed cards, the sandbox *verifies* the top candidates causally, evolution
*confirms* survivors in real games. Three cheap filters in series instead of one statistical
estimate that never gets enough data.

---

## 6. Traps — every one of these has already cost a run

**Verify the gate against the real distribution before trusting any synergy work.**
`MinPairGames` was set to 200 by analogy with `pairShrinkK` while the busiest CSC draft pair had
**166 games**. Every synergy path was silently dead for three full runs; the decks looked like
piles of good cards and it was caught by reading decklists, not code. The unit test "covering" it
used inline data with 4 000 games and passed throughout.

**A test with unrealistic fixture data measures nothing.** Twice now. Use volumes the real data
actually reaches.

**Selection scores must be commensurate.** `SynergyWithDeck` summed over cards weighted by
copies, producing ~+290 against card values in the ±25 range — card quality became rounding
error, and a deck rated Dragonstorm (+261) above Ancestral Recall (+240). `DeckFit` averages for
this reason; `DeckFit_StaysCommensurateWithCardValue` pins it.

**Filter and ranking must use the SAME score.** The cut filter scored on local history only,
making globally-terrible cards uncuttable. `AGloballyTerribleCard_IsAlwaysCuttable` pins it, and
asserts the junk is cut *better than chance*, not merely reachable.

**Unmeasured ≠ bad.** Imputing missing pairs at zero created a tenure bonus that cut Sol Ring
(+20.62pp, best in its run) and Ancestral Recall. `DeckHistory` imputes at the deck mean.

**Card values absorb deck strength.** `corr(draft rate, movement) = +0.224` — mildly amplifying.
Unlike draft training this **cannot be fixed by equalising strength**, because the mode
deliberately makes decks unequal. Read `constructed_values_*.json` as "how this card did in the
decks that played it".

**Two games-in-hand tables from different formats share no zero point.** Draft 0.5162 vs
constructed 0.4439 — a 7.2pp offset that made every card read as ~9pp worse. `Movers` centres on
the median; `FormatOffset` reports it.

**Do not blame the pilot.** An earlier version of this document claimed a 2-turn beam search
cannot pilot storm or combo, and used it to explain Dragonstorm's 1/780. **That is false.** The
`DeckRegistry` precons — Traditional Storm, Reanimator, Affinity, Goblins — won games from the day
they were written, and Storm was balanced *down* for being too strong. Dragonstorm rated 1/780
because the decks holding it **contained no dragons**: it was a blank card, not a mispiloted one.

The real cause is the search operator — see `MtgSimulator/CLAUDE.md` §"The pilot is NOT the
ceiling". A win rate measured on a deck that never assembled the concept says nothing about the
card, and reading it as a pilot limitation sent a session hunting the wrong thing.

**`sim_results/` is relative to the shell's cwd**, and `dotnet build` can succeed over a stale
`MtgCore.dll`. Run from the repo root; check DLL timestamps before believing a run.

---

## 7. Verification

**Before trusting a feature table:**
1. Print how many feature-pairs clear the evidence gate. If it is near zero, the features are too
   fine-grained — bucket harder.
2. Read the top 20 feature-pairs by joint delta. They must be mechanically legible
   ("discard-cost + graveyard-active-trigger"). If they are not, the extractor is wrong.
3. Check a known case by hand: `SacrificeAdditionalCost{Artifact}` should pair positively with
   `is-artifact`.

**A/B against `csc1` and `csc2`**, which are the baselines, at the same seed and settings:
`8 decks, 25 generations, 800 presim, 45% diversity, no culling, CSC`. Compare field spread,
viability, **distinct cards in final decks** (55 / 38), and Spearman.

**The open regression to watch:** csc2 dropped pool coverage to 38 distinct cards with diversity
pinned at the 45% floor. A feature table should *widen* this, because it can value cards with no
pair data. If coverage does not improve, the feature work is not doing its job. First lever to
try is `DeckBuilder.ExplorationBonus` (currently 3.0, likely too small now the synergy term is
bounded) — but change one thing at a time.

---

## 8. Deliberately out of scope

- **Colours.** Would be the natural diversity mechanism (one colour per deck slot), but the
  engine has no colour at all — see the deferred table in `MtgCore/CLAUDE.md`. Large change,
  separate decision.
- **Archetype tagging by hand.** Rejected at design time and again since. The components already
  encode behaviour; a hand-maintained label can drift from what the card does, and 785 cards is
  an unbounded upkeep cost.
- **Deep neural nets.** Data-starved: a 4-hour run produces ~800 distinct decklists against a
  785-dimensional input. Revisit only with millions of deck-outcomes.

---

## 9. Phase 1, built: demands read off the cards

`MtgSimulator/Evolution/PoolFeatures.cs`, driven by `MtgSimulator.Tests/CardDemandSweep.cs`
(7 fast tests + an `[Explicit]` dump). **Supersedes options 1, 2 and 4 above**: a feature-pair
table would spend thousands of games rediscovering statistically what the cards already state,
and would still be blank for a card nobody played.

### The two harvest rules

No mechanic-to-meaning table exists, because two engine conventions make one unnecessary:

1. **Every `TargetSpecification`-typed property in MtgCore is named `Filter` or `AppliesTo`
   ("which cards qualify" — a demand) except `TargetingStrategy.Specification` ("what am I aiming
   at" — Lightning Bolt's "any creature" is about the opponent's board).** So the demand *is* the
   card's own filter object, run against the pool in a fixture. `TargetSpecification` is a record,
   so value equality dedupes pool-wide for free — Atog and Arcbound Ravager produce one demand.
2. **A `string` property named `Subtype` always means "cards of this subtype qualify."** This is
   what finds Dragonstorm's `SelectCardFromLibraryAction{Subtype = Dragon}`, four levels deep
   inside a `PipelineAction`.

### Measured on CSC (408 spells, 188 ms, zero specs threw)

20 distinct demands; **55 cards (13%) state one**. Every top demand is mechanically legible:

| Suppliers | Demand | Concept |
|---|---|---|
| 237 | creatures you control | anthems / lords |
| 237 | creature card in your graveyard | reanimator |
| 182 | creature, mana value ≤ 3 | cheap-creature tutor |
| 105 | instant or sorcery in your graveyard | **storm / flashback** |
| 58 | creatures with flying | flying matters |
| 48 | `SacrificeAdditionalCost{Artifact}` | **affinity / Atog** |
| 24 / 20 / 18 / 5 / 2 | Soldier / Elf / Goblin / Spirit / Wolf | tribal |

Tribal, graveyard, artifact, flying and big-spell axes, all found with nothing hand-labelled.

### Three things the first dump caught, all of which would have been silent

- **`ImmutableArray<T>` is a struct.** An `IsClass` guard placed before the `IEnumerable` branch
  dropped every `Card.Components` while `AdditionalCastCosts` (an `ImmutableList`, a class) kept
  working — so sacrifice costs were harvested and nothing else was. A *half*-working extractor is
  the worst outcome: plausible output for some cards, silently none for the rest.
- **Degenerate demands.** `IsNotSelfSpecification` and `IsControlledByYouSpecification` came back
  answered by **408 of 408** cards. They are trigger qualifiers, not questions about your deck, and
  a demand every card answers adds the same constant to every deck. Dropped above 99% coverage.
  **This is not the rarity cutoff that was rejected** — "creatures you control" at 58% survives,
  because anthem-plus-tokens is a real deck and density decides that, not rarity.
- **Object-referential specs read as unanswerable.** `IsSourceCardSpecification`,
  `IsEquippedBySourceSpecification` and `IsControlledByOpponentSpecification` are about one
  specific object, so nothing in the pool answers them — which would have marked every death
  trigger and every equipment as a DEAD CARD. The dead-card rule running backwards.

Also fixed: `DiscardAdditionalCost{Land}` reads 0 pool suppliers because a mana base is a scalar
on `Decklist`. `Satisfaction` adds `deck.Lands` for demands a land answers.

### NaN is not zero, and that is the dead-card rule

`Satisfaction` returns **NaN when a card asks nothing** (Lightning Bolt — fine anywhere) and **0
when it asks and the deck answers with nothing** (Dragonstorm with no dragons — a blank card
holding a slot). Those were indistinguishable before, which is why dragonless Dragonstorm survived
whole runs. `DeadCards(deck)` lists the second kind.

### Trigger demands, closed by probe

A trigger's demand is `EventTypeName` **plus** `Filter`, and reading the filter alone gives the
degenerate `{ControlledByYou, NotSelf}` — answered by every card, dropped as uninformative, and
155 CSC cards fell out of the count that way. The whole `TriggeredAbilityComponent.Condition` is
now captured instead, and answered by **playing every card in the pool and asking the condition
about the events the engine really emits** (`ProbeTriggers`). Two perturbations per card: it
enters the battlefield, then it is destroyed.

**No event type name appears in `PoolFeatures`.** The engine supplies the events, so a new event
works the day it is emitted — the mapping table this design exists to avoid was never needed.

| | CSC | ALL |
|---|---|---|
| Spells | 408 | 780 |
| Distinct demands | 20 → **43** | **61** |
| Cards stating a demand | 13% → **31%** | **29%** |
| Build time | **550 ms** | **585 ms** |

New axes the probe found, none of them expressible from card data alone: ETB-matters (216
suppliers), death-matters (210), tribal death (Human, 55), flying-ETB (49), big-creature-ETB (33),
lifegain (5). On ALL the cross-set demands appear — Artifact 58, Spirit 55, Human 137 — which is
the case §3.1 says measured co-occurrence can never have data for.

**Not probed: casting, attacking, turn start, life gain.** Casting needs targets, mana and cost
payments (`CardValueSandbox` solves that at much greater cost), so "whenever you cast a spell"
demands sit at zero suppliers. That is safe rather than wrong — see the zero-supplier rule below.

### The dead-card rule must RANK the cut, not delete the card

Reviewed and deliberately weakened before shipping any of it into `DeckBuilder`, because
`Satisfaction == 0` cannot tell two very different cards apart:

| Card | Satisfaction with no support | Actually |
|---|---|---|
| Dragonstorm, 7 mana, no dragons | 0 | does literally nothing |
| A 2/2 for 2 with "whenever another Goblin enters, gain 1 life" | 0 | a fine 2/2 for 2 |

A second false positive is structural: `EventTriggerCondition{CreatureDestroyedEvent, Filter = }`
has **no controller restriction**, so it fires on the OPPONENT's creatures dying. A creatureless
control deck supplies zero and reads as dead while the card is live all game.

So satisfaction sorts a card to the front of the cut candidates — it goes first among cards the
deck was already going to cut — and never deletes on sight. The measured win rate still decides;
satisfaction only breaks ties, so Dragonstorm gets cut in generation 2 rather than generation 30.
The "never add a demand-carrying card without suppliers" half of the original proposal is dropped
for the same reason.

**"Zero dead cards in the final decks" is NOT an acceptance test.** A winning deck containing a
fine 2/2 whose rider is irrelevant is a correct outcome. The test is the original one: does a
Dragonstorm deck end up with dragons, or without Dragonstorm.

The principled upgrade is measurable rather than guessable — value the card in a fixture with and
without suppliers; if the value barely moves the demand is a rider, if it collapses the demand IS
the card. Build it only if ranking proves too blunt in the decklists.

### A demand nothing answers is IGNORED, not treated as starving

The third instance of the same trap. Some triggers fire on things no card supplies — "at the start
of your upkeep", "whenever you gain 5 life", "whenever a creature attacks" (unprobed) — and
counting those as unsatisfied would mark every mana dork and upkeep payoff in the pool as a dead
card. `Informative` skips any demand with no supplier anywhere; a card whose demands are all
uninformative reports NaN (asks nothing) rather than 0 (starving).

The cost is a false NEGATIVE: in a pool with no Dragons at all, Dragonstorm is dead everywhere and
this will not say so. **That is the safe direction** — a missed warning leaves a bad card in a
deck, a false positive deletes a good one — and it does not weaken the case the rule exists for,
since a pool that HAS dragons gives the demand real suppliers and a dragonless deck still reads 0.

### Cost probe: demands that live in engine code, not on the card

`AffinityComponent` is a marker with **no data at all** — its meaning is a subtraction inside
`CostEngine` — so Thoughtcast states nothing and the harvest cannot see it. The artifact concept
existed only because *Atog* names artifacts in a sacrifice cost, so seeding on it would have pulled
in the artifacts and left out the payoff that wants them: a half-built Affinity deck, which is the
failure this feature exists to fix.

`ProbeCostDemands` needs no new vocabulary. It reuses the demands already harvested: put four
suppliers of demand D on the battlefield, recompute every card's effective cost, and any card that
got cheaper demands D — whatever mechanic did it, including one written later.
`ComputeEffectiveCost` takes a card TEMPLATE, so no candidate is placed and it is one pure call
per card. Catches affinity, convoke and `ConditionalCostReductionComponent`.

Final coverage: CSC **43 demands / 32% of cards / 395 ms**, ALL **61 demands / 30% / 656 ms**.

**Still unread: threshold, graveyard-count and storm**, whose payoff shows up in P/T or in
resolution count rather than in cost. Those are different observables (`GetEffectiveStats`, and a
resolution counter for storm) and are not built. Storm in particular wants a game-counter axis
(`SpellsCastThisTurn`) rather than a board-population one, which is a different shape from
everything here.

---

## 10. Phase 2, part 1: satisfaction wired into `DeckBuilder`

`PoolFeatures` is threaded through `Seed` / `Mutate` / `Fill` / `PickWeakest` as an **optional
parameter defaulting to null**, so every existing caller and all 42 prior evolution tests keep
their exact behaviour and the term can be A/B'd by passing null. Nothing in `MetagameEvolver`
passes it yet — this is the mechanism, not the run.

One function, used in both directions:

```
SupportScore(card, deck, features):
  NaN (asks nothing)  ->  0          neither rewarded nor punished
  0   (asks, unmet)   -> -DeadCardPenalty (5.0)
  n   (supported)     -> +SupportBonus (4.0) * min(n / 12, 1)
```

Both constants are in `CardDelta` percentage points so they stay commensurate — the trap that
produced `DeckFit`'s ~+290 against card values in the ±25 range. The support term is **bounded**
for the same reason.

### Three tests, and the middle one is the counterweight

| Test | Asserts |
|---|---|
| `ACardTheDeckCannotSupportAtAll_IsCutFarMoreOftenThanChance` | unsupported lord cut in >25% of proposals against ~11% blind |
| `AWinningCardIsNotCutJustBecauseItIsUnsupported` | a lord winning by >10pp is cut in **<11%** — below blind chance |
| `SeedingWithFeatures_BuildsBetterSupportedDecks` | A/B on the same seeds: mean support **1.98 -> 2.58** |

The second is the one that matters. Without it the penalty could be raised until it deletes good
cards and every other test would still pass — the exact shape of
`AGenuineEnabler_IsStillSelected_EvenIfWeakAlone`.

**The A/B fixture had to be widened before it measured anything.** At 4 tribe members against 8
fillers it read 9.30 -> 10.20, because a random fill clusters a tribe by accident in a narrow
pool; at 4 against 30 it reads 1.98 -> 2.58 off a genuinely low baseline. Third instance of the
unrealistic-fixture trap in this file.

### Honest reading: seeding clusters, but weakly

+30% support is a nudge, not an archetype. That is by design — `SupportBonus` is 4.0 against
`CardDelta` in the ±25 range, so card quality still dominates, which is what keeps the term from
building bad decks. **Concept-committed seeding is the part that would make it strong** and is not
built: pick a demand, jam its suppliers, hold the concept while refining, abandon only when the
candidate set is exhausted. That is the next step, and it is where a decklist should visibly
change.

---

## 11. Phase 2, part 2: concept-committed seeding

`DeckBuilder.SeedConcept` — **the operator the mode was missing.** `Package` brings an anchor plus
up to two partners; a concept needs its critical mass at once, because every intermediate step
toward it is worse than the pile it came from and `Mutate`'s accept-if-better rule rejects it.

1. Pick a demand with at least `MinConceptSuppliers` (6) suppliers in the pool, weighted by
   supplier count. Below that it is an interaction, not an archetype.
2. Candidate set is **both halves** — every card that ANSWERS the demand and every card that ASKS
   it. Without the payoffs you build 24 artifacts and no Atog; without the suppliers, Atog and
   nothing to eat.
3. Jam it: 4 copies at a time, best-first with softmax noise, until the deck is full.
4. Top up from the whole pool if the concept cannot fill 36 — a 20-card package plus good cards
   is still that deck.

**No curve target is imposed**, because affinity and reanimator have high printed curves and low
real ones. The concept's own average cost is used, and `AdjustLands` tunes the mana base after.

### Labelled field

`SeedField(..., conceptSlots: n)` names slots `Synergy-1..n`, `Midrange-A..`, `Wildcard`. Concept
slots are seeded FIRST — a synergy deck is the hardest shape to fit past the diversity floor,
since its cards are the ones it cannot substitute — and each takes a **distinct** demand, because
three slots that all discover Goblins are one deck and the floor would reject two of them anyway.

### Measured, on inline fixtures

| Test | Result |
|---|---|
| `SeedConcept_JamsTheWholeArchetype_NotAPackageOfTwo` | >=16 on-concept cards in 36 slots |
| `SeedConcept_BeatsOrdinarySeeding_AtBuildingTheArchetype` | on-concept cards **>3x** ordinary seeding, same seeds |
| `SeedField_LabelsSlotsAndGivesEachSynergySlotItsOwnConcept` | two synergy slots land on **different** archetypes |

### Two fixture traps hit again in one sitting

- **4 tribe members is below `MinConceptSuppliers`**, so `SeedConcept` silently fell back to
  ordinary seeding and the tests measured nothing. A fixture under a production threshold does not
  fail loudly; it passes for the wrong reason.
- **A one-concept pool cannot test the distinct-concept rule.** The second slot correctly finds
  nothing left and falls back, which is indistinguishable from the rule not working. Needed a
  second independent tribe.

### A real concurrency bug, found by a flaky suite

`PropertyCache` shipped as an unsynchronised `static Dictionary`. Two fixtures calling `Build` on
different threads corrupted it and surfaced as **an unrelated test failing once in several runs**.
Now a `ConcurrentDictionary`. `MetagameEvolver` runs its games under `Parallel.For`, so any static
on a build path has to be thread-safe — this would have been far worse to diagnose during a run
than in the suite.

### Not yet done

`MetagameEvolver` still passes no features, so **no real run has been made**. Wiring is: build
`PoolFeatures` once at startup, pass it to `SeedField` with `conceptSlots`, and pass it through
`Mutate`. The refine/abandon half — hold the concept while pruning, abandon only when the
candidate set is exhausted rather than on the viability floor — is also not built, and without it
mutation can still dissolve a concept deck back toward a pile.

---

## 12. Phase 2, part 3: wired into the run, and the first decklists

`MetagameEvolver` takes `conceptSlots` (default **0 = off**, which is the A/B control and leaves
every existing path untouched). Console mode 6 prompts for it after the draft-prior question.
`PoolFeatures` is built once at startup and reported on its own line.

Culling changes for concept slots: `ConceptGraceMultiplier` (3) leaves them alone three times as
long, and a culled concept slot **re-seeds on a concept** rather than on an anchor-and-kernel pile
— replacing it with a midrange deck silently retires the exploration arm, which is how the
wildcard slot stopped contributing anything on the combined pool. The long grace is an admitted
**proxy for "the candidate set is exhausted"**, which is the honest abandon rule and is not built.

### First run: CSC, 4 decks, 2 generations, 2 concept slots

Deliberately tiny — this reads the DECKLISTS, not the win rates.

| Slot | Deck | Concept |
|---|---|---|
| Synergy-1 | Stormfront Pegasus, Empyrean Eagle, Vampire Nighthawk, Avaricious Dragon, Thundermaw Hellkite, Archangel of Thune | **Flyers**, around the flying lord |
| Synergy-2 | Anointer of Champions, Kytheon, Fencing Ace, Unchained Berserker, Barrin, Master of Diversion, Xathrid Necromancer | **Humans**, tribal |
| Midrange-C | Ravaging Blaze, Preordain, Corpse Knight, Fauna Shaman, Soul Sear, Shifting Ceratops | a pile, 14 distinct, no theme |

**Nothing names "flyers" or "humans" anywhere.** Both came from demands read off the cards —
`HasFlyingSpecification` (58 suppliers) and `IsSubtypeSpecification{Human}` (56). The midrange slot
in the same run produced exactly the pile this whole document exists to explain, which is the
contrast worth having.

**Read no win rate from that run.** Two generations at two games per matchup is noise; Synergy-2
reading NON-VIABLE at 25% means nothing at that sample size.

### What is still not built

- **Refine/abandon.** Mutation can still dissolve a concept deck back toward a pile over 25
  generations; the only thing resisting it is `SupportScore` in `PickWeakest`. Whether that is
  enough is the first thing a real run will show.
- **A real A/B.** 8 decks x 25 generations, `conceptSlots: 0` against `conceptSlots: 3`, same seed
  and settings, compared on **distinct cards in final decks** (csc1 55, csc2 38) rather than on
  spread — a synergy slot narrows its own deck on purpose, so spread is the wrong instrument.

---

## 13. MEASURED: an A/B that turned out to be INSIDE THE NOISE

> **READ §14 FIRST.** The conclusions in this section were drawn before the control was run
> against itself. When it was, the control differed from its own repeat by MORE than it differed
> from the treatment, so nothing below is established. It is kept because the diagnosis of
> `PickConcept` in it was independently confirmed by a unit test, and because the reasoning is a
> worked example of the error §14 describes.

CSC, 8 decks x 25 generations, 3 mutants, 6 games/matchup, 20 final, 0.45 diversity, 800 presim,
no culling, draft prior on, **identical seed**, same binary. Arm A `conceptSlots: 0`, arm B
`conceptSlots: 3`. `constructed_values_csc.json` was snapshotted and restored between arms — without
that, arm B seeds from arm A's data and the comparison measures contamination.

| | Arm A (control) | Arm B (concepts) |
|---|---|---|
| **Distinct cards in final decks** | **49** | **50** |
| Field spread | 15.7pp | 20.0pp |
| Viable | **8/8** | 7/8 |
| Min diversity | **55%** | **46%** |
| Runtime | 16.7m | 24.8m |

**No improvement in coverage, worse diversity, one non-viable deck.** Do not ship concept slots on
the current implementation.

### Two separate causes, separated by the diversity trajectory

```
arm A:  gen1 69%  gen2 67%  gen5 64%  gen10 61%  gen25 55%   gradual decay
arm B:  gen1 59%  gen2 48%  gen5 49%  gen10 50%  gen25 46%   collapsed in one generation
```

**1. `PickConcept` prefers the WRONG demands.** It weights by supplier count, so it draws the
BROADEST demands — on CSC those are "creatures you control" (237 of 408), "creature card in your
graveyard" (237), "creature mana value <= 3" (182). Those are nearly the same set of cards and none
is an archetype; "play creatures" is not a deck. The narrow interesting ones (Goblin 18, Elf 20,
Spirit 5) are almost never drawn. Arm B therefore STARTS at 59% diversity against the control's
69% — the three concepts were similar before mutation touched them.

This is why the earlier 4-deck smoke run looked good (it drew Flyers and Humans with 2 slots) and
this run did not. **A promising smoke run on a different draw is not evidence.**

**2. Nothing holds a concept.** 59% -> 48% in one generation. `SupportBonus` (4.0) against
`CardDelta` in the +/-25 range loses to card quality by design, which was the right call for safety
and is insufficient for holding. Synergy-2 finished as a pile carrying a stranded 3x Empyrean Eagle
and a stranded **1x Goblin Chieftain** — the half-built deck this whole document exists to prevent,
reproduced by the feature meant to fix it.

### The fix for cause 1, and why it does not contradict the Anthem argument

Concept choice should prefer **distinctive** demands: at least `MinConceptSuppliers`, but weighted
DOWN as breadth rises. A demand 58% of the pool answers cannot produce a deck that differs from any
other deck.

This is not the rarity cutoff rejected in §9. That rejection was about **scoring whether a card is
supported**, where breadth is irrelevant — anthem-plus-cheap-tokens is a real deck and "creatures
you control" is the right demand for it. Choosing what a whole deck is ABOUT is a different
question with the opposite answer. Keep the two rules separate.

### What survives

The extractor (43 demands, 395 ms, legible), the seeding mechanism itself, and `conceptSlots: 0`
being bit-identical to prior behaviour. What is disproved is concept SELECTION and the assumption
that seeding alone would show up in a run.

**Do not run ALL until a re-run on CSC shows a real effect.** ALL costs ~2x and would measure the
same two defects more slowly.

---

## 14. THE INSTRUMENT IS NOT VALID: single-run A/B cannot measure this mode

After fixing `PickConcept` (§13) the same A/B was re-run. Comparing the CONTROL arms of the two
runs — identical seed (`-1002640004`), byte-identical `constructed_values_csc.json`, identical
settings, `conceptSlots: 0` in both:

| | distinct cards | spread | min diversity | viable |
|---|---|---|---|---|
| **control, run 1** | **49** | 15.7pp | **55%** | **8/8** |
| **control, run 2** | **47** | 25.7pp | **47%** | 7/8 |
| concepts, run 1 | 50 | 20.0pp | 46% | 7/8 |
| concepts, run 2 | 48 | 37.1pp | 46% | 6/8 |

**The control differs from itself by more than it differs from the treatment.** The diversity gap
called a defect in §13 (55% -> 46%) is the same size as the control's own run-to-run gap
(55% -> 47%). Every number in §13 is inside the noise, in both directions: the feature was not
shown to hurt, and the §13 fix was not shown to help.

### The engine is deterministic at small scale

Verified rather than assumed. Same binary, same seed, twice:

| Config | Result |
|---|---|
| 4 decks, 3 generations, presim 0 | **bit-identical** |
| 4 decks, 2 generations, presim 200 | **bit-identical** (only wall-clock differs) |

So this is not a shuffling or RNG-plumbing bug at the level those configs exercise. **It is `PreSimulation.Run` that does not reproduce.** Localised by elimination, same binary and
seed throughout:

| Config, run twice | Result |
|---|---|
| 4 decks, 3 generations, presim 0 | identical |
| 4 decks, 2 generations, presim 200 | identical |
| **8 decks, 2 generations, presim 0** | **identical** |
| **8 decks, 2 generations, presim 800** | **DIFFERS at generation 1** |

The generation loop is deterministic at full deck count. Presimulation produces different card
values run to run, which changes seeding, which changes every generation after it.

### ROOT CAUSE, confirmed: the 300s wall-clock net decides which games count

```
run 1:  9585 games in 9.4m, 1 excluded.   Base rate 50.0%
run 2:  9584 games in 8.9m, 2 excluded.   Base rate 50.0%
```

Same seed, same inputs. `PreSimulation.Run` drops any game whose `EndReason` is
`TimeLimitReached` — which is the **300 000 ms wall-clock safety net**, the one termination
`MtgSimulator/CLAUDE.md` marks as *not* deterministic. In a 9 600-game parallel batch one or two
games get starved past 300s, and WHICH ones depends on machine load. Different games excluded ->
slightly different card values -> different softmax draws at seeding -> a completely different
field by generation 1.

**This is the same bug class already fixed once in this project.** `GameRunner` no longer ENDS a
game on the clock ("Wall-clock time must never decide a game"), but `PreSimulation` still
DISCARDS DATA on it. The fix removed wall-clock from one decision and left it in another.

Scale dependence follows directly: presim 200 (2 391 games) sees no timeouts and reproduces
exactly; presim 800 (9 600 games) has enough contention to hit one or two.

**Costed options, none applied — this touches a safety mechanism and is a decision, not a
cleanup:**

| Option | Effect | Risk |
|---|---|---|
| Raise the net far above any load-induced delay (e.g. 1800s) | exclusions go to 0 in normal operation, so the count is stable | a genuine engine hang takes much longer to surface |
| Bound the game deterministically instead (turn/action limits already do; the AI's rollout budget already does) and delete the net | fully deterministic | needs proof no path escapes those bounds |
| Keep the net but FAIL the run loudly when it fires | turns silent nondeterminism into an obvious error | a single starved game aborts a 40-minute run |
| Leave it and use `presim 0` for A/B work | valid comparisons today, no engine change | loses the pool coverage presim exists to provide |

**Superseded lead, recorded so it is not re-investigated:** `PreSimulation.Run` hands ONE `Random`
to both strategies in a game
(`var aiRng = new Random(g.GameSeed + 4)` passed twice). `MtgSimulator/CLAUDE.md` records that
`StrengthHarness` fixed exactly this pattern — "the original shares one `Random` between both
strategies, so they consume each other's draws — a confound, **and why it could not be
parallelised**" — and `PreSimulation` both shares the RNG and runs under `Parallel.For`. That is a
confound rather than the nondeterminism — the per-game instance is created inside the loop body, so
the sharing is within a game rather than across threads. Worth fixing on its own (it is exactly
what `StrengthHarness` corrected) but it is NOT the cause of the irreproducibility.

**Practical workaround available today: run A/B comparisons with `presim 0`.** The generation loop
reproduces exactly, so a paired comparison is valid without presimulation. It costs the pool
coverage presim was added for, which is a real loss, but it makes the instrument work.

### Why single-run comparison was never going to work here

Even with a perfectly deterministic engine, this mode is **chaotic**: one different game outcome
changes which mutant is accepted, which changes the field, which changes every subsequent
matchup. `MetagameEvolver` uses common random numbers to make the *parent vs mutant* decision
survive that — an unpaired 42-game evaluation has ~7pp of standard error — but nothing pairs the
FIELD-LEVEL outcome across runs. Final coverage and final diversity have no such protection.

**The rule this establishes: a field-level metric from one run of mode 6 is an anecdote.** Any
claim about a change to seeding, mutation or scoring needs several seeds per arm and a comparison
of distributions, with the noise floor measured first by running the control against itself.

That noise floor has never been measured for this mode, and every documented result in
`MtgSimulator/CLAUDE.md` for it — csc1 vs csc2 included — is a single run per configuration.
Those comparisons should be read as provisional until the floor is known.

### What is still solid

Nothing here touches the parts pinned by unit tests: the extractor (43 legible demands on CSC,
zero specs throwing), the `PickConcept` weighting fix (**38/60 vs 50/60** concept seeds committing
to a tribe, and the test fails under the old weighting), the NaN-vs-zero distinction, and
`conceptSlots: 0` being bit-identical to prior behaviour.

---

## 15. FIXED and verified: mode 6 is reproducible again

`GameRunner.SafetyTimeoutMs` 300 000 -> **1 800 000**, and `PreSimulation` now warns loudly on any
exclusion. Rationale at the constant; the short form is that a game's RESULT is deterministic and
only the waiting is load-dependent, so waiting is strictly more correct than discarding.

Acceptance test — the exact configuration that failed (8 decks, presim 800, 2 generations, same
seed, run twice):

```
run 1:  gen 1: spread 26.2%-69.0% (42.9pp), diversity 67%, accepted 5/8 ... 0.9m
run 2:  gen 1: spread 26.2%-69.0% (42.9pp), diversity 67%, accepted 5/8 ... 0.9m
run 1:  gen 2: spread 38.1%-64.3% (26.2pp), diversity 50%, accepted 5/8 ... 1.6m
run 2:  gen 2: spread 38.1%-64.3% (26.2pp), diversity 50%, accepted 5/8 ... 1.4m
```

**Bit-identical apart from the elapsed-time column**, which is the only thing that should vary.
Post-fix presim reports **0 excluded** (9 586 games) against 1 and 2 before.

**Cost: presim ~9m -> 23.5m.** One straggler game now runs to completion instead of being cut at
300s, and in a parallel batch it dominates the tail. Not a defect — 100 turns x 200 actions permits
a ~23-minute game — but budget for it, and if it ever needs reducing, change the deterministic
bound rather than the clock.

### What this changes about the plan

The noise floor for a PAIRED comparison may now be zero, which would make single-run A/B a valid
instrument after all — the opposite of §14's conclusion, and the reason §14 said to measure the
floor rather than assume it. Re-run the concept A/B (arm A `conceptSlots: 0`, arm B `3`, same seed)
before spending anything on measuring variance that no longer exists.

**§13 and §14 remain retracted.** Both A/Bs behind them were run on the broken build, so the
question "do concept slots help" is still unanswered, not answered negatively.

### Operational note, learned expensively

The snapshot/restore scripts these comparisons depend on had no `mkdir`, so pointing one at a new
directory made the backup silently fail and the runs would have merged into the shared
`constructed_values_<set>.json` with nothing to roll back to. Worse, **`TaskStop` kills the shell
and leaves the `MtgSimulator.Console` child running**, which then spawned the next loop iteration;
the processes had to be killed explicitly. No contamination occurred (verified byte-identical), but
any script that guards `sim_results/` must `mkdir -p` and abort on a failed backup.

---

## 16. MEASURED on a valid instrument: seeding is solved, HOLDING is not

First A/B on the fixed build. Both arms report **0 excluded, 9586 games**, so both are
reproducible and the comparison is real. Same seed, `PickConcept` inverse-frequency fix in place,
arm A `conceptSlots: 0`, arm B `conceptSlots: 3`.

| | Control | 3 concept slots |
|---|---|---|
| **Distinct cards in final decks** | **50** | **50** |
| Field spread | 25.7pp | 20.0pp |
| Viable | 7/8 | **8/8** |
| Min diversity | **53%** | 46% (at the 45% floor) |

**Coverage is identical.** Concept slots buy a tighter field and one more viable deck, at the cost
of diversity pinned to the floor. Nothing here is a reason to ship them.

### The decisive evidence is the decklists, and it is unambiguous

At generation 25 the synergy slots are piles. Synergy-1 is Frenzied Goblin / Noxious Grasp / Nissa
/ Path of Bravery / Dungeon Geists / Sublime Archangel / Aetherspouts / Thragtusk / Massacre Wurm —
no theme at all. All three carry 4x Nissa, 4x Sublime Archangel and Path of Bravery.

**Seeding builds archetypes; mutation dissolves them.** That is now measured rather than suspected,
on a valid instrument, and it isolates the remaining work precisely:

- `SeedConcept` works — pinned by unit test (>=16 on-concept cards, >3x ordinary seeding).
- `PickConcept` works — pinned by unit test (38/60 -> 50/60 committing to a tribe).
- **`SupportScore` at 4.0 cannot hold a concept against `CardDelta` at +/-25 over 25 generations**,
  which is exactly what it was sized not to do. The refine/abandon half — constrain mutation to
  preserve the concept, abandon only when the candidate set is exhausted — is the missing piece and
  is not built.

### A hypothesis formed and DISCARDED, recorded so it is not re-run

Narrow concepts need generic top-up to fill 36 slots, so three synergy decks should share the same
filler and converge. **Tested and false:** cards appearing in ALL slots of a kind are 3 for synergy,
3 for midrange, and 3 for the control arm — field-wide convergence on the best cards, not a concept
artifact. The closest pairs in arm B are Synergy-3/Midrange-D (46%) and **Midrange-D/Midrange-E
(46%)**, so the synergy decks are not similar to *each other*; arm B's whole field simply sits on
the floor.

### The remaining methodological caveat

Determinism removes RUN-to-run noise, not SEED-to-seed variation. This comparison is exact for this
seed and says nothing about how much of the 53% vs 46% is seed-specific. **A cheap instrument now
exists for the seeding question**: concept seeds can be compared at generation 0 across many seeds
by unit test, with no 40-minute run — measure what a change does to the seeds before paying to
watch 25 generations grind them down.

---

## 17. MEASURED on the full pool: no effect, and the CSC signal does not replicate

ALL (780 spells), 8 decks x 25 generations, same settings and seed as §16. Both arms report
**0 excluded, 9590 games**.

| | Control | 3 concept slots |
|---|---|---|
| **Distinct cards in final decks** | **45** | **43** |
| Field spread | 19.3pp | 18.6pp |
| Viable | 8/8 | 8/8 |
| Min diversity | 45% (floor) | 45% (floor) |

Coverage is slightly WORSE with concept slots, everything else is a wash. Combined with §16's
50 vs 50 on CSC: **concept slots do not improve pool coverage on either pool.**

### The CSC per-slot advantage was noise

| Pool | Synergy slots (mean) | Profiled/anchor slots (mean) |
|---|---|---|
| CSC | **54.0%** | 45.7% |
| ALL | **47.4%** | 53.6% |

The sign flips between pools. Concept slots are seeded FIRST in both (they get first pick before
the diversity constraint narrows), so ordering cannot explain a reversal. One seed per pool, and
the effect changes direction — that is what noise looks like, and the CSC number should not have
been read as a win.

### What the full pool actually builds

Piles of the format's best cards. Every synergy deck runs 4x Ancestral Recall or 4x Liliana of the
Veil or 4x Steppe Lynx; Siege Rhino, Basri Ket and Sublime Archangel recur. **No storm, no
reanimator, no affinity, no goblins** — and only **43-45 distinct cards out of 780** reach a final
deck, about 5.5% of the pool.

Whether that is a failure is exactly what §18 tests, because after the goblin result it can no
longer be assumed.

### An unexpected finding about the historical ALL record

`MtgSimulator/CLAUDE.md` records ALL runs as consistently bad — best 37.9pp spread, worst 61.4pp
with a deck collapsing to 8.6%, and "more time made ALL worse, not better". These runs give
**19.3pp and 18.6pp with 8/8 viable in both arms**, which is better than every CSC run in that
document too.

The obvious candidate is the determinism fix (§15): those historical runs discarded a
non-deterministic subset of presim games, and a deck collapsing to 8.6% is what a field looks like
when its card values were seeded from corrupted data. **Not proven** — the runs also differ in
generations, presim size and accumulated data — but the historical ALL numbers should be treated as
suspect rather than as a property of the pool.

---

## 18. THE REAL FINDING: the field has no external reference, and it is ~15pp weak

`MtgSimulator.Tests/ArchetypeChallenge.cs` plays hand-built decks from `DeckRegistry` against the
evolved ALL field (`metagame_all_20260830_023326.json`), 20 games per matchup, 160 games each:

| Deck | vs the field |
|---|---|
| **Zoo** (hand-built RGW aggro — 24 Plains, 36 efficient creatures) | **66.2%** |
| **Traditional Storm** | **63.1%** |
| Dragonstorm | 50.6% |
| Jund | 46.9% |
| Goblins | 46.3% |
| Reanimator | 44.4% |
| Affinity | 44.4% |

### Zoo is the control, and it overturned the obvious reading

With only the Storm number in hand the conclusion was "hill climbing fails at COMBO, where the
payoff is a cliff rather than a slope, and succeeds elsewhere". **Zoo disproves that.** It is the
least exotic deck in the registry — no combo, no threshold, nothing a one-card step cannot reach —
and it beats the field by MORE than storm does.

So the failure is not about reachability of exotic archetypes. **The field converges on something
roughly 15pp worse than a straightforward aggro deck that is entirely within reach of the existing
mutation operators.** That is a convergence problem, not a search-operator problem, and it means
the synergy work in §9-17 was aimed at a real but secondary issue.

Note also that not every hand-built deck wins — five of seven land at 44-51%. The AI is not
hopeless; it is specifically beaten by decks with a coherent fast plan.

### Why the mode cannot see this

**Fitness is measured entirely inside a closed field, and a round-robin averages exactly 50% by
construction.** When all eight decks converge onto 4x Steppe Lynx and 4x Liliana of the Veil, every
internal metric reports health — 8/8 viable, 18.6pp spread, diversity at its floor — while the
whole field sits 15pp below a human deck.

There is no absolute reference point anywhere in mode 6. It cannot distinguish "my decks are good"
from "my decks are equally mediocre", because the two produce identical numbers. The acceptance
rule inherits the same circularity: a mutant is kept when it beats the frozen field, so "better"
means "better at beating weak decks".

Observed convergence in the ALL run: **4x Steppe Lynx in 8/8 decks, 4x Liliana of the Veil in 8/8,
Ancestral Recall in 7/8** — about 10 of every deck's ~38 spells identical, held apart only by the
45% diversity floor. Card values are then measured inside that converged field, which is the
"card values absorb deck strength" loop already recorded, tightened to its limit.

### `ArchetypeChallenge` is the yardstick the mode never had

It was built to test one assumption about goblins. What it turned out to be is the only instrument
that answers "is this field any good" rather than "which of these eight is best". Run it against a
few reference decks after any evolution run, the same way
`SelfCheck_ADeliberatelyTerribleDeckLosesFarBelowTheViabilityFloor` is run to prove fitness can see
anything at all.

---

## 19. GAUNTLET: it works, +6.2pp — but not against the decks that were beating us

Fixed reference decks added to fitness (`Gauntlet.cs`, `gauntletGames` prompt, **0 = off and
bit-identical to before**). All 9 `DeckRegistry` decks, 4 games each, ALL pool, 8 decks x 25
generations, identical seed and binary. Both arms 0 excluded.

| | Gauntlet OFF | Gauntlet ON |
|---|---|---|
| Field spread | 19.3pp | **15.0pp** |
| Min diversity | 45% | 45% |
| Viable | 8/8 | 8/8 |
| **Field win rate vs the gauntlet** | **47.1%** | **53.3%** |

**+6.2pp against an external benchmark**, which is the first absolute improvement anything in this
project has produced. Per deck (field's point of view):

| Gauntlet deck | vs OFF field | vs ON field | change |
|---|---|---|---|
| Jund | 50.0% | 61.9% | **+11.9** |
| Reanimator | 52.5% | 63.1% | **+10.6** |
| Goblins | 46.9% | 56.9% | **+10.0** |
| Affinity | 51.9% | 61.2% | **+9.3** |
| Dragonstorm | 56.3% | 56.3% | 0.0 |
| **Traditional Storm** | **41.9%** | **41.2%** | **-0.7** |
| **Zoo** | **30.0%** | **32.5%** | **+2.5** |

### The hole, and it is a scoring design flaw not a search failure

**The two decks that beat the field are the two it did not improve against.** Zoo still wins 67.5%,
Storm 58.8%.

The cause is equal weighting. Fitness is the MEAN over 9 gauntlet decks, so a mutant gaining 10pp
against Jund is worth five times one gaining 2pp against Zoo — and gains against decks you already
beat are far cheaper to find. The field rationally optimised the easy majority and left the two
hard matchups alone. Averaging over a gauntlet rewards padding, exactly as averaging over a field
did.

Adoption is visible but partial, the same shape as the goblin package: Zoo's **Kird Ape, Wild
Nacatl and Qasali Pridemage** all appear in the gauntlet-on field, at 2-3 copies, alongside the
unchanged Steppe Lynx / Ancestral Recall / Liliana of the Veil core.

### Next iteration, in order of expected value

1. **Retire gauntlet decks the field already beats.** The treadmill, and it fixes this directly —
   a gauntlet of Zoo and Storm alone would put all the fitness pressure where the gap is.
2. **Score the worst matchup, not the mean.** Maximin: a mutant only improves if it raises the
   floor. Sharper than weighting and needs no tuning constant.
3. Weight by opponent difficulty. Same intent, one more constant to pick.

The report should also print the field-vs-gauntlet table automatically at the end of a run — it is
the only absolute number the mode produces and it currently requires a separate harness to obtain.

---

## 20. THE CONSTRAINT: the decks that beat the field were ILLEGAL

`Decklist.MinLands` was **20**. Every hand-built deck that beats an evolved field runs fewer:

| Deck | Lands | Precon round-robin |
|---|---|---|
| Traditional Storm | **12** | 1st, 63.1% |
| Zoo | **14** | 2nd, 62.5% |
| Affinity | **14** | 49.4% |
| Goblins | **16** | 4th, 53.8% (beats Storm 60%) |

**They were not hard to reach. They were outside the search space.** No amount of hill climbing,
gauntlet pressure, synergy detection or extra generations can reach a deck `Validate()` rejects.

This retroactively explains the whole investigation:

- **Why every evolved deck is a midrange pile** — at 20-26 lands a fast aggro deck is unbuildable;
  six to eight slots that would be threats are lands.
- **Why the gauntlet lifted Jund/Reanimator/Affinity by ~10pp but moved Zoo +2.5 and Storm -0.7** —
  the mid decks live inside the legal space, the top two do not.
- **Why decks finish pinned at exactly 20 lands** (Aggro-G, Midrange-E, and others). A binding
  constraint looks exactly like that.
- **Why Ancestral Recall and Liliana of the Veil dominate every list.** They are not a mistake:
  inside a 20-land deck with a slow clock, card advantage really is the right plan. The card values
  were correct *for the deck space they were given*.

### Why 20 was wrong for THIS engine

The constant was justified as "real constructed mana bases" — an analogy to paper Magic. Two rules
here break that analogy, both pushing the floor down:

1. **No colours.** A land is pure quantity, never fixing, so the usual reason to overshoot is absent.
2. **Every opening hand contains three lands by rule.** 20 of 60 on top of a guaranteed three is far
   more than an aggressive deck wants.

### Now configurable

`MinLands`/`MaxLands` are `static readonly`, overridable by `MTG_MIN_LANDS`/`MTG_MAX_LANDS`,
defaulting to the old 20/26. **This exists so an A/B runs both arms on ONE binary** — rebuilding
between arms is how the stale-`bin/` trap gets in, and this project has paid for that twice.

### The open risk in testing it

The mutator moves lands **one at a time**, so walking a 20-land seed down to 14 needs a chain of
accepted mutations, each of which must look better in isolation. If the intermediate steps score
badly, widening the floor alone will not be enough and the fix is to let SEEDING start lower —
`LandsForCurve` already derives the count from the curve target, so an aggressive seed would pick a
low count automatically once the floor permits it.

---

## 21. MEASURED: lowering the land floor works, and it is the biggest single win

ALL pool, 8 decks x **40 generations**, gauntlet on (9 decks x 4 games), identical seed, ONE binary
with the floor set per arm via `MTG_MIN_LANDS`.

### The constraint was binding, and the mutator could walk out on its own

```
floor 20:  21 21 23 23 20 26 20 22    mean 22.0
floor 12:  17 19 21 14 20 22 14 22    mean 18.6
```

Four of eight decks went below the old floor and **two landed on exactly 14 — Zoo's land count**.
The risk flagged in §20 (that one-land-at-a-time mutation could not walk 20 down to 14) did not
materialise; no seeding change was needed.

### Field vs gauntlet, per deck (field's point of view)

| Gauntlet deck | floor 20 | floor 12 | change |
|---|---|---|---|
| Jund | 55.0% | 66.2% | **+11.2** |
| **Zoo** | **38.1%** | **45.6%** | **+7.5** |
| Goblins | 53.7% | 60.6% | +6.9 |
| Affinity | 60.6% | 66.2% | +5.6 |
| Reanimator | 63.1% | 61.9% | -1.2 |
| Dragonstorm | 56.2% | 53.7% | -2.5 |
| **Traditional Storm** | **44.4%** | **41.2%** | **-3.2** |
| **OVERALL** | **53.0%** | **56.5%** | **+3.5** |

**The gain lands exactly where the mechanism predicts.** Zoo's edge was a 14-land aggressive curve
the field was forbidden from building, so freeing the floor closes that gap (+7.5). Storm's edge is
a combo, not a land count, so it does not move (-3.2) and is now the largest remaining gap.

### Cumulative, against an external benchmark

| Configuration | Field vs gauntlet | vs Zoo |
|---|---|---|
| no gauntlet, 25 gen, floor 20 | 47.1% | 30.0% |
| gauntlet, 25 gen, floor 20 | 53.3% | 32.5% |
| gauntlet, 40 gen, floor 20 | 53.0% | 38.1% |
| **gauntlet, 40 gen, floor 12** | **56.5%** | **45.6%** |

**+9.4pp overall and +15.6pp against Zoo.** Attribution: gauntlet +6.2, land floor +3.5,
generations **+0**.

### More generations does nothing — now measured directly

40 generations against 25, same settings and seed: spread 15.0 -> 16.4pp, diversity 45% -> 45%,
viable 8/8 -> 8/8, acceptance 3/8 -> 3/8. It never stops accepting mutations; it simply does not
go anywhere. Do not spend compute on generation count.

### Side effects worth keeping

- **Pool coverage rose**: 14-18 distinct cards per deck against 12-13 at floor 20. Fewer land slots
  means more spell slots.
- **Zoo's cards are properly adopted now**, not dabbled in: 4x Wild Nacatl, 2x Goblin Guide,
  2x Qasali Pridemage in one deck, where every previous run managed 2-3 copies of one of them.
- **Sol Ring and Mox Pearl appear** for the first time — fast mana becomes worth playing once the
  deck is not drowning in lands.

### What remains

Storm at 41.2% is the biggest gap and it is NOT a land-count problem. `MinLands` should probably
default to ~14 rather than 12 or 20, but that wants its own sweep rather than a guess.
