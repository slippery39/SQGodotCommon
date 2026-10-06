---
name: generate-art
description: Generate card, creature or backdrop art with a LOCAL image model through ComfyUI, cull it, and drop it into a Godot card game in this repo. Use when art is needed in bulk, when a card has no art (MTG shows the placeholder dagger; Scryfall had nothing for it), when asked to "generate art", "make art with AI", "use ComfyUI/Stable Diffusion", or when setting up local image generation for the first time. Covers detecting whether ComfyUI is installed and walking the user through installing it, the three defects that survive a contact sheet, and the per-game rules that silently drop a file on the floor. Game-agnostic; MTG paths below.
---

# Generating art locally

Learned on the KIN branch (2026-09-24 to 26), ~100 images. The right route for bulk (~12 s an
image) and for creatures. **For a screen's whole LOOK, or a sprite that must match a mockup, use
the `match-mockup` skill** — ChatGPT designs, this pipeline produces.

**Nothing of this is in the repo except `tools/gen_art.py`, `img2img.py` and `pixelate.py`.**
ComfyUI, the checkpoints and the GPU live on the machine, so **step 0 is always finding out whether
any of it is there.**

## 0. Is it set up?

Do not assume a previous session's setup survived:

```bash
curl -s -m 5 http://127.0.0.1:8188/system_stats     # ComfyUI running?
ls D:/AI/ComfyUI_windows_portable/ComfyUI/models/checkpoints/   # checkpoints present?
```

- **Server answers** → step 1.
- **Installed but not running** →
  `D:\AI\ComfyUI_windows_portable\python_embeded\python.exe -s ComfyUI\main.py --port 8188`
  in the background. ~40 s to bind; poll `/system_stats` in an `until` loop, never a fixed sleep.
- **Not installed** → section A. **Ask before downloading gigabytes, and ask WHERE** — the system
  drive is often the wrong disk.

### A. Installing it

Confirm the hardware first; it decides the model:

```powershell
nvidia-smi --query-gpu=name,memory.total --format=csv
Get-PSDrive -PSProvider FileSystem | Select-Object Name, @{n='FreeGB';e={[math]::Round($_.Free/1GB,0)}}
```

Then, with the user's agreement:

1. **ComfyUI portable** — the `ComfyUI_windows_portable_nvidia.7z` asset of the latest release.
   `api.github.com/repos/comfyanonymous/ComfyUI/releases/latest` **301s** (the repo moved): curl
   with `-L`, or you parse `{"message":"Moved Permanently"}`. Needs 7-zip
   (`winget install 7zip.7zip`). Extract to a data drive — ~4 GB before any model.
2. **A checkpoint**, ~6.6 GB, into `ComfyUI/models/checkpoints/`. **DreamShaper XL Turbo** is the
   measured choice:
   `huggingface.co/Lykon/dreamshaper-xl-v2-turbo/resolve/main/DreamShaperXL_Turbo_v2_1.safetensors`.
   `gen_art.py --ckpt` must match the file name on disk.
3. Verify CUDA: `python_embeded\python.exe -c "import torch; print(torch.cuda.is_available())"`

**Not Illustrious or another booru-tagged anime finetune.** Measured over 27 images on 9 seeds with
English prompts: abstract triangles, blank frames, garbled text. It wants comma-separated booru
tags, and leans anime-monster.

## 1. Generate — always several, always cull

```bash
python tools/gen_art.py --styles flat --variants 3 \
  --subjects "a tall heron with a long sharp beak,a mossy tortoise" --out "$SCRATCH/art"
python tools/gen_art.py --prompt-file prompts.json --out "$SCRATCH/art"   # whole prompts, any size
```

The script locks checkpoint, sampler, steps, CFG and style suffix so a batch looks like one artist,
and varies the seed PER SUBJECT so one bad seed cannot poison the set. Styles and the negative
prompt live in it. SDXL wants ~60 words of keywords, not paragraphs; wide is 1344x768.

**Never one image per subject.** Malformed output is a seed lottery with a per-subject base rate —
"a mossy tortoise" grew a second head on 2 of 3 seeds; a heron never did.

