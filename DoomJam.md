# DOOMJAM — Godot Wild Jam design doc

**Branch `GWJ-ImminentDoom` is NOT the MTG game.** Theme: IMMINENT DOOM. Sub-themes: TAG ALONG
(required), GO SPINNY, PERSPECTIVE SHIFT. 9-day jam. **That is what the jam OFFERED — see
Sub-themes for which are actually being built.**

The second goal is a measurement: **how hard is it to build a completely different card game on
`ImmutableGameObjects`?** Whatever we end up wishing we could lift out of `MtgCore` is the finding.
Record it under "Engine findings" as we hit it.

> **THE GAME IS CALLED *ENDLING* (2026-09-18).** An endling is the last surviving member of a
> species, which is what you and the thing following you are by floor 45.
>
> **"DOOMJAM" was the jam working title and stays as the CODEBASE prefix** — `DoomCore`, `DoomBoard`,
> `DoomJam.md`, `doom-balance.md`. Renaming a solution is churn with no gameplay in it. The two
> places that face a player are `MainMenu.Title` and `project.godot`'s `config/name`; if a third
> ever appears, it reads from one of those rather than hard-coding a fourth.

## State of play (2026-09-17)

**The whole loop is built and playable in Godot.** Pick an act, fight down twenty floors, take
rewards between them, eat or dodge the apocalypse on the clock, die or finish. `DoomConsole` is
still the remote surface and still the faster way to test a rules change.

**BUILT:** five-lane automatic combat; the run layer; the Companion; the Opponent and killing it as
the win condition; recurring dooms; scenario scope; all three acts reachable from a picker; rewards
weighted by rarity; the full front end — board, hand, drag-to-lane, reward screen, intermission,
animation, a keyword glossary on hover. **113 tests green.**

**A DESIGN PASS ON 2026-09-17 SUPERSEDED THE COMBAT MODEL, AND NONE OF IT IS BUILT YET.** Units
become ephemeral, persistence becomes a premium keyword, enemies get intent patterns, and the
Companion becomes the run's engine. Read **"Combat v3"**, **"Enemies must have PATTERNS"** and
**"TAG ALONG"** before touching `DoomCore/Actions/` or any content number. Everything this file
still marks BUILT is the v2 game that is in the repo today.

**`docs/findings/doom-balance.md` measures the v2 game, and v3 invalidates every absolute number in
it.** Keep the file: the methodology and the transferable findings — tune against the dodge rate,
never report one number across acts, power pays and toughness barely does — all survive. The
numbers do not.

**NOT BUILT, and this is where a design pass should look:**

- **Rapture** still has no implementation and is gated to floor 99 so it is never offered. Ship it or
  cut it.
- **Elites, salvage, events and multi-act runs.** None exist. `DoomIntermission` is the screen they
  would live on and should be generalised ONCE, when events actually need it.
- **Ashfall leaves no mark on the Companion** — see "Open questions". It is playable content with a
  silent no-op in it.
- **The stalemate tail.** The mean battle is healthy; the worst case is not. See
  `docs/findings/doom-balance.md` and the pacing handoff.
- **The Rising is the weak act** at 20.7% completion against 34.3% and 29.0%. Its cards pay once.
  Read findings run 12 before redesigning them — the obvious fix was tried and made it worse.

**Two content rules the front end now depends on**, both established 2026-09-17:

- **Effect text is short and leans on keywords.** `"Doom: 6 to every enemy"`, not `"when the doom
  fires: 6 to every enemy"` — the long form did not fit a card and rendered truncated. `Doom` is a
  keyword in `KeywordLibrary` with reminder text on hover. **Reach for the glossary before reaching
  for a smaller font.**
- **`Description` is flavour and is no longer shown on a card.** A card with no ability shows no text
  box at all and its art grows into the space, which makes "this card does something" readable at a
  glance across a hand. Flavour still appears in `DoomConsole`'s content dump.

## Pitch

A solitaire roguelike deckbuilder. You face an **opponent** across five lanes while the world ends
around you on a repeating clock. Every few turns a **doomsday scenario fires**, reshapes the board
or your deck, and the fight carries on. You win by killing the opponent — and the real question is
how many apocalypses you take on the way, because the apocalypses are also the only thing that
makes your deck stronger.

**On the theme.** A doom you can outrun is *more* imminent than one you cannot, not less: a
guaranteed apocalypse produces resignation, a raceable one produces urgency. The countdown is
something you are playing against every turn rather than waiting out.

## The doom is a clock you can outrun — at a price  [BUILT]

**The doom fires on its interval, repeatedly. Kill the Opponent first and it never lands at all.**

> **SUPERSEDED TWICE.** This section used to be called "the one rule that must not bend", and it
> has now bent twice. Both changes are recorded because the reasoning matters more than the rule:
>
> **v1 — "the countdown always runs out and ENDS the battle."** Clearing the enemies early was
> forbidden: it would mean beating the apocalypse. It made a cleared board into dead air, nothing to
> do but press end-turn. Gone. Do not reintroduce a battle that ends because a counter ran out.
>
> **v2 — "the doom always fires; the first firing is unraceable."** The doom recurred and the battle
> ended on the Opponent's death, but you could never dodge an apocalypse entirely. Gone too: making
> inevitability a GLOBAL rule meant every battle had to be tuned so no opening could ever be fast
> enough, which prices every future card against one constraint forever.

**Inevitability is now a per-battle design choice, not a law.** A doom you cannot dodge is one the
battle was *built* to make undodgeable — a big Opponent, or an enemy that cannot be killed before
the countdown. That is content, and it can differ floor to floor, which the global rule could never
allow.

### Dodging is not free, and that is what balances it

Killing the Opponent before the first firing skips `DoomTransforms` **and**
`Companion.Marked` — `Run.AfterBattle` returns early on `DoomsFired == 0`, so both are missed.

| | your deck | your companion |
|---|---|---|
| **dodge it** | unchanged — clean, thin, and no stronger than it started |
| **eat it** | rewritten: stronger and more distorted |

**The apocalypses ARE the power curve** — there is no separate progression system, by design. So a
player who dodges everything arrives at floor 15 with a starter deck and an Ash that survived
nothing. Speed buys safety and costs power, and the choice is real in both directions.

That is the self-correction. Enemy HP and enemy abilities are for *tuning* which battles can be
outrun, not for holding the whole structure up.

| you kill the opponent in | dooms you eat | outcome |
|---|---|---|
| before the first firing | 0 | untouched deck, no power gained |
| ~8 turns | 2 | rewritten twice |
| ~15 turns | 4+ | unrecognisable, and probably very strong |

**Nothing ends a battle but a death.** No turn limit. The pressure on a battle that will not end is
reinforcements scaling on the turn number: the longer you fail to break through, the worse the
bodies you have to break through.

## The core hook

*BUILT. `DoomTransforms` for permanent scenarios, `DoomBattleEffects` for battle ones,
`StarterContent.ScopeOf` deciding which, and each hook throwing when handed the other kind.*

> **The doom doesn't kill you. The doom edits your deck.**

Every scenario is one thing: **read the board when the doom fires, then change something.** One
engine hook; every apocalypse after that is data. This is where content comes from — do not build a
second mechanism.

**A scenario has a SCOPE, fixed at design time:**

| scope | changes | lives in |
|---|---|---|
| **Battle** | the current `GameState` — board, hand, draw pile | a hook holding the battle |
| **Permanent** | the `Run` deck, forever | `DoomTransforms` |

A scenario is **one** of these, never both, and never a tiered pair of the same idea. Variety comes
from having MANY scenarios, not from re-tiering three of them.

**Scope is the difficulty curve.** Early floors draw from battle-only scenarios — inconveniences you
navigate, gone when the fight is. Later floors draw from permanent ones, where a firing leaves marks
on the run. `PlayableOn(floor)` already does that gating and is the right mechanism.

