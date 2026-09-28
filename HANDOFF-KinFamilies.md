# Handoff — the look, the journey, levels, and the first two FAMILIES

**Read this, then `KinFamiliesPlan.md` (the design interview, the brief, the Grove + Ember kits), then
the top of `KinJam.md` (newest first).** `HANDOFF-KinRelay.md` is the session before this one: the
Relay's rules still hold; its screens, run structure and "next" list do not — read it for its scars.

## State at handoff (2026-09-27)

- Branch **`kin-pivot`**, nothing pushed. **310 KinCore tests green**; the solution and the Godot
  project build. `SQGodotCommon/project.godot` is left UNCOMMITTED on purpose (a headless import
  strips two comment lines; it predates all of this).
- The last APK (built from the committed tree) was served to Shayne over a Cloudflare tunnel.
- **Shayne is about to PLAY the families slice** — practice scenarios 4 (Ember) and 6 (Grove), and a
  run with Bramble (Grove) or Pike (Ember). Nothing below the brief is settled until he has.

## 1. The game now, in one paragraph

A monster-collecting roguelike deckbuilder, in **style D** (fine line art, cel-shaded). A run is
**town → wild route → next town → leader → …**, both as interactive MAPS: a town is buildings you walk
to (hospital — healing costs gold — shop, pen, the leader's hall from town 2, the gate); a route is a
branching node map (visible wild fights, tall grass, finds, springs, trainers, one rare lair). Battles
are THE RELAY (two lines, steps from the back, fronts clash last). Every creature has a **LEVEL**;
yours gain XP. Monsters belong to **FAMILIES** (types without a weakness chart) — **Grove** (growth:
Grow, Rooted Block, Thornwall) and **Ember** (spell count: Kindle) are built; Storm and Mire are tags
and themes only, so far.

## 2. What this session did (commits, oldest first)

| Commit | What |
|---|---|
| `773727c` | The Relay's sprite screen (the previous session's Phase 4) |
| `78921e5` | **Style D chosen**, the visual-design pipeline and the `match-mockup` skill |
| `5168042` | **The look rebuilt**: stage, standing sprites, textured UI kit, run screens |
| `b14884d` | **The journey**: route + town maps, leaders, LEVELS + XP, the early game rebalanced |
| `f87bd8e` | Docs: `KinMapPlan.md`, the journey and levels in `KinJam.md`, four `party-sim` passes |
| *(this)* | **FAMILIES — the Grove + Ember slice** (see §3) |

## 3. Where things are

- **Families engine — `KinCore/Party/PartyFamilies.cs`**: `Family`; GROW, ROOTED Block, KINDLE; the
  passives as data components (Thornwall, Nursery, Mossback, Spores, Alpha, Stoker, EchoFirstSpell,
  Emberskin, KindleFinisher); the new card steps. Hooks live at turn start (`StartPartyTurnAction` →
  `PartyFamilies.TurnStart`), `HitAlly` (Rooted), Thorns and `AttackFoes` (`PartyState`),
  `SpellDamageTo`, card play (casts + Kindle), token summon and faint (`PartySummon`).
- **Levels — `PartyLevels.cs`**: stats = base × `0.6 + 0.08 × level`; **content numbers ARE Lv 5**.
  `PartyWorld.Tiers` is the one balancing table: foes per fight, the wild level range, and each
  town's LEADER level (set against the route BEFORE the town).
- **The journey — `PartyRoute.cs`, `PartyRun.Route.cs`, `PartyTown.cs`, `PartyRun.Town.cs`**;
  screens `KinRouteMap`, `KinTownMap`, `KinMapKit`, building screens in `KinPartyRunScreens.Map.cs`.
- **The look — `KinUiKit` (textures from `tools/make_ui.py`), `KinCardKit`, `KinRelayCreature`**.
  Art in `Art/sprites|cards|backdrops|buildings|ui`; the pipeline is the `match-mockup` skill.
- **Tests** — `PartyTests.Families.cs` (every Grove/Ember rule), `PartyRunTests.{Route,Town,Levels}.cs`.

## 4. Tools

```
dotnet run --project KinConsole -c Release -- party-sim 300        # per region: survival, leader level vs team
./Run-Godot.ps1 KinGame/kin_party.tscn -Capture shots/x -Seconds 1.8 -GameArgs '--scenario=4','--fight','--play=0'
./Run-Godot.ps1 KinGame/kin_party.tscn -Capture shots/x -Seconds 1.6 -GameArgs '--starter=0','--screen=route'
python tools/install_art.py sprite|card|backdrop|building gen.png "Name"   # then --headless --import
./Build-Apk.ps1 ; cloudflared tunnel --url http://localhost:8000 --no-autoupdate
```
All `--screen=` flags (route, route2, routefight, town2, gym, gymfight, hospital, shop, pen,
between, over) are in `Commands.md`. `Run-Godot.ps1` needs PowerShell 7 — Windows PowerShell 5
misreads its UTF-8 and fails to parse.

## 5. Scars worth not re-earning (this session)

1. **A leader's level must come from the route you reach it BY.** The first build used the route
   ahead (Lv 10 after Lv 2–4 foes) and its first turn wiped Shayne's team.
2. **Levels balance nothing without a sim column for the TEAM's level** — `party-sim` now prints the
   team's average level against each leader's. Read it before moving a tier.
3. **Leaders after the first never kill, even +3 levels** — their lines are 2–3 foes; late routes
   field 4–5. A content problem (leader exams), not a numbers one.
4. **Kindle changes every spell test after the first spell.** Two old tests were updated to show the
   Kindle in their arithmetic; expect the same when adding spell tests.
5. **`Deal` pads the deck with zero-Block cards named "Guard"** — name a test card anything else, or
   `InHand("Guard")` finds the padding.
6. **Name + level overflowed the creature view** — the level lives in the HP bar ("LV5 · 24/24").
7. **Python on this machine defaults to cp1252**: `read_text/write_text(encoding="utf-8")`, or an em
   dash becomes byte 0x97 and the file stops parsing. Long patches go in scratchpad FILES.
8. **ComfyUI draws designs badly from text; it REDRAWS well** — img2img at ~0.7 from a mockup crop or
   an old portrait keeps the design. Check every sprite faces RIGHT (`--flip`).
9. **Capture folders ship in the APK unless ignored** — `SQGodotCommon/shots/.gdignore` covers them.
10. **A fast (30s) export can still be right** — verify by searching the APK's `KinCore.dll` for a
    name added this session, not by the time it took.

## 6. Not built / open

- **Leader exams** (the Warden's spell-halving line for Ember; the Old Tusker already tests Grove's
  front) and a **Firebreak** foe that removes Kindle — after Shayne plays the slice.
- **Storm and Mire** as full families (kits, cards) — after the slice is judged.
- **Growth hooks**: training grounds (moves), held items, card upgrades — designed, not built.
- **The late game is too easy** (58% of bot runs won vs a 25% target): leader lines and late pools.
- The practice-scenario picker still says "Spellcraft" and "Summon" for the Ember and Grove showcases.
- Towns share one layout; the route backdrop has no crags; Bramble's medallion shows mostly shell.
- ChatGPT mockups for the town and route maps (prompts S19, S20 in `docs/mockups/mockup-prompts.md`).

## 7. Next

1. **Shayne plays the slice** — scenarios 4 and 6, then a run. Watch for: does an engine form? Does
   the big turn (Harvest, Flashpoint) land — or should it be dropped (it is a plain card, cheap to
   change)? Do rewards feel like "this changes my run"?
2. Act on that before the leader exams or the next two families.
