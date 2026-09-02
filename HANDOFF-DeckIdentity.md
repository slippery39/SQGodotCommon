# Handoff — Deck identity: specify it, build it, optimise it without dissolving it

**Read this, then `MtgSimulator/CLAUDE.md` §"A candidate is a payoff CARD…" onward for detail.**

Supersedes `HANDOFF-EngineDiscovery.md` on everything about cores, discovery and combo finding. That
document is still correct on the land floor, common random numbers and mode 6 mechanics.

State at handoff: **MtgSimulator 296/296, MtgCore 802/802**, HEAD `16540c0`.

---

## 1. The headline

The previous handoff said random mutation plus win rate is a gradient-follower and three of four deck
classes are not gradients. That is still true. **What changed is that identity no longer has to be
searched for — it is read off the card, and only the tuning is left to search.**

| | |
|---|---|
| **Specify** | `DeckRequest` — a card name, a theme, a curve, or any combination |
| **Build** | `DeckCore.For` reads the archetype off the payoff's own demands; `Complete` fills from the archetype's pool |
| **Optimise** | `Mutate` swaps within slots and rebalances between them; the core protects what may be cut AND narrows what may be added |

Measured at each stage: 100% on-theme with 0 dead cards at build; cohesion held through 20
generations of hill climbing while an unconstrained walk went **43/43 off-theme**; and in real
12-deck fields the engine slots come back as decks a person would recognise.

---

## 2. The one idea worth carrying

**A core is a conjunction of demands anchored on a payoff CARD.** `EngineCandidate` used to be keyed
on a single `DemandIndex`, which is one level too low: Dragonstorm asks `SpellsCastDemand` *and* a
Dragon filter, so a demand-keyed loop probes two unrelated concepts and neither is Dragonstorm.

**Nothing had to be discovered to fix that.** `PoolFeatures.DemandsOf("Dragonstorm")` has returned
both demands since the card was written. What was missing was the JOIN.

It also explains the standing asymmetry: a tribal deck is ONE demand with a big supplier set, which
mutation stumbles into; an N-card combo is a conjunction of narrow demands, which it never will. **A
conjunction has to be constructed, not searched.**

Equally: nothing infers an archetype LABEL. "Mana engine" is a human word for a relation
`ProbeManaProfit` already computes — storm's suppliers ARE the mana- and card-positive cards. When
DES turned out to have no rituals, storm rebuilt itself around card draw with nobody telling it to,
because the supply rule is `net mana >= 0 || net cards >= 0`.

---

## 3. What shipped

| Piece | What it is |
|---|---|
| `DeckCore.For(features, payoff)` | The conjunction join. Payoff slot = cards whose demands are a SUBSET of the anchor's |
| `DeckCore.ForDemand(features, d)` | The demand-anchored unit, revived — right for *"a graveyard deck"*, wrong for *"a Dragonstorm deck"* |
| `DeckRequest` | Compiles cards + themes + curve into slots; reports what it matched and what it could not meet |
| `CoreSlot.TargetCopies` | The CAP, distinct from `MinCopies` the FLOOR. Fetched slots cap at the floor; count-demands stay unbounded |
| `CoreSlot.Supply` | Per-card supply weight, carried on the slot so `Satisfy` can order by it |
| `DeckCore.Complete` | Satisfies the core, then fills from the archetype's OWN pool before the format |
| `CardValueSandbox.MeasureLeverage` | Second sandbox arm with demands stocked. 783 cards in 11s |
| `LoopDetector` | Resource fingerprint + dominance + **replay confirmation**. 241k pair fixtures in 28s |
| `SwapWithinSlot` / `Rebalance` | Slot-aware mutation operators, active only under a core |
| Breadth gate | A core needs at least one SUPPORT slot ≤35% of the pool |

---

## 4. Measured

**Build cohesion.** Filling flex from the format gave Dragonstorm 38% on-theme (Atog, Frogmite, Kird
Ape — three unrelated decks). Filling from the archetype pool: **100%, 0 dead cards.**

**Identity survives optimisation** (`IdentityUnderOptimizationTests`, 20 generations vs a fixed
precon, asserting COHESION not win rate):

