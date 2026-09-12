---
paths:
  - "MtgSimulator/Evolution/MetagameEvolver.cs"
  - "MtgSimulator/Evolution/Deck*.cs"
  - "MtgSimulator/Evolution/Constructed*.cs"
  - "MtgSimulator/Evolution/IdentityValues.cs"
  - "MtgSimulator/Evolution/PreSimulation.cs"
  - "MtgSimulator/Evolution/Gauntlet.cs"
  - "MtgSimulator/Evolution/MutationLog.cs"
  - "MtgSimulator/Evolution/Decklist.cs"
  - "MtgSimulator/ColorIdentity.cs"
  - "MtgSimulator/ManaBase.cs"
  - "MtgSimulator/Decks/*.cs"
  - "MtgSimulator.Tests/*Archetype*.cs"
  - "MtgSimulator.Tests/*Identity*.cs"
  - "MtgSimulator.Tests/*Metagame*.cs"
  - "MtgSimulator.Tests/*ManaBase*.cs"
  - "MtgSimulator.Tests/*Presim*.cs"
---

# MtgSimulator — constructed metagame evolution (mode 6)

Loaded when you touch the evolver, the deck builder, or the constructed value tables.
Measured run results live in `docs/findings/evolution.md`.

## Phase two: engine slots in mode 6, holding a POOL rather than a decklist

`MetagameEvolver(enginesPath:)` seeds the top-**LIFT** archetypes from a mode 7 report into the
first slots of the field. The console prompts for it: *"Engine file from mode 7? (blank = none)"*.

```bash
export MTG_MIN_LANDS=12
printf '6\n\n4\n8\n25\n3\n6\n20\n0.45\n800\nY\nY\n0\n4\nsim_results/engines_all_<stamp>.json\nmyseed\n' \
  | dotnet run --project MtgSimulator.Console -c Release
```

**A `DeckCore` narrows the card pool `Mutate` draws from.** One clause at the top of `Mutate`
filters `spells` to the core's own slot cards, and every operator — Swap, Recount, Package,
AdjustLands and the `Fill` they share — takes its candidates from that list, so nothing outside the
archetype can enter by any path. No operator needed to learn about engines.

**This paragraph described a `DeckBuilder.EngineIdentity` and an `EngineIdentityTests` for a whole
session, and NEITHER EXISTED** — a grep over every `.cs` in the solution found only prose. The core
protected what could be CUT (`ProtectedIn`) and nothing protected what could be ADDED, so every free
slot was refilled from the format: the good-stuff failure re-entering through the one door the
constraint did not watch. **Check that a documented mechanism exists before reasoning from it.**

## It shipped first as a 60% quota, and the drift went straight into the allowance

The reasoning was that a deck should be able to pick up metagame answers, so 40% of spells were
left free. Measured on a real run, the storm slot spent all of it:

```
Steppe Lynx x3, Gravecrawler x3, Liliana of the Veil x2, Zombie Horde Leader x2,
Cathartic Reunion x3, Incorrigible Youths x1     — 14 of 43 spells, and Tendrils gone
```

**29 of 43 in-pool = 67%, legal against the 60% floor the whole time**, so nothing reported a
problem. A budget for drift gets spent on drift.

Narrowing the pool is also strictly better mechanically than grading the result:

- **Monotone.** A quota is checked after the fact, so a starting deck already below it freezes the
  slot solid — every proposal rejected, forever, silently. `SeedConcept` tops up from the whole
  pool when a concept cannot fill 36 slots, so impure starts are expected and that failure was
  reachable.
- **No wasted proposals.** A rejected mutant costs a mutation slot for that generation.
- **It converges INWARD.** `PickWeakest` may still cut anything, and nothing outside can return, so
  an impure start cleans itself up. `MetagameEvolver` prints starting purity for exactly this
  reason — the first version's failure was invisible, and a number is what makes it checkable.

Cutting stays unconstrained, so evolution can still discover that a storm deck wants fewer rituals.
It cannot discover that it wants Steppe Lynx.

`MutationNeverDrawsFromOutsideTheCoresPool` chains 60 generations and asserts nothing outside the
pool ever appears, with `WithoutACore_MutationDoesWanderOutsideThatPool` confirming that
unconstrained mutation *does* leave the same card set — without it the test would pass on a mutator
that never changes anything.

**The pool must be wide enough to fill a deck or the control measures the fixture.** At six cards
every swap produces an illegal list, `Mutate` returns null, and "the deck did not move" says nothing
about the mutator. `TheSameMutationsStillExploreInsideThePool` uses 24 and previously asserted that
a core "leaves everything else free" — a claim the pool lock deliberately makes false.

## A slot carries a FLOOR and a CAP, and they answer different questions

`CoreSlot.MinCopies` is the identity floor — never cut below, and what `ProtectedIn` locks.
`CoreSlot.TargetCopies` is the cap the fill aims for, defaulting to unbounded.

**Collapsing them produced 12 Dragons where a real list plays six.** With only a floor, the fill ran
to 60 cards best-first inside the archetype pool, so a slot that qualified kept getting topped up.

The cap is set by *what kind of question the demand asks*, which is derived rather than declared:

