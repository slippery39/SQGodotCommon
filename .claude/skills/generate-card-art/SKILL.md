---
name: generate-card-art
description: Generate KIN card, enemy or companion art with a LOCAL image model through ComfyUI, then cull it and drop it in. Use when art is needed in bulk, when a subject has no drawing, when asked to "generate art", "make art with AI", "use ComfyUI/Stable Diffusion", or when setting up local image generation for the first time. Covers detecting whether ComfyUI is installed at all and walking the user through installing it if not, the three defects that survive a contact sheet, and why a square slot means no background removal is needed.
---

# Generating card art locally

Art can be **authored as flat SVG** (see the `draw-card-art` skill) or **generated with a local
model**. This skill is the generated route. It is the right one for bulk — 56 subjects at ~12s each
— and for creatures, which a model draws better than hand-written SVG geometry does.

**Nothing about this is checked into the repo except `tools/gen_art.py`.** ComfyUI, the checkpoints
and the GPU all live on the machine, so **step 0 is always finding out whether any of it is there.**

---

## 0. Is it set up?

Run these before anything else. Do not assume a previous session's setup survived.

```bash
curl -s -m 5 http://127.0.0.1:8188/system_stats     # ComfyUI running?
ls D:/AI/ComfyUI_windows_portable/ComfyUI/models/checkpoints/   # checkpoints present?
```

- **Server answers** → skip to step 1.
- **Installed but not running** →
  `D:\AI\ComfyUI_windows_portable\python_embeded\python.exe -s ComfyUI\main.py --port 8188`
  in the background. It takes ~40s to bind; poll `/system_stats` in an `until` loop rather than
  sleeping a fixed time.
- **Not installed** → walk the user through section A below. **Ask before downloading gigabytes to
  their machine**, and ask WHERE — this is many GB and the system drive is often the wrong disk.

### A. Installing it, if it is not there

Confirm the hardware first; it decides the model:

```powershell
nvidia-smi --query-gpu=name,memory.total --format=csv
Get-PSDrive -PSProvider FileSystem | Select-Object Name,
    @{n='FreeGB';e={[math]::Round($_.Free/1GB,0)}}
```

Then, with the user's agreement:

1. **ComfyUI portable** — the `ComfyUI_windows_portable_nvidia.7z` asset from the latest release.
   The repo moved: `api.github.com/repos/comfyanonymous/ComfyUI/releases/latest` **301s**, so curl
   with `-L` or you get `{"message":"Moved Permanently"}` and a confusing empty parse.
   Needs 7-zip (`winget install 7zip.7zip`). Extract to a data drive, not `C:` — it is ~4GB
   unpacked before any model.
2. **A checkpoint**, ~6.6GB, straight into `ComfyUI/models/checkpoints/`.
   **DreamShaper XL Turbo** is the measured choice:
   `huggingface.co/Lykon/dreamshaper-xl-v2-turbo/resolve/main/DreamShaperXL_Turbo_v2_1.safetensors`
3. Verify CUDA before generating anything:
   `python_embeded\python.exe -c "import torch; print(torch.cuda.is_available())"`

**Do not reach for Illustrious or another booru-tagged anime finetune.** Measured over 27 images on
9 seeds with natural-language prompts: abstract triangles, blank frames, and a heron with garbled
text baked into it. It wants comma-separated booru tags, not English, and the style leans
anime-monster. That is a different prompting discipline for a worse fit.

---

## 1. Generate — always several, always cull

```bash
python tools/gen_art.py --styles flat --variants 3 \
  --subjects "a tall heron with a long sharp beak,a mossy tortoise" \
  --out "$TEMP/art"
```

~12s per 1024x1024 image on an 8GB card. Style suffixes and the negative prompt live in the script;
it locks the checkpoint and the style so a batch looks like one artist, and varies the seed per
subject so one bad seed cannot poison the whole set.

**Never generate one image per subject.** Malformed output is a seed lottery with a per-subject base
rate — "a mossy tortoise" grew a second head on 2 of 3 seeds while a heron never did on any.

---

## 2. LOOK at it, at 500px or more

**A contact sheet is not an inspection.** Two-headed turtles and low-poly faceted skin both survived
review at ~200px a cell, twice, and were caught by a person looking at a full-size image.

```bash
python -c "
from PIL import Image; import glob
for f in glob.glob('$TEMP/art/*.png'):
    Image.open(f).resize((500,500), Image.LANCZOS).save(f.replace('.png','_big.png'))"
```

Then Read the images. Cull anything with extra heads, fused limbs, or scenery that swamps the
subject. **Expect to cull roughly one in three.**

### The three defects, and which lever fixes each

| defect | cause | fix |
|---|---|---|
| extra heads, fused limbs | **the seed**, with a per-subject base rate | generate more, cull. Anatomy negatives measurably did NOT help |
| low-poly faceted skin | **the positive prompt** — "simple geometric shapes" read as *build the creature out of geometric shapes* | remove the phrase. Negating "low poly, faceted" did NOT work |
| scenery, grass, forests | the subject noun pulls its habitat in | reword the subject. Background negatives barely help |

**The pattern: at CFG 2 on a turbo model, negative prompts are nearly inert.** Anatomy negatives did
not fix the heads; CFG 3.5, 5.0 and 7.0 at 20 steps left the malformed seed malformed. **Fix the
positive prompt or the seed, not the negative.**

---

## 3. Drop it in

Filename is the card/enemy/companion name, lowercased, spaces to underscores — the same convention
`draw-card-art` documents, and getting it wrong silently resolves a card to a *different* drawing.

```bash
cp "$TEMP/art/flat_a_tall_heron_v1.png" SQGodotCommon/KinGame/Art/pike.png
./Run-Godot.ps1 KinGame/kin_board.tscn -Headless -Seconds 2   # imports it
```

`KinArt.Drawing()` tries `.png` before `.svg`, so a generated image can sit beside the SVG it
replaces without deleting anything — which is what makes an A/B possible.

**No background removal is needed, and that is deliberate.** Lane slots are SQUARE (175x175) and the
art fills them edge to edge, precisely so opaque generated output drops straight in. See `KinUI.md`.
The one case that still needs transparency is the *fallback* silhouette, which is authored SVG.

---

## 4. Check it in the game, not in the folder

```bash
./Run-Godot.ps1 KinGame/kin_card_preview.tscn -Capture shots_cards -Seconds 2
```

Then Read a frame. A drawing that looks right at 1024 can be mush in a 175px lane slot — though
measured, flat generated art survives that better than expected, and it is **background busyness,
not detail**, that kills the small read.

---

## Credits

Locally generated images need no third-party attribution, unlike the CC-BY SVGs. **If any authored
asset is replaced rather than added, update `CREDITS.md`** — it must ship, and it currently credits
art that may no longer be in the build.