**A scenario added to the wrong hook silently does nothing**, and looks exactly like one that
worked — the failure mode this codebase keeps rediscovering. So scope is an explicit `ScopeOf`
lookup beside `CountdownFor`, and each hook **throws** when handed a scenario of the other scope.
Rapture already sets that precedent by throwing rather than no-opping; follow it.

**Dooms do not escalate within a battle by default.** The same scenario fires the same way every
time, which is what makes it plannable. A scenario that escalates is one *designed* to escalate, and
says so.

## Design rule: bargain, not tax — PERMANENT scenarios only

**Every PERMANENT doom converts one resource into another. None are purely bad.** A deck that only
gets worse is a misery engine players quit, and it makes progressively harder enemies unbalanceable.
Tradeoffs mean the apocalypses *are* the power curve — no separate progression system is needed.

**Battle-only scenarios are exempt**, and that exemption is the point. Nothing carries forward, so a
battle doom can be a pure obstacle — a puzzle for this fight rather than a tax on the run. That
makes early-game content far cheaper to write: you only have to find a bargain for the ones that
leave scars.

Currency insight: **a unit absorbs rather than prevents, so toughness IS life.** A 1/1 standing in
front of a 5-damage attack is worth exactly 1 life. Creature bodies and life are the same currency in
two forms, and every scenario trades on that one axis. **This survived the move to lanes unchanged**,
which is the test any future combat change has to pass — it is what makes the dooms tradeable.

| Scenario | Scope | Reads | Effect | Interval |
|---|---|---|---|---|
| **Flood** | battle | what is standing | everything in play is **washed to Discard** — you keep the cards, you lose the board and the energy you spent on it | 5 |
| **Zombie Apocalypse** | permanent | what died | deaths return as 1/1 Zombies in the deck — quantity bought with deck space | 3 |
| **Nuclear** | permanent | what was left on board | those become **Irradiated**: permanent +2/+2, lose 1 life when drawn | 2 |
| **Rapture** | permanent | what you sacrificed | sacrificed creatures return as life — the doom you *want* at 6 HP | 3 |

This is a **starting set, not the set.** The plan is many scenarios, each doing something distinct
and creating a situation a battle has to be played around. Adding one should stay ~15 lines: an enum
entry, a `ScopeOf` row, a `CountdownFor` row, a `PlayableOn` row, and one case in its scope's hook.

Interval varies per scenario on purpose: it is free texture, and it makes each apocalypse feel
different before the player reads a word of its text.

> **FLOOD IS A NO-OP UNDER v3 and must change or be cut (2026-09-17).** It washes the board to
> Discard, and in v3 the board washes itself at the end of every turn. It is the scenario that
> teaches the fiction on floor 1, so a replacement is worth more than a deletion — wash the HAND, or
> take next turn's draw. Everything below is the v2 reasoning, kept because the *why* still holds.

**Flood was rewritten (2026-09-14).** It used to delete never-summoned units from the run deck and
duplicate the ones you played.

> **Why it changed:** permanent card REMOVAL causes more problems than it is worth. The old doc
> already knew — it gated Flood behind `FloodUnlocksAtFloor = 8` because on a 10-card starter deck it
> could delete everything and end a run outright. That gate meant Flood simply **did not exist** for
> the first seven floors. Rewriting it as a board wash lets it appear from floor 1 and teaches its
> fiction — *the water takes what is standing* — long before anything with teeth does. Removal may
> return later as some other scenario's deliberate gimmick; it is not the baseline.

Flood keeps the lane game honest: you can see it coming, so the question becomes *how much do I
commit to a board that is about to be washed?*

## Combat v3 — UNITS ARE EPHEMERAL  [DESIGNED 2026-09-17, NOT BUILT]

> **SUPERSEDES the permanent board described in "Combat — FIVE LANES" below.** Lanes, automatic
> resolution, absorption-with-excess and "no targeting anywhere" all survive untouched. What changes
> is how long a unit stays.

**A unit you play leaves the field at the end of the turn.** You rebuild the board every turn from a
fresh hand. Persistence is a premium keyword that a few cards and every Companion carry.

### Why: the game had two contradictory economies stapled together

The hand was Slay the Spire — drawn to 5, discarded every turn, ephemeral by design. The board was
Hearthstone — pay once, keep forever, damage persists all battle. **The stall was the seam between
them.** An ephemeral hand keeps feeding a permanent board until the board saturates, and then the
hand has nowhere to go: you draw five units, every lane is held by a healthy unit of your own,
`PlayCardAction` refuses all five, and the only legal move is End Turn.

Said plainly, from the playtest that found it: *committing permanent resources while drawing five new
permanent resources a turn creates a board the enemies cannot clear faster than you refill it.*

**The stall was therefore the reward for playing well** — clear the lanes, your units stop dying, the
board locks. Negative feedback on success, and it bit hardest on EASY floors, where two enemies
contest two lanes and your other units are immortal.

Ephemeral units do not patch that. They delete the conditions for it, the same way the recurring-doom
model deleted dead air rather than fixing it.

### The second reason, which is the better one

> *"which lanes do I contest, knowing the rest hit my face — that tension is the whole battle"*

That decision happened on turn one and then decayed to nothing. **v3 makes the doc's own stated core
decision happen every turn instead of once.** It is not a new pillar; it makes the existing one
load-bearing.

### The rules

- **A unit withdraws at end of turn** and goes to Discard. It is not dead: no `OnDeath` triggers, it
  feeds no scenario read, and it counts as no death anywhere.
- **Dead means 0 toughness**, ephemeral or persistent. That fires `OnDeath` and feeds Zombie.
  Withdrawn ≠ dead is the whole distinction, and it is load-bearing — it is what lets a Companion pay
  off "units that died last turn" without paying for its own board wiping itself every turn.
- **Any unit may be placed into ANY lane. Whatever was there goes to Discard, and there is no
  refund.** This is a law, not a convenience. It is what stops `Persistent` reintroducing the stall in
  miniature: persistence must mean *it stays if you leave it*, never *you may not use this lane*. The
  cost of overwriting is already exact — you are throwing away something you paid for.
- **The doom fires BEFORE the withdrawal**, so a firing reads the board you committed this turn. Six
  scenarios use `FiringRead.Standing`; sweeping first would make every one of them silently read an
  empty board, which is precisely the silent no-op this codebase keeps rediscovering. Pin it with a
  test that a firing sees a non-empty field.

### Persistent — premium, and the Companion is the glimpse

**`Persistent`: this unit stays in its lane, and keeps its damage.** Ephemeral units arrive fresh
every turn; a persistent 4/8 is 8 absorption once, then 5, then 2, then it is gone. **The permanent
thing is the thing that accumulates scars**, which is the game's entire fiction. Damage clears
between battles — it erodes inside a fight and is whole for the next one.

**It is premium and sparing, at uncommon and rare only.** The teaching problem that would normally
force a keyword into the starter deck is already solved: **the Companion is persistent, so every
player plays alongside one from floor 1**, and a persistent card in a reward screen reads as "another
Ash" — a thing you already know you want.

**The pricing dial is the BREAK-EVEN TURN** — how many turns a persistent unit needs to beat an
ephemeral one of the same cost. At 2 it is an auto-include and stops being premium; at 4, against
enemies that kill it in 3, it is a trap that feels bad to draw. **Target 3, and give it its own line
in the balance table** — it decides whether a whole card class is playable, and completion rate will
not show it. Same lesson as `dooms dodged`.

**A persistent unit parked in an uncontested lane with a per-turn trigger is an engine with no off
switch** — the stall returning as a win condition. The answer is on the enemy side, not a nerf: see
Piercing and Shifting below. **Build those two intents BEFORE the first persistent card**, or the
first one will look balanced and be an auto-win.

