# Handoff — DOOMJAM: the game got played, and playing it found what measuring it could not

> **SUPERSEDED by `HANDOFF-KinVisualPass.md` (2026-09-17).** Read that first. This is kept for its
> §4 scars and its §7 measurements, which are still the current balance numbers — but §5 "what to do
> next" and §6 "committing this" are both done and are history now.

**Read this, then `KinJam.md`, then `docs/findings/doom-balance.md` runs 9-14.** That findings file
is the evidence for every number below. `HANDOFF-KinBalanceAndThemes.md` is the previous session
and is superseded — read only its §4 scars.

State at handoff: **KinCore.Tests 105/105 green, Godot project builds, MTG projects untouched.**

**EVERYTHING IS UNCOMMITTED.** 18 modified files, plus `SQGodotCommon/KinGame/KinThemeSelect.cs`
and its `.uid`. See §6 for how to split it.

---

## 1. The headline

1. **All three acts are reachable in the game.** `KinThemeSelect` picks the act at run start and
   lays out its whole schedule. Before this the front end always got the `LongEmergency` default and
   two of three acts existed only inside the simulator.
2. **Rewards have RARITY, not floor gates.** A rare can turn up on floor 1 — deliberately.
3. **Battles went 7.9 turns to 4.8, and the DODGE came back: 6.4% → 22.4% of battles.**
4. **The doom clock scales with the act** instead of being flat.
5. **The UI is legible.** Card rules text was rendering at 8 real pixels.
6. **A hand-play found the biggest problem of the session, after ten sim runs missed it.**

## 2. The one thing to carry

**The harness measures what you ask it. It does not tell you the game is bad.**

`turns per battle 7.9` was printed in every sim run for ten runs. It was read as a statistic. Then
the game got played by hand for twenty minutes and the first sentence back was "the battles are way
too long" — and the number had been sitting in the output the whole time.

The same failure, in its sharper form: **tune a mechanic against the metric that mechanic is FOR,
not against the headline number.** Cutting every doom clock to 2 moved completion by four points
(invisible) and took dodging from 24.3% to 9.7% — deleting one of the three outcomes `KinJam.md`
promises. Completion is nearly blind to the doom clock. `dooms dodged` has been printed all along.

> **Play the game after a pacing change. Every time.** The bot cannot tell you a battle is a chore.

## 3. What exists now

| | |
|---|---|
| `SQGodotCommon/KinGame/KinThemeSelect.cs` | run start: pick the act, its whole schedule shown, seed displayed |
| `KinCore/Content/ThemeLibrary.BandsOf` | the act as floor bands — what the picker renders |
| `KinCore/Run/RunCard.Rarity` + `StarterContent.WeightOf` | Common/Uncommon/Rare, weighted 6/3/1 |
| `StarterContent.StartingLife` | the life budget, 120, finally a named constant |
| `KinPalette.Caps` | `AiUprising` -> "AI UPRISING", used everywhere a scenario is named |
| `StarterContent.EachTurn` | the `OnTurnEnd` effect helper — read its doc before using it |

## 4. Scars worth not re-earning

**Design:**

- **Rarity is a WEIGHT, never a floor gate.** Floor-gating was built, measured and rejected
  (findings run 9). An early rare you build a run around is where a memorable run comes from, and
  gating trades those away for a tidier curve. `ARareCanBeOfferedOnTheFirstFloor` holds the
  decision; it is a design test, not a precaution.
- **Gating CONCENTRATES rather than delays.** Tiering by cost made the game far EASIER (35/43/29%)
  because a smaller early bag offers its best card more often. Flat-pool dilution had been doing
  unaccounted balance work.
- **`OnTurnEnd` only pays if the unit is still standing at end of turn.** A repeating trigger was
  costed at 40-50 firings a run and delivered a fraction of that on 1-cost bodies. **Power pays for
  a body that fights; toughness pays for a body that HOSTS a repeating effect** — Almoner is a 2/8
  and the toughness is the engine. Both rules are true, for different cards.
- **The doom clock must scale with the band.** Flat is what forced the frequency-vs-dodge trade.

**Measurement:**

- **n=300 cannot RANK the acts** — it read them in exactly the reverse order of n=900. Before/after
  diffs on one sample survive; rankings do not.
- **A themed-card change replays the other acts IDENTICALLY, to the decimal.** That is a free
  regression control: any drift in an act that does not hold the card is a LEAK, not noise. Does not
  apply to shared-pool cards or life/health changes, which resample everything.
- **Rarity breaks the card-value table across tiers.** A rare is only taken by runs that got far, so
  its delta is mostly reach. Compare within a rarity, or not at all.
