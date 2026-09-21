# Handoff — the doom comes out, the prefix becomes Kin, and the companion becomes the point

**Read this, then `KinJam.md`'s header, then `docs/findings/kin-balance.md`'s header.**
`HANDOFF-KinCardsAndFlood.md` is the previous session and is superseded — read only its §4 scars,
which all still hold.

State at handoff: **122 tests green in 14s** (was 159 in 3m52s), the solution builds, `KinConsole`
builds and runs, the Godot project imports and plays. **Nothing committed — the whole pivot is in
the working tree**, 243 deletions, 18 modifications, 18 new files.

---

## 1. The headline

**The jam is over and the game pivoted top-down.** The doom theme was judged the weakest part and
the companion the strongest, so the doom layer is gone and the companion is the spine. The new
register is monster-collecting-adjacent and deliberately not dark — but **no fiction has been
chosen yet**, and the 56 grimy SVGs, card names and enemy names are all still to be re-themed.

**The `Doom*` prefix is now `Kin*`**, chosen as a codename that survives the re-theme rather than as
the game's title. `ENDLING` is still the player-facing name in `MainMenu.Title` and
`project.godot`, and is also expected to change.

**A companion roster and a run-start select screen exist.** Five companions, five axes, all five
verified to actually fire.

**The game is currently unbalanced to 0% act completion, and that is measured, expected and
recorded.** See §3 — it is the single most important thing in this file.

---

## 2. What changed

### Deleted (~1,300 lines, net deletion)

`KinScenario`, `ScenarioLibrary` (14 scenarios), `KinTransform`, `KinTransforms`,
`KinBattleEffects`, `KinPreview`, `KinFiring`, `KinScope`, `ResolveKinAction`, `KinClockDial`, and
five test files. Stripped rather than deleted: the countdown and firing record off `KinBattle`, the
doom branch in `Run.AfterBattle`, `EffectTrigger.OnDoomFires`, `CountOf.DoomsFired`, the `Doom`
keyword, `Bands`/`FinalDoom` off `ThemeDefinition`, and the clock and scenario banner off
`KinBoard`.

**`ThemeLibrary` was KEPT, not deleted.** Acts still need names and bosses; only the doom schedule
went. `DiedRunCardIds` and `SummonedRunCardIds` went too — written by two actions, read by nothing
but the transforms.

### Kept deliberately

**`TakeCardsAction` and `ZoneType.Taken` have NO caller in content.** They are the "something takes
your cards for N turns" primitive, which is the shape a boss ability wants later — you named that
use case directly. `TakenCardTests` was rewired to drive the action **directly** rather than through
Flood, so it cannot rot silently. Deleting a tested primitive to re-add it in a month is the wrong
kind of lazy; leaving it untested would have been worse.

### Rehomed rather than deleted

Six `OnDoomFires` effects became `OnTurnEnd`: enemies Tollman and Doomsayer, bosses The Last Warden
and The Last Morning, and Opponent traits Cruel and Vindictive. **Numbers were divided down** — the
doom fired roughly every three turns and these now fire every turn. Four cards (Salvage Rig, Drone
Swarm, Reactor Crew, Long Watcher) moved to `OnPlay`; the two that scaled on `DoomsFired` now read
`CountOf.YourUnits` and carry a `ponytail:` comment saying so — **those are stand-in reads, not
designs**, and want a real one in the card pass.

### Added

- **Square lane slots, art edge to edge** (`KinLaneCell`, 296x156 -> 175x175). Chosen over card
  ratio, which measured 493px over the canvas. **This deletes the transparency requirement**:
  generated art is opaque 1024x1024 and a square slot takes it with no crop and no matting. Two
  framing branches — opaque drawings cover, transparent fallback silhouettes stay centred on a
  ground. See `KinUI.md`.
- **`KinArt.Drawing()` tries `.png` before `.svg`** — it hardcoded `.svg`, so no generated art could
  load at all.
- **`StarterContent.Roster`** — five companions: Ash (attrition), Bramble (survival), Tally
  (volume), Pike (the face), Moss (spatial).
