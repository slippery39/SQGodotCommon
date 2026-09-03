# Handoff — why the builder ignores certain cards, answered; and the instruments built to answer it

**Read this, then `HANDOFF-ComboProvingGround.md` §6 (the open-items list this session worked from)
and `MtgSimulator/CLAUDE.md` §"The mutation log" and §"Context value".**

State at handoff: **MtgCore 830/830, MtgSimulator 347/347, SQGodotCommon 146/146.**

**Set menu: `1=LEG 2=HLM 3=CSC 4=CMB 5=DES 6=ALL`.** Two environment variables now matter:
`MTG_MIN_LANDS=12` (as before) and `MTG_SELF_ACTIONS` (new, default 1 — see §5).

---

## 1. The headline

**The deckbuilder was not ignoring cards because of a flaw in the deckbuilder.** It was ignoring them
because the PILOT could not use them, which made every downstream measurement correctly report them
as worthless. The chain, every link measured rather than argued:

```
PlayGreedyTurn takes ONE action per simulated turn
  → a mana engine's payoff turn (activate, cast, cast, cast) is unrepresentable
  → the pilot leaves the card in hand (Wirewood Conduit: drawn 3, cast 0)
  → OutputProbe measures it as worthless — correctly
  → the deckbuilder never proposes it
```

Fix the first link and the card appears at the last: Conduit went from **zero proposals in every
prior run** to proposed, accepted, and then **defended** — three generation-12 proposals tried to cut
it and were rejected at −9.1, −9.1 and −1.5.

**And it did not make the decks better.** Field vs references 38.0% → 35.7%, the elf slot itself
31.7% → 23.3%, runtime 48 → 100 minutes. `MTG_SELF_ACTIONS` stays **off by default**.

**Read that pairing carefully. "The card is now played" is not success** — it is the confirmation
trap `HANDOFF-ComboProvingGround.md` §5 is about. A real defect was found and fixed; fixing it did
not produce better decks.

---

## 2. What shipped

| Piece | What it is |
|---|---|
| `MetagameEvolver(excludedEngines:)` | Drop archetypes a previous run measured as dead. Console prompt; the run prints its own next exclusion list |
| `MutationLog` + `sim_results/mutations_*.csv` | Every proposal: generation, slot, cards in/out, parent vs candidate rate, outcome |
| `MetagameEvolver.TryMutate` | Re-rolls a failed mutation instead of burning the budget slot |
| `PoolFeatures.ProbeChainedTriggers` | A second trigger pass, so a trigger fed by another card's event finds suppliers |
| `TargetingOnlyObjectReferentialSpecs` | `IsControlledByOpponentSpecification` disqualifies a TARGETING spec, not a trigger |
| `DesignedGauntletDecks` + `Gauntlet.For` split | Three hand-built references for DES, which had none |
| `MetagameEvolver.FinalGauntlet` / `PrintGauntlet` | Reports the field-vs-references gap, which nothing printed |
| `OutputProbe` | Solitaire against an opponent that cannot die, measuring OUTPUT not position score |
| `CardValueSandbox.MeasureInContext` | Deck-conditioned card value against a median baseline (superseded by `OutputProbe` — see §4) |
| `MultiTurnBeamSearchAiStrategy(selfActionsPerTurn:)` | Simulated own-turn may take several actions. Default 1 = unchanged |
| Reversal guard (`MutationLog.Reversal`) | Refuses a mutation that undoes one accepted within 4 generations |
| `explorationGenerations` | Playset-sized moves and **no culling** while exploring. Default 0 = unchanged |

---

## 3. Bugs found, in order of how badly they were hiding

1. **The mutation budget was spent on CALLS, not mutants.** `Mutate` rolls one operator and returns
   null when it cannot produce a legal list; the slot was consumed either way. Null rates: 3–9% with
   no core, **34%** for a `Control` profile below its band, **95%** for a 5-card core pool. Two
   engine slots got 2 and 3 real proposals across twelve generations and were then reported
   NON-VIABLE — frozen at their seed, not refusing improvements.