| Demand | Cap | Why |
|---|---|---|
| a COUNT (storm, artifacts, a tribe) | **unbounded** | a storm deck wants every ritual it can hold |
| ONE OBJECT you FETCH (a Dragon) | **the floor** | past "enough survive to be found", a further copy is a card you did not want to draw |

Measured — Dragonstorm at `MTG_MIN_LANDS=12` went from 12 Dragons to **4**, and the freed slots
filled with Mox Pearl, Lotus Bloom and Consult the Drowned. That is the trade the fill could not
previously make.

**The floor is where the cap STARTS, not where it belongs.** It is the number an optimiser should
move, and deriving it means there is something honest to move away from. Note `Satisfy` overshoots
it slightly — it tops a card to a full playset once chosen, so a floor of 3 yields 4 — which is why
the lists read as playsets rather than as odd counts.

**Engine slots are never culled.** Mode 7 already judged the archetype on whether it ASSEMBLES; the
win rate is here to tune it against the field, not to decide whether it deserves to exist. A
half-built combo deck loses every game, so a viability floor would delete exactly the decks the
feature exists to keep — which is what every unconstrained run has done. The rate is still reported.

**So the only way a dead engine leaves the field is the exclusion list**, and it has to leave,
because it does not merely waste its own slot. Mere-Storm read 8.2% and 0.0% across two CMB runs and
was **every other deck's best matchup** — the field spread, the viable count and every overall rate
were partly measured against a punching bag. `MetagameEvolver(excludedEngines:)` takes concept names
(console: *"Engines to exclude?"*), matched case-insensitively with a leading `Engine-` tolerated so
a deck name pasted out of a previous report works. An excluded slot falls back to a curve deck,
which is the honest control.

Three properties, all deliberate:

- **Excluded BEFORE the tier cut.** Filtering the chosen slots instead would let a dead archetype
  consume one of `usable * TierBreadth` places, so the list would quietly narrow the field it exists
  to widen.
- **Named on the console, not counted.** A misspelt exclusion is indistinguishable from an effective
  one in the final matrix, so unmatched names are warned about by name.
- **The run prints its own next exclusion list** — engine slots that finished below the viability
  floor, formatted ready to paste. Deciding an archetype is dead is still the reader's call; only
  the retyping is removed.

`EngineExclusionTests` pins it, with `WithoutAnExclusionList_EveryEngineIsStillSeeded` as the
vacuity guard — a `LoadEngines` that returned nothing would pass the exclusion assertion alone.
Cross-run persistence of the list is deliberately **not** built; the prompt plus the printed line is
the whole feature.

The last slot is never an engine: it is the permanent wildcard, and replacing the exploration arm
with a fixed archetype removes the only slot that can find something nobody has thought of.

**Known: LIFT-ranked selection can pick near-duplicate concepts.** The first real run took both
`Subtype{Spirit}` and `Spirit ∧ OnBattlefield ∧ ControlledByYou` into adjacent slots. The seeded
field still cleared the diversity floor, so this is untidy rather than broken; a distinctness pass
over chosen concepts is the fix if it ever costs a slot that matters.

**Re-baseline before any phase-two A/B.** Steppe Lynx, Liliana of the Veil and Chandra's Regulator
were nerfed after every number in `HANDOFF-ConstructedEvolution.md` was measured. Phase one does
not depend on this — a stale value table shifts which cards `SeedConcept` picks, not whether an
engine assembles.

## Constructed Metagame Evolution

Mode 6. Eight AI-built 60-card decks play each other, mutate toward better matchups against the
rest of the field, and the run outputs a metagame — decklists plus the matchup matrix. The first
thing in the project that BUILDS a constructed deck rather than measuring a hand-written one.

Each generation: every deck proposes M mutants, every candidate plays the frozen field, the best
improving mutant that stays distinct is accepted, and at most one non-viable deck is culled.
Phases mirror `DraftTrainer` exactly — sequential proposals, one parallel game batch into a
pre-allocated array, sequential fold-in.

## Common random numbers are what make it work at all

A mutation is worth ~1-3pp. An unpaired 42-game evaluation has a standard error near 7pp, so the
accept/reject decision would be a coin flip and thirty generations of it would be a random walk
that looks like evolution.

`MetagameEvolver.Seed(gen, deck, opponent, repeat)` deliberately does **not** take the candidate
index, so a parent and all of its mutants play identical opponents on identical shuffles with
identical AI RNG. Shuffle and search variance is shared between the arms and cancels in the
difference. **If one detail of this mode is ever "simplified", this is the one that silently
invalidates every number it prints.**

## A round-robin averages 50%, so the 40% target is a floor, not a goal

The sum of win rates in a closed field is exactly `50% x N` by construction — "every deck at 40%"
is unreachable arithmetic. It is implemented as a **viability floor**: a deck that cannot clear
40% against the field is a non-viable list and gets replaced. Grace period of 5 generations (a
fresh seed starts bad by definition) and at most one cull per generation (the field has to be
re-measured after any replacement).

The report also prints each deck's BEST matchup, because a deck under the floor that still
counters something is a real archetype and one that beats nothing is not.

## Seeding is a concept, not 36 random cards

