# AI strength harness — measured results

Not loaded into context. Read when a change touches what these runs measured;
the live rules that came out of them are in `.claude/rules/`.

## Measured

| Change | Result | Verdict |
|---|---|---|
| Terminal discount vs none | 50.2% ± 1.5pp, 1120 games | neutral — kept |
| Fastest-win vs first-win | 50.1% ± 1.5pp, 1120 games | neutral — kept |
| Toughness weight 0.5 | 50.2% ± 1.5pp, 1120 games | neutral — **not shipped** |
| Toughness weight 1.33 | 51.0% ± 1.5pp, 1120 games | neutral — **not shipped** |
| Resolving choices before scoring | 50.2% ± 1.5pp, 1120 games | neutral — kept, see below |
| Keyword term (2/15) vs none | 50.9% ± 1.5pp, 1120 games | neutral — kept |
| Keyword term wall-clock cost | +0.3% time, +1.6% actions/game | free |
| Wider simulated own-turn, all turns (3 actions) | 51.2% ± 1.5pp, 1120 games | neutral — **not shipped** |
| Wider simulated own-turn, first turn only (3 actions) | 53.0% ± 1.5pp, 1120 games | neutral — **not shipped** |
| Weights const → init-only property | −0.6% wall time, actions identical | free |

The self-check numbers above were taken **before** the keyword term shipped, so `Default()` is no
longer the AI they were measured on. Re-measured with keywords live, `DefaultAgainstItself_IsEven`
reads **52.7%** — still even within 2 SE, but do not read a 1.4pp move between those two figures as
a finding. **A self-check number is only comparable to one taken against the same `Default`.**

## The rollout asymmetry: a real defect that does NOT move the win rate

`PlayGreedyTurn` played a land and **exactly one** other action per simulated turn while
`SimulateOpponentTurn`'s `BoardOnly` mode LOOPS every attack — the model gave the opponent a whole
turn and us a single play. A mana engine's payoff turn is *activate, cast, cast, cast*, a shape that
rollout cannot represent at any lookahead depth or evaluator weighting.

**The defect is demonstrable at the function level**, pinned by `ConduitBehaviourTests`:

| | |
|---|---|
| the activation is offered by the generator | yes |
| mana gained, and the evaluator's response | **4 mana, 34.00 → 34.00** |
| attacking spends the tap? | no — `IsExhausted` is separate from `HasAttacked` |
| activate-then-cast / activate-then-deploy-wide found once in play? | **yes, both** |

So the broken decision was CASTING it: priced as a vanilla 1/1 for 2, the pilot left it in hand —
measured at *drawn 3, cast 0* in one real game. Widening the rollout doubled Conduit casts (3 → 6)
and tripled activations (1 → 3) over 8 games, **and ran faster** (6550 ms → 5488 ms).

**And it is worth nothing measurable in a win rate**: 51.2% for the full version and 53.0% for the
first-turn-only variant, against a `DefaultAgainstItself_IsEven` baseline of 51.3%. Read against that
baseline rather than against 50%, the full version is exactly neutral and the cheap one is +1.7pp at
1.1 SE. `selfActionsPerTurn` defaults to 1; the knob exists so the question can be reopened.

**Two things to carry rather than re-derive:**

- **The population caveat is the most likely explanation and was stated before the run.** The harness
  drafts CSC, where tap-ability mana engines are thin, so a change affecting a handful of cards is
  diluted across 1120 mixed games — the same reason the half-applied-action fix read 50.2% while
  fixing a real bug. **A null here is weak evidence.** The DES gauntlet is the better instrument for
  this particular change and has not been used for it.
- **The cheap variant beating the full one is NOT established.** 51.2 and 53.0 are 1.8pp apart at
  1.5pp SE each. A plausible story exists — three actions on *every* simulated turn over-commits the
  deeper lookahead to a greedy line — but it is a hypothesis, not a finding.

## The keyword cost run is a worked example of lesson 1

Measured AB — keywords on first, off second — it read **+10.7% time against +1.6% actions**, and
the obvious reading is that the ~9pp gap is the `GetEffectiveStats` allocation, since actions
barely moved. That reading was **wrong**, and it was argued in this project before the check that
disproved it had been run.

Re-run ABBA (on, off, off, on) the two configurations land on **180s each**. The raw sequence was
83 / 89 / 91 / 97 — rising monotonically across the run *regardless of configuration*. It was
machine drift, and the AB ordering handed all of it to whichever arm ran second.

**`AvgActions` is what makes this legible, and it is now on `Outcome` for that reason.** Actions
were 63.0 vs 62.0 in every one of the four runs — deterministic and repeatable — so the workload
difference was real, tiny, and entirely separate from the clock. The rule the project already had
("read actions/game beside the clock") is necessary but not sufficient: when actions and time
disagree, the honest conclusion is *unexplained*, not *therefore the thing I just changed*.

**Mirror-match a configuration against itself rather than rebuilding an old commit.** Both arms
identical within a run makes the run time purely that configuration's cost, and `Keywords(0f)` is
behaviourally identical to the commit before the term existed — the term is appended last in
`Explain`'s sum, so at zero every other term adds to exactly the float it did before, and
`ScanBattlefield` skips the allocation. Same binary, so a stale `bin/` cannot measure code that
was never compiled in.
