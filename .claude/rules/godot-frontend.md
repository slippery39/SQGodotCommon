---
paths:
  - "SQGodotCommon/Common/*/*.cs"
  - "SQGodotCommon/Common/*/*/*.cs"
  - "SQGodotCommon/Common/*/*/*/*.cs"
  - "SQGodotCommon/Common/*/*/*/*.tscn"
  - "SQGodotCommon/MtgGame/*.cs"
  - "SQGodotCommon/MtgGame/*/*.cs"
  - "SQGodotCommon/MtgGame/*.tscn"
  - "SQGodotCommon/MtgGame/*/*.tscn"
  - "SQGodotCommon/Project/*.cs"
  - "SQGodotCommon/Project/*/*.cs"
---

# Godot front end — the traps that fail SILENTLY

Loaded when you touch the Godot project. Game-agnostic: every entry was paid for once on the KIN
branch (2026-09-17 to 10-03) and is true of any game built on `Common/`. MTG's card-face and board
rules are in `mtg-presentation.md`; how to run and capture a scene is in `Commands.md`.

**A size, position or layout that has not been seen on a screen is a guess.** Capture it
(`./Run-Godot.ps1 <scene> -Capture shots/x`) and read a late frame. And a capture only catches what
is on screen — twice, twenty minutes of hand-play found what a screenshot loop could not.

## Layout — nothing here errors when it breaks

- **A Container overrides its children's anchors and positions on every layout pass.** If a
  `Position` or anchor preset is ignored, check the PARENT's type before your maths. A
  `PanelContainer` root stretched a screen full-size and dragged an anchored button behind the
  cards; a `ColorRect` root fixed both.
- **A `Control` container will not lay out a `Node2D`** — it leaves it at the origin. Card rows are
  `Node2D` and are positioned by hand for this reason.
- **An unwrapped `Label`'s minimum width is its whole line**, and a Control is never smaller than its
  content's minimum, so anchors are a suggestion until the text wraps. Set `AutowrapMode` **and**
  `CustomMinimumSize.X = 1`; autowrap alone still reports a minimum width.
- **`CanvasLayer` defaults to layer 1**, above plain `Node2D` children on layer 0. A "background"
  added that way covers the screen. Use `Layer = -1` for a ground.
- **`ZIndex` beats tree order.** `Hand2D` assigns one per card, so the hand draws over a dimmed
  overlay unless the overlay's content is raised or the hand hidden.
- **Tweening a Control's `position` inside a container loses to the layout pass.** Shake the
  `CanvasLayer`'s `Offset` instead — nothing owns that.
- **`TextureRect`: set `ExpandMode = IgnoreSize` BEFORE `Texture` and `Size`** in the initializer.
  Set after, the texture's own pixel size has already become the minimum size (a 512 px icon covered
  half the screen).
- **A `Label` cannot hold an icon.** Inline symbols need a `RichTextLabel` (`[img=WxH color=#..]`).
- **Text over a busy picture needs its own `LabelSettings` with an outline** (and a shadow); a
  theme's `outline_size` override drew nothing. Over a bright scene, add a blurred backshadow
  texture — a `StyleBoxFlat` panel reads as a dark card, not as a shadow.
- **Small symbols need brighter tints than the palette.** A colour that reads at panel size reads as
  mud at chip size.

## Errors that look like missing UI

- **An exception while a screen is being built SILENTLY drops everything built after it.** A reward
  row AND its SKIP button vanished. **A button or row missing from a capture: check the Godot log for
  an exception before anything else.**
- **New assets do not exist until imported** (`godot-mono --path SQGodotCommon --headless --import`).
  Until then `ResourceLoader.Exists` is false and nothing errors.
- **`--resolution` does not resize the window** while `project.godot` sets
  `window/size/window_*_override`. Check a capture's real pixel size before believing it.

## Input, mouse and touch

- **A hover precondition is a touch bug.** With a mouse the pointer hovers for many frames before a
  click; with a finger **the press IS the first contact** and Area2D picking has not run, so "am I
  the hovered card?" is false exactly when the press arrives. `DraggableNode2D` therefore only ARMS
  a drag on press and starts it on the first motion. Any new "only while hovered" gate needs the
  same treatment, or it works on desktop and never on a phone.
- **Card hover is physics picking**, so any Control that catches the mouse over a card silently
  blocks it. A symbol with a tooltip over clickable content wants `MouseFilter.Pass` (shows the tip
  AND passes the click); `Ignore` shows nothing, `Stop` eats the click.
- **A Button inside a `ScrollContainer` eats the swipe** on a touchscreen — the row scrolled only by
  its bar. Give the buttons `MouseFilter.Pass`, and ignore a press if the row moved between
  `ButtonDown` and `Pressed` (a swipe is not a pick).
- **A live hand card inside a menu fights the menu** — it takes hover and drag through its own
  collision area. Show a card outside the hand as a picture: render it once into a `SubViewport` and
  show the texture in a `TextureRect`.
- **A synthetic mouse move does not drive physics picking**, and calling a handler directly skips
  every mouse filter. To verify anything clicked, push REAL input events through the viewport.

## The shared card (`Common/Cards/2D`) — every game uses it

**Add nodes to an instance; never edit the shared scenes, and never mutate a shared `LabelSettings`
in place** — duplicate it first. Every game on this branch and its forks renders through these.

- **`Card2D.tscn` and `card_2d_canvasgroup.tscn` carry a scale of their own (0.7).** With a hand or
  zone scale and the window's stretch that is three factors, not two. Author sizes through one
  helper that encodes the chain, never in raw points.
- **`CardUI2D` fills only once READY** — `_internalCardUI2D` is assigned in `_Ready`, so `ApplyTo` on
  a card not yet in the tree throws (and see "drops everything built after it", above). Fill it in
  `ui.Ready +=` or after `AddChild`.
- **`CardUI2D.Clicked` fires only while it is the hovered card**, and starting a drag clears the
  hovered card on the same press. A draggable card can never be clicked; click-to-select needs
  dragging off (`DragEnabled`).
- **`StartHover` tweens the card to the BOTTOM of the viewport** — right for a hand, wrong anywhere
  else. `IsPosLerping = true` suppresses it.
- **`Hand2D.DrawCard()` starts the card at global x = 0.** Pass an origin, or every draw flies in
  from the left edge. Invisible on a still frame; it had been true for three sessions before a
  capture of motion showed it.
