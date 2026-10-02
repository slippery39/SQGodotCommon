---
name: match-mockup
description: Make a KIN screen look like its mockup (or like the style-D kit when it has none), and make the art for it — the capture-compare-fix loop; ChatGPT for designs, local ComfyUI for sprites, card art and backdrops; the edge matte and `tools/install_art.py` that drop art into the game; `tools/make_ui.py` for textured UI. Use when asked to "match the mockup", "iterate on the visuals", "make it look better / less flat", to restyle a screen, when a new creature or card has no art ("falls back to a silhouette/portrait"), to generate a sprite, card illustration or backdrop, or to make mockup prompts. Read KinVisualDesign.md first.
---

# Matching a screen to its mockup — and making its art

**Read `KinVisualDesign.md` first**: the requirements, the chosen style (**D, fine line art**,
2026-09-26) and what each build iteration measured. The battle reference is
`docs/mockups/round1/chatgpt_D_S1.png`. `KinUI.md` is the layout contract.

## The loop

1. **Capture** the real screen (`Commands.md` has every flag):
   `./Run-Godot.ps1 KinGame/kin_party.tscn -Capture shots/x -Seconds 1.6 -GameArgs '--scenario=0'`
   Run screens: `--starter=0` (town), plus `--screen=areas|find|deep|gym|between|over`.
2. **Compare** with the target, side by side at the same size — never from memory.
3. **List the differences, biggest first.** Order that worked: art (sprites) → scale and placement →
   stage (backdrop) → cards → material (textured UI) → polish.
4. **Fix one, re-capture, LOOK.** "Looks off" is a bug report: find the cause (a label's hidden
   shadow, a horizon under the feet), never paint over the resemblance.
5. **Record** what was learned in `KinVisualDesign.md`, the contract change in `KinUI.md`.

A generated mockup is **mood, hierarchy, materials and composition** — not pixel positions and not
its text (it invents numbers). Take facts from the engine.

**No mockup for a screen? Apply the KIT, and give it a PLACE.** The battle mockup already fixed the
look; the run screens were restyled with no mockup of their own by changing the shared helpers
(`KinPartyRunScreens`: `Begin/Tile/Button/Label/Monster`) — one change, every screen. What a screen
lacks is a scene behind it: `Begin(title, subtitle, "town")` loads `Art/backdrops/town.png`.

## Which tool makes what

| Need | Route |
|---|---|
| A new screen's LOOK | **ChatGPT** — STYLE block + SCREEN block (`docs/mockups/mockup-prompts.md`), one chat per style. Daily quota: spend it only here |
| A creature's design, matching a mockup | ChatGPT in the mockup's chat: "draw <NAME> alone … side view facing right … flat pure white background" |
| Sprite from a mockup crop or an old portrait | **img2img, denoise ~0.7** (`tools/img2img.py`) — keeps the design, restyles it. 0.55 keeps the scene, 0.8 drifts |
| Sprite for a creature with NO art | **text-to-image** (`tools/gen_art.py --prompt-file`, the sprite style block in `KinVisualDesign.md`), 2–3 seeds |
| Card illustration | text-to-image 1344x832 — ACTIONS and objects come out well from text |
| Backdrop / screen scene | text-to-image 1344x768; for the battle, raise the stage (`StageLift`) until the ground is under the feet |
| UI chrome (plates, buttons, bars, orb, soft shadows) | **drawn, not generated**: `python tools/make_ui.py` → `Art/ui/`, used via `KinUiKit` 9-slices |

**Dropping it in — one command, then import:**
```
python tools/install_art.py sprite  gen.png "Old Mire" [--flip] [--grass]
python tools/install_art.py card    gen.png "Battle Cry"
python tools/install_art.py backdrop gen.png town
godot-mono --path SQGodotCommon --headless --import
```
`sprite` cuts it out with `pixelate.matte` (edges stop the flood at the dark outline, on any flat or
gradient ground), sizes it, and names it the way `KinArt` looks it up.

## Judging generated art

- **Contact sheet with the TARGET in row one** (the mockup's crop of the same thing), and the
  CUT-OUT on a coloured ground, not the raw image — the matte is where it fails.
- **Check FACING on every sprite: the file must face right** (`--flip`). About a third of seeds face
  left; a crop of a FOE from a mockup always does. Obvious in a capture, invisible on a sheet.
- **Nouns pull stereotypes** ("eagle" → bald eagle, "spirit" → an imp, "decoy" → a robot, "grub" → a
  toad). Say the shape: "a floating cyan flame", "a straw scarecrow on a post", "a caterpillar larva".
  Negatives barely help at CFG 2 (turbo); fix the positive prompt or the seed.
- **A portrait that is a whole SCENE cannot be matted** (its cut-out is the scene) — regenerate it.
- Colour bleeds between phrases ("yellow beak" → yellow wings). Keep colours to the thing they name.

## Traps (each cost a capture)

- **Every label on a backdrop needs its own `LabelSettings` with an outline** (and a drop shadow) —
  the theme's `outline_size` override drew nothing.
- **Readable words over a bright scene need a BACKSHADOW** — a blurred ellipse texture
  (`ui/scrim.png`). A `StyleBoxFlat` panel, even with shadow_size, reads as a dark card.
- **A creature "hovers" when**: the backdrop's ground under it is the far distance, or its contact
  shadow is hard-edged. Soft ellipse (`ui/contact.png`) sized to the sprite; raise the stage.
- **"Flat, default Godot" = solid `StyleBoxFlat`.** Use the kit textures: gradient, gloss, bevelled rim.
- **The shared card's rules label has a black outline AND a size-10 black shadow** — dark ink on
  parchment needs both zeroed.
- **`KinAnimator.Pop` tweens a Control's scale to 1** — scale an inner node, not a view's root.
- **Capture folders ship in the APK** unless ignored — `SQGodotCommon/shots/.gdignore` covers them.
- ComfyUI's queue is FIFO across processes: two jobs halve each other. Queue what the next capture
  needs FIRST. `gen_art.py --variants 1` writes `<label>.png`; more writes `<label>_v0.png`.
- Python here defaults to cp1252: always `read_text/write_text(encoding="utf-8")`. Long patches go
  in scratchpad files — shell heredocs mangle `\n` and quotes.
