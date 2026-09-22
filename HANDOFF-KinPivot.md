# Handoff — the doom comes out, the prefix becomes Kin, and the companion becomes the point

**Read this, then `KinJam.md`'s header, then `docs/findings/kin-balance.md`'s header.**
`HANDOFF-KinCardsAndFlood.md` is the previous session and is superseded — read only its §4 scars.

State at handoff: **131 tests green in ~37s** (was 159 in 3m52s before the pivot). Solution builds,
`KinConsole` runs, the Godot project imports and plays, and an Android APK was built and played on a
real phone. **Seven commits on `kin-pivot`, nothing pushed, working tree clean.**

---

## 1. What this session did

**The jam is over and the game pivoted top-down.** The doom theme was judged the weakest part of the
design and the companion the strongest, so the doom layer is gone and the companion is the spine.

1. **Deleted the doom layer** — ~1,300 lines, net deletion.
2. **Renamed `Doom*` to `Kin*`** across projects, namespaces, scenes, docs and settings.
3. **Added a companion roster and a run-start select screen** — five companions, five axes.
4. **Made lane slots square** so generated art fills them, which deletes the transparency problem.
5. **Rebuilt the power curve as companion upgrades** — 0.0% to 23.0% act completion, measured.
6. **Installed local image generation** and proved the pipeline end to end.
7. **Fixed two bugs found by playing**, one of them latent since before the pivot.

---

## 2. What changed, in detail

### Deleted

`KinScenario`, `ScenarioLibrary` (14 scenarios), `KinTransform(s)`, `KinBattleEffects`, `KinPreview`,
`KinFiring`, `KinScope`, `ResolveKinAction`, `KinClockDial`, five test files, and the `Irradiated`
keyword — which survived the first sweep and was still telling the player *"drawing this card costs
1 life"* about a rule that no longer exists.

Stripped rather than deleted: the countdown and firing record off `KinBattle`, the doom branch in
`Run.AfterBattle`, `EffectTrigger.OnDoomFires`, `CountOf.DoomsFired`, `Bands`/`FinalDoom` off
`ThemeDefinition`, and the clock and scenario banner off `KinBoard`.

**`ThemeLibrary` was KEPT** — acts still need names and bosses; only the schedule went.

### Kept deliberately, with no caller

**`TakeCardsAction` and `ZoneType.Taken`.** The "something takes your cards for N turns" primitive,
which is the shape a boss ability wants later. `TakenCardTests` drives the action **directly** rather
than through the deleted Flood, so it cannot rot silently.

### Added

- **`StarterContent.Roster`** — Ash (attrition), Bramble (survival), Tally (volume), Pike (the
  face), Moss (spatial). Each has a test proving it actually fires.
- **`KinCompanionSelect`** — the run-start screen `KinBoard` had predicted in a comment.
- **`CompanionUpgrade` + `UpgradePool`** — the power curve. See §3.
- **Square 175x175 lane slots**, art edge to edge. Card ratio was tried first and measured **493px
  over** the 1080 canvas, because there are two lane rows.
- **`KinCardTap`** — one tap takes a card, on a phone as well as a mouse.
- **`tools/gen_art.py`** and the **`generate-card-art` skill**.
- **`Run-Godot.ps1`** — runs a scene on the second monitor, with `godot-mono`, creating the capture
  directory.

---

## 3. The power curve — rebuilt, and the dial is non-linear

`KinJam.md` said it and it was true: *"the apocalypses ARE the power curve — there is no separate
progression system."* Deleting them cashed that in, and companion upgrades replace it.

| `FloorsPerUpgrade` | upgrades a run | act completion |
|---|---|---|
| every floor (1) | 24 | **82.5%** |
| **every second floor (2)** | 12 | **23.0%** — chosen, target 25% |
| every third floor (3) | 8 | **1.5%** |