```
Dragonstorm         66.7% -> 83.3%   cohesion 100% throughout
Tendrils of Agony   66.7% -> 100%    cohesion 100% throughout
same 60 proposals, no core:  43/43 off-theme, Dragonstorm 0x
```

Both improved by finding real cards — Dragonstorm added Thundermaw Hellkite beside Hunted Dragon for
**six dragons, the historical count**, reached by `Rebalance` rather than set by anyone.

**Fields built.** ALL 8-deck (5 engine + 3 curve): 4 of 5 identity slots cohesive. DES 12-deck: ten
distinct archetypes across two runs, all cohesive, spanning 0.9%–63.6%.

**Loop detection.** ALL, 783 single cards and **241 173 pairs: 0 loops**, and that zero is only
meaningful because a planted free ability is found first. Expected — the engine has no untap and no
copy primitive, so no loop is expressible. **Re-run both sweeps the day either ships.**

---

## 5. Mistakes made this session — read this section

Nine, and the pattern is sharper than the list: **almost every one was a stale artifact or a
positional contract, not a logic error.** Each produced plausible output.

1. **`IReadOnlySet<string>` serializes and throws on load.** `Program.cs` only compares engine
   COUNTS after reloading, so a report whose slots came back empty would have passed that check and
   un-constrained every engine slot downstream.
2. **`MTG_MIN_LANDS=12 printf … | dotnet run` sets the variable for `printf`.** Documented in two
   files. Every mode 6/7 run using it measured decks at the 20-land default.
3. **The mode 6 prompt order in CLAUDE.md was wrong** (Cull and Seed-from-draft-model swapped).
4. **`DeckBuilder.EngineIdentity` and `EngineIdentityTests` were documented in detail and did not
   exist.** The core protected what could be CUT; nothing stopped free slots being refilled.
5. **Dominance is not a loop.** Any one-shot gain dominates the position before it. Fixed by
   replaying the cycle — that check IS the feature.
6. **`Describe` returned a bare type name**, so two creatures attacking both read `"AttackAction"`
   and a board of two bears reported a confirmed loop. Same defect as `ActionsMatch` re-finding an
   X-cost cast as X=0.
7. **`Slots[0]` meant three different things across three commits.** Adding a `Required:` slot broke
   dedupe silently — the report went 50 → 97 copies of the same archetypes. `CoreSlot.IsIdentity`
   replaced the positional contract.
8. **`string.GetHashCode` is randomised per process.** Seeding shuffles from it resampled a whole
   experiment; caught only because an arm whose code had NOT changed moved 6.1% → 7.2%.
9. **The DES field was run against an engine report written before `Supply` existed**, so the fix
   under test was never active. Same class as the stale-binary trap.

**The rule that would have caught most of them: when an unchanged arm moves, or a documented
mechanism is about to be reasoned from, verify it exists and is running before reading anything
else.**

Two more worth naming:

- **A membership constraint freezes anything that starts outside it.** The curve band rejected every
  mutant from generation 0 because seeding only TARGETS a band (Aggro seeds at 1.65 against 2.0–2.7).
  Made monotone — never move FURTHER out — which is the same shape `ProtectedIn` already uses. The
  old 60% pool quota failed identically; this is the second arrival at that trap from a new
  direction.
- **A "fix" that produces byte-identical output is a no-op wearing a feature's clothes.** Widening
  the archetype pool by one step of demand closure returned the whole format.

---

## 6. Live findings that are NOT bugs

**An artifact deck is not a distinguishable archetype in the CSC/ALL pool.** An aggro pile satisfies
the Atog core (29/45 cards) while scoring 0/45 against Dragonstorm and Tendrils. The format's best
cheap cards ARE artifacts. Same shape as the goblins result — the pool decides which archetypes are
real.

**DES has no rituals.** Rite of Flame, Seething Song, Lotus Bloom, Mox Pearl, Sol Ring and Dark
Ritual are all Legacy-only. A DES storm deck at a low win rate is a true statement about DES.

**Legacy is 9% of the ALL pool and 50% of its top 20 by isolation value** — 5.5x over-represented,
which is why DES exists. (Note: the claim that Steppe Lynx and Ancestral Recall are "well clear of
anything in HLM/CSC" is FALSE — Steppe Lynx is rank 12, with Rotting Regisaur above it. The argument
is over-representation, not dominance.)

