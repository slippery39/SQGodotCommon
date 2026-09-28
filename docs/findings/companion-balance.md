# THE COMPANION GAME — measured by `party-sim`

`dotnet run --project KinConsole -c Release -- party-sim N` (and `party-sim trace <seed>` to watch one
run). `PartyBot` beam-searches a turn's plays, scored by the engine's own forecast of this turn and
the next; map rules are fixed (Snares to 2, one card if affordable, a random area, the deeper path
only with two monsters and above 60% of your health and the team's HP). **A floor, not the game.**
Read it for WHERE runs die and HOW they are won, per region.

## 2026-09-28 (b) — THE EXAMS for regions 1–2: the wall is gone, and now it is too easy (300 runs)

The designed bosses and elites (`PartyExams`), catches at half HP, boss relics and a full heal after
each boss. Then one pass raising the wild tiers for "a little chip". Regions 3–5 reuse region 2's exams.

| pass | won | Bramble | Pike | Pike died, R1 / R2 / R3 / R4 / R5 | elites won | wild chip R1→R5 |
|---|---|---|---|---|---|---|
| exams | 91.7% | 100% | 83.3% | 3 / 14 / 4 / 3 / 1 | 99.1% | 0 / 3 / 4 / 5 / 5% |
| + wild tiers up | 88.3% | 100% | 76.7% | 2 / 10 / **15** / 4 / 4 | 99.4% | 4 / 5 / 7 / 6 / 7% |

- **Region 1's wall is gone** (Pike 60 deaths → 2–3).
- **Now the difficulty is in the wrong place again**: bosses and elites almost never kill (elites won
  99%+; the bot takes one only when healthy), and with the wild tiers up, **12 of Pike's 15 region-3
  deaths are WILD fights**. Bramble has not lost a run.
- The team out-levels every boss (region 5: Lv 23.7 against 22).
- Tiers now: wild 1–2 foes Lv 3–5 → 3 foes Lv 18–20; elite and boss 6/7 → 21/22.

## 2026-09-28 — ONE FAMILY, FIVE REGIONS, ELITES AND BOSSES: Pike's wall is region 1 (300 runs, seeds 1–300)

The run is now five regions of town → route → BOSS; one family per run (Bramble = Grove, Pike =
Ember); 1–2 ELITES a route (the bot takes one only with two monsters and above 60% HP); relics from
elites. **Elites and bosses are PLACEHOLDERS** (the Old Tusker and Old Mire lines). The TARGET column
is still the old ten-region curve — ignore it. Shayne's playtest: died at the first elite.

| | won | region 1 died (elite / boss) | region 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| **Bramble** (150) | **98.0%** | 0 (0 / 0) | 0 | 1 | 2 | 0 |
| **Pike** (150) | **37.3%** | **60 (19 / 40)** | 19 (13 / 5) | 7 (7 / 0) | 5 (3 / 1) | 3 (0 / 3) |

- **Every elite death is Pike's.** Elites won 94.5% (719/761) overall — the bot dodges them when weak.
- **Region 1 is the only wall, and only for Pike**: 40% of Pike runs die there, two thirds to the
  boss (Lv 7) with a team of ~2 at ~Lv 6. Pike has 18 HP to Bramble's 30 and no Block engine.
- **After region 1 it is easy**: 96–99% survive each later region, and the team out-levels the boss
  (Lv 13.6 vs 13, 17.6 vs 16, 21.6 vs 19). Bosses are not the late threat the design wants.
- 4.4 caught, 28.7 battles, 2.5 turns a battle, 2.5 elites fought a run.

## 2026-09-27 — THE JOURNEY with LEVELS: early game on target, late game too easy (300 runs, seeds 1–300)

The run is now town → route MAP → town with a LEADER, and every creature has a LEVEL
(`PartyLevels`, `PartyWorld.Tiers`). The bot walks routes (spring or find when hurt, the rare's lair
when healthy) and pays the hospital when hurt. **The 2026-09-24 tables below measured a different
game** (trails, the deeper path, HP/damage tiers); do not compare against them.

Shayne's playtest: "the first town leader's first turn was enough to wipe out all my monsters."
**Cause: its level came from the route AHEAD (Lv 10), not the one you reach it by (Lv 2–4).** The
team arrives at 4.7 on average (a Lv 5 starter and Lv 2–4 catches).

| pass | change | won | region 2 (1st leader) | worst early | late (6–10) survived |
|---|---|---|---|---|---|
| 1 | leader = route-before + 2 (Lv 6), XP 30×L | 49.0% | 87% (31 died at the leader) | region 3 85%, region 5 81% | 89–100% |
| 2 | explicit leader levels, XP 22×L | 52.3% | 91% (21 at the leader) | region 3 85%, region 5 82% | 90–98% |
| 3 | FIRST LEADER = Old Tusker's two (was the Old Mire's three); routes 3 and 5 one foe fewer | 63.0% | **96% (1 at the leader)** | all ≥ 99% | 84–100% |
| 4 (kept) | regions 4–10 and their leaders +1–3 levels | **57.7%** | 96% (1) | region 5 94% | **80–100%** |

