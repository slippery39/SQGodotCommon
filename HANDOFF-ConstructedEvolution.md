# Handoff — Constructed Evolution (mode 6), synergy/combo investigation

**Read this first, then `SynergyFeaturePlan.md` §9–21 for the detail.** This document is the
orientation; that one is the evidence.

---

## 1. What this session was trying to do

Make the constructed evolver build genuinely synergistic decks (storm, affinity, goblins,
reanimator) instead of converging on midrange piles of individually-good cards.

**It did not achieve that.** What it did instead was find and fix several defects that were
making the question unanswerable, and disprove four plausible explanations. The single most
valuable output is a set of measurement tools that did not previously exist.

---

## 2. The headline finding

**The evolver could not build the best decks because they were ILLEGAL in its deck space.**

`Decklist.MinLands` was 20. Every hand-built deck that beats an evolved field runs fewer:

| Deck | Lands | Precon round-robin |
|---|---|---|
| Traditional Storm | 12 | 1st, 63.1% |
| Zoo | 14 | 2nd, 62.5% |
| Affinity | 14 | 49.4% |
| Goblins | 16 | 4th, 53.8% |

Lowering the floor to 12 moved four of eight evolved decks below 20 lands, two to exactly 14, and
raised the field's score against an external gauntlet by **+3.5pp**. Evolved decks had been sitting
pinned at exactly 20 — a binding constraint.

**`MinLands`/`MaxLands` are now `static readonly`, overridable by `MTG_MIN_LANDS`/`MTG_MAX_LANDS`,
still defaulting to 20/26.** The default has NOT been changed — the evidence says 20 is wrong but
does not say what is right. **Pick this with a sweep, not a guess. ~14 is the likely answer.**

Why 20 was wrong here: no colours (a land is quantity, never fixing) and **every opening hand
contains three lands by rule**, so 20-of-60 on top of a guaranteed three is far more than an
aggressive deck wants.

---

## 3. Cumulative measured improvement

All against the same external gauntlet, ALL pool, same seed:

| Configuration | Field vs gauntlet | vs Zoo |
|---|---|---|
| no gauntlet, 25 gen, floor 20 | 47.1% | 30.0% |
| gauntlet, 25 gen, floor 20 | 53.3% | 32.5% |
| gauntlet, 40 gen, floor 20 | 53.0% | 38.1% |
| **gauntlet, 40 gen, floor 12** | **56.5%** | **45.6%** |

**Attribution: gauntlet +6.2pp, land floor +3.5pp, extra generations +0.0pp.**

---

## 4. What was DISPROVEN — do not re-tread these

| Hypothesis | Verdict | Evidence |
|---|---|---|
| The AI can't PILOT combo decks | **False** | Precons win games; Storm had to be nerfed for being too strong. The old `MtgSimulator/CLAUDE.md` claim was removed. |
| "Half-built archetype" always means failure | **False** | Max-density goblins scored **24.4%**; the AI's half-goblin deck scored **55%**. The AI was right. |
| Storm is unbeatable | **False** | Goblins beats Storm 60–40 in the precon round-robin. |
| More generations will help | **False** | 40 generations vs 25: spread 16.4 vs 15.0pp, diversity 45% vs 45%, acceptance 3/8 both. Indistinguishable. |
| Concept/synergy slots improve the field | **Unproven, no effect seen** | Coverage 50 vs 50 (CSC), 43 vs 45 (ALL). `conceptSlots` defaults to 0. |
| Goldfish speed is a good combo fitness | **False** | Dismantling Storm makes it goldfish FASTER (5.0 → 4.0). See §7. |

---

## 5. Two engine bugs found and fixed

**5.1 Mode 6 was not reproducible.** `PreSimulation` excluded `TimeLimitReached` games — a
**wall-clock** decision. Two identical-seed runs dropped a different game each (1 vs 2 of 9,600),
producing completely different metagames. `GameRunner` had had the clock removed from *ending* a
game years ago but it remained in *discarding* one.

Fixed by raising `SafetyTimeoutMs` 300s → 1800s (a game's result is deterministic; only how long
we wait is load-dependent, so waiting is strictly more correct than discarding). Verified
bit-identical across runs. **Cost: presim ~9m → 23.5m** — see `DesignNotes.md` for the slow-game
investigation this makes urgent.

**Diagnostic rule: if two runs at one seed disagree, read the excluded count before anything else.**

**5.2 `MoveCardToGraveyardAction` had no `CardIdContextKey`**, unlike its exile twin, making
Entomb-style effects inexpressible. Added.

---

## 6. What shipped (all behind defaults that preserve old behaviour)

| Component | What it is | Default |
|---|---|---|
| `Evolution/PoolFeatures.cs` | What each card ASKS of your deck and which cards ANSWER, read off cards by reflection. **No mechanic-to-meaning table.** | opt-in |
| `Evolution/Gauntlet.cs` | Fixed reference decks in fitness, per-pool, with buildability warnings | `gauntletGames: 0` = off |
| `Evolution/Goldfish.cs` | Solitaire "turns to kill an inert opponent" | diagnostic only |
| `DeckBuilder.SeedConcept` / `DeckProfile` | Concept-committed seeding; Aggro/Midrange/Control curve bands | `conceptSlots: 0` = off |
| `Tests/ArchetypeChallenge.cs` | **Play any hypothesis deck against a saved metagame** | `[Explicit]` |
| `Tests/ComboGradientTest.cs` | Interpolate combo → good-stuff pile, measure the fitness gradient | `[Explicit]` |
| 3 new cards | Entomb, Chromatic Sphere, Ornithopter Shard — designed to be good ONLY in their archetype | in LEG |

