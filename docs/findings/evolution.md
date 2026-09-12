# Constructed evolution — measured results

Not loaded into context. Read when a change touches what these runs measured;
the live rules that came out of them are in `.claude/rules/`.

## Measured: an identity SURVIVES optimisation, and dissolves without a core

`IdentityUnderOptimizationTests` hill climbs a requested deck for 20 generations against a fixed
precon and asserts **cohesion**, not win rate. That framing is the point: a Dragonstorm deck that
loses to Zoo is still a Dragonstorm deck, and a pile that wins more while holding no archetype is
the failure the work exists to prevent. Win rate is the local gradient inside an identity; it is
never the judge of whether the identity survived.

```
Dragonstorm         gen 0  66.7%  ->  gen 20  83.3%   cohesion 100% throughout
Tendrils of Agony   gen 0  66.7%  ->  gen 20 100.0%   cohesion 100% throughout

same 60 proposals, no core:  43/43 off-theme, Dragonstorm 0x
```

**Both decks improved by finding real cards.** Dragonstorm added Thundermaw Hellkite beside Hunted
Dragon — **six Dragons, the historical number, reached by `Rebalance` rather than set by anyone**.
Tendrils found 4x Past in Flames, which makes your graveyard castable and is exactly what a storm
deck wants.

Two operators exist only when a core is supplied, so mode 6's unconstrained slots behave exactly as
before and every earlier measurement stays comparable:

- **`SwapWithinSlot`** — a different card for the same role at the same count. `Satisfy` fills a
  slot best-first and one playset usually covers the floor, so a freshly built deck plays ONE of a
  role's options; this is what lets the rest be tried.
- **`Rebalance`** — move one copy between roles, deck size fixed. The "is four Dragons better than
  six" operator. It cuts only from a slot above its floor, so it cannot erode the identity —
  `ProtectedIn` is not consulted because an illegal move cannot be proposed.

`TargetCopies` is deliberately NOT consulted by mutation. The cap is an opening position for the
fill; once a deck exists, what it should hold is a question for measurement, and a cap that also
bound mutation would answer it by assumption.

## The control was wrong first, and the correction is a finding

The negative control originally hill climbed without a core and expected the deck to dissolve. It
**did not** — 100% on-theme for all 20 generations, Dragonstorm never cut. That is not the pool lock
working, because the lock was off: `SupportScore` already pays `SupportBonus` for a card the deck
answers and `DeadCardPenalty` against one it does not, so `Fill` prefers on-theme cards in a deck
that is already on-theme. **The soft pressure and the hard lock were doing the same job and the
fitness never had to choose.**

The honest claim is narrower: unconstrained mutation *can* leave the archetype, which a random walk
shows (43/43 off-theme) and a fitness-guided climb from a good deck does not. Read the pool lock as
a **guarantee** rather than as the only thing keeping decks together.

## Measured: first real run (CSC, 8 decks x 30 generations)

38 682 games, **0 excluded**, 38.7 minutes, prior exactly 0.5000 (draw-free). All 8 output decks
valid: 60 cards, max 4 copies, 21-26 lands, 13-17 distinct spells. Final field spread 35.7-65.0%,
6/8 above the viability floor, 10 culls.

Do not record specific card values here — run it and read the file. What is worth carrying:

**Diversity decays to the constraint and stops there: 76% at gen 1, 35% at gen 30**, i.e. exactly
the threshold. The constraint is *binding*, not slack — the field wants to converge further and
only the hard constraint prevents it. Read a final diversity equal to `minDifference` as "these
decks are as similar as they were allowed to be", not as a healthy result.

**Spread does not converge.** 33pp at gen 1, 40pp at gen 30, oscillating 24-55pp throughout with
no trend, and 5-8 of 8 mutants accepted every single generation. The field churns rather than
settling. That is not obviously wrong — a real metagame churns too — but it means **the final
matrix is a snapshot of decks at different ages**, some re-seeded two generations earlier and
still climbing. Deck A finished NON-VIABLE at 35.7% having been re-seeded at gen 28.

