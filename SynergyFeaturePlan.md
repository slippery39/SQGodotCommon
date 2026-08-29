# Feature-Based Synergy for Constructed Evolution — Handoff

**Status:** planned, not started. Everything described under "What exists" is built, tested and
measured; everything under "The proposal" is not.

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

**The AI ceiling is real and shapes every measurement.** A 2-turn beam search cannot pilot storm
or a full combo engine. Dragonstorm rates 1/780 partly *because it is unpilotable by this AI*,
not because it is a bad card. **A feature table will learn that card-advantage mechanics
underperform** — true for this engine, wrong for Magic. Write that on the tin.

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
