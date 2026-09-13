# DOOMJAM — Godot Wild Jam design doc

**Branch `GWJ-ImminentDoom` is NOT the MTG game.** Theme: IMMINENT DOOM. Sub-themes: TAG ALONG
(required), GO SPINNY, PERSPECTIVE SHIFT. 9-day jam.

The second goal is a measurement: **how hard is it to build a completely different card game on
`ImmutableGameObjects`?** Whatever we end up wishing we could lift out of `MtgCore` is the finding.
Record it under "Engine findings" as we hit it.

## Pitch

A solitaire roguelike deckbuilder. Each battle is a **doomsday scenario on a countdown**. You fight
the enemies in front of you while positioning for an apocalypse you cannot prevent — and when it
lands, **it permanently rewrites your deck** based on what you did.

## The one rule that must not bend

**The countdown always runs out. The doom always resolves. It can never be prevented.**

If clearing the enemies could end a battle early, the player has *beaten* the apocalypse and it is an
obstacle, not doom. So:

- The doom is a **scheduled event**, not a threat. It costs no life directly.
- The **enemies** are the threat. They hit your life total, the only fail state.
- Fighting well and positioning for the doom **pull in opposite directions**. That squeeze is the game.

## The core hook

> **The doom doesn't kill you. The doom edits your deck.**

Every scenario is one thing: **read the board at countdown 0, apply a permanent transform to the run
deck.** One engine hook; every apocalypse after that is data. This is where content comes from — do
not build a second mechanism.

## Design rule: bargain, not tax

**Every doom converts one resource into another. None are purely bad.** A deck that only gets worse is
a misery engine players quit, and it makes progressively harder enemies unbalanceable. Tradeoffs mean
the apocalypses *are* the power curve — no separate progression system is needed.

Currency insight: **a unit absorbs rather than prevents, so toughness IS life.** A 1/1 standing in
front of a 5-damage attack is worth exactly 1 life. Creature bodies and life are the same currency in
two forms, and every scenario trades on that one axis. **This survived the move to lanes unchanged**,
which is the test any future combat change has to pass — it is what makes the dooms tradeable.

| Scenario | Reads | Transform | Countdown |
|---|---|---|---|
| **Zombie Apocalypse** | what died | deaths return as 1/1 Zombies in the deck — quantity bought with deck space | 3 |
| **Nuclear** | what was left on board | those become **Irradiated**: permanent +2/+2, lose 1 life when drawn | 2 |
| **Flood** | what you committed | creatures summoned this round **duplicate**; creatures never summoned are **removed from the deck** | 5 |
| **Rapture** | what you sacrificed | sacrificed creatures return as life — the doom you *want* at 6 HP | 3 |

Countdown length varies per scenario on purpose: it is free texture, and it makes each apocalypse feel
different before the player reads a word of its text.

**Scenarios are TIERED by floor.** Flood does not appear before floor 8
(`StarterContent.FloodUnlocksAtFloor`). On a 10-card starter deck it can delete everything and end a
run outright; losing cards is only an interesting cost once there is a deck worth losing. Expect more
scenarios to want tiering as they are added — it is a property of the floor, not of the scenario.

## Combat — FIVE LANES, resolved automatically

**The board is five lanes. One of your units and one enemy per lane. They fight each other
automatically.** There is no targeting anywhere in the game and no attack-or-block choice: you pick
a lane when you play a card, and that is the entire decision.

Enemies keep their **intent telegraphed a turn ahead** — an enemy that is winding up shows the
number it will hit its lane for. Do not hide an intent.

Two global rules, deliberately not keywords:

- **A unit absorbs up to its remaining toughness and the excess hits your face.** So a body in a lane
  is worth exactly its toughness in life. No keyword to teach, and stalling is impossible by
  construction.
- **An open lane costs you the enemy's whole attack.** Covering a lane is the only defence, and you
  cannot cover five lanes with three energy.

Damage **persists for the whole battle** on both sides — units carry marked damage, enemies carry
lost HP — so a lane is a grind you can win over two or three turns rather than a single comparison.

**Turn shape:**

1. Enemy intents already visible, per lane
2. Draw, spend Energy, place units into lanes
3. Resolve every lane at once — both sides deal damage, excess and open lanes hit your face
4. Countdown ticks; enemies declare next intents