**Only 182 of 408 cards were ever played, and 146 cleared 100 games.** The other 226 have no
constructed data at all and will keep scoring at their draft prior forever — the same
self-reinforcing blind spot this document warns about for draft bootstrapping, in a new place.
More generations do not obviously fix it; the wildcard slot is the only pressure against it.

**corr(draft rate, movement) = +0.224.** For reference, the poisoned draft run measured +0.63
(amplification) and the healthy one −0.451 (regression to the mean). Positive means card values
are mildly absorbing the strength of the decks that happened to hold them. **That is structural
here and cannot be fixed the way it was for draft training**: the fix there was making every seat
equal strength, and this mode deliberately makes decks unequal — that is the entire point. Read
`constructed_values_*.json` as "how well this card did in the decks that played it", not as a
context-free card rating.

## The two tables are NOT directly comparable, and the first report said they were

Both are games-in-hand rates, and a card is credited only for games in which it was **drawn**.
Constructed games end faster than limited ones, so the mean games-in-hand win rate differs by
format even at identical priors: **draft 0.5162 against constructed 0.4439, a 7.2pp offset.**

Uncentred, that made *every* card read as "worse in constructed" by ~9pp — the faller list was
the offset and the riser list was whichever cards beat it. It looks like a dramatic finding and
is an artifact. `Movers` now subtracts the median movement and `FormatOffset` reports it; the
run prints the offset on its own line. `RawMove` is kept so the artifact stays visible rather
than being silently absorbed.

The **ranking was never affected** — `SpearmanAgainstDraft` is rank-based, and read 0.597 either
way, squarely in the healthy band. Only the magnitudes were wrong. Pinned by
`Movers_CentresOutTheCrossFormatOffset`.

**The general rule: two games-in-hand tables measured in different formats share no zero point.**
Compare ranks, or centre, but never subtract one pp column from the other and read the result.

## Measured: 100 generations, all sets, synergy-aware cutting

100 002 games, 258.9 minutes, 46 excluded (0.05%). 201 030 constructed deck-games, median **1 420
games/card** over 429 cards, median **472 games/pair** over 4 654 pairs — the per-deck pair data
is dense, which is what makes `DeckHistory` usable.

Against the 30-generation runs, three things improved and one broke.

**Diversity stopped decaying.** 83% at gen 1, never below 60%, 61% at the end — where the first
CSC run went 76% → 35% and sat on its floor. The constraint is no longer the only thing holding
the field apart.

**The real field got tighter, not looser.** The headline spread of 45.7pp is a wildcard artifact;
excluding it the seven real decks span **37.5–58.3%, 20.8pp**, against 29.3pp for the first run
*including* its wildcard.

**Decks brew when left alone.** Four slots reached age 70–95 (Deck F survived 95 generations
uncut) and the decks are visibly more coherent than the earlier piles — the best deck was 8 of 10
distinct cards from one Hollowmere graveyard theme, where a pre-fix deck was Path to Exile +
Tarmogoyf + 4x Past in Flames with no storm enablers.

## The wildcard slot is structurally broken on a large pool

**13 of 48 culls were the wildcard, and it never once survived** — final rate 17.1%, age 7, and
EVERY other deck's best matchup is "vs Wildcard". It is a permanent punching bag inflating all
seven real decks and both headline numbers ("Viable 7/8", "spread 45.7pp").

The cause is the uniform anchor. On CSC's 408 cards a uniform pick landed on something playable
often enough; over 785 it is a lottery ticket that never wins, so the slot re-seeds, dies, and
re-seeds forever. The exploration it was meant to provide never materialises — a deck that dies
every seven generations contributes nothing to the metagame it is supposed to widen.

**Do not read this as "the wildcard idea was wrong."** It worked on the smaller pool. What is
wrong is *uniform over everything* as the anchor rule. The fix to try is a restricted uniform —
sample the anchor uniformly among cards that have measured synergy partners, or among the top
half by value — so it still ignores what the prior prefers without being a pure lottery.
Untested; do not ship it on intuition.

