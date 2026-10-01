# THE COMPANION GAME — measured by `party-sim`

`dotnet run --project KinConsole -c Release -- party-sim N` (and `party-sim trace <seed>` to watch one
run). `PartyBot` beam-searches a turn's plays, scored by the engine's own forecast of this turn and
the next; map rules are fixed (Snares to 2, one card if affordable, a random area, the deeper path
only with two monsters and above 60% of your health and the team's HP). **A floor, not the game.**
Read it for WHERE runs die and HOW they are won, per region.

## 2026-09-30 (c) — THE FOE PASS, REDONE ON PARITY: 38.8% won, Bramble 36.5% / Pike 41% (400 runs)

| step | runs won | Bramble | Pike | through by region (target 81/66/53/43/35) |
|---|---|---|---|---|
| parity, old foes | 54.2% | 54.5% | 54.0% | 96/74/67/59/54 |
| tiers up (region-1 exams Lv 7/8, regions 3–5 +3) | 38.0% | 45.5% | 30.5% | 91/64/54/43/38 |
| region-1 boss back to Lv 7; Pike 30 HP | 38.0% | 45.5% | 30.5% | 94/67/56/45/38 |
| Ember's combo pieces get a floor (Ignite applies 3 first, Spreading Flames +3, Flashpoint 6 +, Chain Lightning 5 a spell) | 44.8% | 45.5% | 44.0% | 94/69/61/51/45 |
| **regions 3–5 +1 level; region-1 elites Lv 8** | **38.8%** | **36.5%** | **41.0%** | **94/68/56/47/39** |

Wild chip by region: 4/5/9/10/12%.

**What it says:**
- **Harder foes hurt Ember more** (fragile): the first tier raise reopened a 15-point gap. It closed
  by giving Ember's COMBO PIECES a floor — `party-sim cards` showed its losers were the cards that do
  nothing alone (Spreading Flames −19%, Flashpoint, Ignite, Chain Lightning), because the bot takes
  whatever it is offered. A person drafts better; the sim is the scale.
- **Region 1 stays easy (94% through against 81%)** — by design: its wild foes are at most a level
  over base and its boss at Lv 7 (a solo monster). Its losses are left to region 2.
- **9 runs STALLED, all Bramble** (counted as losses — ~4 points of hers). Traced (seed 47): 40 turns
  against the Goblin Chief, the bot walling its minions with Sprouts and chipping the Chief. A person
  would press; but nothing ENDS a walled Grove fight, and the Chief does not enrage.

## 2026-09-30 (b) — FAMILY PARITY: Pike 7% → 54%, Bramble 72% → 54.5% (400 runs, 200 a starter)

Shayne: "It is hard to buff or nerf the enemies around such a disparity … get them so that absolute
maximum they are within 10% win rate of one another." **The rule from here: the two families within
10 points of each other before any foe balancing.**

**Where the gap was** (`party-sim variants 100`, same seeds a row):

| row | won | died in region 1 |
|---|---|---|
| Pike | 6% | 29 |
| Pike, Bramble's 30 HP | 7% | 15 |
| **Pike, GROVE family** | **67%** | 4 |
| Bramble | 79% | 1 |
| **Bramble, EMBER family** | **12%** | 8 |
| Bramble, no passive | 59% | 2 |
| Bramble, Pike's 24 HP | 71% | 5 |
| **Pike, basics only** (no Zap, Kindle) | **13%** | 20 |
| Pike, starts Root + Sow | 20% | 5 |
| Bramble, basics only | 70% | 4 |
| Bramble, starts Zap + Kindle | 58% | 4 |

- **The FAMILY was ~60 points; the starter monster 5–20.**
- **Ember's two starting cards were worse than none** — Zap (5) under a Pike Strike (5 + 3 Power),
  Kindle useless without spells after it.
- `party-sim cards 400` (new): no single card carried either family — Grove won ~70% whatever it
  held, Ember ~12% of those reaching region 2. So the fix was each family's FLOOR, not its rares.

**The steps** (`party-sim 200`, then 400):

| step | Bramble | Pike |
|---|---|---|
| start | 72% | 7% |
| Ember's floor up: Zap 8, Kindle draws 2, spells ~+40%, Ember Block over a Guard, monsters +6 HP, Pike 28 HP | 70% | 41% |
| Grove trimmed: Root 6, Sow no longer draws (Sow+ does), Sprout 5 HP, Seedling 3, Thicket 4, Hardwood 6/6, Treant 16, Mosshell 30 | 57% | 41% |
| Bramble 27 HP (400 runs) | 58.5% | 45.5% |
| **first attacks: Bramble +4 Rooted, Pike +2 Spell Power (400 runs)** | **54.5%** | **54.0%** |

**Now the whole game is too easy again — 54% against the 35% target** (through by region
96/74/67/59/54 against 81/66/53/43/35), so the foe pass has to be redone on top. 4 of 400 runs
stalled past 40 turns (unexamined).

## 2026-09-30 — THE FOE DIFFICULTY PASS: on the curve for the average, and a 72/7 starter split (150 runs a row)

Shayne's playtest: both families "too easy" (Grove walled a whole act for 3 damage). Decided in an
interview: difficulty from BOTH numbers and foe traits; wild fights a REAL chip (~5–10% a fight);
exam hits may rise because every exam is a fixed, telegraphed cycle; the sim steers, then Shayne
plays. **The bot's target moved from ~50% to ~35% of runs won** (`PartySim.BotWins`): a person plans
further than the bot, and 50% was far too easy for a person.

| step | runs won | Bramble | Pike | through by region (target 81/66/53/43/35) | wild chip by region | elites won |
|---|---|---|---|---|---|---|
| before (traits in) | 80.0% | 96% | 64% | 93/84/83/82/80 | 4/4/5/5/7% | 99.8% |
| tiers up, exams ×2.0/×1.8 HP and ×1.3 hits | 40.7% | 76% | 7% | 76/56/48/44/41 | 4/6/8/8/10% | 95.7% |
| region-1 exam hits back to ×1; regions 4–5 up | **39.3%** | **72%** | **7%** | 87/58/51/44/39 | 5/7/8/9/12% | 94.8% |

**What it says:**
- **The foes are on the curve for the average run** — every region within a few points of its target.
- **The average hides the families: Bramble 72%, Pike 7%.** No foe number fits both. Pike dies in
  region 1 (19, the Tusker ×8) and region 2 (35, bosses ×25). Foe tuning stops here: the gap is the
  families', and the bot may play Ember worse than a person (chains and banked energy are the plans a
  one-turn lookahead misses). Shayne's play is the judge.
