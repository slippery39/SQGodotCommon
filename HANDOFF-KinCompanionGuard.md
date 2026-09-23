# Handoff — every card a decision, the companion as the thing you protect

**Read this, then the top of `KinJam.md` (the design philosophy), then the "EXPLORING or TUNING"
block in the root `CLAUDE.md`.** `HANDOFF-KinPivot.md` is the previous session and is superseded —
read only its §5 scars.

State at handoff (2026-09-23): **166 tests green.** Solution builds, the Godot project builds and
plays. Branch `kin-pivot`, **nothing pushed**. Ten commits this session (below), plus **one round of
playtest fixes still uncommitted** — see §6.

---

## 0. Three standing decisions that frame everything below

1. **THE DESIGN PHILOSOPHY — Shayne's, top of `KinJam.md`, and it kept being misconstrued before it
   was written this concretely.** Every card and every enemy should create a decision. Each
   companion IS an archetype. Enemies must ask what is in your deck (a victim and an answer, never a
   flat tax). Bosses are exams. Vanilla cards are honest filler only.
2. **EXPLORING, NOT TUNING — root `CLAUDE.md`.** In exploration: design, build, and write tests that
   prove a mechanic FIRES. **No sims** — every design change voids the last one; broken numbers are
   fine until Shayne calls a tuning pass. Judge by principle instead, especially the stall test:
   *does a longer battle pay this ability more?* If yes, it is a stall engine. **Repeatable healing
   fails it and is cut.**
3. **COMBAT STAYS LANE-BASED — to keep the project short.** Wildfrost's leader-on-board rows, Darkest
   Dungeon's ranks and a companion duel were researched and set aside. The goal is to make the
   companion interesting INSIDE lanes.

Also recorded in `KinJam.md`: **the Opponent never shares the player's capabilities and telegraphs
every move** (the Slay the Spire intent model), and **where an enemy moves is authored per enemy,
never automatic**.

---

## 1. What this session did, in commit order