Pass 4, per region — THROUGH (target) / survived here (target):
```
 1 Greenwood   100% (97%)  100% (97%)
 2 Mirelands    96% (93%)   96% (97%)   first leader Lv 5, team 4.7
 3 Stonefells   95% (90%)   99% (97%)   leader Lv 9,  team 7.8
 4 Deepwood     94% (82%)   99% (91%)   leader Lv 13, team 11.1
 5 Emberwastes  88% (75%)   94% (91%)   leader Lv 17, team 14.3
 6 Sunken Vale  84% (60%)   95% (80%)
 7 Thornmarch   67% (48%)   80% (80%)   46 died on the route
 8 Ashen Steppe 64% (39%)   95% (80%)
 9 High Crag    58% (31%)   90% (80%)
10 Wyrm's Rest  58% (25%)  100% (80%)
```

**What the numbers say, and what they cannot fix:**
- **The early game (regions 1–3) is on the curve** — the thing Shayne's playtest asked for.
- **After the first, LEADERS NEVER KILL, even 3 levels above the team.** A leader fields 2–3 foes; a
  late route fields 4–5; the team arrives healed (the hospital) with a big deck. **A content problem,
  not a level one**: leaders need bigger or rule-bending lines (the planned gym-leader rules).
- **Late routes split by POOL, not level**: regions drawing Ember Crags / Stony Ridge kill (7: 46,
  9: 19), regions drawing Misty Marsh / Mossy Hollow barely (8: 10, 10: 0). Only four areas feed ten
  regions; new areas are the fix.
- So the run is still won 58% against a 25% target — the late game is the next balancing job, and it
  is a design job first.

## 2026-09-24 (c) — TEN REGIONS, TUNED TO THE CURVE: 25.3% won (300 runs, seeds 1–300)

**The curve (Shayne), for the bot as the baseline:** 90% through region 3, 75% through region 5,
25% win all ten. Players get compared to the bot later. Regions 3–10 reuse the four areas and two
gyms, scaled by `PartyWorld.Tiers` (foes per fight, HP ×, damage ×); leader health 60 → 90 base.

| | Through 3 | Through 5 | Win |
|---|---|---|---|
| Target | 90% | 75% | 25% |
| Bot | **94%** | **70%** | **25.3%** |

Per region (survived / target): 98/97, 98/97, 98/97, 96/91, **77/91**, 92/80, 88/80, 76/80,
68/80, 87/80. Pike 32%, Bramble 26%, Gale 18%. ~0.75 s a run; 300 runs ≈ 4 minutes.

**What it took — and the finding that matters more than the numbers:**
- **HP and damage scaling alone did NOTHING**: at 2.6× HP and 4.2× damage, 88% still won, your
  health untouched (30/30 at every gym). Catches join at their region's scaling and fights had at
  most three foes, so three monsters always covered every attack.
