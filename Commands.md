# Useful Commands

Reference doc, not instructions — see CLAUDE.md files for the "why". Run from repo root unless noted.

## Build & test

```
dotnet build
dotnet test
dotnet test MtgCore.Tests
dotnet test MtgSimulator.Tests
dotnet test SQGodotCommon.Tests
```

## Play in console

```
dotnet run --project MtgConsole
```

## Inspect what the AI is doing

In the Godot game: **Space** pauses the AI, **F6** opens the inspector, **F7** saves the position.
Pause before opening the inspector or the AI moves on while you are reading it.

F7 writes to `user://scenarios/` (the toast prints the absolute path); the console reads
`scenarios/` relative to the shell's cwd, so copy it across — same trap as the draft model asset:

```
cp "<path from the toast>" scenarios/
dotnet run --project MtgSimulator.Console -c Release      # mode 5
```

Mode 5 loads a scenario and has several strategies decide in the same position, printing each
one's chosen action and the evaluator terms it moved. Non-interactively:

```
printf '5\n1\n\n' | dotnet run --project MtgSimulator.Console -c Release
```

Fields: mode, which scenario (blank = first), which strategies (blank = all).

## Art pass (download card art)

```
dotnet run --project MtgArtScraper -- SQGodotCommon/MtgGame/Assets/Card_Art HLM
```

Pulls art crops from Scryfall by card name, skips files that already exist. Cards Scryfall
doesn't recognize (your own designs) land in `_needs_art_<code>.txt` beside the output folder.

## Train a draft model

```
printf '4\n\n\n300\n8\n1\n3\nn\ncscfinal\n' | dotnet run --project MtgSimulator.Console -c Release
```

