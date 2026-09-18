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

## DOOMJAM

```
dotnet test DoomCore.Tests                       # the rules engine
dotnet run --project DoomConsole -c Debug 42     # play it in a terminal, seed 42
godot-mono --path SQGodotCommon DoomGame/doom_board.tscn    # the battle screen
```

**`godot` on PATH cannot run C# at all** — it is the standard build. Use `godot-mono`.

**Any scene can be opened directly.** `GameManager` is an autoload, so logging and the input map
are initialised no matter which scene you start. It logs `Initial scene: <name>` at boot, which is
the quickest way to confirm what actually loaded.

**Screenshot the running game** — no code needed, and it works from a remote session:

```
godot-mono --path SQGodotCommon --write-movie shots/doom.png --fixed-fps 10 --quit-after 14   DoomGame/doom_board.tscn
```

Writes a numbered PNG per frame (and a stray .wav). Take a LATE frame: the container layout and the
card tweens have not settled on frame 0. Needs a real renderer, so it opens a window briefly —
`--headless` cannot render at all.

**New art must be IMPORTED before the game can see it.** Godot only imports assets when the editor
runs, and the game launched with `--path` does not do it — so a new `.svg` silently does not appear
and `ResourceLoader.Exists` returns false with no error anywhere:

```
godot-mono --path SQGodotCommon --headless --import
```

Run it after adding or renaming anything in `SQGodotCommon/DoomGame/Art/`. It writes the `.import`
files beside each asset; if those are missing, that is the symptom.

**Pin the window to one monitor** so a capture cannot land on top of other work — and never drive it
with synthetic clicks (see `HANDOFF-DoomPacingAndRewards.md` §4):

```
godot-mono --path SQGodotCommon --position 1920,0 --resolution 1600x900   --write-movie shots/doom.png --fixed-fps 10 --quit-after 40   DoomGame/doom_board.tscn
```

`--position` takes virtual-desktop coordinates, so `1920,0` is the second monitor on a side-by-side
pair.

**`--autostart` is gone from these commands and from the code (2026-09-18).** It existed because
`--write-movie` cannot click a button: the board used to open on a theme picker, so every capture
without the flag was a picture of that menu. The picker was deleted and the board opens on floor 1
by itself, so there is nothing left for the flag to skip. `--reward`, `--shop` and `--autoturn` are
unaffected and still do real work.

`shots/` must exist first or Godot writes nothing and only complains about the `.wav`.

**Add `--autoturn` to see ANIMATION.** It ends a turn every 1.6s through the real engine:

```
... --fixed-fps 20 --quit-after 140 DoomGame/doom_board.tscn -- --autoturn
```

**A still board proves nothing about motion** — nothing moves until state changes, so a capture of a
fresh battle is always a settled screen. This is what caught `Hand2D` drawing every card in from
global x=0, which had been true for three sessions and had never once been seen.

To find the interesting frames, diff them against a settled one rather than guessing at the timing:

```python
from PIL import Image, ImageChops
import glob
fs = sorted(glob.glob('shots_anim/*.png'))
base = Image.open(fs[10]).convert('RGB')
for f in fs[25:130]:
    d = ImageChops.difference(Image.open(f).convert('RGB'), base).convert('L')
    print(sum(i * c for i, c in enumerate(d.histogram())) / 1e6, f)
```

**`--reward` opens the reward screen**, which is otherwise reachable only by winning a floor:

```
... --quit-after 30 DoomGame/doom_board.tscn -- --reward
```

It marks the Companion with every apocalypse in the library, three times each, so the panel is shown
its worst case — a name that long is what pushed the intermission off the screen on floor 19.

**The shop, and the card grid inside it:**

```
... --quit-after 22 DoomGame/doom_board.tscn -- --shop
... --quit-after 22 DoomGame/doom_board.tscn -- --shop --remove
```

`--shop` forces 400 gold and doubles the whole reward pool into the deck, so both screens are drawn
at the worst case they have to handle. `--remove` opens the removal grid, which is the layout that
actually breaks: it sizes itself to a deck that can be forty-six cards, and the first version ran off
the top AND bottom of the screen, drew over the panel, and buried the BACK button behind the cards.
**A grid that fits twelve cards tells you nothing about one that has to fit forty-six.**

**The card preview needs no battle at all**, and is the right loop for card work:

```
godot-mono --path SQGodotCommon --position 1920,0 --resolution 1600x900   --write-movie shots_cards/cards.png --fixed-fps 10 --quit-after 25   DoomGame/doom_card_preview.tscn
```

It loads the cards that BREAK the layout — widest statline, longest name, a Rite with no stat badge
— at hand scale and at hover scale. Three card bugs were found in it that the board had never shown.

Headless prints shader-compiler errors about `custom_samplers` when the card scene loads. That is
the dummy renderer failing to compile the card outline shader, not a broken scene — the run still
exits 0. Ignore them headless; judge the cards on a real renderer.

Headless works for checking the battle drives correctly:

```
godot-mono --headless --quit-after 60 --path SQGodotCommon DoomGame/doom_board.tscn
```

## Measure the balance (the bot)

```
dotnet run --project DoomConsole -c Release -- sim 1000          # 1000 runs, seeds 1-1000
dotnet run --project DoomConsole -c Release -- sim 200 Life=4    # override any eval weight
```

**Release, always**, and the console project sets `ServerGarbageCollection`. The sim is ALLOCATION
bound, not CPU bound — the engine rebuilds a GameState per action and the bot simulates a whole turn
per candidate line — so workstation GC serialises every worker on one heap. Measured: the same
300-run workload went 693s -> 224s on server GC, with byte-identical output.

**A sim gets slower as the game gets better**, because a run that completes a 20-floor act is five
times the work of one that dies on floor 3. Budget by `runs x mean floor`, not by runs.

Writes every run to `doom_sim_results/sim-<timestamp>.json` (gitignored) and prints four tables:
survival by floor, pressure, apocalypses, card value. **Quote the file, never the memory of a run**
— the tables go stale the moment content changes.

`sim N <Weight>=<value> ...` overrides anything on `DoomEvalWeights` by name, and the override is
stamped into the results file's version string. Sweeping a weight is how you check the bot is near
its own ceiling: if a weight change moves the survival curve a lot, the curve is measuring the bot
and not the game. See `docs/findings/doom-balance.md`.

**n matters more than you think.** The card-value table splits ~1000 runs into "took it" and "did
not" groups of ~160, and at 200 runs the deltas are indistinguishable from noise.

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
