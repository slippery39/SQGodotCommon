---
paths:
  - "MtgSimulator/Draft/*.cs"
  - "MtgSimulator/CardPool.cs"
  - "MtgCore/Sets/*.cs"
  - "MtgCore/Sets/*/*.cs"
  - "MtgSimulator.Tests/*Draft*.cs"
  - "SQGodotCommon/MtgGame/Draft/*.cs"
---

# MtgSimulator — draft, card sets and draft training

Loaded when you touch drafting, a card set, or the trained picker model.
Measured per-set draft results live in `docs/findings/draft-training.md`.

## Draft

Deck *selection* as a game mode, for human and AI players alike. Lives in `Draft/`, namespace `MtgSimulator` (flat, like `Decks/`).

**Not a `GameState`.** `DraftState` is a plain immutable record graph. Drafting needs none of the action stack, pipelines, choice resolution, or event log, and the cards it holds are owner-agnostic templates that never enter a `GameState`. Do not migrate this to `GameAction`s.

**Two formats, one model.** `DraftFormat.Booster` (15-card packs, pick one, pass, 3 packs) and `DraftFormat.Digital` (offered N cards, pick one, repeat — seats never interact). `Draft.Create` pre-generates every pack and every offer up front into each seat's `Queue`, so the only per-format branch in `ApplyPicks` is *rotate the remainder* vs *open your own next group*. Booster pass direction alternates per pack via `DraftState.Round`.

## Card sets

Which cards get drafted comes from a `CardSet` (`MtgCore/Sets/`), not from `CardLibrary.All`
directly. `DraftRunner` and `DraftTrainer` both take an optional `CardSet set` parameter
defaulting to `SetRegistry.Default`; `DraftScene` has a single `DraftedSet` field. Swapping
sets needed no change to `Draft` itself — `Draft.Create` has always taken an
`IReadOnlyList<Card>` card pool.

**Models are per-set.** `DraftTrainingStore.PathFor(setCode)` gives the model path;
the Legacy set keeps the original unsuffixed `sim_results/draft_training.json` so the existing
model and its Godot asset copy load without migration, and every other set gets
`draft_training_<code>.json`. `DraftScene` derives its `res://` asset filename from the same
`PathFor` call, so the two cannot drift apart.

**A new set must be trained from scratch, never bootstrapped.** `DraftPickers.Trained` is keyed
by card name and scores unknown cards at exactly the prior. Bootstrapping a new set from an old
model would have every new card score 0 against known cards scoring up to +16.8, so they would
be passed over every pick, never make a deck, and never accumulate data — a self-reinforcing
blind spot. A fresh run uses card-agnostic Curve/Random, which samples new cards uniformly.

**Train from scratch after a rules change too, not just a new set.** The stored values describe
how good a card was under the rules it was measured in. When the Flying restriction landed it
changed what ~55 cards were worth, so every value in the model was describing a game that no
longer existed — bootstrapping from it would have drafted decks by obsolete valuations.

**Bootstrapping and merging are separate decisions in mode 4, and conflating them is a trap.**
"Merge into it rather than replace?" governs only the *output file*. Whether the drafters use
the existing model — which decides which cards get sampled, and therefore which get measured at
all — is a different question, and the console now asks it separately: *"Draft with the existing
model? (Y/n — n trains from scratch)"*. Answering "replace" alone still bootstrapped the
drafters, which silently produced a model trained on stale valuations.

**Strip pairs from the shipped Godot asset on a large set.** Pair count is O(cards²): the
82-card Legacy set has 3 321 pairs (450 KB), the 300-card Hollowmere set has 44 850 (6.2 MB), and
CSC now has 82 560 — **11.5 MB against 46.8 KB stripped**. `synergyWeight` defaults to 0 so pairs
are never read at pick time. `sim_results/` is gitignored, so keep the full file there for any
future synergy experiment and commit only the stripped copy to `MtgGame/Assets/`.

**300 drafts is not enough for CSC.** `ShippedDraftModelTests` requires every card to hold at
least 100 games — the test that tells a from-scratch retrain apart from a merge, which trains old
cards deeply and new ones thinly. A 300-draft run over the 408-card set came back with a median of
352 games per card and **one card at 91** (Heroic Reinforcements), which fails the floor on its
own. 500 drafts is the size that clears it. The console cannot do "from scratch AND merge" —
answering `n` to *"draft with the existing model?"* sets the bootstrap to null, which also skips
the merge prompt and replaces — so the only way to add depth under an unchanged policy is a bigger
single run.

