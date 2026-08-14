# MtgSimulator

Class library containing all AI strategies, game runners, deck factories, and reporting for MTG simulation. Referenced by Godot and by `MtgSimulator.Console` (the runnable console entry point). Keeping it a library prevents file-locking conflicts when the console app and Godot are running simultaneously.

## Source Map

| File | Purpose |
|------|---------|
| `SimulatorRunner.cs` | Orchestrates N games, aggregates results, prints all reports |
| `GameRunner.cs` | Runs a single game to completion using two `IAiStrategy` implementations |
| `IAiStrategy.cs` | Interface: `SelectAction` + `ResolveChoice` — all AI implementations conform to this |
| `RandomAiStrategy.cs` | Baseline AI — picks a random legal action; used as playout policy |
| `DepthLimitedAiStrategy.cs` | Greedy depth-limited DFS AI — retained for comparison; not the default |
| `BeamSearchAiStrategy.cs` | Beam search AI — the default strategy; two-bucket pruning (concrete + potential) |
| `IPotentialEvaluator.cs` | Interface for secondary "potential" signals used during beam pruning |
| `FastManaPotentialEvaluator.cs` | Potential evaluator that scores by current available mana (preserves fast-mana lines) |
| `StateEvaluator.cs` | Scores a `GameState` from a given player's perspective (float) |
| `CardPool.cs` | Defines the full card pool; `BuildRandomDeck` samples 40 random cards per game |
| `Decks/ZooDeckFactory.cs` | Builds a fixed 60-card Zoo deck (RGW aggro — 24 Plains + 36 spells) for a given player |
| `Decks/GoblinsDeckFactory.cs` | Builds a fixed 60-card Goblins deck (red aggro tribal — 24 Plains + 36 spells) for a given player |
| `Decks/ValakutDeckFactory.cs` | Builds a fixed 60-card Valakut ramp deck (14 Plains + 4 Valakut + 4 Glimmervoid + 4 Field of the Dead + 2 Simic Growth Chamber + ramp/spells) for a given player |
| `Decks/JundDeckFactory.cs` | Builds a 60-card Jund midrange deck (18 Plains + 2 Mox + 2 Sol Ring + hand disruption, removal, threats, Siege Rhino) for a given player |
| `Decks/DelverDeckFactory.cs` | Builds a fixed 60-card Delver deck for a given player |
| `Decks/DragonstormDeckFactory.cs` | Builds a fixed 60-card Dragonstorm combo deck for a given player |
| `Decks/ReanimatorDeckFactory.cs` | Builds a fixed 60-card Reanimator deck for a given player |
| `Decks/TraditionalStormDeckFactory.cs` | Builds a fixed 60-card Traditional Storm combo deck for a given player |
| `Decks/AffinityDeckFactory.cs` | Builds a fixed 60-card Affinity artifact aggro deck for a given player |
| `Decks/DeckRegistry.cs` | Registers all named precon decks (`DeckInfo` records); exposes `All` and `Build(name, ownerId)` |
| `Draft/Draft.cs` | `DraftFormat` / `DraftSeat` / `DraftState` records + `Create`, `ApplyPicks`, `RunToCompletion`, `BuildDeck` |
| `Draft/DraftPickers.cs` | `DraftPicker` delegate; `Random` (baseline) and `Curve` (stats-per-mana heuristic) pickers |
| `Draft/DraftRunner.cs` | All-AI draft harness: drafts a table, builds decks, round-robins via `GameRunner`, prints win rates per seat and per picker; `PlayGame` is the shared single-game helper |
| `Draft/DraftTournament.cs` | Round-robin standings for a drafted pod with one human seat — circle-method pairings, background AI round simulation, `Standing` rows |
| `Draft/DraftGameSetup.cs` | Builds a pre-begin `GameState` from two drafted pools; shared by `DraftRunner` and `DraftTrainer` |
| `Draft/DraftTrainingData.cs` | `CardStat` / `PairStat` / `DraftTrainingData` count DTOs, `Shrink`, `Merge`; `DraftTrainingStore` load/save/merge JSON and `PathFor(setCode)` |
| `Draft/DraftTrainer.cs` | Training mode: N drafts, parallel games, accumulates games-in-hand counts per card and per card pair |
| `PreconstructedStats.cs` | Aggregates precon game results into four stat tables; exposes row records for deck (inc. AvgWinTurn/MinWinTurn/MaxWinTurn), matchup, card GIH WR (inc. AvgCopiesPlayed), and card-per-matchup GIH WR (inc. AvgCopiesPlayed) |
| `PreconstructedSimulatorRunner.cs` | Round-robin precon runner: builds schedule, runs games via `GameRunner`, feeds `PreconstructedStats`, prints console summary, triggers CSV export |
| `PreconstructedCsvExporter.cs` | Writes all four stat tables to `sim_results/precon_<timestamp>.csv`; escapes card names with commas (e.g. "Krenko, Mob Boss") |
| `GameResult.cs` | Record capturing outcome, turn count, actions, drawn/played cards, end reason, duration, all events, exception info |
| `PreconstructedGameResult.cs` | Wraps `GameResult` with deck names and on-play metadata for preconstructed mode |
| `GameStateSnapshot.cs` | Human-readable snapshot DTO — `GameStateSnapshot`, `PlayerSnapshot`, `CreatureSnapshot`, `TurnLog` |
| `FlaggedGameSaver.cs` | Builds a snapshot from a flagged `GameState` and writes it as JSON to `flagged_games/` |

