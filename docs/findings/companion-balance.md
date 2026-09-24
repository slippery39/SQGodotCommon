# THE COMPANION GAME — measured by `party-sim`

`dotnet run --project KinConsole -c Release -- party-sim N` (and `party-sim trace <seed>` to watch one
run). `PartyBot` beam-searches a turn's plays, scored by the engine's own forecast of this turn and
the next; map rules are fixed (Snares to 2, one card if affordable, a random area, the deeper path
only with two monsters and above 60% of your health and the team's HP). **A floor, not the game.**
Read it for WHERE runs die and HOW they are won, per region.

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
