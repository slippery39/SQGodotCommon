# Handoff — DOOMJAM: the game got a face, and then behaviour became data

**Read this, then `KinJam.md` in full, then `KinUI.md` before touching the front end.** Those two
are CURRENT and this deliberately does not restate them. What follows is the session's shape, the
things that will bite you, and what to do next.

State at handoff: **KinCore.Tests 75/75 green, working tree clean, 24 commits this session**
(`git log --oneline 768b753..HEAD`). MTG projects untouched and still off limits.

---

## 1. The headline

The previous session handed over a rules engine you could only play in a terminal. This one gave it
a face and then pulled its content out of the code.

1. **`SQGodotCommon/KinGame/` exists and a full run is playable** — menu → battle → drag a card
   into a lane → end turn → floor cleared → pick a reward → descend.
2. **Effects exist, ported from MtgCore.** Cards that are not units, enemies that do something when
   they die, Opponents that heal themselves.
3. **Enemies, Opponents and apocalypses are CONTENT.** Stat formulas are gone; behaviour is data.

## 2. The one thing to carry

**The run/battle split has now paid off five times, and this session found its price.**

Effects are `GameAction`s over `GameState`. The run deck lives OUTSIDE `GameState` — deliberately —
so a battle apocalypse can be data and a **permanent one cannot**. It stays a
`(run, firing) -> run` function in `KinTransforms`.

That is not a gap to close. It is the same boundary that made the Companion free, made rewriting
combat touch nothing above `KinCore/Actions/`, and lets a doom rewrite a deck the battle never
sees. **Before proposing that effects reach the run, read the engine findings in `KinJam.md`.**

## 3. What exists now

| | |
|---|---|
| `SQGodotCommon/KinGame/KinBoard.cs` | the battle screen; reads state, decides nothing |
| `KinGame/KinLaneCell.cs` | one lane: silhouette + attack/life pips |
| `KinGame/KinHandView.cs` | the fan on `Common/Cards/2D`; drag → `PlayCardAction` |
| `KinGame/KinIntermission.cs` | floor cleared, deck diff, reward choice, next apocalypse |
| `KinGame/KinArt.cs` | generated flat art — no sprite files anywhere |
| `KinCore/Effects/` | `EffectAction`, `KinEffect`, `KinTargeting`, resolver, 5 effects |
| `KinCore/Content/EnemyLibrary.cs` | 6 enemies, 3 Opponents, with effects |
| `KinCore/Content/ScenarioLibrary.cs` | 5 apocalypses; battle-scope ones are pure data |

**The whole effect system is ~250 lines against MtgCore's ~1600**, and the saving came from the
design having deleted targeting, not from writing less.

## 4. Scars worth not re-earning

**Godot, and they cost real time:**

- **GUI picking runs BEFORE physics picking.** Any `Control` with `MouseFilter.Stop` under the
  cursor swallows the click, so a card's `Area2D` never fires `mouse_entered`, so `CardUIManager`
  never learns it is hoverable, so **the drag never starts**. A full-screen `ColorRect` did this to
  the whole board. `KinBoard.MakeTransparentToMouse` sweeps every Control to `Ignore` except
  Buttons — **delete that sweep and cards stop being draggable, with no error anywhere.**
- **`CustomMinimumSize` is a MINIMUM.** A label wider than its slot drags the whole row out of line.
  The companion's card is named `Companion.FullName`, which grows with every apocalypse survived, so
  this got worse the further a run went. Lane names clip.
- **`Render` runs inside `_Ready`, before the container layout pass.** `GetGlobalRect()` returns
  `(0,0)` there. Lane hit-testing depends on those rects; measure after layout or every card lands
  in lane 0.
- **Screenshots: `godot-mono --write-movie shots/x.png` needs no code** and works remotely. **Take a
  LATE frame** — 39, not 13. Cards tween in from x=0 and an early frame looks like a layout bug.