- **Region-1 exam hits stay ×1.** At ×1.3 the Old Tusker (100 HP) wound up a Gore that went through
  two Guards; Pike fell with the boss at 15 (trace, seed 2). A solo region must stay answerable.
- **2 runs stalled** (a battle past 40 turns) on the last row — unexamined.
- Elites are still won ~95% of the time: the bot takes one only above 60% team HP.

## 2026-09-29 — GROVE DRAFT 1 BUILT: a crash check, not a tuning run (40 runs, seeds 1–40)

**Bramble 100% (20/20), Pike 70% (14/20)** — both families now drafted. Pike's 6 deaths are all
bosses in regions 1–2 (region 2's boss ×5). A Bramble trace (seed 1) plays across all four
archetypes: Root 25, Sow 20, Briar Patch 18, Thicket 16, Bristle 15, Heartwood 13, Thornmail 11,
Crushing Weight 7, Deep Roots 6, Bark Slam 5, Harvest 3… 40 runs is a smoke test, not a number to
tune to: tuning waits for Shayne to call it.

## 2026-09-28 (f) — EMBER DRAFT 2 BUILT: a crash check, not a tuning run (40 runs, seeds 1–40)

**Pike 60% (12/20)** — was 27% on the placeholder Ember; Bramble 100% (20/20, still placeholder).
Pike's deaths: region 1 boss ×1, region 2 ×5 (boss 4, elite 1), regions 4–5 ×2. 40 runs is a smoke
test: it proves the bot plays the new cards through whole runs (one trace: Zap 40, Meteor 35,
Overload 24, Heat Surge 13, Flicker 13, Pyroblast 8, Flame Ward 7…), not a number to tune to. Tuning
waits for Grove's draft.

## 2026-09-28 (e) — ROUND 4's rules on placeholder content: 61.7%, and the family gap (300 runs)

Monsters act only through cards (a first-attack bonus each), come from bosses, never level; springs
heal or upgrade. Every kit is a PLACEHOLDER (old species + a bonus).

| pass | tiers (wild → boss levels, region 1 → 5) | exams (HP ×) | won | Bramble | Pike |
|---|---|---|---|---|---|
| the old tiers | 3–5 … 19–21 wild; bosses 8 → 29 | boss 2.5, elite 2.4 | **4.3%** | 8.7% | 0% |
| **flat tiers** | 3–5 … 7–9 wild; bosses 6 → 10 | boss 1.8, elite 1.6 | **61.7%** | **96.0%** | **27.3%** |

- Without ally levels, the old climbing tiers were a wall (boss fights 8–16 turns).
- **Flat tiers put the run near the 50% target overall — as two games again.** Pike's wall is
  region 2 (61 of 109 Pike deaths: 43 to the boss, 15 to elites). Grove's placeholder kit has Block
  on nearly everything; Ember's has little. The family drafts are the fix, not a global number.
- Deaths are mostly to bosses (85 of 115); elites won 97.9%; wild chip 2–6%.

## 2026-09-28 (d) — WHY PIKE LOSES: the family, a Gore no region-1 deck can answer (150 runs a row)

`party-sim variants 150` swaps one thing at a time; `party-sim catches 300` counts catches by species;
`party-sim trace 2` is one Pike run.

| variant | won | died in region 1 | caught |
|---|---|---|---|
| Pike | 23.3% | 37 | 3.3 |
| Pike, 30 HP | 44.7% | 14 | 4.4 |
| **Pike, GROVE family** | **86.0%** | 5 | 6.8 |
| Bramble | 94.0% | 0 | 6.8 |
| **Bramble, EMBER family** | **28.0%** | 7 | 3.2 |
| Bramble, no Thornwall | 92.0% | 0 | 8.0 |
| Bramble, 18 HP | 82.7% | 2 | 6.0 |

- **The FAMILY is ~60 points; the starter's kit is small.** Thornwall is worth 2 points; HP ~11–21.
- **Catching is not the cause**: with a Snare in hand, Ember species are caught 36–67% of meetings,
  Grove 38–61%. Grove runs meet twice as many of their own kind only because they LIVE longer.
- **The trace: the bot plays Ember sensibly** (Stoke first, Zap, Spark Scroll) — and loses the
  region-1 boss on turn 2: the Old Tusker's wind-up telegraphs **Gore 29** against Pike's 19 HP. Guard
  (8) cannot answer it, so no play does. `Toughen`'s ×1.3 hits broke the exam's answer (it was 18).
- Spell damage never scales with level; exams gain HP with level and then double. Grove's power
  lives on its monsters, whose attacks do scale.
- Fixed: the bot valued leaving OFF-family foes at catchable HP (`PartyBot.Value` now asks
  `IsYourKind`).

## 2026-09-28 (c) — "WAY TOO EASY": exams toughened, the bot at 57% — and a 90/23 starter split (300 runs)

Shayne played (Pike) and agreed: way too easy — bosses, elites, and out-levelling. Target: **the bot
wins ~50%**. Levers chosen: **stronger exams** and **tougher wild fights** (not slower XP, not less
healing). Levels alone barely move a foe (~7% a level), so `PartyLevels.Toughen` multiplies an exam's
HP and hits on top: `PartyWorld.BossHp/BossHit`, `EliteHp/EliteHit`.

| pass | exams | won | Bramble | Pike | deaths to elite / boss | boss fight turns R1→R5 |
|---|---|---|---|---|---|---|
| levels +2–4 only | ×1 | 87.0% | 98.7% | 75.3% | 7 / 3 | 2.7 → 5.4 |
| boss ×2.0 HP ×1.3 hits, elite ×1.6/×1.2 | | 64.0% | 90.7% | 37.3% | 17 / 77 | 5.1 → 11.6 |
| **+ elite ×2.0/×1.4, later bosses +2–3 levels** | | **56.7%** | **90.0%** | **23.3%** | 49 / 69 | 4.8 → 13.5 |

- **At the target overall, and the deaths are in the right place** — elites and bosses (118 of 130).
- **But it is two games: Bramble 90%, Pike 23%.** Pike dies 35 times in region 1 and 39 in region 2.
  **Shayne played Pike and found it too easy** — so the BOT misplays Ember (it looks one turn ahead;
  Kindle pays over a fight), and Pike's numbers here are a floor, not a verdict on Ember.
- Survival still falls off early and flattens late: regions 4–5 let 94–97% through.
- Late boss fights run long (13.5 turns in region 5) — HP ×2 at high levels.
- Wild chip 4–6%: the tougher wild tiers did less than the exams.

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
