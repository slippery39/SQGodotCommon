# Handoff — Colours: a second mana track, and what the first two evolution runs said

**Read this, then the `## Colours` section of the root `CLAUDE.md`, then `MtgCore/CLAUDE.md`
section "Mana System".**

Supersedes nothing. `HANDOFF-DeckIdentity.md` and `HANDOFF-ConstructedEvolution.md` remain correct on
cores, discovery and mode 6 mechanics — this adds the colour dimension to all of it.

State at handoff: **MtgCore 846/846, MtgSimulator 405/405, SQGodotCommon 119/119,
ImmutableGameObjects 106/106**, HEAD `9ae733b`, 19 commits.

---

## 1. The headline

The engine had no colours. It now has them, on **two independent tracks**: a land grants 1 generic
AND its colours, and a cost of "1W" spends 1 generic and 1 White. The two never substitute for each
other, so payment is fully determined — no ordering choice, no solver, no manual tapping, which was
the design constraint that made this shape the right one.

**Coloured mana DEPLETES and refills each turn, exactly like generic.** It is not an Eternal-style
permanent threshold. That was argued and chosen deliberately: under a non-depleting model one source
unlocks a colour forever, so in a 40-card limited deck colour commitment becomes strictly negative EV
and a drafter is taught the opposite of the skill.

Everything else in this document follows from one measured fact, below.

---

## 2. The one number to carry

Sources of a colour needed to cast a card ON CURVE, 90% of the time, in a 60-card 24-land deck.
**Measured in this engine, not taken from paper Magic** (`ManaBaseCalibrationTests`):

| pips | cost 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|
| 1 | 13 | 12 | 11 | 10 | 9 | 9 |
| 2 | - | 18 | 17 | 17 | 15 | 15 |
| 3 | - | - | 22 | 22 | 21 | 20 |

A 24-land two-colour deck split 12/12 casts a **single pip 89% on turn one and 94% by turn three,
but a double pip only 65% by turn three** — 17 of its 24 lands would have to be one colour.

**So a double pip is effectively a mono-colour card, and the entire colour constraint lives in pip
DEPTH rather than in colour COUNT.** Single-pip greed is barely taxed. Design the constraint there.

**Paper tables do not transfer and must not be used.** The opening hand is guaranteed to hold exactly
three lands (`SetupGameAction.OpeningHandLandCount`), drawn uniformly from the manabase, which makes
early colour access far more reliable here than a real seven-card draw. Anything lifted from Frank
Karsten systematically over-builds. Re-run the calibration if that rule ever changes.

---

## 3. What exists now

| | |
|---|---|
| `ManaPool` / `ManaColor` | five ints; a card's pips, a land's production, a player's colour max/current |
| `ManaEngine.GrantLandMana` | the single place a land pays out — generic, coloured and deferred |
| `CostEngine.ValidateManaPayment` / `PayMana` | the single pair every cast path pays through |
| `ManaBase.Build` | the only place a manabase is made; allocates by MEASURED DEMAND, not pip count |
| `ColorIdentity` | 5 mono + 10 pairs; a card is legal when its pips are a SUBSET |
| `IdentityValues` | per-(card, identity) win rates, shrunk toward the pooled rate |
| `DeckBuilder.IdentityForSlot` | one field slot per identity, plus an unconstrained wildcard |

**Sets**: CSC, LEG and CMB are all assigned and verified. **Hollowmere was RETIRED** — it was built
around having no colours ("ten overlapping themes stand in for colours") and its tribes do not map
onto five colours evenly; Humans alone were 75 of 308 cards. A set designed WITH colours is the
intended successor. **The set menu is now `1=LEG 2=CSC 3=CMB 4=DES 5=ALL`** — it has shifted twice.

**Three win-rate tables**, weighed against the feature scores (`DeckFit`, `SupportScore`) which are
theoretical rather than measured:

| table | answers | scope |
|---|---|---|
| `CardDelta(name)` | worth in a random deck of its colours | persisted |
| `CardDelta(name, identity)` | worth in mono-red as against red-white | persisted |
| `contextValue` (`OutputProbe`) | worth in THIS deck | run-scoped, discarded |

The identity rate is shrunk TOWARD the pooled rate, never added to it — they are one population
viewed two ways, and summing double-counts. Both persisted tables come only from PRESIM random decks;
nothing from an evolved deck may enter either.