2. **Context values were inert for a whole 10-deck run.** Every engine slot threw and fell back to
   isolation value; 35.2% against 35.8% was run-to-run noise on an unchanged configuration. Two
   causes, both failing identically for every candidate: a candidate already in the shell got copies
   ADDED (8 copies of a 4-of), and removing it then left the shell short while the resize only
   trimmed. **Only the fallback warning made it visible.**
3. **The gauntlet was counted in fitness but never reported.** `FinalRoundRobin` plays field-vs-field
   only, so a run shaped by the references still printed a closed round-robin averaging 50%.
4. **The search oscillates.** One elf slot spent four accepted mutations swapping the same pair back
   and forth, each direction scoring +3 to +6pp. 18 of its 27 proposals involved one card.
5. **Sanguine Reciprocity's demand was never harvested**, not "harvested with no suppliers" —
   `IsObjectReferential` recurses into a trigger's `Filter` and `IsControlledByOpponentSpecification`
   disqualified it.

---

## 4. Three substrates tried for context-conditioned card value

| Substrate | Result |
|---|---|
| `MeasureLeverage` (demand-conditioned, existing) | **Cannot discriminate.** Keyed on the DEMAND, so all three elves share one and measure identically; 4 of 6 played cards return "asks nothing answerable" |
| `MeasureInContext` (deck-conditioned, `StateEvaluator`) | **Blind by construction.** The evaluator counts `MaxMana` only and permanent power only, so a tap-for-mana elf and a tap-to-pump elf score EXACTLY as doing nothing (−0.70, three cards identical). A longer horizon made it worse: at 20 half-turns every card converged to +0.0 because the fixture resolves itself |
| `OutputProbe` (solitaire, unkillable opponent, OUTPUT) | **Works.** Damage is an outcome, not a position score, so the evaluator's exclusions do not reach it; and a game that never resolves accumulates instead of cancelling |

`OutputProbe` ranks Timberwatch Elder **first** where the isolation table ranks it **last of six** —
the disagreement the exercise was for, in the direction the hand-built deck says is right.

**Reported against the MEDIAN of the candidate population**, not against the next-best alternative:
argmax is O(n²), unstable and non-transitive; a median is O(n) and rescales when the pool's power
level moves. `Score` weights damage 1, permanents 0.5, cards 0.25 — **deliberately crude and
untuned**, because a tuned aggregate is how a proposal generator becomes a fitness function.

---

## 5. The standing measurement problem: nothing here is resolvable

**A candidate plays ~66 games, so one standard error is ~6pp.** The deltas the search accepts are
+1.5 to +6.1. **Every accept/reject in a normal run is at or below one SE** — the search is
accepting coin flips, and the oscillation in §3.4 is what that looks like from outside.

This is the frame for everything else in this document:

- 59% of proposals move exactly one copy (~2% of a deck). Their true effect is a fraction of a point.
- Field-level comparisons across runs are chaos, not treatment. Engine slots overlapped 62–100%
  between two runs; curve slots overlapped **30–43%** — different decks entirely, from the same
  configuration.
- **The noise floor has never been measured.** `SynergyFeaturePlan.md` §14 already flags this.

**The single most valuable next action is to measure it**: two runs of one configuration, identical
seed, and see how far apart the fields land. Until that number exists, every single-run comparison in
this project — including all of this session's — is provisional.

---

## 6. Next steps, in the order I would take them

### (a) Measure the noise floor — one run, unblocks everything

Same config twice, and compare final decks and the gauntlet gap. If 66 games cannot resolve
anything, no amount of mutation tuning helps and the lever is games-per-candidate.

### (b) The other half of the exploration phase is NOT built

The design was *playsets while exploring* **and** *trade proposal count for evaluation depth while
optimising*. Only the first shipped. A ±1 trim is exactly the change 66 games cannot resolve, so
optimisation still coin-flips — just on smaller changes. It needs games-per-matchup to vary by phase:
fewer, deeper proposals in the optimisation half. **Do not run a phased experiment until this
exists**, or half the design is untested.

### (c) Then run the phased experiment

`explorationGenerations` ~4 of 12, culling off during it. Read the mutation log's step-size
distribution and acceptance rate by size, not the win rate.

### (d) Open from the previous handoff, unchanged

