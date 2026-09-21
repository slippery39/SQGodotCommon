# Handoff — the card design pass, a doom that finally fires, and a build that stops lying

**Read this, then `KinJam.md` "Card design pass", then `KinV3Plan.md`.**
`HANDOFF-KinAndroidAndText.md` is the previous session and is superseded — read only its §3 scars,
which all still hold, and note that its §4 (the Flood finding) is now FIXED and described here.

State at handoff: **159 tests green**, Godot builds, KinConsole builds, the Android APK builds and
was verified running on a real phone. **Six commits, `f6b29b0` to `2a889dc`, working tree clean.**
Nothing pushed.

---

## 1. The headline

**A card design pass exists as a plan, and its first cluster is built.** `KinJam.md` gained a
"Card design pass" section: six clusters of cards, the enemies that punish each, relics reopened
under a constraint, and the build order. Cluster 1 — sacrifice — is in the game.

**Flood does something.** It had shipped since v1 and had done literally nothing since combat v3
landed, in three scenarios. It takes the CARD now, for two turns.

**The Android export was shipping stale APKs while exiting 0**, and had been for at least one build.
`Build-Apk.ps1` makes that failure impossible rather than documented.

---

## 2. What exists now

**Cluster 1 — sacrifice** (`89210dc`):

- **`DestroyAction`** — the first thing that can make a unit die on demand. It marks the target and
  calls `EndTurnAction.ClearTheDead` rather than writing a second account of dying, and clears
  INLINE so no corpse sits in a lane mid-turn. The companion is never destroyed.
- **`CountOf.DiedThisTurn`** — five lines. `DiedThisTurnRunCardIds` already existed for the dooms
  and had no reader. `DiedLastTurn` pays for what the enemy took; this pays for what you spent, in
  the turn you spend it, which is what makes a sacrifice a combo instead of a setup.
- **A rite played into a LANE is a targeted card.** `PlayCardAction` always carried a lane and only
  units validated it; it now reaches the effect, so `UnitInSourceLane`, `EnemyInSourceLane` and both
  adjacency rules work from a rite. **No targeting UI, no prompt — the drop is the choice.**
- **`Devour`** — a flag on a card: the unit it replaces DIES instead of leaving.
- **`ReturnToHandAction`**, and **`KinRulesText`**, which is the single place card text is assembled.
- Cards: **Pyre Keeper** (1, 6/8, Devour), **Gallows Feast** (1 rite), **Butcher's Bill** (2 rite),
  **Twice Buried** (1, 4/4). Keywords: Sacrifice, Devour, and Loss rewritten to mean *died*.
- `KinBot` offers lane-scoped rites into all five lanes and Devour cards into lanes it already
  holds — without that, `sim` measures both keywords as blanks.

**Flood** (`b43e8c4`): `TakeCardsAction` moves what is standing to a new **`ZoneType.Taken`** —
out of draw, hand, discard and board — stamped with the turn it returns. `StartTurnAction` hands
back what is due BEFORE the draw. `SweepFieldAction` is deleted; **The Last Host and Detonation take
now too.** `KinBattleEffects.Apply` returns its events, so a battle doom can finally be animated.

**Art** (`324948c`): four SVGs, `art_check.gd` (renders a subject at 256 and at the 40px lane size),
and the **`draw-card-art` skill**.

**Build** (`2a889dc`): `Build-Apk.ps1`. `build/` is gitignored.

---

## 3. The design pass, and what is NOT built

It is in `KinJam.md` under "Card design pass". **Two arguments there overturn earlier reasoning in
the same file**, both of which had been used to call a card impossible:

- **Thorns is not "what every unit already does".** Your unit's power always hits the enemy in its
  lane — but thorns is damage that does not come from the intent, so it lands ON TOP of the trade. A
  6/12 wall soaks a telegraphed 8 and you take nothing; against thorns the same wall takes 8 + thorns
  and the excess spills to your face. It is the only thing that makes a big toughness body unsafe.
- **Strikes twice is not "double power".** It is a hook: thorns hits it twice, an on-strike trigger
  fires twice. It collapses to a number only in a game with no per-hit rules.

