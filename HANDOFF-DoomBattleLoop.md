# Handoff — DOOMJAM: the battle loop, from attack-or-block to a race against a recurring apocalypse

> **SUPERSEDED (2026-09-15) by `HANDOFF-DoomFrontEndAndEffects.md`. Read that one first.**
>
> **§5 "What to do next" is DONE and will mislead you**: the Godot front end exists, scenarios are
> content now, and the tuning numbers in §7 predate two content rewrites.
>
> Kept for §4, whose scars are still true and still bite — deaths consumed by exactly one firing,
> the preview and the firing sharing one capture, a scenario in the wrong hook doing nothing, and
> the reinforcement telegraph being load-bearing rather than polish.


**Read this, then `DoomJam.md` in full.** That doc is the source of truth and it is CURRENT — this
handoff deliberately does not restate it. What follows is the session's shape, the things that will
bite you, and what to do next.

State at handoff: **DoomCore.Tests 54/54 green, working tree clean.** MTG projects untouched and
still off limits. **4 commits this session** — `git log --oneline -4` shows them; the last one is
everything in §6.

---

## 1. The headline

The battle went from *"survive a countdown"* to *"kill an Opponent while the world ends on a loop
around you."* Three structural changes, in order:

1. **Combat is five automatic lanes.** No targeting, no attack-or-block. You pick a lane when you
   play a card; that is the whole decision.
2. **There is an Opponent with HP, and killing it is how you win.** A lane you hold with no enemy in
   it hits them. The board is symmetric.
3. **The doom recurs.** Reaching zero fires the apocalypse and resets the clock. Only a death ends a
   battle.

**The doc's "one rule that must not bend" bent twice in one session.** Both versions and their
causes of death are recorded in `DoomJam.md` under "The doom is a clock you can outrun". Read that
before proposing anything that touches how a battle ends — the two dead rules are dead for reasons
you will otherwise rediscover.

## 2. The one thing to carry

**Toughness IS life, and it has survived every rewrite.** A unit absorbs up to its remaining
toughness and the excess hits the face behind it, so a body in a lane is worth exactly its toughness
in life. Every doom scenario trades on that one axis.

It survived attack-or-block → lanes, and lanes → the symmetric board. **It is the test any future
combat change has to pass.** If a change breaks that equivalence, the scenarios stop being
tradeable and the design loses its spine.

## 3. What exists now

| | |
|---|---|
| `DoomCore/Actions/EndTurnAction.cs` | the whole turn: lanes resolve, dead clear, Opponent reinforces, clock ticks, doom fires |
| `DoomCore/Enemies/Opponent.cs` | the thing you kill; also `PendingSummon`, the telegraphed reinforcement |
| `DoomCore/DoomFiring.cs` | **what one firing READ, in run ids.** `Capture` is shared by the real firing and the preview |
| `DoomCore/DoomScope.cs` | Battle vs Permanent — a fixed property of a scenario |
| `DoomCore/Run/DoomBattleEffects.cs` | the hook for battle-scope scenarios (Flood lives here) |
| `DoomCore/Run/DoomTransforms.cs` | the hook for permanent ones (Zombie, Nuclear), folded over firings |
| `DoomConsole/` | **the only way to play.** No Godot front end exists |

Scenarios today: **Flood** (battle scope — washes the board to Discard), **Zombie** and **Nuclear**
(permanent). **Rapture still throws** for want of a sacrifice mechanic, deliberately — an apocalypse
that silently did nothing would look exactly like one that worked.

## 4. Scars worth not re-earning

- **A recurring doom cannot be a single end-of-battle read.** `DoomFiring` exists because a Nuclear
  that fired twice would otherwise irradiate the final board twice and the earlier board never.
- **Deaths are CONSUMED by the firing that reads them.** `DiedRunCardIds` is cleared on each firing.
  A cumulative list pays Zombie for the same corpse on every later firing — invisible at countdown
  2, ruinous at countdown 1. Pinned by `ADeathIsPaidForByExactlyOneFiring`.
- **The preview and the real firing must share one capture.** Both call `DoomFiring.Capture`. Two
  copies drift and the player plans around a dial that no longer matches the apocalypse.
- **A scenario in the wrong hook does nothing and looks like it worked.** Both hooks throw when
  handed the other scope. Follow that precedent for every new scenario.
- **The reinforcement telegraph delay is load-bearing, not polish.** A lane that refilled the instant
  you cleared it makes the Opponent unreachable. `ALaneYouClearStaysOpenLongEnoughToShootThrough` is
  the test that will break first if someone "tidies" the ordering in `EndTurnAction`.
- **The console found a bug the tests could not.** `OpponentDamagedEvent` was raised and never
  rendered — the Opponent's health fell with nothing on screen explaining why, and every state
  assertion passed. **Play it, do not just test it.** Same class as MTG's blank card faces.
- **A unit that died once could never be replayed** (fixed 2026-09-14). `UnitComponent.Damage`
  survived death -> Discard -> reshuffle -> hand, so replaying the card put a unit with `IsDead`
  already true onto the Field. `UnitInLane` filters dead units, so it was invisible and inert — the
  card left your hand and the energy was spent for nothing. **The rule already existed**, written
  into Flood's board wash as "what returns from Discard is the card, not the body that stood in the
  lane" — but it lived at that ONE call site, so the death path never got it. Now cleared in
  `PlayCardAction`, the single point every board unit enters through. Pinned by
  `AUnitThatDiedEarlierComesBackWholeWhenItIsReplayed`.