**Per-act clear rates at the chosen setting: 68.5% / 60.6% / 55.4%.** Smooth descending attrition,
no act unplayable and none trivial — a better shape than the pre-pivot 20.7 / 34.3 / 29.0, which was
not even monotonic. Reported per act because the single 23.0% would hide exactly the failure this
repo's own rule was written about.

**Three-to-one on frequency is 82.5% to 1.5%.** Upgrades compound, and Echo compounds hardest
because its value is whatever you already took. **Measure this dial, never interpolate it.**

**`OffersUpgradeOn` is asked in ONE place** so the front end and the simulator cannot drift.

---

## 4. Two bugs found by PLAYING, which no test had caught

**A card that killed the Opponent did not end the battle.** `IsOver` was set in `EndTurnAction` and
nowhere else, so lethal from a direct-damage card left the fight running until End Turn was pressed
on a corpse. Fixed at the root: `KinStateExtensions.SettleBattleEnd` is the ONE account of a battle
ending, idempotent, called from `DealDamageAction` — the single chokepoint every mid-turn packet
passes through. **All three new tests were confirmed to fail with the fix reverted.**

**Reward cards needed TWO taps on a phone.** `CardUI2D` raises `Clicked` only while it is the
*hovered* card, and a finger has no hover. The hand was never affected because cards there are
dragged. `KinCardTap` wires the hover area's own input instead — `HoverArea.input_pickable` is true
and its shape is 232x315, verified on the scene rather than assumed.

**The wrong diagnosis, recorded because it cost the most time.** The obvious suspect was the new
upgrade row making the panel overlap the cards. Measured: panel ends at y=393, cards start at 427,
and the panel is `MouseFilter.Ignore` anyway. The geometry was never involved.

---

## 5. Scars worth not re-earning

- **`godot` on PATH is the NON-.NET build and cannot load C# at all.** Use `Run-Godot.ps1`, which
  uses `godot-mono`. Cost ~15 minutes chasing phantom rename damage.
- **`--write-movie` needs its output directory to exist.** Exits 0, prints "Done recording movie at
  path: ...", writes nothing. `Run-Godot.ps1` creates it.
- **A `Button` with `Flat = true` draws no stylebox** — panel, border and hover vanish silently.
- **`git mv` on a directory refuses if anything inside it is a staged deletion.** Plain `mv` plus
  `git add -A` is fine; git detects renames by content.
- **A word-boundary rename misses `ToDoomCard`.** Audit a rename with a case-insensitive
  `\w*doom\w*` sweep afterwards, not the pattern you renamed with. The lowercase `[doom]` section in
  `project.godot` slipped through the same way and would have failed silently.
- **A heal at full life is clamped to nothing**, so the obvious life-gain test asserts 0 and passes
  for the wrong reason.
- **The field is EMPTY at `OnTurnStart`** — units withdraw at end of turn. Two of five companions
  would have been silent no-ops on the obvious trigger. `CardsPlayedThisTurn` is zero there too.
- **A guard invented to cover your own second wire is not a guard.** `KinCardTap` shipped with two
  input paths and a latch to dedupe them; the latch only existed because of the second wire.

---

## 6. Local image generation — working

**ComfyUI 0.37.0 portable at `D:\AI\ComfyUI_windows_portable`**, torch 2.13+cu130 on an RTX 3060 Ti.
`dreamshaperXL_turbo.safetensors` is the measured choice. **~12s per 1024x1024 image.**

The full procedure, including detecting whether any of it is installed and walking a user through
setting it up, is the **`generate-card-art` skill**. The findings that skill exists to carry:

- **Malformed anatomy is a SEED lottery with a per-subject base rate.** Generate 3+, cull ~1 in 3.
- **Low-poly faceted skin was the POSITIVE prompt** — "simple geometric shapes". Negating "low
  poly, faceted" did not fix it; removing the phrase did.
- **Negatives are nearly inert at CFG 2.** Anatomy negatives did not fix the heads, and CFG 3.5/5/7
  left the malformed seed malformed.