## Architecture

`SimulatorRunner` creates a game via `SetupGame()` (calls `MtgGameFactory.Create()`, builds random decks from `CardPool`), injects two `IAiStrategy` instances into a `GameRunner`, and collects `GameResult`s. All reporting runs after all games complete.

`GameRunner` owns the full game lifecycle: it calls `BeginGame` at the start of `Run()`, captures the begin-game events (initial hand draws, first turn start) into `AllEvents` and `DrawnCards`, then drives the game loop. This ensures no events are lost to the caller. The loop checks for pending choices first (delegates to `activeStrategy.ResolveChoice`), then calls `MtgActionGenerator.GetLegalActions` and `activeStrategy.SelectAction`. When no legal actions remain, it auto-fires `EndTurnAction`.

## Game Limits (in `GameRunner`)

| Limit | Threshold | Effect |
|-------|-----------|--------|
| Time limit | 5 000 ms wall-clock | `GameEndReason.TimeLimitReached` — game ends as draw |
| Turn limit | 100 turns | `GameEndReason.TurnLimitReached` — game ends as draw |
| Action warning | 50 actions in one turn | `HadActionWarning = true` — game continues |
| Action limit | 100 actions in one turn | `GameEndReason.ActionLimitReached` — game ends as draw |
| Unhandled exception | any thrown exception | `GameEndReason.UnhandledException` — game ends as draw, exception captured |

`GameRunner.Run()` wraps the entire game loop in a try/catch. On exception, it terminates with `UnhandledException`, capturing the last known `GameState`, all events up to the crash, and the exception message and stack trace — then returns normally so the run continues with the next game.

`GameRunner.Run()` returns `(GameResult Result, GameState FinalState)` — the final state is passed to `FlaggedGameSaver` by both runners when the result is flagged.

Flagged games (any of the above) are collected separately and printed in the flagged games report. Both `SimulatorRunner` and `PreconstructedSimulatorRunner` track and report flagged games.

## AI Strategies

**`RandomAiStrategy`** — picks a uniformly random legal action. Baseline; also used as the playout policy inside more sophisticated strategies.

**`DepthLimitedAiStrategy`** — greedy depth-first search to a fixed depth. Not minimax — opponent responses during search are not modelled. Retained for comparison; not the current default.