Anchor (sampled by card value) → kernel of its best-evidenced synergy partners → curve target →
weighted fill. Archetypes fall out of the curve and the kernel; **nothing is hand-labelled**, and
nothing should be. Both halves already existed in the trained model: per-card rates say what is
good, `PairStat` says what wants to be together, and both were found unsupervised.

Deck 8 is a permanent **wildcard** slot: uniform anchor ignoring card value, double synergy
weight. It is often bad and gets culled, which IS the exploration. Without a slot that ignores
what the prior already believes, the field can only refine cards the prior already liked, and a
combo deck of individually-mediocre pieces is unreachable.

## The synergy gate was set to a value nothing could reach

**`MinPairGames` was 200 and that silently disabled every synergy path in the mode for three
full runs.** The CSC draft table's busiest pair has **166** games (median 53, p99 108), so
nothing cleared it: `SynergyDelta` returned 0 for all 83 028 pairs, `TopPartners` came back
empty so the seeder's kernel never formed, and both fill and cut scoring collapsed to card
quality plus curve.

The output was piles of individually-strong cards with visible anti-synergies — Atog with no
artifacts, a sweeper alongside the deck's own mana dorks. **It was caught by reading the
decklists, not the code**, which is the same lesson as the inert-card audits: a wrong-but-
plausible output is the only symptom a dead code path produces.

Three compounding causes, all worth recognising again:

1. The 200 was copied from `pairShrinkK`, a **shrinkage constant** — a different quantity that
   happens to be a number about pairs.
2. The unit test used inline data with `Games = 4000`, so it passed while no real pair could
   clear the gate. A test measuring nothing, and the second in this file.
3. Nothing reports a synergy term that is uniformly zero. `TopPartners` returning empty is
   indistinguishable from "this card has no partners".

Now 50, calibrated against measured distributions rather than analogy:

| table | pairs | p50 | p99 | max | ≥50 games |
|---|---|---|---|---|---|
| CSC draft | 83 028 | 53 | 108 | 166 | 56% |
| constructed (one run) | 3 484 | 119 | 5 768 | 13 704 | 56% |

At 50, 46 868 CSC pairs clear and the top measured synergies are mechanically real (Siege-Gang
Commander + Volley Veteran, Siege-Gang + Swiftfoot Boots). `RealisticPairEvidence_ClearsTheGate`
and `TopPartners_FindsAKernel_AtRealisticEvidence` use volumes the real data reaches, so raising
the gate back fails a test instead of going quiet.

**Before trusting any synergy work here, check that pairs actually clear the gate on the model
you are using** — the one-liner is in the Draft Training section and takes seconds.

## Per-deck history: synergy-aware cutting

The global pair table cannot answer "is this combination pulling its weight in THIS deck". It is
spread over 83 000 pairs at a median of 53 games, and **on the combined pool 58% of the pair
space is cross-set and can never have data at all**, because sets are drafted separately — Atog
is in LEG and every artifact it wants is in CSC, a pair that has never existed in any draft.

`DeckHistory` answers it per deck slot instead. Within one deck the same question is far better
conditioned: ~15 distinct cards is ~105 pairs rather than 83 000, every one a 4-of drawn in most
games, so pairs reach hundreds to thousands of games within a few generations.

- **Cutting only.** `PickWeakest` adds `KeepScore`; selection stays on overall win rate plus
  curve, because a card not yet in the deck has no history in it.
- **`PairDelta` sums rather than averages**, so a card carrying four winning pairs is harder to
  cut than one carrying a single lucky pair — the linchpin survives an unremarkable solo rate,
  which is exactly what a synergy piece looks like and what pure card quality cuts first.
- **Summing is safe here where it was not globally.** The measured harm was from adding ~27
  thin cross-pool deltas; these are dense, few, about the deck being scored, and carry two
  orders of magnitude more evidence per pair.
- **Reset on cull**, and pairs whose partner has been cut are ignored — a record about a card no
  longer in the deck is not evidence about the deck now.

Baseline is the deck's OWN base rate, not an independence baseline: inside one deck the question
is simply whether games drawing both go better than that deck's average game, and the cards'
individual rates are already carried by `CardDelta`.

## Selection scores ABSOLUTE joint win rate, never deviation from a baseline

`ConstructedValues.JointDelta` (how the pair does against the base rate) is the selection
criterion. `SynergyDelta` (how it does against `ExpectedPairRate`) is a diagnostic and **must not
be used to choose cards**.

The reason is not a preference. For two individually-terrible cards the independence baseline is
catastrophic, so a pair that merely performs badly scores as strong positive synergy — **the
worst cards in a format are the easiest to show synergy for.** Measured:

| Pair | Baseline expects | "Synergy" | Absolute |
|---|---|---|---|
| Dragonstorm + Tendrils of Agony | −35.4pp | **+8.91pp** | **−26.11pp** |
| Thoughtcast + Dragonstorm | −33.7pp | +5.08pp | −28.26pp |

That ranking built a 4x-Dragonstorm storm deck which finished at **8.6%** and sat near-dead for
30 generations, dragging the whole field's numbers with it.

