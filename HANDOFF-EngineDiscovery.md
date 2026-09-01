# Handoff — Engine discovery, deck cores, and why evolution is the wrong tool

**Read this first, then `MtgSimulator/CLAUDE.md` §"Engine Discovery (mode 7)" for the detail.**

Supersedes `HANDOFF-ConstructedEvolution.md` on the combo/synergy question. That document is still
correct on the land floor, common random numbers, and the mode 6 mechanics — it is not superseded
on any of those.

---

## 1. The headline

**Random mutation plus win rate is a gradient-follower, and three of the four deck classes we want
are not gradients.** This is not a tuning problem.

| class | shape | why search fails |
|---|---|---|
| aggro / midrange / control | **gradient** | it doesn't — this is the solved case |
| mana engines (Storm, Elves, Ironworks) | **cycle** | no payoff until the loop closes |
| tribal / artifacts | **threshold** | mutation moves 4 cards; the step needs 16 |
| N-card combo | **conjunction** | no partial credit at all |

Measured in this repo, not argued: `ComboGradientTest` shows Storm falling off a **cliff at 4 cards
swapped** (90% assembly → 0%), while Reanimator and Affinity have real gradients. **No local search
on any fitness reaches Storm.**

Independent corroboration: Kowalski & Miernik's baseline evolution sat below 50% and *"generates
average solutions during the first generation that it cannot further improve"*, unchanged by far
greater budget. Our 50-generation run reproduced that signature exactly — spread 46.2pp → 42.3pp,
no trend.

---

## 2. What shipped and works

| Component | What it is |
|---|---|
| `Evolution/EngineProbe.cs` | **Did the payoff resolve with its support deployed?** Read off one game's event log in a single pass. Payoffs deck-derived, enablers pool-derived. |
| `Evolution/EngineDiscovery.cs` + **console mode 7** | Probes every viable concept in a pool, ranks by LIFT, writes `sim_results/engines_<set>_<stamp>.json`. No battles. |
| `Evolution/DeckCore.cs` | `CoreSlot(Role, Cards, MinCopies)` + `DeckCore`. **What a deck must CONTAIN**, as a checkable fact. |
| `MetagameEvolver(enginesPath:)` | Seeds top-LIFT archetypes into field slots, constrained by their core. |
| Tests | `EngineProbeTests` (10), `DeckCoreTests` (7), `SpellResolvedEventTests` (2), `ThresholdSweepTests` (`[Explicit]`), engine columns in `ComboGradientTest` |

**`EngineProbe` is the most valuable thing here and appears to be ahead of the published work.**
Neither Hearthstone MAP-Elites paper has an axis resembling "did the engine assemble" — they
measure mana curve and game length, which is *why* they only ever produce aggro and control.

### The gate that validated it

`ComboGradientTest`, ALL pool, `MTG_MIN_LANDS=12`, 10 games/step. `assem` = share of games where a
payoff resolved with support down:

| swapped | Storm | Reanimator | Affinity | goldfish (all three) |
|---|---|---|---|---|
| 0 | **90% / 12.5** | **80% / 3.5** | **100% / 6.5** | 4.0–5.0 |
| 12 | 0% | 20% / 0.0 | 100% / 5.0 | 4.5–5.0 |
| 32+ | 0% | 0% | 0% | 4.0–5.0 |

**The goldfish column has no trend across three decks and thirty steps.** That is the blindness,
measured. Run this gate before trusting any new fitness.

---

## 3. Six engine defects found, all silent

1. **`SpellResolvedEvent` was declared and emitted by NOBODY.** No instant or sorcery had ever
   announced it resolved. Sixth instance of this repo's recurring event bug. Fixed in
   `ResolveSpellAction`, pinned both directions.
2. **`SeedConcept` built concept decks with no payoff in them** — 17 of 39 viable concepts, and the
   broad demands (whose payoffs are rarest) failed systematically. Payoffs now seed first.
3. **Storm was structurally undiscoverable** — storm lives in engine code, not card data.
   `PoolFeatures.SpellsCastDemand` harvests it from `HasStorm` and the cast restriction.
4. **Reanimator was a payoff for nothing** — `Reanimate` asks via `TargetingStrategy.Specification`,
   which was excluded outright. Now a demand when its candidates come from a non-battlefield zone.
5. **`ProbeTriggers` played one card onto an EMPTY battlefield**, so "whenever ANOTHER creature
   enters" was unfireable. Soul Warden supplied no life gain at all. Companion added; 0 → 20 concepts.