**What this replaced, and why it is not a loss.** Combat used to be "each unit may attack OR block,
never both", which was the stated core decision. Lanes delete it and replace it with *which lanes do
I contest, knowing the rest hit my face* — the same investment-vs-survival squeeze, expressed
spatially, readable at a glance and with no targeting UI to build. "Blockers deal no damage" went
with it: it existed only to keep attack-vs-block a clean either/or, and there is no such choice left
to protect. **The currency insight survived intact**, which is what mattered — see below.

**Lanes give the dooms a spatial axis to read** ("everything in lane 3 is Irradiated") that did not
exist before. That is free content, and it is the main reason to prefer lanes beyond simplicity.

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
  great thing for this game to allow.
- Rewards are normal (card choice / relic). "You keep what you kill" is **one scenario's gimmick**,
  not a global rule.
- **Weight reward offers toward the doom just taken or about to be walked into.** Tag cards by which
  doom they answer. With ~35 cards, "couldn't find the answer" reads as *unfair* far more often than as
  *I wasn't clever enough*. It is a dictionary lookup, and it makes a run feel like a conversation.

## Sub-themes

| Theme | How | Priority |
|---|---|---|
| TAG ALONG | the Companion, above — structurally load-bearing | required |
| GO SPINNY | scroll the countdown dial to **preview what the doom would do right now** | high — it earns its place |
| PERSPECTIVE SHIFT | 2D cards over a 3D scenario backdrop that changes per apocalypse | if time survives |

The spinny dial acts on the principle that **certainty is permission to show the player everything**.
The tension here is inevitability, not surprise — so show the future.

## MVP (build this first)

**A few enemies per battle, one battle per scenario. No acts, no multi-battle chains.** Lanes need
2-4 enemies to be a decision — one enemy across five lanes is covered by one unit and stops being a
threat — so `StarterContent.EnemiesFor` scales the count with the floor up to the 5-lane cap.

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

**GO SPINNY is unconstrained** now that phone is not a target — a real mouse wheel is available, so the
doom-preview dial can be built as the jam intends.

## Open questions

- **Does an empty deck still end the run now that the companion exists?** It used to be strictly
  unwinnable; with a companion you always have one blocker, so it is merely grim. Currently still an
  instant loss (`Run.HasNoCards`).
- Should Flood have a floor — never removing your last N units — rather than only being tiered late?
- Do reinforcements arrive mid-battle? (lean: no — fixed at battle start, reinforcements as one
  scenario's gimmick). **This is now also the escape hatch** for the dead-air problem below, if
  filling lanes turns out not to be enough on its own.
- Should enemies be able to SHIFT lanes between turns, so a defender can be dodged? Costs a movement
  rule to telegraph; buys a reason to keep reacting after the lanes are covered.
- How many battles is a full run?
- Does the Companion have an activated ability, or only its accumulated marks? (currently marks only)
- Should the player choose between several companions at run start? (currently one, "Ash" 1/3)
- Deck size and starting deck composition
- Does anything let you *change* the countdown, or is it strictly fixed? (lean: strictly fixed, except
  the rare card keyword that burns it)

## Clearing the enemies early must not be dead air

Lanes make it plausible to kill everything before the countdown ends, and the one rule that must not
bend says the battle cannot end early. So those turns have to be worth playing.

**They already are, in principle:** enemies threaten your LIFE, the doom edits your DECK. Clearing
the lanes removes the first and none of the second, so the remaining turns are spent positioning for
what the doom reads — which is exactly the squeeze the design is built on.

**The cheap way to make that true in practice: make the dooms read LANES, not just the board.** An
empty lane at countdown 0 is a wasted slot, so you are racing to fill all five whether or not
anything is still attacking. Zero new mechanics — it makes lanes load-bearing for the doom rather
than only for combat. Reinforcements stay in reserve as the escape hatch if playtesting says it is
still flat.

**Not yet built.** The scenarios currently read the board without caring where anything stands.

## Engine findings

What `ImmutableGameObjects` gives us for free, and what had to be built. **Fill this in as we go — it
is the deliverable for "how flexible is this engine?"**

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
