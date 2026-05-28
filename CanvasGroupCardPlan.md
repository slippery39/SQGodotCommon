# CanvasGroup Card Variant — Implementation Plan

## Goal

Create a parallel `Card2DCanvasGroup` variant that uses a `CanvasGroup` instead of
`SubViewportContainer + SubViewport` for the internal card rendering. This lets us
visually compare both approaches before committing to one.

## Why SubViewport Causes Scaling Issues

`internal_cardui2d.tscn` renders all card layers into a fixed 400×500 `SubViewport`.
When `Card2D` scales the card down (e.g. in hand), Godot bitmap-scales that 400×500
texture — quality degrades. `CanvasGroup` instead renders children at the actual
display resolution, so scaling is clean at any size.

## Shader Compatibility

Both shaders are `shader_type canvas_item` and are compatible with CanvasGroup:

- **`damage_and_highlight.gdshader`** (outline) — currently on `SubViewportContainer`.
  CanvasGroup composites all children first, then runs its material shader on the
  result — same pattern, but at display resolution instead of 400×500.
  - The `outline_thickness` parameter currently means "pixels in the 400×500 viewport."
    After migration it means "screen pixels." Visual result should be similar;
    may need a small value tweak after testing.
  - The vertex expansion (`VERTEX += (UV*2-1) * outline_thickness`) needs room to
    bleed outside the group bounds → set `fit_margin = 16` on CanvasGroup.

- **`new_holo.gdshader`** (holographic) — on `CardContainer` (Node2D), applied
  per-sprite via `use_parent_material = true`. Nothing about this depends on
  SubViewport. Completely unchanged.

## Files to Create / Modify

### 1. `InternalCardUI2D.cs` — one-word change
Change `UpdateOutlineShader()` from `private` to `protected virtual`.
No other changes. This lets the subclass override only the outline lookup.

### 2. `InternalCardUI2DCanvasGroup.cs` — new file (same folder)
- Extends `InternalCardUI2D`
- `[Tool]` attribute (needed for editor preview to work)
- Adds `private CanvasGroup _canvasGroup`
- Overrides `_Ready()`:
  - Finds `%CanvasGroup` and stores it **before** calling `base._Ready()`
    (so that when `base._Ready()` → `UpdateVisuals()` → `UpdateOutlineShader()`
    fires, our override already has the reference set)
  - Calls `base._Ready()` for all other node lookups (sprites, labels, etc.)
- Overrides `UpdateOutlineShader()` to drive the CanvasGroup's material

Why set `_canvasGroup` before `base._Ready()`? `base._Ready()` calls `UpdateVisuals()`
at the end, which calls `UpdateOutlineShader()`. Virtual dispatch means our override
runs — but it needs `_canvasGroup` to already be set or it's a no-op.

### 3. `internal_cardui2d_canvasgroup.tscn` — new file (same folder)
New scene that uses `InternalCardUI2DCanvasGroup` as root script.
Structure replaces the SubViewport stack:

```
Before                                     After
------                                     -----
InternalCard2D (Node2D)                    InternalCard2D (Node2D)
└── SubViewportContainer  ← outline mat    └── CanvasGroup  ← outline mat, fit_margin=16
    └── SubViewport (400×500)                  └── CardContainer  ← holo mat
        └── Node2D (pos 200,250)                   ├── MainFrame
            └── CardContainer  ← holo mat          ├── ArtFrame
                ├── MainFrame                      │   └── ArtSprite
                ├── ArtFrame                       ├── Name
                │   └── ArtSprite                  │   └── NameLabel
                ├── Name                           ├── ManaCost
                │   └── NameLabel                  │   └── ManaCostLabel
                ├── ManaCost                       └── RulesText
                │   └── ManaCostLabel                  └── RulesTextLabel
                └── RulesText              └── BottomPoint (pos 0,235)
                    └── RulesTextLabel
└── BottomPoint (pos 0,235)
```

All sprite positions remain unchanged — they were relative to the centering Node2D
(200,250) in viewport space, which maps exactly to local origin (0,0) in Node2D space.

The intermediate centering `Node2D` is removed because CanvasGroup doesn't need it —
its children render in local space starting at (0,0).

### 4. `card_2d_canvasgroup.tscn` — new file (same folder as Card2D.tscn)
Copy of `Card2D.tscn` with one change: the `InternalCard2D` instance points to
`internal_cardui2d_canvasgroup.tscn` instead of `internal_cardui2d.tscn`.

Root script stays as `CardUI2D.cs` — no change needed there. `CardUI2D` does
`GetNodeOrNull<InternalCardUI2D>("InternalCard2D")`. Since `InternalCardUI2DCanvasGroup`
inherits `InternalCardUI2D`, this cast succeeds and all hover/drag/duplicate logic
continues to work without modification.

## Testing Plan

1. Open `card_2d_canvasgroup.tscn` in the Godot editor — verify it renders correctly
2. Assign `card_2d_canvasgroup.tscn` to `Hand2D.CardScene` temporarily and run the game
3. Compare the hand cards at various scales vs the original
4. Check outline shader looks correct (may need `OutlineThickness` value adjustment)
5. Verify holographic effect still animates

## What Is NOT Changing

- `CardUI2D.cs` — untouched
- `Hand2D.cs` / `Hand.tscn` — untouched (just swap `CardScene` export to test)
- `InternalCardUI2D.cs` — one visibility word only
- All shader files — untouched
- All texture resources — untouched
- `FitRulesTextToBox()` hardcoded dimensions (130px height, 230px width) — still valid,
  those are in card local-space units which don't change
