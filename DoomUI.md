# DOOMJAM — UI design

**Read `DoomJam.md` first.** This doc is downstream of it: the game design decides what is true, this
decides how it is shown. Where they disagree, `DoomJam.md` wins.

Status: **BUILT and played.** A full battle has been fought and won in the Godot front end — board,
hand, drag-to-lane, end turn. The layout contract below is what it implements.

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

## The first screen — theme select, built 2026-09-16

**Three columns, one per act, each showing its whole schedule.** The doom is a schedule rather than
a roll, so there is nothing to hide: floor bands, the apocalypse each holds, and its one-line
flavour, all before a card is drawn. This is the picker, the difficulty preview and the pitch at
once — `DoomJam.md` has wanted it since the schedule existed.

**Red marks a doom that REWRITES YOUR DECK**, bone one that only takes the fight in front of you.
That is the single distinction a player choosing an act is actually choosing between, and it is the
bargain the whole game is built on, so it earns the reserved colour.

Bands are read from `ThemeLibrary.BandsOf`, never grouped in the UI — a second copy of the band
arithmetic is a second schedule, and the screen would eventually advertise a run nobody plays.
`EveryFloorFallsInExactlyOneBandThatNamesItsDoom` asserts the cover.

The **seed is rolled per run and shown here**. It was a const 42, which made every playthrough of
the build byte-identical: same Opponents, same traits, same three cards on every floor. The
schedule is the fixed part; the enemies and the rewards are what the seed is for.

## Layout contract

Five bands, top to bottom. Each region names the state it reads and the event that should animate it.

| Band | Region | Reads | Animated by |
|---|---|---|---|
| 1 | Scenario name + countdown | `Battle.Scenario`, `Battle.CountdownRemaining` | `CountdownTickedEvent`, `DoomResolvedEvent` |
| 1 | What the doom does | static per scenario — see below | — |
| 2 | Opponent silhouette + health bar | `GetOpponent().Health` / `.MaxHealth` | `OpponentDamagedEvent`, `OpponentDefeatedEvent` |
| 3 | Enemy lane slots x5 | `EnemyInLane(n)` — name, `Health`, `Intent`, `IntentAmount` | `EnemySummonedEvent`, `EnemyDiedEvent` |
| 3 | Summon telegraph symbol | `GetOpponent().NextSummon` — its `Lane` | `EnemyTelegraphedEvent` |
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

## Decided against the mockup (2026-09-14)

The mockup's bands are right. Four console readouts had no home in it; three were cut deliberately.

**The doom banner says what the doom DOES, statically.** Scenario name, countdown, and a fixed
per-scenario description — *"The water takes whatever is still standing in it."* That is the whole
band. **No live preview, no dial**: GO SPINNY is dropped, and the reasoning is recorded in
`DoomJam.md` under Sub-themes. `DoomPreviewer` keeps running in the console and loses only its UI
surface.

**The summon telegraph is one symbol in the lane it is coming to.** `Opponent.NextSummon` announces
a body a turn ahead, and the handoff is explicit that this delay is load-bearing rather than polish —
a lane that refilled the instant you cleared it makes the Opponent unreachable. So the warning must
be on screen, but it does not need stats: a marker in the target lane says *this is closing next
turn*, which is the only thing the player acts on.

**The two damage totals are cut.** The console prints *"your open lanes will hit the Opponent for
3"* and *"open lanes will cost you 5 life this turn"*; `Renderer.cs` argues that totalling five
lanes by hand is how a lane game becomes a chore. Overruled for the UI, and the reason is that the
console had no better option: a terminal cannot draw a line from a lane to a face, so it had to sum.
**A screen can show the relationship spatially.** Each unit's power and each enemy's intent are
already in their lane, so the arithmetic is visible rather than absent.

Revisit only if play shows people miscounting — and if it does, the fix is making the per-lane
numbers read better, not adding a total back.

## The mockup, read back — 2026-09-17

