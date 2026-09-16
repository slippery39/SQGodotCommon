# DOOMJAM balance, as measured by the bot

**Reproduce:** `dotnet run --project DoomConsole -c Release -- sim 1000`. Every table below is from
one such run; the raw file is in `doom_sim_results/` (gitignored — regenerate rather than quote).

**These numbers describe (game + bot), not the game.** The bot is `DoomBot`: it enumerates every
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

**There are no rest sites.** No map, no node types, nothing: `RunSimulator` and `DoomBoard` both
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
`DoomLaneCell` renders attack and life as PIPS, so doubling the scale doubles the pip counts, which
is a `DoomUI.md` question and not only a balance one.

---

## Run 8 — the act is completable

Three changes, in dependency order, each measured on its own.

**1. Every stat in the game doubled** — life, power, toughness, attack, enemy and Opponent health,
every effect amount, the companion's marks, the summon ramp, Nuclear's buff, the Zombie body,
the Irradiated draw cost. **Measured neutral, as predicted**: 11.04 → 11.17 mean floor, 2.5% → 2.5%
completions, 4.6 turns both. Tuning resolution doubled for free.

It cost 12 test failures, every one of them a test restating a content constant instead of reading
it. They now read the authored value, and `DoomTransforms.IrradiatedBuff` and `IrradiatedDrawCost`
are named constants rather than literals in six assertions. **The pip worry was unfounded** —
`DoomPalette.Pip` draws a numeric badge, not one pip per point, so the scale is invisible to the UI.

**2. Countdowns cut** — Flood 5→4, Ashfall 4→3, to fit the battles we actually have rather than
the ones the numbers were written for. Dodge 48% → 29%, firings 7.1 → 9.1 a run, for 0.27 floors.
**The cheapest change measured all session.**

**3. Rest floors, as content.** Every fourth floor is a `FloorKind.Rest` and the last floor never
is: 16 battles and 4 rests across the 20. `StarterContent.FloorKindFor` is asked by BOTH
`RunSimulator` and `DoomBoard`, with a test asserting no run ever fights on a rest floor — if those
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
every action**, and `DoomBot` simulates an entire `EndTurnAction` per candidate line, so one turn of
one battle allocates hundreds of states. A console app defaults to WORKSTATION GC: one shared heap,
every thread contending on it.

| same 300-run workload | time | per run |
|---|---|---|
| workstation GC | 693.5s | 2312ms |
| **server GC** | **224.2s** | **747ms** |

**3.1x, from two lines in `DoomConsole.csproj`.** The balance output was byte-identical across the
change — same completions, same mean floors, same per-theme numbers — which is the property a
performance change has to have before it can be trusted.

**Any project that drives this engine in bulk wants `ServerGarbageCollection`.** That is a finding
about `ImmutableGameObjects`, not about DOOMJAM: an immutable engine allocates per action by
construction, so bulk simulation is allocation bound by construction.