## Spearman fell to 0.280 and the reading is genuinely ambiguous

Against 0.597 on the first CSC run, over 392 cards rather than 146. **This is not thin data** —
median 1 420 games/card — so the two honest readings are:

1. Real divergence. A 785-card format values cards very differently from limited, and more cards
   measured means more of that difference is visible.
2. Context narrowing. Decks are now specialised into themes, so a card played in only one deck
   is measured entirely inside that deck's shell, which is the same deck-strength absorption
   already recorded above (`corr(draft rate, movement)`), sharpened by specialisation.

These need separating before the constructed table is trusted as a card rating. The cheap
discriminator is per-card *deck spread*: a card measured in one deck only is a different kind of
number from one measured across five, and nothing currently distinguishes them.

## MEASURED: the DES field loses to hand-built references, 32.7%

**10 decks x 12 generations, 21 846 games, 3 references x 20 games each.** The first run in which
the Designed pool had a gauntlet at all.

| | field wins | reference wins |
|---|---|---|
| CMB Twin | 25.0% | **75.0%** |
| CMB Elves | 29.5% | **70.5%** |
| CMB Reanimator | 43.5% | 56.5% |
| **field overall** | **32.7%** | |

Every internal metric read healthy at the same time: **8/10 viable, 47.8pp spread, 44% diversity**
against a 35% floor. That is the closed-round-robin blindness this file already records for ALL
(Zoo 66.2%), now measured on the pool all recent work has used.

**The strongest single result is the controlled one.** `Engine-Wirewood Herald` was SEEDED with the
elf core and evolved for twelve generations; the hand-built elf list beat it **65-35**. Same
archetype, same pool, handed to the builder — and a human list of it wins. That is an optimiser
result, not a card-power one, because the archetype was not something the search had to discover.

## CONFIRMED with the data gap closed: (c)/(f) is real, and the table measures the WRONG QUANTITY

The presim re-run gave every CMB card real isolation data — the table went 711 to 732 cards — and
**the elf slot still plays none of them.** The gauntlet gap moved 32.7% to 35.8%, i.e. barely.

| card | rate in RANDOM decks | in the builder's deck |
|---|---|---|
| Sylvan Ranger | 66.8% | 4x |
| Radha, Heart of Keld | 63.1% | 4x |
| Nissa, Vastwood Seer | 59.4% | (4x previous run) |
| Wirewood Conduit | **51.6%** | **0x** |
| Wirewood Symbiont | **48.9%** | **0x** |
| Timberwatch Elder | **47.8%** | **0x** |

**The valuations are correct and that is the problem.** `PreSimulation` measures a card in RANDOM
decks, which is value-in-isolation — and a 1/1 that pumps your other Elves genuinely IS mediocre in
a random deck. A synergy card is *defined* by being weak alone and strong in context, so more data
never fixes this: it measures the isolation value more precisely. **This is the cleanest statement
of items (c)/(f) available and it is now measured rather than argued.**

It also rules out the cold-start reading recorded above: the blind spot was real and worth fixing,
but closing it changed almost nothing.

## SOLVED by changing the substrate: `OutputProbe`, and the gate now passes

Solitaire with an opponent that **cannot die**, measuring accumulated OUTPUT instead of a position
score. `Goldfish` already played a real `Decklist` against an inert seat; the additions are a
permanent carrying `CannotLoseComponent` on that seat and a `maxTurns` cap on `GameRunner`.

Two things fall out, and they are the two failures of the evaluator-based version:

- **Damage is an OUTCOME, not a position score**, so `StateEvaluator`'s exclusions do not reach it.
  If a mana elf buys a creature that attacks, the damage appears whether or not anything scores
  temporary mana.
- **The game never resolves**, so a longer horizon accumulates rather than cancelling. Verified in
  the fixture: `damage 304, permanents 16, drawn 19, turns 13` — life is 284 past dead and the game
  is still running.