- **More foes than monsters is the lever** (up to five from region 5; `Formation` now places 4 and
  5). Nearly every death since is YOUR HEALTH running out on the trail — the rule trainer health
  was added for, finally pressured.
- **Region 5 is a spike** (77% vs 91%): the Emberwastes use the Ridge + Crags pools, whose wide
  spitters punish uncovered columns. Area pools differ in difficulty; 3, 5 and 9 share them.
- **Gyms barely kill** (a few in regions 1–4); the curve is made on the trails. Racing the leader
  now wins 42–82% of gyms (was ~90%) — a real option, not yet an even one.

## 2026-09-24 (b) — THE SAME GAME, a better bot: 99.0% won (300 runs, seeds 1–300)

Shayne doubted a 5-second sim; a trace showed the first bot played one move ahead, never held a foe
at catchable HP, and took the deeper path alone. The bot now searches SEQUENCES of plays (beam 5,
up to 6 plays) scored on this turn and the next, values a Snare-able foe, and goes deeper only with
two monsters. ~11,800 engine passes a run (was ~1,600).

| | Region 1 | Region 2 |
|---|---|---|
| Survived it | **99.0%** | **100%** |
| At the gym: your health / team HP / team size | 27.1/30, 48%, 2.9 | 29.4/30, 71%, 3.0 |
| Gym fell to the LEADER's health | **87.9%**, in 3.7 turns | **90.2%**, in 4.1 turns |
| Went deeper | 37.6% | 90.9% |

Pike 99%, Gale 100%, Bramble 98%. 4.8 caught a run, 3.1 turns a battle.

**What it says:**
- **The first sim's 67% measured the BOT.** Played competently, the game as built is easy.
- **Racing the leader is THE way to win a gym** — nine in ten fall to it within four turns. Two of
  five columns are always empty with three monsters, and Rally/Frenzy/Momentum land there unopposed.
  The risk named when the leader was proposed, now measured.
- **Your health barely moves when played well** (27–29 of 30 at the gyms): the tension it was added
  for exists only when you misplay or are short of monsters.
- Caveat: the search sees what Dash will draw before playing it — a small peek a person does not get.

## 2026-09-24 (a) — trainer health, the gym leader, the in-battle bench (300 runs, seeds 1–300)

**Superseded by (b): this bot played one move ahead, and the numbers below measured it.**

| | Region 1 — The Greenwood | Region 2 — The Mirelands |
|---|---|---|
| Runs reaching it | 300 | 208 |
| Survived it | **69.3%** | **97.1%** |
| Died on the trail | 11.7% (your health: 34 of 35) | 1.4% |
| Died down the deeper path | 9.7% (your health: 29 of 29) | 0.5% |
| Died at the gym | 9.3% (your health: 17 of 28) | 1.0% |
| At the gym: your health / team HP / team size | 24.7/30, 46%, 2.8 | 29.1/30, 71%, 3.0 |
| Went deeper | 25.8% | 96.1% |

Runs won: **67.3%** — Pike 85%, Gale 62%, Bramble 55%. 3.5 caught a run, 4.1 turns a battle.

**What it says:**
- **The difficulty curve is upside down.** Region 1 kills 31% of runs; region 2 kills 3%. By
  region 2 the team is three strong (catches) and the town has healed everything.
- **Region 1 deaths are your HEALTH, not your monsters** (80 of 92). One starter covers one column
  of five, so most blows find nobody and hit you. Trainer health bites hardest exactly when you have
  the fewest monsters.
- **The deeper path in region 1 is a trap for the bot**: of the ~90 runs that went down it, 29
  died there — all to your health.
- **Bramble is the weakest alone** (slow, one column, damage from being hit); Pike's speed and
  Momentum carry a solo start.

Levers, not yet pulled (exploring — Shayne's call): single foes in region 1's wild fights; more
starting health; a second starter monster; a harder region 2.