**The value table only drives the curve decks.** Measured: engine decks hold **0** of the top-20
cards by value, curve decks hold 16–27 of ~40. The pool lock cuts engines off from it entirely.

---

## 7. Where it stopped — pick one

**(a) A pool-limitation vs builder-failure discriminator.** The most valuable open item. Right now
you must eyeball a decklist to tell "the pool cannot support this" from "the builder failed", and
that ambiguity already cost one wrong diagnosis this session. **A first attempt failed and the
reason is understood**: `bestSupply` only carries a magnitude for channels that have one (net mana,
cards moved), so a plain subtype filter is always flat 1 and "no signal" reads as "weak signal". A
correct version needs supply weight where magnitude exists and supplier COUNT where it does not.
Test it before trusting it.

**(b) Broad slots still absorb decks.** Spirit Bonds passes the breadth gate on a narrow Spirit slot
(51) while carrying two 460-card creature slots that constrain nothing, so half its deck is aggro
good stuff. Cheapest fix: cap broad slots via `TargetCopies`, which already exists and is already
used for fetched slots.

**(c) Causal supply into the HAND is too loose.** Goblin Lackey asks for *"a Goblin in your hand"*
and its causal slot is **72 cards — every card that puts anything in your hand**, so the deck is told
to play 13 draw spells against 11 goblins. The movement rule requires no filter match on the moved
card, which is right for a graveyard (you choose what to pitch) and wrong for a hand (you do not
choose what you draw). Scale causal supply by the filter's density where the filter is checkable.

**(d) Aggro is a weak proxy.** The curve band is enforced now, but a deck of one-mana cantrips
satisfies "aggro" without being aggressive, and the band constrains spell cost while `AdjustLands`
moves lands freely — a run produced Aggro-G at **26 lands**. Tie lands to the profile.

**(e) `UntapPermanentAction`.** Deferred. Nothing can loop without it; both loop sweeps are a
regression baseline waiting on it.

---

## 8. Run commands

```bash
# export it — `MTG_MIN_LANDS=12 printf … | dotnet run` sets it for printf, NOT for dotnet run.
export MTG_MIN_LANDS=12

# Set menu: 1=LEG 2=HLM 3=CSC 4=DES 5=ALL.  DES was inserted, so any older command
# passing 4 for ALL now silently runs DES. Read the menu.

# Mode 7 — discover cores. Fields: mode, AI depth, set, games/engine, highlight, seed
printf '7\n\n4\n10\n8\nseedword\n' | dotnet run --project MtgSimulator.Console -c Release

# Mode 6 — evolve a field. Count the prompts; this list has been wrong twice.
# mode, depth, set, decks, gens, mutants, games, finalGames, minDiff, presim,
# cull, draftPrior, conceptSlots, gauntlet, enginesPath, engineSlots, seed
printf '6\n\n4\n12\n15\n3\n6\n20\n0.35\n0\nY\nY\n0\n0\n<engines.json>\n6\nseedword\n' \
  | dotnet run --project MtgSimulator.Console -c Release

# ALWAYS regenerate the engine report after changing anything DeckCore writes —
# MetagameEvolver reads cores from that FILE, so a stale one silently disables the change.
```

**Use a fixed seed to compare runs.** Engine sampling is seeded off the run seed, so two runs at
different seeds build different archetypes and are not comparable — by design, for exploration.

Diagnostics, all `[Explicit]` and none needing games unless noted:
`DeckCoreGeneratorTests.DumpCoresForARealPool`, `LeverageSweepTests.DumpLeverageForARealPool` (11s
for 783 cards), `CausalSupplyTests.DumpSupplyWeights`, `CoreVsConceptChallenge`
`.WhatDoesTheCoreBuilderActuallyBuild`, `ValueTableOrderingTests` (diffs a deck built from two value
tables), `LoopDetectorTests.NoPairOfCardsInThePoolLoops` (28s), `IdentityUnderOptimizationTests`
(plays games, minutes).

`sim_results/` is relative to the SHELL's cwd — run from the repo root.