**Clusters 2-6 are designed and unbuilt**: thorns, strikes/on-strike, weaken and the clock, Powers,
the six punishing enemies, relics. Each has stat lines and text in the doc; all numbers are first
guesses in scale with the pool (1-drop ≈ 14 total stats, 2-drop 26-36, costs 0-2).

**The standing rule this pass runs on, and it is worth keeping:** *we are exploring what is fun, not
enforcing what is written.* Every rule in that doc is a note from a previous pass, and breaking one
on purpose is a design decision, not an error.

---

## 4. Scars worth not re-earning

**The build lies quietly, in two different ways:**

- **The Android export SKIPS the C# build when its own output is newer than your sources, and an
  edit saved while an export is RUNNING lands inside exactly that window.** Measured: `KinBoard.cs`
  saved at 00:55:14 with an export in flight; that export compiled at 00:55:15 without the change;
  **every later export then saw a `.dll` one second newer than the `.cs` and skipped the rebuild.**
  Exit 0, 184 assemblies, plausible size, shipping pre-edit code indefinitely. `dotnet build` proves
  nothing — only the ExportRelease assembly is stale.
  **A timestamp check cannot catch this**, because the stale assembly IS newer than the source. Only
  forcing the compile does. `Build-Apk.ps1` deletes `.godot/mono/temp/{bin,obj}/ExportRelease` and
  then checks the other direction (a source newer than the built assembly) plus the assembly count.
  **Never edit project sources while an export runs.**
- **Godot 4.6 takes the Java and Android SDK paths from EDITOR SETTINGS ONLY.** It does not read
  `JAVA_HOME` or `ANDROID_HOME`, and this machine's settings have come back empty on their own. The
  symptom is `A valid Android SDK path is required in Editor Settings` on a machine where the SDK is
  plainly installed, and there is no command-line flag for it. The script writes both into
  `editor_settings-4.6.tres` before every export.
- **Verify a build by extracting an assembly and searching for a string you just wrote** (UTF-16LE —
  that is how .NET stores them). Size, exit code and assembly count all looked perfect while the
  build was stale.

**Git, in this repo specifically:**

- **The CSharpier pre-commit hook runs `git add` on the WHOLE staged file.** A partial `git add -p`
  of any `.cs` file cannot survive a commit — the unstaged rest of the file is silently pulled in.
  It dragged three keywords into the wrong commit before it was noticed. **To split a `.cs` file
  across commits, put the intermediate state in the WORKTREE**, commit, then restore. Markdown is
  unaffected; the hook only touches `.cs`.
- **`build/` was not gitignored** and holds a 106 MB APK. It is now.

**Content and engine:**

- **`DrawCardsAction` ignored `PerEach` entirely**, reading its raw `Amount` while every other effect
  action used `Scaled`. A scaling draw would have drawn its base number and looked exactly like a
  card that worked. Found by writing a card that needed it, not by reading the code.
- **A keyword that is a FLAG has no renderer.** Every rules-text surface built its text from
  `Effects.Select(e => e.Text)`, so `Devour` — a bool, not an effect — would have rendered on no
  surface at all: a card that silently eats your unit and never says so. `KinRulesText` is now
  shared by the card face and the console dump.
- **`KinBattleEffects.Apply` executed effects inline and kept only the state**, dropping every event
  they raised. No battle-scope apocalypse could be animated — Detonation's 14 damage to the face
  included — for as long as that was true.
- **`KinArt.FileName` cuts a card name at the first comma, em dash or HYPHEN.** "Twice-Buried" looks
  for `twice.svg`, misses, and falls back to a generated figure with no error. Fix the NAME, not by
  adding a file called `twice.svg`.
- **You cannot tell what an SVG looks like by reading it.** The first Butcher's Bill cleaver had a
  curved blade and rendered unmistakably as a frying pan. Render it (`art_check.gd`) and look, at
  256 AND at 40.
