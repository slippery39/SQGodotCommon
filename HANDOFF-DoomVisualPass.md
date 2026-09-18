# Handoff — DOOMJAM: the visual pass, and what looking at it kept finding

> **SUPERSEDED by `HANDOFF-DoomCombatV3.md` (2026-09-18) — read that first.** The §4 scars below
> are all still true and are the reason to keep this file.
>
> **SUPERSEDED IN PART by the v3 design pass (2026-09-17).** A playtest found three problems —
> Ash is irrelevant, board stalls are common, and cards and enemies are all just stats — and the
> answer was to make units EPHEMERAL. Read `DoomJam.md` "Combat v3" and "Build order for v3" FIRST.
> The visual findings below all still hold; the balance numbers in them describe the v2 game.

**Read this, then `DoomJam.md`, then `DoomUI.md`.** `HANDOFF-DoomPacingAndRewards.md` is the previous
session and is superseded — read only its §4 scars.

State at handoff: **DoomCore.Tests 113/113 green, Godot project builds, MTG projects untouched, and
the working tree is CLEAN.** Everything is committed, in eighteen commits.

---

## 1. The headline

1. **The game has a face.** Board, cards, reward screen, intermission, animation, a hover glossary
   and a painted backdrop — all against a mockup, all recorded in `DoomUI.md`.
2. **Every card, enemy and Opponent is drawn** — 52 SVGs. No generated pawns left in play.
3. **Rules text rendered for the first time.** It had never worked; the fallback returned `""`.
4. **Three numbers on screen were WRONG**, not merely ugly, and each looked like a style problem.
5. **The board measures its own height** and says so when it overflows.
6. **The thing that found nearly all of it was looking at a capture**, not reasoning about code.

## 2. The one thing to carry

**Every "it looks a bit off" in this session was a bug, and never the one it looked like.**

- Rules text looked too small. It was **empty** — `Description` was authored for every card and
  displayed nowhere.
- The stat badge looked cramped. It was showing **the wrong number**: `2/10` rendered as `2/1`.
- Fonts looked mis-sized. A **third scale factor** nobody had counted — the card scene carries a 0.7
  of its own — made every card label 40% of its authored size.
- The reward panel's title ran off screen. The title was **innocent**; an unwrapped Label's minimum
  width is its whole line, so one long string dragged the entire panel past the edge.
- A reward card sat lower than its neighbours. Not a layout bug: hover **moves a card to the bottom
  of the viewport**, which is right for a hand and wrong for a mid-screen row.

> **Look at the thing, then find out WHY it looks like that. Do not fix what it resembles.**

The corollary, which cost real time: **a capture only catches what happens to be on screen.** The
hover bug existed from the moment the reward screen was built and only appeared when the mouse
happened to rest over a card during a capture. Screenshots are not playing it.

## 3. What exists now

| | |
|---|---|
| `DoomCardFace` | **the whole card face, in one place** — the hand, the preview and the reward screen all draw through it |
| `DoomCardPreview` + `doom_card_preview.tscn` | a card rack with no battle behind it. Loads the cards that BREAK the layout |
| `DoomAnimator` | float / pop / flash / shake, on one `Speed` dial. F4 cycles it live |
| `DoomCardInspector` | the hover panel: full rules text, then keywords |
| `DoomClockDial` | the doom clock as a ring of segments in the banner corner |
| `KeywordLibrary` (DoomCore) | the glossary, as data. Whole-word matching, with a test |
| `RunCard.ToDoomCard()` | **one** conversion — `Run.StartBattle`, the preview and the rewards all use it |
| `DoomArt.Drawing()` / `Art/*.svg` | art by name convention, falling back to the generated silhouette |
| `Art/background.png` | the painted backdrop, replacing 80 lines of polygons |
| `CREDITS.md` (root) | **must ship.** CC BY 3.0 is not satisfied by a file in a repo |

## 4. Scars worth not re-earning

**Godot:**

- **A Container overrides its children's anchors and positions, every layout pass.** The intermission
  root was a `PanelContainer`, so the panel stretched full-screen and an anchored button was dragged
  up behind the cards. A `ColorRect` fixed both at once. If a position is being ignored, check the
  parent's type before you check your maths.
- **An unwrapped `Label`'s minimum width is its entire line**, and a Control is never smaller than
  its content's minimum. Anchors are only a suggestion until the content can wrap. `AutowrapMode`
  **plus** `CustomMinimumSize.X = 1` — autowrap alone still reports a minimum.