**The 2026-09-14 mockups were lost.** Nothing was committed and the session that made them recorded
only prose, so the second one had to be supplied again by hand. **Commit the image this time** —
`docs/mockups/` — because the description below is not a substitute for it and this has now cost two
sessions.

What the mockup settles, and what was deliberately overruled:

| | Mockup | Built |
|---|---|---|
| Empty lane | dashed outline, no fill | **adopted** — `DoomArt.DashedSlot` |
| Held lane | solid fill, bone border | already so |
| Stats | bone SWORD + bare number left, RED DISC right | **adopted**, on cards and lanes alike |
| Cost | disc on the card's top-left, over the art | **adopted** — it also freed the name row |
| Card text | none at all — icon and three numbers | **OVERRULED**, see below |
| Backdrop | full-bleed drowned city and water | not yet built |
| Opponent | red-OUTLINED hood over a centred pill bar | not yet built |
| Banner | full-width bar, doom dial top-right | partly — no dial |

**The card keeps its name and its rules text.** The mockup's card carries neither, and that is the
one place it was judged wrong rather than aspirational: a nameless card cannot be talked about, a
Rite has no stat badge and so would be unidentifiable in hand, and the game already authors a
`Description` for every card. The mockup's LOOK is adopted in full; its information budget is not.

Card art is the project's own SVGs; UI glyphs come from game-icons.net under CC BY 3.0 — attribution
in `SQGodotCommon/DoomGame/Art/CREDITS.md`, **which must ship**.

## The visual language — written 2026-09-17

**This section is the design system. Everything above it decides what is on screen; this decides what
it looks like and how it behaves.** A screen that disagrees with this section is wrong even if it
looks fine in isolation — consistency is the whole point, and the failure mode is five screens that
were each reasonable on their own.

### The readability budget, and the number the whole thing hangs off

**Three scales stack between an authored font size and a real pixel**, and the third was missed for a
whole session:

```
0.7    card_2d_canvasgroup.tscn sets scale on the card scene ITSELF
0.68   DoomHandView.CardScale, the fan shrunk into one band
0.833  the 1920x1080 design canvas letterboxed into the 1600x900 window
-----
0.397  what an authored size is actually worth
```

So **author N / 0.397 to get N real pixels.** Rules text at 26pt rendered at 10.3px, which is why
nobody could read a card. This is not a card-only trap: any node under a scaled parent pays the same
tax, and the only reliable check is a screenshot.

> **Every size in this game is verified by looking at a capture, never by arithmetic alone.**
> The arithmetic was done last session, carefully, and was wrong because it was missing a term it had
> no way to know about. A number that has not been seen on a screen is a guess.

**Minimum real sizes.** Below these, the element is a bug:

| Role | Real px | Notes |
|---|---|---|
| Rules text, the smallest thing a player must read | **16** | at 1600x900. Never go under this to fit more words — cut the words |
| Card name | 20 | it is scanned, not read; weight matters more than size |
| A number that changes the play (cost, power, toughness, intent, life) | 22 | these are read across the board, at speed |
| The doom countdown | 34 | the single most important number on screen |
| Screen and scenario titles | 44+ | |

**When text does not fit, the text is wrong, not the box.** Shrinking to fit is forbidden. A card
whose rules text cannot be said in the space gets a keyword instead — see *Hover and explanation*.

### Type

One family, three weights, and no exceptions. A jam with two typefaces reads as a jam.

- **Numbers are tabular, never proportional.** `12/16` and `2/10` must occupy the same width, or lane
  pips jitter every time a unit takes damage.
- **A badge sizes to its longest possible value, not its typical one.** The P/T badge was built for
  `2/2` and clipped `2/10` into `2/1` — a *wrong number* on screen, which is worse than an ugly one.
  Every numeric container is sized against the largest value its content can produce.
- Card names and screen titles are caps via `DoomPalette.Caps`; rules text is sentence case.

### Colour, past the reserved two