**`BeamSearchAiStrategy`** — beam search (breadth-first) to a fixed depth. At each level, candidates are pruned to two buckets before expanding the next level:
- **Concrete bucket** — top N by `StateEvaluator` score (default: 10)
- **Potential bucket** — top M per `IPotentialEvaluator` (default: 5 slots via `FastManaPotentialEvaluator`)

Potential evaluators preserve setup lines (fast mana, etc.) that score poorly on the main evaluator but may enable a win condition deeper in the tree. The final action is always chosen by concrete score at the leaf level. Falls back to `EndTurnAction` as a tiebreaker (to avoid neutral attacks or pointless spells), then random among remaining ties. Current default: depth 3, concreteSlots 10, with `FastManaPotentialEvaluator` injected by default.

**Land-first override**: before entering beam search, `SelectAction` plays any available `PlayLandAction` immediately. Permanent mana is the highest-priority resource; no search is needed for this decision.

**`IPotentialEvaluator`** — pluggable interface for secondary beam-pruning signals. Implement to add new potential heuristics (graveyard value, storm count, etc.) without touching the search logic.

**`MultiTurnBeamSearchAiStrategy`** — beam search (same pruning as above) combined with a multi-turn greedy rollout. Each candidate is scored by `ScoreAfterCompletingTurn`, which completes the current turn greedily then runs `MultiTurnGreedyRollout` for N lookahead turns (default 2). The rollout alternates between `PlayGreedyTurn` (our turn) and `SimulateOpponentTurn` (opponent turn). Default opponent mode is `BoardOnly`. Default: depth 3, concreteSlots 10, lookaheadTurns 2.