### What v3 costs, recorded before it is paid

- **Every number in the game moves.** A 6/6 in an open lane used to deal 6 a turn for ever for one
  energy paid once; now that 6 costs a card and an energy every turn. Per-turn output collapses to
  roughly what 3 energy buys. Opponent health, enemy health, card power and costs are all re-derived.
  `sim` measures it — do not hand-tune the Godot build first.
- **Costs must compress toward 0-2.** Five cards and 3 energy at costs 0-3 means you play two and bin
  three, every turn, and now you bin a board slot with them. STS lands on 0-2 for this reason.
- **Keep the end-of-turn hand discard.** Two ephemeral economies that match is the entire point.
- **Flat energy, and SCALING IS A DECKBUILDING OUTCOME — never a property of the board.** This is a
  rule, established 2026-09-17, and it is the STS model: a Strike/Defend deck does not scale, and a
  long fight is lost by the deck that could not close it. **Do not add an in-battle energy ramp**, or
  anything else that hands the player growth for simply surviving turns — that is the board doing the
  deck's job, which is exactly what v2 was doing by accident.

  **v2 was scaling you for free and nobody noticed.** A permanent board is a stockpile: a 6/6 played
  on turn 1 hits for 6 every turn after, so output grew with time on flat energy and a long battle
  was self-correcting. v3 deletes that, and it should.

  **Two consequences follow, and both matter more than the rule.**

  First — **the dodge-vs-eat bargain finally has teeth.** The doc has always promised that dodging
  buys safety and costs power, but in v2 a dodged deck could still grind out a long fight on a
  stockpile it got for nothing. It cannot now: scaling comes from doom transforms and companion
  marks, which you only get by EATING apocalypses. The relic slot in this game is occupied by the
  dooms, and this is what makes that true rather than merely stated.

  Second — **"it is up to your deck" is not true yet, because no card in the game can grow.** Every
  card is a fixed stat line and every effect amount is a literal (`DealDamageAction { Amount = 6 }`).
  So count-based amounts and a buff action are no longer synergy nice-to-haves: **they are what makes
  a long battle winnable at all.** Build them alongside v3, not after it.

  The tail is a PACING problem, not a correctness one. Reinforcements arrive at `Health + turn/2`,
  `Attack + turn/4`, so enemy attack grows without bound against flat absorption — a battle you
  cannot close ends with you losing it, the same floor STS has. If `sim` shows that taking 40 turns,
  steepen the reinforcement scaling. Do not reach for a new mechanism.
- **Flood must change or be cut.** It washes a board that now washes itself.
- **One-turn bodies are less memorable than permanent ones.** The risk of v3 is trading boring turns
  for a boring deck. The Companion's ability is what carries deckbuilding identity instead — if that
  does not land, v3 has not landed.

### Six scenarios get sharper for free

`FiringRead.Standing` is the most-used read in the game — Nuclear, Hell Uprising, Famine, Judgement,
AI Uprising and Grey Goo. Today it reads whatever accumulated over eight turns, which the player
never chose. In v3 it reads **the Companion plus exactly what you committed on the firing turn**, so
each of the six poses a different question as the clock hits 1: Nuclear (+2/+2, a life on draw) wants
your best cards standing; Judgement (everything flattened to 6/6) wants your worst. **A commit-or-hold
decision on every firing turn, and it costs nothing to build.**

## Enemies must have PATTERNS, not a number  [DESIGNED 2026-09-17, NOT BUILT]

**`Enemy.Intent` is set once at creation and never changes.** Every enemy attacks for the same number
every turn, for ever, and `IntentKind` has two values of which one is unused. That barely matters in
v2, where your board is already built and the intent is arithmetic you solved on turn one. **In v3 it
is the only thing that makes this turn's puzzle different from last turn's.**

So an intent becomes a SEQUENCE the enemy advances through each turn, cycling — a list and an index,
data, no engine change. Without it, per-turn placement is the same bin-packing problem every turn:
**v3 guarantees a decision, not an interesting one**, and this is what makes it interesting. It is
also why the telegraph finally earns its place: today it shows a number that never changes.

| Intent | What it does | What it makes you do |
|---|---|---|
| **Wind-up** | `0, 0, 24` | bank real toughness for turn three |
| **Piercing** | ignores toughness, hits your face | **kill it — a body cannot answer it** |
| **Shifting** | moves to your emptiest lane, then attacks | never leave a hole, never park |
| **Splash** | hits its lane and both adjacent | stop clumping |
| **Reaping** | attack scales with units you placed this turn | stop going wide |
| **Growing** | +3 attack each turn it lives | a clock inside the clock |

**Piercing is the one that matters most.** Everything else is answered by placing a body; an enemy a
body cannot answer forces you to spend POWER instead of toughness, which flips that lane from defence
to offence. **Piercing and Shifting are also the guard on persistence** — between them, no lane is
ever safe to park in indefinitely.

Existing enemy effects are all chip damage or healing, which is a bigger number rather than a
different plan. An enemy should punish a BEHAVIOUR, the way STS does — that is what these are for.

## Combat — FIVE LANES, resolved automatically  [BUILT]

**The board is five lanes. One of your units and one enemy per lane. They fight each other
automatically.** There is no targeting anywhere in the game and no attack-or-block choice: you pick
a lane when you play a card, and that is the entire decision.

Enemies keep their **intent telegraphed a turn ahead** — an enemy that is winding up shows the
number it will hit its lane for. Do not hide an intent.

### The Opponent — the board is symmetric  [BUILT]

**The enemy units belong to someone.** The Opponent is an entity with its own HP, sitting behind the
lanes the way you sit behind yours. **Killing it is how you win**, and it is the only way a battle
ends in your favour.

That makes every lane one of three cases, with no exceptions:

```
                    OPPONENT  38hp
  L0        L1        L2        L3        L4
[ Brute ] [   -    ] [ Brute ] [   -    ] [   -    ]   <- theirs
[ 3/4   ] [ 2/2    ] [   -    ] [ 4/4    ] [   -    ]   <- yours
   ↕ trade   → 2 face   ← 3 to you  → 4 face   (nothing)
                    YOU  47hp
```

| lane | what happens |
|---|---|
| both filled | they fight each other, both ways |
| yours only | **your unit hits the Opponent** |
| theirs only | their unit hits you |
| empty | nothing |

**An open lane is now your win condition, not just a leak.** Holding a lane is offence and defence in
the same act, and three energy will not cover five lanes — that tension is the whole battle.

**The Opponent refreshes its units**, summoning into its open lanes to stop the bleeding. It is
**telegraphed** like everything else: "summoning a 3/3 into L1" shows a turn ahead. Certainty is
permission to show the player everything; the tension here is inevitability, not surprise.

Two global rules, deliberately not keywords:

- **A unit absorbs up to its remaining toughness and the excess hits the face behind it.** So a body
  in a lane is worth exactly its toughness in life. No keyword to teach, and stalling is impossible
  by construction.
- **An unheld lane delivers the full hit.** Both directions. No global blocking, no interception.

Damage **persists for the whole battle** on both sides — units carry marked damage, enemies carry
lost HP — so a lane is a grind you can win over two or three turns rather than a single comparison.

**Turn shape:**

1. Enemy intents and the Opponent's next summon already visible, per lane
2. Draw, spend Energy, place units into lanes
3. Resolve every lane at once — both sides deal damage; unheld lanes hit the face behind them
4. The doom clock ticks; if it reaches zero the apocalypse fires and the clock resets
5. Enemies declare next intents; the Opponent summons what it telegraphed