Fields: mode, AI depth (blank=3), format (blank=Booster), drafts, seats, generations, **set
choice** (index printed by the menu — read it, don't hardcode), train-from-scratch, seed. The
`n` is load-bearing — answering the default merges into the old model instead of replacing it.
Writes `sim_results/draft_training_<code>.json`. 300 drafts ≈ 25 min.

**Must run from repo root** — `sim_results/` is relative to the shell's cwd, not the project's.

Godot loads its own copy, not `sim_results/` directly — copy the file across after training:

```
cp sim_results/draft_training_csc.json SQGodotCommon/MtgGame/Assets/draft_training_csc.json
```

Check the new model against the old prior before shipping it:

```
python -c "import json;d=json.load(open('sim_results/draft_training_csc.json'));print(d['Prior'],d['Perspectives'])"
```

## Card win rates as a spreadsheet

Training writes `sim_results/draft_training_<code>.csv` automatically beside the JSON — no command
to run. One row per card, best first:

```
Name,ManaCost,Types,Games,Wins,WinRate,ShrunkWinRate,DeltaPP,DeckGames,DrawRate
```

**Sort on `DeltaPP`, not `WinRate`.** That is the shrunk rate's distance from the run's base rate,
in percentage points, and it is the number the picker actually drafts on. A raw rate off 30 games
swings on noise, and an absolute rate means nothing without the base rate (printed in the `#`
header lines at the top, along with the sample size).

`DrawRate` is `Games / DeckGames` — how often the card was drawn when it was in the deck. A card
far below ~0.44 is ending games early rather than being unlucky.

**Cards near the bottom are as likely to be broken as weak** — a card that does nothing scores
about the same as one that is merely bad. See DesignNotes.md, "The low win-rate band is a bug
detector".

## Inspect a trained model

```
node inspect-draft-training.js sim_results/draft_training_csc.json
```


## Training and evolution runs

### Train a draft model (mode 4)

Mode 4 is interactive, but the console reads plain `Console.ReadLine()`, so it drives fine from
stdin — no CLI-argument path was added because piping needs no shipped code:

```
printf '4\n\n\n300\n8\n1\n3\nn\ncscfinal\n' | dotnet run --project MtgSimulator.Console -c Release
```

Fields in order: mode, AI depth (blank = 3), format (blank = Booster), drafts, seats, generations,
**set choice** (the index printed by `ReadSet`, which changes as sets are registered — read the
menu, do not hardcode it), **train-from-scratch**, seed.

**The `n` is load-bearing and this command was missing it.** Once a model file exists, the trainer
asks "Draft with the existing model? (Y/n — n trains from scratch)" and then "Merge into it rather
than replace? (Y/n)", and an exhausted stdin answers **Y to both**. Adding a colour and running
the old command therefore merged the new cards into the previous colour's model instead of
retraining — the exact bootstrapping failure the section above warns about, arrived at by
following the documented command. Answering `n` skips the merge prompt entirely and replaces the
file, so the field count differs between the two paths; count the prompts in the output, do not
assume.

The final `Console.ReadKey` throws `InvalidOperationException` when stdin is redirected. It fires
*after* the model is written, so the file is safe; ignore it.

**Verify the console's `bin/` timestamps before trusting a training run.** `dotnet build
MtgSimulator.Console.csproj -c Release` can report success while leaving a stale copy of
`MtgCore.dll` / `MtgSimulator.dll` in `MtgSimulator.Console/bin/Release/net10.0/`, and
`dotnet run --no-build` then measures code that is not in the binary. This cost two full training
runs and three wrong conclusions in one session: a fix was declared ineffective twice when it had
simply never been compiled in. The tell is maddening — unit tests pass (the test projects rebuild
correctly) while the training run disagrees, which reads exactly like a real bug in the fix.

It is the same class as the `sim_results/` trap below, one level down: the thing you are measuring
is not the thing you changed.

```
rm -rf MtgSimulator.Console/bin MtgSimulator.Console/obj MtgSimulator/bin MtgSimulator/obj        MtgCore/bin MtgCore/obj
dotnet build MtgSimulator.Console/MtgSimulator.Console.csproj -c Release --no-incremental
ls -la MtgSimulator.Console/bin/Release/net10.0/MtgCore.dll   # must be newer than your edit
```

**When a training run contradicts a passing unit test, suspect the binary before the diagnosis.**

**`sim_results/` is relative to the SHELL's working directory, not the project's.** `dotnet run`
does not chdir into the project, so running from the repo root writes `./sim_results/` while
running from inside `MtgSimulator.Console/` writes `MtgSimulator.Console/sim_results/`. Two
directories with the same filename in them is how a freshly trained model gets silently
overwritten by a stale one — which happened, and the only symptom was every card's learned value
being byte-identical after a retrain that had clearly produced different summary numbers.

Always run from the repo root, and check `Prior` against the run's reported base win rate before
shipping a model:

```
python -c "import json;d=json.load(open('sim_results/draft_training_csc.json'));print(d['Prior'],d['Perspectives'])"
```

Reference rate, measured: 5 drafts = 140 games = 28s, so ~5.6 games/sec. 300 drafts ≈ 8 400 games
≈ 25 minutes.

### Evolve a constructed metagame (mode 6)

```
printf '6\n\n3\n8\n30\n3\n6\n20\nY\n<seed>\n' | dotnet run --project MtgSimulator.Console -c Release
```

Fields in order: mode, AI depth (blank = 2), set (the index printed by `ReadSet` — **read the
menu, do not hardcode it**), decks, generations, mutants, games/matchup, final games/matchup,
seed-from-draft-model, seed. Count the prompts in the output rather than trusting that list —
mode 4's documented command was wrong for exactly this reason.

Reference cost: 8 decks x 30 generations x 3 mutants x 6 games = ~1 300 games/generation (a
little under 1 344, because some mutation proposals return null and are not scheduled).

**Measured at ~1 000 games/minute on the default configuration**, i.e. ~1.3 minutes per
generation and **~40 minutes for a 30-generation run**. Constructed games are much faster than
drafted ones — the reference rate elsewhere in this document is 5.6 games/sec for draft training,
and these are ~17/sec, because 60-card decks with a real curve end sooner than 40-card limited
decks and nothing here pays for the draft itself.

Both output files are relative to the **shell's** working directory — run from the repo root, and
verify `bin/` timestamps before trusting a run. Same two traps as draft training.
