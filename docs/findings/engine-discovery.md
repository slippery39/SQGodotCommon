# Engine discovery — measured results

Not loaded into context. Read when a change touches what these runs measured;
the live rules that came out of them are in `.claude/rules/`.

## MEASURED: mode 7's GAME columns are not reproducible at a fixed seed

> **FIXED — kept as the diagnosis, not as a live warning.** The cause was
> `StringComparer.Ordinal.GetHashCode`, randomised per process, seeding each candidate's deck build;
> the fix is `EngineDiscovery.StableHash` (FNV-1a), called from `Probe`. **Do not re-derive
> this bug from the section below.** What still holds is the sampling caveat: at 10 games per engine
> the standard error on `assem` is ~16pp, so a single run's LIFT is noisy even now that two runs at
> one seed agree. Raise games-per-engine before ranking on it.

**Three runs, identical seed (`comboseed`), identical binary, DES.** Half the table is deterministic
and half is not:

| Column | Stable? |
|---|---|
| `supp` / `pay` / `enab` — the core's structure | **identical every run** |
| `bare` / `supp'd` — leverage | identical for almost every row |
| `assem` / `depth` / `LIFT` / `cover` / `kill` | **swing wildly** |

```
                     run A                        run B
Blood for Bones      60%  depth 15.0  LIFT +15.0  30%  depth 0.0  LIFT 0.0
Wirewood Herald      50%  depth  1.5  LIFT  +0.5  80%  depth 6.0  LIFT +5.5
Ajani's Pridemate    80%  depth  4.0  LIFT  +4.0  40%  depth 0.0  LIFT  0.0
```

**LIFT is the column the mode exists to produce, and it moved +15.0 → 0.0 for one concept between
two runs of the same seed.** Deck construction and the leverage sandbox are seeded; the solitaire
games are not, or not fully. At 10 games per engine the sampling error alone is ~16pp on `assem`, so
even a correctly seeded run would need far more games before a single number meant anything.

**How to read a mode 7 report until this is fixed:**

- **Trust the structural columns.** `supp`, `pay` and `enab` say what the pool can support and are
  exactly reproducible. So is the core listing underneath.
- **Do not rank on a single run's LIFT**, and do not compare LIFT between runs. A concept that reads
  +15 in one run and 0 in the next has told you nothing.
- **A CONSISTENT zero across runs is still evidence.** Kilnmother Vess reads `depth 0.0, LIFT 0.0` in
  every run — that is a real "never assembles", not noise.

This is the same family as the two non-determinism bugs already recorded here (the parallel-batch
draw disaster and `string.GetHashCode`), and the same diagnostic rule applies: **when two runs at
one seed disagree, find out why before reading either.** Fixing it means threading the run seed into
the solitaire games; raising games-per-engine reduces the noise but does not make runs comparable.

## Four probe corrections, all found by reading a report rather than the code

| Symptom | Cause | Fix |
|---|---|---|
| Banefire, Hangarback Walker read as free storm enablers | `ManaCost` is **0** for every X card; X lives on the cast action | `XCostComponent` ⇒ cost unknowable ⇒ cannot be shown profitable |
| Past in Flames excluded from storm | probe counted cards **in hand**; it makes your GRAVEYARD castable | count castable cards anywhere — and note `IsInCastableZone` does **not** cover flashback, that is `MtgActionGenerator`'s job, so the graveyard clause is explicit |
| Entomb filed as a graveyard PAYOFF | a `SelectCard*Action.Filter` names what the card **fetches**, not a precondition | if resolving a card moves another into the demand's zone, it SUPPLIES and is struck from the askers |
| Stoke the Flames a payoff of every tribe | convoke discounts per creature of ANY kind, so 4 Zombies move its cost | control probe: attribute only if cost moves for the representative and **not** for a non-matching permanent |
| Soul Warden supplied no life gain | `ProbeTriggers` played ONE card onto an EMPTY battlefield, so "whenever ANOTHER creature enters" was structurally unfireable | play a vanilla companion after the subject, stripping the companion's own events so it does not make every card a supplier |

Measured after: Stoke is attributed only to genuinely creature-general demands, all sitting in the
±3 lift band; Soul Warden went 0 → 20 concepts; the storm pool went to 85 cards holding Lotus
Bloom, Mox Pearl, Rite of Flame, Seething Song, Silt Ritual, Faithless Looting, Ancestral Recall
and Past in Flames, at lift **+12.5** and 100% assembly.

**Two verification traps worth naming.** The card is `"Past in Flames"` — lowercase "in" — and
three case-sensitive greps in a row reported the fix as failing when it had worked; `GetByName` is
`OrdinalIgnoreCase` and hides the discrepancy. And `Satisfaction` **sums** across a card's demands,
so Dragonstorm's satisfied storm count masked its starving Dragon count and it read as fully
supported. `WeakestSatisfaction` is the per-demand form; `SupportScore` and `DeadCards` both use it
now, and `EngineCandidate.DeadInDeck` reports what is starving in the sample.

