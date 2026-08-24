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