**Measured on the elf shell, 4 copies each, 3 shuffles, turn 8:**

```
                        raw   over avg          isolation rank
Timberwatch Elder     248.3      +81.4          LAST of six
Poison-Tip Archer     184.8      +17.9          3rd
Wirewood Conduit      149.1      -17.9          4th
Reclamation Sage       96.8      -70.2          5th
```

Both gate assertions pass, and the ranking **inverts** the isolation table on the card that matters:
Timberwatch Elder is dead last by win rate in random decks and first here. That is the disagreement
the whole exercise was for, in the direction the hand-built deck says is right.

`OutputProbe.Score` weights damage 1, permanents 0.5, cards 0.25. **Deliberately crude and untuned**
— a tuned aggregate is how a proxy becomes a fitness function. All three components stay on
`DeckOutput` so a caller can see which is carrying a number.

**Still a proposal generator, never a judge.** `Goldfish` SPEED was measured to be the wrong fitness
(dismantling Storm made it goldfish *faster*) — that was about comparing DECKS, and this compares
CARDS inside one fixed shell, which the confound does not transfer to cleanly. The weaker version
does transfer: at a short horizon an immediate-damage card beats a long-term mana card, which is why
turns is a parameter and `HowDoesTheRankingMoveWithTheHorizon` records the sensitivity rather than
hiding it.

**Not yet wired into the fill.** The metric passes its gate; whether using it produces better decks
is a gauntlet question and is unmeasured.

##### `Compare`'s shell resize could HANG, and only at the land floor the real runs use

`Compare` removes the candidate from the shell and regrows the rest to leave exactly room for it, so
every candidate is measured in a deck of one size. The grow loop cycled the remaining cards adding a
copy at a time **with no progress check** — so once every one of them sat at `MaxCopies` with the sum
still under `room`, it span forever.

It is reachable from a real run: `MetagameEvolver` calls `Compare` with an engine slot's own shell,
and a shell with few distinct spells caps out below `room`.

**The land bound decides whether it fires, which is why a plain `dotnet test` never saw it:**

| | spells needed | cards available | cap |
|---|---|---|---|
| `MTG_MIN_LANDS` unset (20 lands) | 36 | 9 | 36 — terminates exactly |
| `MTG_MIN_LANDS=12` (17 lands) | **39** | 9 | **36 — hangs** |

The symptom was not a failing test. Under 12 the suite reported **`Passed! 282, Failed 0,
Skipped 0`** and looked healthier than the default's 348: `OutputProbeTests` wedged the host
partway, vstest reported only what had completed, and the other **66 tests were silently never
executed**. Test *discovery* was identical (421 both ways), so nothing anywhere said tests were
missing.

Two rules out of it:

- **Read a suite's TOTAL, not the word `Passed`.** `Skipped: 0` does not mean nothing was lost. This
  is the vacuity problem this file records for LIFT and for context values, now in the test harness
  itself.
- **A capacity guard THROWS rather than clamping.** A short shell measures a different deck for one
  candidate than for the others, which destroys the comparability the resize exists to create — and
  the caller already treats a throw as "fall back to isolation value" with a loud warning.

`OutputProbeTests.ShellCards` is 12 names rather than 10 for the same reason: the fixture has to be
legal at the FLOOR, not merely at the default. Both now pass at either bound.

## Pool-conditioned card value: the fourth substrate, and the first that measures WIN RATE

**Same known answer, no proxy.** `PreSimulation.Run(pool, …, fixedLands:, copiesPerCard:)` samples
random decks from ONE archetype's card pool at playset granularity and counts games-in-hand exactly
as the format-wide bootstrap and `DraftTrainer` do. No evaluator, no fixture, no solitaire — the
quantity measured is the one the builder actually selects on.

Two parameters carry the whole idea, and both are load-bearing:

- **`copiesPerCard: 4`.** At playset granularity a sampled deck plays ~11 of a 16-card pool and
  **excludes the rest**, which is the presence/absence contrast a per-card win rate needs. Rolling
  1–4 copies out of a small pool puts nearly every card in every deck, so only the COUNT varies and
  "does this card belong here" has no control group.
