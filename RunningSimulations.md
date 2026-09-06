# Running the Simulations

How to drive `MtgSimulator.Console` yourself: what each mode does, how to script it, where the
results land, and what to read before trusting a run.

Every command and field list here was **verified by running it** on 2026-09-05, not copied from
documentation. See "Known documentation drift" at the bottom.

---

## Quick start

```bash
cd c:/Users/shayn/Desktop/GameDev/SQGodotCommon
dotnet build MtgSimulator.Console/MtgSimulator.Console.csproj -c Release
dotnet run --project MtgSimulator.Console -c Release
```

## Two rules that cost real runs

**1. Always run from the repo root.** `sim_results/` and `flagged_games/` are created relative to
the *shell's* working directory, not the project's. Run from inside `MtgSimulator.Console/` and you
get a second `sim_results/` there — two directories with the same filenames in them is how a freshly
trained model gets silently overwritten by a stale one.

**2. Build before every run.** `dotnet run --no-build` will happily measure a stale `MtgCore.dll`.
This cost two full training runs and three wrong conclusions in one session: a fix was declared
ineffective twice when it had simply never been compiled in. The tell is maddening — unit tests pass
(test projects rebuild correctly) while the run disagrees.

> *When a run contradicts a passing unit test, suspect the binary before the diagnosis.*

Nuclear option when in doubt:

```bash
rm -rf MtgSimulator.Console/bin MtgSimulator.Console/obj \
       MtgSimulator/bin MtgSimulator/obj MtgCore/bin MtgCore/obj
dotnet build MtgSimulator.Console/MtgSimulator.Console.csproj -c Release --no-incremental
ls -la MtgSimulator.Console/bin/Release/net10.0/MtgCore.dll   # must be newer than your edit
```

---

## The modes

| Mode | What it does | Use it for |
|---|---|---|
| 1 | Random card pool | Engine smoke test |
| 2 | Preconstructed decks | Round-robin of the hand-built decks |
| 3 | Draft | Compares pickers (Trained / Curve / Random) |
| 4 | Train draft pickers | **Produces the draft model** |
| 5 | Inspect a saved scenario | Several strategies decide in one position, side by side |
| 6 | Evolve a constructed metagame | **The main deckbuilding run** |
| 7 | Discover synergy engines | **"Does this archetype assemble?"** — solitaire, no battles |

The intended order for deckbuilding is **7 → 6**. Mode 7 finds which engines a pool supports; its
output file then seeds mode 6 as pool-locked archetype slots. Asking mode 6 for archetypes cold is
what makes it converge on midrange piles.

## The set menu — read it, never hardcode it

```
1 = Legacy (LEG, 90)            4 = Combo Proving Ground (CMB, 21)
2 = Hollowmere (HLM, 308)       5 = Designed Sets (DES, 732)
3 = Core Set Cube (CSC, 408)    6 = All Sets (ALL, 809)
```

Modes 6 and 7 get options 5 and 6 (the unions); modes 3 and 4 do not. **CMB was inserted mid-list**,
so any old piped command written against the previous numbering now runs a different set silently.

- **DES** — every designed set (HLM + CSC + CMB), Legacy excluded. The default choice for mode 6/7:
  CMB's planted combos live inside it, and it has a gauntlet.
- **ALL** — includes Legacy, whose ad-hoc broken cards (Ancestral Recall +11.86, Steppe Lynx +14.32)
  make a run answer "which deck abuses the broken cards" before anything about synergy.
- **CMB alone** measures the fixture, not the builder — at ~21 cards the breadth gate switches off
  entirely. It is a supplement to play *inside* DES.

---

## Driving it from the command line

The console is plain `Console.ReadLine()`, so piping works. **Count the prompts in the output rather
than trusting any written field list** — the field count changes with your answers.

The final `Console.ReadKey` throws `InvalidOperationException` when stdin is redirected. It fires
*after* all files are written, so the output is safe. Ignore it.

### Mode 6 — evolve a metagame

17 fields with no engine file, **19 with one** (an engine file adds two prompts).

```bash
printf '6\n\n3\n8\n30\n0\n3\n6\n20\n\n300\nY\nY\n0\n0\n\nmyseed\n' \
  | dotnet run --project MtgSimulator.Console -c Release
```