- **A contact sheet is not an inspection.** Check at 500px or more.
- **Illustrious is the wrong tool** with natural-language prompts: 27 images, 9 seeds, mostly
  abstract shapes and blank frames. It is booru-tag trained. `illustriousXL_v01.safetensors` is
  still on disk (6.6GB) and can be deleted.

---

## 7. NOT DONE — the work this session leaves behind

### 7a. The card design pass — **the biggest gap, and it is a fun problem not a theme one**

**26 distinct units and 8 rites, and only about 15 units carry an effect at all.** The rest are
vanilla stat lines. There are no combos to find and nothing to build toward beyond the companion.

The vocabulary already exists and is barely used:

| | available | used by |
|---|---|---|
| triggers | `OnPlay`, `OnDeath`, `OnTurnStart`, `OnTurnEnd` | mostly `OnPlay` |
| actions | Buff, DealDamage, Destroy, DrawCards, GainLife, ReturnToHand, **TakeCards** | TakeCards has no caller at all |
| reads | `YourUnits`, `LivingEnemies`, `DiedLastTurn`, `DiedThisTurn`, `CardsPlayedThisTurn` | a handful |
| targets | self, player, opponent, all enemies, lane, **adjacent lanes** both sides | adjacency barely used |
| keywords | Sacrifice, Devour, Exhaust, Rite, Taken, Adjacent | Cluster 1 only |

`KinJam.md`'s "Card design pass" has clusters 2-6 designed and unbuilt: thorns, strikes/on-strike,
weaken, Powers, and the six enemies that punish a behaviour. **Two arguments recorded there overturn
earlier reasoning — thorns and multi-attack are NOT redundant with what combat already does.**

Two stand-in reads to replace while doing this: Reactor Crew and Long Watcher were rehomed off
`CountOf.DoomsFired` onto `CountOf.YourUnits` and carry `ponytail:` comments saying so.

### 7b. The setting and re-theme — **parked deliberately**

`KinSettingSketches.md` has three frames with regions, and the decisions taken. Blocked on nothing
but a choice. What it unblocks: card names, enemy names, act names, the palette, all 56 SVGs, and
the title (`ENDLING` is still the grimmest word in the project).

**Two code prerequisites if regions become real places:** enemies are NOT act-scoped
(`PlayableOn(floor)` is run-wide, so act 3 draws act 1's creatures), and `ActMap.Order` is a fixed
array with no pool to draw variants from.

### 7c. The art pass

56 authored SVGs, all in the grimy register. The pipeline is proven and the skill is written; this
is blocked on 7b, not on tooling. About 35 minutes of GPU for 168 candidates at 3 variants each.

### 7d. Smaller, known, and deliberately left

- **`Tags` / `HasTag` on `RunCard` and `KinCard` has ZERO writers** — it existed for the doom marks.
  Inert, not lying. Delete it, or give it a use in the card pass.
- **Evolutions at act breaks** — wanted, and now sensible because there is a tuned per-floor curve
  to sit on top of.
- **Companions unlocked between runs** — where the collecting fantasy lives given one companion.
  Nothing exists.
- **The top band is half-empty** where the clock dial sat, and floor/act prints twice.
- **Lane polish**: the bottom stat scrim clips a standing subject's feet; the floating damage number
  sits half outside the smaller cell; enemy silhouettes are dark on a dark ground.
- **`KinV3Plan.md` phases** about intent patterns and `Persistent` are still live and unbuilt.
- **`CREDITS.md`** must ship and may credit art that is about to be replaced.
- **The branch is `kin-pivot`, nothing is pushed.**

---

## 8. What I would do next

1. **Play it more.** Both real bugs this session came from one phone session, not from 131 tests, a
   200-run sim, or any screenshot. The four questions worth answering: does the companion change how
   you draft, is the upgrade pick a decision or a formality, is the battle boring without the clock,
   and do the square slots read at arm's length.
2. **The card design pass (7a)** — it needs no setting, and it is where "is this fun" actually lives.
3. **The setting (7b)**, then the art (7c), in that order.
