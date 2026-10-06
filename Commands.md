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

## Run a Godot scene — USE THE SCRIPT

```
./Run-Godot.ps1                                              # the main scene (the menu)
./Run-Godot.ps1 MtgGame/Draft/DraftScene.tscn                # any scene, directly
./Run-Godot.ps1 MtgGame/Draft/DraftScene.tscn -Capture shots/draft -Seconds 4
./Run-Godot.ps1 MtgGame/Draft/DraftScene.tscn -Headless -Seconds 4   # log-only check
```

**`Run-Godot.ps1` exists because three things go wrong every time and none are memorable.**
`godot` on PATH is the NON-.NET build and fails every C# script load with errors that look like a
broken project — the script always uses `godot-mono`. It opens on the SECOND monitor (detected,
not hardcoded) instead of over your work. And it creates the capture folder: `--write-movie` with a
missing folder exits 0, prints "Done recording movie", and writes nothing.

**Captures land inside the Godot project, so they would ship in the APK.** The script drops a
`.gdignore` in every capture folder it makes (the KIN branch shipped 122 MB of frames before this).

**Any scene opens directly** — `GameManager` is an autoload, so logging and the input map exist
whichever scene boots, and it logs `Initial scene: <name>`. A scene that needs an earlier screen's
data still fails honestly: `MtgGameScene` throws "Service DeckSetupData has not been registered"
unless entered from deck select.

Traps, each paid for once on the KIN branch:
- **Build the Godot project before a capture** — `dotnet build SQGodotCommon/SQGodotCommon.csproj`.
  Godot run from the command line uses the LAST BUILT assemblies; a change that has not been built
  looks exactly like a change that does not work.
- **Run from the repo root** — a cwd left elsewhere makes `./Run-Godot.ps1` "not recognized".
- **Take a LATE frame.** Layout and card tweens have not settled on frame 0.
- **`--headless` cannot render**, and prints shader-compiler errors (`shader_compiler.cpp`,
  `custom_samplers`) with stack traces when the card scene loads. That is the dummy renderer, not a
  broken scene. Judge visuals with `-Capture`, never headless.
- **New assets do not exist until imported.** The game launched with `--path` never imports; until
  `godot-mono --path SQGodotCommon --headless --import` runs, `ResourceLoader.Exists` is false and
  nothing errors — the art is just missing.
- **`--resolution` does not resize the window** while `project.godot` has
  `window/size/window_*_override` (it does: 1280x720). Check a capture's real pixel size.
- **A capture cannot hover or click**, and a still frame proves nothing about motion — nothing moves
  until state changes. KIN added debug flags (`--hover-card=N`, `--mouse=x,y`, real clicks through
  the viewport) for this; MtgGame has none yet. Add one when a screen needs it, and send REAL input
  events through the viewport rather than calling the handler, or the capture "verifies" something
  no player can do.

To find the frames where something animates, diff against a settled one instead of guessing timing:

```python
from PIL import Image, ImageChops
import glob
fs = sorted(glob.glob('SQGodotCommon/shots/x/*.png'))
base = Image.open(fs[10]).convert('RGB')
for f in fs[11:]:
    d = ImageChops.difference(Image.open(f).convert('RGB'), base).convert('L')
    print(sum(i * c for i, c in enumerate(d.histogram())) / 1e6, f)
```

## Android build (phone)

```
./Build-Apk.ps1                                          # MTG -> build/mtg.apk. USE THIS
cd build; python -m http.server 8000 --bind 127.0.0.1    # serve it...
cloudflared tunnel --url http://localhost:8000 --no-autoupdate   # ...on a public HTTPS URL
```

**`Build-Apk.ps1` exists because the raw export ships stale and broken APKs while exiting 0.** It
writes the two SDK paths into Godot's editor settings, deletes the ExportRelease output so the C#
is always recompiled, exports, then refuses the result if the APK has too few assemblies or any
source file is newer than the assembly that was built. `-Preset` picks another game's preset.

The tunnel URL is unguessable, changes every restart, and serves whatever is in `build/` — rebuild
and the same URL serves the new APK. **Chat/file transfer caps at 30 MiB**, so the APK can never be
sent that way. Arm64 only, signed with a local debug keystore; Play Protect warns on a sideload.

