# DOOMJAM v3 — implementation plan

**The design is in `DoomJam.md`** ("Combat v3", "Enemies must have PATTERNS", "TAG ALONG"). This file
is only the order of work, the files each step touches, and the traps found by reading the code
before writing any of it. **Do not re-derive the design from here.**

Starting state: commit `2219c49`, **113 tests green**, working tree clean, nothing built.

---

## The first playable cut

**Phases 1-4 alone fix all three playtest complaints.** Ephemeral units, replaceable lanes, a bot
that can still measure, and a rescale. Play it there before going further — everything after is
enrichment on a game that is already un-stalled and deciding something every turn, and playing it is
the only way to find out whether the enrichment is aimed at the right thing.

**There is no submission deadline** — the jam is no-AI, so this build is not eligible and is being
built for its own sake. That removes the reason to rush and promotes the project's *second* goal to
first place: **how hard is it to build a different card game on `ImmutableGameObjects`?**

Order still matters even without a clock. Do not build 5-7 half-done: a persistent card without
Piercing/Shifting is an auto-win, and a companion ability without a rescale is unmeasurable.

### v3 is a second combat rewrite, and that makes it a real measurement

DoomJam.md already records that switching to lanes was a **net deletion that touched no file above
`DoomCore/Actions/`**. v3 is a bigger change to the same layer, so it re-tests that claim directly
rather than taking it on one data point.

**Capture it as you go, in DoomJam.md "Engine findings":** after phase 2, what did `ImmutableGameObjects`
need? After phase 6, what did a new keyword cost? After phase 7, did the run/battle split pay a fifth
time? A finding written afterwards from memory is worth much less than one written the day it hurt.

---

## Phase 1 — units withdraw at end of turn  ✅ DONE

**116 tests green** (113 + 3 new), `sim 60` completes with **0 stalled**, Godot project builds.
Findings written up in DoomJam.md "Engine findings". Two things it turned up that the plan did not
predict:

- **A unit the APOCALYPSE killed was being withdrawn instead of dying** — no `OnDeath`, nothing in
  `DiedRunCardIds`, Zombie unpaid. The dead are cleared early in `EndTurnAction` and the doom
  resolves after that. Fixed by calling `ClearTheDead` a second time from `WithdrawUnitsAction`
  rather than writing a second account of dying. **Found by an NRE, not by any assertion.**
- **The preview is now only true on the turn the doom fires.** It reads the standing board, and the
  standing board no longer survives the turn. Nothing broke; two tests simply began describing a
  board that will not exist. **Phase 9 must not render it as a forecast on any other turn** — show
  it only at countdown 1, or label it "if it fired now". The standing rule is that the player must
  never plan around a lie.

**Also observed, for phase 3:** the bot's branching factor went up in phase 1 already, not just in
phase 2 — lanes no longer fill, so `OpenLanes()` stays wide every turn where it used to shrink.
`sim 60` ran at 2.4s a run. Measure budget exhaustion before trusting any number.

### What it took

**Files:** `DoomCore/Actions/EndTurnAction.cs`, a new `DoomCore/Actions/WithdrawUnitsAction.cs`.

**THE TRAP, and it is the whole phase.** `EndTurnAction.Execute` does not resolve the doom inline —
it calls `SpawnAction(new ResolveDoomAction ...)` and the spawn queue runs FIFO *after* Execute
returns. So **withdrawing inline in `Execute` would run before the apocalypse**, and every one of the
six `FiringRead.Standing` scenarios would read an empty board and silently do nothing.

So withdrawal is **its own spawned action**, queued after `ResolveDoomAction` and before
`StartTurnAction`. Target order inside one end-turn:

```
ResolveLanes -> ClearTheDead -> RefreshTheOpponentsLine -> [OnTurnEnd] -> DiscardHand
  -> tick countdown -> death checks -> [ResolveDoom] -> [OnDoomFires] -> [WithdrawUnits] -> [StartTurn]
```

**Withdrawn is not dead.** `WithdrawUnitsAction` moves each unit to Discard and does **not**: fire
`OnDeath`, append to `DiedRunCardIds` / `DiedThisTurnRunCardIds`, or raise `UnitDiedEvent`. It needs
its own event so the front end can animate it as leaving rather than dying.

The companion is skipped by `CompanionComponent` for now. Phase 6 replaces that check with the real
`Persistent` one and the special case disappears.

**Tests that break, and are right to break:** `AUnitHoldsItsLaneAcrossTurns` asserts the superseded
rule — rewrite it as `AUnitWithdrawsAtTheEndOfTheTurn`.

**Tests to add:**
- a unit played this turn is in Discard, not the Field, at the start of the next
- a withdrawn unit fires no `OnDeath` and is absent from `DiedRunCardIds`
- **`ADoomFiringSeesTheBoardYouCommitted`** — the ordering guard. Without this the phase looks done
  and six scenarios are dead.

