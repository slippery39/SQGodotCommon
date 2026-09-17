# DOOMJAM — Godot Wild Jam design doc

**Branch `GWJ-ImminentDoom` is NOT the MTG game.** Theme: IMMINENT DOOM. Sub-themes: TAG ALONG
(required), GO SPINNY, PERSPECTIVE SHIFT. 9-day jam. **That is what the jam OFFERED — see
Sub-themes for which are actually being built.**

The second goal is a measurement: **how hard is it to build a completely different card game on
`ImmutableGameObjects`?** Whatever we end up wishing we could lift out of `MtgCore` is the finding.
Record it under "Engine findings" as we hit it.

## State of play (2026-09-17)

**The whole loop is built and playable in Godot.** Pick an act, fight down twenty floors, take
rewards between them, eat or dodge the apocalypse on the clock, die or finish. `DoomConsole` is
still the remote surface and still the faster way to test a rules change.

**BUILT:** five-lane automatic combat; the run layer; the Companion; the Opponent and killing it as
the win condition; recurring dooms; scenario scope; all three acts reachable from a picker; rewards
weighted by rarity; the full front end — board, hand, drag-to-lane, reward screen, intermission,
animation, a keyword glossary on hover. **113 tests green.**

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
| **dodge it** | unchanged — clean, thin, and no stronger than it started | **unmarked** |
| **eat it** | rewritten: stronger and more distorted | marked |

**The apocalypses ARE the power curve** — there is no separate progression system, by design. So a
player who dodges everything arrives at floor 15 with a starter deck and an Ash that survived
nothing. Speed buys safety and costs power, and the choice is real in both directions.

That is the self-correction. Enemy HP and enemy abilities are for *tuning* which battles can be
outrun, not for holding the whole structure up.

| you kill the opponent in | dooms you eat | outcome |
|---|---|---|
| before the first firing | 0 | untouched deck, unmarked companion, no power gained |
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

## TAG ALONG — the Companion  [BUILT]

**The only thing the doom cannot touch — but it keeps a mark from every apocalypse it survives.**

- On the board free at the start of every battle, no summoning cost. Also solves "short round and I
  drew badly" — the board is never empty.
- **Immune by construction, not by a special case:** the companion lives on `Run.Companion` and is
  not in `Run.Deck`, and every transform operates on the deck. Nothing had to be taught to skip it.
- Its battle card carries `RunCardId = 0`, which no deck card can hold (ids start at 1), so nothing
  mapping a battle unit back to a deck entry can find it.
- **A companion death is not a deck event.** It leaves the battle outright rather than going to
  Discard, and does not feed Zombie — otherwise chump-blocking with it minted a free card every turn.
  It returns next battle.
- Each doom survived **stamps it**, permanently and cumulatively: Gravemarked +0/+2 (Zombie),
  Glowing +2/+0 (Nuclear), Barnacled +1/+1 (Flood).
- By the last floor it is a patchwork of every ending you lived through — **the record of your run**,
  and the one thing you carried out. `Companion.FullName` renders it: "Ash — Gravemarked, Glowing".

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

## MVP (build this first)

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

**Older, still open:**

- **NOTHING CURRENTLY EMPTIES A DECK.** Flood was the only thing that removed cards and it is a
  board wash now, so `Run.HasNoCards` is unreachable. The rule is kept as the floor under any future
  scenario that removes cards, and its test asserts it directly rather than through a doom that can
  no longer cause it. It also means **deck attrition is no longer the backstop** for a battle that
  will not end — Irradiated's life-on-draw is, and that only applies on Nuclear floors. An
  unwinnable battle against an Opponent you cannot out-damage currently has no ending at all.
- Should enemies be able to SHIFT lanes between turns, so a defender can be dodged? Costs a movement
  rule to telegraph; buys a reason to keep reacting after the lanes are covered.
- How many battles is a full run?
- Does the Companion have an activated ability, or only its accumulated marks? (currently marks only)
- Should the player choose between several companions at run start? (currently one, "Ash" 1/3)
- Deck size and starting deck composition
- Does anything let you *change* the doom interval, or is it strictly fixed? (lean: strictly fixed,
  except the rare card keyword that burns it)

**Closed by this revision:** whether reinforcements arrive mid-battle (yes — the Opponent refreshes,
and it is core rather than one scenario's gimmick), and whether Flood needs a floor on how much it
removes (moot — it no longer removes anything).

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