- **`CanvasLayer` defaults to layer 1**, above plain Node2D children on layer 0. A "background"
  added that way covers everything. This cost two separate debugging sessions.
- **`ZIndex` beats tree order.** `Hand2D` gives every card one, so the hand drew over a dimmed
  overlay and over the reward cards.
- **A `Control` container will not lay out a `Node2D` at all** — it leaves it at the origin. The
  reward cards are positioned by hand for exactly this reason.
- **`--resolution` on the command line does NOT resize the window** — `window/size/window_*_override`
  in `project.godot` wins. Two "small window" captures were silently 1600x900. **Check the PNG's
  real size before believing a small-window screenshot.**
- **New assets are invisible until `--headless --import` has run.** `ResourceLoader.Exists` returns
  false and nothing errors anywhere.
- **Tweening a Control's `position` inside a container loses to the layout pass.** Shake the
  `CanvasLayer`'s `Offset` instead; nothing owns that.

**Drawing:**

- **Build a creature from named parts, never one hand-written polygon.** Four subjects failed this
  way and all four read instantly once rebuilt. Mechanical subjects survive a single polygon;
  organic ones do not.
- **Draw order is composition.** A thing a figure holds goes down AFTER the figure. Reliquary Guard
  was drawn box-first and the body hid the box completely.
- **Avoid `opacity`.** The preview renderer ignores it and rendered soft glows as solid blobs — and
  the flat style forbids gradients anyway. Pre-blend, or use rays.
- **It must read at 40px**, the lane-figure size. A shape that survives 40px always survives 256px.

**Process:**

- **I clobbered a test file with `cat >` and caught it only by a number.** `CompanionTests.cs`
  already existed; the test count went **110 → 106 after adding four tests**. Append, or check
  `git status` for ` M` before writing a file you think is new.
- **Verify a fix by the number, not by the absence of an error.** The column-overflow check reported
  "1456px of 1080" on its first run because it counted the hand twice — a wrong number looks exactly
  like a large one.
- **Tune the doc to the measurement, not the other way round.** The 16px floor does not hold at
  1280x720; that is written down rather than quietly redefined.

## 5. What to do next

**A game design pass, which is why this handoff exists.** The visuals are no longer the bottleneck.
`DoomJam.md`'s "State of play" and "Open questions" were refreshed on 2026-09-17 for exactly this.

The design work that is actually queued, in the order it is likely to pay:

1. **PLAY IT.** Every session that skipped this paid for it. Twenty minutes of hand-play has twice
   found what ten sim runs missed.
2. **The Rising is the weak act** — 20.7% against 34.3% and 29.0%. Read `docs/findings/doom-balance.md`
   run 12 first; the obvious fix was tried and made it worse.
3. **The stalemate tail.** The mean is healthy and the worst case is 50 turns. Prime suspect is
   stacked healing, so the fix is probably a cap rather than a change to any one number.
4. **Ashfall authors no Companion mark** — see `DoomJam.md`, "Open questions". Playable content with
   a silent no-op in it, deliberately left because the stats are a balance decision.
5. **Rapture** is still unimplemented and gated to floor 99.
6. **Elites, salvage, events, multi-act.** `DoomIntermission` is where they live; generalise it ONCE,
   when events actually need it.

Visual work still open, none of it blocking: per-act backdrops (the prompt file has the variants and
the loading mechanism exists), a credits screen, and art for the apocalypses themselves — the only
named content with no drawing, and the only one with nowhere to put one.

## 6. What needs a human

- **`docs/mockups/doomjam-board-mockup.png`.** Still missing. An agent cannot write an image out of
  a conversation to disk, and this is the second session the mockup has been lost in.
- **The game-icons attribution must reach a player** — a credits screen or the itch.io page. See
  `CREDITS.md`.

## 7. How to reproduce anything here

```
dotnet test DoomCore.Tests                                   # 113/113
dotnet run --project DoomConsole -c Release -- content       # every act, doom, enemy and card
godot-mono --path SQGodotCommon --headless --import          # after ANY new art

godot-mono --path SQGodotCommon --position 1920,0 --write-movie shots/doom.png \
  --fixed-fps 10 --quit-after 40 DoomGame/doom_board.tscn -- --autostart
```

`-- --autostart` skips the picker, `--autoturn` drives real turns so animation can be seen, and
`--reward` opens the reward screen with a Companion carrying every mark in the library. `Commands.md`
has all of it and the traps. **Take a LATE frame.**
