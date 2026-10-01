---
paths:
  - "SQGodotCommon/KinGame/*.cs"
  - "SQGodotCommon/KinGame/*.tscn"
  - "SQGodotCommon/Common/Cards/2D/Card2D/*.cs"
  - "SQGodotCommon/Common/Cards/2D/Hand2D/*.cs"
---

# DOOMJAM front end — the Godot traps that cost real time

Loaded when you touch `SQGodotCommon/KinGame/`. **Read `KinUI.md` too** — it holds the layout
contract and the visual language. This file is only the things that go wrong SILENTLY, each of which
was paid for once on 2026-09-17.

**Game logic never lives here.** The UI reads a fact from `KinStateExtensions`; it never computes
one.

## Nothing here errors when it breaks

- **A Container overrides its children's anchors and positions, every layout pass.** If a `Position`
  or an anchor preset is being ignored, check the PARENT's type before checking your maths. A
  `PanelContainer` root stretched the intermission full-screen and dragged an anchored button up
  behind the cards; a `ColorRect` fixed both at once.
- **A `Control` container will not lay out a `Node2D` at all** — it leaves it at the origin. Card
  rows are `Node2D` and are positioned by hand for exactly this reason.
- **An unwrapped `Label`'s minimum width is its whole line**, and a Control is never smaller than its
  content's minimum — so anchors are a suggestion until the text can wrap. Use `AutowrapMode`
  **and** `CustomMinimumSize.X = 1`; autowrap alone still reports a minimum width.
- **`CanvasLayer` defaults to layer 1**, above plain `Node2D` children on layer 0. A "background"
  added that way covers the whole screen. Use `Layer = -1` for a ground.
- **`ZIndex` beats tree order.** `Hand2D` assigns one per card, so the hand draws over a dimmed
  overlay unless the overlay's content is raised or the hand is hidden.
- **Tweening a Control's `position` inside a container loses to the layout pass.** Shake the
  `CanvasLayer`'s `Offset` instead — nothing owns that.
- **New assets do not exist until they are imported.** `godot-mono --path SQGodotCommon --headless
  --import`. Until then `ResourceLoader.Exists` returns false and nothing errors anywhere.
- **`--resolution` does NOT resize the window** — `window/size/window_*_override` in `project.godot`
  wins. Check a capture's real pixel size before believing it is small.

## Symbols first — the default, not a law (Shayne, 2026-10-01)

**When unsure, start with a SYMBOL and a number, and put the words in a hover.** Text on screen has to
earn its place; it is not the default. Not everything needs a symbol — but a screen cluttered with
sentences is the failure this project already had once (the declutter pass, `KinUI.md`). Every symbol
must say what it means on hover, and its meaning is written once, in `KinSymbols`.

## Learned in the declutter pass (2026-09-30 / 10-01) — each one broke silently

- **`TextureRect`: set `ExpandMode = IgnoreSize` BEFORE `Texture` and `Size`** in the initializer.
  Set after, the texture's own 512 px had already become the minimum size — a Block shield covered
  half the field.
- **The shared card (`CardUI2D`) fills only once it is READY.** `ApplyTo` / `KinCardFace.Style` on a
  card not yet in the tree throws — and an exception while a screen is being built SILENTLY drops
  everything after it (the reward cards AND the SKIP button vanished). Fill it in `ui.Ready +=`.
  **A button or row missing from a capture: check the Godot log for an exception first.**
- **A live hand card in a menu fights the menu**: it takes hover and drag through its own collision
  area (physics picking). Show a card OUTSIDE the hand as a picture — drawn once into a
  `SubViewport`, its texture in a `TextureRect` (`KinPartyRunScreens.CardButton`).
- **A `Label` cannot hold an icon.** Inline symbols need a `RichTextLabel` (`[img=WxH color=#..]`)
  ADDED to the card instance over the shared rules box; hide the original with `SelfModulate`
  alpha 0, never by emptying it — the shared fitter and `Details` still read it.
- **The battle field catches no mouse on purpose** (card hover is physics picking), so a hover tip
  there is the BOARD hit-testing (`KinRelayField.TipAt` → `KinRelayCreature.TipAt`), never a
  Control's `TooltipText`. On the run screens a symbol is a Control with `MouseFilter.Pass` and a
  `TooltipText`: PASS shows the tip AND lets the click reach the tile (Ignore shows nothing, Stop
  eats the click).
- **A dangling `else` binds to the nearest `if`** — `if (a) for (...) if (b) x; else y;` ran `y` for
  every refused drop, not when `a` was false. Brace any `if` whose body is a loop.
- **Small symbols need brighter tints than the palette**: Ember's `#C2621F` read as mud at chip size
  over a meadow; chips use `#FF9A3C`.

## The shared card is MTG's too

`Common/Cards/2D` is used by the MTG scenes, which this project must leave a clean no-op. **Add nodes
to an instance; never edit the scene, and never mutate a shared `LabelSettings` in place** —
duplicate it first.

Consequences worth knowing:

- **`card_2d_canvasgroup.tscn` carries a scale of its own (0.7).** With the fan scale and the window
  letterbox that is three factors, not two. `KinCardFace.Pt()` encodes the chain — author through
  it, never in raw points.
- **`CardUI2D` raises `Clicked` only while it is the hovered card**, and beginning a drag clears the
  hovered card on the same press. A draggable card can therefore never be clicked.
- **`StartHover` moves the card to the bottom of the viewport.** That is right for a hand and wrong
  anywhere else; `IsPosLerping` is the scene's own early-out and suppresses it.
- **`Hand2D.DrawCard()` starts a card at global x = 0.** Pass an origin, or every draw flies in from
  the screen's left edge.

## Verify by looking

**A size that has not been seen on a screen is a guess** — the enforcement loop is in `KinUI.md`
and the flags are in `Commands.md`. `kin_card_preview.tscn` renders cards with no battle behind it
and loads the ones that BREAK the layout; use it for card work rather than starting a run.

**A capture only catches what happens to be on screen.** It is not playing the game, and twice now
twenty minutes of hand-play has found what a screenshot loop could not.