**Exit:** suite green. Battles will be badly balanced and that is expected until phase 4.

---

## Phase 2 — any lane is playable, no refund  ✅ DONE

**118 tests green.** `sim 30` returned numbers **identical to phase 1** — 16.7% completion, mean
floor 11.43, 5.4 turns a battle, 21.8 life a battle.

**That identity IS the result, and it is the argument for phase 3.** The bot never replaces a unit
because `Candidates` only offers `OpenLanes()`, so the rule that was just added is invisible to the
only thing measuring the game. Any balance number taken before phase 3 is measuring v2-with-extra-steps.

**One thing the plan did not anticipate: the companion had to be exempted.** It is a unit in a lane,
so "replace whatever is there" would have discarded it — and it has nowhere to be discarded TO,
because Discard is drawable and the companion is meant to be the one thing that cannot be taken from
you. `SweepFieldAction` already spares it for the same reason. Playing into its lane is refused, and
that can never cause a dead turn because the other four lanes are always open. **Phase 7 deletes this
exemption** by making the companion something you place, at which point the question is where you put
it rather than whether you may build over it.

### What it took

**Files:** `DoomCore/Actions/PlayCardAction.cs`.

Delete the occupied-lane refusal in `ValidateAdd` (the `UnitInLane(Lane) is { } held` branch). In
`Execute`, move any held unit to Discard **before** placing the new one — by the same rule as
withdrawal: discarded, not dead, no triggers, no refund.

**Tests that break:** `ALaneHoldsOneUnitAndRefusesASecond` — rewrite as
`ALaneAcceptsASecondUnitAndTheFirstIsDiscarded`. Keep `ALaneOutsideTheBoardIsRefused` untouched;
lane bounds are still a real error.

**Test to add:** the replaced unit fires no `OnDeath` and does not feed Zombie.

---

## Phase 3 — the bot, BEFORE any rescale  ✅ DONE, and it was almost entirely unnecessary

**The plan was wrong about this phase, and measuring is what showed it.** Both feared problems were
checked and neither exists. What shipped is a version stamp and three comments.

**1. The node budget does not bind.** `sim 30 NodeBudget=200000` — ten times the default — returned
*identical* numbers: 16.7% completion, mean floor 11.43, 5.4 turns a battle, 21.8 life a battle. Only
runtime moved (2.4s → 3.9s a run). The search is not truncating, so the 2.4s is the cost of more
legal lines, not evidence of a cliff. **Zero code, and it settled the question the plan wanted a
prune written for.**

**2. `OpenLanes()` is already the correct candidate set.** The board is empty at the start of every
turn, so the only thing holding a lane mid-turn is something the search placed a moment ago —
replacing that is strictly worse than never playing the first card, because you paid twice for one
lane. The companion's lane is refused outright. So open lanes are the legal, non-dominated set, and
a prune would have been machinery guarding nothing. **This stops being true in phase 6**, when a
persistent unit can survive into a turn you did not place it on; widen it then and re-measure the
budget.

**3. The eval did not need re-pricing either.** `ScoreEndingTurnHere` scores the state *after*
`EndTurnAction`, and by then units have withdrawn — so the board term already prices only the
companion, which is exactly right: a board is worth nothing once the turn is over. What a unit was
worth is counted in `OpponentHealth`, `EnemyHealth` and `Life`. **Do not re-add a board term** to
make the bot "value its board"; it would pay twice for the same turn.

**Shipped:** `Version` is now `bot-1/v3` — the bot is unchanged, the GAME changed, and without the
stamp run 14 reads as a catastrophic regression against run 13 rather than as a different game.

### What the plan said to do

**This is the phase that will feel skippable and is not.** Every balance number comes from
`DoomBot`, and two things in it are now wrong. A rescale measured on a broken bot is thirteen runs of
`doom-balance.md` all over again.

**Files:** `DoomCore/Ai/DoomBot.cs`.