The reserved colours are in the table near the top of this file and they do not move: **gold is yours
and precious, red is the enemy and life.** Everything below is the rest of the system.

| Token | Hex | Job |
|---|---|---|
| Navy | `#16212E` | the ground everything sits on |
| Slate | `#233444` | a surface raised off the ground — a panel, a card back, a filled lane |
| EmptySlot | `#1B2836` | a hole. Darker than the ground, so a held lane reads as the exception |
| Bone | `#E8EEF2` | all text, and the one bright mark on a silhouette |
| Gold | `#E3B23C` | **reserved** — the Companion, your energy |
| Red | `#C73E3A` | **reserved** — the Opponent, enemies, life, and any doom that rewrites your deck |

**Colour is never the only carrier of a fact.** Red alone says "enemy", but a colourblind player gets
nothing from it — so every red thing also has a position (the enemy row) or a glyph. This is not
optional polish; it is the difference between a game that can be played and one that cannot.

**Card colour must MEAN something.** `DoomArt.ColourFor` currently hashes the card's name into one of
five browns and teals, which spends the strongest signal a card has on noise — two Scavengers being
the same colour is not information, it is a coincidence that looks like information. Card colour
carries exactly one of: cost band, or act of origin. Pick one and hold it everywhere.

### Art

Flat vector SVG, authored to the palette, one file per subject in `SQGodotCommon/DoomGame/Art/`.
Godot 4 imports SVG natively, so there is no build step and no atlas.

- **256x256 viewBox, subject filling roughly 85% centred.** The same file is the card art and, tinted
  down, the lane figure — a card and the body it becomes must be legibly the same thing.
- **Three tones and no more:** a near-black silhouette `#0C131B`, one mid tone for interior form
  (`#233444` or `#2B4054`), and at most one accent — red for hostile, gold for yours.
- **Build creatures from named parts and `<use>` them**, never from one hand-written polygon. The
  first Feral Pack was a single 20-point path and rendered as a blob; the same subject built as
  body / haunch / four legs / wedge head / ears read instantly. Mechanical subjects survive a single
  polygon. Organic ones do not.
- **It must read at 40px.** That is the lane-figure size. Check it there before checking it on a card
  — a shape that survives 40px always survives 256px, and the reverse is not true.

### Motion

**Nothing on this board may change without saying so.** A card game where the numbers simply differ
after you click is a game where the player reconstructs what happened instead of playing it.

`Render()` already receives the full `ImmutableList<GameEvent>` for the step, and the layout contract
above already names the event that animates each region. So motion is one queue that plays those
events in order — not a set of ad-hoc tweens bolted onto the renderer.

**Every duration is a multiple of one config value.** A single `Speed` multiplier scales the whole
queue and is exposed as a setting, because pacing is the thing most likely to want tuning late and
the thing a player is most likely to want faster on a second run.

| Beat | At Speed 1 |
|---|---|
| A card leaves the hand and lands in a lane | 0.25s |
| A number changes (damage, life, energy) | 0.20s, with the delta shown |
| A unit or enemy dies | 0.30s |
| A lane attacks | 0.20s out, 0.15s back |
| A card is drawn | 0.18s, staggered 0.06s each |
| The doom fires | 0.9s — it gets the long one, because it reshapes the run |

- **`Speed = 0` must be exact, not fast.** An animation queue that merely runs quickly is a flake
  generator — a 0.01s tween still takes frames. Zero means every tween is skipped and the final
  state applied in one frame.
- **Zero does not stop the hand.** `Hand2D` tweens the fan on hard-coded durations and is shared
  with the MTG card scenes, which this project may not touch. Verified by capture: at zero the
  floats, pops, flashes and screen shake stop dead and the cards still slide in over ~0.2s. Say that
  accurately rather than claiming a still board.
- **The queue is skippable.** A click during a sequence finishes it immediately and shows the end
  state. Never make a player wait through an animation they have already understood.
