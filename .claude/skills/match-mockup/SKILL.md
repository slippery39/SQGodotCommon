---
name: match-mockup
description: Give a Godot card game in this repo a deliberate LOOK — write the visual requirements, prompt mockups (ChatGPT/Gemini for screens, local ComfyUI for sprites and backdrops), judge them against a rubric, lock a style, then make each screen match its mockup with a capture-compare-fix loop. Use when asked to "make it look better / less flat / less default Godot", to "match the mockup", "iterate on the visuals", restyle a screen, make mockup prompts, or when a screen looks wrong and nobody can say why. Game-agnostic; proven on the KIN branch (2026-09-25 to 10-01).
---

# Matching a screen to its mockup

KIN went from "the game still looks terrible" to a chosen style and restyled screens in two days
with this process. The look half lives on `kin-pivot` as an example: `KinVisualDesign.md`
(requirements, rubric, what each build iteration measured), `docs/mockups/` (prompts, results).

**Division of labour (Shayne's preference): Shayne runs ChatGPT and Gemini — give the prompts
INLINE in chat, ready to paste. Claude runs ComfyUI locally** (`generate-art` skill) and does
cut-outs and resizing in Python.

## A. Choosing the look — once per game

1. **Requirements first, in a doc** — what the look must DO, not what it looks like: canvas and
   readability (what must read at a glance, at what size), yours vs theirs, what the main screen
   must answer with no hover, what must survive a mechanic change, and producibility (could our
   pipeline make 25 more of these?). Plus a **screen inventory** and the real content to show, so
   mockups carry real information.
2. **Prompts = one STYLE block + one SCREEN block, pasted together.** The style block is pasted
   word for word every time; that text is the only thing holding screens together. 2–4 directions,
   one style block each.
3. **Generate.** For ChatGPT / Gemini (they can lay out a UI and short text):
   - **One chat per direction.** Start with the hardest screen (the main board) — it sets the look.
   - Every later screen in that chat starts: *"Same game, same art style, same UI kit, same palette
     and same card/creature designs as the previous image. Now show a different screen:"*
   - Drifting anyway → upload the first image and say "match this". A new chat drifts.
   - Ask for landscape 16:9. Say the platform ("PC game on a 1920x1080 monitor, mouse-driven" — or
     phone, if that is the target), or the model guesses.

   ComfyUI cannot lay out a UI or write words: use it for what the pipeline will really produce —
   creatures, card art, backdrops — and to test whether a direction can be made locally at all.
4. **Save every result as `docs/mockups/<game>/<generator>_<direction>_<screen>.png` and COMMIT
   them.** KIN's first mockups were lost because nobody did.
5. **Score against the rubric** (below), compare DIRECTIONS rather than single images, and pick one
   or a named mix.
6. **Lock it** as a style guide in the design doc: palette hexes, type, frame shapes, the
   creature/art recipe, the icon set.

### The rubric — 1 to 5 each

1. **The main screen reads at a glance.** Can you say what matters most (whose turn, what the
   opponent threatens, who is closest to losing) in five seconds, at the real window size?
2. **Sides read as a confrontation**, not a spreadsheet.
3. **Consistent across screens** — one game, not six pictures.
4. **Cards / creatures read at their real size** — distinct silhouettes, one style.
5. **Producible** — could our pipeline make 25 more? Test it with ComfyUI, don't guess.
6. **Buildable in Godot** — styleboxes, 9-slices, icons? Or a painting that only works as one image?
7. **Tone** — does it feel like the game it is?

**A generated mockup is mood, hierarchy, materials and composition — never pixel positions or
text.** Models invent UI and numbers and garble words. Take facts from the engine.

## B. The loop — making a screen match

1. **Capture** the real screen: `./Run-Godot.ps1 <scene> -Capture shots/x -Seconds 4` (build the
   Godot project first — a capture runs the last build).
2. **Compare side by side at the same size** — never from memory.
3. **List the differences, biggest first.** Order that worked: art → scale and placement → stage
   (backdrop) → cards → material (textured UI) → polish.
4. **Fix ONE, re-capture, LOOK.** "Looks off" is a bug report: find the cause (a hidden shadow, a
   horizon under the feet) rather than painting over the resemblance.
5. **Record** what was learned in the design doc, and any layout-contract change.

**No mockup for a screen? Apply the kit through shared helpers** — KIN restyled every run screen
with no mockup of its own by changing one set of helpers (title, tile, button, label), and gave each
screen a PLACE: a backdrop behind it.

## Which tool makes what

| Need | Route |
|---|---|
| A screen's LOOK | **ChatGPT/Gemini** (Shayne) — STYLE + SCREEN block. Quota is daily: spend it here |
| A creature's design, matching a mockup | ChatGPT in the mockup's own chat: "draw <NAME> alone … side view facing right … flat pure white background" |
| Art from a mockup crop or an old portrait | **img2img at denoise ~0.7** (`tools/img2img.py`) — keeps the design, restyles it. 0.55 keeps the scene; 0.8 drifts |
| Art for something with NO design yet | text-to-image (`tools/gen_art.py --prompt-file`), 2–3 seeds |
| Card illustration | text-to-image — actions and objects come out well from text |
| Backdrop | text-to-image 1344x768; raise the stage until the ground is under the feet |
| A transparent cut-out | `pixelate.matte` (the `generate-art` skill) |
| True pixel art | `tools/pixelate.py` — no generator makes a real pixel grid; this pass does |
| UI chrome (plates, buttons, bars, soft shadows) | **drawn, not generated** — `tools/make_ui.py` on `kin-pivot`; port it with this game's palette once a style is locked |

`img2img.py` takes a JSON job list: `[{"init", "crop": [l,t,r,b] | null, "prompt", "negative",
"denoise", "seed", "out"}]` — it pads the crop to a square on white.

## Judging generated art

- **Contact sheet with the TARGET in row one** (the mockup's crop of the same thing), and every
  cut-out shown on a coloured ground, not the raw image — the matte is where it fails.
- **Check FACING on every sprite** — about a third of seeds face the wrong way, and a crop of an
  opponent's creature from a mockup always does. Obvious in a capture, invisible on a sheet.
- The `generate-art` skill has the defect table (seeds, stereotyped nouns, colour bleed).

## Traps — each cost a capture

The Godot layout traps are in `.claude/rules/godot-frontend.md`. The visual ones:

- **"Flat, default Godot" = solid `StyleBoxFlat`.** A material needs texture: gradient, gloss, a
  bevelled rim, as 9-slice `StyleBoxTexture`.
- **Every label over a picture needs its own `LabelSettings` with an outline** (and drop shadow);
  over a bright scene, a blurred backshadow texture behind the words.
- **A creature "hovers" when** the backdrop's ground under it is the far distance, or its contact
  shadow is hard-edged. A soft ellipse sized to the sprite; raise the stage.
- **The shared card's rules label has a black outline AND a size-10 black shadow** — dark ink on a
  light (parchment) box needs both zeroed, on a DUPLICATED `LabelSettings`.
- **Capture folders ship in the APK** unless ignored — `Run-Godot.ps1` writes their `.gdignore`.
