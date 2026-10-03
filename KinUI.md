# DOOMJAM — UI design

**Read `KinJam.md` first.** This doc is downstream of it: the game design decides what is true, this
decides how it is shown. Where they disagree, `KinJam.md` wins.

Status: **BUILT, and given a full visual pass on 2026-09-17.** Board, hand, drag-to-lane, reward
screen, intermission, animation, hover glossary, painted backdrop, and art for every card, enemy and
Opponent in the game. The layout contract below is what it implements; the visual language section
further down is what it looks like, and a screen that disagrees with that section is wrong.

## THE DECLUTTER PASS — symbols, not sentences (interview 2026-09-30; BUILT 2026-10-01)

> **The default for every screen from here (Shayne, 2026-10-01): when unsure, START WITH SYMBOLS.**
> Not a strict rule — not everything will have a symbol — "but it should not be assumed that we
> should want text … I would rather begin with symbols if unsure, than have the UI be cluttered with
> a bunch of text everywhere." So: a symbol and a number first; the words in a hover (every symbol
> must explain itself — its meaning written once, in `KinSymbols`); a line of text only where it
> carries what no symbol or hover does (a boss's name, gold earned). The Godot traps this pass hit
> are in `.claude/rules/kin-frontend.md`; the capture traps in `Commands.md`.

Shayne: "Everything is very verbose, there is text everywhere. Most games use symbols to portray
certain values … there is still a bunch of fluff text everywhere that overexplains everything." The
audit found it on every screen (the list is in this section's history: the battle's subtitle sentence
and always-on hint line, two text lines under every creature, word badges, a combat log, "played",
"ENERGY", card sentences, and subtitles on every run screen — some of them stale: "monsters you can
catch", "Wants to be HIT").

| | Decided |
|---|---|
| Icons | **game-icons.net** (CC BY — the set our 5 already come from; every new one goes in `CREDITS.md`) |
| A creature | **STS style**: Block as a shield + number on the left of the HP bar (Rooted a different tint); under the bar ONE row of icon + number chips (Power, Spell Power, Thorns, Burn); the passive an icon that explains itself on hover. **No family word** — on yours or the foes' |
| Teaching | **Hover, and a ? button** — no always-on hint line, no instructional subtitles; a ? opens a one-screen how-to. A subtitle stays only where it carries information (the boss's name) |
| Card text | **WORDS — reversed 2026-10-01** (below). Short sentences, STS/MTG style: "Gain 8 Block.", "Attack 5."; the lit drop places say where it goes, so no "Drop on a foe:" |
| Combat log | **Cut** — the floats show every hit; `party-sim trace` keeps the record |
| Foe intents | **Icon + number + an aim glyph** (front, back, front two, all, weakest); CRUSH a cracked shield; the aim explained on hover |
| Rewards, shop, spring | **The real card faces**, as in the hand — one look everywhere; price or SOLD under it |
| Title box | **One short line**: "Turn 2 · Wild Bog Toad" (or the boss) — knocked-out monsters greyed on the field |

Also: "3/3", not "3/3 ENERGY"; Spell Power as a chip; floats as icon + number; the debug "played"
goes; first-attack badges as icon + number.

**As built** (each step seen on screen):
- **Icons**: 11 more from game-icons.net in `Art/icons/` (power, spell_power, thorns, burn, grow,
  crush, spell, passive, aura, draw, energy), bone silhouettes tinted in code; `KinArt.*Icon`.
- **A creature** (`KinRelayCreature`): a shield + number on the HP bar's left end (green = some
  Rooted); one row of `Chip`s (icon + number) under the forecast, from a fixed pool of slots.
- **Badges**: a foe's intent is [sword or CRUSH's cracked shield] [number] [one dot a place in your
  line, back to front, filled where the ENGINE aims it — `IntentTargets`, the forecast's account, so
  "weakest" shows exactly whom]. Our badge is [sword] [the bonus's symbol] [number]; spent, it hides.
- **Battle chrome**: title "TURN n · NAME"; no subtitle, hint line or combat log; "3/3" alone;
  Spell Power a swirl + number; auras a mark + name; floats an icon + number (`KinAnimator.Float`'s
  `icon`); the hint spot now carries only refusals.
- **Cards — icons tried, then REVERSED (Shayne, 2026-10-01)**: a basic card read as symbols
  (`KinCardIcons`, deleted). Even Shayne read Kindle's "[swirl] +3 [cards] 2" as *Burn 3* — it
  is Spell Power. **Symbols are for STATUS you glance at (chips, intents, the board); a CARD is a
  CHOICE you read, so it says it in words**, as STS and MTG do. Attacks say "Attack 5." (front is
  the default; any other aim is said: "Attack all foes for 2."); Power is never written — **dragged
  over one of your monsters, the card shows that monster's real total**, green
  (`PartyState.AttackPreview`, `KinCardFace.ShowAttack`). Green on a spell only when a number on it
  actually grew.
- **The run's screens (2026-10-03)**: CHOOSE YOUR FAMILY (a tile each, its first monster and a line);
  YOUR TEAM (the rolled three, REROLL once, BEGIN); EVOLVE ONE (each evolvable monster as its form,
  "FROM X: +HP, +POWER" on a taller tile). On the field an evolved monster's name wears a ★ — not a
  gold rim, since gold means a drop target — and a long name steps its size down. An evolved form
  draws its base form's art until it has its own (`KinArt.Drawing`).
- **Drop targets are a HIGHLIGHT, not words (2026-10-02)**: a place a held card can land on turns its
  ground and name gold; an empty place shows the gold ground mark alone. No "▲ STRIKE HERE", and no
  card name floating off what it was played on — a pop says it landed.
- **Run screens**: real card faces (`CardButton` — the hand's face drawn once into a `SubViewport`
  and shown as a picture, so it takes no input; filled on the card's `Ready`, not before);
  starter/monster tiles as a passive star, a first-attack row and heart/Power/Spell Power symbols;
  every instructional subtitle gone; "TOWN: BOSS: …" and "+20 GOLD" kept.
- **?** beside MENU: three how-to lines and every symbol's meaning; Esc or a click closes it.
- **EVERY SYMBOL EXPLAINS ITSELF ON HOVER** (Shayne, 2026-10-01: "I should be able to at least
  hover over the icons and see what it means"). The words live ONCE, in `KinSymbols` — the ? legend,
  the tips and a card's panel all read it. On the field the creature view catches no mouse (card
  hover is physics picking), so the board hit-tests: `KinRelayCreature.TipAt` → a tip at the cursor
  (chips, the Block shield, the badge); a hovered CARD gets a panel with its rules and each symbol
  it uses (`KinSymbols.Of`, from its steps). On the run screens the symbols are Controls with
  `MouseFilter.Pass` and a native tooltip, themed by `KinSymbols.TooltipTheme`; a card button's
  tooltip is the same explanation in plain text. Capture: `--mouse=x,y`, `--hover-card=N`.
- **Not done**: knocked-out monsters greyed on the field (they still leave the line).

**Build order, as planned** (each step seen on screen before the next): icons in → the creature (bar, Block,
chips) → the battle chrome (title, hint, log, "played", energy) → intents and first-attack badges →
the run screens (subtitles, real cards, stale text) → card text with inline icons (the riskiest:
the shared card's rules box is a `Label`, so icons need a `RichTextLabel` added to the instance —
never edit MTG's scene) → the ? how-to.

## The rule this doc exists to enforce

> **The UI never computes a game fact. It reads one.**

Every number on screen comes from `KinStateExtensions` or `KinPreviewer`, never from arithmetic in
a presentation script. This is the same rule that stops `KinPreviewer` writing a second account of
each scenario, and it is the rule `MtgCardMapper` broke at 2084 lines. A UI that computes "how much
damage will I take" is a second rules engine that will drift from the first.

`KinConsole/Renderer.cs` is the reference implementation of this contract and **the more trustworthy
spec of the two** — it is the information set that survived real play, and it is where the
`OpponentDamagedEvent` blind spot was caught. Build against it, not against the mockup.

## Style — flat vector, chosen 2026-09-14

> **REPLACED (2026-09-26) by style D, fine line art:** `KinVisualDesign.md`. The layout contract
> and the readability rules in this file still stand; the style below does not.

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

## ~~The first screen — theme select~~ — DELETED 2026-09-18

> **The game opens on floor 1. There is no first screen.**
>
> Chaining the three acts into one run deleted the only thing this screen chose: a run is every act,
> in the fixed order in `ActMap.Order`, and `Run.Theme` is derived from the floor you stand on. The
> screen went on showing three columns and taking a pick that was ignored, which is worse than not
> having it — **a screen that appears to choose something and does not is a lie to the player.**
>
> **The SEED moved to the status strip** (`FLOOR n   ACT n   SEED nnnn`) and did not go with it. A
> run you cannot name is a run you cannot report a bug about, and that was the one thing here the
> game still needed.
>
> `ThemeLibrary.BandsOf` now has no caller but its test. It is kept because the invariant it asserts
> — every floor falls in exactly one band that names its doom — is about the CONTENT rather than the
> screen, and because the next run-start screen will want it: **if one returns it should pick the
> COMPANION**, which is the only thing that now declares what a deck is going to be.
>
> Everything below described that screen and is kept for the reasoning only.

## The first screen — theme select, built 2026-09-16

**Three columns, one per act, each showing its whole schedule.** The doom is a schedule rather than
a roll, so there is nothing to hide: floor bands, the apocalypse each holds, and its one-line
flavour, all before a card is drawn. This is the picker, the difficulty preview and the pitch at
once — `KinJam.md` has wanted it since the schedule existed.

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
| 1 | Act name + floor | `ThemeLibrary.Of(Run.Act).Name`, `Run.Floor` | — |
| 2 | Opponent silhouette + health bar | `GetOpponent().Health` / `.MaxHealth` | `OpponentDamagedEvent`, `OpponentDefeatedEvent` |
| 3 | Enemy lane slots x5 | `EnemyInLane(n)` — name, `Health`, `Intent`, `IntentAmount` × `Strikes`, and `KinRulesText.Traits` | `EnemySummonedEvent`, `EnemyDiedEvent` |
| 3 | Summon telegraph symbol | `GetOpponent().NextSummon` — its `Lane` | `EnemyTelegraphedEvent` |
| 4 | Your lane slots x5 | `UnitInLane(n)` — name, `Power` × `Strikes`, `RemainingToughness`, `KinRulesText.Traits` | `CardPlayedEvent`, `UnitDiedEvent` |
| 4 | Companion ring | `HasComponent<CompanionComponent>()` | — |
| 5 | Status strip | `Run.Floor`, `Player.Life/MaxLife`, `Player.Energy/MaxEnergy`, `Battle.TurnNumber` | `PlayerDamagedEvent`, `PlayerDiedEvent` |
| 5 | Hand | `CardsIn(ZoneType.Hand)` — `Cost`, `Name`, `UnitComponent`, `Tags` | `CardPlayedEvent` |

### The lane slot is SQUARE, and the art fills it  (2026-09-21)

**175 x 175, and the drawing goes edge to edge with the name and the pips on scrims over it.** It
was 296 x 156 — a landscape strip holding a transparent silhouette on a coloured plinth.

**Why it changed:** generated art is opaque and square (1024 x 1024). Dropped into the old strip it
read as a postage stamp stuck on a tile, because the plinth showed all round an image that had its
own background. A square slot takes a square image edge to edge — **no crop, no letterbox and no
transparency pass**, which deletes the whole background-matting problem rather than solving it.

**Card ratio was tried first and does not fit.** At 0.70 portrait and the old width it measured
**1573px of the 1080 canvas** — 493 over, because there are TWO lane rows. Square costs far less
height for the same "art fills the slot" result. `WarnIfColumnOverflows` is what measured it; use it
before changing any band height.

**The two art cases are framed DIFFERENTLY and both must be checked.** A real drawing is opaque and
is `KeepAspectCovered`. A fallback silhouette is transparent, so covering with it would crop a
mostly-empty shape into a meaningless corner — it stays `KeepAspectCentered` over the lane's ground
colour. **The pool grows faster than the drawings do, so the fallback is the common case**; a change
that only looks right on one of the two is the failure to expect.

The ground colour lives on its own `ColorRect` under the figure, **not** on `Root`'s stylebox:
`Style()` runs after `Stand()` and writes that stylebox for the border, so a ground painted there is
silently overwritten every time.

Known rough edges, deliberately not fixed yet: the bottom stat scrim clips a standing subject's
feet; the floating damage number sits half outside the smaller cell; enemy silhouettes are dark on a
dark ground.

**The TRAIT strip (2026-09-22): FLIER, THORNS 6, STRIKES TWICE — one per line, gold, under the
name.** Enemies had NO text on the board before it, so a Flier and a Wretch were indistinguishable
and countering either was luck. It shows only the keyword FIELDS (`KinRulesText.Traits`), never
effect sentences — a lane has room for a word. **Still invisible, and known:** effect-text enemies
("on death: the Opponent heals 6"), and the Opponent's own trait (a Corrosive Opponent shaves 3 off
every unit at turn start with nothing on screen saying why). Both want an enemy hover in the
inspector, which follows only the hand today.

**THE COMPANION ON THE BOARD (2026-09-22, two playtests in one day).** The first build changed the
rules and nothing on screen ("it felt exactly the same"). What stuck:

- **Guard is a shield icon and a number on the companion's cell**, mirroring the sword and attack. It
  was the red toughness disc (read as toughness), then the word GUARD, which the 175px cell clipped
  to "GUARD 1" — a wrong number. Guard soaked floats bone from the shield (`GuardSoakedEvent`).
- **A hit that reaches your life flashes the companion** and floats red from the life pill.
- **Life stays in the status strip, with a red `−N` beside it: what ending the turn now would cost**,
  asked of `LifeLostIfTurnEndsNow` (a real end of turn on a copy), never summed from intents here.
  Just the number — "incoming" is the glossary word for a telegraphed summon.

**Tried and reverted: life as a health bar ON the companion's cell.** The playtest said it made life
harder to read — squeezed into 175px it competed with the art. Keep life where it is easy to find.

**Click an empty lane in your row and the companion moves there**, once a turn — read by `KinBoard`'s
`_UnhandledInput` and hit-tested with `LaneAt`, exactly like a card drop. **NOTHING on the board may
catch the mouse**, and that rule was learned twice in one day:

1. `MakeTransparentToMouse` turns the whole board off to the mouse, and the move first shipped behind
   it: the cells were wired and could never fire ("I couldn't move my companion").
2. The fix set the lane cells to `Stop` — and **card hover is physics picking, which any Control
   catching the mouse blocks.** The reward screen's cards sit over the lane row, so the top half of
   every reward card stopped hovering ("hovering is inconsistent").

**Verify input with `--click-lane` (a real click), and a hover problem with `--catchers-at`** (lists
every Control catching the mouse at a point). A synthetic mouse move does NOT drive physics picking,
so a hover can only be checked through its cause. `--move` skips the click and proved nothing.

A double-striker's attack reads **`6×2`**, not 6: the pip is read at a glance and 6 is the wrong
number for a body that lands 12. Both facts are shown; nothing is multiplied in the UI.

**Input is one verb.** Drag a card onto a lane slot, or click card then lane. There is no targeting
anywhere in this game and no attack-or-block step — the lane IS the decision. Do not build a
targeting system; there is nothing to target.

A play must go through `PlayCardAction.ValidateAdd` and show the engine's own refusal text. The
console prints `can't: Lane 2 is already held by Ash`; the UI must not silently drop the drag. **A
click that does nothing is the worst bug a card game front end can have** — that is how the `c`
command shipped unwired for two sessions.

## THE RELAY screen — `kin_party.tscn` (2026-09-25) — SUPERSEDES the cells below

**Two lines of creatures facing each other, the FRONTS meeting in the middle** (`KinRelayPlan.md`,
Phase 4). The rows of cells and everything below about columns, steps, trainer and leader health
are history. The hand band, the choice panel and the inspector stay.

**The frame (style D, 2026-09-26)**: the full-width banner and status strip are GONE. Top-left, a
region PLATE as wide as its words (title, and the subtitle that names the knocked-out and the
bench); top-right, the practice picker and MENU as kit buttons. Bottom-left, the energy ORB
("3/3", with ENERGY or "−1 NEXT TURN" under it) and SNARE ×N below it; bottom-right, a big
framed END TURN. The hint — the engine's refusal
of a play — is outlined words above the hand. The kit is `Plate`/`StyleButton` in `KinPartyBoard`:
navy at 0.92, a thin gold border (bone for information), gold text on hover.

**The field** (`KinRelayField`, 1856 x 400 on the 1920 x 1080 canvas) replaces the two rows:

```
 ┌──────────── YOUR LINE ─────────────┐        ┌──────────── THEIR LINE ────────────┐
 │  4      3      2      1    [0]     │  gap   │ [0]    1      2      3      4      │
 │ back                     FRONT ──▶ │  96px  │ ◀── FRONT                   back   │
 └──── 5 slots x 176px, 880px ────────┘        └──── 5 slots x 176px, 880px ────────┘
```

- **A creature is a VIEW keyed by its id, not a slot.** It stands at its line position and SLIDES
  to a new one when the line changes (a faint, a swap, a token in front, deploy). The slide waits
  until the turn's events have played, so a blow's number rises off the place it landed.
- **The view, top to bottom** (`KinRelayCreature`, 176 wide — rebuilt to the style-D mockup
  2026-09-26, `KinVisualDesign.md`): a BADGE ROW — a dark pill with an icon and the move in short
  ("6 → front", `KinMoveText.Short`; the move's NAME lives in the inspector) bordered gold yours /
  red theirs, and the STEP disc on its corner; the creature — a transparent STANDING sprite
  (`Art/sprites/<name>.png`, feet on the ground line, mirrored for a foe) on a shadow that turns
  GOLD when it is a legal drop, or, until its sprite exists, the old portrait MEDALLION with its
  ring; the NAME; the HP bar; the NOTE (the forecast, "−18" in lifted red, or the drop hint in
  gold); one STATUS line. **Every label is outlined through its own `LabelSettings`** — the view
  stands on a painted backdrop now, and the theme's `outline_size` override drew nothing.
- **Families (2026-09-27, `KinFamiliesPlan.md`)**: the status line leads with the FAMILY ("GROVE ·
  NURSERY"), then Block (with "(n ROOTED)") and GROW; the LEVEL rides in the HP bar ("LV5 · 24/24").
  A card shows its family on the type line under the art. EMBER's KINDLE is orange text over the
  energy orb, hidden at 0.
- **The STEP badge is the step, not a queue place.** `ActingSteps` groups by position, both sides
  at once, so a pair at the same depth shares its number (Gale and Wisp 1, Pike and Stonebeak 2,
  Bramble and Boar 3). The flat 1–6 of `ActingOrder` made simultaneous blows look sequential.
- **The stage**: `Art/backdrops/greenwood.png` behind everything, lightly dimmed and RAISED 240px
  (`StageLift`) so its meadow is under the lines' feet. ComfyUI-made; one backdrop for every battle
  until there are regions' worth.
- **Places widen to fit the longest line** (290px at three a side, 176px at five) and the view
  scales with them up to 1.3×, on an inner node (`KinAnimator.Pop` owns the root's scale).
- **The card (style D, `KinCardKit`)**: dark body edged in the OWNER's colour (steel for a trainer
  card), name in caps on a darker plate, a blue diamond cost gem, the ACTION illustration
  (`Art/cards/<name>.png`, cover-cropped) — never the owner's portrait — a parchment text box in
  dark ink (the shared label's outline AND shadow zeroed), and the owner's medallion at the foot.
- **A fallen creature fades where it fell** after the blows have played, then the line closes.
- **Drops are slots, not creatures**: 0–4 your line, 5–9 theirs, so a Gust can be dropped anywhere
  on theirs. The engine lights the legal ones; the field only draws. **A lit place must be a real
  choice**: Summon first lit all five of your places though its token always arrives at the front,
  so the engine now takes a summon only on your front.
- **No deploy (cut 2026-10-02 — never used in play).** Monsters are not dragged in a fight; the
  team's order is set in TOWN, at the hospital: press a monster to send it to the front.
- **Motion** (`KinAnimator`, one Speed dial): a blow LUNGES its attacker toward the other line
  (0.12s out, 0.12s back), the target flashes and its number rises; the line slides (0.3s).
- Sizes: move and HP 22, name 22, status and note 20 — authored on the 1920 canvas (x 0.833 on the
  1600 window), so every line is at or over the 16px floor. Check them on a capture.

## THE TOWN MAP — `KinTownMap` (2026-09-26, `KinMapPlan.md`)

A village green (`backdrops/town_ground.png`) with each building's cut-out sprite
(`buildings/<kind>.png`) placed from `PartyRun.Town` data, on a soft contact shadow, a name plate
under it (gold for the leader's hall while the leader stands, red "SHUT ·" on a closed gate), and
a tooltip. The lead monster waits by the well and walks to the door you click; the building's
screen opens (`ShowBuilding`) with BACK TO TOWN. A shut gate does not open — the note says why.
Known: the generated green has houses of its own painted at the edges, which are not buildings.

## THE ROUTE MAP — `KinRouteMap` (2026-09-26, `KinMapPlan.md`)

A route's places drawn from their DATA on a painted map (`backdrops/route.png`): `Row` bottom (the
town you left) to top (the next town), `X` across (left of centre is the region's first area, right
its second). Each place is a disc (`ui/node_gold|red|bone`): **gold = you can walk there** (the
battle's legal-drop language), red = a fight, bone = anything else. Inside: a visible fight's lead
species, the rare's creature, "?" for tall grass, the trainer's sword, the spring's heart, a find's
Snare or "$". A caption under each, a tooltip on hover. Dashed paths, each over a dark casing so it
reads on forest, meadow and water; gold from where you stand. The lead monster is the token and
walks (0.4s) before the run moves. Places behind you fade. Top-left the region, top-right the purse,
bottom-left the team, bottom-centre what you just found.

## THE COMPANION GAME screen — `kin_party.tscn` (2026-09-23) — the lanes; history

> **AUTO-BATTLE v1 (2026-09-24) changed what the cells say; the rules below about owners are
> history.** Trainer cards are drawn plain and nothing lights "PLAYS". **MONSTER DECKS (same day)
> brought owner colour back**: a card from a monster's deck is painted in its colour, carries its
> portrait, and names it on the type line, because it leaves when that monster faints. Now:
> - **Every creature's name band carries its ORDER BADGE** ("1 · PIKE") — when it acts at the end
>   of the turn, from `PartyState.ActingOrder`. A monster that has acted (Hasten) drops it.
> - **A monster's second foot line is its NEXT MOVE, with the damage it will really deal** ("▲ JAB 5"
>   includes Power, Rally and Momentum). "NEXT:" was cut: "BUFFET 3 (3 WIDE)" wrapped to a fifth line.
> - **Foes show the forecast too** ("▲ −5 HP if turn ends") — your monsters' attacks are as
>   telegraphed as theirs.
> - **A hovered card lights every space on BOTH rows the engine accepts**: a monster ("▲ GUARD
>   HERE"), a foe (Stagger), or an empty foe space (Gust: "GUST a foe in here"). The hand passes
>   one int, so the board folds the row into it: 0–4 yours, 5–9 the foe's.
> - **Hover any creature for the INSPECTOR** (`KinPartyInspector`): stats, when it acts this turn,
>   its passive's rule, statuses, and its whole cycle in words with the next move marked. Its
>   labels wrap at a FIXED width and it re-fits every frame: measured at once, every word took a
>   line and it ran the height of the screen. Hidden while a card is hovered or dragged.
> - **Your health sits first in the status bar** ("YOU 30/30"), red with "▼9" when ending the turn
>   would cost you. A foe attack that will land on no monster says "→ YOU"; in a gym, a monster's
>   swing that will find no foe says "→ LEADER", and the line between the rows carries the
>   leader's health. The hint strip WRAPS: unwrapped it pushed END TURN off screen.
> - **A choice mid-card** ("discard a card") opens MTG's `ChoicePanel` over a dimmed board; the
>   board waits until it is confirmed (`KinPartyBoard.Settle`). KIN styles its panel (Navy, Slate
>   border): unstyled, the options floated over the board.
> - **A hand card's cost badge is what it costs NOW** (`PartyState.CostOf`), so Scrap Hammer
>   drops as you discard. A thief's steal floats red ("STOLE GUARD") and its move says "+ steals".
>   A foe's wild trait is the inspector's first status line.
> - **Borrowed energy shows on the label** — "ENERGY 4/3  −1 NEXT TURN" — a cost paid later must
>   be visible now. An X card's badge is your current energy.
> - **A token's passive line is "TOKEN · FADES IN N"**, in a pale shared colour. Known: a token
>   that will FADE (not die) still shows "−N HP if turn ends" — the forecast plays the next turn's
>   start. The line above it says why.
> - **The practice scenarios are one dropdown** (`OptionButton`): at seven, a row of buttons made
>   the banner wider than the screen and pushed END TURN off it (scar 6 again — unwrapped text
>   sets its container's width).
> - **The step**: click a monster, and the spaces beside it light — "STEP HERE", or "▲ SWAP HERE" on
>   an ally.

The slice's battle screen, `KinPartyBoard` + `KinPartyCell`. Foes on the top row, your companions on
the bottom, a combined hand. Built from the first playtest: **"every card looked the same" and "I
could not see what a card did."**

- **SUPERSEDED 2026-09-28 — a monster's colour is its FAMILY's** (`KinPalette.Family`: Grove green,
  Ember orange, Storm blue, Mire violet): name pills, card edges, team buttons, reward and shop
  tiles. The companion colour below is now only the fallback for a family-less monster, and the
  medallion on a card still says whose deck it is from. Playtest: kin were unreadable.
- **Each companion has an identity colour** (`KinPalette.Companion`), and it is on EVERYTHING of
  theirs: the whole card face, the card's art window, the cell they stand in, their figure. Never
  gold or red. The owner's name is also on the card's type line, but colour is what reads at a
  distance.
- **A card's art is its OWNER's**, so real monster art reaches every card at once.
- **Hover or drag a card and its owner lights gold with "▲ PLAYS <CARD>"**; a card that moves its
  owner also lights the spaces it can be dropped on ("DROP HERE").
- **Every event is told, one beat at a time**: the card's name rises gold off the companion that
  played it, then each hit (`−N`, or BLOCKED) and each `+N BLOCK` off the thing it happened to,
  staggered so the foes' turn reads in the order they acted.
- **Rules text gets two lines.** Root Wall ran to three and silently lost "Block." at the bottom
  of the box. **The cause was a size mismatch**: the shared `FitRulesTextToBox` assumes a 112px box
  and `KinCardFace` had cut it to 104, so text that "fit" was clipped. The box is 112 now — and the
  fitter also measures WITHOUT line spacing, so Flank still lost "lone foe." at 112. **A card with no
  stat row (every companion card) takes the stat row's room** (`ApplyStats`). Keep text short anyway.
- **A push or swap lights the FOE row** ("DROP HERE" / "▼ SLAM HERE"), a step lights yours — only
  the spaces `PlayPartyCardAction` accepts. A drop on either row names that column.
- **The monster IS the cell** (`KinPartyCell`): art covers the whole space, the name on a dark band
  at the top, the numbers on a band at the foot in the companion's colour, darkened. **Four lines at
  most over the art** — six covered Pike to the ears; Speed is not printed (the move line shows its
  effect). The foot sits in a full-rect VBox: anchored to the bottom and grown upward it grew DOWN
  and the clip ate the forecast.
- **Six generated portraits** (`Art/bramble|pike|gale|boar|wisp|stonebeak.png`, DreamShaper XL Turbo,
  flat style) — a companion's portrait is also the art on every one of its cards. Local, so no
  `CREDITS.md` entry. Stonebeak needed a stone-FIRST prompt: "a heavy grey bird with a stone beak"
  gave three plain grey birds.
- **The run's screens** (`KinPartyRunScreens`) are an overlay of Buttons over the board, and the
  board HIDES THE HAND while one shows (the fan's ZIndex draws over any overlay). Every tile line
  wraps, the title too — "WHIRLING STRIKE (2)" unwrapped dragged its column past the tile edge.
- **A companion's passive is its own line** on the cell, live ("THORNS 5 this turn"); clicking the
  companion states the passive's rule in the hint strip.
- **The move shows itself**: a companion's cell says MOVE READY (click); clicking it lights the spaces
  it can step to. Nothing on the board catches the mouse — clicks are hit-tested in
  `_UnhandledInput`, exactly as `KinBoard` does.

## Decided against the mockup (2026-09-14)

The mockup's bands are right. Four console readouts had no home in it; three were cut deliberately.

**The doom banner says what the doom DOES, statically.** Scenario name, countdown, and a fixed
per-scenario description — *"The water takes whatever is still standing in it."* That is the whole
band. **No live preview, no dial**: GO SPINNY is dropped, and the reasoning is recorded in
`KinJam.md` under Sub-themes. `KinPreviewer` keeps running in the console and loses only its UI
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
| Empty lane | dashed outline, no fill | **adopted** — `KinArt.DashedSlot` |
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
in `SQGodotCommon/KinGame/Art/CREDITS.md`, **which must ship**.

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
0.68   KinHandView.CardScale, the fan shrunk into one band
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

This rule has now been applied for real: `"when the doom fires: 6 to every enemy"` needed three lines
in a two-line box and rendered TRUNCATED. The prefix was the same nineteen characters on every
doom-triggered card, so it became the keyword `Doom:` — shorter on the card, and still explained on
hover. **Reach for the glossary before reaching for a smaller font.**

### Type

One family, three weights, and no exceptions. A jam with two typefaces reads as a jam.

- **Numbers are tabular, never proportional.** `12/16` and `2/10` must occupy the same width, or lane
  pips jitter every time a unit takes damage.
- **A badge sizes to its longest possible value, not its typical one.** The P/T badge was built for
  `2/2` and clipped `2/10` into `2/1` — a *wrong number* on screen, which is worse than an ugly one.
  Every numeric container is sized against the largest value its content can produce.
- Card names and screen titles are caps via `KinPalette.Caps`; rules text is sentence case.

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

**Card colour must MEAN something.** `KinArt.ColourFor` currently hashes the card's name into one of
five browns and teals, which spends the strongest signal a card has on noise — two Scavengers being
the same colour is not information, it is a coincidence that looks like information. Card colour
carries exactly one of: cost band, or act of origin. Pick one and hold it everywhere.

### Art

Flat vector SVG, authored to the palette, one file per subject in `SQGodotCommon/KinGame/Art/`.
Godot 4 imports SVG natively, so there is no build step and no atlas.

- **256x256 viewBox, subject filling roughly 85% centred.** The same file is the card art and, tinted
  down, the lane figure — a card and the body it becomes must be legibly the same thing.
- **Three tones and no more:** a near-black silhouette `#0C131B`, one mid tone for interior form
  (`#233444` or `#2B4054`), and at most one accent — red for hostile, gold for yours.
- **Draw order is composition.** A thing a figure HOLDS goes down after the figure, not before it.
  Reliquary Guard was drawn box-first and the body covered it completely — it read as a plain hooded
  figure and nothing else. The same rule put the ground line over Tunneller rather than under it.
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

- **A keyword is data, never a delegate.** Card text today is an authored string on `KinEffect.Text`
  and `RunCard.Description`. A keyword table lives in `KinCore` as data, so the console and the
  board read the same definition — and the serialization rule in the root `CLAUDE.md` applies with no
  exceptions.
- **Hover is never the only way to learn something a player must know to make the current decision.**
  It is for depth and reminders. If a fact is needed to choose a lane this turn, it is on the board.
- **Everything hoverable looks hoverable**, and the cursor changes. A tooltip nobody discovers is
  worse than no tooltip, because it was paid for.
- The enlarged card and its panel are **one hover target between them** — moving the mouse from the
  card onto the panel must not dismiss it.

### Small windows — measured 2026-09-17

**The supported minimum is 1280x720, and the 16px floor does not hold there.** That is a measured
limitation, recorded rather than papered over.

`canvas_items` stretch scales the whole canvas, so every text size scales with the window. At
1280x720 the factor is 0.667 against the 1920 canvas, so a 20-canvas-pixel label lands at 13 real
pixels. Nothing CLIPS, overflows or falls off at 720p — the layout holds — but the smallest text is
below the floor.

What was bought back rather than left:

| | was | now | at 1600x900 | at 1280x720 |
|---|---|---|---|---|
| Card rules text | 20 canvas | **22** | 18.3px | 14.7px |
| Doom banner description | 22 canvas | **24** | 20px | 16px |

Going further costs more than it buys: the rules box cannot hold three lines of anything larger, and
**`--resolution` on the command line does not change this** — `window/size/window_*_override` in
`project.godot` wins, so a capture that looks smaller may not be. Check the PNG's actual size before
believing a small-window screenshot.

- **Nothing carrying a number is ever dropped** at any size — a cell shrinks, it does not shed facts.
- If the floor has to hold at 720p later, the answer is a UI scale setting, not smaller margins.

### The glossary

`KinCore/Content/KeywordLibrary.cs` holds every term the game expects a player to know, as DATA —
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
  KinGame/kin_board.tscn -- --autostart
```

`--position 1920,0` keeps the window off the developer's other monitor. `-- --autostart` skips the
theme picker, without which every capture is a picture of the menu. Take a LATE frame; container
layout and card tweens have not settled on frame 0.

## Not decided

- Where the discard and draw piles are shown, or whether they are. The console does not show them
  and has not missed them.
- ~~The backdrop.~~ **Done 2026-09-17:** a painted flat-vector drowned city at
  `SQGodotCommon/KinGame/Art/background.png`, generated from
  `docs/mockups/backdrop-prompt.md`. It replaced eighty lines of procedural polygons.
  **The constraint that made it usable is the empty middle** — measured before wiring it in, the
  centre 60% has a per-channel standard deviation of 4 to 10, which is flat enough to draw five
  lanes over. Any replacement has to meet that; check it, do not eyeball it.
- Whether a PER-APOCALYPSE backdrop ships — one image per act rather than one for the game. The
  prompt file carries the three variants. Cheap now that the mechanism exists.
- ~~Card art.~~ **Done 2026-09-17:** hand-authored flat SVG in `SQGodotCommon/KinGame/Art/`, one
  file per subject name, falling back to the generated silhouette for anything undrawn. **Every card,
  enemy and Opponent in the game is drawn** — 52 files. The only named content without a drawing is
  the fourteen apocalypse SCENARIOS, which have no art slot in the UI; giving them one is a feature,
  not a gap. Budget by subject rather than by count: mechanical subjects land first try in about five
  minutes, organic ones need a second pass and take about twelve.
- Whether `Common/Cards/2D/` (`CardUI2D` + `Hand2D`, ~1600 lines of drag/hover/fan with no MTG in
  it) is reused as-is or trimmed. It is free and it is the reason the hand band is not a build task.
