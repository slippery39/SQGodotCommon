---
paths:
  - "MtgSimulator/Evolution/Engine*.cs"
  - "MtgSimulator/Evolution/PoolFeatures.cs"
  - "MtgSimulator/Evolution/LoopDetector.cs"
  - "MtgSimulator/Evolution/Goldfish.cs"
  - "MtgSimulator/Evolution/OutputProbe.cs"
  - "MtgSimulator/Evolution/DeckCore.cs"
  - "MtgSimulator.Tests/*Combo*.cs"
  - "MtgSimulator.Tests/*Supply*.cs"
  - "MtgSimulator.Tests/*Probe*.cs"
  - "MtgSimulator.Tests/*DeckCore*.cs"
  - "MtgSimulator.Tests/*Loop*.cs"
  - "MtgSimulator.Tests/*Demand*.cs"
---

# MtgSimulator — engine discovery (mode 7)

Loaded when you touch the demand/supply model, the probes, or deck cores.
Measured discovery results live in `docs/findings/engine-discovery.md`.

## Engine Discovery (mode 7)

**Phase one of the synergy work: what engines does this pool support, and do they assemble?** No
battles, no win rate, no evolution — every viable concept gets a deck built for it by
`DeckBuilder.SeedConcept` and plays solitaire games, and `EngineProbe` reports whether the payoff
went off with its support already deployed. Output is a ranked report you READ, plus
`sim_results/engines_<set>_<stamp>.json` for the evolver to seed from.

**The split exists because win rate cannot judge an engine that is not built yet.** A
half-assembled storm deck loses every game, so hill climbing on win rate walks away from it before
it is finished — which is what every run of mode 6 has done, *correctly*, to the wrong question.
Asking "does it assemble" first and "is it competitive" second is the only ordering where the
first question has an answer.

```bash
export MTG_MIN_LANDS=12
printf '7\n\n4\n10\n8\nengineseed\n' \
  | dotnet run --project MtgSimulator.Console -c Release
```

**`export` it on its own line — `MTG_MIN_LANDS=12 printf ... | dotnet run` sets the variable for
`printf` and NOT for `dotnet run`.** That form was documented here and in the handoff for a whole
session, and it silently runs the discovery at the 20-land default. The tell is in the output: the
console prints `NOTE MinLands is 20` when the floor is above 14, and every number in a run carrying
that line is measuring decks the archetypes cannot legally be.

Fields: mode, AI depth (blank = 2), set index (read the menu, it moves), solitaire games per
engine, engines to highlight, seed. **Set `MTG_MIN_LANDS=12`** — Storm runs 12 lands, Zoo and
Affinity 14, so at the 20 default every one of them is illegal and an engine is being asked to
assemble out of a deck it cannot be. Mode 7 prints a NOTE when the floor is above 14.

## A combo deck can be built correctly, measured correctly, and STILL not assemble

