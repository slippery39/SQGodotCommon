# DOOMJAM — UI design

**Read `DoomJam.md` first.** This doc is downstream of it: the game design decides what is true, this
decides how it is shown. Where they disagree, `DoomJam.md` wins.

Status: **design only. `SQGodotCommon/DoomGame/` does not exist yet.**

## The rule this doc exists to enforce

> **The UI never computes a game fact. It reads one.**

Every number on screen comes from `DoomStateExtensions` or `DoomPreviewer`, never from arithmetic in
a presentation script. This is the same rule that stops `DoomPreviewer` writing a second account of
each scenario, and it is the rule `MtgCardMapper` broke at 2084 lines. A UI that computes "how much
damage will I take" is a second rules engine that will drift from the first.

`DoomConsole/Renderer.cs` is the reference implementation of this contract and **the more trustworthy
spec of the two** — it is the information set that survived real play, and it is where the
`OpponentDamagedEvent` blind spot was caught. Build against it, not against the mockup.

## Style — flat vector, chosen 2026-09-14

Two mockups were generated, painterly-grimy and flat-vector. **Flat vector wins on producibility:**
silhouettes and a fixed palette are something one person can draw twenty more of during a jam.
Painted creature art is not.

- Bold geometric shapes, thick clean outlines, solid colour, no texture or gradients
- Characters are **silhouettes with eye dots**, not illustrations
- Five colours, and two of them are reserved:

| Colour | Means | Used for |
|---|---|---|
| Gold | **yours, and precious** | the Companion, energy pips |
| Red | **the enemy, and life** | Opponent health, enemy health, unit remaining toughness |
| Navy / slate / off-white | everything else | frames, empty slots, text |

**Red doing double duty is deliberate, not a collision.** A unit absorbs up to its remaining
toughness and the excess hits the face behind it, so a body in a lane is worth exactly its toughness
in life — the same currency in two forms. One colour for both says that without a tutorial.

Gold being unique to the Companion is what makes it findable in a five-lane board, the same job the
`@` prefix does in the console today.

## Layout contract

Five bands, top to bottom. Each region names the state it reads and the event that should animate it.

| Band | Region | Reads | Animated by |
|---|---|---|---|
| 1 | Scenario name + countdown | `Battle.Scenario`, `Battle.CountdownRemaining` | `CountdownTickedEvent`, `DoomResolvedEvent` |
| 1 | Flavour line | static per scenario | — |
| 1 | **The dial (GO SPINNY)** | `DoomPreviewer.Preview(run, state)` | scroll input |
| 2 | Opponent silhouette + health bar | `GetOpponent().Health` / `.MaxHealth` | `OpponentDamagedEvent`, `OpponentDefeatedEvent` |
| 3 | Enemy lane slots x5 | `EnemyInLane(n)` — name, `Health`, `Intent`, `IntentAmount` | `EnemySummonedEvent`, `EnemyDiedEvent` |
| 4 | Your lane slots x5 | `UnitInLane(n)` — name, `Power`, `RemainingToughness` | `CardPlayedEvent`, `UnitDiedEvent` |
| 4 | Companion ring | `HasComponent<CompanionComponent>()` | — |
| 5 | Status strip | `Run.Floor`, `Player.Life/MaxLife`, `Player.Energy/MaxEnergy`, `Battle.TurnNumber` | `PlayerDamagedEvent`, `IrradiatedDrawnEvent`, `PlayerDiedEvent` |
| 5 | Hand | `CardsIn(ZoneType.Hand)` — `Cost`, `Name`, `UnitComponent`, `Tags` | `CardPlayedEvent` |

**Input is one verb.** Drag a card onto a lane slot, or click card then lane. There is no targeting
anywhere in this game and no attack-or-block step — the lane IS the decision. Do not build a
targeting system; there is nothing to target.

A play must go through `PlayCardAction.ValidateAdd` and show the engine's own refusal text. The
console prints `can't: Lane 2 is already held by Ash`; the UI must not silently drop the drag. **A
click that does nothing is the worst bug a card game front end can have** — that is how the `c`
command shipped unwired for two sessions.

## What the mockup is missing

The mockup is a good skeleton and the bands are right. These four readouts exist in the console and
have no home in it. Three are load-bearing.

1. **The dial has no readout.** It is drawn as a pretty gauge with nothing to say. The whole point of
   GO SPINNY is `preview.Summary` — *"Flood: 1 swept off the board — nothing permanent"* — plus the
   LOSE / GAIN / CHANGE lists for permanent scenarios. **A dial without its preview is decoration,
   and the sub-theme stops earning its place.** Put the readout in a panel that opens off the dial.
2. **No summon telegraph.** `Opponent.NextSummon` announces a body a turn ahead, and the handoff is
   explicit that this delay is load-bearing rather than polish — a lane that refilled instantly
   makes the Opponent unreachable. Draw it **as a ghost card in the target lane slot**: dashed gold
   outline, stats shown, clearly not yet real. Spatial beats the console's text line.
3. **No outgoing total.** *"your open lanes will hit the Opponent for 3."* Put it on the Opponent's
   health bar as an arrow and a number.
4. **No incoming total.** *"open lanes will cost you 5 life this turn."* Put it on the life value in
   the status strip, same treatment.

3 and 4 are per-lane information the player *could* add up themselves. The console added them for a
reason, recorded in `Renderer.cs`: **making the player total it per lane is how a lane game becomes a
chore.**

## Not decided

- Where the discard and draw piles are shown, or whether they are. The console does not show them
  and has not missed them.
- Whether the 3D scenario backdrop (PERSPECTIVE SHIFT) ships. It is flagged "if time survives" in
  `DoomJam.md`. The flat style makes a per-apocalypse backdrop cheap — three geometric layers — so
  it may survive after all.
- Card art. Currently one flat icon per card. Twenty of those is a real afternoon.
- Whether `Common/Cards/2D/` (`CardUI2D` + `Hand2D`, ~1600 lines of drag/hover/fan with no MTG in
  it) is reused as-is or trimmed. It is free and it is the reason the hand band is not a build task.