- **The pool lock excludes off-theme finishers.** The hand-built elf deck plays 3x Hoofthunder
  Colossus — a Beast, outside the elf core pool, unreachable by design. That is the missing top end
  that makes a mana engine worth playing, and it is item (d)'s second half.
- **Colours (g)** — still uncosted, still the largest change on the list.

### (e) Things deliberately NOT done, with reasons

- **Scoring temporary mana in `StateEvaluator`.** The `HandQualityWeight` failure is the precedent:
  a term that should inform one decision must live at that decision. The pilot already finds
  activate-then-deploy once the card is in play; the broken decision was casting it.
- **Mana as an `OutputProbe` channel.** Mana is valuable only conditional on a sink. Scoring it
  directly would promote Conduit into exactly the deck where it does nothing.
- **Raising `ContextValueWeight` (6.0) until the effect is bigger.** That is how a proposal generator
  becomes a fitness function; the 24.4% maximum-density goblin deck is what that looks like.

---

## 7. Traps this session hit, several of them documented ones

- **`sim_results/` resolves against the process cwd**, which under a test run is `bin/` — an empty
  table where every card reads 0.00. Cost an hour and nearly produced a "quality-blind run"
  diagnosis. Anchor on the solution root.
- **`MTG_MIN_LANDS` unset in a test process** makes every 13-to-17-land deck illegal and every
  mutation return null. A fixture that hardcodes a land count passes under the runs' `12` and fails a
  plain `dotnet test` at the `20` default; read `Decklist.MinLands` instead.
- **An 8-game timing sample said the wider rollout ran FASTER** (6550 ms → 5488 ms). At scale it is
  **2× slower**. It was measuring game-length variance. Read wall-clock beside actions/game.
- **A uniformly-failing measurement produces a plausible number.** Both the inert context values and
  the earlier LIFT column did this. Every new metric needs a loud fallback and a vacuity guard.
- **Narrowing a numeric range is not the same as excluding an operator.** `rng.Next(9)` kept
  `Recount` because the roll table is not ordered by step size.

---

## 8. Run commands

```bash
export MTG_MIN_LANDS=12
# export MTG_SELF_ACTIONS=3     # wider simulated own-turn; default 1, and 2x slower

# Mode 6 — evolve. COUNT THE PROMPTS; this list has been wrong three times.
# mode, depth, set, decks, gens, mutants, games, finalGames, minDiff, presim,
# cull, draftPrior, conceptSlots, gauntlet, enginesPath, engineSlots, EXCLUDED, seed
printf '6\n\n5\n10\n12\n3\n6\n20\n0.35\n300\nY\nY\n0\n4\n<engines.json>\n6\nMere-Storm\n7\n' \
  | dotnet run --project MtgSimulator.Console -c Release

# Mode 7 — discover cores. mode, depth, set, games/engine, highlight, seed
printf '7\n\n5\n10\n12\nseedword\n' | dotnet run --project MtgSimulator.Console -c Release
```

**Reference cost, measured this session:** 10 decks × 12 generations with presim 300, gauntlet and
context values ≈ **48 minutes**; the same run at `MTG_SELF_ACTIONS=3` ≈ **100 minutes**.

**Always rebuild `-c Release --no-incremental` and check `bin/` timestamps before a run.**

Diagnostics worth knowing about, all `[Explicit]`:
`ConduitBehaviourTests` (four tests isolating why a card is unused),
`ConduitActivationTests.DoesAWiderRolloutMakeThePilotPlayIt`,
`MutationYieldTests.WhereDoTheNullProposalsComeFrom` (null rate by cause),
`GauntletCardStatsTests.WhichCardsCarryTheHandBuiltElfDeck` (per-card GIH inside one deck),
`OutputProbeTests.HowDoesTheRankingMoveWithTheHorizon`.

---

## 9. One result worth carrying

**The best deck in the DES field is a plain curve slot**, not a discovered engine — `Midrange-H` at
77.2% in one run, clear of every engine, on 4 real proposals because it sat above `StableRate` and
was deliberately left alone. And **the whole field loses to untuned hand-built references at
32.7–38.0%** while every internal metric reads healthy: 7–8/10 viable, 40–50pp spread, diversity well
above its floor.

That gap is the number worth attacking. Nothing in this session moved it.