6. **`ProbeCostDemands` over-attributed convoke** — 4 Zombies discount every convoke card, so Stoke
   the Flames was a payoff of every tribe. Control probe added.

Plus: X-cost cards read `ManaCost` 0 and were treated as free storm enablers; `Satisfaction` summed
across a card's demands so Dragonstorm's starving Dragon count was masked (`WeakestSatisfaction`);
movement supply so Entomb is an enabler rather than a payoff; castable-anywhere including flashback,
since `IsInCastableZone` does **not** cover it.

---

## 4. What was measured, including the bad news

**Mode 7 on ALL** — 43 concepts, and LIFT separates real archetypes from vocabulary coincidences:

| | n | mean LIFT | mean control depth |
|---|---|---|---|
| broad concepts (>300 suppliers) | 14 | **−0.54** | 3.82 |
| narrow concepts (<100 suppliers) | 19 | **+5.18** | 0.45 |

**Phase 2, 50 generations + gauntlet, 100,464 games, 4h31m.** An engine won the field (82.9% vs the
wildcard's 67.9%) — and that means nothing, because a closed round-robin averages 50% by
construction. Against the hand-built precons the field scored **23.9%**. All seven beat it.

**And that 23.9% is a TRAINING score.** The nine gauntlet decks were in the fitness for fifty
generations, then seven of them were used as the yardstick. **Hold 2–3 precons out of the gauntlet**
so there is something honest left to test on.

**Threshold sweep** (`ThresholdSweepTests`, 80 games/point, 1 SE = 5.6pp so anything under ~11pp is
noise):

```
Goblin (24 in pool)   Artifact (57)      Spirit (37)
  4 → 18.8%             4 → 11.3%          4 → 11.3%
  8 → 28.7%             8 →  8.7%          8 →  6.2%
 16 → 16.3%            16 →  7.5%         16 →  3.7%
 28 → 40.0%            28 →  7.5%         28 →  2.5%
```

**No interior peak anywhere.** Goblins trends *up* to maximum; artifacts are flat noise; spirits
decline. **Discount this**: every arm is far below 50%, because the non-theme slots use the same
best-`CardDelta` pile in all seven arms and that pile is not a deck. This measures density on top
of a bad baseline. Only the goblin 16.3 → 40.0 gap survives the noise floor.

---

## 5. Mistakes made this session — read this section

Five, and four of them produced plausible-looking output.

1. **The LIFT column was vacuous TWICE.** First the control excluded enablers from its filler, so it
   could not deploy one (18 of 43 controls scored exactly 0.0). Fixed that; still broken, because
   `FromConcept` derived enablers from the concept *deck*, so a control holding different cards had
   none either. `EngineDiscovery` now **warns when no control scores above 2.0** — that check is the
   automated form of "verify a test fails when you break it", and would have caught both.
2. **Built a 60% pool quota when asked for a pool lock.** The storm slot spent its 40% allowance on
   Steppe Lynx, Gravecrawler, Liliana and Zombie Horde Leader — legal throughout. **A budget for
   drift gets spent on drift.** Replaced by `DeckCore`.
3. **Cull-exemption on 7 of 8 slots** meant `culled 0` for all fifty generations. Every slot was
   protected, turnover was off, and two engines sat at ~20% from generation 1 with no possibility of
   replacement. **Take 3–4 engine slots, not 7.**
4. **Trained on the test set** (see §4).
5. **Three case-sensitive greps in a row** reported Past in Flames as broken when the fix worked —
   the card is `"Past in Flames"`, lowercase "in", and `GetByName` is `OrdinalIgnoreCase`.

Also: **Python writes stripped CRLF** and inflated the diff from 1,410 to 3,425 lines. Note
`PoolFeatures.cs` is **LF** in HEAD while its neighbours are **CRLF** — check per file, not per
directory.

---

## 6. What the research says (reading list: artifact `4e9fdfd3`)

- **Win rate is a deceptive objective.** Lehman & Stanley: objective functions *actively misdirect*
  on deceptive problems. The applied answer is quality-diversity / MAP-Elites.
- **Both Hearthstone MAP-Elites papers score on average health difference, NOT win rate**, explicitly
  for a smooth gradient. Cheapest transferable idea here — `Goldfish.Result.FinalScore` already is one.
- **Deep Surrogate Assisted MAP-Elites** (GECCO 2022): a model that guesses outcomes, trained online
  so the search's own exploits become training data. 2.5× QD-score. Also: a *frozen* model trained on
  DSA-ME data beat the online loop.
- **Active genes** (Kowalski & Miernik 2020): constrain operators to relevant genes, inherit the rest.
  Their no-active-genes control performs worst — "constant forgetting". Blending parent/child at
  **0.75/0.25** beat replacement decisively.
- **Nobody has solved combo deckbuilding.** Every combo tool in existence (Commander Spellbook,
  Draftsim, combo-finder) is **lookup against a human-curated database**. The one paper that
  *generates* combos is HoningStone, a 2015 computational-creativity experiment.
- **Base rate**: in a hand-labelled 75-card Slay the Spire set, **16.8% of pairs synergise, 2.2%
  anti-synergise**. Most of any pairwise space is noise.

---

## 7. The plan, and where it stopped

Agreed framing: **"not the highest win-rate deck — the highest win-rate deck THAT CONTAINS X."**
Constrained optimisation. Win rate stays the judge; the constraint carries the identity.

| Stage | State |
|---|---|
| 1 · `DeckCore` | **done**, 7/7 tests |
| 2 · Core generators | threshold and mana-engine cores are buildable from data already on disk — `AskersOf` is a payoff set, `SuppliersOf` an enabler set, and `ProbeCardProfiles` net mana/cards give the ritual and cantrip sets |
| 3 · Threshold sweep | **run, inconclusive** — see §4 |
| 4 · Cores into the evolver | wired but unproven |
| 5 · Combo discovery | **not started** |

### Next step — pick one

**(a) Fix the Stage 3 baseline.** Evolve the non-theme slots under the `DeckCore` constraint so each
arm is a *good* deck at that density. Slower, but measures the thing we care about instead of
density-on-a-pile.

**(b) Goblins at higher resolution.** 40 games/point, densities 20/24/28/32/36. ~1,600 games, ~25 min.
Confirms or dissolves the one signal that survived the noise floor.

**(c) Stage 5, combo discovery.** The biggest prize and the least certain. Rigged fixture — candidate
cards in hand, 20 mana — with **exhaustive action-sequence expansion**, not the game AI (beam depth 3
with a branching cap of 16 will not find a five-step line, and a miss is an invisible false negative).
Detect a **dominance cycle**: same permanents, every resource ≥, at least one win-condition resource
strictly up. Needs a `GameState` comparison key, which `CLAUDE.md` lists as deferred — this is the
feature that justifies building it. 306k pairs for N=2; extend from confirmed pairs for N=3+ (blind
spot: combos where no pair interacts).

**Recommended: (b) then (a).** Both are cheap, and neither commits to a harness whose premise is
currently one suggestive curve out of three.

---

## 8. Run commands

```bash
# Mode 7 — discover archetype card pools. MTG_MIN_LANDS is load-bearing:
# Storm runs 12 lands, Zoo and Affinity 14, all illegal at the 20 default.
MTG_MIN_LANDS=12 printf '7\n\n4\n10\n8\nengineseed\n' \
  | dotnet run --project MtgSimulator.Console -c Release

# Mode 6 with engine slots (prompt order verified — count them, do not assume)
MTG_MIN_LANDS=12 printf '6\n\n4\n8\n50\n3\n6\n20\n0.45\n0\nY\nY\n0\n4\nsim_results/engines_all_<stamp>.json\nmyseed\n' \
  | dotnet run --project MtgSimulator.Console -c Release

# The gate — engine depth must fall to 0 while the goldfish stays flat
MTG_MIN_LANDS=12 dotnet test MtgSimulator.Tests -c Debug \
  --filter "FullyQualifiedName~ComboGradient" --logger "console;verbosity=detailed"

# Threshold sweep
MTG_MIN_LANDS=12 dotnet test MtgSimulator.Tests -c Debug \
  --filter "FullyQualifiedName~ThresholdSweep" --logger "console;verbosity=detailed"

# External yardstick — the ONLY absolute reference
MTG_FIELD=sim_results/metagame_all_<stamp>.json dotnet test MtgSimulator.Tests -c Debug \
  --filter "FullyQualifiedName~HandBuiltArchetype" --logger "console;verbosity=detailed"
```

Always run from the repo root; `sim_results/` is relative to the shell's cwd. Verify
`MtgSimulator.Console/bin/Release/net10.0/MtgCore.dll` is newer than your edit before trusting a run.

Saved artifacts from this session (gitignored, will not survive a clean):
`sim_results/engines_all_supplyweight.txt`, `phase2_50gen_gauntlet.txt`,
`phase2_50gen_challenge.txt`, `combo_gradient_20260831.txt`.

Regressions at handoff: **MtgSimulator 250/250, MtgCore 802/802.**