- **Cutting the life budget cuts the per-battle bill too.** `mean floor = life / life per battle`
  still holds, but the denominator is not independent — fewer, shorter battles means fewer dooms,
  and dooms do damage. The prediction was mean floor 15; it came out 17.9.

**Process:**

- **One lever at a time, and I broke it twice in one session.** Both failed Rising cards had statline
  AND trigger changed together, costing a full 900-run measurement to work out which. The only
  reason that experiment produced anything is that The Choirmaster was an accidental control —
  stats untouched, effect added.
- **A replacement can invalidate the NEXT replacement in the same script.** Renaming `Seed` to
  `_seed` made the following match fail. Assert on every replacement; the assert caught it.
- **Tests define cards INLINE.** A mechanism test (`AUnitsDeathTriggerFiresAndHitsTheOpponent`)
  pulled Gravedigger out of the reward pool and broke when that card's design changed — the exact
  failure the inline rule exists to prevent, caught in the act.
- **Do not drive the desktop with synthetic clicks.** A screenshot attempt clicked into the user's
  running MTG Arena window because Godot opened behind it. Launch and screenshot is fine; clicking
  blind is not.

## 5. What to do next

**In this order.**

1. **PLAY IT.** The pacing has changed more than anything else this session and only the bot has
   experienced it. The last play-test found what ten sim runs missed.
2. **The stalemate tail MOVED, it did not go away.** Capping enemies at 4 fixed the floors that
   fielded five — floor 18's worst battle went 14 turns to 11. The worst battles are now **50 turns
   on floor 6 and 49 on floor 10**, floors with two or three enemies where open lanes were never the
   constraint. Prime suspect is stacked healing: `The Choir` 2/turn, `Gravecaller` 4/turn, `Zealous`
   4/turn, against roughly 12 damage a turn through two open lanes. **The mean is healthy and only
   the worst case is broken**, so the fix is likely a cap on stacked healing rather than a change to
   any one number.
3. **The Rising is still the low act, 20.7%.** Its cards pay once. **Read findings run 12 before
   redesigning them** — the obvious fix was tried and made it worse.
4. **The UI changes are UNVERIFIED by a human.** Sizes were chosen by arithmetic against the window
   scale, not by looking. Check the hand and the lane pips before trusting them.
5. **`Rapture` is still unimplemented** and gated to floor 99 so it is never offered. Ship it or cut
   it.
6. **Elites, salvage, events, multi-act** — all still unbuilt. See the previous handoff's §5 for the
   plan; `KinIntermission` should be generalised ONCE, when events need it.

## 6. Committing this

It is one working tree holding three unrelated changes. Suggested split:

1. **The theme picker and the UI sizing** — `KinThemeSelect.cs`, `KinBoard`, `KinHandView`,
   `KinLaneCell`, `KinPalette`, `KinIntermission`, `project.godot`, `ThemeLibrary.BandsOf`,
   `ThemeTests`, `KinUI.md`, `CLAUDE.md`.
2. **Reward rarity** — `RunCard`, `StarterContent` (rarity + weights), `ContentCommand`,
   `ThemedCardTests`.
3. **The pacing rebalance** — `EnemyLibrary` (health), `ScenarioLibrary` (clocks), `StarterContent`
   (`StartingLife`, enemy cap, the Rising card revert).

`StarterContent.cs` is touched by all three, so they cannot be split by file alone.

## 7. The measurement that matters

`sim 900`, 300 runs per act, 363s:

| act | completions | mean floor | life/battle |
|---|---|---|---|
| The Long Emergency | 34.3% | 17.65 | 16.8 |
| The Reckoning | 29.0% | 17.97 | 15.8 |
| The Rising | 20.7% | 14.59 | 18.8 |

Turns per battle **4.8**. Dooms fired **16.7** a run, **dodged in 2585 of 11550 battles (22.4%)**.
Deck 24.3 at the end.

Against the start of the session: turns per battle 7.9, dooms dodged 6.4%, completion 27.0 / 28.3 /
17.7. **Re-measure before trusting any of it.**

## 8. How to reproduce anything here

```
dotnet test KinCore.Tests                                      # 105/105
dotnet run --project KinConsole -c Release -- content          # every act, doom, enemy, card
dotnet run --project KinConsole -c Release -- sim 300          # steer (CANNOT rank the acts)
dotnet run --project KinConsole -c Release -- sim 900          # record
godot-mono --path SQGodotCommon KinGame/kin_board.tscn        # the game, all three acts
```

**Release, always** — Debug is roughly 4x slower and server GC is set on the console project.
Kill a stale `KinConsole` before rebuilding; it holds the build lock. `Commands.md` has the traps.