**`ArchetypeChallenge` is the most valuable thing here.** Mode 6 has no absolute reference — a
closed round-robin averages 50% by construction, so a field that converges on something mediocre
reports itself perfectly healthy. This is the only instrument that answers "is this field any
good" rather than "which of these eight is best". **Run it after every evolution run.**

---

## 7. Where the combo work stopped, and why

The plan was: give combo slots their own phase with a fitness that rewards ASSEMBLING an engine
(win rate being useless for a half-built combo deck — it loses either way). `Goldfish` was built
for that.

**Measured, and the gradient points the wrong way.** Interpolating from assembled Storm toward a
pile of the format's best cards:

| Cards swapped | Storm | Reanimator | Affinity |
|---|---|---|---|
| 0 (assembled) | 5.0 | 4.0 | 4.5 |
| 16 | 4.0 | 5.5 | 4.5 |
| 36 (pure pile) | 4.0 | 5.0 | 4.0 |

Dismantling Storm makes it goldfish **faster**. Against an inert opponent the fastest kill is just
cheap creatures attacking; combo has to assemble first. So the goldfish measures "how quickly can
you deal 20", which aggro wins outright.

**Storm's 63.1% does not come from speed.** It comes from Tendrils damage being uninteractive —
it cannot be attacked, blocked or answered. **The goldfish removes interaction, and
interaction-immunity is exactly Storm's edge**, so it is blind to the property that matters.

### The proposed replacement, NOT yet built

Measure **execution**, not speed: did the payoff resolve with its demand satisfied?

- Tendrils resolved with storm count ≥ N
- a creature entered play from the graveyard
- the affinity payoff was cast at reduced cost

A pile of Steppe Lynx scores **zero** on all of these where it scored *best* on the goldfish. It
stays reasonably generic — "did the deck's payoff card resolve while its demand was satisfied" is
expressible from `PoolFeatures` plus the event log without naming an archetype.

**Prototype it against `ComboGradientTest` before wiring it into anything. The goldfish looked
obviously correct too.**

---

## 8. Next steps, in the order I would do them

1. **Re-baseline.** Steppe Lynx, Liliana of the Veil and Chandra's Regulator were nerfed at the end
   of the session. Every number in this document predates that. `constructed_values_all.json`, the
   precon round-robin and all gauntlet scores need re-establishing before any new A/B means
   anything.
2. **Sweep `MinLands`** (12 / 14 / 16 / 20) and set a real default.
3. **Prototype execution-based combo fitness** and validate it on `ComboGradientTest`.
4. **Gauntlet treadmill.** Currently 7 of 9 gauntlet decks are ones the field already beats, so
   ~78% of the added fitness pressure is wasted padding — which is why Jund/Reanimator/Affinity
   moved +9 to +12pp while Zoo moved +2.5 and Storm −0.7. Cut the gauntlet to decks that still beat
   the field, and/or score the **worst** matchup instead of the mean.
5. **Selectable lands.** Five non-basics exist (Glimmervoid, Field of the Dead, Simic Growth
   Chamber, Valakut, Seat of the Synod) and no deck can play them. Affinity needs Seat of the
   Synod; Valakut is unbuildable. Lazy path: put non-basic lands in the selectable pool as if they
   were spells — `Materialize` already resolves them by name. Caveat: `AverageCost` would count
   them as 0-cost and skew the curve target.
6. **Investigate the slow games** — see `DesignNotes.md`. Presim went 9m → 23.5m because one or two
   games run ~1000× the median, and nobody knows why.

---

## 9. Traps this session paid for

- **A test that measures nothing.** Hit three times. A single fixed seed cannot test a change to a
  probability distribution; a fixture below a production threshold silently falls back; a
  one-concept pool cannot test a distinctness rule. **Verify a test fails when you break the thing
  it tests.**
- **`sim_results/` is relative to the WORKING DIRECTORY.** NUnit runs from the test binary's
  folder, and stray `sim_results/` directories exist under `bin/`. Anchoring there loads an empty
  table where **every card reads 0.00pp** — it looks like missing data, not a wrong path. Anchor on
  the solution file.
- **`TaskStop` kills the shell, not the child process.** A backgrounded run's
  `MtgSimulator.Console` survived and spawned the next loop iteration. Kill explicitly.
- **Any script guarding `sim_results/` must `mkdir -p` and abort on a failed backup.** One missing
  directory silently skipped a snapshot and nearly merged two arms into the shared values table.
- **Python file writes flip line endings** and turned an 87-line change into a 4,569-line diff.
  Normalize to match `HEAD` before committing.
- **A wall-clock number names a symptom, not a culprit** — now four for four in this project.

---

## 10. Run commands

```bash
# Evolution (prompts: mode, depth, set, decks, gens, mutants, games, final, minDiff,
#            presim, cull, prior, conceptSlots, gauntletGames, seed)
printf '6\n\n4\n8\n25\n3\n6\n20\n0.45\n800\nn\nY\n0\n4\nmyseed\n' \
  | dotnet run --project MtgSimulator.Console -c Release

# Precon round-robin — the true power ranking of known decks
printf '2\n\n10\n' | dotnet run --project MtgSimulator.Console -c Release

# Score a field against the hand-built decks (the external yardstick)
MTG_FIELD=sim_results/metagame_all_<stamp>.json \
  dotnet test MtgSimulator.Tests -c Debug --filter "FullyQualifiedName~HandBuiltArchetype" \
  --logger "console;verbosity=detailed"

# Wider land space
MTG_MIN_LANDS=12 dotnet run --project MtgSimulator.Console -c Release
```

Always run from the repo root, and verify `bin/` timestamps before trusting a run.