- **`fixedLands`.** Land count is a confound, not a variable: a 26-land and a 17-land sample of one
  archetype are different decks.

**Measured, elf pool (16 cards), 140 decks, 1668 games, 17.6m, 0 excluded, base rate 50.0%:**

| | isolation (random FORMAT decks) | pool-conditioned |
|---|---|---|
| Sylvan Ranger | **66.8% — best of six** | **−2.38pp — worst of sixteen** |
| Radha, Heart of Keld | 63.1% | +4.26pp |
| Nissa, Vastwood Seer | 59.4% | −1.28pp |
| Timberwatch Elder | **47.8% — worst** | **+6.64pp — 4th** |

`PoolSampledValueTests` holds it to the same two orderings `ContextValueTests` demands. Both pass —
but read the resolution before trusting either: at ~1600 games-in-hand one SE on a card rate is
~1.25pp, so ~1.8pp on a difference. **Timberwatch > Archer is 6.93pp (~3.9 SE) and decisive;
Conduit > Sage is 1.53pp (~0.86 SE) and is NOT resolved** — it passed on a coin flip that landed
right. Still strictly better than the alternatives, which returned *identical* numbers for these
cards, but do not quote the Conduit ordering as established.

**The pool restriction is what makes PAIRS measurable, and this is the reusable finding.** Pair
density is the thing every synergy attempt in this file has died on:

| | pairs | median games/pair | clear the 50-game gate |
|---|---|---|---|
| format-wide presim (3587 games) | 38 792 | **6** | **0** |
| 16-card pool (1668 games) | 120 | **754** | **120 of 120** |

~125x the density at full coverage, from restricting the pool rather than from more games. The
disproved `synergyWeight` result was measured on the top row; nothing in this file has ever
evaluated a synergy term on the bottom one.

**The vacuity control passed, and it is the half worth trusting.** `Spearman ρ = 0.041` against the
format-wide isolation ranking over the same 16 cards — essentially orthogonal, where this file's
other readings are 0.874 (stable), 0.597 (healthy divergence) and 0.280 (ambiguous).

**Do not read ρ ≈ 0 as the evidence, because a broken measurement produces it too** — that is the
uniform-failure shape recorded three times above. The evidence is that the movement sorts BY
MECHANIC:

| rises | move | | falls | move |
|---|---|---|---|---|
| Timberwatch Elder (pump per Elf) | **+12** | | Sylvan Ranger (fetch a land) | **−14** |
| Dwynen, Gilt-Leaf Daen (Elf lord) | **+9** | | Elvish Visionary (draw a card) | **−10** |
| Wirewood Symbiont | **+7** | | Fauna Shaman (tutor) | −6 |
| Elvish Archdruid (mana per Elf) | +3 | | Poison-Tip Archer (generic body) | −4 |

Every riser is a tribal payoff; every faller is generic card advantage or mana smoothing. Noise does
not sort by mechanic. Wirewood Herald is rank 1 in BOTH tables at +10.85pp over ~1350 games (~8 SE),
so there is real signal rather than scatter.

**Nothing was told what an Elf is.** The tribal-payoff-versus-good-stuff axis fell out of win rate in
real games — the same property the demand model gets structurally, arrived at by measurement.

**Triplets are still out.** C(16,3) is 560 against 120 pairs, and the density gain that makes pairs
viable is exactly what disappears.

**Cost: ~1.6 games/sec against the ~17/sec constructed average.** Elf go-wide boards are the
documented worst case for the beam search, so budget a pool-conditioned sample of an aggro archetype
at ~10x the per-game cost of presim.

**Not wired into the builder.** The gate passes; whether selecting on it produces better decks is a
gauntlet question and is unmeasured. The aggregation trap is the known risk — "top cards by win rate
plus synergy" is the shape that cost win rate at every weight tested, and the fix this file already
names is to confidence-gate the pair sum rather than to sum all of them.

