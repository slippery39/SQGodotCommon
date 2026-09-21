# Handoff — DOOMJAM: the game became measurable, and then it became three games

**Read this, then `KinJam.md`, then `docs/findings/doom-balance.md`.** The findings file is the
evidence for every number below; this file is the shape of what happened and what will bite you.

**SUPERSEDED by `HANDOFF-KinPacingAndRewards.md`.** Read that one to pick the work back up; read
this one for its §4 scars, which still hold.

State at the time: KinCore.Tests 100/100 green, working tree clean, Godot project builds.

---

## 1. The headline

The previous session handed over a game that had to be played by hand to be judged. This one built
something to judge it, then used it hard enough to change the game's shape.

1. **There is a bot and a simulator.** `sim N` plays N runs and reports survival, pressure,
   apocalypses, per-act and per-card tables. A balance question is now a ten-minute measurement.
2. **A run is a 20-floor act with rests**, five of them, including the campfire before the boss.
3. **The doom is a SCHEDULE, not a roll.** You pick a theme; it decides every apocalypse you face.
4. **Three themed acts exist**, at 27% / 28% / 18% completion for a near-optimal bot.
5. **Permanent apocalypses are DATA.** A read and a verb, not a hand-written function.

## 2. The one thing to carry

**Never report one number across more than one act.**

The three acts first measured at **58% / 13% / 1.5%** — and the aggregate across them was **24.2%**,
almost exactly the 25% target we had been steering by. The average was not merely uninformative, it
was actively reassuring while two of three acts were unplayable.

`sim` prints a per-theme table first, above everything else, for this reason. If you add an act,
add it to that table before you add it to anything else.

## 3. What exists now

| | |
|---|---|
| `KinCore/Ai/KinBot.cs` | enumerates every legal line for a turn, scores each by really ending the turn on a copy |
| `KinCore/Ai/RunSimulator.cs` | plays a run, deals themes round-robin across seeds, records everything |
| `KinConsole/SimCommand.cs` | `sim N [Weight=value]` — five tables, JSON to `doom_sim_results/` |
| `KinConsole/ContentCommand.cs` | `content` — every act, doom, enemy, trait and card, read from the libraries |
| `KinCore/Content/ThemeLibrary.cs` | the three acts and the floor→doom schedule |
| `KinCore/Run/KinTransform.cs` | the permanent-doom language: a read × a verb |
| `KinCore/Run/FloorKind.cs` | Battle or Rest |
| `KinCore/Content/EnemyLibrary.cs` | 17 enemies, 4 Opponents, 7 traits, the act boss |

## 4. Scars worth not re-earning

**Measurement:**

- **Trigger frequency dominates effect size.** A death trigger fires once; a doom trigger fires
  about **nineteen times a run**. Cards costed as if those were comparable measured at **−2.5**.
  Fixing three statlines doubled an act's completion rate.
- **A broken number can be invisible when another mechanic covers for it.** Grey Goo grew decks to
  **103 cards** — in the act with the BEST completion rate, because AI Uprising one band above made
  every copy a free 8/8. Only the per-act deck-size breakdown exposed it.
- **One lever at a time, and I broke it.** Changing Famine and Judgement together lost the
  attribution of a 58%→5% swing. Changing Judgement alone recovered it in one run AND proved Famine
  had never been at fault.
- **Steer at n=300, record at n=900.** The same config reads about five points apart between them.
- **`mean floor = life budget ÷ life lost per battle`** held across every change for eight runs.
  Card buffs only ever move the denominator.
- **Sweep the eval weights to check the bot, not the game.** If a weight change moves the survival
  curve much, the curve is measuring the bot. Every config lands in the same band today.

**Performance:**

- **The sim is ALLOCATION bound.** The engine rebuilds a `GameState` per action and the bot
  simulates a whole turn per candidate line. Server GC took a 300-run workload **693s → 224s** with
  byte-identical output. Range partitioning looked like the cause and measurably was not.
- **A sim gets slower as the game gets better.** Budget by `runs × mean floor`.

**Rules:**

- **A permanent doom on the last floor is a no-op.** `AfterBattle` applies it, then the act ends, so
  it rewrites a deck nobody draws again. `EveryThemeEndsOnABattleScopeDoom` enforces battle scope.
- **Order within a scenario's transform list is load-bearing.** Duplicate-then-Delete is not
  Delete-then-Duplicate: copies are minted with new ids and fall outside a later read.
- **Minting and deleting have no natural ceiling**; a stat buff does. That is what `PerN` is for,
  and why Grey Goo reads STANDING (capped at five lanes) rather than SUMMONED.

**Process:**

- **CSharpier silently breaks string-match edits.** It reformats on commit and on demand, so a
  replacement written against a file you read earlier matches nothing and does nothing. **Assert on
  every replacement.** Bitten three times this session.
- **Chain a measurement behind its build with `&&`, never `;`.** A failed build let a sim run the
  stale binary; the tell was that no new card appeared in the card table.
- **Tests that restate a content constant break on every balance pass.** Twelve did. They read the
  authored value now — `KinTransforms.IrradiatedBuff`, `EnemyLibrary.HeraldOfTheEnd.Health`.
- **A stale `KinConsole` process holds the build lock.** Kill it before rebuilding.

## 5. What to do next

**In this order.**