**1. `Candidates` enumerates `s.OpenLanes()`** ([DoomBot.cs:107](DoomCore/Ai/DoomBot.cs#L107)). In v3
every lane is legal, so the branching factor goes from roughly 10 to 25 per node. At depth 3 on a
flat 3 energy that is ~15k nodes against a `NodeBudget` of 20000 — so the search starts truncating
and **the measurement degrades without erroring.**

Prune: offer an overwrite only where it could plausibly be right — a lane held by a damaged unit, or
one whose power is below the candidate's. **Record the honest cost in the findings file**: any prune
means the bot undervalues replacement, so v3 sim numbers are a floor, not a true reading.

**2. `Score` prices a board that now evaporates** ([DoomBot.cs:166](DoomCore/Ai/DoomBot.cs#L166)).
`UnitPower` and `UnitToughness` value units as standing assets; an ephemeral unit is worth only what
it does this turn. Re-price, and **bump `DoomEvalWeights.Version` to `bot-2`** — the file already
warns that numbers measured under different weights are not comparable.

**3. `EnemyAttack` reads `enemy.Intent == IntentKind.Attack`** ([DoomBot.cs:172](DoomCore/Ai/DoomBot.cs#L172)).
Leave it; phase 5 revisits it when Piercing exists, or the bot will chump-block piercing for ever.

**Exit:** `sim 100` completes and a typical turn does not exhaust the node budget. Print the budget
exhaustion rate — if it is high, the numbers are not readable.

---

## Phase 4 — rescale  ⚠️ PART DONE, and it is not a rescale

**Full numbers in `docs/findings/doom-balance.md` run 15.** The headline: the total rescale this
plan predicted **was not needed**. Two acts landed in the 25-50% target band with no tuning at all.

| act | before | after | note |
|---|---|---|---|
| The Long Emergency | 28.0% | 28.0% | in band, untouched |
| The Reckoning | 20.0% | 20.0% | just under, untouched |
| The Rising | **0.0%** | **6.0%** | Vampires fixed; the act is not |

**Done:** Vampires, which was the single worst thing in the game — 42.1% of battles facing it ended
in death against 0.9-12.3% for every other non-boss doom. Countdown 2→3, heal 4→2, damage 8→6 took it
to 24.6%, in line with the rest. The other two acts returned numerically identical results at every
step, which is what makes it a clean single-variable result.

**Also measured, and the doc's prediction was backwards: the stalemate tail HALVED** — worst battle
50/49/55 turns in v2, **33** in v3, median 5, p95 9, and every long battle now ends in `Died`.
Ephemeral units fixed the tail as a side effect of fixing the stall, because an impenetrable wall is
no longer possible. **Delete the energy-ramp worry from DoomJam.md's cost list when convenient.**

### NOT done, and it is content rather than a knob — DECIDE BEFORE TUNING

**The Rising does not have a difficulty problem, it has a deck problem.** Life lost per battle after
the deck should have come online:

| floor | 10 | 11 | 13 | 15 | 18 |
|---|---|---|---|---|---|
| The Long Emergency | **-0.1** | **-0.4** | 1.7 | 3.4 | 14.1 |
| The Rising | 30.3 | 22.9 | 23.5 | 23.0 | 22.2 |

One act stops paying for battles; the other pays ~23 life a battle for ever. **v3 raised the stakes
on deck quality enormously** — the deck IS your per-turn output now — so an apocalypse that fails to
improve it costs you every remaining floor. The Long Emergency's dooms hand the deck something
(AI Uprising rewrites, Grey Goo replicates). The Rising's do not: **Zombie** dilutes with 2/2s,
**Vampires** is battle scope and leaves nothing, **Hell Uprising** is +6 power and **-2 toughness**
when toughness is now your blocking every single turn.

The rule that diagnoses it is the doc's own: *every permanent doom converts one resource into
another, none are purely bad.* **Decide what Zombie and Hell Uprising should PAY before tuning The
Rising further** — run 12 already tried the obvious fix here and made it worse.

**Then sweep healing.** Seven effects remain (`Gravecaller` 4/turn, `The Choir` 2, `The Last Morning`
4, `Zealous` 4, `Shepherd` 4 to every enemy, `Chorister`, `Last Chorus`). Healing is priced against
damage per turn, and v3 collapsed damage per turn — **every one of them got stronger**, and run 13
already had stacked healing as the prime suspect for the v2 tail.

### What the plan originally said

**Files:** `DoomCore/Content/EnemyLibrary.cs`, `DoomCore/Content/StarterContent.cs`,
`docs/findings/doom-balance.md`.

Per-turn output has collapsed to what 3 energy buys. Expect Opponent health to fall hard and costs to
compress toward **0-2** (five cards on 3 energy at costs 0-3 means you play two and bin three).

**Do not hand-tune the Godot build first.** `dotnet run --project DoomConsole -c Release -- sim 1000`.

Write it up as **run 14**, per act, never one number across acts. Watch **max turns per battle per
floor**, not the mean — the v2 tail was invisible in the mean. If long battles take 40 turns to lose,
steepen reinforcement scaling in `RefreshTheOpponentsLine`; **do not add an energy ramp** (DoomJam.md
has the rule and the reasoning).

**This is where "tests read authored values, never restate them" gets audited.** If a rebalance of
this size breaks a dozen tests, they were restating content and should be fixed to read it.

**Exit:** completion rate in the target band on all three acts, and the front end is still untouched.

---

## Phase 5 — intent sequences, Piercing and Shifting first

**Files:** `DoomCore/Enemies/Enemy.cs`, `DoomCore/Content/EnemyDefinition.cs`,
`DoomCore/Content/EnemyLibrary.cs`, `DoomCore/Actions/EndTurnAction.cs`, `DoomCore/Ai/DoomBot.cs`.

`Enemy.Intent` is a single value set once at creation and never changed. It becomes a **list plus an
index**, advanced once per turn — data, serializable, no engine change.

Build **Piercing** and **Shifting** first and alone. They are the counterplay that makes phase 6
safe, and everything else (Splash, Reaping, Growing, Wind-up) is content on top.

- **Piercing** — `ResolveLanes` currently soaks up to `RemainingToughness`; piercing bypasses the
  soak entirely. Then teach the bot, or it will chump-block it for ever.
- **Shifting** — needs a lane-move step before resolution **and a telegraph of where it is going.**
  "Do not hide an intent" is a standing rule.

**Exit:** an enemy visibly does something different on consecutive turns, verified in `DoomConsole`
rather than reasoned about.

---

## Phase 6 — `Persistent`

**Never before phase 5.** A persistent unit with no way to be dislodged is an auto-win.

**Files:** a `PersistentComponent` marker (follow `CompanionComponent`'s precedent),
`WithdrawUnitsAction`, `DoomCore/Cards/DoomCard.cs` / `RunCard`, `DoomCore/Content/StarterContent.cs`.

Withdrawal skips persistent units, and the phase-1 companion special case collapses into this — the
companion is simply persistent. Damage already clears on re-entry via `PlayCardAction`, so "erodes
inside a fight, whole for the next" needs no code.

Content: **3-5 cards, uncommon and rare only.** Then a second `sim` pass measuring the **break-even
turn** — how many turns a persistent unit needs to beat an ephemeral one of the same cost. Target 3.
**Give it its own line in the findings table**; completion rate will not show it.

---

## Phase 7 — the Companion

**Files:** `DoomCore/Run/Companion.cs`, `DoomCore/Run/Run.cs`, a new place-companion action,
`DoomCore.Tests/CompanionTests.cs`, `SQGodotCommon/DoomGame/DoomThemeSelect.cs`.

Three things, in order — each is independently shippable:

1. **Ability** — a `DoomEffect` list on `Companion`, copied onto its battle card. The effect system
   already does not care what holds it, so this is content.
2. **Chosen lane each turn**, free. Drop the `Lane = LaneCount / 2` pin in `Run.StartBattle`.
3. **Death and resummon** — to hand, exempt from the end-of-turn discard, escalating cost per death
   within a battle. Marks and run-scope gains survive; the board must SAY it is down and what it
   costs.

Then a **roster of 2-3 companions** and a run-start pick. `CompanionTests` has eleven tests; several
assert the fixed lane and the return-next-battle rule and will need rework.

---

## Phase 8 — Flood, and the six `Standing` scenarios

Flood washes a board that now washes itself. Replace it (wash the HAND, or take next turn's draw) —
it teaches the fiction on floor 1, so a replacement is worth more than a deletion.

Then re-read Nuclear, Hell Uprising, Famine, Judgement, AI Uprising and Grey Goo and write down what
each now asks on a firing turn. They changed meaning without changing code.

---

## Phase 9 — the front end

Last, deliberately. `DoomBoard.cs` is 985 lines and every phase above changes what it renders.

- `DoomBoard` — the board empties every turn; withdrawal needs an animation distinct from death
- `DoomLaneCell` — a persistent/ephemeral tell, and the intent sequence rather than one number
- companion down-and-resummonable state, with its cost visible
- `DoomCardFace` — the `Persistent` keyword on a card
- `KeywordLibrary` — Persistent, Piercing, Shifting. The glossary is data and the UI reads it free.

---

## Phase 10 — scaling content, which is what makes long battles winnable

**Not optional, and easy to mistake for polish.** Scaling is a deckbuilding outcome now, and **no
card in the game can grow**: every effect amount is a literal (`DealDamageAction { Amount = 6 }`).

Two primitives, both data, both serializable:

- **count-based amounts** — `Amount` times a `PerCount` read (`YourUnits`, `DoomsFired`,
  `CardsPlayedThisTurn`, `DiedLastTurn`). One enum, one resolve step.
- **a buff action** — changes a `UnitComponent`'s stats. Triggered one-shot only; **do not build a
  continuous/static layer**. That is MtgCore's expensive machinery and this game does not need it.
  Record the absence in DoomJam.md "Engine findings".

`DoomTargeting` also wants `AdjacentLanes` — one case, and it makes lane position mean something
beyond which enemy you face.

---

## Standing traps

- **An inert card throws no error.** Every new keyword, intent and effect gets a test that the
  CONSEQUENCE happened, not that the construction exists.
- **Check `git status` before writing a file you believe is new.**
- **Never report one balance number across more than one act.**
- **A rule file fails silently** — LF endings, and `**` in a glob matches nothing.