---

## 4. The two runs, and what they actually showed

Both 16 slots (15 identities + wildcard), CSC, 30 generations, ~2h each.

**Run A had culling ON (the default) and its standings are unusable.** Run B turned culling off.
Sorting the change by whether a deck had been culled in A gives a perfect separation:

| culled in A (young) | change | never culled in A (age 30) | change |
|---|---|---|---|
| UR-Midrange | **+28.3** | G-Midrange | +4.0 |
| BG-Midrange | **+18.3** | WG-Control | +2.7 |
| U-Midrange | **+15.7** | WB-Aggro | +2.0 |
| UB-Aggro | +5.4 | B-Control | +1.0 |
| BR-Aggro | +2.7 | Wildcard | -5.6 |
| UG-Control | +1.0 | WU / RG / W / R / WR | -6.6 ... **-26.0** |
| **mean +11.9** | | **mean -7.1** | |

All six culled decks improved. **A culled slot is perpetually half-built, so every other deck farms
it for free wins** — which inflates the survivors and deflates the culled. Field spread halved,
41.3pp to 24.3pp, once culling was off.

**Consequence: "blue is the weakest colour" was an artifact of run A and was reported as a finding.**
Mono-U went 26.0 to 41.7 and UR-Midrange 23.0 to 51.3 once they were allowed to finish. Blue is
middling.

**Run B (culling OFF, all decks age 30) — the readable one:**

```
WB-Aggro 62.7 | WG-Control 61.0 | Wildcard 57.7 | BR-Aggro 54.7 | RG-Control 54.7
G-Midrange 52.7 | BG-Midrange 52.3 | B-Control 52.0 | W-Aggro 51.3 | UR-Midrange 51.3
UB-Aggro 46.7 | WU-Control 45.7 | U-Midrange 41.7 | UG-Control 38.7 | R-Aggro 38.7 | WR-Midrange 38.3
```

Three observations from the lists themselves:

- **Decks collapse to mono inside a two-colour identity.** The best deck, `WB-Aggro`, plays 22 Plains
  and not one black card. `UR-Midrange` plays 23 Mountain and no blue. The identity is a PERMISSION,
  not a requirement, and section 2 explains why focus wins. Two-colour decks that stayed two-colour
  did it with cheap pips (WG: Elvish Mystic and Scavenging Ooze under white three-drops).
- **The same colour, two slots, 11pp apart.** `WB` (mono-white in practice) 62.7% against `W-Aggro`
  51.3%. `W-Aggro` spent slots on colourless filler (4 Manifold Key, 4 Perilous Vault) where `WB`
  played 4 Angelic Destiny and 4 Sublime Archangel.
- **The bottom decks are incoherent, not off-colour.** `WR-Midrange` (last) plays 12 colourless cards
  that ask nothing of its colours; `UG-Control` runs singleton Overrun / Shifting Ceratops / Thragtusk
  under three 6-7 drops. Builder results, not colour results.

**RUN-TO-RUN VARIANCE DWARFS THE SIGNAL.** `WR-Midrange` went 64.3% (best in A) to 38.3% (worst in B).
That is 26pp against a ~2.8pp standard error on a 300-game round-robin, so it is not measurement
noise — different seeds evolved genuinely different decks. **One run cannot establish a colour-pair
tier list.** Anything conclusive needs several seeds averaged or a fixed-seed A/B.

**The one result that replicated**: the wildcard placed 3rd-4th of 16 in both runs, having genuinely
used its freedom (run B: `W28 R11 G4`). Above median, never dominant, and both times it converged on
"best pair plus a small splash" rather than a real three-colour deck. Weak evidence that the colour
constraint has a small real cost with no compensating benefit — but n=2.

---

## 5. Scars worth not re-earning

**A slot's colour identity was dropped by a code path that did not pass it, twice.** The cull's
re-seed, and `SeedDistinct`'s own last-resort branch three lines below its correct one. Eight of
sixteen slots silently stopped being the archetype they were named for; every column in the report
read normally, and deck AGE was the only tell. `identity` is now a REQUIRED parameter on
`DeckBuilder.Seed`, positioned before the optional ones, so omitting it does not compile.