**What this replaced, and why it is not a loss.** Combat used to be "each unit may attack OR block,
never both", which was the stated core decision. Lanes delete it and replace it with *which lanes do
I contest, knowing the rest hit my face* — the same investment-vs-survival squeeze, expressed
spatially, readable at a glance and with no targeting UI to build. "Blockers deal no damage" went
with it: it existed only to keep attack-vs-block a clean either/or, and there is no such choice left
to protect. **The currency insight survived intact**, which is what mattered — see below.

**Lanes give the dooms a spatial axis to read** ("everything in lane 3 is washed away") that did not
exist before. That is free content for scenarios, though it is no longer load-bearing — it was once
proposed as the fix for dead air, and the recurring-doom model deleted that problem instead.

**Energy: 3/turn, refills.** Deletes mana, lands and colours entirely. "Burn a turn off the countdown
to cast this now" stays a **rare card keyword**, never the base economy — accelerating your own
apocalypse should be a desperate move, not routine.

**Life does not heal automatically (STS-style).** 0 life ends the run.

## TAG ALONG — the Companion  [v2 BUILT; v3 DESIGNED 2026-09-17, NOT BUILT]

> **COMPANION MARKS ARE CUT (2026-09-18).** Every apocalypse survived used to stamp a permanent
> +N/+N on the companion — Gravemarked, Glowing, Barnacled — and that "record of your run" is gone,
> along with `CompanionMark`, `ScenarioDefinition.Mark` and the collapsed `FullName`.
>
> **Cut on playtest feedback: "I never liked this mechanic."** It read as a stat trickle nobody
> chose, attached to a name that grew until it had to be collapsed to stay on screen. The companion
> is its ABILITY now, and nothing else.
>
> **Everything below about marks is superseded.** What survives is the part that was never about
> them: the companion is on the board free, no transform can touch it, and its death is not a deck
> event. The dodge-vs-eat bargain still holds — it is the DECK that an apocalypse rewrites, which
> was always the larger half.

**The only thing the doom cannot touch — and, from v3, the engine your deck is built around.**

### What v2 built, and what survives

- On the board free at the start of every battle, no summoning cost. Also solves "short round and I
  drew badly" — the board is never empty.
- **Immune by construction, not by a special case:** the companion lives on `Run.Companion` and is
  not in `Run.Deck`, and every transform operates on the deck. Nothing had to be taught to skip it.
- Its battle card carries `RunCardId = 0`, which no deck card can hold (ids start at 1), so nothing
  mapping a battle unit back to a deck entry can find it. **This guard holds even if the companion
  is ever put into the deck**, which is what makes the resummon options below safe.
- **A companion death is not a deck event.** It does not feed Zombie — otherwise chump-blocking with
  it minted a free card every turn.
- Each doom survived **stamps it**, permanently and cumulatively. By the last floor it is a patchwork
  of every ending you lived through — **the record of your run**, and the one thing you carried out.
  `Companion.FullName` renders it, collapsing repeats: "Ash — Hardened x3, Rewritten x4".

### v3: the Companion is the synergy driver

**The ability is the build declaration, and it is chosen at run start.** This resolves the open
question about choosing between several companions: yes, and it is the game's character select. A
companion is one record with a `DoomEffect` list, and the effect system already does not care what
holds it — so a roster is cheap content, not a system.

**The reward screen becomes a conversation with your companion.** Which cards are good is answered by
who you brought, which is exactly the deckbuilding depth v2 lacked: every card was a vanilla body and
nothing made one reward better than another except its stat line.

**Design the ability to change how you PLACE, not what you draft.** "Bonus when you play Scavengers"
is a checklist you satisfy at the reward screen and then forget. "Power for each unit that died last
turn" makes you feed losing lanes on purpose, every turn. Placement is the only decision the game
has, and the companion is the only thing on the board that persists long enough to see a pattern.

Six axes, so a roster does not go samey — each makes a **different reward screen correct**:

| Axis | Ability shape | What it makes you do |
|---|---|---|
| **spatial** | adjacent lanes +1/+1 | clump, concede the flanks |
| **attrition** | power per unit that DIED last turn | feed losing lanes on purpose |
| **survival** | power per unit that LIVED last turn | overcommit toughness, play safe |
| **volume** | bonus per card played this turn | cheap cards, wide turns |
| **the clock** | grows on every firing, or stronger near zero | eat apocalypses instead of dodging |
| **the face** | pays off when a lane hits the Opponent | race, leave lanes open |

**The attrition axis only works because withdrawn ≠ dead.** "Units that died last turn" has to mean
units the enemy killed, not the four that walked off at end of turn. That rule was tidiness when it
was written and is load-bearing now.

### v3: it is placed, it is persistent, and it can die

- **`Persistent` by default**, and it is the only persistent thing most runs will own — see Combat
  v3. It keeps its damage inside a battle and is whole again for the next one.
- **You choose its lane every turn, free.** Not pinned to lane 2. Everything else on the board is
  fluid, so a statue in the middle would be the one strange exception, and "where does Ash stand this
  turn" is a real decision that costs nothing and cannot be drawn badly. It is the issue-#1 fix that
  needs no new system at all.
- **It can die, and it must be able to.** Under v3 every other body costs a card and an energy every
  turn. A companion that cannot die is a free unkillable permanent unit in a game where nothing else
  is permanent — the strongest thing in the game by a distance, and a set-and-forget engine with no
  off switch. It is also *fair* now in a way it was not: you pick its lane, so its death is your read
  going wrong rather than the shuffler's fault.

**The best tension in the design, and it costs nothing to build: your engine is also your best
blocker, and you cannot have both.** Park it safe and it holds nothing; put it where it is needed and
you grind down the thing your deck is built around. Piercing and Shifting mean no lane is safe for
ever.

### v3: death and resummon — the COMMANDER model

**It dies, and you may resummon it for a cost. The cost takes part of your turn, so it is never
free.** Exact cost TBD; the shape is MTG's commander tax, and the tax is what prices out the exploit
below.

- **Marks and every run-scope gain survive its death.** It returns next battle carrying everything.
  Losing a run's accumulated identity to one bad lane read is the worst outcome this design can
  produce, and it would happen to new players first. Battle-scope effects die with the body; the
  run/battle split already draws exactly this line everywhere else.
- **The escalating cost is not flavour — it prices out suicide-to-heal.** A companion that returns
  fresh makes dying a way to clear its damage. Rising resummon cost within a battle (1, then 2, then
  3) kills that loop with one integer on the battle record, and it is the same reason MTG escalates.
- **Heals to full between battles.** Arriving at floor 15 permanently at 3 toughness is misery, and
  it matches how persistent cards work: erode inside a fight, whole for the next.
- **The board must SAY it is down and what it costs.** An empty lane and a silently missing ability
  is the class of bug this codebase keeps rediscovering.

Where it goes while dead was the one live choice. Recorded so it is not re-litigated:

| | cost of dying | verdict |
|---|---|---|
| **to hand, exempt from the discard, pay to redeploy** | one turn of the ability, plus energy | **chosen.** Deterministic, short, and a decision — pay now, or spend on the lane about to kill you |
| shuffled into the deck | several turns, plus a draw | random recovery for the thing the whole deck is built around, against the doc's own "certainty is permission" principle. Technically safe (`RunCardId = 0` still protects it), just worse |
| returns automatically after N turns | a wait | simplest, and fits the telegraph — but it is a wait, not a decision |

### Cut: attachments and upgrade cards

**Considered and dropped in the same pass.** Cards that upgrade a unit you already hold were the
answer to dead turns on a saturated board — and v3 deletes saturated boards, so they answer a
question that no longer exists. Worse, an attachment on an ephemeral unit buffs something that leaves
at end of turn, so they would have collapsed into "premium persistent targets only", i.e. the
companion.

