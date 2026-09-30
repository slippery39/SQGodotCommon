# Handoff — one family per run, bosses and elites, and ROUND 4 (the simpler game)

**Read this, then `KinFamiliesPlan.md` from the top (rounds 4, 3 and 2 — newest first), then the top
of `KinJam.md`.** `HANDOFF-KinFamilies.md` is the session before this one: read it only for its
scars; its game (catching, the relay, levels, KIN) is gone.

## State at handoff (2026-09-28)

- Branch **`kin-pivot`**, nothing pushed. **313 KinCore tests green** (after Ember); the solution and the Godot project
  build. `SQGodotCommon/project.godot` is still left uncommitted on purpose (a headless import strips
  two comment lines).
- **The rules of round 4 are built on PLACEHOLDER content.** Next is the family drafts.
- **EMBER DRAFT 2 IS BUILT** (later the same day): `PartyEmber` (Burn, Spell Power for the turn or
  the fight, auras, chains, energy, the passives), `EmberCards` (29 cards, 4 boss monsters), the UI
  (SPELL POWER and auras over the energy orb; BURN n on a foe; the tick floats), 24 tests in
  `PartyTests.Ember.cs`. The as-built notes are under the Ember draft in `KinFamiliesPlan.md`.
- **GROVE DRAFT 1 IS BUILT** (2026-09-29): `PartyGrove` (GROW as +Power for the fight, Thorns for
  the turn or the fight, Grove Block, Graft, Harvest, the auras, the passives), `GroveCards` (4 tokens,
  29 cards, 4 boss monsters), tokens as `Ally.IsToken` apart from fading, 29 tests in
  `PartyTests.Grove.cs` (333 green). As-built notes under the Grove draft in `KinFamiliesPlan.md`.

## 1. The game now, in one paragraph

A deckbuilder whose monsters guide the deck. **Pick a starter — it IS your family** (Bramble = Grove,
Pike = Ember). Five regions, each **town → route → BOSS** (named from the town, a 30% spring before
it). Routes show **1–2 ELITES**. **Monsters act ONLY through cards**: an attack card played on a
monster strikes, and **the first attack on each monster each turn fires its FIRST-ATTACK bonus**.
Monsters have HP, Power and **Spell Power (a team total, added to every spell)**. **You start with one
monster; the first two bosses each offer three of your family — pick one.** No catching, no bench, no
levels or XP. A knockout is back at 1 HP. Rewards and the shop offer only your family and colourless,
weighted by rarity. A boss pays a boss relic (no drawbacks), a full heal and gold; an elite a relic and
a rare-led card. **Springs heal 30% or upgrade a card to its + version.**

## 2. This session's commits (oldest first)

| Commit | What |
|---|---|
| `a0850b7` | Fading tokens no longer call the bench; KIN; family colours; live spell numbers; engine pop-ups |
| `580d08b` | **One family per run** (rewards, catching, starting decks) |
| `aee59f2` | **The run's shape**: 5 regions, boss at the route's end, elites, relics |
| `43bd92e` | **Bosses and elites** for regions 1–2 (`PartyExams`, `PartyBosses`: wind-up, pull, shell, enrage, phases, minions); boss relics |
| `34a4d44`, `395a0d6` | Tuning toward a 50% bot; exams toughened by HP only (hits stay answerable); Pike 24 HP |
| `226fb01` | **Round 4, step 1**: no catching/bench/levels; monsters from bosses; knockout → 1 HP |
| `9aac1ba` | **Round 4, step 2**: monsters act only through cards; `FirstAttack`; Spell Power; Kindle not automatic |
| `0c973c9` | **Round 4, step 3**: springs heal or upgrade; `KinCard.Upgraded` |
| *(this)* | Round 4, step 4: flat tiers; docs |

## 3. Where things are

- **Monsters** — `PartyMonsters.cs` (`FirstAttack`, `Attacks`); placeholder kits in `PartyContent`
  (`Bramble`, `Pike`, `MonstersOf(Family)` with `Joins(...)`).
- **Exams** — `PartyExams.cs` (the 8 bosses/elites), `PartyBosses.cs` (their mechanics).
- **Run** — `PartyRun.cs` (boss prizes: `RelicChoice`, `MonsterChoice`), `PartyRun.Route.cs` (springs).
- **Relics** — `PartyRelics.cs` (6 elite relics, 4 boss relics).
- **Sim** — `party-sim N`, `party-sim variants N` (starter vs family), `party-sim trace <seed>`.
- **Capture flags** — `--screen=boss|relics|monsters|spring` (`Commands.md`).

## 4. Scars worth not re-earning

1. **A multiplier on HITS can make a telegraphed blow unanswerable.** ×1.3 made the Tusker's Gore 29 vs
   a 19-HP Pike; no Guard saves that. Toughen exams through HP; keep hits at their level.
2. **Levels are a weak lever** (~7% a level). Two levels above the team barely moved the sim.
3. **Check what the bot can't play before blaming it.** "The bot misplays Ember" was wrong: the trace
   showed the bot playing Ember sensibly into an unwinnable blow. `party-sim variants` swaps one thing
   at a time — it found the FAMILY, not the starter, was ~60 points.
4. **Catch counts follow survival, not the reverse**: Ember caught half as much because its runs ended
   sooner (per meeting, rates were equal).
5. **Python here writes CRLF on Windows and a heredoc can mangle `\b` into a backspace.** Edit files as
   bytes, keep each file's own line endings (some are LF, some CRLF), and put long patches in scratchpad
   scripts. CSharpier reformats on commit — anchors from before a commit may no longer match.
6. **A capture flag must drive the real flow.** A flag that called a screen directly showed the town map
   instead (it draws over the run screens); `DebugEndBattle` + `BattleOver` is the honest path.

## 5. Next

1. **The family drafts** (`KinFamiliesPlan.md`, round 4): Ember and Grove are BUILT; next are the
   **3 colourless monsters** (and their cards), interview-led.
   **Playtest 2026-09-30**: Grove walled a whole act (Rooted now lasts one extra turn; THORNWALL
   gone); Spell Surge now costs 3, rare. Both families still too easy — a foe difficulty pass next.
2. Then re-tune with `party-sim` per region and per starter; the 50% target and even losses are in
   `PartySim.Target`.
3. Regions 3–5 still reuse region 2's bosses and elites; their exams are undesigned.