- **An animation never decides anything.** It plays what the engine already resolved. If a tween can
  change what is true, the rule that the UI reads facts rather than computing them is already broken.

### Hover and explanation

The rule is **two layers, and a card never carries the second one**.

1. **On the card: what it does, in as few words as the effect can be said in.** `"6 to every enemy"`.
   If it will not fit at 16px, it becomes a keyword.
2. **On hover: everything else.** The card enlarges to full size, and any keyword or symbol on it is
   expanded in the side panel beside it.

- **A keyword is data, never a delegate.** Card text today is an authored string on `DoomEffect.Text`
  and `RunCard.Description`. A keyword table lives in `DoomCore` as data, so the console and the
  board read the same definition — and the serialization rule in the root `CLAUDE.md` applies with no
  exceptions.
- **Hover is never the only way to learn something a player must know to make the current decision.**
  It is for depth and reminders. If a fact is needed to choose a lane this turn, it is on the board.
- **Everything hoverable looks hoverable**, and the cursor changes. A tooltip nobody discovers is
  worse than no tooltip, because it was paid for.
- The enlarged card and its panel are **one hover target between them** — moving the mouse from the
  card onto the panel must not dismiss it.

### Small windows

The game must be readable in a half-screen window; jam judges do not play fullscreen.

- `canvas_items` stretch stays. It is doing the right thing; the bug is that the board is laid out to
  crowd left and leave a third of the frame empty.
- **Below 1280 wide, the hand fan tightens and the lane cells lose their flavour line, in that
  order.** Nothing carrying a number is ever dropped — a cell shrinks, it does not shed facts.
- **The 16px floor is measured at the smallest supported size, not the design size.** A budget that
  only holds at 1600x900 is not a budget.

### The glossary

`DoomCore/Content/KeywordLibrary.cs` holds every term the game expects a player to know, as DATA —
a name, a reminder sentence, and the phrases that count as mentioning it. It is in the engine and
not the front end so the console and the board cannot explain a word differently.

**Matching is whole-word, and that rule has a test.** A substring match reports Rite for "favourite"
and Power for "powerful"; reminder text that appears for no reason teaches a player to stop reading
the panel, which costs more than the missing keyword would have.

The hover panel shows, in order: the card's name, a stat line, **its full rules text**, then any
keywords in it. The full text is the point — the card face clips to what fits at the 16px floor, so
hover is where a card too complicated for its own face is still readable.

### How this section is enforced

Change a screen, capture it, look at it:

```
godot-mono --path SQGodotCommon --position 1920,0 --resolution 1600x900 \
  --write-movie shots/doom.png --fixed-fps 10 --quit-after 40 \
  DoomGame/doom_board.tscn -- --autostart
```

`--position 1920,0` keeps the window off the developer's other monitor. `-- --autostart` skips the
theme picker, without which every capture is a picture of the menu. Take a LATE frame; container
layout and card tweens have not settled on frame 0.

## Not decided

- Where the discard and draw piles are shown, or whether they are. The console does not show them
  and has not missed them.
- Whether the 3D scenario backdrop (PERSPECTIVE SHIFT) ships. It is flagged "if time survives" in
  `DoomJam.md`, and it is now the only optional sub-theme left — GO SPINNY is dropped. The flat
  style makes a per-apocalypse backdrop cheap, three geometric layers, so it may survive after all.
- ~~Card art.~~ **Decided 2026-09-17:** hand-authored flat SVG in `SQGodotCommon/DoomGame/Art/`,
  one file per card name, falling back to the generated silhouette where nobody has drawn one yet.
  Eight exist. Mechanical subjects take about five minutes, organic ones about twelve — budget by
  subject, not by card count.
- Whether `Common/Cards/2D/` (`CardUI2D` + `Hand2D`, ~1600 lines of drag/hover/fan with no MTG in
  it) is reused as-is or trimmed. It is free and it is the reason the hand band is not a build task.