**Identity-scoped deck assembly starves GOLD cards of draft data, and it is structural.** A
two-colour card is legal in exactly one of the fifteen identities and a three-colour card in none,
so it only reaches a deck when the seat's chosen identity happens to be its pair. Measured on the
retrained models:

| set | kind | cards | median games |
|---|---|---|---|
| CSC | colourless | 43 | 1 138 |
| CSC | mono | 335 | 590 |
| CSC | **gold (2)** | 30 | **216** |
| LEG | colourless | 15 | 2 295 |
| LEG | mono | 66 | 1 422 |
| LEG | **gold (2)** | 3 | **33** — Geist of Saint Traft got **5** |
| LEG | **gold (3+)** | 1 | **11** — Siege Rhino, playable in NO identity |

This is the draft-side twin of the pooled-table problem `ColorIdentity.Playable` already documents
for constructed, and it arrived the moment decks stopped being five-colour piles. The old Legacy
model gave those same three cards 3 762-4 444 games — deep numbers measured in a world where every
deck played every colour, so they are not a fallback, they are differently wrong.

**Splashing does NOT repair this, measured rather than assumed.** Retraining under splash-enabled
selection moved the gold median 216 -> 223 and the minimum 127 -> 158, both inside seed noise. The
mechanism is real — a W deck splashing U can play a WU card — but only 3 of 200 decks splash, so the
extra exposure is negligible. The fix, if gold ratings ever matter, is on the TRAINING side:
allocate drafts across identities deliberately, the same remedy `ColorIdentity.Playable` defers for
the constructed pooled table.

**CSC ships anyway at ~220 median**, above the 100-game floor and roughly a third of a mono card's
evidence; its gold values are weak, not noise. **LEG was NOT re-shipped**: at 5-33 games the new
values are noise, the set is not player-facing (`DraftScene.DraftedSet` is CSC), and 10x the drafts
— four hours — would buy 330 games on three cards. Re-measure with `GoldCardSamplingDiagnostic`
before trusting any gold card's rating in either model.

**Shipping a model is three steps, and the tests enforce two of them.** Strip the pairs, copy to
`MtgGame/Assets/`, and run `ModelComparisonDiagnostic` first — it drafts the new model against the
shipped one AT THE SAME TABLE and plays the pools, which is the only thing that says the retrain
is an improvement rather than merely newer. The first CSC retrain measured **53.6% +/- 2.0 over
640 games**, and the gain came from decks now being ASSEMBLED castably rather than from any change
in how the trainer drafts: a from-scratch run uses Curve/Random drafters, so `DraftPickers`' lane
term is not involved in training at all.

**That safety rests on a RUNTIME default sitting a long way from a BUILD-TIME deletion**, so it is
pinned rather than trusted: `StrippedModelTests` asserts a stripped model drafts identically to a
full one over 50 seeds, and — because a vacuous test would pass just as well — a second test raises
`synergyWeight` and confirms the two arms genuinely do diverge. Both use inline data, so neither
depends on anyone having run a training pass.

## The human seat (Godot)

`SQGodotCommon/MtgGame/Draft/DraftScene.cs` is the caller the "human seats have no picker type" rule anticipated. It renders `Seats[0].Offer`, takes a click, and builds the pick list as `i == humanSeat ? clickedIndex : pickers[i](offer, pool)` before calling `ApplyPicks`. **No library change was needed to make drafting playable** — keep it that way.

The Godot scene loads the trained model through `DraftTrainingStore.FromJson` rather than `Load`, because `System.IO` cannot read a `res://` path inside an exported build. The model is duplicated under `SQGodotCommon/MtgGame/Assets/`; **regenerating the file in `sim_results/` does not update it** — copy it across, or the game keeps drafting against a stale model.

`DraftScene.DraftedSet` selects which set the UI drafts — currently `CoresetCube.Set`, changeable in one line. `ModelPath` is derived from `DraftTrainingStore.PathFor(DraftedSet.Code)`, so the asset filename tracks the set automatically and cannot drift from what the trainer writes.