| # | Prompt | Default |
|---|---|---|
| 1 | Mode | — |
| 2 | AI depth | blank = **2** |
| 3 | Which set | 1 |
| 4 | How many decks | 8 |
| 5 | How many generations | 30 |
| 6 | …how many are exploration generations | 0 = off |
| 7 | Mutants per deck per generation | 3 |
| 8 | Games per matchup while evolving | 6 |
| 9 | Games per matchup in the final round-robin | 20 |
| 10 | Minimum deck difference | 0.35 |
| 11 | Pre-simulation decks | 300, 0 = skip |
| 12 | Cull non-viable decks (Y/n) | Y |
| 13 | Seed from the draft model (Y/n) | Y |
| 14 | Synergy (concept) deck slots | 0 = off |
| 15 | Gauntlet games per reference deck | 0 = off |
| 16 | Engine file from mode 7 | blank = none |
| 17 | *(only if 16 non-blank)* How many slots are engines | blank = all but a wildcard |
| 18 | *(only if 16 non-blank)* Engines to exclude | blank |
| 19 | Benchmark seed | blank = random |

**Exploration generations are a subset of the total**, not extra. 20 exploration + 10 optimization
means answering `30` then `20`.

### Mode 7 — discover engines

6 fields.

```bash
printf '7\n\n5\n10\n30\nmyseed\n' | dotnet run --project MtgSimulator.Console -c Release
```

mode · AI depth *(blank=2)* · set · solitaire games per engine · engines to highlight · seed

The highlight count is **display only** — every viable concept is probed either way. Set it to at
least `engine slots × 3`, because that is the pool mode 6 samples from (see the worked example).

### Mode 4 — train draft pickers

```bash
printf '4\n\n\n300\n8\n1\n3\nn\ncscfinal\n' | dotnet run --project MtgSimulator.Console -c Release
```

mode · AI depth · format *(blank = Booster)* · drafts · seats · generations · set · **train-from-scratch** · seed

**The `n` is load-bearing.** Once a model file exists the trainer asks two separate questions:

1. *"Draft with the existing model? (Y/n — n trains from scratch)"* — governs which cards get
   drafted, and therefore which get **measured at all**.
2. *"Merge into it rather than replace? (Y/n)"* — governs only the output file.

**Exhausted stdin answers Y to both.** Conflating these is a trap: answering "replace" alone still
bootstraps the drafters, silently producing a model trained on stale valuations. Answering `n` to
the first skips the merge prompt entirely, so **the field count differs between the two paths**.

Train from scratch whenever cards were added (new cards score at the prior, get passed over every
pick, and never accumulate data — a self-reinforcing blind spot) **or the rules changed underneath
the model**.

### Other modes

- **Mode 3 (draft):** mode · depth · format · seats · set · *(synergy weight, only if a model exists)* · seed
- **Mode 1 (random pool):** mode · depth · games · seed
- **Mode 2 (precon):** mode · depth · N games per side per matchup — no seed prompt
- **Mode 5 (scenario):** returns before the AI-depth prompt; drives its own menu

---

## Where the results go

All under `sim_results/` at the repo root. **Gitignored**, along with `flagged_games/`.

| File | From | Contents |
|---|---|---|
| `metagame_<set>_<stamp>.json` | 6 | Final decklists + full matchup matrix |
| `mutations_<set>_<stamp>.csv` | 6 | **Every proposal the search tried**, not just survivors |
| `constructed_values_<set>_presim.json` | 6 | Card values — *the table everything reads* |
| `constructed_values_<set>_evolved.json` | 6 | **Diagnostics only** — never read back as a value |
| `engines_<set>_<stamp>.json` | 7 | Discovered engines; the file you feed into mode 6 |
| `draft_training_<set>.json` | 4 | The draft model (LEG keeps the unsuffixed name) |
| `card_values_<set>.json` | sandbox | Value-once-castable table |
| `precon_<stamp>.csv` | 2 | Four stat tables |

`flagged_games/` holds JSON snapshots of games that hit a limit or threw — final board, stack,
turn-by-turn event log, and the stack trace for exceptions. Capped at 25 per run.

### The two values files are NOT interchangeable

`constructed_values_*_presim.json` is measured from uniformly-random decks and is the table
everything reads. `constructed_values_*_evolved.json` is `P(win | card in deck)` inside evolved
decks, which conflates the card with the quality of the decks that played it. Merging them was a
feedback loop with a ratchet: Goblin Chieftain read +13.43 against the draft table's +3.81, while
Storm's payoff sank to −6.62 and could then never be seeded again. **Never read the evolved table
back as a card value.**

### The game ships separate copies

