# Handoff — ENDLING on Android, the remote loop, and a doom that did nothing

**Read this, then `KinJam.md`, then `KinV3Plan.md`.** `HANDOFF-KinCombatV3.md` is the previous
session and is superseded — read only its §4 scars, which are all still true.

State at handoff: **147 tests green** (146 + one new), Godot builds, KinConsole builds, **an Android
APK builds and runs on a real phone**. Working tree is DIRTY and nothing is committed — the user
reviews before committing.

---

## 1. The headline

**ENDLING runs on a phone, and the whole loop is remote.** One command produces a signed APK; a
Cloudflare quick tunnel serves it on a public HTTPS URL with no upload step, so a change can go from
edit to "installed on the phone" without touching the machine.

**Dragging did not work on touch, and the cause was not touch.** It was a hover precondition that a
finger can never satisfy — see §3. That fix is in `Common/Core`, so everything draggable got it.

**And Flood — a doom that has shipped since v1 — does nothing at all.** See §4. It is the biggest
thing in this handoff and it is not fixed yet.

---

## 2. What exists now

- **`--export-release "Android"` produces a 106 MB signed arm64 APK.** Documented in `Commands.md`
  under "Android build (phone)", with all four of its silent failure modes.
- **The whole Godot dependency chain is pinned to `net9.0`** — `SQGodotCommon`, `KinCore`,
  `ImmutableGameObjects`, `MtgCore`, `MtgSimulator`. The prebuilt Android template supports net9.0
  and refuses anything else. Raising any of those TFMs breaks the PHONE build only; desktop and
  tests keep working, so it fails where nobody is looking.
- **Touch drag works**, confirmed on a real device by the user.
- **Keyword reminder text is ~30% shorter** with no rules lost, and `ReminderTextStaysTight` keeps
  it that way.

---

## 3. Scars worth not re-earning

**Android export, four ways it fails quietly:**

- **Godot refuses Android export unless `rendering/textures/vram_compression/import_etc2_astc=true`
  and prints NOTHING to say so.** `has_valid_export_configuration` does a bare `valid = false` with
  no message appended (`platform/android/export/export_plugin.cpp`). The only symptom is
  "configuration errors:" followed by the *unrelated* "C#/.NET is experimental" line, which sends
  you chasing the .NET support status instead. Reading the engine source was the only way to find it.
- **A missing solution produces a SUCCESSFUL APK containing zero C# assemblies.** The `.sln` lives
  one level above the Godot project, so `dotnet/project/solution_directory=".."` is required. Without
  it the export prints one C# stack trace in the middle of a long log, **exits 0**, and ships a game
  with no code in it. It would have installed and launched to nothing.
  **Always check `unzip -l build/endling.apk | grep -c '\.dll'` — ~184, never 0.**
- **`NETSDK1152`** — each referenced project emits `deps.json` in both the RID and non-RID output
  dir. `ErrorOnDuplicatePublishOutputFiles=false` in `SQGodotCommon.csproj`, with a comment.
- **Everything imported into the project ships, at ETC2 size rather than on-disk size.** The 367
  `--write-movie` frames in `shots*/` were 122 MB of the APK until each dir got a `.gdignore`, and
  MtgGame's card art is 34 MB of JPG that imports to **145 MB** of texture (`exclude_filter`).

**An APK that is byte-identical in size is NOT evidence the build did not change.** Two consecutive
builds came out at exactly 105,977,040 bytes while `SQGodotCommon.dll` inside went 493,056 →
493,568. Zip alignment absorbed it. Verify by extracting the assembly and grepping for a symbol you
just added, not by looking at the total.

**Touch:**

- **A hover precondition is a touch bug.** `CardUI2D`'s `CanDrag` required
  `CardUIManager.CurrentHoveredCard == this`. With a mouse the pointer hovers for many frames before
  the click; with a finger **the press IS the first contact**, Area2D picking has not run,
  `MouseOveredCards` is still empty, and `CurrentHoveredCard` is null — so `CanDrag` is false at
  exactly the moment the press arrives, and the press is gone forever. The tell was oddly specific
  and correct: the only draggable card was one a PREVIOUS touch had left hovered.
- **The fix is not to hit-test harder at press time.** Nothing can disambiguate an overlapping fan on
  that frame, because the data hover uses does not exist yet. A press now only ARMS the drag
  (`_pressArmedDrag`) and the first motion starts it, once `CardUIManager._Process` has picked a
  winner. Disarm happens on ANY release, including outside the area — otherwise a press on empty
  space stays armed and the next drag passing over the object grabs it.

**Text:**

- **The fluff generator is: state the rule, then restate it as a consequence.** Rite said "not a
  body", "goes to the discard pile" AND "never holds a lane" for one fact.
- **But length is the symptom, not the rule.** Toughness' second sentence (damage beyond it hits the
  face behind) is an independent rule nothing else states. The rule is **cut any clause the player
  can derive from the clause before it** — a judgement, so the test only holds the SHAPE (20 words,
  2 sentences) and asserts nothing about what any keyword says.