1. ~~**The Godot front end only plays ONE act.**~~ **DONE 2026-09-16** — `KinThemeSelect` picks the
   act at run start and shows all three schedules; the seed is rolled per run and shown with them.
   Original note, for the reasoning: `KinBoard` reads `_run.Theme` but never sets it, so
   it always gets the `LongEmergency` default. **Two of the three acts exist only in the simulator.**
   A theme-select screen is the single highest-value thing on this list, and the design already
   wants it: the whole schedule can be shown at run start, which is the theme picker, the difficulty
   preview and the pitch in one.
2. **The Rising's cards need REPEATING payoffs.** Gravedigger +0.13, Pyre Tender +0.30, Choirmaster
   +0.54, against +1.7 to +2.3 for the other acts. Their statlines are on curve now; the problem is
   structural and no amount of body-pricing fixes it.
3. **Elites and Salvage.** Neither exists. An elite is an Opponent gated to a band's last floor;
   salvage is a relic-equivalent, and the pattern to use is MtgCore's `ActiveInZone` — a `Relics`
   zone scanned by `FireTriggers` alongside enemies, units and the Opponent. `IHasEffects` and the
   whole effect pipeline already exist, so it is roughly one enum entry and one line in the scan.
   Unique, rarity-weighted, one per elite.
4. ~~**Reward tiers.**~~ **DONE 2026-09-16, as RARITY — not floor tiers.** `RunCard.Rarity`,
   weighted 6/3/1, drawn without replacement. **Every card is offerable on every floor**; rarity
   only weights the bag, so a rare can turn up on floor 1 and half of all runs are offered one
   inside the first five screens. That is deliberate and it is a DESIGN decision, not a balance
   one — an early rare to build a run around is where a memorable run comes from, and
   `ARareCanBeOfferedOnTheFirstFloor` is the test that holds it.

   Floor gating (`MinFloor`) was built and measured first and is recorded in
   `docs/findings/doom-balance.md` run 9 as the road not taken. **Read its three findings before
   touching rewards**, two are traps: **gating CONCENTRATES rather than delays** (the first cut
   measured 35/43/29 — far EASIER — because a smaller early bag offers its best card more often);
   **n=300 cannot rank the acts**, it read them in exactly the reverse order of n=900; and a gate
   **breaks the card-value table**, since a gated card can only be taken by a run that reached it.

   Shipped numbers, n=900: **16.7 / 23.7 / 16.7%**, band 10.6 → 7.0 points wide, mean floor down
   only one floor (gating cost three). Overall 19.0% against a 25% target — see run 10 for why
   that is the first honest reading of the pool rather than a regression.
5. **Events.** A new `FloorKind` and a definition record. The cost is UI: `KinIntermission` only
   knows one-button panels and a card row, and this would be the fourth time it needed widening —
   generalise it once instead.
6. **Multi-act.** Deliberately not built. The shared-core card pool was designed so acts compose;
   what would need changing is `ScenarioFor` and `FloorKindFor`, which both take an ABSOLUTE floor
   and would need a floor-within-act.

**Known outliers:** Scavenged Rounds is handled — it is a RARE now, 1 ticket in 69 rather than
1 card in 14, and that correction alone is most of the 8-point difficulty change in run 10.
Riot Shield is fixed — 2/14 → 8/10, **−0.45 → +1.85**, and LE 16.7% → 20.7% with the other two
acts replaying IDENTICALLY (run 11: a themed-card change is perfectly attributable, and drift in an
act that does not hold the card means the change leaked). **Shieldbearer at +0.16 is the same shape
of dead card** — a 1-cost 4/12 — and it is SHARED, so the same fix should lift all three acts at
once. Power pays; toughness barely does, and that has now held three times. `Rapture` is still unimplemented
and gated to floor 99 so it is never offered — decide whether it ships or gets cut.

## 5b. SUPERSEDED — the 2026-09-16 session

That session shipped the theme picker, rarity-weighted rewards and a pacing rebalance, and its
account lives in **`HANDOFF-KinPacingAndRewards.md`**. Everything below in this file describes the
content as it was BEFORE that work — the numbers in §6 in particular are stale.

## 6. The measurement that matters

`sim 900`, 300 runs per act, 783s:

| act | completions | mean floor | life/battle |
|---|---|---|---|
| The Reckoning | 28.3% | 18.22 | 27.4 |
| The Long Emergency | 27.0% | 17.83 | 30.2 |
| The Rising | 17.7% | 15.93 | 30.6 |

Turns per battle 6.7. Dooms fired 19.9 a run, dodged in 791 of 11,887 battles. Deck 28.1 at the end.
**Re-measure before trusting any of it** — every content change invalidates it, which is the whole
reason the harness exists.

## 7. How to reproduce anything here

```
dotnet test KinCore.Tests                                      # 100/100
dotnet run --project KinConsole -c Release -- content          # every act, doom, enemy, card
dotnet run --project KinConsole -c Release -- sim 300          # steer
dotnet run --project KinConsole -c Release -- sim 900          # record
dotnet run --project KinConsole -c Release -- sim 300 Life=220 # override any eval weight
godot-mono --path SQGodotCommon KinGame/kin_board.tscn        # the game (LongEmergency only)
```

**Release, always** — Debug is roughly 4x slower and server GC is set on the console project.
`Commands.md` has the traps.
