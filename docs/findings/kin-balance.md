> # EVERY NUMBER BELOW IS SUPERSEDED (2026-09-21).
>
> **The doom layer was deleted.** Scenarios, transforms, the clock and the per-theme schedule are
> gone, and with them the entire power curve — `KinJam.md` said it outright: *"the apocalypses ARE
> the power curve, there is no separate progression system."* Nothing replaced it yet.
>
> **Measured immediately after, over 200 runs: act completion 0.0%, mean floor 12.7.** Two controls
> were run rather than reasoned about:
>
> | change | act completion | mean floor |
> |---|---|---|
> | before the removal (run 20, this file) | 20.7–34.3% by act | — |
> | after, unchanged | 0.0% | 12.72 |
> | after, `StartingLife` 120 → 180 | 0.0% | 14.57 |
> | after, the six rehomed `OnDoomFires` effects neutralised | 0.0% | 14.84 |
>
> **Two findings worth keeping.**
>
> 1. **Life is not the lever here, and that is new.** This file's own rule — `mean floor = life
>    budget / life lost per battle` — still holds, but the denominator MOVED WITH the numerator:
>    life lost per battle went 18.4 → 28.1 as the budget went up, because the bot spends slack on
>    tempo. A bigger budget buys proportionally more spending, not more floors.
> 2. **The hole is structural, not a tuning error.** Neutralising every effect rehomed off the doom
>    trigger still measured 0.0%. The deck no longer gains power while `HealthScaleFor` and
>    `AttackScaleFor` keep climbing per act, so the player is static against a rising curve.
>
> ## The curve was rebuilt, and these are the numbers that matter now (2026-09-21)
>
> **Companion upgrades: three offered every second cleared floor, take one.** `UpgradePool` is seven
> entries — stat bumps, three effect grants, and Echo, which copies everything the companion already
> has.
>
> | `FloorsPerUpgrade` | upgrades a run | act completion |
> |---|---|---|
> | every floor (1) | 24 | **82.5%** |
> | **every second floor (2)** | 12 | **23.0%** |
> | every third floor (3) | 8 | **1.5%** |
>
> **The dial is violently non-linear and that is the finding.** Twenty-four upgrades to eight is not
> a threefold change in difficulty, it is 82.5% to 1.5% — compounding, and Echo compounds hardest
> because its value is whatever you already took. Do not interpolate this dial; measure it.
>
> **Per-act clear rates at the chosen setting: 68.5% / 60.6% / 55.4%** (200 entered act 1, 137
> cleared it, 83 cleared act 2, 46 cleared act 3). Smooth descending attrition, no act unplayable
> and none trivial — a better shape than the pre-pivot 20.7 / 34.3 / 29.0, which was not even
> monotonic. Reported per act because the single 23.0% would hide exactly the failure this file's
> own rule was written about.
>
> Everything below is the v2 game, kept for its METHOD — tune against the dodge rate, never report
> one number across acts, power pays and toughness barely does. The methodology survives; the
> numbers do not.

# DOOMJAM balance, as measured by the bot

**Reproduce:** `dotnet run --project KinConsole -c Release -- sim 1000`. Every table below is from
one such run; the raw file is in `doom_sim_results/` (gitignored — regenerate rather than quote).

**These numbers describe (game + bot), not the game.** The bot is `KinBot`: it enumerates every
legal line for a turn and scores each by ending the turn on a copy, so it is close to optimal
*within* a turn and blind past it. A better bot moves every number here. The readings that survive
a mediocre bot are the shape of the survival curve and before/after diffs on the same bot — an
absolute win rate is the weakest thing here.

---

## Run 1 — 2026-09-15, `bot-1`, 1000 runs, seeds 1-1000

Content as of commit `d3fb7cf`. 131ms a run, 2m11s total.

### The headline: nothing survives floor 4

| floor | reached | cleared | died | avg life on entry |
|---|---|---|---|---|
| 1 | 1000 | 1000 | 0 | 60.0 |
| 2 | 1000 | 1000 | 0 | 49.3 |
| 3 | 1000 | 696 | 304 | 36.4 |
| 4 | 687 | 44 | 643 | 15.9 |
| 5 | 41 | 15 | 26 | 13.0 |
| 6 | 15 | 3 | 12 | 13.1 |
| 7 | 3 | 0 | 3 | 9.3 |

**Act completion: 0/1000. Mean floor reached 3.76 of 20. Every single run died of life loss** —
not one ran out of deck, not one stalled.

### It is arithmetic, not difficulty

Life lost per battle is **18.1**, and it barely moves with floor or with how the bot is tuned.
60 life ÷ 18.1 = 3.3 battles. **The run length is decided before the player makes a decision.**
Floors 1 and 2 are free (100% cleared) and floor 4 is a wall (6% cleared) for the same reason: the
life bill has come due, not because floor 4 is a harder puzzle.

This is the same finding the last handoff recorded from one hand-played floor — "floor 1 barely
threatens a player who fills lanes" — with the other end of it now visible: floor 1 is free
*and* the act is unfinishable, because nothing heals.

### The bot is not the bottleneck

Sweeping each eval weight, 40 runs apiece:

| weights | mean floor reached |
|---|---|
| default (`Life=2`, `OpponentHealth=3`) | 3.73 |
| `Life=4` | 3.75 |
| `Life=6` | 3.52 |
| `OpponentHealth=1.5` | 3.75 |
| `OpponentHealth=6` | 3.73 |
| `UnitToughness=2` | 3.60 |
| `EnemyAttack=3` | 3.75 |
| `DeckCard=0` | 3.80 |

**Every configuration lands between 3.5 and 3.8.** Racing harder, blocking harder, valuing life
more — none of it matters, because the life bill is paid regardless. That plateau is what licenses
reading the survival curve as a property of the game.

### Apocalypses

| scenario | faced | died | death % | avg life lost | avg turns | avg floor |
|---|---|---|---|---|---|---|
| Flood | 1961 | 285 | 14.5% | 14.6 | 5.8 | 1.8 |
| Ashfall | 946 | 268 | 28.3% | 20.0 | 5.3 | 2.7 |
| Zombie | 422 | 212 | 50.2% | 23.2 | 5.8 | 3.4 |
| Nuclear | 417 | 223 | 53.5% | 24.5 | 6.1 | 3.5 |

**Read the floor column first.** `PlayableOn` gates the permanent-scope scenarios to later floors,
so Zombie and Nuclear face a harder board by construction and their death rates are scenario and
depth mixed together. The columns that are not confounded are `avg turns` — every scenario resolves
into a battle of the same length, 5.3 to 6.1 turns — and the *gap* in life lost, 14.6 for Flood
against 24.5 for Nuclear, which is larger than the floor gap alone explains.

**Dooms dodged: 1149 of 3746 battles (31%).** The design wants dodging "close but not free", and
31% with no card built to enable it is close to that.

### Card value

Mean floor reached by runs that took the card vs runs that did not, n ≈ 165 each side. **Rewards
are picked at random on purpose** — a greedy picker would make this table measure the picker.

| card | delta | | card | delta |
|---|---|---|---|---|
| Field Dressing | +0.34 | | Scrapper | +0.12 |
| Bonepicker | +0.33 | | Long Watcher | +0.04 |
| Scavenged Rounds | +0.32 | | Rust Golem | +0.03 |
| Siege Ram | +0.30 | | Stray | +0.01 |
| Breaching Charge | +0.25 | | Ash Walker | −0.02 |
| Warden | +0.16 | | Last Orders | −0.02 |

**Every card that helps is reach or life; every card that does nothing is a defensive body.**
Field Dressing (+6 life), Bonepicker (5/1), Scavenged Rounds and Breaching Charge (direct damage),
Siege Ram (7/2) lead. Rust Golem (2/6), Long Watcher (3/8) and Shieldbearer (0/5) are worth nothing
measurable. That is the life bill again: a battle is a 5.7-turn race, so a wall that outlasts it
never gets to matter.

**This table has almost no resolution yet** — every run ends between floor 3 and 5, so the whole
observable range is 0.3 floors wide. It gets sharper the moment runs last longer, and it is the
first thing to re-run after any change to the life economy.

### What this says to change

One lever at a time, re-running `sim 1000` between each, is the whole point of the harness:

1. **Healing between floors, or a shorter act.** 20 floors at 18 life a battle needs roughly 360
   life. Nothing else on this page matters until that gap closes.
2. **Floors 1-2 are free** (100% cleared, 0 deaths in 2000 attempts) and could carry more.
3. **Defensive rewards are dead cards.** Either battles need to be long enough for a wall to pay
   off, or the walls need something other than toughness.

---

## Run 2 — 2026-09-15, `bot-1`, 1000 runs: the enemy stat pass

Two changes, measured separately, both on enemy stats only. Nothing else moved.

**2a — Opponent health −23%** (The Opponent 26→20, The Choir 40→30, The Last Warden 58→44):

**2b — enemy health −15% on top** (Wretch 7→6, Scav Hound 5→4, Revenant 9→7, Herald 11→9,
Rotbearer 13→11, Siege Hulk 18→15). **This is the state the content is in.**

| | baseline | 2a: Opponent health | 2b: + enemy health |
|---|---|---|---|
| mean floor reached | 3.76 | 4.01 | **4.23** |
| act completions | 0/1000 | 0/1000 | 0/1000 |
| life lost per battle | 18.1 | 17.0 | **16.0** |
| turns per battle | 5.7 | 5.3 | 5.2 |
| deaths on floor 3 | 304 | 159 | **71** |
| floor 4 clear rate | 6% | 16% | **25%** |
| dooms dodged | 31% | 52% | **54%** |

**A 23% cut to enemy health bought 0.47 floors — 12% more run.** It did what it should: floor 3
stopped being a coin flip and floor 4 became winnable a quarter of the time. What it did NOT do is
change the shape — every one of 1000 runs still died of life loss, and none saw floor 10.

**The side effect is the most interesting result.** Dodging went 31% → 54%, because a cheaper
Opponent can be killed before the countdown expires. Enemy health is therefore a *doom-frequency*
dial as much as a difficulty one — cut it further and apocalypses stop landing at all, which costs
the game its entire premise.

### Why health is the weak lever

Life lost per battle barely moved (18.1 → 16.0) for a 23% stat cut, because **the leak is
front-loaded**. You take the most damage in the first turns, when your board is still empty and
3 energy cannot cover 5 lanes; ending the battle sooner removes turns from the *cheap* end.

Run length is `life budget ÷ life lost per battle` = 60 ÷ 16 = 3.75, and that is the mean floor
reached, to two decimal places. **Every balance question about run depth is that one fraction.**

## Run 3 — the attack probe (measured, then reverted)

Same 1000 runs with enemy ATTACK cut ~30% instead (Wretch 2→1, Scav Hound 3→2, Revenant 3→2,
Herald 3→2, Rotbearer 2→1, Siege Hulk 5→4) and health left at run 2b's values:

| floor | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 10 | 20 |
|---|---|---|---|---|---|---|---|---|---|
| reached | 1000 | 1000 | 1000 | 999 | 838 | 596 | 155 | 17 | 1 |

**Floor 6 went from 46 runs reaching it to 596. One run walked out of the act** — the first act
completion ever recorded. A comparable percentage cut to attack is worth roughly **four times** what
it is worth to health, and it is the same fraction that says why: attack is the denominator.

Reverted, because it belongs in its own pass with its own measurement. But **any future attempt to
make the act completable should start here or at the life budget, not at enemy health.**

---

## Run 4 — the early-game pass, against the Slay the Spire model

The target: floors 1-3 cost a couple of life with average draws; deckbuilding decides the later
floors; rewards are visibly better than the deck you were handed.

### First, four probes on what actually moves a run (control 4.23)

| probe | mean floor |
|---|---|
| enemy attack −30% | ~6, first act completion ever |
| starter units +1 power | 5.25 |
| starter units +2 toughness | 5.12 |
| rest every 3rd floor, heal 30% max | 4.67 |

**Power beats toughness point for point, on both sides of the board.** Killing a thing removes
every point of damage it had left; a point of toughness absorbs one point, once.

**This is the structural gap with Slay the Spire, and it is in the rules, not the numbers.** There,
1 energy buys 5 block EVERY turn — a Defend a turn absorbs ~25 over a fight. Here 1 energy buys a
Bulwark's 4 toughness once and it never comes back, because unit damage persists for the whole
battle (`UnitComponent.Damage`). Per energy, blocking here is about 5x weaker over a battle, which
is why every purely defensive card measured at zero. **Toughness is not block and cannot be
balanced as if it were.**

### The changes, measured in order

| | mean floor | life lost/battle | floor 1 cost |
|---|---|---|---|
| run 2b (control) | 4.23 | 16.0 | 7.5 |
| A1: floors 1-3 roster softened | 5.12 | 13.4 | 1.5 |
| A2: unlock curve spread out | 6.97 | 9.6 | 1.5 |
| B: reward pool pass | **7.13** | **9.4** | **1.5** |

**A1** — Wretch 6/2 → 4/1, Scav Hound 4/3 → 3/2, Revenant 7/3 → 6/2. The rule applied: a 2-power
starter must be able to kill an early enemy in two turns and live. It could not before, so a
blocker died every turn and the block had to be re-bought while nothing ever died.

**A2** — the curve is the roster, so the roster was spread: Herald 3→5, Rotbearer 4→7, Siege Hulk
6→10, The Choir 4→6, The Last Warden 9→12.

**B** — Ash Walker and Bulwark removed from the reward pool (they ARE starter cards, so taking one
upgraded nothing), and the defensive bodies given power: Shieldbearer 0/5 → 1/5, Rust Golem 2/6 →
3/6, Long Watcher 3/8 → 5/8, Stray 1/2 → 2/2.

### Where the curve sits now

| floor | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 10 | 12 |
|---|---|---|---|---|---|---|---|---|---|---|
| reached | 1000 | 1000 | 1000 | 1000 | 1000 | 998 | 654 | 238 | 52 | 5 |
| life it costs | — | 1.5 | 3.4 | 4.5 | 4.2 | 9.3 | 18.6 | 2.0 | 4.6 | 3.9 |

**Floors 1-5 are free and cost 1.5 to 4.5 life each — the goal, met.** Every card in the reward
pool now has a positive delta (worst +0.05, was −0.02), and the buffed walls went from zero to
real: Shieldbearer 0.00 → +0.26, Rust Golem +0.03 → +0.16.

### What is still wrong

1. **Floor 6-7 is the new cliff, and it is the same cliff.** The Choir unlocks at 6 (30 HP, heals
   2 a turn, fields Heralds) and floor 7 costs 18.6 life. Three Opponents across 20 floors means
   every transition is a step, not a slope. **A fourth Opponent between them is content, not code.**
2. **0/1000 act completions, and the fraction says why.** 60 life ÷ 9.4 a battle = 6.4, and the
   mean floor reached is 7.13. A 20-floor act needs roughly 190 life or 3 life a battle.
3. **Dodging is now 62% of battles.** It was 31% at baseline. The apocalypse is the premise, and
   the bot is outrunning it in two battles out of three. **This is the number to watch while
   lowering difficulty any further.**

### Rest sites, re-measured

The same probe, re-run on the fixed early game: **7.13 → 8.04, +0.91 floors** — double the +0.44 it
was worth before. **Rest compounds only once runs survive to reach a second one**, so it was the
right idea measured at the wrong time. It is still not sufficient alone: max floor reached 18, no
completions. It needs the map, so it stays unbuilt and unmeasured beyond this probe.

---

## Run 5 — chasing a completable 20-floor act

**There are no rest sites.** No map, no node types, nothing: `RunSimulator` and `KinBoard` both
start a battle on every floor, so a 20-floor act is **20 consecutive battles with no healing of any
kind**. `Run.ActLength`'s comment says "rests and events fill the rest" — that describes an
intention, not the code.

That fixes the budget: **60 life ÷ 20 battles = 3 life per battle, for every floor, forever.**

### The changes, no rests and no energy change (control 7.13)

| | mean floor | act completions |
|---|---|---|
| run 4 | 7.13 | 0/1000 |
| C: starter deck buffed | 8.13 | 0/1000 |
| D1: enemy count ramp `floor/3` → `floor/6` | 9.25 | 1/1000 |
| D2: late tiers softened | **11.04** | **21/1000** |

**C** — Scavenger 1/2/2 → 1/3/3, Bulwark 1/0/4 → 1/1/5, Ash Walker 2/3/3 → 2/4/5, Lantern Bearer
0/1/1 → 0/2/2. It also compressed the reward table (Feral Pack went to −0.07): **starters and
rewards have to move together, or a starter buff quietly deletes the reward tier.**

**D1** — the enemy count filled all five lanes by floor 9 while energy is a flat 3 that never
grows. Two lanes leaked every turn no matter how well the bot played, which is why late battles
cost ~20 life apiece. Five lanes now arrives at floor 18.

**D2** — Herald attack 3→2 and its death burn 4→2, Siege Hulk attack 5→3, The Choir's heal 2→1,
The Last Warden's summon interval 2→3.

Late floors flattened as a result: runs that reach floor 10 arrive with ~21 life, hold there, and
clear 73-90% of every floor after it. **The act now has one wall, at floor 9, instead of a
staircase.**

### The measurement that decides the act's shape

Everything above is the game getting better at 20 unhealed battles and still only completing 2% of
the time. Three probes, each reverted, on what closes the rest of the gap:

| probe (on top of run 5) | mean floor | completions |
|---|---|---|
| 20 floors as 15 battles + 5 rests | 9.91 | 0.3% |
| + energy 3 → 6 across the act | 10.65 | 3.3% |
| life budget 60 → 100, no rests | **14.59** | **10.0%** |

**Life is the only lever that reaches the goal, and rests are the same lever wearing a hat.**
60 life over 20 battles is 3 a battle; floors 1-5 cost 1.0-2.7 and floors 6+ cost 11-20. For every
floor to fit in 3, every floor has to look like floor 1 — **without healing, "completable act" and
"escalating difficulty" are the same dial pointed in opposite directions.**

The resolutions, all arithmetically equivalent, differing only in what the player gets to decide:

1. **A bigger life budget.** Pure balance, one number, no new systems. ~140 life is where 20
   unhealed battles start completing often. It gives the player no decision.
2. **Rests / non-battle floors.** Same life, spent as a choice, and it is what `ActLength` already
   claims the map does. Measured at +1.8 floors and 10x the completions when it was probed.
3. **A shorter act.** 10 floors at the current 6-7 life a battle already works.

---

## Run 6 — the reward pool buffed to match the starters

Every body lifted clearly above the starter card of its cost, surplus in power: Scrapper 3/1 → 5/2,
Shieldbearer 1/5 → 2/6, Tunneller 2/3 → 4/3, Stray 2/2 → 3/3, Rust Golem 3/6 → 5/7, Feral Pack
4/2 → 6/4, Bonepicker 5/1 → 7/2, Siege Ram 7/2 → 9/3, Warden 4/6 → 6/8, Long Watcher 5/8 → 6/10,
Scavenged Rounds 2 → 3 to all, Breaching Charge 5 → 7. Field Dressing left at +6 deliberately.

| | mean floor | completions | life/battle | turns/battle | dodged |
|---|---|---|---|---|---|
| run 5 (D2) | 11.04 | 2.1% | ~7 | 3.9 | 66% |
| run 6 (rewards) | **11.70** | **3.0%** | **5.4** | **3.3** | **71%** |

**The pool is healthy now.** Every card is positive, from +0.18 to +1.11, where before the buff
four were at or below +0.1 and Feral Pack was negative.

### Two things this run settles

**1. The fraction is invariant, and it has never once missed.**

| run | life budget | life lost per battle | budget ÷ cost | mean floor |
|---|---|---|---|---|
| 1 | 60 | 18.1 | 3.3 | 3.76 |
| 2b | 60 | 16.0 | 3.8 | 4.23 |
| 4 | 60 | 9.4 | 6.4 | 7.13 |
| 6 | 60 | 5.4 | 11.1 | 11.70 |
| life-140 probe | 140 | ~8 | 17.5 | 17.11 |

**Mean floor reached = life budget ÷ life lost per battle.** Every buff to the player lands on the
denominator and every change to the act's length lands on the numerator. Nothing else has moved it
all session.

**2. Buffing the player is now actively costing the game its premise.**

Dodge rate by run: **31% → 54% → 62% → 66% → 71%.** It has climbed with every single player buff,
because a stronger deck kills the Opponent sooner and battles are down to 3.3 turns against
countdowns of 2-5. **The apocalypse now fails to fire in seven battles out of ten.** Flood was
faced 3874 times and killed 5.2% of the runs that met it.

This is the trap, stated plainly: **under a fixed life budget, "the act is completable" and "the
apocalypse matters" are the same dial pointed in opposite directions.** Buffing the player shortens
battles, which outruns the doom. Lengthening battles so the doom lands raises life lost per battle,
which shortens the run. Only the numerator — life budget, rests, or act length — relieves both at
once.

**Recommendation: stop buffing cards.** The pool is measurably healthy and further lifts only buy
dodge rate. The next change should be on the numerator, and rests are the version of it that costs
the player a decision rather than granting them a bigger number.

---

## Run 7 — separating battle LENGTH from battle LETHALITY

The idea under test: raise enemy and Opponent health, leave attack alone. Battles get longer
without getting harder, so the doom clock gets time to land.

Health only — Wretch 4→7, Scav Hound 3→5, Revenant 6→9, Herald 9→13, Rotbearer 11→16, Siege Hulk
15→22, The Opponent 20→32, The Choir 30→48, The Last Warden 44→70. No attack moved.

| | run 6 | + health | + life 60→80 |
|---|---|---|---|
| turns per battle | 3.28 | 4.50 | **4.60** |
| dooms dodged | 71% | 51% | **48%** |
| dooms fired per run | 3.7 | 5.6 | **7.1** |
| life lost per TURN | 1.64 | 1.50 | 1.63 |
| life lost per BATTLE | 5.37 | 6.73 | 7.50 |
| mean floor | 11.70 | 9.41 | **11.04** |

**The hypothesis is right per turn and wrong per run.** Per-turn lethality went DOWN (1.64 → 1.50):
the battles genuinely are not harder. But 37% more turns at 91% the rate is 25% more life per
fight, and **the run pays per battle, not per turn** — so the same fraction took it straight back
out of the run's depth. Twenty life of budget bought it back.

**This is the shape to keep.** Enemy health is the clean dial for how often the apocalypse lands,
and it costs run depth at a known, payable rate.

### The tuning ceiling nobody has hit yet

To make battles longer AND no more expensive, attack has to come down as health goes up. **Attack
values are 1, 2 and 3.** The smallest cut available is one point — 33% to 50% — and Wretch is
already at 1 and cannot go lower.

**The stat scale is too compressed to tune at this resolution.** Doubling every number in the game
(life, power, toughness, attack, enemy and Opponent health) is balance-neutral by construction —
every interaction is the same number of hits — and doubles the tuning resolution. It is not free:
`KinLaneCell` renders attack and life as PIPS, so doubling the scale doubles the pip counts, which
is a `KinUI.md` question and not only a balance one.

---

## Run 8 — the act is completable

Three changes, in dependency order, each measured on its own.

**1. Every stat in the game doubled** — life, power, toughness, attack, enemy and Opponent health,
every effect amount, the companion's marks, the summon ramp, Nuclear's buff, the Zombie body,
the Irradiated draw cost. **Measured neutral, as predicted**: 11.04 → 11.17 mean floor, 2.5% → 2.5%
completions, 4.6 turns both. Tuning resolution doubled for free.

It cost 12 test failures, every one of them a test restating a content constant instead of reading
it. They now read the authored value, and `KinTransforms.IrradiatedBuff` and `IrradiatedDrawCost`
are named constants rather than literals in six assertions. **The pip worry was unfounded** —
`KinPalette.Pip` draws a numeric badge, not one pip per point, so the scale is invisible to the UI.

**2. Countdowns cut** — Flood 5→4, Ashfall 4→3, to fit the battles we actually have rather than
the ones the numbers were written for. Dodge 48% → 29%, firings 7.1 → 9.1 a run, for 0.27 floors.
**The cheapest change measured all session.**

**3. Rest floors, as content.** Every fourth floor is a `FloorKind.Rest` and the last floor never
is: 16 battles and 4 rests across the 20. `StarterContent.FloorKindFor` is asked by BOTH
`RunSimulator` and `KinBoard`, with a test asserting no run ever fights on a rest floor — if those
two disagree about what a floor is, every number on this page describes a game nobody plays.

### Where it lands, 1000 runs

| | run 7 | **run 8** |
|---|---|---|
| act completions | 2.5% | **23.9%** |
| mean floor | 11.04 | **17.00** |
| turns per battle | 4.6 | **5.1** |
| dooms dodged | 48% | **17%** |
| dooms fired per run | 7.1 | **14.7** |
| deck size at the end | 24.8 | 31.8 |

**The goal is met: a semi-optimal bot finishes the act about one run in four**, and the apocalypse
lands in five battles out of six instead of being outrun.

The apocalypses have also converged: Flood 4.7%, Ashfall 5.5%, Zombie 5.7%, Nuclear 6.7% deaths —
a two-point spread where run 1 had a forty-point one. Deaths now concentrate on floors 18-20, which
is the right place for them.

### The one outlier left

| card | delta | | card | delta |
|---|---|---|---|---|
| **Field Dressing** | **+3.50** | | Stray | +0.95 |
| Tunneller | +1.23 | | Shieldbearer | +0.88 |
| Scavenged Rounds | +1.13 | | Bonepicker | +0.73 |

Everything else sits between +0.12 and +1.23. **Field Dressing is still three times the next best
card**, exactly as it was at +1.57 and +1.02 in earlier runs. It has survived every balance pass
because it is the only card that pays out in the currency the run is actually short of. Rests did
not dethrone it.

Either it comes down, or life stops being scarce enough for one card to own the format. **It is the
next thing to change**, and its delta is the cleanest measure of whether the life economy is fixed.

---

## Run 9 — the content pass, and the card pool coming good

11 new enemies (6 → 17), an act boss gated to floor 20, and Opponent traits: four Opponents crossed
with seven traits, assigned as a pure function of (floor, seed) so no run fights the same Opponent
twice. `NoOpponentIsFoughtTwiceWearingTheSameTrait` enforces it.

### The rebalance the content forced

| | run 8 | + content | rebalanced |
|---|---|---|---|
| act completions | 23.9% | 0.8% | **17.7%** |
| mean floor | 17.00 | 13.63 | 15.25 |
| life per battle | 21.3 | 26.9 | 30.8 |

**The new effects were not the problem.** Cutting every per-turn drain and doom burst bought back
0.9 life a battle out of 5.6. The cost was the ROSTER: `PlayableOn` returns everything legal, so
late floors roll random picks from seventeen enemies averaging far bigger than the old six. Adding
content to a pool makes every floor above its `MinFloor` harder, whether or not you touched a number.

Two structural fixes did the work, and neither was a stat:

1. **Three unlocks were landing on floor 13 at once** — The Last Warden, the enemy-count step to 4,
   and The Tally. That one floor cost **62.6 life**, more than floors 1-5 together. The count steps
   at floors 6, 13 and 18; nothing else may unlock there.
2. **The pre-boss rest was worth four times everything else.** Floors 17-20 were four unbroken
   battles at the hardest tier, clearing 22% by the end. A rest at `ActLength - 1` took completions
   from 5.2% to 21.8% on its own.

**n=400 read 21.8% and n=1000 read 17.7%.** Four points of optimism from the smaller sample — use
400 to steer and 1000 to record.

### Field Dressing is no longer the format

| | run 8 | run 9 |
|---|---|---|
| Field Dressing | **+3.50** | +2.39 |
| next best | +1.23 | +2.28 |
| worst | +0.12 | +0.90 |
| best-to-worst spread | **29x** | **2.6x** |

Run 8 predicted this: *"either it comes down, or life stops being scarce enough for one card to own
the format."* Life stopped being that scarce — rests and a 200-life budget — and the card that only
ever paid out in the scarce currency came back to the pack without being touched.

**A dominant card is usually a symptom of a starved resource, not a broken card.** Three balance
passes tried to fix it by adjusting other cards and none of them worked. Fixing the economy did it
in one.

The apocalypses are level too: 5.2% to 7.7% deaths, a 2.5-point spread where run 1 had forty.

---

## Run 10 — themed acts, and what measuring them separately exposed

The doom is a SCHEDULE now, not a roll: a theme is picked once and decides the whole sequence.
Three acts, three bands of six floors each plus a boss-floor doom of its own.

| act | 1-6 | 7-12 | 13-18 | boss |
|---|---|---|---|---|
| The Long Emergency | Civil Unrest | AI Uprising | Grey Goo | Detonation |
| The Rising | The Rising | The Thirst | Hell Uprising | The Last Host |
| The Reckoning | The Flood | Famine | Judgement | Brimstone |

### The aggregate was a lie, and the per-theme table caught it

First measurement of the three acts: **58.0% / 13.0% / 1.5%** — and the aggregate across them was
**24.2%**, almost exactly the 25% target. **Three acts differing by 40x averaged to the number we
were aiming for.** Any single completion figure covering more than one act is worthless; the
per-theme breakdown went into `sim` on the strength of this and earned its place on the first run.

### Every doom's real cost, once each owned a band

Sequencing exposed what the random roll had been averaging away:

| doom | death rate | life lost | turns |
|---|---|---|---|
| Vampires | 24.2% | 69.5 | 9.8 |
| Hell Uprising | 25.6% | 39.6 | 6.0 |
| Famine | **0.1%** | 15.5 | 6.0 |
| Judgement | **1.2%** | 22.9 | 6.4 |
| Grey Goo (as a board wipe) | 75.2% | 139.0 | 6.4 |

**Famine and Judgement were net BUFFS.** Famine made survivors cheaper and deleted only cards the
bot was not playing; Judgement setting everything standing to 10/10 UPGRADES most of a starter deck.
A levelling effect has to level DOWN to be a doom. Judgement is 6/6 now and Famine takes one
unplayed unit in two rather than one in four.

Vampires was halved (enemies heal 8 -> 4) on the theory that healing the whole enemy line fights the
only win condition the player has. **It did nothing** — The Rising went 1.5% -> 1.0%. Wrong lever.

### Trigger frequency dominates effect size

The first themed cards measured like this:

| card | trigger | delta |
|---|---|---|
| Drone Swarm | on doom fires | **+3.52** |
| Reactor Crew | on doom fires | +2.78 |
| Gravedigger | on death | **-2.52** |
| The Choirmaster | on doom fires, weak body | **-2.44** |
| Pyre Tender | on death | -1.59 |

**A death trigger fires once. A doom trigger fires about nineteen times a run.** Every card built
around dying came out negative and every card built around the clock came out strongly positive.
The bodies had been priced below the shared pool's curve to pay for effects that could not cover it
— Gravedigger was a 4/4 for 1 where the shared Tunneller is 8/6 for 1.

### Grey Goo was exponential

Duplicating what you COMMITTED compounds: copies enter the deck, get played, become eligible to be
copied again.

| act | deck at floor 13 | deck at floor 18 |
|---|---|---|
| The Long Emergency | 19.0 | **102.9** |
| The Rising | 24.7 | 28.3 |
| The Reckoning | 10.6 | 14.6 |

`PerN` only halves the BASE of an exponential. The fix is to bound the READ: duplicating what is
STANDING caps a firing at two copies, because standing is capped at five lanes.

It hid because AI Uprising in the band above makes everything standing a free 8/8, so a 103-card
deck of free 8/8s draws perfectly well — that act had the BEST completion rate while its deck was
five times the size of anyone else's. **A broken number can be invisible when another mechanic is
covering for it.**

### Process

**A build failed and the sim ran the stale binary**, because the command chained with `;` instead of
`&&`. The card table was the tell: none of the themed cards appeared in it. Chain measurement behind
a build with `&&`, always.

---

## Run 11 — the three acts brought into a band

Three changes, each measured on its own, each confirming a diagnosis rather than guessing at one.

| | first measured | after |
|---|---|---|
| The Long Emergency | 13.0% | **22.0%** |
| The Reckoning | 58.0% | **22.0%** |
| The Rising | 1.5% | **14.0%** |
| deck size at the end | 48.0 | 27.9 |

**Grey Goo, bounded.** Reading STANDING instead of SUMMONED caps a firing at two copies, because
standing is capped at five lanes. End-of-run decks fell 48.0 -> 27.9 and the act's completion did
not move, which confirms the 103-card decks were absurd but genuinely harmless — AI Uprising one
band above makes everything a free 8/8, and a huge deck of free 8/8s draws perfectly well.

**The Rising's cards, brought to the shared pool's curve.** Gravedigger -2.52 -> +0.55, Pyre Tender
-1.59 -> +0.33, The Choirmaster -2.44 -> +0.06. **Fixing three statlines doubled the act's
completion rate**, 7% -> 14%. The bodies had been priced below curve to pay for effects that could
not cover it.

**Judgement 6/6 -> 8/8, and nothing else.** 5% -> 22%. Famine was changed in the same pass as the
6/6 and was deliberately left alone this time, which is the only reason the swing is attributable:
**Famine was never the problem.** 10/10 upgraded a starter deck, 6/6 deleted the reward pool
(Siege Ram is 18/6, Long Watcher 12/20), 8/8 humbles without erasing.

### Confirmed at n=900

300 runs per act, 783s once server GC landed:

| act | n=300 | n=900 | mean floor |
|---|---|---|---|
| The Reckoning | 22.0% | **28.3%** | 18.22 |
| The Long Emergency | 22.0% | **27.0%** | 17.83 |
| The Rising | 14.0% | **17.7%** | 15.93 |
| all three | 19.3% | **24.3%** | 17.33 |

All three read about five points higher than at n=300. The n=300 seeds are a SUBSET of the n=900
ones, so this is sampling, not a change — **trust the larger sample**. Steer at n=300, record at
n=900.

Every card in the game is positive, +0.13 to +3.41. The weakest four are all of The Rising's:
Gravedigger +0.13, Pyre Tender +0.30, The Choirmaster +0.54, Blood Price +0.97, against +1.7 to
+2.3 for the other acts' cards. Bringing their statlines to curve took them from harmful to merely
unexciting; **a one-shot trigger cannot be priced into competing with one that fires nineteen times
a run.** That set needs repeating payoffs, not bigger ones.

Outliers to watch: Scavenged Rounds at **+3.41** has been the best card in the game in every
measurement, and Shieldbearer at +0.16 is dead.

### What the whole arc says

Every act is now inside a playable band and the aggregate finally means something because the parts
agree. Three findings generalise past this game:

1. **Never report one number across several acts.** 58 / 13 / 1.5 averaged to 24.2% against a 25%
   target. The aggregate was not merely uninformative, it was actively reassuring while two of the
   three acts were unplayable.
2. **Trigger frequency dominates effect size.** A death trigger fires once; a doom trigger fires
   about nineteen times a run. Cards costed as though those were comparable came out at -2.5.
3. **A broken number can be invisible when another mechanic covers for it.** Grey Goo's exponential
   decks showed up in the act with the BEST completion rate, and only the per-act deck-size
   breakdown made it visible at all.

---

## The sim was allocation bound, not CPU bound

Sims had grown from 2 minutes to over an hour across this project, because **the sim gets slower
exactly as the game gets better** — a run that completes an act is fifteen battles where one that
dies on floor 3 is three.

At 16 logical cores it was running at **3.1x**. The obvious suspect was `Parallel.For`, which
range-partitions: each worker takes a contiguous block of seeds, and with run costs this uneven one
worker draws all the long runs while the rest idle. **That was wrong.** Chunking seeds one at a time
measured 3.6x -> 3.1x, i.e. no change, and the speculative fix was reverted.

The real constraint is the engine's own shape. **`ImmutableGameObjects` rebuilds a `GameState` for
every action**, and `KinBot` simulates an entire `EndTurnAction` per candidate line, so one turn of
one battle allocates hundreds of states. A console app defaults to WORKSTATION GC: one shared heap,
every thread contending on it.

| same 300-run workload | time | per run |
|---|---|---|
| workstation GC | 693.5s | 2312ms |
| **server GC** | **224.2s** | **747ms** |

**3.1x, from two lines in `KinConsole.csproj`.** The balance output was byte-identical across the
change — same completions, same mean floors, same per-theme numbers — which is the property a
performance change has to have before it can be trusted.

**Any project that drives this engine in bulk wants `ServerGarbageCollection`.** That is a finding
about `ImmutableGameObjects`, not about DOOMJAM: an immutable engine allocates per action by
construction, so bulk simulation is allocation bound by construction.

---

## Run 9 — 2026-09-16, `bot-1`, 900 runs, seeds 1-900 — reward tiers, THE ROAD NOT TAKEN

> **This design was built, measured, and rejected. `MinFloor` is not in the code.** It is recorded
> because the measurements are real and two of them are traps anyone touching rewards will hit. What
> shipped is rarity weighting — run 10 below.
>
> **Why it was rejected, and it is not a balance reason:** floor-gating the rares makes an early
> rare impossible, and an early rare you get to build the rest of the run around is where a
> memorable run comes from. Slay the Spire lets a rare turn up on floor 1 for exactly that reason.
> The tidier difficulty curve below was bought with the best runs in the game, which is a bad
> trade at any completion rate. `ARareCanBeOfferedOnTheFirstFloor` now holds that decision.

`RunCard.MinFloor` gated what a floor may OFFER: two-cost from floor 7, three-cost from floor 13,
and **Scavenged Rounds from floor 13 despite costing 1** — the one card whose gate disagreed with
its cost.

Compared against Run 8, same bot, same seeds, flat pool:

| act | flat (run 8) | tiered | Δ | mean floor, flat → tiered |
|---|---|---|---|---|
| The Long Emergency | 27.0% | **18.0%** | −9.0 | 17.83 → 14.26 |
| The Reckoning | 28.3% | **22.3%** | −6.0 | 18.22 → 14.93 |
| The Rising | 17.7% | **26.7%** | +9.0 | 15.93 → 15.26 |

Band width **10.6 → 8.7 points**, and the act that was the outlier is no longer the outlier. Overall
22.3%, against the 25% target. 796s. Dooms fired 21.0 a run. Deck 25.7 at the end.

### Tiering is a CONCENTRATION change, not an ordering change

This was backwards from the prediction. Gating cards out of early floors does not merely delay
them — it **shrinks the early bag**, and a smaller bag offers its best card more often. Floor 1 went
from a 14-card pool to a 7-card one, and the first measurement (tiers by cost alone, Scavenged
Rounds left on floor 1) came out at **35 / 43 / 29%** — every act far EASIER, because the best card
in the game had just had its offer rate roughly doubled.

**The flat pool's dilution was doing balance work nobody had accounted for.** Gating Scavenged
Rounds to floor 13 is what brought it back to target. That single card is worth 10-20 points of
completion depending on how early it can be offered:

| Scavenged Rounds gate | LE | Reckoning | Rising | spread |
|---|---|---|---|---|
| floor 1 (cost tier) | 35.0% | 43.0% | 29.0% | 14 |
| floor 7 | 30.0% | 26.0% | 18.0% | 12 |
| **floor 13** | 25.0% | 20.0% | 19.0% | **6** |

*(that table is n=300 — see the next finding before trusting its ordering)*

### 100 runs per act cannot RANK the acts

The n=300 steer read **25.0 / 20.0 / 19.0** (LE best, Rising worst). The n=900 record of the same
build read **18.0 / 22.3 / 26.7** — the exact reverse order. Nothing changed but the sample.

At 100 runs per act the standard error on a ~20% rate is about 4 points, so a 6-point gap between
acts is barely one and a half of them. The handoff's rule was "steer at n=300, record at n=900,
they read about five points apart"; the sharper version is that **at n=300 the five points are
enough to reorder the acts, so n=300 can say whether a change moved the game and must not be used
to say which act is worst.** Both n=300 conclusions above survive — tiering helps, the gate is worth
a lot — because both are before/after diffs on the same sample. The ranking did not survive.

### MinFloor breaks the card-value table

A gated card can only be taken by a run that already reached its gate, so its "with" mean is bounded
below by that floor. Warden reads **+5.50** and Long Watcher **+5.41** — both floor-13 cards, and
both numbers are mostly the gate, not the card. The table was always confounded with run length
(every card reads positive); tiering makes it uninterpretable across tiers.

**Compare cards within a tier, or not at all.** Ranking the whole pool by delta is now wrong.

### Riot Shield is the only negative card in the game, and tiering made it matter

Riot Shield: **−0.45** over 215 takes, the one card whose delta is below zero.

It is also The Long Emergency's only cheap themed card, so concentrating the early pool concentrated
LE's offers onto its own dud — which is the most likely reason LE fell 9 points while The Rising
rose 9. The Rising has two one-cost cards that pay (Gravedigger, Blood Price) and gained from the
same mechanism. **The next lever is Riot Shield's statline, and it is a one-card change with a
clean prediction attached: LE should come back up without touching the other two acts.**

### Pacing regressed, and completion cannot see it

Mean floor fell about three floors in every act (17.8 → 14.3, 18.2 → 14.9, 15.9 → 15.3) while
completion stayed near target. Reaching floor 20 at all went 58% → 48%; clearing it once there went
33% → 47%. A losing run ended mid-band-three rather than at the boss, so **a player who lost saw
less of the act than before** — same difficulty, less content. Completion rate is blind to this by
construction.

**Rarity weighting (run 10) costs almost none of this**, which is the strongest practical argument
for it over gating: mean floor −1 floor instead of −3, for a comparable difficulty change. Gating
shortens the run; weighting only makes the deck more ordinary.

---

## Run 10 — 2026-09-16, `bot-1`, 900 runs, seeds 1-900 — rarity weighting (SHIPPED)

`RunCard.Rarity` — Common / Uncommon / Rare, weighted **6 / 3 / 1** in `StarterContent.WeightOf`,
drawn without replacement so the three offers stay distinct. **A weight, never a gate: every card is
offerable on every floor.** 5 rares, 11 uncommons, 10 commons.

| act | flat (run 8) | gated (run 9, rejected) | **rarity (shipped)** | mean floor, flat → rarity |
|---|---|---|---|---|
| The Long Emergency | 27.0% | 18.0% | **16.7%** | 17.83 → 16.77 |
| The Reckoning | 28.3% | 22.3% | **23.7%** | 18.22 → 17.33 |
| The Rising | 17.7% | 26.7% | **16.7%** | 15.93 → 14.92 |

Band width **10.6 → 7.0 points**, the tightest of the three designs. Overall 19.0%, against a 25%
target — the game is now about five points too hard. 947s, 20.7 dooms a run, deck 27.7 at the end.

### Weighting costs pacing that gating spent

This is the reason to prefer it, and it is not a difficulty argument — both designs land in a
similar band. It is that **mean floor fell one floor under weighting and three under gating**, for
comparable completion. Reaching floor 20 at all: 58% flat, 48% gated, **55% weighted.**

Gating shortens the run, because a floor-1 player is drawing from a deliberately worse bag and dies
earlier for it. Weighting leaves the whole pool reachable and only makes the average draw more
ordinary, so the player still walks nearly the full act — with a more ordinary deck.

### The offer odds, computed from the pool rather than measured

Weighted draw without replacement, three offers, per act composition:

| act | rare in a given screen | ≥1 rare in the first 5 screens | ≥1 rare in a run |
|---|---|---|---|
| The Long Emergency | 13.5% | 51.4% | 86.8% |
| The Rising | 13.5% | 51.4% | 86.8% |
| The Reckoning | 12.9% | 49.9% | 85.6% |

**Half of all runs are offered a rare inside the first five reward screens**, ~87% see one at some
point, about 1.8 rare offers a run. That is the intended shape: an early rare is an event, not a
guarantee and not a lottery. Compute these from the authored pool — do not read them off take
counts, which confound the offer rate with whether the bot wanted the card.

### Why the game got harder

Under the flat pool every card was 1-in-14, so **Scavenged Rounds — the best card in the game in
every measurement ever taken — was offered as often as Shieldbearer**, which is dead. Flat
completion rates were quietly propped up by handing out the best card at common frequency. It is now
1 ticket in 69, and the 8-point drop across acts is almost entirely that correction.

So 19.0% is not a regression to fix by re-tuning weights. It is the first honest reading of the
reward pool, and the levers that should close the gap to 25% are content and life budget:

1. **Riot Shield at −0.45 is the only negative card in the game** and it is The Long Emergency's
   cheap themed card — LE is also the worst act at 16.7%. One statline, targeted at the worst act.
2. The life budget, if still short after that. `mean floor = life budget ÷ life lost per battle`
   has held for ten runs; life lost per battle is 29.6 and has barely moved across all three
   reward designs.

---

## Run 11 — 2026-09-16, `bot-1`, 900 runs, seeds 1-900 — Riot Shield, and a free control

One card. **Riot Shield 1-cost 2/14 → 8/10**, the only negative-delta card in the game.

| act | run 10 | run 11 | Δ |
|---|---|---|---|
| The Long Emergency | 16.7% | **20.7%** | **+4.0** |
| The Reckoning | 23.7% | 23.7% | **0.0** |
| The Rising | 16.7% | 16.7% | **0.0** |

Riot Shield itself: **−0.45 → +1.85**. Mean floor for LE 16.77 → 17.09. Overall 20.4%.

### An unchanged act replays EXACTLY, and that is a regression test

The two control acts did not come back close. They came back **identical, to the decimal** — same
completion, same mean floor, same life per battle. Riot Shield is a Long Emergency card and is not
in the other two pools, so those 600 runs are the same 600 runs, replayed.

That is worth more than the error bar it was run to measure:

- **A themed-card change is perfectly attributable.** No sampling noise to argue about, because
  there is no resampling — the unaffected acts are a byte-identical replay.
- **Any drift in an act that does not hold the card is a LEAK, not noise.** Shared state, an
  RNG stream crossing themes, a "themed" card that reached the shared pool. This control costs
  nothing and should be read on every themed-card change.
- It does not generalise to shared-pool cards or to life-budget changes, which resample everything.
  There the n=300-cannot-rank-acts finding from run 9 still governs.

### The statline finding held a third time

Power pays and toughness barely does. 2/14 → 8/10 is **two fewer total stats** and the card went
from the worst in the game to comfortably positive. Shieldbearer (1-cost 4/12, +0.16) is the same
shape and the same dead card, and is now the obvious next candidate — the prediction is that
shifting it toward power lifts every act at once, since it is shared.

---

## Run 12 — The Rising's per-turn rewrite, and why it FAILED

Gravedigger and Pyre Tender were moved from `OnDeath` to `OnTurnEnd` on a frequency argument: a
turn trigger fires ~40-50 times a run against 5-10 for a death. The Rising went **16.7% → 12.0%**.

| card | was | became | |
|---|---|---|---|
| Gravedigger | 8/6, on death: 16 | 6/6, each turn: 3 | +0.43 → **−2.77** |
| Pyre Tender | 10/8, on death: 12 | 8/8, each turn: 3 | +0.86 → **−1.30** |
| The Choirmaster | 12/18, doom only | **stats unchanged**, per-turn ADDED | +0.40 → **+0.91** |

**`OnTurnEnd` only pays if the unit is still standing at the end of the turn.** The 40-50 figure
assumed survival. A 1-cost body in a contested lane usually does not survive, so the trigger fired a
fraction of the costed number. Almoner — the card that makes this trigger look strong at +1.83 — is
a **2/8**: the toughness IS the engine, and that is the opposite of the vanilla-body rule that power
pays and toughness barely does. **Both rules are true, for different cards.** A body that fights
wants power; a body that hosts a repeating effect wants to survive to fire it.

Two process notes:

- **I moved two levers again.** Both failed cards had statline AND trigger changed together.
  Restoring the statlines alone left it at 12.0%, which is what identified the trigger — but that
  cost an entire 900-run measurement to learn what one lever would have told me directly.
- **The Choirmaster was an accidental control** and it is the only reason the experiment produced
  anything: stats untouched, effect added, +0.51. Without it the whole run would have read as
  "per-turn triggers are bad", which is false.

Gravedigger and Pyre Tender are reverted; The Choirmaster keeps its added line.

---

## Run 13 — 2026-09-16 — the battles were too long, and it was structural

**Found by hand-play, not by the harness.** Battles ran 8-9 turns from floor 5 on, with individual
battles of 40-55 turns. The sim had been reporting `turns per battle 7.9` for ten runs and nobody
read it as a defect.

**The mechanism.** You damage the Opponent only through OPEN lanes — a lane where your unit faces
no enemy. Enemy count is `2 + floor/6`, capped at the lane count, 5.

| | floor 1 | floor 6 | floor 13 | floor 18+ |
|---|---|---|---|---|
| enemies | 2 | 3 | 4 | **5** |
| open lanes | 3 | 2 | 1 | **0** |

**At five enemies there are no open lanes and the Opponent cannot be damaged at all** until you kill
something, while it heals 2-8 a turn. That is the 55-turn tail.

Enemy and Opponent health were both cut **30%**, and then the life budget from 200 to **120**:

| | before | health −30% | +life 120 |
|---|---|---|---|
| turns per battle | 7.9 | 5.1 | **4.9** |
| floors 5-7 | 7.9 / 8.9 / 9.0 | 5.4 / 5.9 / 5.8 | — |
| life lost per battle | 29.6 | 22.2 | **17.0** |
| dooms fired per run | 20.6 | 14.6 | **12.1** |
| **dooms DODGED** | **6.4%** | 21.3% | **24.3%** |
| completion | 20.7 / 23.7 / 12.0 | 86.7 / 64.0 / 71.3 | **28.0 / 33.3 / 18.7** |

### The dodge was the real bug

`KinJam.md` promises three outcomes: kill before the first firing (untouched deck, no power), ~8
turns (rewritten twice), ~15 turns (unrecognisable). At 6.4% **the first row did not exist** — every
battle was the middle one. The game had one lane where the design called for three, and no single
number said so. Battle length was the symptom; the missing choice was the defect.

### Life budget moves difficulty WITHOUT moving battle length

Turns per battle went 5.1 → 4.9 across a 40% life cut: unchanged. That is what makes it the right
difficulty lever once pacing is set — pacing and difficulty are separable here, and the life budget
is the knob that only touches the second.

It overshot its own prediction (mean floor ~17.9, predicted ~15) because **life lost per battle fell
too**, 22.2 → 17.0. Fewer and shorter battles mean fewer dooms (14.6 → 12.1) and dooms do direct
damage. `mean floor = life budget / life lost per battle` still holds, but the denominator is not
independent of the numerator — cutting life cuts the bill as well as the wallet.

### Still open

**The stalemate tail survived.** Max turns per battle is still 55/44/40 on the floors that field
five enemies. Cutting health lowered the mean and left the tail, because the tail is the zero-open-
lane structure, not the health totals. **Capping enemies at 4 is the targeted fix** and is one
constant.

---

## Run 14 — 2026-09-16 — the doom clock SCALES, and that is what saved the dodge

Asked for: dooms more often, battles shorter. Delivered both, and nearly lost the design's first
outcome doing it.

**Step 1, flat clock.** Every countdown cut by 1 (floor 2) and enemies capped at 4:

| | before | flat clock + cap |
|---|---|---|
| dooms fired per run | 12.1 | **18.6** |
| turns per battle | 4.9 | 4.7 |
| **dooms DODGED** | **24.3%** | **9.7%** |

More dooms and shorter battles, both as asked — and **dodging collapsed**. Against a 4.7-turn battle
a 2-turn fuse fires before you can finish, so "kill it before the first firing" stopped existing for
the second time this session.

**Step 2, scale the fuse by band.** Band-1 openers back to their original clocks, band 2 and 3 left
short:

| band | dooms | clock |
|---|---|---|
| 1 | Flood, CivilUnrest | 4 |
| 1 | Zombie | 3 |
| 2-3 | everything else | 2 |

| | before | flat | **scaling** |
|---|---|---|---|
| dooms fired per run | 12.1 | 18.6 | **16.7** |
| turns per battle | 4.9 | 4.7 | **4.8** |
| dooms dodged | 24.3% | 9.7% | **22.4%** |
| completion | 28.0 / 33.3 / 18.7 | 32.3 / 35.7 / 18.0 | **34.3 / 29.0 / 20.7** |

**+38% dooms over baseline with the dodge intact.** The flat clock was not buying frequency with
difficulty — it was buying it with an OUTCOME, and completion barely moved either way (about four
points across both steps) while the dodge halved and doubled.

### Tune the clock against the DODGE RATE, never completion

This is the transferable finding. Completion rate is nearly blind to the doom clock: it moved 4
points while the dodge went 24.3 → 9.7 → 22.4. A tuner watching the headline number would have
shipped the flat clock and never seen that one of the three promised outcomes had been deleted.

**Any number that is the whole point of a mechanic needs its own line in the table.** `dooms dodged`
has been printed all along; run 13 is where it was first read as a design metric rather than trivia.

### The enemy cap worked, and did not fix the tail

`2 + floor/6` capped at `LaneCount - 1` so one lane is always open. On the floors that used to field
five it is decisive — **floor 18's worst battle went 14 turns to 11, floor 20's 18 to 12**, and
those are now among the shortest battles in the act.

**The tail moved to the middle and is a different bug.** Worst battles are now **50 turns on floor
6, 49 on floor 10, 32 on floor 7** — floors that field two or three enemies, where open lanes were
never the constraint. Prime suspect is stacked healing: `The Choir` heals 2 a turn, `Gravecaller` 4,
the `Zealous` trait another 4, against roughly 12 damage a turn through two open lanes. **The mean
is healthy and only the worst case is broken**, which points at a cap on how much healing can stack
on one Opponent rather than at any of the numbers individually.

---

## Run 15 — 2026-09-17 — COMBAT v3, and the act that lives or dies on its DECK

**`bot-1/v3`, 150 runs, 50 per act.** The first measurement of combat v3: units withdraw at the end
of the turn, any lane is playable, the held unit is discarded with no refund.

**The `/v3` in the weights id marks a change to the GAME, not the bot.** The search and every weight
are untouched from `bot-1`. **Nothing below is comparable to runs 1-14** — a win rate is a property
of (game, bot) and the game changed underneath.

### v3 did NOT need the total rescale it was predicted to need

| act | completed | mean floor | per-floor clear, bands 2/3/4 |
|---|---|---|---|
| The Long Emergency | **28.0%** | 11.64 | 77% / 97% / 94% |
| The Reckoning | 20.0% | 13.42 | 87% / 97% / 82% |
| The Rising | **0.0%** | 8.86 | 71% / 69% / 67% |

Target band is 25-50%. **Two acts landed in or beside it with no tuning at all**, against a plan that
said every number in the game would move. Per-turn output did collapse exactly as predicted; the acts
absorbed it far better than predicted.

**The aggregate was 16.0%, and it is a lie in both directions** — it says "everything needs work"
when two acts are fine and one is dead. This is the third time that rule has earned its place.

### The stalemate tail HALVED, and the prediction was backwards

| | v2 (run 13) | v3 |
|---|---|---|
| worst battle | 50 / 49 / 55 turns | **33 turns** |
| median | — | 5 |
| p95 | — | 9 |

**Every long battle now ends in `Died`.** The design doc predicted v3 would make the tail worse:
flat per-turn output against reinforcements that scale on turn number. The mechanism was right and
**the sign was wrong**. In v2 a saturated board absorbed indefinitely while the Opponent healed, so
nothing resolved; v3 makes an impenetrable wall impossible, so the damage gets through and the battle
*ends*. A battle you cannot win is now a battle you lose in 30 turns rather than one that grinds for
55. **Ephemeral units fixed the tail as a side effect of fixing the stall.**

### Vampires was the single worst thing in the game, and it took an act with it

| | deaths | life lost |
|---|---|---|
| Vampires, as authored (countdown 2, heal 4, 8 to you) | **42.1%** | 46.0 |
| countdown 3 | 32.2% | 38.5 |
| countdown 3, heal 2, 6 to you | **24.6%** | 34.3 |

Against 0.9-12.3% and ~20 life for every other non-boss apocalypse. The Rising is the only act that
fields it, so each step was a genuinely single-variable experiment — and the other two acts returned
**numerically identical** results all three times, which is what says so.

**Why v3 broke it specifically: healing is priced against your damage PER TURN, and v3 collapsed
damage per turn to whatever 3 energy buys.** A v2 board accumulated and shrugged off 4 an enemy; a
v3 board cannot. On a 2-clock it also landed three times in a six-turn battle — 24 unblockable life
before an enemy swung.

**This generalises and the rest has not been swept yet.** Seven healing effects remain — `Gravecaller`
4 a turn, `The Choir` 2, `The Last Morning` 4, the `Zealous` trait 4, `Shepherd` 4 to every enemy,
`Chorister` and `Last Chorus` on death. Run 13 already had stacked healing as the prime suspect for
the v2 tail. **Every one of them got stronger when output went flat.** Sweep them as one pass.

The Rising went 0.0% → 2.0% → **6.0%**, mean floor 8.86 → 9.66 → **10.52**. Better, and not fixed.

### What is actually wrong with The Rising, and it is not difficulty

Life lost per battle, by floor, after the deck should have come online:

| floor | 9 | 10 | 11 | 13 | 14 | 15 | 17 | 18 |
|---|---|---|---|---|---|---|---|---|
| **The Long Emergency** | 14.3 | **-0.1** | **-0.4** | 1.7 | 10.7 | 3.4 | 16.3 | 14.1 |
| **The Rising** | 37.2 | 30.3 | 22.9 | 23.5 | 22.6 | 23.0 | 25.0 | 22.2 |

**The Long Emergency stops paying for battles. The Rising pays ~23 life a battle for ever.** That is
not a difficulty curve, it is a deck that never comes online — and no amount of tuning Vampires
touches it, because the leak is on floors Vampires does not reach.

**v3 raised the stakes on deck quality enormously.** In v2 an accumulated board could carry a weak
deck; in v3 the deck IS your entire per-turn output, so an apocalypse that fails to improve it costs
you every remaining floor.

Read against the acts' own apocalypses:

- **The Long Emergency** — AI Uprising rewrites what is standing, Grey Goo replicates it. Both hand
  the deck something. Life per battle falls to nothing and the act clears 28%.
- **The Rising** — **Zombie** adds 2/2 Zombies, which is quantity that dilutes a deck you now draw
  your whole turn from; **Vampires** is battle scope and leaves nothing behind at all; **Hell
  Uprising** is +6 power and **-2 toughness**, and in v3 toughness is your blocking *every single
  turn*, so it is close to a straight downgrade.

**The doc's own rule is the diagnosis: "every PERMANENT doom converts one resource into another,
none are purely bad."** The Rising's do not, and v3 is what made that fatal rather than merely weak.
It was already the weak act at 20.7% in v2 for this reason; v3 took it to 0.

**So the fix is content, not a knob**, and it is the same one run 12 got wrong by reaching for the
obvious per-turn rewrite. Do not tune The Rising further before deciding what Zombie and Hell
Uprising are supposed to PAY.

### Method notes

- A content rebalance of this size broke **zero** of 118 tests, which is what "tests read authored
  values, never restate them" buys. Twelve broke the last time that rule was violated.
- `sim 150` is 50 runs an act and ~6 minutes. Enough to separate 28% from 0%; not enough to argue
  about 20% against 25%.

---

## Run 16 — 2026-09-17 — Ash gets an ability, and it is worth more than DOUBLING his body

**`bot-1/v3`, 150 runs, 50 per act, three configurations.** The first measurement of a companion that
does something. Ash was a 2/6 with no ability — a third of a one-drop, in the lane nothing contests,
whose "upgrades" were stat trickle chosen for him.

| Ash | The Long Emergency | The Reckoning | The Rising |
|---|---|---|---|
| **2/6, no ability** (run 15) | 28% | 20% | 6% |
| **6/12, no ability** | 50% | 30% | 12% |
| **6/12, +2/+0 per unit that died last turn** | **82%** | **68%** | **20%** |

Target band is 25-50%.

### The ability is worth more than the body, by a long way

Doubling Ash's stats bought **+22 / +10 / +6** points of act completion. Adding the ability on top
bought **+32 / +38 / +8** more.

**That is the answer to "Ash is irrelevant to gameplay".** One card went from an afterthought to the
most important thing in a run — more important than a stat line twice its size. It is also far too
strong: two acts blew straight through the top of the band.

**Both numbers were needed.** Shipping the body and the ability together would have left it
impossible to say which mattered, and "the companion is now good" would have been indistinguishable
from "the companion is now bigger". Isolating cost one extra `sim` run.

### Why the cumulative reading is the strong one

The ability was specified as *"+2/+0 per unit that died last turn"*, and that has two readings:

- **Recalculated** — a bonus that rises and falls with each turn's deaths. Needs a CONTINUOUS effect
  layer, which this game deliberately does not have.
- **Cumulative** — a triggered `BuffAction` that stacks within a battle and resets between them.
  Free, using the effect system as it already is. **This is what shipped.**

It was chosen as the cheap one, and the measurement says it is also **much** the stronger one: a
grinding six-turn battle can hand Ash +12 power. If the ability needs to come down, the recalculated
reading is not merely "the other option" — it is a different power level, and the one originally
described.

### It only works because withdrawn ≠ dead

The ability reads units the enemy KILLED, not the four that walk off the board at the end of every
turn. That rule was written as tidiness in v3 phase 1 and is load-bearing now: without it, a
companion that pays for your losses would be paid every single turn by your own board doing what it
always does. `WithdrawingDoesNotFeedAsh` pins it.

### The Rising is still the weak act, and this is more evidence for the deck diagnosis

20% against 82% and 68%, having gained the least from Ash in both steps (+6 then +8, against +22/+32
and +10/+38). **Ash helps least in the act whose problem is its deck**, which is what run 15 said:
The Rising's apocalypses do not pay the deck, and v3 made the deck the whole of your per-turn output.
A stronger companion cannot fix an act that never comes online — it just delays the bill.

### Also landed, and not yet measured

- **`CountOf`** — effect amounts can be multiplied by something the board answers. Every amount in
  the game was a literal before this, which is most of why the pool had no synergies: they were not
  *unwritten*, they were **unsayable**.
- **`BuffAction`** — stat changes, triggered and one-shot. No continuous layer, on purpose.
- **`AdjacentLanes`** targeting — the first reason in the game's history to prefer one lane to
  another. Lane choice is the only decision here and it was very nearly arbitrary.
- **Two cards stopped lying.** Almoner and The Choirmaster said "each turn: gain N" and fired
  **once** — v3 withdraws the unit that would have fired them again. Rules text is not cosmetic.
- **`OnTurnStart` is now unusable by cards** and a test enforces it: the field is empty when it
  fires, so a card carrying it would be silently inert. The companion is the one legal holder, which
  is exactly what Ash uses.

---

## Run 17 — 2026-09-17 — the pool re-cut for v3, and a table that was lying

**`bot-1/v3`, 150 runs, 50 per act.** The card pool rebuilt around what v3 actually rewards: no
3-costs, deaths READ rather than triggered, and three synergy axes that could not be written before
the `CountOf` / `BuffAction` / adjacency primitives existed.

| act | run 16 | run 17 | |
|---|---|---|---|
| The Long Emergency | 82% | 78% | too easy |
| The Reckoning | 68% | **90%** | far too easy |
| **The Rising** | 20% | **28%** | **in band at last** |

The Rising has now gone **0% → 2% → 6% → 20% → 28%** across four changes. Target is 25-50%, so for
the first time since v3 landed, every act is in band or above it. **The problem is now that two acts
are too easy**, which is a much better problem than an unplayable one.

### THE CARD VALUE TABLE LIES ACROSS ACTS, and it nearly cost a good redesign

The headline table said The Rising's four cards were the four worst in the game: Pyre Tender **-2.96**,
Blood Price **-2.72**, The Choirmaster **-2.36**, Gravedigger **-1.90**. Every one negative. Read at
face value, the re-cut had failed.

**It had not. The table was measuring the act.** An act-specific card is only ever taken inside its
own act, so its "with" average is that act alone while its "without" average is all three. The
Rising's mean floor is 15.74 against 19.46 and 19.78 — a gap of nearly four floors that gets charged
to every card the act ships.

Recomputed **within** The Rising:

| card | across acts | within its act |
|---|---|---|
| Gravedigger | -1.90 | **+2.51** |
| Blood Price | -2.72 | +0.58 |
| The Choirmaster | -2.36 | +0.45 |
| Pyre Tender | -2.96 | -0.05 |

**This is the same disease as "never report one balance number across more than one act", in a table
nobody had audited for it.** Compare a themed card only against runs of its own theme. A shared-pool
card is safe to read across all three; a themed one is not, ever.

### Deaths as a READ, not a trigger — the redesign is vindicated

Gravedigger was **-0.26 in run 15, the only negative card in the game**, carrying "on death: 16 to
the Opponent" — a trigger v3 broke, because a unit withdraws at end of turn instead of dying. Rebuilt
as "8 to the Opponent per Loss", reading `CountOf.DiedLastTurn` across your whole board rather than
requiring this card to be the corpse, it is **+2.51 within its act** and the most valuable card The
Rising ships.

**The mechanic was not the problem; the direction of it was.** Nothing needed to die *itself* — the
act needed to be paid for having *lost* things.

### What the re-cut did

- **Nothing costs 3.** Every 3-cost became a 2-cost with smaller numbers. Run 15 measured why: a
  3-cost spends the whole turn holding ONE lane where two 1-costs hold two.
- **Three axes that were previously unsayable.** Adjacency (Siege Ram, Warden), volume (Scavenged
  Rounds, Almoner), and the clock (Long Watcher, Reactor Crew). All positive within their acts.
- **Card NAMES were deliberately not changed.** `KinArt` resolves art by name from 52 authored
  SVGs, so a rename silently downgrades a card to a generated figure. Re-stat and re-ability the
  names that exist; treat a new name as an art debt taken knowingly.
- **Card text was too long and got shortened onto keywords.** "8 to the Opponent for each unit that
  died last turn" does not fit a card face — `Loss` and `Adjacent` are keywords now, with reminder
  text on hover, and the text reads "8 to the Opponent per Loss". KinJam.md's rule: when the text
  does not fit, the text is wrong, not the box.
- **Warden and Rust Golem were both 2-cost 10/14**, one with an ability, which made the vanilla one
  strictly worse. Caught by reading the rendered content dump, not by any test. Warden is 8/16 now.

### A test that named a card, and why that was the bug

`AUnitsDoomTriggerFiresWhenTheApocalypseLands` loaded **Reactor Crew** by name and asserted life went
up. The re-cut gave that card an OnPlay effect, and the test failed with "healed nothing" — which
reads like a broken trigger rather than a moved ability. It now selects whichever card in the act
*has* the trigger and asserts the precondition explicitly, so a future re-cut is free to move
abilities and a genuinely missing trigger still fails loudly.

The neighbouring Gravedigger test defines its card **inline** and kept passing throughout, which is
exactly why that rule exists.

---

## Run 18 — 2026-09-17 — three fixes from a real playtest, one of them a genuine bug

**`bot-1/v3`, 150 runs.** A human played it. Everything below came from that session, and the second
item is something no amount of `sim` was going to surface.

| act | run 17 | run 18 |
|---|---|---|
| The Long Emergency | 78% | 86% |
| The Reckoning | 90% | 82% |
| The Rising | 28% | 20% |

Three changes pulling in different directions: capping healing made enemies weaker, and the Ash nerf
plus Field Dressing's exhaust made the player weaker. **The Rising fell furthest because it leans on
Ash's attrition synergy hardest** — the act built around losses lost the most when the thing that
paid for losses was cut. The spread 86/82/20 is now the problem rather than any single number.

### Healing could raise its own ceiling, and that was a real bug

`DealDamageAction` set `MaxHealth = Math.Max(MaxHealth, healed)`, so an overheal **moved the
maximum up**. `Gravecaller` heals 4 a turn and sits at full health, so it grew by 4 every turn,
without bound, for the whole battle — and the `Shepherd` trait did it to an entire enemy line at
once. The Choir, The Last Morning and the `Zealous` trait did it to the Opponent.

Reported as *"they can heal past their original health, which makes them super hard to beat,
especially when they start to take over all the lanes."*

**The old behaviour was argued from DISPLAY** — the lane cell draws health against MaxHealth, so
clamping the bar "would show a lie". That reasoning was about a health bar and it cost the game its
difficulty curve. **Healing restores; if something should get bigger, that is a `BuffAction`, which
says so on the card.**

Run 15 had already flagged that seven healing effects were systemically over-priced under v3 and
needed a sweep. This was the sharp end of it and it was not a pricing problem at all.

**The player named the wrong enemy** — Herald of the End has no healing, only "on death: 4 to you".
The diagnosis was still exactly right. *What* a player reports is often wrong; *that* they hit
something is not.

### An expiring buff needs no duration system

Ash's ability shipped cumulative and was called OP in one session, which matches run 16 measuring it
at +32/+38/+8 points. It is now one turn only, and the implementation is **the same buff negated on
the opposite trigger** — `OnTurnStart` +2 per Loss, `OnTurnEnd` -2 per Loss. No duration field, no
continuous layer, no new action.

**The constraint is sharp and worth writing down: this only works for a count that cannot change
within a turn.** `DiedLastTurn` is written once by `StartTurnAction` and fixed until the next, so
both firings read the same number and the unwind is exact. The same pattern with
`CardsPlayedThisTurn` would apply a small buff and remove a large one, quietly draining the unit.

### Exhaust, and why a small deck needed it

v3 discards the hand every turn and reshuffles Discard the moment Draw runs dry, so a ~20 card deck
is seen over and over. A 1-cost "gain 12 life" came back roughly every other turn and healing stopped
being a decision. `Exhaust` sends a card to its own zone for the rest of the battle — **battle scope,
so the run deck is untouched and it is back next fight.** Nothing but a doom transform may remove a
card from a run, and that rule was not bent for this.

---

## Run 19 — 2026-09-17 — the bot never declined a reward, and runs 1-18 were measured on that

`RunSimulator` took a card after **every** battle. The Godot reward screen has always offered a skip
— `KinIntermission.OfferRewards` says so out loud — but the sim never used it, so **every number in
this file up to run 18 describes the most bloated deck the game can produce**, not the one a player
builds.

With a flat 25% decline:

| | deck at end, before | after |
|---|---|---|
| The Long Emergency | 46.4 | 42.7 |
| The Reckoning | 16.0 | 14.4 |
| The Rising | 22.0 | 19.1 |

**Completion barely moved** — 86/82/20 to 86/80/22. Four fewer cards is not what decides these runs,
which is worth knowing before building any deck-thinning mechanism: **bloat at this level is not the
constraint.** It will matter more once acts chain, where never declining would build a fifty-card
deck nobody would own.

The skip rate is a flat guess and the only guess in that file. A picker that reads the measured
card-value table would replace both it and the random pick — and would make the per-card numbers
mean something different, so it needs its own run when it lands.

### Method note

The first attempt at this measurement **silently did not run**. The command chained
`dotnet build | grep -c error && dotnet run`, and `grep -c` exits non-zero when it matches nothing —
so "0 errors" short-circuited the sim and the analysis read the PREVIOUS results file. It was caught
only because the deck size came back byte-identical to the run before it.

**An unchanged number is evidence of a change that did not happen, not of a change that did
nothing.** Check the results file is new before reading it.

---

## Run 20 — 2026-09-17 — three acts in one run, and a curve that is all boss

**`bot-1/v3`, 120 runs, 45 floors each.** A run is now every act in a fixed order — The Reckoning,
The Long Emergency, The Rising — with 15 floors an act, 8 battles, two shops, two rests, full heal at
each act break and gold that survives it.

**The structure works.** Runs reach floor 45, each act runs its own doom schedule from its own first
band, and mean floor reached is 26.04 of 45. **The difficulty curve does not.**

### Every ordinary floor is free and every boss is a wall

| floor | what | deaths |
|---|---|---|
| 1-13 | ordinary | **1 death in ~1,400 battles** |
| 15 | act 1 boss, Ashfall | 46 of 117 — **39.3%**, 40.3 life |
| 30 | act 2 boss, Detonation | 53 of 71 — **74.6%**, 104.6 life |
| 45 | act 3 boss, The Last Host | 11 of 18 — **61.1%**, 113.2 life |

Life lost per ordinary battle is **10.9**, down from 21.9 in a single-act run. Turns per battle fell
5.5 → 4.3. **113 of 120 runs died, and essentially all of them died on a boss floor.**

Two causes, and they compound:

1. **Ordinary floors got easier and healing got more generous.** Content is chosen by the floor's
   position within its act, so act 2 restarts at act 1's early roster with only a stat multiplier on
   top — and now there is a full heal at every act break as well as the rests.
2. **The boss floors got the multiplier too, on top of numbers already tuned to be a finale.**
   `TheLastMorning` is the Opponent on every act's last floor (`MinFloor = ActLength`), so act 3
   fights it at 2.4x health *and* eats The Last Host, which was authored as the end of a whole run.

**A guessed multiplier applied uniformly is the mistake.** `HealthScaleFor` moves the ordinary
floors and the boss by the same factor, and they needed opposite corrections — the ordinary floors
wanted more, the late bosses far less.

### What to do next, in order

- **Scale the ordinary floors up and the boss floors down**, separately. They are not one dial.
- **Reconsider the full heal at an act break.** With 8 battles an act at 10.9 life apiece, an act
  costs ~90 of 120 life and then hands all of it back; the budget does not bind anywhere except the
  boss. A partial restore is the obvious first thing to try.
- **A per-act Opponent for the boss floor.** One Opponent for all three finales is the root of the
  spike; it is content, not a number.

### Note on the sim itself

`RunSimulator` **walks past shops without spending**, because the bot cannot shop. Every number here
therefore describes a run that never bought or removed a card — recorded rather than faked, since a
random purchase would put noise in the table and call it a measurement. Gold accumulates unspent.

---

## Run 21 — 2026-09-17 — the curve pass, and a boss that was a cliff rather than a dial

**`bot-1/v3`, 120 runs, 45 floors.** Four iterations on the chained run's difficulty. Run 20 left
every ordinary floor free and every boss a wall.

| | run 20 | **run 21** |
|---|---|---|
| act completed | 5.8% | **24.2%** |
| mean floor of 45 | 26.04 | 28.12 |
| act 1 boss deaths | 39.3% | **11.2%** |
| act 2 boss deaths | 74.6% | **6.0%** |
| act 3 boss deaths | 61.1% | **14.7%** |

Deaths are spread now instead of concentrated: floors 7-9 (34), 15 (9), 23-30 (8), 37-39 (21),
41-45 (11). The pressure points are the middle of act 1 and the middle of act 3, with the finales as
real but survivable spikes. Target band is 25-50%; 24.2% is marginally under and close enough to
leave until the shop exists, since gold is currently unspendable.

### The sim had stopped showing two thirds of the run

`SimCommand`'s survival table looped `floor <= Run.ActLength`. Chaining acts changed what `ActLength`
means, so the table quietly printed **only floors 1-15** while looking like a complete table — act
2's and act 3's curves were invisible, and act 2's boss was being tuned blind for two iterations.

**A table that silently narrows is worse than one that errors.** It was found by noticing that a
45-floor run had no rows past 15.

### One multiplier cannot serve a floor and a finale

Run 20 applied a single act multiplier to both. They needed opposite corrections, so they now have
separate dials — `HealthScaleFor` / `AttackScaleFor` for ordinary floors, `BossScaleFor` for the last
floor of an act. The ordinary attack scale also needed a base ABOVE 1.0 (1.2), because act 1's
multiplier is 1.0 by definition and act 1 was where the problem was.

### The boss was a THRESHOLD, and no multiplier can sit on a threshold

`TheLastMorning` — the Opponent on all three finales — healed 4 a turn. That makes the fight a race
with a cliff: out-damage the heal and it folds, fall short and it is unkillable. The measurements
show the cliff plainly:

| boss scale | act 1 finale deaths |
|---|---|
| 0.8 | 6.2% |
| 1.0 | 42.2% |

**A 25% change in one dial swung the outcome sevenfold.** Detonation did the same going 1.7 → 1.25:
74.6% → 5.4%.

So the heal was removed and health raised to pay for it — **a health total is linear and tunable; a
heal race is a cliff.** The first attempt overpaid, 140 → 200 when the heal was worth about 28 over a
seven-turn fight, and act 1's finale got *worse* (42.2% → 47.5%). At 170 it lands at 11.2%.

**The dial is now stable, and the real fix is still content.** One Opponent fights all three finales.
Giving each act its own boss would let `BossScaleFor` be deleted rather than tuned.

### Healing had to come down before anything else could be read

A full restore at each act break made the run three independent acts — nothing spent in act 1 could
cost you in act 2, so the only floor that could kill you was whichever one spiked. It is half of max
now. With that plus the ordinary-floor correction, life lost per battle is 9.6 and the budget
finally binds somewhere other than a boss.

---

## Run 22 — 2026-09-18 — a boss per act, and gold with somewhere to go

**`bot-1/v3`, 120 runs, 45 floors.** Two changes: each act ends on its own authored boss, and shops
sell cards, healing and **card removal**.

| | run 21 | **run 22** |
|---|---|---|
| act completed | 24.2% | **25.8%** |
| mean floor of 45 | 28.12 | **33.56** |
| deck at end | 31.9 | 31.6 |

Deaths spread across floor 9 (17), 15 (7), 23-24 (7), 30 (6), 38-39 (23), 43-45 (22). Mid-act-1,
mid-act-3 and the finale — which is the shape a three-act run should have.

### Deleting a dial beat tuning it

`BossScaleFor` is **gone**. It existed only because one Opponent fought all three finales, separated
by a multiplier — and a boss is a race, so that multiplier sat on a cliff rather than a slope
(run 21: a 25% change swung act 1 from 6.2% deaths to 42.2%).

Three authored bosses replaced it, and the numbers immediately said things a multiplier could not:

| | first pass | corrected | deaths now |
|---|---|---|---|
| The Last Warden (act 1) | 150 | 210 | 6.9% |
| The Choir (act 2) | 220 | 250 | 7.2% |
| The Last Morning (act 3) | 260 | **190** | 36.7% |

**Act 3's boss had to get SMALLER than the others, not bigger.** The Last Host — that act's
apocalypse — costs 80.4 life on its own, so the body standing behind it is not what makes the fight.
No multiplier could ever have expressed that; it is only sayable because the three numbers are
independent.

**No boss heals**, and that is now a rule with a test behind it. Healing makes a finale a threshold,
and thresholds cannot be tuned.

### Splitting the rosters broke two things, and both were right to break

Bosses were in `AllOpponents`, so act 1's boss was also the ordinary Opponent for floors 10-14 — you
fought the finale five times before reaching it — and the untraited boss collided with the untraited
ordinary instance of itself. `TheActEndsOnABossFoughtNowhereElse` and
`NoOpponentIsFoughtTwiceWearingTheSameTrait` caught both.

That leaves **one ordinary Opponent**, so its identity now comes entirely from traits — and seven
could not cover an act's ten ordinary battles. Three were added. The rule the old doc stated ("at
least as many traits as the longest span any one Opponent holds") survived the restructure and is
what flagged the shortfall.

**Adding a second ordinary tier back needs authored art.** There are four Opponent SVGs and the
three bosses hold the other three.

### The shop

Cards, healing, and card removal — the first thing other than an apocalypse that can take a card out
of a run. Removal is priced to rise with each use (`Run.CardsRemoved`), because thinning is the
strongest thing gold can buy under combat v3 and a flat price would make it the only purchase worth
making. `Run.MinDeckSize` floors it at 8: `HasNoCards` is a LOSS, and removal is the first thing a
player can choose that could reach it.

**The bot now shops, on a crude priority** — thin, then heal if badly hurt, then buy. That is a floor
on how well shopping can go, not a model of how a player shops, and it is the second guess in
`RunSimulator` after the reward skip rate.

### Still open

- **Act 1's finale still LOSES the player 3.4 life on average** — you leave it healthier than you
  arrived. 6.9% deaths is survivable but a finale should cost something.
- **No Godot shop screen.** The shop is engine, content and bot only, so it is measurable but not
  yet playable.

---

## Run 23 — 2026-09-18 — cutting the companion's marks took a third of the run with it

**`bot-1/v3`, 10 runs, same seeds before and after.** A small sample and enough: the shift is not
subtle.

| | with marks | **marks cut** |
|---|---|---|
| act completed | 10% | **0%** |
| mean floor of 45 | 33.5 | **23.0** |

**Marks were doing far more work than they looked like they were doing.** Each apocalypse survived
stamped +4 or +6 on the companion, and a 45-floor run eats roughly twenty firings — so Ash finished
a run at something like 6+40 power and 12+60 toughness. A stat trickle nobody noticed choosing was
quietly the largest single source of power in the game.

That is also the argument for cutting it. **A mechanic contributing that much while reading as
bookkeeping is a design problem, not a feature** — the player's own note was "I never liked this
mechanic", and the numbers say it was carrying the late game while doing it.

**The run needs retuning and has not been retuned here.** Recorded rather than patched, so the next
pass starts from a known state rather than from a number chosen to hide this one.

### Field Dressing: exhaust works, the card is just strong

Reported as *"I'm not sure it exhausts"*. **It does** — verified end to end against the SHIPPED card
rather than an inline one (`ExhaustContentTests`), including that a one-card deck cannot replay it
and that the flag survives `ToDoomCard`.

The card is simply strong: **1.69 copies per deck by the act-1 finale, at 12 life each, against 8.9
life lost per battle.** A typical deck heals more per fight than the fight costs, and drawing it —
not exhaust — is the only thing limiting it. Left alone; see `DesignNotes.md` for the condition
under which that stops being acceptable.

### Method note

The first version of the exhaust test measured LIFE across six turns and failed — life went *down*,
because the Opponent reinforces into an empty line and those bodies attack. **It was measuring the
battle, not the card.** Counting replays instead makes it a test of the one thing it is about.

---

## Run 24 — 2026-09-18 — buying back what the marks were paying for, and the 2-drop pass

**`bot-1/v3`, 25 runs a step.** Two jobs: make a 2-drop worth playing, and give back the power that
cutting the companion's marks took out of the run (run 23).

| | run 23 (marks cut) | **run 24** |
|---|---|---|
| mean floor of 45 | 23.0 | **36.00** |
| act completed | 0% | **12%** |
| life lost per battle | 11.1 | 8.7 |

**Mean floor is now better than it was WITH marks** (33.5). Completion is 12% against a 25-50%
target — recovered, not finished.

### The 2-drop rule, and it is about lanes rather than energy

A card costs energy AND a lane, and the lane is the scarce one. Three 1-drops fill three lanes for
about 42 total stats; a 2-drop plus a 1-drop fills two for (2-drop) + 14. **A 2-drop therefore needs
~28 total just to break even, and more to pay for the lane it gives up.** Every 2-drop in the game
was at 16-24. Vanilla 2-drops are 26-36 now; ones carrying an effect sit lower because the effect is
the rest of the card.

**It narrowed the gap and did not close it.** Mean card delta within the run: **1-cost +7.63,
2-cost +3.89, 0-cost +4.80.**

### And the reason is not "buff them more" — it is WHICH 2-drops

Among the well-sampled ones:

| card | line | delta |
|---|---|---|
| Bonepicker | 18/8 | **-7.20** |
| Feral Pack | 16/12 | **-4.38** |
| Rust Golem | 12/20 | +0.49 |
| Warden | 10/20 | +6.49 |

**Power-heavy 2-drops are still bad; toughness-heavy ones are fine** — and that REVERSES the v2
finding that "power pays and toughness barely does" (runs 8-12).

It reverses because v3 changed what a stat is worth. A unit now deals its power once and absorbs up
to its toughness once, in the same single turn — so the two are symmetric per play, and act scaling
put far more incoming damage on the board than there is Opponent health to chew through. Toughness
saves life every turn; power only shortens a fight you were winning anyway.

**Do not treat the v2 card-value findings as current.** They were measured on a board that
accumulated, where a surviving body dealt its power again every turn and its toughness was spent
once. That asymmetry is what v3 deleted.

**Caveat, and it matters:** 25 runs, and these deltas compare a card's runs against the runs that
skipped it. Bonepicker was taken in 22 of 25, so its "without" group is three runs. Treat the
DIRECTION as the finding and re-measure the size at a larger n.

### Three floors were doing all the killing

Deaths by act, before this pass: act 1 **2 of 24 runs**, act 2 six (all on its boss), act 3 fourteen.
Difficulty was backloaded, not uniformly wrong.

- **Enemy health scaling: base 0.82, step 0.9 -> 0.55.** Your output is flat — 3 energy, every turn,
  for the whole run — so an act multiplying enemy health by 2.6 is not harder, it is a game where
  nothing you do arrives in time. Act 3 cleared 100% of its first band and 33% of its third.
- **The base was tried at 0.95** to put teeth in act 1, and **cost more than the back half gained**
  (mean floor 34.3 -> 31.1). Reverted. Act 1 being gentle is a PACING problem and the act multiplier
  is the wrong dial for it.
- **Vampires lost its heal**, for the third cutback this scenario has had. Healing is priced against
  damage per turn and v3 collapsed that; act scaling then multiplied the health it had to chew
  through. 61.5 life a battle -> 26.4, deaths 33% of its band -> 17.6%.
- **The last floor was double-dipping.** The Last Host deals face damage on a 2-clock, and The Last
  Morning — the only Opponent it is ever fought beside — also carries "when the doom fires: 10 to
  you". One firing was 26 unblockable; the fight killed 60% of the runs that reached it at 93 life.
  16 -> 10 on the doom, sweep untouched: **60% -> 40%.**
- The Choir (act 2's boss) 205 -> 175, and it was the single worst floor in that act.

### Still open

Completion is 12%, not 25%. The remaining killers are Vampires (17.6% of its band) and the final
floor (40%), and only five runs in twenty-five reach floor 45 — so that last number is thin and
should not be tuned again on this sample.

---

## Run 25 — 2026-09-22 — is the upgrade curve one exponential card? NO

**`bot-1/v3`, 200 runs a side, seeds 1-200, A/B on the upgrade pool itself.** The hypothesis was
that `FloorsPerUpgrade`'s violently non-linear dial — 82.5% / 23.0% / 1.5% for every floor, every
second, every third — was not a curve at all but **Echo**, the one upgrade that compounds
multiplicatively. `Companion.With` does `effects = effects.AddRange(effects)`, nothing filters an
already-taken upgrade out of the offer, so two Echoes is 4x and four is 16x.

**The hypothesis was wrong, and the A/B is unambiguous about it.**

| | act 1 | act 2 | act 3 | run complete |
|---|---|---|---|---|
| Echo in the pool | 69.0% | 63.0% | 49.4% | **21.5%** |
| **Echo removed** | 68.5% | **73.7%** | 47.5% | **24.0%** |

Removing the only exponential upgrade did not lower completion. It **raised** it 2.5pp — and on 200
runs the binomial standard error at p≈0.22 is ~2.9pp, so **the honest reading is no detectable
effect in either direction**, not "Echo is bad". Act 2's 10.7pp swing is ~2.6σ and the most likely
real number on the page, but it is one of three acts compared at once.

**So the `FloorsPerUpgrade` cliff is genuine compounding of twenty-four versus twelve versus eight
STAT upgrades, and the dial can be trusted as measured.** It still must never be interpolated.

### Echo does fire, and nothing proved that until this run

Verified on the board, not in the list: an echoed Pike deals **12** to the Opponent at end of turn
against a plain Pike's **6**. `EchoActuallyDoublesWhatTheCompanionDealsToTheOpponent` asserts it and
was confirmed to fail — reading 6 — with the `EchoesAbility` branch commented out.

**The two tests that existed could not have caught an inert Echo.** One asserts a symmetric
buff/unwind pair still nets zero, which is exactly what an Echo that does nothing produces; the
other counts `Effects` entries. Construction, not consequence — the shape this repo keeps
rediscovering.

**The helper that measures it needs an enemy in the companion's lane**, and the first version of the
test did not have one. An open lane sends the companion's power at the Opponent, so it read 14 and
20 rather than 6 and 12, and the ability's share was buried inside the body's.

### What the picks actually look like, which is the bigger finding

Upgrades taken across the 200 baseline runs, mean 11.0 a run:

| Steady | Thickset | Sharpened | Goring | Warding | Barbed | Echo |
|---|---|---|---|---|---|---|
| 460 | 436 | 418 | 266 | 263 | 252 | 98 |

**Three flat stat bumps are 60% of every pick made in the game.** The pool is seven items and a run
takes eleven, and `UpgradesFor` rebuilds the pool from the full list every floor — so the same stat
bump is re-taken about 2.3 times a run. **The handoff asked whether the upgrade pick is a decision
or a formality. Measured, it is mostly a formality.**

Echo stacking, baseline: 63.5% of runs took none, 27.5% one, **6.5% two, 1.5% three, 1.0% four.**
So 16x happened in two runs of two hundred.

### The one thing the sim cannot see

**`RunSimulator` picks an upgrade uniformly at random among the three and never declines**
(`RunSimulator.cs`, and the comment there says why declining is not modelled). So every number above
is what a RANDOM picker achieves. A player who understands that Echo is worth most taken last will
take it last, and the 2^n ceiling is reachable on purpose rather than by accident.

**That is a ceiling nobody designed, not a measured problem.** The measurement says Echo is not
breaking balance today; it says nothing about a competent player, because the bot is not one. Any
future greedy or measured-value picker changes what this whole table means — see the
`KinEvalWeights.Version` rule.

---

## Run 26 — 2026-09-22 — the Bulwark/Face vertical slice, and the first sim of anyone but Ash

**`bot-1/v3`, 200 runs a side, seeds 1-200.** The slice: Thorns, Strikes, Breakthrough, Flier;
nine archetype cards (five Bulwark, four Face); Harpy, Razorback and Flail Knight; companion
starters (seven generic cards + three of the companion's archetype); and the act pools folded
into one. `sim N companion=<Name>` is new, and **every results file before this run is Ash** —
`RunSimulator` called `NewRun(seed)` and never passed a companion.

| | act 1 | act 2 | act 3 | run complete |
|---|---|---|---|---|
| Ash, run 25 (before the slice) | 69.0% | 63.0% | 49.4% | 21.5% |
| **Ash, after** | 49.0% | 56.1% | 43.6% | **12.0%** |
| Bramble, generic starter (control) | 94.5% | 94.2% | 69.7% | 62.0% |
| **Bramble, Bulwark starter** | 95.5% | 97.9% | 71.1% | **66.5%** |
| Pike, generic starter (control) | 64.0% | 50.8% | 46.2% | 15.0% |
| **Pike, Face starter** | **82.0%** | 53.7% | 39.8% | **17.5%** |

The controls are the same build with `Companion.Starter` ignored, so each pair differs ONLY in the
three starter cards.

### Bramble is broken by herself, and always was

**62.0% on the generic starter, against a 25% target.** The Bulwark starter adds 4.5pp, inside
the ~3.4pp standard error at that rate — so the slice is not what broke her. She was never
measured: the previous session's roster had one test per companion proving each ability FIRES,
and no sim of any of them. Life lost per cleared battle on floors 1-9 reads 0.9, -0.1, 2.3, -0.2,
3.7, 3.0, 0.1, 4.5 — **she heals more than act 1 deals on most floors**, floors 1 and 2 included,
where no counter exists yet. "End of turn: 2 life per unit still standing" at four units is 8 a
turn against a battle that costs ~10.

**Nothing about Bulwark can be judged in play until she is fixed** — a playtest cannot find out
whether Thorns creates decisions in a run that cannot be lost.

### Pike's starter works, and does what a starter should

Act 1 **64.0% -> 82.0%**, +18pp on 200 runs, well outside noise. Act 3 is within noise of the
control. Three archetype cards matter most when they are three of ten, and matter less as the deck
grows — which is the shape wanted. Pike overall (17.5%) is the closest of the three to target and
has the healthiest per-act descent.

### Ash got harder, and floor 6 is where

Ash's starter is byte-identical to before, so the drop is the new enemies and the diluted pool.
Deaths moved to floors 8-9 (0 -> 14, 23 -> 56), the last battles before the floor-10 rest — so it
is attrition across act 1. Life lost per cleared battle, before -> after:

| f1 | f2 | f3 | f4 | **f6** | f7 | f8 | f9 |
|---|---|---|---|---|---|---|---|
| 6.7 -> 6.5 | 5.1 -> 5.5 | 8.7 -> 10.1 | 10.5 -> 11.5 | **11.6 -> 19.9** | 14.2 -> 17.4 | 11.5 -> 14.5 | 17.3 -> 15.7 |

**Floor 6 is the Flail Knight, and it LEADS there** — it is alone at `MinFloor` 6 and
`EnemiesFor` always leads with the highest tier a floor allows, so it is guaranteed in lane 0 on
floor 6 of every act. A deck with no Thorns eats ~8 extra life there three times a run. That is
the counter working as designed, but a GUARANTEED counter is not a matchup you prepare for — it is
a tax again, which is what the design philosophy says an enemy must not be.

### Still unmeasured

Tally and Moss have never been simulated. The reward picker is still uniform-random, so no number
here says how an archetype plays when you DRAFT toward it — only what its starter and its
companion are worth.