## LoopDetector — the balance instrument, built and validated

`Evolution/LoopDetector.cs`. Depth-first over the controller's legal actions, comparing every
reached position against every ancestor **on the current path**, and confirming by replay.
`LoopDetectorTests` drives it; the pool sweep is `[Explicit]` and runs **783 cards in 658 ms**.

**Measured: ALL, 783 cards, 0 looping, 0 threw.** That zero is only meaningful because
`APlantedFreeLoop_IsFound` passes — a detector that never fires reports the same thing.

Three defects were found while building it, and each produced a plausible result:

1. **Dominance is not a loop.** Any one-shot gain produces a position that dominates the one before
   it, so a single "gain 1 life" activation was indistinguishable from an unbounded one. `Repeats`
   replays the cycle from where it ended and requires it to dominate *again*. This is the whole
   feature — without it the detector flags every beneficial action.
2. **`Describe` returned a bare type name**, so two vanilla creatures attacking both read
   `"AttackAction"` and the replay of "attack" matched the OTHER creature. A board of two bears
   reported a confirmed loop. Identity now carries `CardId`, `AbilityIndex` and `XValue` by
   reflection — **the same defect as `ActionsMatch` re-finding an X-cost cast as X=0**, and the
   same rule fixes it: anything that distinguishes two legal actions must be in the match.
3. **`HasAttacked` belongs in the fingerprint alongside `IsExhausted`.** A creature that already
   attacked is spent for the purpose of repeating a line.

Four rules in the fingerprint, each pinned by a test:

- **Not a `GameState` key.** Every loop iteration holds new instance ids, so state equality never
  fires and a detector built on it reports zero forever while looking correct. This is why
  `CLAUDE.md`'s deferred "GameState comparison key" is NOT the thing to build here.
- **Dominance, not equality.** A token engine ends each iteration with strictly more permanents, so
  an equality test misses every growing loop.
- **Permanents keyed by name AND spent-state** (`U`/`T`/`A`). A line leaving a creature tapped where
  it started untapped has spent something and is not repeatable.
- **A grown graveyard is never the gain.** Every cast grows it, so counting it would make any two
  casts look like an engine. Checked for non-decrease, excluded from "something increased".

**`ActivatedAbilityComponent.MaxActivationsPerTurn` (default 1) is the engine's existing guard**,
and `TheEnginesOwnActivationCap_ClosesTheLoop` pins that the same card at the default is not a loop.
The realistic bug this catches on a set edit is that cap missing or set high enough not to bind.

**The pair sweep is built and is cheap: 241 173 fixtures in 28 seconds, 0 loops, 0 threw.**

```
ALL: 783 cards, 306153 pairs, 241173 with a repeatable source (64980 pruned), 0 looping
```

The prune is a **necessary condition, not a heuristic**: a loop needs something repeatable, and
casting is not repeatable because the card leaves your hand, so a pair where neither card has an
`ActivatedAbilityComponent` or a `TriggeredAbilityComponent` cannot loop. The pruned count is
printed, because a filter that quietly shrinks the search is how a sweep comes back clean for the
wrong reason.

**The fixture puts permanents on the battlefield and everything else in HAND.** The first version
put every card on the battlefield, which leaves an instant sitting there doing nothing — so the
sweep tested no spell at all and would have reported a clean pool for the wrong reason. Third time
this class of bug appeared in one session; see the `Describe` and dominance-vs-loop entries above.

**Read the zero as a BASELINE, not a proof.** What it is bounded by, all deliberate:

| Bound | Consequence |
|---|---|
| depth 6, branching 8, node cap 4000 | a longer or wider line is missed |
| `Repeats` matches steps by description | a line whose steps cannot be re-identified fails to confirm |
| no counters in the fingerprint | a counter-only loop looks like an identical board |
| two cards, one controller, 99 mana, turn already started | no three-card lines, no opponent interaction |

Given the engine has no untap and no copy, zero is the EXPECTED answer and its value is as a
regression baseline. **Re-run both sweeps the day an untap or copy primitive ships** — that is the
moment the number should be able to move, and if it does not, suspect the detector before believing
the pool.

Nothing feeds `LoopDetector` from mode 6 or 7; it is a test-time instrument today.

## Measured end to end: cores are NOT yet better than what they replaced

`CoreVsConceptChallenge` builds the same payoff two ways — `DeckCore.For` + `Satisfy` + flex fill
against `DeckBuilder.SeedConcept` — and plays both against the nine hand-built precons, 180 games
per arm, 1 SE = 3.7pp. **Two independent samples:**