**The simplification that falls out: marks stay plain stat bumps.** Marks-as-effects was proposed to
give the companion an identity it did not have. The ABILITY is the identity now, so the marks can
stay the cheap thing they already are — the ability is who your companion is, the marks are how much
of the run it has eaten.

## Run structure

- **STS-style map: 20 floors per act.** Not every floor is a battle — rests, events and shops fill the
  rest. Battles are the only thing built for now; the map comes later.
- Battles chain; life and deck persist. 0 life = run over.
- **Between battles: see the next apocalypse BEFORE choosing your reward.** Free tension, zero cost,
  turns the reward screen into a real decision.
- **Branch: choose which doom you walk into.** Two options shown. Choosing your own apocalypse is a
  great thing for this game to allow — and it got **heavier** when dooms started recurring: you are
  no longer picking the thing that happens once at the end, you are picking the thing that will hit
  you three or four times. It is also the reason a battle runs ONE scenario on repeat rather than
  cycling through several; a grab bag would make this choice meaningless.
- Rewards are normal (card choice / relic). "You keep what you kill" is **one scenario's gimmick**,
  not a global rule.
- **Weight reward offers toward the doom just taken or about to be walked into.** Tag cards by which
  doom they answer. With ~35 cards, "couldn't find the answer" reads as *unfair* far more often than as
  *I wasn't clever enough*. It is a dictionary lookup, and it makes a run feel like a conversation.

## Sub-themes

| Theme | How | Priority |
|---|---|---|
| TAG ALONG | the Companion, above — structurally load-bearing | required |
| PERSPECTIVE SHIFT | 2D cards over a painted backdrop | **partly shipped** — one flat backdrop, not 3D and not yet per-apocalypse |

**Certainty is still permission to show the player everything.** The tension is inevitability, not
surprise, so the screen says which apocalypse is coming, when, and what it does. That principle
survives; only the instrument changed.

> **GO SPINNY — DROPPED (2026-09-14), during UI design.** It was a dial you scrolled to see what the
> next firing would do to your deck *right now*, and this doc rated it "high — it earns its place".
>
> **Why it went:** the screen only needs to say WHAT THE DOOM DOES, and a static per-scenario
> description does that in one line. As drawn it was a second countdown sitting beside a countdown
> already rendered in 60pt type. Its one real payload was the live deck diff, and that was buying a
> whole input mode to deliver information the player can simply be told.
>
> **`DoomPreviewer` is NOT deleted.** `DoomConsole` prints it every turn, it is what caught the
> lanes regression for free, and it has tests. It loses its UI surface, nothing more. If a preview
> ever returns to the screen it must still be `DoomPreviewer.Preview` — never a second hand-written
> account of a scenario, which would drift from the scenario and have the player planning around a
> lie.

## Build order for v3  [the next thing to do]

The v3 pass changes combat, so build it the way the lane switch was built: rules first in `DoomCore`,
measured in `DoomConsole`, and only then into Godot. The lane rewrite was a **net deletion** that
touched no file above `DoomCore/Actions/` — expect the same shape here.

1. **Units withdraw at end of turn**, and the doom fires BEFORE the withdrawal. Withdrawn ≠ dead.
   Pin the ordering with a test that a firing sees a non-empty field.
2. **Any lane is always playable; the held unit goes to Discard, no refund.** Delete the refusal in
   `PlayCardAction.ValidateAdd`.
3. **Rescale with `sim`, not by reasoning.** Everything moves. Expect Opponent health to fall hard
   and costs to compress toward 0-2. Add the persistence break-even turn as its own table line.
4. **Intent sequences, with Piercing and Shifting first.** Without these the turn is the same
   bin-packing problem every turn, and persistence has no counterplay.
5. **`Persistent`, at uncommon and rare only.** After step 4, never before it.
6. **The Companion: ability, chosen lane each turn, death and resummon.** Marks stay stat bumps.
7. **Flood's replacement**, and a pass over the six `FiringRead.Standing` scenarios to check what
   each now asks on a firing turn.

Then re-read the front end: `DoomBoard` shows a board that empties every turn, `DoomLaneCell` needs a
persistent/ephemeral tell, and the companion needs a visible down-and-resummonable state.

## MVP — v1, kept as the record of how the battle layer was built

**One Opponent per battle, one scenario per battle, repeating on its interval. No acts, no
multi-battle chains.** Lanes need 2-4 enemy units to be a decision — one unit across five lanes is
covered by one of yours and stops being a threat — so `StarterContent.EnemiesFor` scales the count
with the floor up to the 5-lane cap, and the Opponent refreshes them as they die.

**Build order:** Opponent entity + the win condition → recurring dooms (the clock resets instead of
ending the battle) → the `ScopeOf` tag and its guard → then scenarios, which is where the content is.

Cut from v1, revisit only after playtesting: scenario/enemy pairing, scenarios as multi-battle "acts",
relics, multiple companions.

## Remote work and build targets