The models the Godot game drafts against live in `SQGodotCommon/MtgGame/Assets/` and are **not**
updated by regenerating `sim_results/`. Copy across manually, or the game keeps using a stale model.
Strip pairs from the shipped copy on a large set — CSC is 11.5 MB full against 46.8 KB stripped, and
`synergyWeight` defaults to 0 so pairs are never read at pick time.

---

## Reading a run

Three things to check before trusting any result:

**1. The excluded count.** Nonzero means the run is **not reproducible** — a game was dropped by the
wall-clock safety net, which is the one non-deterministic termination in the system. Two runs at an
identical seed with byte-identical inputs once differed by one dropped game, which changed the card
values, which changed the softmax draws at seeding, which produced a completely different metagame
by generation 1. Two full A/B comparisons were read before anyone noticed.

> *If two runs at one seed disagree, read the excluded count before reading anything else.*

**2. The seeding-prior line.** The run self-reports, e.g. `Seeding prior: NONE (quality-blind
seeding)`. Answering `Y` to the draft-prior prompt when no model file exists **degrades silently**
to quality-blind. The log is the only place that says which arm you actually got.

**3. The "Constructed vs limited" Spearman**, in the run's own output:

| Reading | Means |
|---|---|
| ≈ 1.0, no movers | **Failing** — constructed data is not displacing the limited prior |
| ~0.5–0.8, coherent mover list | Working — the format has its own valuations |
| ≈ 0 early | Noise, not signal — check games/card first |

Sanity-check the mover list by eye: 4-of-dependent and narrow-but-powerful cards rising, big vanilla
creatures and slow card advantage falling.

**For mode 7, read cohesion and assembly — never win rate.** There are no battles; a half-built
combo deck loses every game, so a win rate cannot answer that question, and asking it anyway is what
makes mode 6 converge on midrange piles.

**Never read a model comparison without an equal-data control.** Comparing a new model against an
old one once showed Spearman 0.596 and looked like a large reranking; the same comparison with the
feature disabled gave 0.599. The movement was entirely sample size — five times the real effect.

---

## Worked example: 10 engine decks + 4 curve decks, 20 exploration + 10 optimization

14 decks, 30 generations of which 20 are exploration. Specifying an explicit engine-slot count
**turns the wildcard slot off automatically**, which is the desired behaviour here — on a large pool
the wildcard never survived anyway (13 of 48 culls, final rate 17.1%).

### Step 0 — set the land floor, for both steps

```powershell
$env:MTG_MIN_LANDS = "12"     # PowerShell
```
```bash
export MTG_MIN_LANDS=12       # Bash
```

`Decklist.MinLands` defaults to 20 and **every hand-built deck that beats an evolved field runs
fewer** — Storm 12, Zoo 14, Affinity 14, Goblins 16. At 20 those decks are not hard to reach, they
are outside the search space, and evolved decks finish pinned at exactly 20, which is what a binding
constraint looks like. Two rules here break the "real mana bases" analogy: no colours (a land is
quantity, never fixing) and every opening hand contains three lands by rule.

### Step 1 — discover the engines

```bash
dotnet build MtgSimulator.Console/MtgSimulator.Console.csproj -c Release
printf '7\n\n5\n10\n30\nmyseed\n' | dotnet run --project MtgSimulator.Console -c Release
```

Highlight is 30 rather than the default 8 on purpose: mode 6 samples from the top
`slots × 3` = **top 30** distinct archetypes, so this prints exactly the pool it will draw from.

Writes `sim_results/engines_des_<stamp>.json` and reads it straight back to verify the round-trip.
Copy the exact filename.

### Step 2 — the evolution run

```bash
printf '6\n\n5\n14\n30\n20\n3\n6\n20\n\n300\nn\nY\n0\n4\nsim_results/engines_des_<stamp>.json\n10\n\nmyseed\n' \
  | dotnet run --project MtgSimulator.Console -c Release
```

| Prompt | Answer | Why |
|---|---|---|
| Which set | `5` | DES |
| How many decks | `14` | 10 engine + 4 curve |
| Generations | `30` | Total |
| …exploration | `20` | Playset-sized moves, no culling while it runs |
| Mutants / games / final | `3` `6` `20` | Defaults |
| Min difference | blank | 0.35 |
| Pre-simulation decks | `300` | **Do not skip** if no values table exists yet |
| Cull | `n` | See below |
| Seed from draft model | `Y` | |
| Concept slots | `0` | Engine slots do this job; don't run both |
| Gauntlet games | `4` | See below |
| Engine file | from step 1 | Unlocks the next two prompts |
| Engine slots | `10` | The other 4 become Aggro / Midrange / Control |
| Exclusions | blank | First run |