- **The shape test immediately caught three sprawls that eye review had passed** — Doom, Loss and
  Toughness — and the prediction of which would fail was wrong. Do not eyeball word counts.
- **"Withdraw" is not in the glossary** and combat v3 is built on it. Loss' text has to say "leaving
  at end of turn" longhand because there is no term to point at. Looks like a real content gap.

---

## 4. THE FLOOD FINDING — a doom that does nothing

**`SweepFieldAction` and `WithdrawUnitsAction` do the same thing**: move every non-companion unit
from Field to Discard. `EndTurnAction` spawns the doom and THEN the withdrawal. So Flood sweeps the
board a moment before combat v3 sweeps it anyway.

| Scenario | Effects | Status |
|---|---|---|
| **Flood** | sweep only | **fully inert — the doom does nothing** |
| The Last Host | sweep + 14 damage | half inert; only the damage lands |
| Detonation | sweep + 14 damage | half inert; only the damage lands |

All three tell the player something false — "the board is gone", "everything you hold is taken" —
about a board that was leaving regardless.

**This is `CLAUDE.md`'s "verify a primitive fires" scar at the SCENARIO level.** Combat v3 turned a
working doom inert without touching it, and nothing failed. The v3 pass changed what withdrawal
means and never re-asked what that did to scenarios built on the old meaning. **Every scenario whose
effect is "remove your units" needs re-reading against v3, not just these three.**

**Balance consequence: Flood is `MinFloor = 1`, so early floors carrying Flood have been
effectively doom-free, and `docs/findings/doom-balance.md` runs 15-24 were measured with that in
them.** Fixing Flood makes act 1 harder than every number in that file says.

**Decided direction (user, this session): swept cards are removed from your DECK for N turns, then
return.** Not the body — the card. Under v3 the body was leaving anyway, so the only thing left
worth taking is the card, and taking it temporarily is a cost the player can play around. **Not
built.** It needs a delayed-return mechanism that does not exist yet; check whether anything in the
engine already does timed returns before building one.

---

## 5. Remote build and delivery

```
godot-mono --headless --path SQGodotCommon --export-release "Android" \
  C:/SQGodotHelperApps/SQGodotCommon/build/endling.apk
cd build && python -m http.server 8000 --bind 0.0.0.0
cloudflared tunnel --url http://localhost:8000 --no-autoupdate    # prints a public HTTPS URL
```

The tunnel URL is **public but unguessable, and changes every restart**. The machine must be on.
Rebuilding into `build/` is enough — the same URL serves the new file, with no upload.

**Chat/file transfer caps at 30 MiB**, so the APK can never be sent that way. Do not try again.

**The repo `slippery39/SQGodotCommon` is PUBLIC** (confirmed unauthenticated against the GitHub
API). The user does not want a public repo for a game they intend to release — flagged, not changed.
This is also why GitHub Releases was rejected as the hosting route; private release assets need an
authenticated request, which a phone browser will not make. Firebase App Distribution is the free
tool for private test builds if that comes up again.

---

## 6. What to do next

1. **Card, relic and enemy design — the reason this session handed off.** The user wants the three
   designed together so they complement each other and produce real deckbuilding decisions.
   **Read `KinJam.md:304` FIRST: "The relic slot in this game is occupied by the dooms," and line
   631 cuts relics from v1 "revisit only after playtesting".** Relics are a deliberately rejected
   feature, not a missing one. Playtesting has now happened, so reopening is legitimate — but it
   reopens a decision, and anything added has to earn a slot the dooms currently hold.
2. **Fix Flood** (§4), then re-measure act 1. A scenario that does nothing has been in the game since
   v1 and in every balance table since.
3. **Re-read every scenario against v3's meaning of withdrawal**, not just the three sweeps.
4. **Consider "Withdraw" as a keyword.** One line, and it is load-bearing vocabulary the glossary
   currently cannot name.
5. Everything still open from `HANDOFF-KinCombatV3.md` §6 — difficulty at 12% against a 25-50%
   target, and 2-drops behind 1-drops.

---

## 7. How to reproduce anything here

```
dotnet test KinCore.Tests/KinCore.Tests.csproj          # 147, ~4 min
dotnet run --project KinConsole -c Release -- sim 25     # ~5 min; 120 will time out
dotnet run --project KinConsole -c Release -- content    # every card, as the player sees it
```

Android prerequisites installed this session, all one-time: `temurin17-jdk` (scoop), Android SDK
cmdline-tools + `build-tools;36.0.0` + `platforms;android-36`, Godot **4.6.3** mono export templates
in `~/scoop/persist/godot-mono/editor_data/export_templates/` (scoop's godot-mono is PORTABLE — its
editor data is NOT in `%APPDATA%/Godot`), a debug keystore beside them, and `cloudflared`.
`SQGodotCommon/export_presets.cfg` is gitignored, so a fresh clone has no Android preset.