| Commit | What |
|---|---|
| `aae65d1` | Deleted dead mechanics: `KinBot`'s doom deck-term (a constant — verified by an identical `sim 30`), `Tags`/`HasTag` (zero writers), stale doom comments; pruned dead cards from `KinJam.md`'s card pass |
| `b8eb2db` | Measured Echo: **not** the power curve (A/B inside noise); first test proving Echo fires at all |
| `12f6f88` | Setting settled as a **generic fantasy substrate**; the three frames kept as candidate themes |
| `6898e7a` | **Thorns** and **Strikes** |
| `4d9477d` | The design philosophy |
| `de7123c` | **The Bulwark/Face vertical slice**: Breakthrough, Flier, buffs that grant Thorns/Strikes; 9 archetype cards (`StarterContent.Archetypes.cs`); Harpy / Razorback / Flail Knight as the counter triangle; companion starters (7 generic + 3 archetype); act card pools folded into one; a **trait strip** on the lane (FLIER, THORNS 6) and `6×2` intents; `sim N companion=<Name>`; capture flags `--companion`, `--floor`, `--seed` |
| `91c4120` | **Bramble reworked**: "end of turn: the Opponent takes what your units absorbed" (was a repeatable heal). Flail Knight to floor 7 so it stops leading floor 6 of every act |
| `aae14cb` | The exploring-vs-tuning rule |
| `a727808` | **Healing cut** (Reactor Crew, Almoner, Gallows Feast's life; Field Dressing 12 → 6 as the baseline); **Echo and Warding removed**; Barbed → 3 to every enemy; Ash's text says when; **every 2-drop bumped** as an experiment |
| `69e5082` | **The companion as the thing you protect** — see §2 |

The six commits from `4d9477d` on were split out of one working tree; see scar 1 for how.

---

## 2. How the companion works now

- **Your life IS the companion's health.** The 120 stays as the number, in the status strip. The
  companion can no longer die on its own.
- **GUARD** = the companion's Toughness, **refreshed at the start of every turn**. The attack in the
  companion's OWN lane hits Guard first; the rest reaches your life. **Open lanes and Fliers go
  straight to your life.** Effects that hit "every unit you hold" (Corrosive, Blighted, The Choir) now
  spend Guard, then life — so those traits, which measured nearly free, now bite.
- **Guard cards stack for one turn**, like block. One placeholder: **Brace** (1 energy, +8 Guard).
- **One free move a turn** into a lane you do not hold — click an empty lane in your row.
- **On screen:** a **shield icon + number** on the companion's cell; a red **`−N`** beside the life pill
  is what ending the turn now would cost (`LifeLostIfTurnEndsNow`, a real end of turn played on a
  copy); a hit that reaches your life flashes the companion; Guard soaked floats from the shield.

Why this model: "every open lane hits the companion" makes position meaningless (same damage
anywhere), and "it only takes damage in its own lane" makes every other enemy harmless. Guard +
open-lanes-hit-life makes each turn ask *which attack does my companion eat, or does it step into an
open lane to hit the Opponent and let another lane through?*

`KinBot` considers a move as the FIRST action of a turn only (ponytail-noted).

---

## 3. What was measured, and what it means now

Both runs predate later changes — **quote them as history, not as the current game.**

- **Run 26** (`docs/findings/kin-balance.md`): **Bramble won 62% of runs on her own** — the sim had only
  ever played Ash, so nobody had seen it. Pike's archetype starter lifted act 1 from 64% → 82%.
- **Run 27**: **stats cannot make a 2-drop good.** Vanilla 2-drops at 36 / 40 / 44 total stats all sat
  below an average vanilla 1-drop, with no measurable gain from 36 to 44. A lane is worth at most what
  is in it — power past the enemy's health and toughness past the attack are wasted. **What works is
  REACH** (Drone Swarm, 6 to every enemy, was the best 2-drop). And **the heals were act 2's power
  curve**: without them act 2 cleared at 2–4% for four of five companions.
- **Card power relative to the average card**, computed from existing sims: **healing topped the
  table** (Reactor Crew, Almoner, Field Dressing, the Warding upgrade) — Shayne's principle, in data.
  Bosses were beaten by 89–100% of the runs that reached them: runs die to attrition, not exams.
  Traits that hit your units were nearly free; traits that hit your life were the expensive ones.

**The game is currently close to unwinnable past act 1.** That is known and is a tuning job. The
levers are enemy damage, the life budget and the act-break heal — **not** bringing repeatable healing
back.

---

## 4. Research done this session, and what it pointed at

- **Lane games** (Inscryption, Monster Train, PvZ Heroes, TES Legends, Wildfrost): every one has a way
  to spend overkill — carry it on (Inscryption's queued card, Trample) or spread it (Sweep, Splash,
  Barrage). Monster Train prices big units in floor CAPACITY, not just cost — the origin of the
  multi-lane-unit idea. Legends gives a lane its own rule — the origin of the lane-rules idea.
  Wildfrost makes spread damage trigger every reaction in a row — a counter for reach.
- **Companion games**: **Monster Train's champion forge** (3 paths × 3 tiers, 2 offered, early picks
  lock in) is the cure for the measured "upgrade pick is a formality" problem. **StS2's Osty** is fed
  by Summon cards in the deck. Wildfrost's leader is the life total on the board. Every one attaches a
  stake to the companion's death.
- **Note:** Shayne believed every unit already carries excess power to the Opponent. **It does not** —
  only Breakthrough does (`EndTurnAction`). Universal trample is the cheapest overkill fix on the
  table, if it is ever wanted.

---

## 5. Scars worth not re-earning

1. **The pre-commit hook `git add`s each staged file's WORKING-TREE copy** after CSharpier, so partial
   staging is silently undone at commit time. To split a mixed tree: rebuild each commit's version of
   every file in a scratch `git worktree`, commit there with the hook running, verify byte-identity,
   then `git reset --mixed` the real branch onto it (a fast-forward; the working tree is untouched).
2. **Nothing on the board may catch the mouse.** `KinBoard` sets the whole board to `Ignore`. The move
   first shipped behind that and could never fire; the fix set the lane cells to `Stop`, and **card
   hover is physics picking, which any mouse-catching Control silently blocks** — the reward cards sit
   over the lane row, so their top halves stopped hovering. Lane clicks are now read in
   `KinBoard._UnhandledInput` with `LaneAt`, exactly like a card drop.
3. **`--move` skipped the click and "verified" a move no player could make.** Use **`--click-lane`**
   (a real click through the viewport) for anything clicked, and **`--catchers-at`** (lists every
   mouse-catching Control at a point) for hover. **A synthetic mouse move does not drive physics
   picking** — a probe built on one reported "nothing hovered" everywhere.
4. **Godot run from the command line uses the prebuilt assemblies** — build `SQGodotCommon.csproj`
   first, or a new flag silently does nothing and looks broken.