## Draft Training

Mode 4 in the console. Runs N drafts, plays the resulting decks against each other, and records **games-in-hand** counts: a card is credited for a game only if it was actually drawn, and a pair only when both halves were drawn in that same game. Output is `sim_results/draft_training.json`.

Each `CardStat`/`PairStat` also carries `DeckGames` — how often it was in the deck at all, drawn or not. `Games / DeckGames` is P(drawn | in deck), and the picker needs it (see below). Files written before this field existed load with `DeckGames = 0` and fall back to a scale factor of 1, which silently restores the old bias — **regenerate rather than merge into such a file.**

**Counts are stored, never rates.** That lets runs be merged (`DraftTrainingStore.SaveMerged` folds into whatever is on disk) and lets the scoring formula be retuned without re-simulating. Rates are computed at pick time.

**Shrinkage is not optional.** `DraftTrainingData.Shrink(wins, games, prior, k)` pulls every rate toward the base win rate with `k = 25`. Without it a pair seen twice and won twice reads as 100% and dominates every pick it appears in — an unshrunk synergy term is worse than no synergy term. Pair data is the thin part (median ~76 games/pair after 100 drafts), so this is what makes it usable.

`DraftPickers.Trained` scores each card as `cardDelta + synergyWeight * meanPairDelta`, both in **percentage points**, then samples with a softmax. `temperature` is in those same units: 0 = argmax, ~2 = splits near-ties, high = near-random. Cards absent from the model score 0 (exactly average), so an incomplete model degrades gracefully rather than ignoring unseen cards.

## The synergy baseline

A pair is scored against `DraftTrainingData.ExpectedPairRate(rateA, rateB, prior)` — what the two cards would post together if they did not interact, combining each card's effect in **log-odds** space (the correct way to add independent effects on a win/lose outcome). Synergy is the pair's shrunk rate minus that expectation.

Measuring a pair against the *global prior* instead just re-reports card quality: pair a bomb with anything and the pair looks great, so every pair containing that bomb reads as synergy. Measured on real data, switching baselines dropped Ancestral Recall's pairs from a mean of **+12.3 to +0.6** points, and the all-pairs mean from +3.2 to +0.2 (a proper baseline centres on zero). It also surfaced real interactions the old metric buried — e.g. Carnage Tyrant + Sol Ring at +12.1, ramp into an expensive threat.

Pairs shrink toward **that same expected rate**, not the prior. This matters: shrinking toward the prior would drag a thin pair of two strong cards downward and report it as negative synergy purely for lacking data. Shrinking toward the expectation means a pair with no evidence lands on exactly 0 synergy. `pairShrinkK` defaults to 200 — roughly 10x the card `shrinkK` of 25, matching the ~10x gap in data volume.

## The draw-frequency discount

A card's win rate is conditioned on **that card being drawn**; a pair's on **both being drawn**, which is far rarer. Adding them raw compares quantities measured on different events and over-weights synergy. `DraftPickers.PairDrawRatio` measures the gap from the data — median P(pair drawn) / median P(card drawn), **0.19 / 0.44 ≈ 0.44** — and discounts synergy by it:

```
score(X) = cardDelta(X)
         + synergyWeight · pairDrawRatio · Σ over deck-bound pool Y of pairDelta(X,Y)
```

Three things here are deliberate and were each arrived at by fixing a measured regression:

- **One global scalar, not per-card factors.** Per-card P(drawn) is *endogenous*: a card that wins games faster is drawn less often (measured correlation with win rate: −0.18), so scaling by it penalises exactly the best cards.
- **Applied to synergy only, never to the card term.** Scaling both compresses the whole score range, which makes a fixed `temperature` behave far more randomly — that alone cost ~4 points of win rate (82.2% → 78.4%) and masqueraded as a modelling error.
- **Summed, not averaged**, over only the first `Draft.DefaultMaxSpells` (23) pool cards — the ones `BuildDeck` actually plays. Averaging would arbitrarily divide by pool size; including all 45 counts synergies with cards that never make the deck.

## synergyWeight defaults to 0, on evidence

The synergy term is correct and tested, but **costs win rate at every weight measured**, so it is off by default. Measured on a 16 800-game model (600 drafts, median **464 games per pair**), 288 evaluation games per weight:

| synergyWeight | 0 | 0.5 | 1 | 2 | 4 |
|---|---|---|---|---|---|
| Trained win rate | **82.2%** | 80.2% | 74.6% | 62.8% | 54.5% |

This held after 6x more data, after fixing the independence baseline, and after adding the draw-frequency discount. The metric genuinely works — it ranks Faithless Looting + Tarmogoyf, Goblin Grenade + Goblin Matron and Cranial Plating + Frogmite at the top, and Delver of Secrets + Faithless Looting (looting mills the instants Delver needs) at the bottom. Those are real mechanical interactions, found unsupervised.

The problem is aggregation, not measurement. Per-pair synergy has sd ≈ 0.34 points of which roughly 0.31 is still sampling noise at n = 464; summing ~27 of them accumulates noise as √27 while the true signal is small. The card term (sd ≈ 2.16 after the same scaling) is simply a far better-measured signal, and any weight on synergy trades it away.

Two paths if it is worth revisiting, in order of cost:
1. **Confidence-gate the sum** — only count pairs whose evidence clears a threshold, instead of summing all 27. Standard denoising, cheap to try, untested.
2. **More data** — noise falls as 1/√n, so meaningfully beating it needs ~5x again (≈3000 drafts, ~75 min). Training runs merge, so this is additive.

Do not raise the weight on intuition. The sweep is cheap and has disproved the intuition twice.

## Draw diagnostics

`DraftTrainer.Run` prints a `DrawDiagnostics` report after every training run, because "N draws"
on its own is unreadable — it does not say whether the rules produced a draw or the harness ran
out of patience, and those need opposite fixes.

The report answers four questions, in this order:

| Section | Answers |
|---|---|
| By end reason | Real draw or harness limit. Only `Damage`/`LibraryEmpty` are real. Also prints median turn and median duration per reason — a `TimeLimitReached` at turn 10 is a slow AI, at turn 95 it is a loop. |
| By position in the run | Flat means the card pool; rising means the machine degrading (GC, throttling, another process on the cores). This is what separates a game bug from a measurement artifact, and it has already killed one wrong hypothesis. |
| Final board medians | Draw games vs decided games: life, lowest library, hand, permanent count, and whether anyone decked. |
| Card lift tables | P(draw \| card drawn) and P(draw \| permanent on board at end), each against the run's base draw rate, minimum 30 games. |

`BoardSnapshot` is captured for **every** game, not just draws. A board reading at a draw means
nothing on its own — 11 permanents is only interesting next to the 7 that decided games end on.
It is deliberately not a `GameStateSnapshot`: that carries the whole event log and is written one
file per game, which cannot be held for 28 000 games.

**The most useful single column is median turn at a time-limit draw.** Measured on CSC it is
turn 9–11 after 20 seconds, with roughly double the permanents of a decided game — so the draws
are the AI thinking itself to a standstill on a wide board, not games stalling out. `GameRunner`
checks its clock *between* actions, so one slow `SelectAction` is unbounded; the simulator passes
no `moveTimeBudget`.

Read `Prior` in a saved model as a draw check before shipping it: two perspectives per game and
one winner means a draw-free run gives exactly 0.50, so `Prior = 0.34` is a run that drew 32% of
its games.

## Measured, and fixed: CSC's draws were the machine, not the cards

Before the fix, the draw rate was a function of how many games were in the parallel batch — same
code, same set, same decks, same seeds:

| Games in the batch | Draws | Median game |
|---|---|---|
| 1 120 | 0.4% | — |
| 8 400 | 2.8% | ~2 800 ms |
| 28 000 | **15.2%** | **7 560 ms** |
| 28 000, event logs dropped | 6.0% | ~4 400 ms |
| 1 120, wall-clock removed | **0%** | — |
| 28 000, wall-clock removed | **0.1%** | ~5 400 ms, flat across quarters |

Over 28 000 games, **4 263 of 4 264 draws were `TimeLimitReached` and exactly one was real**.
After the fix the same 28 000 games give 16 draws — 5 genuine turn-limit decking games kept as
data, 11 broken games excluded — and a base win rate of exactly 50.0% against 33.97% before.
Run time cost: +25%, which is the time those games were previously being cut short of.