## Context value: built, gated, and the gate found the real blocker

`CardValueSandbox.MeasureInContext(candidates, context)` scores each candidate with the deck's own
cards stocked on the battlefield, then reports **`OverAverage` = raw minus the MEDIAN of the measured
population** — "how much better than the average card competing for this slot".

Two properties, both inherited rather than invented:

- **Per-mana controls already normalise the turn.** `MeasureOne` charges each card against a control
  at its own mana level, so "N turns elapsed" is subtracted before comparison. The horizon bias that
  makes an eight-drop look best is a BASELINE problem, not a lookahead problem, and the baseline was
  already right.
- **A fixed replacement level, not the argmax alternative.** Ranking each card against "the next best
  other card" is O(n²), unstable (one strong addition moves every other value) and non-transitive.
  Against a median it is O(n) and comparable — the WAR construction, and it rescales itself when the
  pool's power level moves.

Stocking is **battlefield-only** here, unlike leverage's four zones: a deck context is a BOARD, and
diluting the hand and library with eight copies of other spells measures the dilution.

**The gate failed, and it is not a tuning problem.** `ContextValueTests` asserts that in an elf shell
Wirewood Conduit and Timberwatch Elder outrank Reclamation Sage and Poison-Tip Archer. Measured:

```
Poison-Tip Archer   +20.06        Wirewood Conduit    -0.70
Reclamation Sage     +2.30        Timberwatch Elder   -0.70   <- identical
Elvish Visionary     +0.70        Sylvan Ranger       -0.70   <- identical
```

Three identical readings is the tell: those cards produced **nothing the scorer can see**. Conduit is
*"Exhaust: add mana equal to the number of Elves you control"* and Timberwatch taps for an
until-end-of-turn pump — and `StateEvaluator` counts **`MaxMana` only** (temporary mana excluded) and
**permanent power only** (`UntilEndOfTurn` excluded). Both exclusions are deliberate, both are correct
for their original purpose, and together they make these cards score exactly the same as doing
nothing. −0.70 is just the card leaving hand.

**So the blocker is the substrate, not the conditioning.** Every state-impact metric built on
`StateEvaluator` inherits this hole — and so does the AI piloting the deck, which can only find these
lines through downstream rollout consequences. Two ways out, neither tried:

- **Convert the effect into something visible.** A longer rollout with a hand the deck can actually
  spend mana on turns temporary mana into board. Costs rollout depth and still fails for a pump.
- **Use a different observable for that family.** `PoolFeatures.ProbeManaProfit` already measures net
  mana and is what makes storm's enabler set correct. A composite — sandbox impact for board cards,
  mana profit for mana engines — needs no new machinery.

The vacuity guard `ContextValueDisagreesWithIsolationValue` **passes**: the context ranking is not the
isolation ranking, so the measurement is doing something. It is simply not yet doing the thing.

## Leverage does NOT currently answer this, and it was built for a different question

The obvious candidate is `CardValueSandbox.MeasureLeverage`, which already measures a card bare and
then with its demands' suppliers stocked. **Measured on exactly this case, it does not discriminate:**

```
                     winRate      bare   supplied     gain
Wirewood Conduit      +0.90pp     6.00       2.59    -3.41
Timberwatch Elder     -1.40pp     6.00       2.59    -3.41   <- identical
Wirewood Symbiont     -0.92pp     6.00       2.59    -3.41   <- identical
Poison-Tip Archer     +8.05pp    15.93      14.48    -1.45
Nissa / Radha / Sylvan Ranger / Reclamation Sage    "asks nothing answerable"
```

Three failures, all structural rather than tuning:

- **It is keyed on the DEMAND, not the card.** All three elves ask the same Elf subtype demand and
  get the same two stocked suppliers, so they measure identically by construction.
- **Four of six played cards return 0.00** — "asks nothing answerable" or "nothing in the pool to
  stock" — so the column is blank for most of a deck.