Measured 2026-09-12 on Godot **4.6.3.stable.mono** (`godot-mono`; the `godot` on PATH is the standard
build and cannot run C# at all). Spike project: `scratchpad/webspike/`.

**Web export is impossible on the OFFICIAL build.** Not experimental — the engine refuses at the
configuration check:

> `Exporting to Web is currently not supported in Godot 4 when using C#/.NET. Use Godot 3 to target
> Web with C#/Mono instead.`

**But an unofficial build does it.** `ComplexRobot/godot-dotnet-web-export` is a Windows Godot editor
with raulsntos's web-export PR merged, prebuilt, tracking current Godot. Latest release **4.7.2-stable**
(18 Aug). Requirements:

- Godot **4.7.2** — an engine upgrade from 4.6.3
- .NET SDK 9.0 and `TargetFramework` **`net9.0`** — same TFM Android needs, so one change serves both
- `wasm-tools` workload; a `Program.cs` with at least one top-level statement; run its `install.bat`

Breaks: GDExtension (runtime built without position-independent code), globalization (invariant mode),
some BCL APIs including crypto. **None of these touch a card game.**

Official support is `godotengine/godot` PR **#118976** ("[.NET] Add support for web export using static
LibGodot") — still a **draft** at milestone 4.x, so not in 4.6 or 4.7. It supports .NET 9 and 10 and
was reported working on a complex project in May 2026. Coming, not here.

**DECIDED: no web export, no phone build.** Stay on Godot **4.6.3** / **net10.0**. Remote work happens
through `DoomConsole`; the Godot UI is tested on the local machine only. The fork was viable but its
cost is an engine upgrade plus a TFM downgrade to chase a build we do not need, and it exports from a
Windows editor anyway — so it never delivered the "build from a remote session" goal that started this.
**Do not reopen unless the goal changes.**

**Android export works**, and failed only on a version mismatch:

> `C# project targets 'net10.0' but the export template only supports 'net9.0'. Consider using gradle
> builds instead.`

Fix by targeting `net9.0` or enabling gradle custom builds. Templates are not installed yet (nothing
under `%APPDATA%/Godot/export_templates/`), and no `wasm-tools` workload is present.

**What this means for working remotely.** `DoomCore` is plain C# with no Godot types, so a cloud
session can build and test the entire rules engine with `dotnet test` and no Godot at all — which is
where most of the bugs will be. Godot scene work, rendering, input and exports need the local machine.

**A `DoomConsole` is the high-value piece for remote playtesting** — the same trick `MtgConsole`
already pulls. A terminal front end means the game is playable from anywhere a `dotnet run` works,
including a remote session, with no Godot in the loop. It is also the fastest way to iterate on rules
during a jam. Build it early.

**`SQGodotCommon.csproj` references `MtgCore` and `MtgSimulator`.** Dead weight in every build and
every export, and it keeps the off-limits MTG code compiling alongside ours. Dropping the references
requires excluding `SQGodotCommon/MtgGame/` from compilation, since it depends on them — a real task,
not a one-liner. Low priority with no mobile target; do it if build times bite or before shipping.

**GO SPINNY was unconstrained** once phone stopped being a target — a real mouse wheel was
available. Moot now: the sub-theme is dropped (see Sub-themes), so no input mode is needed for it.

## Open questions

**Raised by the recurring-doom model, and unresolved:**

- ~~The Companion is marked per doom survived~~ **RESOLVED, and it needed no code.** `Marked` is
  called once, in `Run.AfterBattle`, not per firing — so it was already once-per-battle by
  construction. Verified in play: three Nuclear firings in one battle, one Glowing mark. The
  run/battle split protected it, the same way it protects the companion from transforms.
- ~~What makes the first firing unraceable?~~ **DROPPED as a rule (2026-09-14).** Apocalypses are
  dodgeable; inevitability is per-battle content now. What replaces it as an open question:
  **which battles should be undodgeable, and how is that expressed?** Opponent HP is the blunt
  lever. The sharp one is an enemy ABILITY — "cannot be killed while the countdown is running" —
  which makes a specific fight a guaranteed apocalypse without touching any other battle's tuning.
  Not built; `Enemy` has no ability system at all yet.
- **Is dodging ever strictly correct?** It should not be: it skips the transform AND the companion
  mark, so it trades power for safety. Wants playtesting — if a dodged run beats a fed one, the
  apocalypses are not paying enough.
- **Is `SummonInterval` 2 the right rate?** It is the dial that decides whether a player can get
  ahead at all: at 1 it exactly matches killing one unit a turn and the board never opens. Wants
  playtesting.
- **Does a battle-only doom hit the board, the hand, and the draw pile — or only the board?** Flood
  as written only washes the board. Scenarios that reach into the draw pile mid-battle are a
  different and more dangerous class.
- ~~Does `DoomPreviewer` still work?~~ **RESOLVED.** A battle-scope preview runs the real board
  effect and diffs the FIELD — "2 swept off the board — nothing permanent" — rather than diffing a
  deck it never touches. Same rule as before: never a second, hand-written account of a scenario.
  Both the preview and the real firing build their snapshot with `DoomFiring.Capture`, so the dial
  cannot disagree with the apocalypse it predicts.
- ~~Ashfall leaves the Companion no mark~~ **MOOT (2026-09-18): marks are cut.** The finding it
  recorded is still worth keeping, because it was never really about Ashfall — a DEFAULT VALUE that
  looks like content is a silent no-op, and `ScenarioDefinition.Mark` defaulting to "Unscathed" is
  how one shipped. Original entry:
- **Ashfall leaves the Companion no mark, and that is content, not a preference.** Of fourteen
  apocalypses, only Rapture (unimplemented, never offered) and **Ashfall** author no
  `CompanionMark`. Ashfall is playable — floor 2 of The Reckoning — so surviving it hands the
  Companion a 0/0 mark literally named "Unscathed" that does nothing and clutters its name.
  **This is the exact silent no-op `ScenarioDefinition` already warns about**: its own comment says
  `MarkFor` was moved off an enum switch precisely because new apocalypses were "handed a mark that
  did nothing and said Unscathed" — and the DEFAULT VALUE still does it. Found 2026-09-17 by
  rendering the maximal companion name; not fixed, because choosing the stats is a balance decision
  and it moves The Reckoning's curve. Fix it with a test that every implemented scenario authors a
  mark, or the next one added will do the same thing.
- **The Companion's name grows without limit.** Repeats are collapsed for display now
  (`Ash — Hardened x3`), which stops it leaving the screen, but the length is still bounded only by
  the number of distinct apocalypses. A name is not a good place to store a run's history; a list
  on the intermission might be.
- **Does the Opponent attack on its own**, or only through its units? Currently only units exist.
- **Is Opponent HP the difficulty dial, or the doom interval?** Probably both, but one should lead.

**Older, still open (pre-v3):**

- **NOTHING CURRENTLY EMPTIES A DECK.** Flood was the only thing that removed cards and it is a
  board wash now, so `Run.HasNoCards` is unreachable. The rule is kept as the floor under any future
  scenario that removes cards, and its test asserts it directly rather than through a doom that can
  no longer cause it. It also means **deck attrition is no longer the backstop** for a battle that
  will not end — Irradiated's life-on-draw is, and that only applies on Nuclear floors. An
  unwinnable battle against an Opponent you cannot out-damage currently has no ending at all.
- ~~Should enemies be able to SHIFT lanes between turns?~~ **RESOLVED — YES, and it is now
  REQUIRED (2026-09-17).** Shifting is one of the two intents that guard persistence: without it,
  a persistent unit can be parked in a quiet lane for ever. See "Enemies must have PATTERNS".
- How many battles is a full run?
- ~~Does the Companion have an activated ability, or only its accumulated marks?~~ **RESOLVED
  (2026-09-17): it has an ABILITY, and the ability is the point.** It is the synergy driver and
  the build declaration — see "TAG ALONG". Marks stay plain stat bumps.
- ~~Should the player choose between several companions at run start?~~ **RESOLVED — YES
  (2026-09-17).** It is the game's character select and its primary build declaration, made before
  floor 1. A companion is one record with a `DoomEffect` list, so a roster is content, not a system.
**Raised by the v3 design pass (2026-09-17), and unresolved:**

- **What does resummoning the Companion cost?** Decided: it costs something, it takes part of your
  turn, and it ESCALATES within a battle so suicide-to-heal is never correct. The numbers are open.
  The commander tax is the model.
- **What replaces Flood?** Decided: it changes or it is cut — washing the board is a no-op once the
  board washes itself. Candidates: wash the HAND, or skip next turn's draw. It is the scenario that
  teaches the fiction on floor 1, so a replacement is worth more than a deletion.
- **Does the whole hand still discard every turn?** Lean: yes. Two ephemeral economies that match is
  the entire point of v3, and banking cards reintroduces the hoarding Famine already punishes.
- ~~Flat energy, or a ramp inside a long battle?~~ **RESOLVED (2026-09-17): flat, and scaling is a
  deckbuilding outcome.** No energy ramp, ever — see the rule in Combat v3. What is still open is
  whether reinforcement scaling is STEEP enough to end an unwinnable battle promptly; that is a
  pacing number for `sim`, not a mechanism.
- **How many persistent cards should a 20-floor act put in a deck?** At uncommon and rare only, the
  answer is roughly three to five, which means the Companion is the only persistent body for most of
  a run. That is intended — but it is the assumption the whole synergy layer rests on, so measure it
  rather than believing it.
- **Which intent patterns does each enemy get?** Piercing and Shifting must exist before the first
  persistent card ships. The rest is content.
- **Does the Opponent itself get an intent pattern**, now that enemies have one?

**Older, still open:**

- Deck size and starting deck composition
- Does anything let you *change* the doom interval, or is it strictly fixed? (lean: strictly fixed,
  except the rare card keyword that burns it)

**Closed by this revision:** whether reinforcements arrive mid-battle (yes — the Opponent refreshes,
and it is core rather than one scenario's gimmick), and whether Flood needs a floor on how much it
removes (moot — it no longer removes anything).

**Closed by the v3 pass (2026-09-17):** whether units persist (no, by default), whether a lane can be
replayed into (yes, always, no refund), whether the Companion can die (yes, and it must be able to),
where it goes when it does (to hand, resummoned for an escalating cost), whether attachments are
built (no — cut in the same pass that removed the problem they solved), and whether marks become
effects (no — the ability carries identity now).

## Dead air — SOLVED, kept as the reasoning

An earlier model had the countdown end the battle, which made a cleared board into dead air: nothing
to do but press end-turn until the dial hit zero. Two fixes were considered — making the dooms read
lanes so filling all five stayed urgent, and enemy reinforcements.

**Neither was built, because the recurring-doom model removed the problem rather than patching it.**
There is always an Opponent left to kill, so there is always something to do with a turn. Recorded
so the patch is not reinvented for a problem that no longer exists.

## Engine findings

What `ImmutableGameObjects` gives us for free, and what had to be built. **Fill this in as we go — it
is the deliverable for "how flexible is this engine?"**

**After porting effects and making enemies, Opponents and scenarios into content (75 tests green):**

- **The ported effect system is ~250 lines, against MtgCore's ~1600.** `EffectAction`, `DoomEffect`,
  `DoomTargeting`, `ResolveEffectsAction` and four concrete effects. The shape is MtgCore's exactly:
  one action per effect holding all its targets, a resolver that resolves, injects and spawns.
- **Everything that was expensive there was targeting, and targeting is a CHOICE.** MTG needs 523
  lines because a player picks. DOOMJAM targets are RULES the board can answer — "the Opponent",
  "every enemy", "your unit in this lane" — so `DoomTargeting` is one switch. **The saving did not
  come from writing less; it came from the design having deleted targeting.**
- **`DoomEffect` never learned what holds it**, and that is the whole return on putting it in its own
  folder. Cards got effects first; giving them to `Enemy` and `Opponent` afterwards changed nothing
  about `DoomEffect` and no existing effect needed rewriting.
- **A battle apocalypse is now DATA and a permanent one cannot be.** This asymmetry is the finding.
  A battle scenario changes this `GameState`, so it is a list of effects like anything else —
  Ashfall was added as one enum entry and one library entry, with no case in any hook. A PERMANENT
  one rewrites the run deck, and **the run deliberately lives outside `GameState`**, so it cannot be
  a `GameAction` at all and stays a `(run, firing) -> run` function in `DoomTransforms`.
- **That boundary is the same one that has paid off four times now** — it is why the Companion needed
  no work, why rewriting combat touched nothing above `DoomCore/Actions/`, and why a doom can rewrite
  a deck the battle never sees. The price of it is that the effect system stops at the edge of the
  battle, and that is the right trade rather than a limitation to fix.
- **`EndTurnAction` stopped reaching into `StarterContent`.** The Opponent carries its own
  reinforcement, so the engine asks the Opponent what it fields. The old call had no way to learn
  the floor, so tiered reinforcements were impossible before this.


**On reusing MtgCore's effect system (asked 2026-09-15, answered by reading it):**

DOOMJAM has no card effects at all — every `DoomCard` is a vanilla body, Cost/Power/Toughness — so
the obvious question is why we do not lift MtgCore's. **This is exactly the finding the project
exists to produce, so it is recorded rather than quietly acted on.**

- **`MtgCore/Effects/CardEffect.cs` is 22 LINES**, and it is only a pairing: a `TargetingStrategy`
  plus a `GameAction` template. On resolve the engine resolves targets, injects ids and spawns the
  action. **That shape is genuinely reusable, and it is tiny.**
- **The size is all targeting.** `TargetingStrategy` (175) + `TargetSpecifications` (348) is 523
  lines of choosing what a spell points at — the exact thing this design deleted. There is no
  targeting anywhere in DOOMJAM; you pick a lane and combat resolves itself.
- **The machinery is already shared and already reused.** `GameAction`, `ActionResult`,
  `ValidationResult` and the spawn queue are 163 lines in `ImmutableGameObjects`, and DoomCore is
  built on them with no engine change at all. `PlayCardAction` already spawns follow-ups — that IS
  the effect pipeline.

So a DOOMJAM effect system is **not a port**: it is a serializable effect record on `DoomCard` plus
a case in `PlayCardAction` that spawns the matching action. With no targets to choose, the strategy
half collapses to nothing. Estimate 40-60 lines.

**The answer to "isn't that what the engine was designed for?" is yes, and it worked** — the
reusable layer is `ImmutableGameObjects`, and it carried a completely different card game unchanged.
What does not transfer is the MTG rules layer above it, and that is not the design failing: it is
the boundary showing itself in the right place.


**After building the battle layer (DoomCore, 10 tests green):**

- **`ImmutableGameObjects` needed no changes at all.** `GameState`, `GameObject`, components,
  `GameAction`, `AddObject`/`MoveObject`/`UpdateObject` and the spawn queue carried a completely
  different card game with zero friction. Nothing was bent to fit.
- **Most of MtgCore turned out to be unnecessary, not unavailable.** No stack, no priority, no
  `PipelineAction`, no `ChoiceAction`, no `PostActionProcessor`, no targeting specs, no replacement
  engine. A battle is a straight sequence, so actions spawn exactly one follow-up each.
- **Nothing was copied from MtgCore.** The one concept re-derived from scratch is `Zone` + `ZoneType`
  — 20 lines, and the enums differ entirely (Draw/Hand/Discard/Field/Enemies vs Library/Graveyard/
  Battlefield/Exile/Stack). Every card game needs zones; none of them need the *same* zones.
- **`MoveObject` appends to the end of the child list**, so shuffling is "re-parent every card in the
  new order" (`DoomRng.ShuffleZone`). Non-obvious, and the only engine behaviour that needed reading
  the source to discover.
- **Combat is ~120 lines.** Attack-or-block, absorption with excess, deaths. The absence of blockers
  in MtgCore turned out not to matter — the rules here are different enough that shared code would
  have been wrong anyway.

**After building the run layer (19 tests green):**

- **`Run` is a plain immutable record, not a `GameObject`.** It holds life, deck, floor and the
  RunCardId counter, and lives entirely outside `GameState`. `Run.BuildBattle` makes a fresh
  GameState; `Run.AfterBattle` reads the finished one back. Nothing in the engine had to change to
  allow a layer above it — it simply never assumed it was the top.
- **`RunCardId` is the whole bridge.** Battle object ids die with the battle, so the battle records
  `SummonedRunCardIds` / `DiedRunCardIds` as it goes and the transform names deck entries by those.
  Flood is what forced it: "duplicate what you summoned" is unsayable in battle ids.
- **Every apocalypse really is one function**, `(run, finished battle) -> run`, in
  `DoomTransforms.Apply`. Zombie, Nuclear and Flood are ~10 lines each. The hook held.
- **Rapture throws instead of no-opping** — it needs a sacrifice mechanic that does not exist, and
  an apocalypse that silently does nothing looks exactly like one that worked.
- Nuclear needed one battle-time rule to not be a pure upside: Irradiated costs 1 life **on the
  draw**, so declining to play the card does not dodge the price. That also made death checkable at
  turn START as well as end.

**After the preview and the console (24 tests green):**

- **The doom preview runs the REAL transform and diffs the decks** (`DoomPreviewer.Preview`). It does
  not describe each scenario in its own words — a second hand-written account of Flood would drift
  from Flood and the player would be playing around a lie. Same rule as MTG's `Explain`/`Evaluate`.
  Every future scenario is previewable for free.
- **`Run.BuildBattle` + `BeginBattle` was a trap and is now one call, `Run.StartBattle`.** A built
  but unbegun battle looks ready and has an empty hand; three tests passed only because `EndTurn`
  spawns `StartTurn` and drew on the second loop. Nothing ever wants an unbegun battle.
- `StartBattle` returns the opening events, because the first hand can already hurt you — an
  Irradiated card costs a life the moment it is drawn.
- **The console is the remote surface and it works**: play, attack, block, end, and the whole
  countdown-to-doom loop, verified by running it rather than by reasoning about it.

**After the Companion (34 tests green):**

- **TAG ALONG cost almost nothing because of where the run/battle split already was.** The companion
  is immune to every apocalypse without a single special case, purely by living on `Run.Companion`
  rather than in `Run.Deck` — the transforms all operate on the deck and never saw it.
- The one real interaction needing a guard was the reverse direction: a companion DYING fed Zombie a
  free card, because deaths are counted by run id. Guarded in `ClearTheDead`.
- Marks are a plain `ImmutableList<CompanionMark>` summed into Power/Toughness. No engine feature
  was needed — this is the "accumulating component list" the design predicted, and it is simpler.

**After the Opponent, recurring dooms and scope (48 tests green):**

- **The run/battle split paid a third time, and this time it answered a question for free.** The
  Companion was supposed to need work — marked per doom survived, with dooms now firing several
  times a battle. It needed none: `Marked` is called in `Run.AfterBattle`, not per firing, so it was
  already once-per-battle. Twice now the split has made a feared interaction a non-event.
- **Changing the rule that a counter ends the battle broke six tests, and every one of them was
  RIGHT to break.** They asserted the superseded rule. The suite behaved as a design record: it told
  us exactly which beliefs the change invalidated, and rewriting them was the honest cost.
- **A recurring doom cannot be a single end-of-battle read.** `DoomFiring` captures what each firing
  saw, in RUN ids, and the run folds them. Without it a Nuclear that fired twice would irradiate the
  final board twice and the earlier board never — correct-looking until a battle runs long.
- **Deaths must be CONSUMED by the firing that reads them.** A cumulative list pays Zombie for the
  same corpse on every later firing, so a long battle mints an exponential pile. Found by reasoning
  about it, pinned by `ADeathIsPaidForByExactlyOneFiring` — the sort of bug that is invisible at
  countdown 2 and ruinous at countdown 1.
- **The preview and the firing must share one capture.** Both call `DoomFiring.Capture`. Two copies
  would drift and the player would plan around a dial that no longer matched the apocalypse — the
  same rule that already stops the preview describing scenarios in its own words.
- **Two hooks split by what they may touch is not a second mechanism**, but it does create a silent
  failure: a scenario in the wrong one does nothing. Both throw instead, following the precedent
  Rapture set.
- **The console found the bug the tests could not.** `OpponentDamagedEvent` was raised and never
  rendered, so the Opponent's health dropped with nothing on screen saying why. Tests asserted the
  state and passed. Playing it took ten seconds to notice. Same class as the blank card faces.

**After v3 phase 1 — units withdraw at end of turn (116 tests green):**

- **The engine needed nothing, for the third combat rewrite running.** One new `GameAction`, one
  `SpawnAction` line, and a method made `internal`. `ImmutableGameObjects` was not touched, and
  neither was anything above `DoomCore/Actions/` — the run layer, the transforms, the preview and
  the companion all carried on. **That is now three combat models on one engine with zero engine
  changes**, which is a much stronger claim than the lane switch made on its own.
- **The spawn queue is an ORDERING mechanism, and that is a two-sided finding.** Being able to say
  "this happens after the apocalypse" is just `SpawnAction` in the right place — free. But the
  FIFO queue also means the *natural* implementation is silently wrong: withdrawing units inline in
  `EndTurnAction.Execute` runs before the spawned `ResolveDoomAction`, so six scenarios would have
  read an empty board and done nothing. **The engine cannot warn about this**, because both
  orderings are valid action sequences. An ordering guarantee in a spawn queue is only ever as good
  as the test holding it — `ADoomFiringSeesTheBoardYouCommitted` is that test.
- **A "leaves the board" step must clear the dead FIRST, and the tests did not catch it — a
  NullReferenceException did.** The dead are cleared early in `EndTurnAction`, and the apocalypse
  resolves after that, so a unit the doom KILLED was still standing when withdrawal ran. It was
  being discarded as a survivor: no `OnDeath`, nothing in `DiedRunCardIds`, and Zombie never paid
  for a corpse it was owed. **The fix was to call the existing `ClearTheDead` a second time rather
  than to write a second account of dying** — same rule as the preview never re-describing a
  scenario. Generalises: **any new step that empties a zone has to ask what the engine had not yet
  finished doing to the things in it.**
- **Six tests broke and every one was right to break**, exactly as the lane switch found. They
  asserted the accumulating board — a unit holding its lane across turns, a firing seeing two units,
  a hole you shot through for free. The suite behaved as a design record again and named precisely
  which beliefs the change invalidated.
- **The preview changed MEANING without changing code, and that is the dangerous kind.**
  `DoomPreviewer` still runs the real transform, so it is still not a second account of anything —
  but "what would the next firing do" used to be stable for several turns and is now true only on
  the turn the doom actually lands, because the board it reads no longer survives the turn. Nothing
  failed; two tests simply started describing a board that will not exist. **A correct function can
  become a lie when the thing underneath it changes lifetime**, and no test asks that question on
  its own.

**After switching combat to five lanes (39 tests green):**

- **Changing the core combat rule was a NET DELETION.** `AssignAction` (64 lines) and the whole
  assignment model went; the two resolve passes (~78 lines) collapsed into one loop over five lanes.
  Nothing in `ImmutableGameObjects` had to change, again — a `GameAction` that takes a lane instead
  of a target is the same shape of action.
- **The run layer did not notice.** Transforms, the preview, the companion and `RunCardId` all read
  *what died / what was left / what you committed*, none of which is a combat concept. The run/battle
  split paid for itself a second time: rewriting combat touched no file above `DoomCore/Actions/`.
- **The preview caught the regression for free.** `DoomPreviewer` runs the real transform, so the
  moment lanes changed what ends up on the board the preview followed with no work — the "never write
  a second account of the rules" decision continues to pay.
- **One subtlety worth keeping: read both sides of a lane BEFORE writing either.** Resolving a lane
  by applying the unit's damage and then re-reading the enemy would let whoever resolved second swing
  with stats the first had already reduced. `AUnitAndAnEnemyThatKillEachOtherBothDie` pins it.
- The only thing that needed a real decision rather than a mechanical port was the **companion's
  lane** — it holds the centre, so `EnemiesFor` spreads enemies outside-in and the free blocker is the
  last lane contested rather than pre-matched with the only enemy.

Rules settled while building, beyond the design doc: hand is **drawn to 5 and discarded every turn**
(STS), units have **no summoning sickness** (a 2-5 turn battle cannot afford it), dead units go to
Discard and **cycle back into the deck** — only a doom transform can remove a card from a run.

Established before writing any code:

- `ImmutableGameObjects` is genuinely game-agnostic. Grepping for "mtg" hits **six doc comments and
  zero code**. GameState, GameAction, PipelineAction, ChoiceAction, components, the spawn queue and the
  PostActionProcessor know nothing about Magic.
- `SQGodotCommon/Common/Cards/2D/` is free: `CardUI2D` + `Hand2D` are ~1600 lines of
  drag/hover/select/fan with no MTG in them.
- `MtgCardMapper.cs` is 2084 lines and is **entirely** MTG. Do not generalise it; write a small new
  mapper.
- **Blocking does not exist in MtgCore at all** — its combat has no blockers. Genuinely new code.
- **The run has no precedent.** MtgCore has no concept of state above a single game. A deck that
  persists across battles and mutates between them is a plain list owned by a run controller, with each
  battle building a fresh `GameState` from it. Likely the most interesting part of the answer.

## Rules of engagement

- **Do not touch `MtgCore`, `MtgSimulator`, `MtgConsole` or `SQGodotCommon/MtgGame/`.** This branch
  must stay a clean no-op for the MTG work.
- New game code lives in `DoomCore/` (engine) and `SQGodotCommon/DoomGame/` (Godot front end).
- Copy nothing from `MtgCore`. If we want something from it, that want is a **finding** — write it down
  above before reimplementing it.