The chain was: a game ended on wall-clock → wall-clock is dominated by AI search, which is slow
on wide boards → anything slowing the process pushed borderline games over the line → a bigger
batch retained more memory, so every game got slower. `DraftTrainer` held every `GameResult`
including its `AllEvents` log, which it never reads; dropping that alone moved 2 578 outcomes,
which is the proof that the outcomes were never about the cards.

Three changes, all of them worth understanding before touching this again:

1. **`GameRunner` no longer ends a game on the clock** (300 s safety net only). Termination is
   turn- and action-bounded, which is deterministic.
2. **`MultiTurnBeamSearchAiStrategy` bounds its own cost deterministically** via
   `DefaultRolloutBudget`, replacing wall-clock as the thing that stops a runaway search.
3. **`DraftTrainer` excludes machine-decided games** (`TimeLimitReached`, `UnhandledException`)
   from the counts instead of folding them in as draws, and prints how many it dropped.

**Never read a training run's draw count as a signal about the cards without checking the end
reason first.** For a year's worth of runs it measured the machine.

`MtgSimulator.Tests/MachineIndependenceTests.cs` pins this: the same game is played twice, the
second time against CPU contention, and the results must be identical.

**The load must come from dedicated below-normal-priority threads, never from `Task.Run`.**
Written with Tasks the test failed every time, and the fix was not at fault: the beam search
parallelises over the thread pool, so busy Tasks starve it of workers instead of merely slowing
it, and one move stretched past the 300-second safety net — the game ended at turn 1 having taken
a single action. It also made the test take 7 minutes instead of 3 seconds. Contend for cores, do
not take the workers.

A failure now means a wall-clock reading has crept back into a decision. The one honest exception
is the safety net firing, which the test asserts against separately and reports as proving
nothing either way.

Phases in `DraftTrainer.Run` are ordered deliberately: drafts run **sequentially** (cheap, must stay deterministic), all games across all drafts run in **one parallel batch** into a pre-allocated array, then results are folded in **sequentially** so the output never depends on thread scheduling.

## Generational (on-policy) training

Mode 4 asks for a generation count. Above 1 it loops: train → save → evaluate → retrain with the model it just produced as the drafters. The motivation is that card rates measured in Curve/Random decks describe how good a card is *in a badly-drafted deck*; a payoff card can look mediocre there and be strong in a deck that supports it.

- **Generations overwrite by default.** The model should describe the *current* drafting policy, so accumulating would blend old and new policies and dilute exactly the effect being created. The merge prompt only appears for a single-generation run.
- **Every seat at a training table must use a picker of the same strength.** No model → all Curve/Random (equally weak). A model → all Trained (equally strong). See below for why; `DraftTrainer.BuildPickers` carries the same warning.
- **Evaluation uses fixed seats (9) and fixed seeds (111/222/333) every generation**, so the only thing varying across the trend is the model. `DraftRunner.Run(verbose: false)` returns wins/games per picker name for exactly this.

Watch for a confound when reading a trend: if generation 1 bootstraps from a model trained on far more drafts, its first overwrite drops the data volume, so an early dip may be a sample-size effect rather than a policy effect. Compare generations against each other at equal drafts-per-generation, not against the seed model.

## Measured result: on-policy retraining neither helps nor hurts

Run correctly (all seats Trained, overwrite, 10 generations × 150 drafts, bootstrapped from a 600-draft model), the trend is flat:

| Gen | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Trained win % | 84.7 | 84.7 | 85.4 | 79.9 | 82.6 | 81.9 | 86.1 | 80.6 | 82.6 | 81.9 | 81.9 |
| CardSpread | 4.98 | 3.62 | 4.40 | 4.15 | 4.37 | 4.18 | 4.35 | 3.99 | 3.99 | 3.83 | 4.45 |

Mean of generations 1–10 is 82.8% against a gen-0 baseline of 84.7%, with a per-generation SE of ±3.3pp at 144 games — i.e. no movement. The mechanism was healthy this time, which is what makes the null result trustworthy:

- **CardSpread stayed flat at ~4pp** (contaminated run: exploded to 14.93pp) — no strength bleed, no diversity collapse.
- **corr(starting rate, change) = −0.451** (contaminated run: +0.630). Negative is regression to the mean, the signature of honest re-measurement rather than amplification.
- **Spearman rank correlation gen0 vs gen10 = 0.874**, top-15 overlap 13/15 — the ranking is stable.