## 2. LOOK at it, at 500 px or more

**A contact sheet is not an inspection.** Two-headed turtles and low-poly faceted skin survived
review at ~200 px a cell, twice, and were caught at full size. Resize and Read each candidate:

```bash
python -c "
from PIL import Image; import glob
for f in glob.glob('$SCRATCH/art/*.png'):
    Image.open(f).resize((500,500), Image.LANCZOS).save(f.replace('.png','_big.png'))"
```

Cull extra heads, fused limbs, scenery that swamps the subject. **Expect to cull one in three.**

| defect | cause | fix |
|---|---|---|
| extra heads, fused limbs | **the seed** | generate more, cull. Anatomy negatives measurably did NOT help |
| low-poly faceted skin | **the positive prompt** — "simple geometric shapes" read as *build it from shapes* | remove the phrase. Negating "low poly" did NOT work |
| scenery, grass, forests | the subject noun pulls its habitat in | reword the subject |
| the wrong creature | **nouns pull stereotypes**: "eagle" → bald eagle, "spirit" → an imp | describe the SHAPE: "a floating cyan flame" |
| a colour on the wrong part | colour bleeds between phrases ("yellow beak" → yellow wings) | keep each colour next to the thing it names |

**At CFG 2 on a turbo model, negative prompts are nearly inert** — CFG 3.5, 5.0, 7.0 left a
malformed seed malformed. **Fix the positive prompt or the seed, never the negative.**

## 3. Drop it in — the per-game rules

A miss is never an error: the loader returns null and the card shows a fallback that looks
deliberate. Check the exact name transform before believing a file is in.

**MTG** (`CardArtLoader`):
- Folder `SQGodotCommon/MtgGame/Assets/Card_Art/`, **`.jpg` only** — a `.png` is never looked for.
- Name: lowercase, every run of non-alphanumerics → `_`, trimmed. "Kird Ape, Jr." → `kird_ape_jr.jpg`.
- Existing art is **626x457** (Scryfall crops, ~1.37:1). Generate at 1216x832 (the nearest
  SDXL-native size) and resize to 626x457 so a generated card matches its neighbours.
- **Import, then set `compress/mode=1` (Lossy) in its `.import` and import again.** New files
  import lossless, which is how 31 MB of art became 145 MB in the APK (`Commands.md`, Android).

```bash
python -c "
from PIL import Image; import re, sys
src, name = sys.argv[1], sys.argv[2]
slug = re.sub(r'[^a-z0-9]+', '_', name.lower()).strip('_')
Image.open(src).convert('RGB').resize((626, 457), Image.LANCZOS).save(f'SQGodotCommon/MtgGame/Assets/Card_Art/{slug}.jpg', quality=90)
print(slug)" "$SCRATCH/art/flat_x_v1.png" "Card Name"
godot-mono --path SQGodotCommon --headless --import
```

**KIN** (`kin-pivot`): `tools/install_art.py` does sprite / card / backdrop in one command,
including the cut-out. Port it here, generalized, when a game on this branch needs cut-outs.

**A transparent cut-out** (sprites, tokens on a board): `pixelate.matte` — it floods the flat or
gradient ground from the border and stops at the subject's dark outline:
`python -c "import sys; sys.path.insert(0,'tools'); from pixelate import matte; from PIL import Image; matte(Image.open('in.png')).save('out.png')"`.
A portrait that is a whole SCENE cannot be matted — regenerate it on a plain ground.

## 4. Check it in the game, not in the folder

`./Run-Godot.ps1 <scene showing it> -Capture shots/art -Seconds 4`, then Read a late frame.
Measured on KIN: flat generated art survives small sizes better than expected, and **background
busyness, not detail, is what kills the small read.**

Locally generated images need no third-party credit. If one REPLACES a credited asset, update
`CREDITS.md`.

## Traps

- ComfyUI's queue is FIFO across processes — two jobs halve each other. Queue what the next capture
  needs first.
- `--variants 1` writes `<label>.png`; more writes `<label>_v0.png`, `_v1`…
- Python here defaults to cp1252: always `encoding="utf-8"` on text files.