**Why cull `n`:** engine slots are never culled regardless — mode 7 already judged them on whether
they *assemble*, and a win-rate floor would delete exactly the decks the engine file exists to keep.
So culling only touches the 4 curve decks, and it is measured harmful: culling resets that slot's
`DeckHistory`, so the deck restarts not just bad but **blind**. Over 100 generations the two slots
culled once reached age 78/95 and finished best; the slots culled 10 and 13 times never recovered.

**Why gauntlet `4`:** a closed round-robin averages exactly 50% by construction, so a field that
converges on something mediocre reports itself perfectly healthy. Measured: the evolved ALL field
lost to hand-built Zoo 34–66, and the DES field measured 32.7% against its references. DES supplies
3 reference decks (Twin, Elves, Reanimator). The gauntlet is deliberately **not** part of the
diversity constraint — converging onto a gauntlet deck means the field found the good deck.

### Check these in the first minute

1. **`Engines: N of M from sim_results/...`** — if N < 10 there is also an explicit WARNING, and the
   shortfall silently becomes extra curve decks (7 engines + 7 curve instead of 10 + 4). Causes are
   the exclusion list and the 80% overlap distinctness guard. If short, re-run step 1 on ALL.
2. **`Seeding prior:`** — see "Reading a run" above.

### Cost

Per-generation cost scales with `decks × (decks − 1)`, so 14 decks is a large jump from 8:

```
per generation = decks × (1 + mutants) × ((decks − 1) × gamesPerMatchup + gauntletDecks × gauntletGames)
               = 14 × 4 × (13 × 6 + 3 × 4) = 5,040 games

× 30 generations = 151,200 games   ≈ 2.5 hours at the measured ~1,000 games/min
+ pre-simulation 300 decks         ≈ 10 minutes
+ final round-robin (91 × 20)      = 1,820 games
```

Budget **~2.5–3 hours**. For a shorter first pass cut `games per matchup` to 4 rather than cutting
decks or generations. Dropping the gauntlet saves ~15% and costs the only absolute reference.

Reference rates: mode 6 ~1,000 games/min (~17/sec). Mode 4 draft training ~5.6 games/sec, so 300
drafts ≈ 8,400 games ≈ 25 minutes. Mode 7 on DES is unmeasured — watch it rather than estimating.

---

## Environment variables

| Variable | Effect |
|---|---|
| `MTG_MIN_LANDS` | Deck legality floor (default 20). **Set to 12** for modes 6 and 7 |
| `MTG_MAX_LANDS` | Upper bound (default 26) |
| `MTG_CARD_VALUES=off` | Disables card values in `ResolveChoice` — the A/B control arm |
| `MTG_SET` | Target set for the `[Explicit]` diagnostic tests (default ALL) |
| `MTG_FIELD` | Saved metagame field for `ArchetypeChallenge` |
| `MTG_SELF_ACTIONS` | `OutputProbe` actions per turn (default 1) |
| `MTG_MAX_SUPPLIERS` | `CausalSupplyTests` cycle dump bound (default 60) |

---

## Watching the AI instead of arguing about it

Evaluation changes were argued rather than watched for two sessions, and several were wrong. Use the
tooling before proposing a scoring change.

| | |
|---|---|
| **Space** in game | Pause the AI — do this *before* F6 so you can click through candidates |
| **F6** in game | AI inspector: every ranked action, and the chosen one's score split into evaluator terms |
| **F7** in game | Save the live position to `user://scenarios/` |
| Console **mode 5** | Load a scenario, have several strategies decide in it side by side |

F7 writes to Godot's `user://scenarios/`. **Copy it into `scenarios/` at the repo root** for console
mode 5 — the console reads that path relative to the shell's working directory.

A scenario is **serialized state**, not a `GameStateSnapshot` report.

---

## Known documentation drift

`MtgSimulator/CLAUDE.md` (§"Running it") carries a mode 6 pipe recipe with **10 fields**. It
predates the exploration-generations, min-difference, pre-simulation, cull, concept-slots, gauntlet
and engine-file prompts, and will mis-answer 7 of them if pasted. The 17/19-field version in this
document is the verified one.

The general rule the project keeps relearning: **count the prompts in the output**, never trust a
written field list — including this one, after the next prompt is added.