**Presets live in `SQGodotCommon/export_presets.cfg`, which is gitignored** — so a fresh clone has
none, and every branch checked out in this directory SHARES one file. This branch's is
`Android MTG`; KIN's is `Android` (it excludes `MtgGame/*`). Add a preset per game; never edit
another's. One-time machine setup (scoop `temurin17-jdk`, Android SDK cmdline-tools +
`build-tools;36.0.0` + `platforms;android-36`, Godot 4.6.3 mono export templates in
`~/scoop/persist/godot-mono/editor_data/export_templates/`, a debug keystore, `cloudflared`) was
done on the KIN branch and is already on this machine.

**The Godot dependency chain is pinned to `net9.0`** — `SQGodotCommon`, `MtgCore`, `MtgSimulator`,
`ImmutableGameObjects` (a comment in each csproj says so). The Android template refuses anything
newer. Raising one breaks the PHONE build only — desktop and tests keep working, so it fails where
nobody is looking. Test and console projects stay on net10; they can reference net9 libraries.

**Card art is imported LOSSY (`compress/mode=1`).** Lossless, 31 MB of JPG imported to 145 MB and
shipped at that size; lossy it is 22 MB, no visible difference at card size. New art imports
lossless by default — set the mode in its `.import` file, or the APK silently grows.

**Godot 4.6 reads the Java and Android SDK paths from EDITOR SETTINGS ONLY**, not `JAVA_HOME` /
`ANDROID_HOME`, and they have come back empty on their own (a headless run that saves settings is
enough). Symptom: "A valid Android SDK path is required in Editor Settings" with the SDK plainly
installed, and no command-line flag for it. The script rewrites both before every export.

**FIVE ways this export fails quietly** (all found on the KIN branch):

- **The export SKIPS the C# build when its output is newer than your source — and an edit saved
  while an export RUNS lands inside exactly that window.** Every later export then sees a newer
  `.dll` and skips again: exit 0, full assembly count, correct size, shipping pre-edit code for as
  many rebuilds as you run. `dotnet build` says nothing, since only ExportRelease is stale. **Never
  edit sources while an export runs.** The script clears
  `SQGodotCommon/.godot/mono/temp/{bin,obj}/ExportRelease` every time for this reason.
- **No ETC2/ASTC, no export — and no message.** `rendering/textures/vram_compression/import_etc2_astc=true`
  is required; without it the only output is "configuration errors:" followed by the UNRELATED
  "C#/.NET is experimental" line.
- **A missing solution path gives a SUCCESSFUL APK with zero C# in it.** The `.sln` is one level up,
  so `dotnet/project/solution_directory=".."` is required. Without it: one stack trace mid-log, exit
  0, a game that launches to nothing. The script checks the assembly count.
- **`NETSDK1152`, duplicate publish outputs** — `ErrorOnDuplicatePublishOutputFiles=false` in
  `SQGodotCommon.csproj`.
- **Everything imported ships**, at IMPORTED size, not on-disk size. Capture frames were 122 MB of
  KIN's APK until each `shots*/` folder got a `.gdignore` (`Run-Godot.ps1` now adds one). Check the
  breakdown before blaming the engine for the size.

**An APK of identical size is NOT evidence the build did not change** — zip alignment absorbed a
512-byte growth in the game assembly twice. To prove an edit shipped, extract
`SQGodotCommon.dll` from the APK and search it for a string you just added (UTF-16LE: that is how
.NET stores string literals).

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
printf '4\n\n\n300\n8\n1\n2\nn\ncscfinal\n' | dotnet run --project MtgSimulator.Console -c Release
```

Fields: mode, AI depth (blank=3), format (blank=Booster), drafts, seats, generations, **set
choice** (index printed by the menu — read it, don't hardcode; `2` = CSC since HLM retired, it
was `3`), train-from-scratch, seed. The
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
printf '4\n\n\n300\n8\n1\n2\nn\ncscfinal\n' | dotnet run --project MtgSimulator.Console -c Release
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
printf '6\n\n2\n16\n30\n0\n3\n6\n20\n\n\n300\nY\n0\n0\n\n<seed>\n' | dotnet run --project MtgSimulator.Console -c Release
```

17 fields, 19 with an engine file — verified 2026-10-06; the per-field table and defaults are in
`RunningSimulations.md`. Set `2` is CSC (**read the menu, do not hardcode it** — it has shifted
twice). Count the prompts in the output rather than trusting any written list —
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