- **Two existing tests described the OLD rule as a virtue** and had to be rewritten, not deleted:
  `ALaneTargetFromSomethingWithNoLaneHitsNothing` proved a rite could never use a lane rule, and a
  Flood test looked for the washed card in the discard pile. When a rule changes, find the tests that
  were protecting the old one and ask what they should protect now.
- **Anything that kills a unit mid-turn must clear the dead inline.** `PlayCardAction` documents that
  a replaced unit is always a live one; `DestroyAction` and Devour keep that true by clearing
  immediately. Anything new that kills during your turn must do the same or it discards a corpse
  without firing its death.

---

## 5. Balance debt — READ BEFORE TOUCHING NUMBERS

**Every table in `docs/findings/doom-balance.md` was measured with Flood doing nothing**, and Flood
is `MinFloor = 1`. Act 1 is now harder than any number in that file says. The same applies, smaller,
to The Last Host and Detonation.

**The only measurement taken this session is a smoke test, not a result.** `sim 25` put The Rising at
27.8% of 18 runs against 6.0% in run 15 — right direction, far too small a sample, and the other two
acts drew 6 runs and 1 run. **Never report one number across acts.** Nothing was written to the
findings file, deliberately.

**The re-measure was deferred to after the thorns and strikes clusters**, which will move everything
again. That was a decision, not an oversight.

---

## 6. What to do next

1. **Cluster 2 — thorns, then strikes and on-strike.** One branch each in `EndTurnAction.ResolveLanes`,
   and they unlock the unit cluster and the enemy cluster together. Build the enemy counterparts
   (Razorback, Flail Knight) in the same pass: content designed against a board where every enemy is
   one fixed number is balanced against a board we are deleting.
2. **Then a real `sim`, per act**, and write it up as run 18 in `docs/findings/doom-balance.md`.
   It is the first honest measurement since Flood started firing.
3. **v3 phase 5 — intent sequences with Piercing and Shifting**, which is the precondition for
   `Persistent` (phase 6) and is already specified in `KinV3Plan.md`.
4. **Powers** (cluster 5). They need no lane, so they do not need phase 5 to be safe, and they are
   the cheapest route to per-turn triggers and scaling content. Relics are the same machinery with
   run scope — do them only if Powers land.
5. **Re-read the six `FiringRead.Standing` scenarios** against v3. Flood, The Last Host and
   Detonation are fixed; Nuclear, Hell Uprising, Famine, Judgement, AI Uprising and Grey Goo have
   not been re-read since withdrawal changed meaning.
6. **"Withdraw" is still not in the glossary** and combat v3 is built on it. One line, and it is
   load-bearing vocabulary.

**Smaller, and genuinely unfinished:**

- **The new cards have never been looked at on a card face.** "8 to every enemy per Loss this turn"
  is 34 characters against a box that truncated at 37 once; `kin_card_preview.tscn` is the loop for
  it and was not run this session.
- **The `Taken` zone has no visual** beyond a line in the battle log. A player sees cards vanish and
  is told only in text that they come back.
- **Gallows Feast's art says *gallows* loudly and *feast* quietly.** If the card should read as its
  payoff rather than its cost, it wants a redraw.
- Everything still open from `HANDOFF-KinCombatV3.md` §6 — difficulty against the 25-50% target, and
  2-drops behind 1-drops.

---

## 7. How to reproduce anything here

```
dotnet test KinCore.Tests/KinCore.Tests.csproj          # 159, ~5 min
dotnet run --project KinConsole -c Release -- content    # every card, as the player sees it
dotnet run --project KinConsole -c Release -- sim 25     # smoke test; 25 is NOT a measurement
./Build-Apk.ps1                                           # the phone build, ~16 min, verified
```

**To get it onto a phone**, serve `build/` and tunnel it:

```
cd build; python -m http.server 8000 --bind 127.0.0.1
cloudflared tunnel --url http://localhost:8000 --no-autoupdate
```

The tunnel URL is public but unguessable and **changes every restart**; the machine must be on. Both
processes died with the last session, so there is no live URL. Chat file transfer caps at 30 MiB, so
the APK can never be sent that way.