**Nothing would have cleaned it up, because the scoring PROTECTS a contaminant.** A card illegal in an
identity has zero games in that cell — presim never plays it there — so the identity lookup falls back
to the POOLED rate. Measured: Baneslayer Angel reads **+5.90 inside a mono-blue slot**, better than
every legal blue card, so cut scoring kept it for 23 generations. `DeckBuilder`'s "converges INWARD"
claim is true for a `DeckCore` pool lock and FALSE for colour.

**Null and empty are opposite things** in `EngineCandidate.Identities` (not computed vs computed and
unbuildable) and in a deck's identity (dropped vs deliberately unconstrained). Collapsing either was a
real bug within minutes of writing it.

**Beware name-anchored bulk edits over card files.** Anchoring an insertion on any quoted occurrence
of a card name puts the edit on whatever card is defined NEXT, because names appear in other cards'
doc comments. Llanowar Elves came out needing `UUBRG` and Lotus Bloom — a colourless artifact — needed
`UU`, and the whole suite stayed green. Anchor on the FACTORY CALL and assert an expected table.

**`sim_results/` resolves against the WORKING DIRECTORY**, which under `dotnet test` starts as the
test binary's folder. Anything reading or writing it must call `TestPaths.ChdirToSolutionRoot()`. A
table loaded from the wrong place is EMPTY, not absent, so every card reads 0.00pp and the failure
looks like missing data rather than a wrong path.

**Do not guess a games threshold.** `MinPairGames` was once set to 200 by analogy against a
busiest-pair of 166, silently disabling every synergy path while the tests passed.
`IdentityShrinkK = 75` was instead calibrated against a measured run (median 24 games/cell, p10 9).

---

## 6. What to do next

1. **Several seeds, then a tier list.** Section 4 shows one run cannot support one. Run 3-5 seeds with
   culling OFF and average the overall rates. Until then treat every colour-pair ranking as noise.
2. **Decide whether culling should default to OFF for colour-slot runs.** It is currently ON and it
   demonstrably distorts the field. A slot that cannot win is now information about a colour pair
   rather than a wasted slot, which is an argument the old design never had to answer.
3. **Ask why the wildcard keeps placing high.** If unconstrained decks are genuinely better, the
   manabase model is too generous — most likely `ManaBase.Build`'s allocation being PROPORTIONAL where
   the real question is a threshold one. A splash needs its nine sources or it is a dead draw, and
   does not care that it is two cards of the deck. Upgrade path is marked in that file.
4. **Colourless filler in bad decks** (section 4) suggests `Fill` weights colourless cards too kindly
   — they are legal everywhere, so they never lose the identity-scoped comparison. Measure first.
5. **Draft picker still knows nothing about colour**, and colour commitment is the core drafting
   skill. Bots will build unplayable five-colour piles. Both trained models need retraining after.
6. **`MtgCardTheme` frames are coloured but the draft UI is not colour-aware** otherwise.
7. **Dual lands and fixing do not exist.** CSC's own 42 lands were deliberately excluded when a dual
   was just a basic; that reasoning is now void. `LandColorComponent` + `BonusManaLandComponent`
   already express basic / dual / tap land / colourless. When they arrive, `ManaBase` needs its second
   phase: requirements (what the deck needs) versus availability (what can supply it), priced against
   the curve — a tap land is a real cost to a one-drop deck and nearly free to a control deck.
8. **Gold cards are under-sampled in the POOLED table** — 27 median games per card against mono's 112
   and colourless's 370, because a gold card is legal in exactly one identity. Its per-identity cell
   is fine. Fix by weighting presim deck allocation toward pair identities.
9. **Activated abilities are generic-only** — no pips. Marked in `ActivatedAbilityComponent`.

---

## 7. How to reproduce

Regenerate the card-value table (four tests need it; `sim_results` is untracked):

    dotnet test MtgSimulator.Tests --filter "FullyQualifiedName~CardValueSweep.SweepTheCoreSetCube"

A 16-slot run with culling OFF — mode 6, set 2 (CSC), then defaults except `n` at the culling prompt.
Pipe blank lines for the defaults, with `n` as the 13th line.

~2 hours. Confirm the header reads `culling OFF` and `Pre-simulation: 15/15 identities represented`
before letting it run. Every deck should finish at **age 30**; if not, something is re-seeding.
A colour-identity violation now THROWS rather than producing a report that reads normally.