Absolute scoring also removes the need for a separate card-quality gate: a card that is weak
alone but genuinely enables the anchor still qualifies, one that is bad alone AND together
cannot. Both directions are pinned —
`TwoTerribleCards_AreNotSelectedAsPartners_EvenWithHugeSynergy` and
`AGenuineEnabler_IsStillSelected_EvenIfWeakAlone`. Without the second, the fix would silently
become "only ever pair already-good cards".

**`DeckFit` averages, it does not sum.** The summed version was unbounded in deck size: a
10-card, 40-copy deck produced ~+290 against card values in the ±25 range, so card quality became
rounding error and a deck rated Dragonstorm (+261 combined) above Ancestral Recall (+240).
Averaging keeps it in `CardDelta` units so a weight of 1.0 means "these matter equally".

## What per-deck history does NOT fix

The user-visible complaint was **anti-synergy**, and half of it is still open. Two shapes:

| | Example | Expressible as a pair? |
|---|---|---|
| Genuine anti-synergy | Wrath + your own mana dorks | Yes, once measured |
| Missing critical mass | Atog with no artifacts | **No** |

Atog does not want *one* artifact, it wants ~15. That is a threshold on a deck, and no pairwise
statistic expresses it at any sample size. The planned answer is **rules-derived** synergy read
off components, which needs no data and therefore works on cross-set pairs:

- `DestroyCreatureAction` + `AllValid(Creatures())` → negative per creature in your own deck
- a cost or count filtered on a subtype (Atog's `SacrificeAdditionalCost`, tribal lords) →
  positive per matching card

This is **not** the card labelling that was rejected at design time — nobody types "aggro" on a
card. The components already encode the behaviour and cannot rot, because they *are* the card.

**Deferred deliberately. The full plan, with measured numbers, costed options and the trap list,
is `SynergyFeaturePlan.md` at the solution root.** Start there rather than re-deriving it.

## The synergy gate is load-bearing

This document records that `synergyWeight` cost win rate at every value tested. **That finding
does not transfer here, and the distinction is the whole reason this works.** It measured summing
~27 noisy pair deltas to rank one draft pick — noise accumulates as √27 and swamps a small signal.
This mode uses only the top few pairs off a single anchor, gated at `ConstructedValues.MinPairGames`
(200), which is the confidence-gating listed above as untested improvement path #1.

**Drop the gate and this becomes the thing that was disproved.** `ThinPairs_ContributeExactlyZeroSynergy`
pins it, and `WellEvidencedPairs_ReportRealSynergy` pins that it is not so tight nothing fires —
a test asserting only the zero would pass on a synergy term that never works.

## Limited values are not constructed values

The draft model measures a card in a 40-card, 23-spell, near-singleton drafted deck. Constructed
is 60 cards, 4-ofs, curated. Big vanilla creatures and grindy card advantage rate well in limited
and poorly in constructed; narrow combo pieces rate near zero in limited and can define a
constructed format. **Seeding from a limited prior and never correcting it just builds more
consistent draft decks**, which is the failure this mode exists to avoid.

Fitness is already immune — it is the win rate in constructed games. The exposure is
*reachability*: hill climbing from limited-good starting points may never find the combo deck. So
the mode counts its own games into `sim_results/constructed_values_<set>.json` and shrinks toward
what it measures.

**The blend needs no decay schedule.** It is `DraftTrainingData.Shrink` with the draft-derived
rate passed as the prior instead of 0.5: few constructed games ⇒ the draft value, many ⇒ the
measured constructed value. With no draft model at all every card scores 0, seeding is
quality-blind, and the table builds from scratch — a supported mode, offered as `n` at the
"Seed from the draft model?" prompt, and the honest control arm if the prior is ever suspected of
dominating a result.

`CardShrinkK` is 25, matching `DraftPickers`. That means a single game moves a valuation ~2.6pp
and one generation of play (~168 deck-games for a card in a deck) flips it outright. Both are
pinned, in both directions, by `ASingleConstructedGame_BarelyMovesTheValuation` and
`OneGenerationOfEvidence_MovesTheValuationSubstantially`. **Do not retune k on intuition** — the
same rule as `synergyWeight`.

## Two self-checks before trusting any run

| Check | Question | Where |
|---|---|---|
| 1 | Can the fitness measurement see anything? | `SelfCheck_ADeliberatelyTerribleDeckLosesFarBelowTheViabilityFloor` — 1/1s for 5 against 3/3s for 2. **Measured 0/40.** `[Explicit]` |
| 2 | Did it escape the limited prior? | The run's own "Constructed vs limited" section: Spearman + top movers |

Self-check 2 is the acceptance test for the whole limited-vs-constructed concern, and the run
warns on its own output above 0.95:

| Reading | Means |
|---|---|
| Spearman ≈ 1.0, no movers | **Failing** — constructed data is not displacing the prior |
| ~0.5-0.8 with a coherent mover list | Working — the format has its own valuations |
| ≈ 0 early | Noise, not signal — check games/card first |

Sanity-check the mover list by eye: 4-of-dependent and narrow-but-powerful cards rising, big
vanilla creatures and slow card advantage falling.

## The combined pool

`SetRegistry.Combined` ("ALL", 785 cards) is every registered set as one format. It does not
contradict the no-merging rule on `SetRegistry` — that rule protects the trained *picker*, which
would score a whole set at the prior and never pick from it. A merged pool is fine wherever the
model is a starting prior rather than the pick policy. It is offered to mode 6 only, via
`ReadSet(includeCombined: true)`.

**18 names collide** and are resolved last-registered-wins, on the rule that two cards printed
with the same name do the same thing and the most recent printing is current. Every replacement
is logged, because a silent behaviour swap between two same-named cards is otherwise invisible.
Do not prefix names with set codes — that breaks the name keying every per-card table depends on.

## The pilot is NOT the ceiling — the search operator is

This section used to claim that a 2-turn lookahead cannot pilot combo, so a metagame skewed to
creature aggro/midrange was inevitable. **That is false and it misdirected a whole session.**
The precon decks in `DeckRegistry` — Traditional Storm, Reanimator, Affinity, Goblins,
Dragonstorm — were piloted well enough to win games from the day they were written, and Storm had
to be **balanced down** for being too strong. Not optimal piloting, but nowhere near unable.

What actually produces the midrange piles is the **mutation operator**, and the difference matters
because one is unfixable and the other is a search change:

Real deckbuilding commits to a concept, jams every card in the pool that serves it, measures, and
then prunes *within* the concept — abandoning it only once the pool is out of options. Nobody
arrives at Goblins by adding eight Goblins to a midrange deck one at a time; that tanks the win
rate at every intermediate step, which is exactly what the accept-if-better rule then rejects.

`Mutate` random-walks: it proposes small edits and keeps whatever raises the win rate this
generation. A half-built synergy deck is worse than the midrange pile it came from at every step
of the way, so the climb never reaches it and the pieces get pruned back out. `Package` was the
first attempt at this and is too small — it brings in an anchor plus up to two partners, where a
concept needs its whole critical mass at once and then needs to be **held** while it is refined.

The symptom to recognise: decks that read as halfway to an archetype. A Dragonstorm deck with **no
dragons in it at all** — a literally blank card holding a slot for generations — and Thoughtcast
in decks with no artifacts. A card whose demands are satisfied at zero is dead, and nothing in the
mode currently notices.

## The mutation log — what the search TRIED, not only what survived

`MutationLog` records every proposal: generation, slot, the cards added and removed, the parent's
rate, the candidate's rate, and why it ended where it did. Printed as a summary plus a per-card
table, and written whole to `sim_results/mutations_<set>_<stamp>.csv`.

**It exists because a final decklist cannot answer the question that keeps being asked of it.**
"Wirewood Conduit is not in the elf deck" has at least two causes with opposite fixes — never
proposed (a selection-heuristic problem) or proposed, played and cut (a survivability problem) —
and this document argued the first from the fill rule for a whole session with nothing measuring
either. The CSV settles it with a sort.

Four things about how it is computed, each of which would otherwise mislead:

- **`ParentRate` is the parent's rate in the SAME generation.** Common random numbers make that a
  paired comparison — the parent and all its mutants played identical opponents on identical
  shuffles — so `Delta` is the mutation's effect with shuffle and search variance cancelled.
  Against the previous generation's number it would be neither paired nor meaningful.
- **`MeanDelta` per card is over PROPOSALS, not over accepted ones.** Acceptance is conditioned on
  beating the parent, so averaging the accepted rows reports every card as positive by
  construction — the same selection artifact this file records for card values measured inside the
  decks that played them. A card tried three times and kept once is a card the search likes and the
  field does not.
- **`too-similar` is a separate outcome from `rejected`.** They are different failures: one is
  losing on fitness, the other is a field pinned by its diversity floor rejecting proposals it
  agreed were improvements. Collapsing them hides the run this file already records whose diversity
  sat exactly on the constraint for all twelve generations while every other number read healthy.
- **A recount is rendered as a change.** `Mutate` moves 3x to 4x far more often than it swaps a card
  in, so a diff over card SETS would make the most-used operator invisible. Land changes likewise,
  as a synthetic `Land` entry that `ByCard` filters back out.

Culls are logged too (`reseeded`), with the full old→new diff — a cull is the largest edit the
search makes, and omitting it makes a card look never-tried when its whole deck was replaced under it.

**Read the rejects first.** The accepted rows only say what worked; the rejects say what the mutator
keeps reaching for and failing with.

## MEASURED: a slot can spend its whole mutation budget producing nothing

**10 decks x 12 generations on DES, 12 204 games, 6 engine slots, `MTG_MIN_LANDS=12`, seed 7.**

| slot | core pool | real proposals | final |
|---|---|---|---|
| Engine-Watcher of the Spheres | 129 | **32** | 47.2% |
| Engine-Drogskol Captain | 54 | **33** | 46.1% |
| Engine-Wirewood Herald | 24 | 7 | 59.4% |
| Engine-Xathrid Necromancer | 130 | 6 | 62.8% |
| Engine-Master of the Wild Hunt | **2** | **3** | **26.1% NON-VIABLE** |
| Engine-Sanguine Reciprocity | **5** | **2** | **32.2% NON-VIABLE** |

`MutantsFor` gives a deck at or below 45% the **full** budget, and both non-viable slots sat there
all run — so each was offered ~36 mutation attempts and used 3 and 2 of them. **They were frozen at
their seeded list for twelve generations and then reported as non-viable archetypes.** Xathrid and
Wirewood are the other half of the rule: both spent most of the run above `StableRate` (0.60), where
the budget is deliberately **zero**.

This matters directly for the exclusion list: a run would have told you to permanently exclude two
archetypes that were never optimised at all. **Check a slot's `dry` count before excluding it.**

`no-proposal` rows are now logged for exactly this reason. The finding had to be inferred from
missing rows the first time, which is how it nearly went unnoticed.

## The bug: a mutation budget spent on calls rather than on mutants

**`MutantsFor` returns "how many mutants to EVALUATE" and the loop spent it as "how many times to
call a function that often fails".** `Mutate` rolls ONE operator and returns null whenever that
operator cannot produce a legal, distinct, core-holding, profile-respecting list — and the slot was
consumed either way. No retry.

Null rates per single call, measured by `MutationYieldTests.WhereDoTheNullProposalsComeFrom` over
600 mutations of each deck a real run produced (DES, `MTG_MIN_LANDS=12`):

| condition | null rate |
|---|---|
| no core, profile `Any` | 3–9% |
| `Control` profile, deck below its band | **34%** |
| engine core, 508 cards in pool | 18% |
| engine core, **5** cards in pool | **95%** |

**Two independent causes, and the curve profile was the one nobody suspected.** A `Control` deck
sitting below its band may only move toward it, so roughly every mutation that lowers the curve is
discarded — an 11x multiplier on the null rate with no pool lock involved at all. That is what put
a curve slot at 0 real proposals against 6 dry and made "narrow core pool" look like the whole story.

`TryMutate` re-rolls up to `MutationRetries` (20). Same configuration before and after:

| slot | before real/dry | after real/dry |
|---|---|---|
| Engine-Sanguine Reciprocity (5-card core) | **0 / 6** | 3 / 4 |
| Control-C (no core, Control profile) | **0 / 6** | **5 / 1** |
| Engine-Spirit Bonds (508-card core) | 8 / 1 | 9 / 0 |
| Aggro-D, Midrange-E | 7 / 0, 5 / 0 | unchanged |

20 proposals to 29; dry 13 to 5. Pinned by `ANarrowPoolStillYieldsProposals_RatherThanBurningTheBudgetOnNulls`,
which measures 92/200 at one attempt against 200/200 at twenty.

**It does NOT make a narrow core searchable and must not be read that way.** A five-card pool
genuinely has few distinct legal lists — Sanguine Reciprocity still reads 4 dry — so re-rolling
stops waste, it does not invent options. The `dry` column still reports what is left.

**Runs before and after this are not comparable at a fixed seed.** A re-roll consumes more draws
from the mutation RNG, so every downstream decision shifts. That is a behaviour change, not a
regression; re-baseline rather than diffing across it.

## Re-baselined: the search now runs, and the verdicts did not move

Same configuration re-run — 10 decks x 12 generations, DES, same engine report, Mere-Storm excluded,
seed 7.

| slot | core pool | real before | real after | rate before | rate after |
|---|---|---|---|---|---|
| Engine-Master of the Wild Hunt | 2 | **3** | **22** | 26.1% | **26.7%** |
| Engine-Sanguine Reciprocity | 5 | **2** | **26** | 32.2% | **35.0%** |
| Engine-Wirewood Herald | 24 | 7 | 18 | 59.4% | 48.3% |
| Engine-Xathrid Necromancer | 130 | 6 | 9 | 62.8% | 67.2% |
| Engine-Drogskol Captain | 54 | 33 | 36 | 46.1% | 40.0% |

**The two frozen slots got a real search and stayed non-viable** — 22 and 26 proposals, and the rates
moved +0.6pp and +2.8pp. So the caveat this file recorded against the exclusion list is **resolved
for these two**: they are genuinely weak in this field, not merely un-optimised, and excluding them
is now a supported decision rather than a guess. The general rule stands — **read the `dry` column
before excluding** — but a slot with dry near zero has had its chance.

**The fix costs time, which is the honest trade**: 12 204 games in 19.3 minutes became 16 956 in
31.2. More real proposals means more games to evaluate them, and that is what the budget always
meant to buy.

**The best deck in the field is a plain curve slot.** `Midrange-H` finished at **77.2%**, clear of
every discovered engine (next best 67.2%), on 4 real proposals — it sat above `StableRate` almost
throughout and was deliberately left alone. That is the good-stuff-beats-archetype tension this
whole mode exists to study, now measured with a search that actually runs.

## MEASURED: (d) is answered — never proposed, and NOT because of the pool lock

Wirewood Conduit appears in **zero** rows across 12 generations, and it **is** one of the 24 cards in
the Wirewood Herald core's pool — so the pool lock is not what excludes it. The elf slot's seven
proposals touched five distinct cards: Fauna Shaman, Elvish Archdruid, Elvish Visionary, Llanowar
Visionary, Yeva's Forcemage.

So the handoff's "considered but not valued" is **wrong as stated** — it was never considered. The
cause is the two selection points that both rank by standalone value (`Complete`'s fill at seeding and
`Mutate`'s `Fill`). **Survivability is not implicated**: the card was never in a deck to die.

**Confirmed under a working search.** The mutation-budget fix took the elf slot from 7 proposals to
**18**, touching 9 distinct cards instead of 5 — and Wirewood Conduit still appears in **zero** rows
across the whole run. So this is not budget starvation; it is the selection heuristic, and it is the
same problem as items (c) and (f). A 1/1 mana dork never surfaces in a value-ranked fill however many
attempts it gets.

## Deck profiles

Every non-concept, non-wildcard slot cycles through `DeckBuilder.DeckProfile` — **Aggro** (curve
2.0–2.7), **Midrange** (2.4–3.6), **Control** (3.3–4.5) — and is named for it. `Any` is the
pre-existing behaviour (uniform 2.0–4.5) and remains the default, so callers that do not ask are
unaffected.

Three bands of one number that already existed, not a new mechanism: `curveTarget` already drives
land count via `LandsForCurve`, so an aggro deck is a low curve with fewer lands. There are no
colours, and anything richer ("play removal", "play card draw") would be the hand-labelling this
project rejects. `ProfiledSlots_ActuallyBuildToTheirCurveBand` pins both the spell curve and the
land count.

**The labels previously lied** — slots were named `Midrange-A` while drawing uniformly from
2.0–4.5, so a "midrange" slot could come out with an aggro curve and the report still called it
midrange. Profiles are cycled rather than split by a fixed count because deck count is a parameter.

**Synergy slots take no profile**, deliberately: affinity and reanimator have high printed curves
and low real ones, so `SeedConcept` uses the concept's own average cost instead.

## The best decks were ILLEGAL: MinLands was the binding constraint

`Decklist.MinLands` was 20. **Every hand-built deck that beats an evolved field runs fewer lands** —
Traditional Storm 12, Zoo 14, Affinity 14, Goblins 16 — so they were not hard to reach, they were
outside the search space. Evolved decks finish pinned at exactly 20, which is what a binding
constraint looks like.

The constant was justified as "real constructed mana bases", but two rules here break that analogy:
**no colours** (a land is quantity, never fixing) and **every opening hand contains three lands by
rule**, so 20 of 60 on top of a guaranteed three is far more than an aggressive deck wants.

It also explains why Ancestral Recall and Liliana of the Veil appear 4x in nearly every evolved
deck: inside a 20-land shell with a slow clock, card advantage genuinely IS the right plan. The
card values were correct for the deck space they were given.

`MinLands`/`MaxLands` are now overridable via `MTG_MIN_LANDS`/`MTG_MAX_LANDS` (default 20/26) so an
A/B runs both arms on one binary.

## The field has NO external reference, and it is ~15pp weak

**Measured, and it is the most important thing on this page.** Hand-built decks from
`DeckRegistry` against the evolved ALL field, 160 games each:

| Deck | vs the field |
|---|---|
| **Zoo** (plain RGW aggro) | **66.2%** |
| **Traditional Storm** | **63.1%** |
| Dragonstorm / Jund / Goblins / Reanimator / Affinity | 44-51% |

**Zoo is the control and it is the finding.** Storm alone would have said "hill climbing cannot
reach combo". Zoo — 24 Plains and 36 efficient creatures, reachable by one-card steps — beats the
field by MORE. So the field converges on something ~15pp worse than a straightforward aggro deck it
could have built at any point.

**Fitness is measured entirely inside a closed field, and a round-robin averages exactly 50% by
construction.** All eight decks converged onto 4x Steppe Lynx and 4x Liliana of the Veil (8/8 each,
Ancestral Recall 7/8) — ~10 of ~38 spells identical, held apart only by the diversity floor — and
every internal metric still reported health: 8/8 viable, 18.6pp spread. **The mode cannot tell
"my decks are good" from "my decks are equally mediocre".** The acceptance rule inherits it: a
mutant is kept for beating the frozen field, so "better" means "better against weak decks".

**Run `ArchetypeChallenge` after any evolution run.** It is the only external yardstick that
exists. A field that loses to Zoo by 16pp is not a metagame, whatever its spread says.

## ArchetypeChallenge — test the assumption before designing around it

`MtgSimulator.Tests/ArchetypeChallenge.cs`. Builds the most theme-dense legal deck a pool allows
and plays it against a **saved metagame** — the decks an evolution actually produced. One
`[TestCase]` line per hypothesis.

**Built because an assumption was wrong and nothing else would have caught it.** A run finished
with 4x Goblin Chieftain supported by only 3x Frenzied Goblin, read all session as the
"half-built deck" failure. Against that same eight-deck field:

| Deck | Win rate |
|---|---|
| Full goblins — 4x each of the 10 best goblins, 37 goblin cards | **24.4%** (39/160) |
| The half-goblin deck evolution actually built | **55.0%** |

**The AI was right.** Committing to the tribe costs more than the payoff returns — your 10th-best
goblin instead of the format's 10th-best card is a losing trade. CSC has 17 goblins, so it is not a
pool limit. The half package is the optimum.

**Treat "the AI half-built an archetype" as a HYPOTHESIS, not an observation.** Magic intuition
about critical mass does not transfer to an engine with no colours, a small pool and mostly-weak
tribe members. Run this before designing anything around a suspected archetype.

It also vindicates the rule that features only GENERATE proposals while the win rate JUDGES them:
letting deck composition into the fitness function would have driven decks toward exactly the
losing configuration.

Two traps it embeds: it anchors on the **solution file**, because stray `sim_results/` folders
exist under `bin/` and anchoring there loads an empty table where every card reads 0.00pp; and it
**reports rather than asserting a threshold**, since a pass/fail bar would encode the assumption
under test.

## Concept (synergy) deck slots

`conceptSlots` (console prompt, **default 0 = off**) seeds N slots via `DeckBuilder.SeedConcept`
instead of anchor-and-kernel: pick a mechanical demand from `PoolFeatures`, jam every card in the
pool that answers or asks it, then refine. Slots are labelled `Synergy-1..N` / `Midrange-A..` /
`Wildcard`, each synergy slot takes a **distinct** demand, and they seed first (a synergy deck is
the hardest shape to fit past the diversity floor). Concept slots get `ConceptGraceMultiplier` (3)
times the normal cull grace and re-seed on a concept when culled.

**Unproven either way — leave `conceptSlots` at 0.** Two A/B runs were done and **neither is
readable**, because the control was later run against itself and differed from its own repeat by
MORE than it differed from the treatment (distinct cards 49 vs 47, diversity 55% vs 47%, viable
8/8 vs 7/8 — all at an identical seed and byte-identical inputs).

**This mode is chaotic, and a field-level metric from a single run is an anecdote.** One different
game outcome changes which mutant is accepted, which changes the whole field. Common random
numbers protect the *parent vs mutant* decision inside a generation; nothing protects final
coverage or final diversity across runs. Any claim about a change to seeding, mutation or scoring
needs **several seeds per arm and the noise floor measured first**. That floor has never been
measured, so every single-run comparison in this document — csc1 vs csc2 included — is
provisional. See `SynergyFeaturePlan.md` §14.

The engine itself is deterministic at small scale (4 decks / 3 generations / presim 0 and presim
200 both reproduce bit-identically at a fixed seed), so this is chaos plus an unmeasured noise
floor, not a shuffling bug — with a large-scale reproduction check still outstanding.

What IS established, by unit test rather than by a run: `PickConcept` must weight demands by
**inverse** supplier count. Weighted by breadth it drew "creatures you control" (237 of 408) over
Goblins (18) — a creature pile, not an archetype. **38/60 vs 50/60** concept seeds commit to a
tribe, and `ConceptChoice_PrefersDistinctiveDemands_OverBroadOnes` fails under the old weighting.

**Judge this on distinct cards in final decks and on the decklists, never on field spread** — a
synergy slot narrows its own deck on purpose. And never on one run.

## Colour and the evolver's field

**Presim random decks are seeded to a COLOUR IDENTITY** (`ColorIdentity`, 5 mono + 10 pairs) and
sample only cards that identity can cast. This is not a refinement — it repairs a bias colour
introduced. A deck sampled across the whole pool is a five-colour pile: measured on CSC it plays
**4.8 colours and gives a card 5.8 sources of its own colour**, against **1.7 and 16.2** when
scoped. Against the pip table in the root `CLAUDE.md` that is a double pip castable on curve ~23%
of the time versus ~86%, and 29% of a random deck's coloured cards are double-pipped. Unscoped, the
presim would deflate every committed card in the format — precisely the failure
`ConstructedValuesStore` documents, reached from a different direction, and its note that
*"deflation makes an archetype unbuildable"* is what makes it serious rather than cosmetic.

So **"neutral" now means random WITHIN a manabase that can cast the card**, not random across the
pool. Three-colour identities are deliberately excluded from the pooled table: their manabases fail
often enough that the games would measure the mana rather than the card.

## Colour slots

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

## The three win-rate tables

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

`IdentityValues.IdentityShrinkK` is **75, calibrated against a measured run** rather than guessed
(`PresimCalibrationHarness`; 300 identity-scoped decks over CSC gave 1 995 cells at p10 9, median
**24**, p90 50 games). A cell's evidence carries `games / (games + k)`, so at the pooled constant of
25 a median cell would carry 49% — half a rating decided by 24 games, whose standard error is ~10pp.
At 75 it carries 24%, and reaches half only around 75 games, which the table accrues over about
three runs. Guessing such a constant by analogy is how `MinPairGames` came to be 200 against a
busiest pair of 166, silently disabling every synergy path while the tests passed.

**Gold cards are under-sampled in the POOLED table, not the identity one** — measured: 27 median
games per card against mono's 112 and colourless's 370, because a gold card is legal in exactly one
identity. Its single cell is as well sampled as anyone's (27 against a median of 24). The fix is to
weight presim deck allocation toward pair identities; deferred, since the pooled rate shrinks toward
the draft prior and the failure mode is "reads unremarkable" rather than "reads wrong".

## `ManaBase.Build` is the only place a manabase is made

Draft, random pools and `Decklist.Materialize` all route through it. Every one of them used to pad
with Plains, which was fine with no colours and silently fatal with them. It allocates by MEASURED
DEMAND — sources needed to cast a card on the turn it costs — not by raw pip count.