```
payoff                  CORE   CONCEPT   delta       delta (2nd sample)
Dragonstorm             9.4%     6.1%    +3.3            +0.6
Zombie Apocalypse      11.7%     3.9%    +7.8           +10.6
Goblin Lackey          31.1%    26.1%    +5.0            -0.6
Angel of Second Rites   4.4%     3.9%    +0.6            -2.2
Atog (control)         18.3%    21.1%    -2.8            -8.9
mean                                     +2.8            -0.5
```

**Three of five deltas change sign between samples, so the effect is smaller than this harness can
see.** Only Zombie Apocalypse is consistently positive. Do not quote the mean of either run.

**The two numbers that ARE robust:**

| | |
|---|---|
| Best AI-built deck | 24–31% |
| Worst hand-built precon | **38–41%** (Dragonstorm) |
| Traditional Storm | **61–63%** |

The gap to hand-built is ~25pp and dwarfs anything the builder change moved. **That is the number
worth attacking, not the +3pp.**

**The composition dump says where it goes** (`WhatDoesTheCoreBuilderActuallyBuild`, no games):

```
Dragonstorm            16 core / 26 flex     <- 61% good stuff: Atog, Frogmite, Kird Ape, Myr Enforcer
Angel of Second Rites  36 core /  4 flex     <- derived minimums nearly fill the list; scored 3.9%
```

Two opposite failures from one flex fill. Ranking 26 free slots by standalone `CardDelta` produces
a pile of three unrelated decks — **the good-stuff failure moved out of the core and into the flex
half**, where nothing constrains it.

## The fix is a POOL LOCK on the flex slots, and cohesion is the metric to read

`DeckCore.Complete` satisfies the core and then fills the rest **from the core's own slot cards**
rather than from the whole format. Measured on Dragonstorm: **38% on-theme → 100%**, and the list
stops being a pile:

```
Dragonstorm — 18 lands, 42 spells, 100% on-theme, 0 dead cards
  4x [0] Inquisition of Kozilek   4x [1] Rite of Flame        4x [5] Thundermaw Hellkite
  2x [0] Lotus Bloom              4x [3] Seething Song        4x [6] Bogardan Hellkite
  4x [1] Ancestral Recall                                     4x [7] Hunted Dragon
  4x [1] Careful Study                                        4x [7] Dragonstorm
  4x [1] Faithless Looting
```

**Nothing was told this is a "mana engine deck".** That phrase is a human label for a relation the
demand model already computes — `ProbeManaProfit` measures net mana and net cards, so storm's
supplier set IS rituals, cantrips, draw and tutors (`Tide of Whispers(4)`, `Ancestral Recall(3)`,
`Lotus Bloom(3)`, `Rain of Revelation(3)`). The archetype's card pool was always the answer to "what
else should this deck play"; it simply was not being asked.

**Do not try to widen the pool by one step of demand closure.** It was tried: for each core card,
add everything supplying a demand it asks. Storm's 85 suppliers each ask demands answered by most of
the pool, so one step returns the whole format and both arms came out byte-identical — a no-op
dressed as a feature.

**Cohesion is the metric, not win rate**, and `WhatDoesTheCoreBuilderActuallyBuild` reports it with
no games: on-theme share, dead cards, payoff present, `Holds`. Win rate is what pulls decks back
toward good stuff, because good stuff is the cheapest way to compete — so read cohesion first and
judge competitiveness separately.

**Still wrong: the fill has no notion of ENOUGH.** It tops up to 60 with the highest-valued on-theme
cards, giving **12 Dragons** where the historical list ran 6 — the slot minimum is a floor with
nothing above it, so a card that qualifies keeps getting added. A storm deck wants those slots on
rituals and cantrips. The missing idea is diminishing returns per slot, not more constraint.

## Two harness bugs found by running it, both of which faked a result

1. **`Satisfy` filled the payoff slot best-first and left the ANCHOR out.** The Dragonstorm core
   built a deck holding Tendrils of Agony and four Dragons that nothing fetches — and `Holds`
   returned **true** throughout, because the slot was satisfied even though the core was not. Fixed
   by sorting the anchor first; pinned by `SatisfyAlwaysPlaysTheAnchor_NotJustSomethingFromItsSlot`.
2. **`string.GetHashCode` is randomized per process**, so seeding the shuffle from it resampled the
   whole experiment on every run. Caught because the CONCEPT arm — whose code path did not change —
   moved 6.1% → 7.2% between two runs that should have been byte-identical. Same class as the
   wall-clock bug that made mode 6 irreproducible: **when an unchanged arm moves, stop and find out
   why before reading the arm that did change.**