- **Found by playing, not by testing — for the third time.** 54/54 passed with this live.
- **The Companion needed no work at all**, twice running. `Marked` is called in `Run.AfterBattle`,
  not per firing, so it stayed once-per-battle by construction. The run/battle split keeps paying.

## 5. What to do next

**In this order.**

1. **Scenarios.** This is the content and the whole point of scope. A battle-scope one is now cheap:
   an enum entry, a `ScopeOf` row, a `CountdownFor` row, a `PlayableOn` row, one case in
   `DoomBattleEffects`. Battle-scope scenarios are **exempt from "bargain, not tax"** — nothing
   carries forward, so they can be pure obstacles. That exemption is what makes them cheap.
2. **Tuning, by playtesting.** Deliberately not done. See §7 — dodging is currently unreachable, and
   that matters because dodging is now a design pillar.
3. **An enemy ABILITY system.** `Enemy` has none. The named want is *"cannot be killed while the
   countdown is running"*, which makes one specific battle a guaranteed apocalypse without
   retuning every other battle.
4. **The Godot front end.** `SQGodotCommon/DoomGame/` does not exist. `DoomCore` is plain C# with no
   Godot types, so all of the above can be done and tested with `dotnet test` and no editor.

**Do not** rebuild "make the dooms read lanes" or reinforcements-as-a-fix-for-dead-air. Both are
recorded in `DoomJam.md` as considered and made unnecessary by the recurring-doom model.

## 6. What landed in the final commit

Everything below is **committed**; the tree is clean. Grouped here because it arrived together and
the reasoning is easier to follow as one story than as four diffs.

- **Enemy refresh.** The Opponent announces a summon a turn ahead and lands it at end of turn, one
  every `SummonInterval` (2) turns, into the lowest free lane. Reinforcements scale on the TURN
  NUMBER, not the floor, so a stalled battle is not a safe one.
- **Apocalypses became dodgeable** — and **this needed no code.** `Run.AfterBattle` already returned
  early on `DoomsFired == 0`, and `EndTurnAction` already checked the Opponent's death before the
  doom. The change was the RULE, not the behaviour. See `DoomJam.md`; the previous two versions of
  that rule are recorded there with their causes of death.
- **One console fix**: the help had advertised `c` (show companion) since the companion shipped and
  it was **never wired**, so typing it printed "? for help". Found by checking the commands before
  documenting them here rather than trusting the help text.
- **`CLAUDE.md`'s source map was stale** — still listing `Assign` and
  `UnitComponent (Power/Toughness/Assignment)`, both deleted earlier in the session. Refreshed.

## 7. The measurement that matters

**Dodging is currently unreachable, so the newest rule has no teeth.** Floor 1, seed 42, a decent
aggressive line:

> **SUPERSEDED — RE-MEASURED 2026-09-15**, in the Godot front end, with the dead-unit-replay bug
> fixed and `SummonInterval` at 3. A greedy line (fill every free lane every turn), seed 42, floor 1:
>
> | turn | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
> |---|---|---|---|---|---|---|---|
> | Opponent | 26 | 23 | 20 | 19 | 16 | 10 | dead |
>
> Flood fired ONCE, on turn 5. **Dodging is now close but not free**: the Opponent was on 16 when
> the clock ran out, so a sharper line plausibly gets there — which is what the design wants, since
> dodging costs the transform and the companion mark.
>
> **The pressure is the real problem, not the clock.** Life fell 60 -> 57 across seven turns. Floor
> 1 barely threatens a player who simply fills lanes, so `EnemiesFor` and `OpponentHealthFor` want
> looking at well before `SummonInterval` does.
>
> The old table below is kept for its cause of death only.
>
> **THIS TABLE PREDATES THE DEAD-UNIT-REPLAY FIX (§4) AND IS SUSPECT.** It was measured while a
> replayed unit that had died once silently did nothing — a card and an energy vanishing per
> occurrence, which on a 10-card deck starts around turn 3. The board was emptier than the player's
> plays deserved, so this reads as *harder* than the game actually is. `SummonInterval` was raised
> 2 -> 3 on the strength of it. **Re-measure before trusting either number.**

| turn | Opponent HP |
|---|---|
| 1 | 26 |
| 2 | 21 |
| 3 | 16 |
| 4 | 13 |
| 5 — first firing | **10** |

The limit is not damage, it is **lanes**: the Opponent's summons close your damage lanes faster than
you can open them. A 7hp Wretch takes a 2-power Scavenger four turns to kill; a Revenant arrives
every two.

The two dials are `StarterContent.OpponentHealthFor` (`20 + floor*6`) and `Opponent.SummonInterval`
(2). **`SummonInterval` matters more than it looks** — at 1 it exactly matches a player killing one
unit a turn and the board never opens at all.

Dodging being *hard* is correct: it skips the transform AND the companion mark, so it trades power
for safety. It needs to be **possible** for that choice to exist.

## 8. How to reproduce anything here

```
dotnet test DoomCore.Tests                                  # 54/54
dotnet run --project DoomConsole -c Debug 42                # seed 42, floor 1 is Flood
```

Console: `p <cardId> <lane>` to play, `e` to end turn, `d` deck, `c` companion, `?` help, `q` quit.
The seed is printed at startup and replays a run exactly — pass it as the first argument.

Piping works for scripted checks:

```
printf 'p 9 1\np 11 3\ne\ne\ne\ne\ne\nq\n' | dotnet run --project DoomConsole -c Debug 42
```

**Card ids change every turn** (the hand is redrawn), so a long piped script will start printing
`can't: Card is not in hand` once it drifts. That is the script being wrong, not the game.