Conclusion: the card ranking converges after a single training pass, and self-play on this card pool has nothing further to teach it. Generational training is wired up and correct, but is not a lever worth spending compute on unless the card pool or the game rules change materially.

## Measured failure: mixed-strength seats poison the data

Mixing a strong picker with weak ones at the same table was tried, on the reasoning that exploration seats preserve deck diversity. **It is actively harmful and must not be reintroduced.**

A card the model likes gets taken by the Trained seats, so it lands in decks that win ~84%; a card it dislikes lands in Curve/Random decks that win ~34%. The card's measured win rate then encodes *which picker drafted it*, not how good it is. Feeding that back compounds it every generation. Over 10 generations × 150 drafts:

| Symptom | Measurement |
|---|---|
| corr(starting rate, change in rate) | **+0.63** — the loop amplified existing preferences |
| Top-20 cards by starting rate | 59.7% → 67.5% (**+7.8pp**) |
| Bottom-20 cards | 46.9% → 37.3% (**−9.5pp**) |
| Card win-rate range | 44–67% → **31–79%** |
| Spread (sd) | 4.98pp → **14.93pp** |
| Actual play strength | 84.7% → **~80%** (slightly worse) |

Sampling was *not* the problem — every card still got 1,701–2,485 deck-games, so this is not starvation or collapse. It is pure drafter-identity contamination.

The opposite failure mode is real too: with all seats on one model, decks can converge until every card appears in winners and losers equally and rates decay toward the base rate. Softmax `temperature` is what prevents it. **Diagnose either failure with the spread of card win rates across generations** — it should stay roughly flat. Exploding means strength contamination; collapsing means diversity died.

**Training is memory-bound, not CPU-bound.** Measured on 16 cores: sequential 25.3s, DOP 4 16.7s, DOP 16 20.0s — extra threads past ~4 add GC contention and make it *slower*. Server GC (enabled in `MtgSimulator.Console.csproj`) flattens this to ~14.1s at any DOP. Do not add a degree-of-parallelism knob; there is nothing to tune. Reference: 2800 games ≈ 4 minutes.

Measured results on the 600-draft model (16 800 games, 33 600 deck-games): Trained **82.2%** vs Curve ~30% vs Random ~35% over 288 evaluation games. Note that `Curve` scores *below* `Random` — its stats-per-mana metric systematically over-drafts big vanilla creatures (Craw Wurm, Mahamoti Djinn rank lowest in the learned data) over efficient spells and card advantage.

Caveat: the model is trained and evaluated on the same card pool. It learns card quality within that pool; it does not generalise to new cards.


## The tournament pod, the pickers, and the seat count

`DraftTournament` runs the pod afterwards: circle-method pairings (seat 0 fixed, the rest rotate) give `seats - 1` rounds where every seat plays every other exactly once. The human's game is played in the UI and reported via `RecordHumanResult`; the other pairings run through `DraftRunner.PlayGame` on a background task started *before* the human leaves for their match, so the tables resolve in parallel with them playing. `SimulateRoundAsync` deliberately touches no tournament state — results come back and are folded in by `CompletePendingRoundAsync` on the caller's thread, which is why there is no lock anywhere in the class.

One known asymmetry, marked `ponytail:` in the source: `MtgGameManager` hardcodes the human as Player 1 and passes them first to `BeginGame`, so **the human is always on the play**. `DraftRunner` alternates across its two games per pair; at one game per pair there is nothing to alternate. Fixing it means threading a "plays second" flag through `MtgGameManager`.

`DraftPickers.Curve` scores stats-per-mana with a nudge away from a top-heavy curve. It sees only `ManaCost`, P/T, and creature-or-not, because that is all `Card` carries — there is no rarity and no color. Upgrade path is the GIH win rates in `sim_results/precon_*.csv` (`PreconstructedStats.CardGihRow`).

`DraftRunner` cycles pickers across seats and alternates who is on the play within each pairing, so its report measures picker quality rather than seat order. With no training file that is Curve vs Random (2 pickers); with one, Trained vs Curve vs Random (3). **Use a seat count divisible by the picker count** or the per-picker rates are not comparable — the runner warns when it isn't.