- **`KinCompanionSelect`** — the run-start screen. `KinBoard` had predicted this exact screen in a
  comment; it reads `StarterContent.Roster` rather than holding a list of its own.
- **`tools/gen_art.py`** — drives a local ComfyUI to generate card art. See §5.

---

## 3. THE BIG ONE: there is no power curve, and it measures 0%

`KinJam.md` said it outright and it was true: *"the apocalypses ARE the power curve — there is no
separate progression system, by design."* Deleting them cashed that in.

| state | act completion | mean floor |
|---|---|---|
| before (run 20) | 20.7–34.3% by act | — |
| after, unchanged | **0.0%** | 12.72 |
| after, `StartingLife` 120 → 180 | **0.0%** | 14.57 |
| after, the six rehomed effects neutralised | **0.0%** | 14.84 |

**Both controls were run rather than reasoned about, and both matter:**

1. **Life is not the lever here.** This repo's own rule — `mean floor = life budget / life lost per
   battle` — still holds, but the denominator moved *with* the numerator: life lost per battle went
   18.4 → 28.1 as the budget rose, because the bot spends slack on tempo. `StartingLife` is back at
   120; do not reach for it first.
2. **The hole is structural.** Neutralising every effect rehomed off the doom trigger still measured
   0.0%, so the rehoming is a ~2-floor contributor and not the cause. The deck no longer gains power
   while `HealthScaleFor` and `AttackScaleFor` keep climbing per act.

**Do not tune balance until a progression system exists.** Numbers measured before that lands get
thrown away. The companion is the intended answer — and per `Companion.cs`, companion growth must be
a **decision at a screen**, never an automatic trickle: marks were already cut once on the playtest
note *"I never liked this mechanic"* for exactly that reason.

A human plays far better than `bot-1/v3`, and the bot was tuned against the old game — so 0% is a
trend, not a literal claim that the game is unwinnable. The trend is real.

---

## 4. Scars worth not re-earning

- **`godot` on PATH is the NON-.NET build and cannot load C# at all.** It reports `4.6.2.stable`
  with no `.mono`, fails every script load on import, and the errors look exactly like a broken
  project. Use `godot-mono`, as `Commands.md` line 23 already says. Cost ~15 minutes of chasing
  phantom rename damage.
- **`--write-movie` needs its output directory to already exist.** It exits 0, prints "Done
  recording movie at path: …", and writes nothing. The tell is `ERROR: Condition "f_wav.is_null()"`
  buried in the output.
- **A `Button` with `Flat = true` draws no stylebox** — panel, border and hover all vanish silently.
  The first companion-select capture was five columns of loose text floating on the background.
- **`git mv` on a directory refuses if anything inside it is a staged deletion** ("fatal: bad
  source"). Plain `mv` plus `git add -A` is fine; git detects the renames by content.
- **`\bDoom` does not match `ToDoomCard`.** The word-boundary regex that renamed 164 files missed
  every internal occurrence. Audit with a case-insensitive `\w*doom\w*` sweep afterwards, not the
  same pattern you renamed with.
- **A heal at full life is clamped to nothing**, so the obvious test for a life-gain ability asserts
  0 and passes for the wrong reason. `BramblePaysLifeForWhatSurvived` starts the run hurt on purpose.
- **The field is EMPTY at `OnTurnStart`** — units withdraw at end of turn. Two of the five
  companions would have been silent no-ops written on the obvious trigger. Anything reading your
  board has to pay at `OnTurnEnd`, and `CardsPlayedThisTurn` is likewise zero at turn start.

---

## 5. Local image generation — installed and working

**ComfyUI 0.37.0 portable at `D:\AI\ComfyUI_windows_portable`** (C: had only 22GB free; D: has
1.7TB). torch 2.13+cu130, CUDA sees the RTX 3060 Ti's 8GB.

- **`dreamshaperXL_turbo.safetensors`** — 6.6GB, downloaded and working. 8 steps, CFG 2.0.
- **`illustriousXL_v01.safetensors`** — was still downloading at handoff; check it completed.
- Start it: `D:\AI\ComfyUI_windows_portable\python_embeded\python.exe -s ComfyUI\main.py --port 8188`

**Measured: 13.7s per 1024×1024 image.** Quality is good — flat vector, centred full body, plain
background, no text leakage.

**The 40px test passed, against expectation.** Downscaled to the lane-cell size the silhouettes
still read — heron, dog, toad and tortoise all identifiable. This was the risk that would have
killed the idea and it did not.

**Two gaps before this can ship into the game:**

1. **Backgrounds are not transparent**, and `KinArt.Drawing` composites RGBA figures over a card
   body and a lane plinth. Needs a matting pass (`rembg`) — untested so far.
2. **"Plain solid background" leaks scenery** — the tortoise and toad came back standing on grass,
   and the background hue differs per image. Prompt and negatives need tightening before a batch.

### Settled: DreamShaper + `flat`, 8 steps, CFG 2.0, 3 variants, cull by eye

**Illustrious is unusable with natural-language prompts** — 27 images over 9 seeds produced abstract
triangles, blank frames, and a heron with garbled text baked in. It is booru-TAG trained and wants
`tortoise, moss, simple background` style tags, not English sentences. That is a prompt-language
mismatch rather than a bad model, but it is a discipline to learn for a style that leans anime-
monster, and DreamShaper works first try. Delete the checkpoint unless someone wants to learn tags.

### Three defects found, and what each one actually was

**None of these were caught by a contact sheet. All three were caught by a person looking at the
full-size image**, after the sheet had been declared clean twice.

1. **Two-headed tortoise — a SEED lottery with a per-subject base rate.** One seed was originally
   locked across the whole comparison so a batch would "look like one artist"; that made a single
   bad seed fail every style and both checkpoints *identically*, which is the worst failure mode
   because the output stays consistent and reads as deliberate. Fixed: `subject_seed()` varies per
   subject, `--variants` defaults to 3. **Culling is not optional** — "a mossy tortoise" in the
   storybook style doubled on 2 of 3 seeds, while the heron never doubled on any.
2. **Low-poly faceted skin — MY PROMPT.** The flat style said `simple geometric shapes`, which read
   as *build the creature out of geometric shapes* and produced flat-shaded 3D mesh facets on every
   body, every seed. A/B'd on fixed seeds: removing the phrase cleared it; negating
   `low poly, faceted, triangulated` on top of the original phrase did NOT.
3. **Negative prompts are nearly inert at CFG 2.** Anatomy negatives did not fix the two heads, and
   background negatives (`grass, forest, trees`) did not remove scenery. Tested CFG 3.5 / 5.0 / 7.0
   at 20 steps — the malformed seed stayed malformed. **Fix the positive prompt, not the negative.**

### Subject wording drives the background

`a mossy tortoise` drags in a forest in every style; `a heron` never does. Background is what breaks
both the 40px read and the matting pass, so subject phrasing needs a convention before a batch.

`tools/gen_art.py` locks the checkpoint and style suffix — which is what actually makes a set
coherent — and varies the seed. Three styles are defined (`flat`, `storybook`, `chunky`); `flat` is
the recommendation on 40px readability and on having the plainest backgrounds.

---

## 6. What to do next

1. **Pick the fiction.** Everything below is blocked on it, and it is a decision only you can make.
2. **Decide the progression system**, then balance. Not before. The companion is the candidate.
3. **Lock an art style** — generate the same three subjects across all three styles and compare, then
   solve transparency.
4. **Re-theme the content** — card names, enemy names, act names, the palette, and the 56 SVGs.
5. Cosmetic leftovers on the board: the top band is half-empty where the clock dial sat; floor/act
   prints twice (banner *and* status strip); the lane's bottom stat scrim clips a standing
   subject's feet; the floating damage number sits half outside the smaller cell; enemy
   silhouettes are dark on a dark ground.