- **Headless prints shader-compiler errors about `custom_samplers`.** That is the dummy renderer
  failing on the card outline shader. Noise. The run still exits 0.
- **`FindChildren` matches ENGINE class names, not C# script types.** It returns zero `CardUI2D`.
- **Physics picking does not appear to run off `Input.ParseInputEvent` under `--write-movie`**, so
  synthetic mouse events prove nothing about hovering.

**Rules and effects:**

- **A dead enemy is MARKED `Mourned`, not removed.** That is why `OnDeath` effects can still resolve
  "self" and "my lane". If anyone makes death remove the object, death effects break silently.
- **The spawn queue is FIFO**, and trigger ordering relies on it: end-of-turn effects are queued
  before `ResolveDoomAction`, doom reactions after it.
- **Battle apocalypses EXECUTE inline, not spawned.** `KinPreviewer` calls into the same hook and
  diffs the board; a spawned action would not have run and the preview would say nothing happens.
  This is the one deliberate departure from how effects run everywhere else.
- **An inert card is refused.** No body and no effect means it would cost energy, leave your hand
  and change nothing — indistinguishable from a card that worked.
- **`KinBattleFactory.Create` adds NO companion.** Only `Run.StartBattle` does. A test that builds
  a board from the factory and asserts about the companion is testing nothing.

**Process:**

- **CSharpier reformats on commit**, so string-matching edits written against a file you read before
  the last commit will silently fail to match. Check the file, do not assume.
- **Two tests this session failed while the code was right** (`Is.SubsetOf` counts a repeat as an
  extra item; the factory has no companion). Read the failure before "fixing" working code.

## 5. What to do next

**In this order.**

1. **PLAY IT AND TUNE. Three difficulty changes landed together and NONE are measured.** Ashfall
   from floor 2, Heralds that cost 4 life when they die from floor 3, and The Choir healing 2 a turn
   from floor 4 — on a curve the player had already found too steep at floor 3. Card rewards were
   added to answer that and have also never been measured. **Change one lever at a time.**
2. **More content, now that it is cheap.** A battle apocalypse is one enum entry plus one
   `ScenarioLibrary` entry. An enemy is one `EnemyLibrary` entry. A rite is one `RewardPool` entry.
   None of it needs engine work.
3. **Reward tiers.** The pool is FLAT — floor 10 offers what floor 1 does. `MinFloor` already exists
   on enemies and scenarios; the same idea on `RunCard` is the obvious next step.
4. **The map, relics, choosing your doom.** All still unbuilt. `KinJam.md` describes them.

**Do not** reach for MtgCore's targeting. There is no targeting in this game and `KinTargeting`
already covers every rule used. Do not make permanent transforms into effects — see §2.

## 6. The measurement that matters

**Last real numbers, and they are already stale.** Greedy line, seed 42, floor 1, before the content
changes in commits `3569953` and `878bef4`:

| turn | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| Opponent | 26 | 23 | 20 | 19 | 16 | 10 | dead |

Flood fired once, on turn 5. **Dodging is close but not free**, which is what the design wants.

**The real finding was the pressure, not the clock:** life fell 60 → 57 across seven turns. Floor 1
barely threatens a player who fills lanes. Floors 2+ have changed since; re-measure before trusting
any of this.

A player reached floor 3 and died, which is what prompted rewards.

## 7. How to reproduce anything here

```
dotnet test KinCore.Tests                                    # 75/75
dotnet run --project KinConsole -c Debug 42                  # terminal, seed 42
godot-mono --path SQGodotCommon                               # the game, from the menu
godot-mono --path SQGodotCommon KinGame/kin_board.tscn      # straight into a battle
```

Screenshot the running game, no code needed, works from a remote session:

```
godot-mono --path SQGodotCommon --write-movie shots/x.png --fixed-fps 10 --quit-after 40 \
  KinGame/kin_board.tscn
```

In game: drag a card onto a lane, **F3** toggles the debug log. `Commands.md` has the traps.