- **The gains are negative**, which is the fixture artifact this file already documents: stocking two
  copies into four zones dilutes a hand and a library, and tribal bodies price worse for it.

**So the machinery is not a drop-in.** It was built to ask "is this payoff a blank without support?"
and answers that adequately for a Dragonstorm shape (bare 0.00, supplied 32.03). What (c)/(f) needs
is a different measurement: **a card's marginal contribution to a REALISTIC deck state** — add it to
a board and hand that look like the archetype, roll forward, subtract the control — rather than to a
bare fixture stocked with two representatives. Same rollout-and-subtract skeleton, different
conditioning.

## CAUSE OF THE EARLIER READING, kept because it was a real defect

**Every CMB card was ABSENT from the constructed values table.** `constructed_values_des_presim.json`
held 711 cards measured before the set existed; all 21 CMB cards scored exactly **0.00** while the
HLM/CSC elves around them read up to **+17.17**:

```
Nissa, Vastwood Seer   +17.17      Wirewood Conduit     0.00   <- never played
Sylvan Ranger          +15.55      Timberwatch Elder    0.00   <- never played
Radha, Heart of Keld   +14.33      Wirewood Symbiont    0.00   <- never played
Llanowar Elves         +12.90      (all 21 CMB cards absent from the table)
```

The run was launched with **`presim 0`**, so nothing generated isolation data for the new cards
either. A card with no data scores zero on the fill's main term and loses to every measured card;
`ExplorationBonus * Unmeasured(name)` exists to counteract exactly this and is nowhere near enough
against +17.

**This is the self-reinforcing blind spot this file already documents twice** — once for draft
bootstrapping ("every new card would score 0 against known cards scoring up to +16.8, so they would
be passed over every pick, never make a deck, and never accumulate data") and once for constructed
("226 cards have no constructed data at all and will keep scoring at their draft prior forever"). It
is the same failure in a third place, and a run that adds a set without a presim pass walks straight
into it.

**Operational rule: after adding cards to a pool, run mode 6 with `presim > 0` at least once, or the
new cards are unplayable by construction.** Check with the one-liner rather than assuming:

```
python -c "
import json;d=json.load(open('sim_results/constructed_values_<set>_presim.json'))
have={c['Name'] for c in d['Cards']};print(len(have))"
```

**Superseded by the presim re-run above**, which closed this gap and changed almost nothing — so the
blind spot was a real defect but not the cause of the elf failure. Keep the operational rule; drop
the diagnosis.

## The elf decklists, which are what localised it

Both decks draw from the same 24-card core pool at the same 17 lands; only the choices differ, so
card power is controlled away and the loss is about selection. The builder's output after twelve
generations:

```
4x Dwynen's Elite      4x Llanowar Elves        4x Reclamation Sage
3x Dwynen, Gilt-Leaf   4x Nissa, Vastwood Seer  4x Sylvan Ranger
3x Elvish Archdruid    4x Poison-Tip Archer     4x Wirewood Herald
4x Elvish Mystic       4x Radha, Heart of Keld
1x Elvish Visionary
```

**Zero Wirewood Symbiont, zero Wirewood Conduit, zero Timberwatch Elder** — three of the four engine
pieces of the archetype it was SEEDED with. The one it kept is the anchor, which `ProtectedIn` locks
so it could not be cut. In their place: four Reclamation Sage, a naturalize body that is near-vanilla
against these decks, plus a stray 1x singleton of the sort a random walk leaves behind.

Note the builder's bodies are BIGGER — Radha 3/3, Poison-Tip Archer 2/3, Dwynen 3/4 against a
hand-built list of mostly 1/1s — and the curves are close (2.40 against 2.16). So it is not a curve
failure and not a stats failure. **Those three names are exactly the cards the values table had never
heard of**, which is what pointed at the cause above.

**References built from HLM/CSC only are still worth having**, to separate "cannot assemble a pushed
planted combo" from "builds weak decks generally". Neither is established yet.

Cost: 36.9 minutes at 10 decks, against 31.2 without a gauntlet.