**Move time budget**: the optional `moveTimeBudget` constructor param caps wall-clock time per `SelectAction`/`ResolveChoice`. When set, the search degrades gracefully once spent — level 0 scores remaining roots with the cheap immediate evaluator instead of a rollout, beam expansion stops, and the best node found so far is returned. **Default is null (unbounded)**, which preserves deterministic simulator behavior; only the interactive Godot path passes a finite budget (the simulator relies on `GameRunner`'s outer limits instead). `ResolveAllChoices` also carries a generous iteration cap (`MaxChoiceResolutionIterations`) so a non-advancing choice can't spin the calling thread forever.

**`OpponentSimulationMode`** — controls how the opponent's turn is simulated in the rollout:
- `PassTurn` — opponent does nothing.
- `Greedy` — opponent plays the single best non-EndTurn action (includes hand cards; exposes hidden information).
- `Random` — opponent plays one random non-EndTurn action.
- `BoardOnly` — opponent iterates all legal `AttackAction` and `ActivateAbilityAction` options greedily in a loop until none remain, then ends turn. No hand cards, so simulation uses only visible information. This is the default. The loop is essential: stopping after a single attack would undercount the opponent's total damage (e.g. missing that two attackers together deal lethal).

**`IAiStrategy`** — swap implementations freely; `GameRunner` and `SimulatorRunner` only depend on the interface.

## StateEvaluator

Scores a non-terminal state as a weighted sum. Terminal states short-circuit.

| Factor | Weight |
|--------|--------|
| Life difference (player − opponent) | 0.2 |
| Creature count difference | 3.0 |
| Total effective Power difference (permanent power only) | 2.0 |
| Creature damage difference (opponent damage − player damage) | 0.1 |
| Non-creature permanent count difference (Mox, Arena, Exploration, etc.) | 1.5 |
| Cards in hand difference | 1.4 |
| Player's own permanent mana (`MaxMana` only — temporary fast mana excluded) | 2.0 |
| Win (opponent has lost) | +10000 |
| Loss (player has lost) | −10000 |

`MaxMana` weight is high (2.0) because in the land system permanent mana is the primary resource — a land behind means fewer spells castable every turn for the rest of the game. Power uses permanent power only (`GetEffectivePermanentPower`); `UntilEndOfTurn` buffs like Giant Growth are excluded since they evaporate next turn. Creature damage (weight 0.1) tracks accumulated damage on surviving creatures — a creature with near-lethal damage is far more fragile than a fresh one, and without this factor the evaluator sees a neutral attack (both creatures survive) as free. Non-creature permanents (weight 1.5) are identified by `PermanentComponent && !CreatureComponent`; land cards are excluded automatically since `Plains` carries no `PermanentComponent`.

Zone IDs are read directly from `MtgGameIds` to avoid child-list scans on every evaluation call.

When tuning weights: changes here affect `BeamSearchAiStrategy` and `DepthLimitedAiStrategy`. `RandomAiStrategy` ignores evaluation.

## CardPool

Defines all cards available for random deck generation. `BuildRandomDeck(ownerId, deckSize = 40, landCount = 13)` builds a 40-card limited deck: 13 Plains + 27 non-land cards sampled without replacement from the pool. Land cards in the pool are excluded from the non-land draw.

**Targeting restriction**: damage spells target opponents and opponent creatures only. The random AI has no targeting intelligence, so restricting targets at the card level prevents self-damage. This is intentional — do not add friendly targets to simulator cards without also updating the AI strategy.

**EventTriggerCondition migration**: `CardPool` contains duplicate cards — one set using concrete condition classes (`CreatureDiesCondition`, `CreatureAttacksCondition`, etc.) and one set using the generic `EventTriggerCondition` system (Grim Watcher, Soul Harvester, War Drummer, Battlefield Scholar). Both run side by side for validation. Once the `EventTriggerCondition` versions are confirmed correct, the old concrete-condition versions should be removed.

## Draft

Deck *selection* as a game mode, for human and AI players alike. Lives in `Draft/`, namespace `MtgSimulator` (flat, like `Decks/`).

**Not a `GameState`.** `DraftState` is a plain immutable record graph. Drafting needs none of the action stack, pipelines, choice resolution, or event log, and the cards it holds are owner-agnostic templates that never enter a `GameState`. Do not migrate this to `GameAction`s.

**Two formats, one model.** `DraftFormat.Booster` (15-card packs, pick one, pass, 3 packs) and `DraftFormat.Digital` (offered N cards, pick one, repeat — seats never interact). `Draft.Create` pre-generates every pack and every offer up front into each seat's `Queue`, so the only per-format branch in `ApplyPicks` is *rotate the remainder* vs *open your own next group*. Booster pass direction alternates per pack via `DraftState.Round`.

### Card sets

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

**Strip pairs from the shipped Godot asset on a large set.** Pair count is O(cards²): the
82-card Legacy set has 3 321 pairs (450 KB), the 300-card Hollowmere set has 44 850 (6.2 MB).
Since `synergyWeight` defaults to 0 the pairs are never read at pick time — verified by running
the same evaluation against the full and pairs-free files and getting seat-for-seat identical
win rates. `sim_results/` is gitignored, so keep the full file there for any future synergy
experiment and commit only the stripped copy to `MtgGame/Assets/` (6.2 MB → 63 KB).

### Measured: Hollowmere (HLM), 300 cards

600 drafts, 16 800 games, 33 600 deck-games, ~112 deck-games per card (the Legacy model has
410/card off the same run size — pair and card density both fall as the pool grows). Evaluated
over 72 games at 9 seats:

| Picker | Win rate |
|---|---|
| Trained | **75.0%** |
| Curve | 37.5% |
| Random | 37.5% |

Curve does not underperform Random here as it does on the Legacy pool, because Hollowmere's
expensive cards are genuinely castable via the reanimation package rather than being traps.

Key rules:
- **Picks are indices into `Seat.Offer`, never `Card` values.** `Card` is a record, so two copies of one template in a pack compare equal and picking by value would remove the wrong card.
- **Packs exclude lands** — `Draft.BuildDeck` supplies the mana base by padding to `deckSize` with Plains. It stamps `OwnerId`/`ControllerId`, so it must be called **per game**, not once per seat.
- **`DraftPicker` is a delegate**, not an interface. The no-delegates serialization rule does not apply because draft state never enters a `GameState` — same reasoning as `DeckInfo.Builder`.
- **Human seats have no picker type.** The caller drives the loop and supplies that seat's index; `RunToCompletion` is for all-AI drafts only. This keeps all presentation (console, Godot) out of the library — a UI renders `Seats[i].Offer` / `.Pool` and needs no library change.
- **Determinism**: `Draft.Create(format, pool, seed, …)` consumes one `Random(seed)` in fixed seat order and fixes the entire draft. `ApplyPicks` and `RunToCompletion` are pure. Only `DraftPickers.Random` holds RNG; `DraftRunner` seeds it as `seed + 100 + seatIndex`, and games as `seed + 1000 + gameIndex * 5` (mirroring `SimulatorRunner`).

### The human seat (Godot)

`SQGodotCommon/MtgGame/Draft/DraftScene.cs` is the caller the "human seats have no picker type" rule anticipated. It renders `Seats[0].Offer`, takes a click, and builds the pick list as `i == humanSeat ? clickedIndex : pickers[i](offer, pool)` before calling `ApplyPicks`. **No library change was needed to make drafting playable** — keep it that way.

The Godot scene loads the trained model through `DraftTrainingStore.FromJson` rather than `Load`, because `System.IO` cannot read a `res://` path inside an exported build. The model is duplicated at `SQGodotCommon/MtgGame/Assets/draft_training.json`; **regenerating `sim_results/draft_training.json` does not update it** — copy it across, or the game keeps drafting against a stale model.

`DraftTournament` runs the pod afterwards: circle-method pairings (seat 0 fixed, the rest rotate) give `seats - 1` rounds where every seat plays every other exactly once. The human's game is played in the UI and reported via `RecordHumanResult`; the other pairings run through `DraftRunner.PlayGame` on a background task started *before* the human leaves for their match, so the tables resolve in parallel with them playing. `SimulateRoundAsync` deliberately touches no tournament state — results come back and are folded in by `CompletePendingRoundAsync` on the caller's thread, which is why there is no lock anywhere in the class.

One known asymmetry, marked `ponytail:` in the source: `MtgGameManager` hardcodes the human as Player 1 and passes them first to `BeginGame`, so **the human is always on the play**. `DraftRunner` alternates across its two games per pair; at one game per pair there is nothing to alternate. Fixing it means threading a "plays second" flag through `MtgGameManager`.

`DraftPickers.Curve` scores stats-per-mana with a nudge away from a top-heavy curve. It sees only `ManaCost`, P/T, and creature-or-not, because that is all `Card` carries — there is no rarity and no color. Upgrade path is the GIH win rates in `sim_results/precon_*.csv` (`PreconstructedStats.CardGihRow`).

`DraftRunner` cycles pickers across seats and alternates who is on the play within each pairing, so its report measures picker quality rather than seat order. With no training file that is Curve vs Random (2 pickers); with one, Trained vs Curve vs Random (3). **Use a seat count divisible by the picker count** or the per-picker rates are not comparable — the runner warns when it isn't.

## Draft Training

Mode 4 in the console. Runs N drafts, plays the resulting decks against each other, and records **games-in-hand** counts: a card is credited for a game only if it was actually drawn, and a pair only when both halves were drawn in that same game. Output is `sim_results/draft_training.json`.

Each `CardStat`/`PairStat` also carries `DeckGames` — how often it was in the deck at all, drawn or not. `Games / DeckGames` is P(drawn | in deck), and the picker needs it (see below). Files written before this field existed load with `DeckGames = 0` and fall back to a scale factor of 1, which silently restores the old bias — **regenerate rather than merge into such a file.**

**Counts are stored, never rates.** That lets runs be merged (`DraftTrainingStore.SaveMerged` folds into whatever is on disk) and lets the scoring formula be retuned without re-simulating. Rates are computed at pick time.

**Shrinkage is not optional.** `DraftTrainingData.Shrink(wins, games, prior, k)` pulls every rate toward the base win rate with `k = 25`. Without it a pair seen twice and won twice reads as 100% and dominates every pick it appears in — an unshrunk synergy term is worse than no synergy term. Pair data is the thin part (median ~76 games/pair after 100 drafts), so this is what makes it usable.

`DraftPickers.Trained` scores each card as `cardDelta + synergyWeight * meanPairDelta`, both in **percentage points**, then samples with a softmax. `temperature` is in those same units: 0 = argmax, ~2 = splits near-ties, high = near-random. Cards absent from the model score 0 (exactly average), so an incomplete model degrades gracefully rather than ignoring unseen cards.

### The synergy baseline

A pair is scored against `DraftTrainingData.ExpectedPairRate(rateA, rateB, prior)` — what the two cards would post together if they did not interact, combining each card's effect in **log-odds** space (the correct way to add independent effects on a win/lose outcome). Synergy is the pair's shrunk rate minus that expectation.

Measuring a pair against the *global prior* instead just re-reports card quality: pair a bomb with anything and the pair looks great, so every pair containing that bomb reads as synergy. Measured on real data, switching baselines dropped Ancestral Recall's pairs from a mean of **+12.3 to +0.6** points, and the all-pairs mean from +3.2 to +0.2 (a proper baseline centres on zero). It also surfaced real interactions the old metric buried — e.g. Carnage Tyrant + Sol Ring at +12.1, ramp into an expensive threat.

Pairs shrink toward **that same expected rate**, not the prior. This matters: shrinking toward the prior would drag a thin pair of two strong cards downward and report it as negative synergy purely for lacking data. Shrinking toward the expectation means a pair with no evidence lands on exactly 0 synergy. `pairShrinkK` defaults to 200 — roughly 10x the card `shrinkK` of 25, matching the ~10x gap in data volume.

### The draw-frequency discount

A card's win rate is conditioned on **that card being drawn**; a pair's on **both being drawn**, which is far rarer. Adding them raw compares quantities measured on different events and over-weights synergy. `DraftPickers.PairDrawRatio` measures the gap from the data — median P(pair drawn) / median P(card drawn), **0.19 / 0.44 ≈ 0.44** — and discounts synergy by it:

```
score(X) = cardDelta(X)
         + synergyWeight · pairDrawRatio · Σ over deck-bound pool Y of pairDelta(X,Y)
```

Three things here are deliberate and were each arrived at by fixing a measured regression:

- **One global scalar, not per-card factors.** Per-card P(drawn) is *endogenous*: a card that wins games faster is drawn less often (measured correlation with win rate: −0.18), so scaling by it penalises exactly the best cards.
- **Applied to synergy only, never to the card term.** Scaling both compresses the whole score range, which makes a fixed `temperature` behave far more randomly — that alone cost ~4 points of win rate (82.2% → 78.4%) and masqueraded as a modelling error.
- **Summed, not averaged**, over only the first `Draft.DefaultMaxSpells` (27) pool cards — the ones `BuildDeck` actually plays. Averaging would arbitrarily divide by pool size; including all 45 counts synergies with cards that never make the deck.

### synergyWeight defaults to 0, on evidence

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

Phases in `DraftTrainer.Run` are ordered deliberately: drafts run **sequentially** (cheap, must stay deterministic), all games across all drafts run in **one parallel batch** into a pre-allocated array, then results are folded in **sequentially** so the output never depends on thread scheduling.

### Generational (on-policy) training

Mode 4 asks for a generation count. Above 1 it loops: train → save → evaluate → retrain with the model it just produced as the drafters. The motivation is that card rates measured in Curve/Random decks describe how good a card is *in a badly-drafted deck*; a payoff card can look mediocre there and be strong in a deck that supports it.

- **Generations overwrite by default.** The model should describe the *current* drafting policy, so accumulating would blend old and new policies and dilute exactly the effect being created. The merge prompt only appears for a single-generation run.
- **Every seat at a training table must use a picker of the same strength.** No model → all Curve/Random (equally weak). A model → all Trained (equally strong). See below for why; `DraftTrainer.BuildPickers` carries the same warning.
- **Evaluation uses fixed seats (9) and fixed seeds (111/222/333) every generation**, so the only thing varying across the trend is the model. `DraftRunner.Run(verbose: false)` returns wins/games per picker name for exactly this.

Watch for a confound when reading a trend: if generation 1 bootstraps from a model trained on far more drafts, its first overwrite drops the data volume, so an early dip may be a sample-size effect rather than a policy effect. Compare generations against each other at equal drafts-per-generation, not against the seed model.

#### Measured result: on-policy retraining neither helps nor hurts

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

#### Measured failure: mixed-strength seats poison the data

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

## Reports

`SimulatorRunner` prints four sections after all games complete:

1. **Aggregate report** — total games, P1/P2 wins, draws, avg turns, avg actions, end reason breakdown (including time limit), action warnings.
2. **Timing report** — total time, avg/min/max ms per game, games per second. Duration comes from `GameResult.GameDurationMs` (measured inside `GameRunner`).
3. **Card win rate when drawn** — per-card P1 draw count + win%, P2 draw count + win%, combined win rate. Sorted descending by combined win rate.
4. **Flagged games** — game number, winner, turn count, total actions, wall-clock time, which limits were hit, and `[saved]` if a snapshot was written. Lists the save directory and cap status.

## Flagged Game Snapshots

When a game is flagged, both runners call `FlaggedGameSaver.TrySave()` immediately after the game ends. Snapshots are written to `flagged_games/` (next to the binary) as indented JSON. Naming: `game_{number}_{reason}_{timestamp_ms}.json`. Saves are capped at `FlaggedGameSaver.MaxSaves` (25) per run — if a run produces more flagged games the first 25 are sufficient to diagnose the cause.

Each snapshot includes:
- Final board state: per-player life, mana, hand card names, library count, graveyard, battlefield creatures (with P/T, damage, sickness, attacked flags)
- Stack contents at termination
- Turn-by-turn event log (`TurnLogs`): one `TurnLog` per half-turn, each containing human-readable event strings (e.g. "Player 1 cast Lightning Bolt", "Goblin attacked", "Player 2 took 3 damage")
- Exception message and stack trace when `EndReason` is `UnhandledException`

`FlaggedGameSaver.TrySave` no longer takes `MtgGameIds` — it resolves all IDs it needs directly from `GameState` via `GetWellKnownId`.

## Known Issues / Tech Debt

- **`IAiStrategy` is in the `MtgCore` namespace** despite its file living in `MtgSimulator/`. Should be moved to the `MtgSimulator` namespace for correctness.
- **EventTriggerCondition migration** — see CardPool section above.

## Console Entry Point

`MtgSimulator.Console/` is the runnable project — it contains only `Program.cs` and references this library. Run that project to launch the simulator interactively. Four modes: 1 = random card pool, 2 = preconstructed decks, 3 = draft, 4 = train draft pickers. `ReadSeed()` is shared by modes 1, 3 and 4 (blank = random, number = literal, word = FNV-1a hashed). `ReadSet()` is shared by modes 3 and 4 and skips its prompt entirely while only one set is registered. Mode 3 auto-loads the selected set's model via `DraftTrainingStore.PathFor` if present and adds the `Trained` picker to the comparison. `ServerGarbageCollection` is enabled here — see the Draft Training section for why. `sim_results/` and `flagged_games/` output folders are written relative to the console app's working directory.

## Key Rules

- Never duplicate legal action generation — always call `MtgActionGenerator.GetLegalActions`. Do not reimplement this in simulator code.
- Never call `BeginGame` from setup code — `GameRunner.Run` is responsible for calling it. Pass a pre-begin `GameState` to `Run`; it calls `BeginGame` internally and captures all resulting events.
- `SimulatorRunner.SetupGame()` uses `MtgGameFactory.Create()` (real mana — players start at 0/0), not `CreateForTesting()`. The simulator tests real land-based mana constraints.
- All state mutation goes through `GameAction`s — no direct state modification in the simulator.