Three separate causes were found for one symptom — a planted two-card combo (CMB's Twin package)
reading `depth 0.0, LIFT 0.0`. The first two were measurement bugs and are fixed; **the third is
not a bug and is the important one.**

1. **`Summarise` medianed depth over ALL games**, including the zeroes from games that never
   assembled, so below 50% assembly `MedianDepth` was pinned to exactly 0 and `Lift` with it. 22 of
   22 engines under 50% read depth 0; 0 of 20 above it did. Fixed — depth now filters to assembled
   games, matching what turn always did. **Engines at depth 0 went 22/42 → 1/43.**
2. **Activating an ability emitted no event naming the card**, so `IsExecution` credited an
   activated-ability payoff when the creature ENTERED PLAY — before the ability could ever be used.
   Fixed by `AbilityActivatedEvent`. Moved the whole table (Mere-Storm depth 7.5 → 22.0).
3. **The deck wins without the combo.** After both fixes the Twin engine still reads
   `assem 20%, depth 1.0` — and the reason is in the same report: **`wins 8 of 10`, goldfish kill
   turn 6.** The flex slots are the format's best cards, the AI curves out and kills the inert
   opponent, and the 4-mana 1/1 copier is simply a worse play than a planeswalker. The combo is
   never needed, so it is never assembled.

**This is the goldfish blindness, one level up.** That criticism is already recorded against
`Goldfish` SPEED as a fitness — *"against an opponent that does nothing the quickest kill is cheap
creatures attacking"* — and `EngineProbe` runs inside that same solitaire game. Its metric is
better; its FIXTURE is the same one, and the game is decided before the engine matters.

**So the open item for combo measurement is the fixture, not the metric.** Three costed directions,
none tried:

- **Ask whether the combo was AVAILABLE, not whether the AI used it.** `LoopDetector` already
  answers this from a position and already finds this exact combo in two actions. Cost needs
  measuring — its published figures are for two-card fixtures, not a mid-game board.
- **Deny the deck its good stuff.** The Twin core's archetype pool is 4 cards, so `Complete` tops
  up from the format; a combo-only fill would force the question. Risks measuring an unplayable
  deck.
- **Give the goldfish opponent a clock**, so curve-out beatdown is not automatically sufficient.

Do not read a low `assem` on a combo core as "the builder failed" — check `Wins` in the same row
first.

## A candidate is a payoff CARD and its whole demand conjunction, not one demand

`EngineCandidate` used to be keyed on a single `DemandIndex`, and that unit is one level too low.
**Dragonstorm asks two things at once** — `SpellsCastDemand` from `HasStorm`, and a Dragon filter
from its `SelectCardFromLibraryAction` — so a demand-keyed loop probes "spells cast" and "Dragon"
as two unrelated concepts, builds a deck for each, and neither of them is Dragonstorm. The card was
structurally unable to be a candidate.

**Nothing had to be discovered to fix this. `PoolFeatures.DemandsOf("Dragonstorm")` has returned
both demands since the card was written; what was missing is the JOIN.** `DeckCore.For(features,
payoff)` performs it: one payoff slot, then one support slot per informative demand.

It also explains the standing asymmetry in what the search finds. A tribal deck is ONE demand with
a large supplier set, which random mutation stumbles into; an N-card combo is a conjunction of
narrow demands, which it never will. A conjunction has to be constructed, not searched.

Three rules in `DeckCore.For`, each with a reason:

- **The payoff slot takes cards whose demands are a SUBSET of the anchor's.** Redundancy without
  naming anything: Tendrils belongs in a Dragonstorm core (it needs strictly less), Dragonstorm
  does not belong in a Tendrils core (that core promises no Dragon, so it would resolve for
  nothing — the dead-card rule).
- **A payoff never supplies its own slot.** Slots are counted independently, so a card in two of
  them satisfies both off one copy. It is also what gives tribal the right shape: 4 lords in the
  payoff slot and N *other* goblins in the support slot.
- **A thin slot CLAMPS rather than rejecting the core.** A count must not get to decide which
  archetypes exist.

## The subset splits again on EQUALITY, and the pinned slot is the equivalence class

The anchor used to get a slot of its own — `Required: {card}`, one card, `MinCopies` 4 — and **which
card that was came from `EngineDiscovery`'s dedupe, which picks its representative with
`OrderBy(Name)`.** A reanimator core was keyed on whichever reanimation spell sorted first and then
pinned it for the whole run, so the cheaper spell was never allowed to compete.

Two things made that worse than a bad label. `ProtectedIn` locks a slot at its floor, so an
alphabetical tiebreak became a hard build constraint; and `DeckBuilder.SwapWithinSlot` skips any
slot holding one card, so the slot was **structurally unswappable** — not "explored and kept",
never proposed.

So `interchangeable` splits in two, on demand-set **equality**:

| relation to the anchor's demands | slot | floor |
|---|---|---|
| **equal** — the same engine, differing in cost and name | `Payoff` | `payoffCopies` |
| **strictly less** — a cheaper rider that does not need the whole core | `Payoff [partial]` | 0 |

Equality is what preserves the failure the anchor slot was built from. A Spirit Bonds slot once
finished with **no Spirit Bonds in it**, because its payoff slot held 70 cards at `MinCopies` 0 and
`Holds` never noticed — and those 70 ask strictly LESS, so they are partial now and its pinned slot
stays at one card.

Measured, mode 7 on DES, same seed both arms, 44 cores each:

```
pinned slot size    1: 28   2: 9   3: 3   5: 1   7: 2   19: 1
```

28 are equivalence classes of one and behave exactly as before. 16 widened, which is the choice
being handed to measurement — `Blood for Bones` became `{Blood for Bones, Rite of Second Drowning,
Second Burial}` with 81 subsumed cards demoted to partial; `Dwynen's Elite` became 7 elf lords.

**The distinct-core COUNT did not move, and expecting it to was wrong.** The dedupe key is already
the union of the identity slots, so equivalent payoffs were never separate rows. What was wrong is
which one got pinned. A duplicate that survives dedupe is the *other* shape — two cards with
overlapping-but-unequal demands — and collapsing those needs a subsumption pass (`coreB.Holds(deckA)`
pairwise), which is **not built**.

## Slot minimums are derived, and one family is still wrong

`MinFor` replaced a flat 8 with two rules. **A demand carrying its own number uses it** —
`SpellsCastDemand.Minimum` is harvested off `HasStorm`, and "m spells cast in ONE turn" is a density
question (m−1 others in hand simultaneously), not a draw-one question. **Everything else is
consistency**: the fewest copies giving ≥1 in hand by `TargetTurn` (5) with `TargetProbability`
(0.90), hypergeometric over the SPELL population.

**An opening hand is 7 cards with 3 lands by rule, so it holds only FOUR spells.** Counting seven
overstates every deck's consistency and understates every count here.

Enabler slots are held to `TargetTurn - 1`, because an outlet has to have resolved *before* the
payoff is worth casting. On ALL that gives target 10x against enabler 11x — a small gap, but derived
rather than invented.

**A FETCHED demand is not counted as though it must be drawn**, and getting this right took two
wrong answers worth recording.

Dragonstorm searches its Dragons out of the library, so the consistency rule — "how many copies to
DRAW one by turn 5" — asks a question the deck never has to answer. It produced **11x of the 7
Dragons in the pool**, a deck nobody would build, and was a regression against the old flat 8 for
every tutor.

**"It only needs one to exist" is also wrong, and the reason is the interesting part.** Storm
resolves the spell `Math.Max(SpellsCastThisTurn, 1)` times and Dragonstorm fetches **one Dragon per
copy**, so a storm count of 4 wants four Dragons left in the library. The historical Standard list
ran **six** — 4 Bogardan Hellkite + 2 Hunted Dragon — enough to turn a realistic storm count into
lethal, plus redundancy for copies drawn dead.

`CopiesThatSurvive(need, …)` implements it: the fewest copies leaving at least `need` UNDRAWN at
`TargetProbability`, where `need` comes from the payoff's OTHER demand. **Two demands on one card
that are not independent, and this is the only place the model expresses that.** Dragonstorm's
Dragon slot went 11x → **3x**.

**The floor is deliberately below the historical six, and that is the right relationship.** Deriving
6 is out of reach — it comes from wanting lethal, which is not in the card data. A core is a floor
with slack: `ProtectedIn` locks a slot only AT its minimum, so tuning stays free to find six. A floor
of 3 permits the real list; a floor of 11 forces a fake one. **When a derived count looks low, check
whether it is a floor before treating it as a bug.**

The signal for "fetched" is the recorded origin — `OriginOf` names the action a demand came from
(`SelectCardFromLibraryAction.Subtype`), marked `ponytail:` since a bool recorded at harvest time
would be sturdier. `DemandZone` will NOT serve: a bare subtype spec matches in every zone of the
probe fixture and returns null for exactly this demand.

**Candidates are deduped on the payoff slot, and the dedupe is load-bearing.** On ALL, 783 spells
give 203 cores but only **50 distinct** — ~64 payoffs that merely "target a creature" produce the
byte-identical core, and probing each is 64 runs of one experiment.

The deck built for a candidate is **the core plus good stuff**, which also makes LIFT cleaner than
it was: the control is the same payoffs plus the same good stuff, so the only difference between
the arms is the core's support slots. Depths are consequently much LOWER than the `SeedConcept`
numbers recorded below — that method jammed the whole deck with concept cards, this one guarantees
8 — so read `cover`, not `depth`, and do not compare the two eras' depth columns.

**Still open: broad cores crowd the table.** Concepts with 450+ suppliers ("a creature entered")
still generate cores, and a slot of 8 cards drawn from 465 constrains nothing. LIFT correctly rates
them ~0.0, but they occupy the report.

## LEVERAGE — what a payoff is worth with its demands answered

`CardValueSandbox.MeasureLeverage` is a second arm on the existing sandbox: the same card cast into
the same fixture twice, once bare and once with the demand's best suppliers stocked. **Both arms
subtract their own control**, so the extra permanents cancel and what is left is the card's own
gain from being supported. `LeverageSweepTests` drives it — **11s for all 783 cards**, so this is
cheap enough to run before any discovery session.

The stocking puts suppliers into **battlefield, graveyard, library and hand at once**, deliberately:
a subtype filter reads the battlefield, a reanimation spec the graveyard, Dragonstorm's fetch the
library, a rummage cost the hand — and dispatching per demand kind is the mechanic-to-meaning table
this project refuses to write. `SpellsCastDemand` is the one exception, set on the counter, the same
exception `PoolFeatures` already makes.

Do not record the values here — run the sweep and read them. What is worth carrying is the **shape
of the answer, which is not what was expected**:

```
payoff                     bare  supplied  LEVERAGE
Drogskol Captain          11.67    105.67    +94.00     <- tribal lord
Stromkirk Captain          9.00     77.67    +68.67     <- tribal lord
Krenko, Mob Boss          12.00     50.80    +38.80     <- tribal lord
Goblin Lackey              6.00     40.43    +34.43     <- tribal lord
Dragonstorm                0.00     32.03    +32.03     <- combo payoff
Archangel of Thune        64.31     15.90    -48.41     <- fixture artifact
```

**Raw leverage ranks TRIBAL above COMBO, which is backwards for the problem it was built for.** It
is measuring two different things under one number:

| signature | reads as | example |
|---|---|---|
| the card is a BLANK without support | **bare ≈ 0**, supplied high | Dragonstorm, Zombie Apocalypse, Spectral Tide |
| the card SCALES with support | bare high, supplied higher | every lord in the pool |

Both are real synergy. Only the first is the combo/engine class, and `bare` is what separates them
— Dragonstorm is the only card in the top ten reading exactly 0.00 without its demands. **Sort or
filter on `bare`, not on leverage alone**, and do not collapse them into one score on the strength
of one run; that is the retune-on-intuition failure this file records three times already.

**Large negatives at the bottom are a known fixture artifact, not anti-synergy.** Stocking two
copies into four zones dilutes a hand and a library, and auras and equipment (Archangel of Thune,
Angelic Destiny, Mark of the Vampire) price worse for it. Read the bottom of the table as "the
stocking is wrong for this card", not as a finding.

**Mode 7 now ranks BLANK-FIRST** — `EngineCandidate.BlankFirstKey`, lowest `bare` (clamped at 0),
then most gained, with unmeasured candidates sorted last so a failed measurement cannot masquerade
as a blank. LIFT is kept as a column. Measured at `MTG_MIN_LANDS=12`, 8 games:

```
concept                supp  assem  LIFT  kill    bare   supp'd
Dragonstorm              91    62%  +1.5   6.0    0.00    32.03
Zombie Apocalypse        79     0%   0.0   6.0    0.00    27.20
Entomb                  492    62%   0.0   5.0    0.00     0.00
Flameshadow Conjuring   453    50%   0.0   5.0    1.50    24.50
Goblin Lackey            95    50%  +0.5   5.0    6.00    40.43
Drogskol Captain         53    25%   0.0   5.0   11.67   105.67   <- was rank 1 under leverage
```

Two things to read off it rather than rediscover:

- **`assem` and `bare` answer different questions and the pair is the diagnosis.** Zombie
  Apocalypse is a perfect blank that gains 27 when supported and assembles **0%** of games — a real
  payoff the deck cannot deploy, which is a mana or consistency problem, not a synergy one.
  Dragonstorm is the same shape at 62%.
- **A zero-leverage blank outranks a high-leverage near-blank** (Entomb 0.00/0.00 above Flameshadow
  1.50/24.50). That is the strict lexicographic sort doing what was asked; `bare` is primary by
  design. Fix it by reading the `supp'd` column, not by inventing a combined score on one run.

## Speed was the wrong fitness, and execution is the replacement

`Goldfish` measures turns-to-kill against an inert opponent, and it was built to be the combo
fitness. **Measured on `ComboGradientTest`, the gradient points the wrong way**: dismantling Storm
made it goldfish *faster* (5.0 → 4.0). Against an opponent that does nothing the quickest kill is
cheap creatures attacking; a combo deck has to assemble first, so the instrument rewarded exactly
what the search already converges on unaided. Storm's real edge is that Tendrils damage cannot be
attacked, blocked or answered — **the goldfish removes interaction, and interaction-immunity is
the property that matters**, so it is blind to it by construction.

`EngineProbe` measures execution instead: *the payoff resolved, and when it did, the deck had
already deployed the cards that make it worth resolving*. A pile of the format's best individual
cards contains no payoff for any distinctive demand, so it reads **zero** here where it read best
on the goldfish. `Goldfish` is retained as a description of a deck, never as a fitness.

**Measured on `ComboGradientTest`, ALL pool, `MTG_MIN_LANDS=12`, 10 games per step.** `assem` is
the share of games where a payoff resolved with support down; `depth` is the median number of
enablers already deployed:

| cards swapped for good stuff | Storm assem/depth | Reanimator | Affinity | goldfish (all three) |
|---|---|---|---|---|
| 0 (assembled) | **90% / 12.5** | **80% / 3.5** | **100% / 6.5** | 4.0–5.0 |
| 4 | 0% / 0.0 | 60% / 3.0 | 100% / 6.0 | 4.0–7.0 |
| 8 | 0% / 0.0 | 60% / 2.5 | 100% / 6.5 | 4.0–6.0 |
| 12 | 0% / 0.0 | 20% / 0.0 | 100% / 5.0 | 4.5–5.0 |
| 16 | 0% / 0.0 | 0% / 0.0 | 100% / 3.5 | 4.0–5.5 |
| 24 | — | — | 50% / 1.0 | 4.0–5.0 |
| 32+ (pure pile) | 0% / 0.0 | 0% / 0.0 | 0% / 0.0 | 4.0–5.0 |

**The goldfish column has no trend at all** across three decks and thirty-odd steps — that is the
blindness, measured, not argued. The engine column falls to zero on all three.

**Storm falls off a CLIFF at four cards swapped, and that is a finding rather than a defect.** The
interpolation cuts in `PickWeakest`'s ordering — worst individual card first — and Storm's payoffs
are among the worst-rated cards in the format, so the very first cut takes them. **No local search
on any fitness can walk back to Storm**; its payoff has to be seeded and then protected, which is
exactly what the phase-two core lock is for. Reanimator and Affinity have real gradients and would
be findable by hill climbing if anything measured them.

Three rules inside `EngineProbe.Read`, each of which is silent when wrong and each pinned by a
paired negative case in `EngineProbeTests`:

- **Deployment is read generically, execution is not.** Any event carrying a `CardId` names that
  card happening — being milled or discarded is a perfectly good way to deploy a reanimation
  target — and that is the same positional reflection rule `PoolFeatures` harvests demands with,
  so a new event works the day it is emitted. Execution cannot be generic: a Tendrils pitched to
  Faithless Looting emits a `CardDiscardedEvent` carrying its id, and counting that as "the payoff
  went off" scores a deck for throwing its combo away. `IsExecution` is a closed three-event list
  (resolved, creature entered, permanent entered) and the comment says why.
- **Enablers are distinct card INSTANCES.** One ritual cast, resolved and buried is three events
  and one enabler.
- **A payoff is credited before it is deployed**, so a card that is both — a Goblin lord asks for
  Goblins and is one — never counts as its own support. Another copy landing earlier does, which
  is correct. Same rule as `PoolFeatures.Satisfaction`.

`Reading` carries `Depth` and `Payoffs` separately on purpose. Depth 0 with payoffs 0 means the
deck never cast the card it is built around; depth 0 with payoffs 3 means it cast it into an empty
board three times. Those are different failures, and **collapsing them into one number is how the
goldfish managed to look correct**.

## A targeting spec IS a demand when it aims at your own zones

**Reanimator was a payoff for nothing.** `WithReanimate` builds
`SingleTarget(IsCreatureInOwnGraveyardSpecification)`, so its demand lives on
`TargetingStrategy.Specification` — the one property the harvest rule excluded outright, on the
grounds that "what am I aiming at" is not "what does my deck need". That is right for Lightning
Bolt, whose "any creature" is a question about the opponent's board. It is exactly backwards for
Reanimate, a card that does nothing except ask your deck for creatures in your graveyard.

The two are separated **empirically, not by a type list**: a spec is a demand about your deck when
`GetCandidateIds` returns nothing on a battlefield. The battlefield is the shared board; hand,
graveyard and library are where your own cards live. `ZoneSpecification` already implements this
and composites already delegate to it, so `IsDeckScoped` is one loop and no new vocabulary. Empty
means no — a spec that selects nothing in a fixture holding the whole pool cannot be shown to be
about your deck, and adding fewer demands is the safe direction.

## …and when it aims at cards YOU CONTROL — the zone rule was one proxy too coarse

**"Not on a battlefield" is a proxy for "about your deck", and it missed the first real combo this
engine could express.** CMB's copiers read *"exhaust: create a token copy of target **Illusionist you
control**"*. That is a statement about what you must BUILD — but its candidates are on the
battlefield, so the zone rule classified it with Lightning Bolt and threw it away. Measured, all
four combo pieces harvested **zero** demands, `DeckCore.For` returned null, and a two-card loop that
`LoopDetector` finds in two actions was **invisible to the builder**. Exactly the same class as the
reanimator miss the zone rule was itself introduced to fix, one level down.

**Controlled-by-you is what un-shares the battlefield.** `IsControlScoped` decides it the same
empirical way, with no type list: the same card is placed on BOTH battlefields, and a spec is
control-scoped when it accepts your copy and rejects the opponent's. Nothing names
`IsControlledByYouSpecification`, so a new way of writing "you control" works the day it ships.

The opponent-side copies live in their own map and are deliberately **not** added to `placements` —
that dictionary is what `Matches` walks to compute supply, so an opponent-controlled copy in it
would make every "creature an opponent controls" spec answered by the whole pool and move every
supply number in the file.

**Measured before trusting, because the worry was a flood of "target creature you control" pump
spells:**

| | before | after |
|---|---|---|
| demands total | 63 | **68** |
| demands informative | 43 | **48** |
| cores built / distinct | 77 / 77 | **82 / 82** |

**+5, and the reason is the reusable part: demands dedupe by VALUE pool-wide.** Every pump spell
sharing one `CreatureControlledByYou` spec contributes a single demand between them, not one each —
so widening a *classification* rule costs far less than widening a *card* rule would.
`ComboProvingDiscoveryTests.DumpDemandAndCoreCounts` is the instrument; re-run it on both sides of
any further change here.

The combo's untappers still harvest zero demands, and that is correct: they are the SUPPLY side, not
the payoff. Only the copier asks for anything.

Unlocked **6 new concepts** on ALL, all of them archetypes the mode could not previously express:
instants-in-graveyard (100% assembly), cards-in-hand, creature-in-own-graveyard,
creature-in-any-graveyard. This is the third demand found living somewhere the harvest did not
look; the first two are storm and cast restrictions below.

## …and when it asks about the OPPONENT inside a TRIGGER — the fourth instance of one shape

**"Object-referential" was one proxy too coarse, in exactly the way the zone rule was.**
`ObjectReferentialSpecs` held `IsControlledByOpponentSpecification`, and `IsObjectReferential`
recurses into a trigger condition's `Filter` — so CMB's Sanguine Reciprocity, *"whenever an opponent
loses life"*, **was never harvested as a demand at all.**

That is a different failure from the one recorded for a whole session. The handoff and this file both
said the demand had *no suppliers* and was dropped as uninformative. It did not exist. A demand with
no suppliers is visible in a dump and reads as "nothing answers this"; an unharvested one is
indistinguishable from a card that asks nothing, which is why the wrong diagnosis survived.

The exclusion was right for its actual reason and the reason is entirely about the FIXTURE: the
placement pass deliberately puts no cards on the opponent's battlefield (see `IsControlScoped`), so
*"target creature an opponent controls"* is answered by nothing and every removal spell would read as
a dead card. **A trigger condition is never evaluated against that fixture** — `ProbeTriggers` asks
conditions against real EVENTS — so the reason does not transfer. `TargetingOnlyObjectReferentialSpecs`
is the split, and `underTrigger` is set once on the way down so a composite cannot smuggle the old
behaviour back.

## The chained trigger probe: some triggers cannot fire alone

Harvesting the demand is only half. `ProbeTriggers` plays each card SOLO, and nothing a card does on
its own makes an opponent lose life — Covenant of Thorns produces that event, but only *after*
something gains you life. `PoolFeatures.ProbeChainedTriggers` is a second pass seeded from the first:
for each card asking a trigger demand that already has suppliers, replay it with one of those
suppliers and record what newly fires. Depth 2, which is what a two-card combo needs.

Three properties, each load-bearing:

- **The subject is played FIRST and the igniter second.** Covenant asks "whenever you gain life"; if
  the life gain already happened when Covenant arrives it fires nothing and the pass measures the
  same zero as before.
- **Attribution is a DIFF against the igniter alone.** This is the whole reason the pass is not the
  trap it was built to avoid: stocking the fixture so the opponent can lose life would make *every*
  card a supplier, push `supply[d].Count` past `UninformativeShare`, and drop the demand anyway —
  same outcome, more code. Same principle as `MeasureLeverage` subtracting its own control arm.
- **One control run per igniter**, cached, not one per subject.

**Measured on DES, and each half is separately necessary:**

| | demands | informative | cores |
|---|---|---|---|
| before | 71 | 52 | 91 |
| chained pass alone | **71** | **52** | **91** — a complete no-op |
| both | 78 | 53 | 93 |

Reciprocity's demand ends with four suppliers — Bloodhunter Bat, Corpse Knight, The Drowned
Archfiend (all drain on ETB, found by the solo pass) and **Covenant of Thorns, which only the chain
finds.** Disabling `ProbeChainedTriggers` drops Covenant and leaves the other three, which is the
pool-level vacuity check; `ChainedTriggerProbeTests` pins the same thing on a three-card fixture,
with `WithoutTheProducingCard_TheDemandStillHasNoSuppliers` as the guard against crediting everything.

**+7 demands for the classification fix is small because demands dedupe by VALUE pool-wide** — the
same reason `IsControlScoped` cost only +5. Widening a *classification* rule is far cheaper than
widening a *card* rule.

ponytail: an igniter whose events depend on the board will differ between the two arms and be
credited to the subject. Over-attribution rather than under, which is the wrong direction for this
file; tighten by comparing event SHAPES rather than satisfied demands if a real archetype is ever
measured gaining a supplier it should not have.

## Storm was structurally undiscoverable, and now is not

`PoolFeatures` harvests a card's own filter objects, and storm has none — it is a `bool` that
`ResolveSpellAction` multiplies by `MtgGame.SpellsCastThisTurn`. Same for
`RequiresSpellsCastThisTurnRestriction`, whose question lives in `CanCast`. So the strongest deck
in the precon round-robin (Traditional Storm, 63.1%) could not be found by a mode built to find
archetypes.

`PoolFeatures.SpellsCastDemand` is **the one demand in that file that is not the card's own filter
object.** Harvested from `HasStorm` (minimum 2 — a storm spell counts itself, so a minimum of 1 is
satisfied by casting nothing) and from the restriction's type. Supplied by every non-land pool card
costing ≤ 2, because there is nothing to evaluate it against a fixture with: "a spell was cast" is
an event in a turn, not a property of a card in a zone.

It is **skipped by `ProbeCostDemands`**, and that guard is load-bearing. Its supplier set would
otherwise pick a representative that is whichever qualifying card sorts first — and if that is an
artifact, four copies on the battlefield move every affinity card's cost and the probe reports the
entire artifact archetype as demanding "cast spells first". The cost probe reads a BOARD; this
demand is about a turn.

## Supply is NET MANA, and the first attempt at it was pointed the wrong way

Supply was "costs 2 or less" for one run, and it produced a storm deck of **Delver of Secrets,
Llanowar Elves, Imposing Sovereign and Skyknight Vanguard** — cheap *creatures*, which are the
opposite of a storm enabler. A two-mana creature costs you two mana to add one to the count. **The
count is not the resource, the mana is.**

`ProbeManaProfit` measures it: deploy each card into a fixture and read the controller's
`CurrentMana` change minus what it cost. Nothing names an action type, so a mana source written
tomorrow is found the day it exists. Two arms, on the split the engine already makes — a permanent
is put onto the battlefield **and a turn is started**, because every mana rock and dork in this
project produces from an upkeep trigger and a probe without the turn scores all of them zero; a
non-permanent has its effects resolved directly.

| | suppliers on ALL (783 cards) | resulting deck | rank |
|---|---|---|---|
| cost ≤ 2 | **335** | cheap creatures, no rituals | 21st of 39 |
| net mana ≥ 0 | **16** | Lotus Bloom, Mox Pearl, Seething Song, Silt Ritual, Ornithopter Shard + 4 storm payoffs, 13 lands | **1st of 43, 100% assembly, depth 9.5** |

**Cantrips come out negative and are excluded, deliberately.** Preordain genuinely does advance a
storm turn by replacing itself, but counting "draws a card" as mana needs a conversion rate between
two resources — a scoring decision, and nothing in this file scores anything. Understating is the
safe direction for a rule that decides what a deck is built out of.

## LIFT is the only column that tells a synergy from a coincidence

The demand model measures **lexical co-occurrence** — cards that mention the same filter. On ALL,
`EventTriggerCondition{CreatureEnteredBattlefield}` has **453 suppliers** and
`IsCardTypeSpecification{Creature}` has **493**: every creature in the pool. A deck of creatures
plus one ETB payoff satisfies those by accident and ranked near the top of the first reports while
being an ordinary midrange pile. Most of the report was noise, and no amount of tuning the depth
metric fixes that, because the deck really is assembling — it just isn't an archetype.

**Lift is a contrast, and contrast is the only thing that answers it.** `GoodStuffControl` builds
the concept's payoffs, at the same copy counts and land count, into a pile of the format's
highest-`CardDelta` cards; the same probe runs against both under **common random numbers** (same
seeds, so shuffle variance cancels in the difference). `Lift = depth - controlDepth`. If a payoff
executes just as readily surrounded by the best cards in the format as by its own concept, the
concept contributes nothing.

Measured on ALL, and it separates cleanly:

| | n | mean LIFT | mean control depth |
|---|---|---|---|
| broad concepts (>300 suppliers) | 14 | **−0.54** | 3.82 |
| narrow concepts (<100 suppliers) | 19 | **+5.18** | 0.45 |

`IsCardTypeSpecification{Creature}` lands at depth 4.0 against control 4.0 — **lift +0.0**, exactly
the right answer. Ranking is on lift.

## It was vacuous TWICE before it worked, both times invisibly

Worth recording because the failure looked identical to success both times: a plausible column of
numbers that ranked the same as the thing it was supposed to correct.

1. **The control excluded enablers from its filler**, to stop it "rebuilding the concept". That
   guarantees it cannot deploy one, so control depth is 0 by construction. 18 of 43 controls
   scored exactly 0.0.
2. **`EngineProbe.FromConcept` derived enablers from the concept DECK.** A control holds different
   cards, so it had no enablers under that definition either — same symptom, different cause,
   surviving the first fix. Enablers are now **pool**-derived and payoffs deck-derived, so "depth"
   means *how many demand-answering cards did this deck deploy*, a question any deck can be asked.

`EngineDiscovery.Run` now **warns when no control scores above 2.0**, because a control that never
scores is not a baseline and nothing else in the output says so. That check is the automated form
of "verify a test fails when you break the thing it tests" — it would have caught both.

## Ranking is on coverage, not raw depth

Raw depth ranks broad concepts first for free: a deck with 30 enablers deploys more of them than a
deck with 12 whether or not either is an archetype. `EngineCandidate.Coverage` is depth over the
deck's own enabler copies — "how much of my support was down" — which is scale-free. Same bias
`DeckBuilder.PickConcept`'s inverse weighting exists to correct, one step further down the pipe.

**Every viable concept is probed, not a top slice.** Taking the N most distinctive gives N demands
with exactly `MinConceptSuppliers` suppliers, which is the narrow tail rather than a survey, and
the question this mode exists to answer is what the pool supports.

## `SeedConcept` was building concept decks with no payoff in them

**Found by the first mode 7 run, and it had been live for every `conceptSlots > 0` run before
it.** The jam loop samples the whole candidate set — suppliers *and* askers together — so when a
demand has 400 suppliers and 4 askers the askers are essentially never drawn. On the ALL pool
**17 of 39 viable concepts came back with no payoff at all**, and the failures were systematic:
the broader the demand, the rarer its payoff, and therefore the less likely it was to be built.
That is the "24 artifacts and no Atog" failure the method's own contract rules out.

`SeedConcept` now places askers FIRST, capped at a third of the spell slots, before the jam loop
fills the rest. Measured on the same seed: **39 of 39 concepts build, payoffs per deck went 1–5 to
3–6, and `SpellsCastDemand` went from unbuildable to 80% assembly at depth 6.0** — storm went from
invisible to found in one change. `EngineDiscovery` now also NAMES any concept that produced no
deck, because a report showing fewer rows than it probed is indistinguishable from a broken
builder, which is how this survived its first run.

## Known limitation: the cost probe over-attributes convoke

`ProbeCostDemands` puts four copies of a demand's representative permanent on the battlefield and
records every card whose cost moved. **Convoke reduces cost per creature of ANY kind**, so probing
"Zombie creature you control" with four Zombies makes every convoke card look like a Zombie payoff.
In the ALL report, Stoke the Flames and Devouring Light appear as payoffs under nearly every
creature-subtype concept.

Not fatal — those cards genuinely do want creatures, and the metric still measures a real thing —
but it inflates tribal concepts and dilutes their payoff sets. The fix is a control probe:
re-measure with a representative that does NOT match the demand, and attribute only the difference.
Pre-existing, not introduced by mode 7; mode 7 is just the first thing that made it visible.

## The engine record is a CARD POOL, not a decklist

`EngineCandidate.Payoffs` and `.Enablers` are **pool-wide** — every card in the set that asks the
demand, and every card that answers it. `Deck` is one sample from that pool, built by `SeedConcept`
to take the measurements with; it is evidence the pool supports a deck, not a recommendation.

**Payoffs were deck-derived and that hid most of every archetype.** Affinity listed Frogmite and
not Myr Enforcer, Atog or Thoughtcast — cards asking exactly the same thing, absent purely because
the seeder did not draw them. `PoolFeatures.AskersOf(demandIndex)` is the reverse lookup that was
missing; nothing needed the direction until the report became a pool.

This is also the better phase-2 contract: **lock the pool, not the list.** Evolution retunes freely
inside the archetype instead of being frozen to twelve specific cards, which keeps the identity
without the max-density failure (24.4% for jammed goblins against 55% for the AI's half-built one).

## Supply weighting: worked for reanimator, did NOT work for storm

`SupplyOf` returned a flat 1 for almost everything, which made the candidate set correct and
**unsortable** — every supplier scored the same through `SupportScore`, so `CardDelta` broke every
tie and a 513-card reanimator pool got sampled for its best creatures rather than its dozen discard
outlets. It now carries a magnitude per supply kind (net mana and cards for a spells-cast demand,
cards moved for a graveyard one, bodies for a token maker, flat 1 for a plain filter match) and
`SeedConcept` reads it at `ConceptSupplyWeight` 3.0.

Two predictions were set before running it. **One passed:**

| | before | after |
|---|---|---|
| Reanimator sample | Basri Ket, Crusader of Odric, Knight of Glory, Kytheon, Village Ironsmith | **Tome Dredger, Undead Alchemist, The Mere Gives Up Its Dead**, Despoiler of Souls, Endless Obedience |
| depth | 3.5 | **4.5** |

The self-mill and recursion cards arrived, which is exactly what the weight was built to do, so the
signal demonstrably reaches the sampler.

**One failed: storm still has no rituals.** The pool contains Lotus Bloom, Mox Pearl, Rite of
Flame, Seething Song, Silt Ritual and Past in Flames, and the sample deck picked Rain of
Revelation, Thought Scour, Chorus of Whispers and Uncomfortable Chill instead. Cause: `Mana` and
`Cards` are **summed as interchangeable**, and they are not — for a storm turn mana is the binding
resource, because a card you cannot cast does not advance the count. A draw-3 scores the same as a
ritual and has a far better standalone rate.

**Do not "fix" this by weighting mana above cards on instinct.** The engine metric currently rates
the draw build at 90% assembly and lift **+16.5**, higher than the ritual build ever scored, so the
measurement disagrees with the Magic-player prior and only a real deck-vs-deck result should settle
it. The honest position is that discovery's deliverable is the POOL, the pool is correct, and which
subset actually wins is a win-rate question for phase 2 rather than a softmax question here.

## The live problem: a correct enabler SET, selected from by raw card value

Both remaining failures are one cause, and it is now the highest-value thing to fix.

The demand model is right: storm's enablers are the 69 mana-or-card-positive cards, reanimator's
are the 513 cards that answer "a creature card in your graveyard" (every creature, plus the
movement suppliers that actually fill a graveyard). But `SeedConcept` samples within the candidate
set by `CardDelta + SupportScore`, i.e. **by how good each card is on its own**, and the genuinely
enabling minority is swamped:

| | before | after | what it cost |
|---|---|---|---|
| Storm | Lotus Bloom, Mox Pearl, Seething Song, Silt Ritual | Ancestral Recall, Sphinx of Uthuun, Llanowar Visionary, Sylvan Ranger | card draw arrived, **the rituals left** |
| Reanimator | Reanimate + fat creatures | Reanimate + better creatures | still **no discard outlet**, lift −5.5 |

Reanimator's 513-card enabler set contains perhaps a dozen cards that put a creature in a
graveyard, so a value-ranked sample essentially never draws one. Storm's 69 contains ~16 rituals
and Moxen against ~53 cantrips and card-draw creatures, and the draw cards have far better
standalone rates.

**`SupplyOf` already returns an int and nothing reads it as a weight.** The fix is to make supply
strength mean something — a ritual or a discard outlet supplies a concept far more than a card
that merely qualifies — and to have `SeedConcept`'s softmax include it. That keeps the "features
generate, win rate judges" rule intact, since it changes what gets *proposed*, not what gets
scored.

## Causal supply IS built — this section used to say it was not

**Do not re-derive this.** The movement rule lives in `PoolFeatures.Build` step 3a: `CardProfile`
records how many OTHER cards a card moved into each non-battlefield zone, `DemandZone` finds the
single zone a demand's candidates live in, and a card that moves cards there **supplies** the demand
and is **struck from its askers** — a card does not demand what it creates. No filter match is
required on the moved card, deliberately: a discard outlet is a graveyard enabler whatever it
happened to pitch in one probe.

Measured on ALL, `IsCreatureInOwnGraveyardSpecification`: **513 suppliers, 118 carrying weight above
1**, and the top of the list is exactly the outlets —

```
The Mere Swallows All(7), Abyssal Dredger(5), Drown in the Mere(5), Flood the Vaults(5),
Sunken Chorus(5), Unhallowed Rite(5), Grim Excavation(4), Mere-Drowned Scribe(4)
```

`CausalSupplyTests` dumps this per demand. Run it before believing any claim about what a slot
contains.

## The probe ACTIVATES the subject, and until it did, no outlet produced anything

`ProbeCardProfiles` deploys a permanent and starts a turn — which is what fires the upkeep triggers
every mana dork produces from — and then stopped. **It never activated anything.** Measured on ALL
before the fix: **140 cards carry an activated ability and not one supplied any demand causally**,
against 44 cards with causal supply pool-wide. Every aristocrats sacrifice outlet and every
ability-based discard outlet in the pool was invisible on the production side, so `DeckCore.For`
built reanimator and sacrifice cores with nothing to fill the graveyard.

Actions come from `MtgActionGenerator.GetLegalActions`, never built by hand — the project's standing
rule, and it is what makes cost payment, targeting, `MaxActivationsPerTurn`, summoning sickness and
exhaust enforced by the engine rather than re-derived. The generator fills `AdditionalCostPayments`
itself, so a discard or sacrifice cost is paid with no choice to resolve. **One activation per
ability**: the generator emits an action per target, so taking every action would fire a targeted
ability once per target and over-attribute what it moved.

Four parts, each measured, each necessary and none sufficient:

| | cards with causal supply |
|---|---|
| before | **44** |
| activate only | 49 |
| + sacrifice fodder | 63 |
| + fodder matching a NARROW filter | 65 |
| + track the subject itself | 71 |
| + pay a spell's CAST costs | 76 |
| + give the opponent a board | **79** |

- **Fodder**, because the subject is otherwise the only permanent on the battlefield — the filler is
  stocked into library, hand and graveyard — so a sacrifice cost had nothing to pay with.
- **Narrow filters** (Goblin Grenade wants a Goblin, Devout Chaplain a Human, Atog an artifact) are
  answered by **evaluating the filter against the pool**, never by a subtype list. The cost carries a
  `TargetSpecification`; running it is the same thing `Build` does for demands.
- **The subject is tracked**, because a card sacrificing ITSELF still puts a card in a graveyard —
  Heartfire Immolator, Generator Servant, Brittle Effigy.

**Scoped to the movement channel deliberately, and check this before touching it.** Activating costs
mana and cards, so folding it into the mana/card readings would move the storm enabler set, which
this file records as calibrated. Those channels read the **pre-activation** state and the storm slot
is byte-identical across the change at 82 cards, min 6 — so any movement in a run is attributable to
production alone. Fodder is placed inside the activation phase for the same reason rather than in the
fixture: a board full of creatures changes what a card produces (Wirewood Conduit adds mana per
creature you control).

**19 cards throw, all planeswalker loyalty abilities.** They are surfaced by name and fall back to
the *whole* pre-activation state, so they get exactly the old measurement rather than a partial one.

**Cast costs are the SPELL half of the same gap.** `ProbeCardProfiles` resolves `spell.Effects`
directly and never casts, so `Card.AdditionalCastCosts` were never paid. Casting for real was
**rejected, not overlooked**: a cast goes through the generator, which needs a legal target, and the
fixture leaves the opponent's battlefield empty — so every removal spell in the pool would stop
producing anything, trading 5 cards for a few hundred. Paying the costs beside the existing
resolution buys the missing half without giving up the half that works.

**That arm RE-RESOLVES from the fixture rather than reusing the cost-free state**, for the scoping
rule above: paying Magmatic Insight's discard before drawing changes its card reading from +1 to 0.
`ponytail:` which leaves the card channel modelling a storm enabler as free when it is not — worth
taking as its own change with its own before/after on the enabler set, never folded into a movement
fix.

**The opponent gets a board, and ONLY inside the activation phase.** Most of what remained is an
ability needing a target ("sacrifice this: deal 2 damage to target creature"), and an empty opposing
board means the generator emits no action at all. This does **not** contradict the empty opponent
battlefield `IsControlScoped` and `TargetingOnlyObjectReferentialSpecs` depend on — that is the
PLACEMENT fixture, whose emptiness stops "a creature an opponent controls" being answered by the
whole pool. This is the card-profile fixture and shares no state with it. Opponent cards are
deliberately **not tracked**, so destroying their creature never counts as filling YOUR graveyard.

**Still invisible, 5 of 38 cost-payers, not diagnosed**: Arms Dealer, Demonic Embrace, Fauna Shaman,
Generator Servant, Hanged Executioner.

`CausalSupplyTests.AnActivatedSacrificeOutlet_FillsTheGraveyard_AndAVanillaBodyDoesNot` pins the
ability half, with the vanilla body as the vacuity guard — a probe that credited every card it played
would pass the first assertion alone. `ASacrificeCastCost_FillsTheGraveyard_AndACostlessSpellStillMills`
pins the spell half, and **its second assertion is the load-bearing one**: the first draft of that arm
returned the bare fixture when a spell had no cast cost, rather than the state where its effects had
resolved — which would have reported every mill spell and every tutor in the pool as moving nothing,
silently. Both pins were confirmed to fail with their arm disabled. `CostAsProductionTests` is the
`[Explicit]` diagnostic that sized the gap and reports what is left.

Mode 7 on DES holds at 44 cores across the whole change. One renames — Blood for Bones → Second
Burial — because Blood for Bones now FILLS the graveyard it used to only ask for, so the "a card does
not demand what it creates" rule moved it from payoff to enabler. The same reclassification Entomb
got, firing on a newly visible producer; the archetype survives under the next member of its
equivalence class.

## The live limitation: SupplyOf merges channels that mean different things

`SupplyOf` returns **one int** covering every way a card can answer a demand — net mana and cards
for a spells-cast demand, cards moved for a graveyard one, bodies for a token maker, flat 1 for a
plain filter match. They are summed and maxed into a single scalar, so nothing downstream can ask
for one kind specifically.

The cost is visible in the same table: `Grave Titan(5)`, `Hornet Queen(5)`, `Throne of Empires(5)`
sit level with genuine self-mill in the graveyard demand's top twelve. A fatty and a mill spell are
both "enablers" of *a creature card in your graveyard*, and a reanimator deck genuinely wants both
— but it wants them in **different quantities and different roles**, and one merged weight cannot
express that.

**The causal channel is now recorded separately as well as merged** — `CausalSupplyOf` /
`CausalSuppliersOf`, written only by the movement pass, because that is the one channel whose
meaning is *this card CAUSES the thing* rather than *this card IS the thing*. `SupplyOf` is
unchanged, so nothing that read the merged number moved.

`DeckCore.For` splits on it, and the reanimator core is now the deck a human would describe:

```
Angel of Second Rites: 3 slots
   4x Payoff                                             [23]
   8x IsCreatureInOwnGraveyardSpecification             [458]   <- things to reanimate
   8x IsCreatureInOwnGraveyardSpecification [enablers]   [42]   <- things that put them there
```

The two are **disjoint, with a card that does both filed as causal** — slots are counted
independently, so an overlap would let one copy satisfy both, the same rule that strips payoffs from
their own support slot.

**"Causal is the scarcer role" is true for zones you have to work to fill, and NOT for the hand —
now gated by likelihood.** Selecting the demand with the most causal suppliers used to land on *"a
Goblin in your hand"*, where the causal side was **72 cards against 23 declarative**: the movement
rule requires no filter match on the card it moved, so every draw spell counted as putting a Goblin
in your hand, and Lackey's core asked for 13 draw spells against 11 Goblins.

**You choose what you pitch; you do not choose what you draw.** A mover is now credited only if it
is **DIRECTED** — its own card data names this demand, which is what Entomb's
`SelectCardFromZoneAction.Filter` does — or **LIKELY**: `1 - (1 - density)^moved >= 0.5`, i.e. more
often than not at least one card it moved matches, where density is the demand's declarative share
of the pool. Measured on ALL, the effect is surgical:

| core | before | after |
|---|---|---|
| Goblin Lackey | 4 slots, 13x from a **72**-card enabler slot | **3 slots**, 10x from 23 real Goblins |
| Angel of Second Rites (reanimator) | 458 declarative / **42** causal | 458 / **42**, byte-identical |

The probe cannot answer this by inspection — its moved cards are a fixed seven-card filler and
never a Goblin — so the expectation is the honest form. Understating stays the safe direction: a
card cut from the causal channel still sits in the declarative slot if it matches the filter
itself. `ABlindDrawDoesNotEnableANarrowHandDemand_ButAMillStillEnablesTheGraveyard` pins both
halves in one pool, and the mill half is the vacuity guard — switching the channel off entirely
would pass the draw half alone.

**Known ceiling, marked `ponytail:` in the source: density is measured over the POOL where what
matters is density in the DECK.** A built Zombie deck mills its own Zombies far more reliably than
the pool share implies, so a narrow-tribe self-mill can fall below the gate. No deck exists at
harvest time; revisit if a real archetype is measured losing its enablers.

`AGraveyardCoreSeparatesTheTargetsFromTheOutlets` still names the graveyard demand rather than
picking by count — the selector is no longer load-bearing for the Goblin case, but naming it is
still the honest way to ask.

## Settled: this engine cannot contain an infinite combo, so stop looking for one

**Do not rebuild the combo-discovery plan.** It was scoped, the cheap half was run, and the answer
is structural rather than a matter of search budget.

The produce/consume graph needs no new machinery — `A -> B` when A supplies a demand B asks, which
is `SupplyOf` and `DemandsOf`, and a card is struck from the askers of any demand it supplies so
self-loops cannot occur. `CausalSupplyTests.DumpProduceConsumeCycles` enumerates it. On ALL,
restricted to demands with ≤60 suppliers: **16 narrow demands, 38 cards that both give and take,
86 two-card cycles — and every one is a tribal membership loop.**

```
Arms Dealer      <-> Goblin Chieftain    (both ARE Goblins and WANT Goblins)
Atog             <-> Frogmite            (both artifacts, both want artifacts)
Cemetery Reaper  <-> Diregraf Captain    (Zombies)
```

That is not a combo, it is a tribe — the thing `DeckCore.For` already expresses as one slot. The
mechanism is that a lord is in the SAME demand's supplier list and asker list at once (Goblin
Chieftain IS a Goblin and WANTS Goblins), so every pair of lords points both ways automatically;
eight goblin payoffs give 28 pairs that are all one archetype.

**An asymmetry filter was tried and does not discriminate — do not retry it.** "Symmetric on one
demand = tribe, different demands each way = combo" is the right idea and it reports 65/21 on ALL,
but all 21 are false positives: `Subtype{Zombie}` and `And{Subtype{Zombie}, OnBattlefield,
ControlledByYou}` are distinct demand OBJECTS meaning nearly the same thing, so a tribal pair reads
as asymmetric. Same near-duplicate-concept problem this file already records for LIFT-ranked
selection. Canonicalising the demands would fix the false positives and would not change the
answer.

**No graph over this vocabulary can find a combo**, canonicalised or not. The demands are
membership predicates (subtype, card type, zone) and trigger events. There are no OPERATIONS in
them, so the best a cycle can ever mean is "these two cards are in the same category and both like
that category".

**The reason was the engine's action vocabulary, not the cube's card list.** A Splinter Twin combo
trades OPERATIONS: untap, copy, sacrifice. The engine had no untap effect and no copy action —
`ExhaustCreatureAction` only taps, only `StartTurnAction` cleared `IsExhausted`, and storm lives
inside `ResolveSpellAction` rather than as a targetable copy.

> **SUPERSEDED IN PART — `UnexhaustCreatureAction` SHIPPED.** The untap half of that argument is
> gone: an untap effect now exists (see `MtgCore/CLAUDE.md` §"Exhaust"), so a creature with a tap
> ability can be re-used within a turn and untap-trading combos ARE expressible. **Re-run both
> `LoopDetector` sweeps** — single cards and the 241k pair sweep — because this is exactly the
> moment their zero should be able to move, and if it does not, suspect the detector before
> believing the pool. Copy is still absent as a targetable action, though a token template carrying
> `CopyOnEnterComponent` reaches most of the same ground.
>
> The paragraph below remains correct about the **produce/consume graph**: that graph is built from
> membership predicates and has no operations in it, so it still cannot find a combo no matter what
> primitives exist. The simulation-based `LoopDetector` is the instrument, exactly as stated.

Without untap you could not re-use a mana source; without copy you cannot loop a spell. **No
infinite combo was expressible, so none could be discovered.** The verifier, the resource
fingerprint and the dominance-cycle check were all cancelled — they would have been correct code
searching a space that was provably empty.

**And when that happens, the detector to build is the SIMULATION one, not this graph.** A rigged
fixture plus a dominance check — same permanents, every resource ≥, at least one strictly up —
reads real game state, so it needs no vocabulary at all and sees a mechanic the day it ships. That
is also the only version that can answer the BALANCE question, which is a separate and standing
use: *"did this set change accidentally print a two-card loop?"* is worth asking on every set
change, whether or not anyone wants to build a combo deck.

Two rules for building it, both already paid for elsewhere in this file:

- **Plant a combo before trusting a zero.** A detector that reports "no loops in the cube" is
  indistinguishable from a broken one. Assert it FINDS a deliberate two-card loop in a test-only
  card set first, then assert it reports zero on the real pool.
- **Key on a RESOURCE FINGERPRINT, not a `GameState` key.** Two iterations of a loop hold different
  card instance ids, so state equality never fires. Use a multiset of (card name, tapped, counters)
  plus mana, hand count, storm count, life and graveyard. And require reachable lethal inside
  `GameRunner`'s 200-action cap — a loop that needs more iterations than that ends the game as a
  DRAW, not a win.

## Detectors know whether an archetype can be BUILT

`DeckCore.PlayableIdentities(pool)` answers which of the fifteen identities a core can be assembled
in, and **empty means the archetype needs more than two colours and no ordinary deck can play it**.
Mode 7's report carries it as a `cols` column — read it before reading LIFT, because an unbuildable
archetype still posts a healthy lift. The evolver drops unbuildable engines before cutting the tier,
and names them rather than dropping them silently.

A slot's colour cost is **the cheapest way to fill it, not the union of its members** — a slot holds
interchangeable cards by definition, so a Twin slot offering a red copier and a blue one costs
whichever the deck can cast. The floor is in COPIES, so an 8-copy slot needs two distinct playable
members, not one.

**Null identities mean "not computed", empty means "computed and unbuildable".** A report saved
before the colour column carries null; treating that as unbuildable empties the entire engine field
and the run comes back clean having seeded nothing. Re-run discovery rather than inferring anything
from an absent column.