5. **Units withdraw inside `EndTurnAction`**, so a test cannot read a unit's damage after the turn —
   assert the spill (`PlayerDamagedEvent`) or a death event instead.
6. **The default Opponent reinforces an empty lane on turn 2**, which then hits you through it. A test
   that measures life across several turns needs an idle Opponent (`GuardAndMoveTests.Idle`).
7. **A seed note goes stale whenever the enemy roster changes.** Seed 36 / floor 6 stopped fielding the
   counter triangle when the Flail Knight moved; it is **seed 53 / floor 9** now. Re-find, never trust.
8. **A badge that clips shows a WRONG number.** "GUARD 16" in a 175px cell rendered as "GUARD 1". The
   fix was an icon, not a smaller font.
9. **A relabel does not exist until the screen shows it.** "Your life is the companion's health" felt
   "exactly the same" in play because nothing on screen changed. **And moving life onto the
   companion's cell made it harder to read** — reverted; life lives in the status strip.
10. **The card-power table is zero-centred by construction** (the average card is subtracted), so half
    of all cards read negative. And **the sim's reward picker is uniform-random**: archetype payoffs
    (Blademaster, Hedge Witch) are measured in decks without their partners, so the sim cannot judge
    them at all.
11. **"incoming" is already a glossary keyword** (a telegraphed summon) — which is why the damage
    forecast is shown as a bare `−N`.
12. **`godot --headless --import` rewrites `project.godot` and strips comments** — it deleted the
    authored note on `animation_speed`. Check `git diff SQGodotCommon/project.godot` after any import.
13. **Bash heredocs feeding Python mangle backslashes** — `"\n"` in a C# string arrived as a real line
    break. Build backslashes with `chr(92)` or use the Edit tool.

---

## 6. Uncommitted — the playtest round, ready to commit

All from Shayne's playtest ("I couldn't move my companion", "it felt exactly the same", reward
hover "inconsistent"):

- The move click fix and the hover fix (scars 2–3), with `--click-lane` and `--catchers-at`.
- `GuardSoakedEvent`; `LifeLostIfTurnEndsNow` and the red `−N` beside the life pill.
- The Guard shield icon (`Art/icons/shield.svg`, **drawn in-house**, so nothing is added to
  `CREDITS.md`), replacing the red disc and then the clipped word.
- Three tests: the forecast equals what ending the turn costs, the forecast changes when the companion
  moves, a struck Guard raises its event.
- `IsShowing` on the intermission and the shop; `KinCardTap.cs.uid` (was missing from the repo).
- Docs: `KinUI.md`, `Commands.md`.

**Unverified by capture, and worth checking in play:** dragging a card onto a lane (the drag reads
press and release in `_Input`, ahead of every Control, so it should be unaffected).

---

## 7. What I would do next

1. **Play the current build.** The move and Guard were untestable in the last playtest — the move was
   broken. The question: *does choosing where the companion stands each turn feel like a decision,
   now that the `−N` shows what it saves?* If not, the next steps below change.
2. **Rival companions as Opponents** — the Opponent becomes a rival handler's companion in the enemy
   line, with its own ability giving each fight its identity. Movement is a **telegraphed, authored
   intent** — which makes the unbuilt intent-patterns phase in `KinV3Plan.md` the place it lands.
3. **Give the companion a growth path worth choosing** — Monster Train's forge: per-companion upgrade
   paths that lock in. The shared pool is now five items with no rare, so the pick is more of a
   formality than ever.
4. **The Guard card family** (Brace is a placeholder), and **cards that feed the companion** (Osty).
5. **Redesign the 2-drops around reach**, per run 27.
6. **The other three archetypes**: Loss (Ash), Volume (Tally), Formation (Moss). Moss's "either side of
   it" ability only makes sense now that the companion can move — revisit it with her.
7. **Smaller, known, and left:**
   - Rewards are not weighted toward the companion's archetype; the starter carries it for now.
   - Effect-text enemies ("on death: the Opponent heals 6") and the Opponent's own trait are still
     invisible on the board — they want an enemy hover; the inspector follows only the hand.
   - Ideas parked for later: **lanes with rules** (a natural way to make bosses exams), **units that
     hold two lanes**, **universal trample**.
   - The new cards and enemies fall back to generated figures (no art yet). Names are generic fantasy;
     the older pool still carries the grimy names.
8. **A tuning pass, only when Shayne calls it** — act 2's collapse without the heals, and Bramble.
