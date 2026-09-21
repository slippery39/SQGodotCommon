"""
Generate card/companion art through a running ComfyUI, one image per subject.

WHY A SCRIPT AND NOT THE ComfyUI UI: 56 subjects have to look like ONE artist. The only way that
happens is by locking every variable except the noun — same checkpoint, same sampler, same steps,
same CFG, same style suffix, same seed policy. A human clicking through a web UI cannot hold that,
and the first time one image is generated with a different CFG the set stops matching.

**GENERATE SEVERAL PER SUBJECT AND CULL, AND INSPECT AT FULL SIZE.** `--variants` defaults to 3.
Two failure modes, and they need different responses:

- **Malformed anatomy is a SEED lottery with a per-subject base rate.** "a mossy tortoise" in the
  storybook style returned two heads on 2 of 3 seeds; the heron never doubled on any. Culling is
  the only guard, and for a shell-and-neck subject you may cull most of a batch.
- **Style artefacts are a PROMPT problem and affect every seed.** See the note on the flat style.

**A contact sheet is NOT an inspection.** Two-headed turtles and low-poly skin both survived review
at ~200px a cell and were caught by a person looking at the full image. Check candidates at 500px
or more before accepting a batch.

Usage:
    python tools/gen_art.py --subjects "a badger,a heron,a frog" --out scratch/art
    python tools/gen_art.py --styles flat --variants 4 --subjects "a mossy tortoise" ...

ponytail: talks to ComfyUI's HTTP API with urllib and polls /history. No websocket, no client lib.
"""

import argparse
import json
import pathlib
import time
import urllib.request
import zlib

SERVER = "http://127.0.0.1:8188"

# The style is the ONLY thing separating a coherent set from 56 unrelated pictures, so each one is
# named and fixed here rather than typed fresh per image.
STYLES = {
    # **DO NOT reintroduce "simple geometric shapes".** It read as *build the creature OUT OF
    # geometric shapes* and produced low-poly triangulated skin — flat-shaded 3D mesh facets across
    # every body. A/B'd on fixed seeds: removing the phrase cleared it completely, while negating
    # "low poly, faceted, triangulated" on top of the original phrase did NOT (negatives are weak
    # at CFG 2 — see NEGATIVE). The positive prompt is the lever here, not the negative.
    "flat": (
        "flat vector illustration, bold clean outlines, limited flat colour palette, "
        "smooth solid colour fills, centred full body, plain solid background, "
        "game card art, no text"
    ),
    "storybook": (
        "children's storybook illustration, soft gouache texture, warm friendly palette, "
        "centred full body character, plain solid background, no text"
    ),
    "chunky": (
        "chunky stylised 3D-render-look creature, thick rounded forms, soft studio lighting, "
        "saturated colours, centred full body, plain solid background, mobile game icon, no text"
    ),
}

# Anatomy terms lead, though MEASURED THEY BARELY HELP — see the seed note on `subject_seed`. A
# malformed subject is a seed problem here, not a prompt problem: adding these changed nothing on
# the seed that was failing, and neither did CFG 3.5, 5.0 or 7.0. They stay because they cost
# nothing, not because they are the guard. The guard is culling.
NEGATIVE = (
    # anatomy
    "extra heads, two heads, extra limbs, extra legs, missing limbs, deformed, mutated, "
    "disfigured, malformed, fused body parts, duplicate, cloned face, bad anatomy, "
    # composition
    "text, watermark, signature, logo, letters, border, frame, multiple characters, "
    "cropped, out of frame, "
    # background — the thing that actually breaks the 40px read and the matting pass
    "cluttered background, scenery, landscape, grass, forest, trees, ground, shadow, "
    "photorealistic, blurry"
)


def workflow(subject, style, ckpt, seed, steps, cfg, size):
    """A minimal SDXL txt2img graph, in ComfyUI's node format."""
    return {
        "4": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": ckpt}},
        "5": {
            "class_type": "EmptyLatentImage",
            "inputs": {"width": size, "height": size, "batch_size": 1},
        },
        "6": {
            "class_type": "CLIPTextEncode",
            "inputs": {"clip": ["4", 1], "text": f"{subject}, {STYLES[style]}"},
        },
        "7": {
            "class_type": "CLIPTextEncode",
            "inputs": {"clip": ["4", 1], "text": NEGATIVE},
        },
        "3": {
            "class_type": "KSampler",
            "inputs": {
                "model": ["4", 0],
                "positive": ["6", 0],
                "negative": ["7", 0],
                "latent_image": ["5", 0],
                "seed": seed,
                "steps": steps,
                "cfg": cfg,
                "sampler_name": "dpmpp_sde",
                "scheduler": "karras",
                "denoise": 1.0,
            },
        },
        "8": {"class_type": "VAEDecode", "inputs": {"samples": ["3", 0], "vae": ["4", 2]}},
        "9": {
            "class_type": "SaveImage",
            "inputs": {"images": ["8", 0], "filename_prefix": "kin"},
        },
    }


def subject_seed(subject, base, variant):
    """
    A seed that differs PER SUBJECT, derived so it is still reproducible.

    **One seed was originally locked across the whole set**, on the theory that holding every
    variable but the noun is what makes a batch look like one artist. It is — and it also means a
    bad seed fails every subject identically, which is the worst possible failure because the
    output stays perfectly consistent and therefore looks fine.

    Measured: seed 7 produced a two-headed tortoise in all three styles and on both checkpoints,
    while seeds 11, 23 and 42 were clean. Anatomy negatives did not fix it and neither did CFG
    3.5/5.0/7.0 — the seed was the whole cause.

    Style coherence comes from the checkpoint and the style suffix, which are still locked. The
    seed was never what was holding the set together.
    """
    return (base * 1_000_003 + zlib.crc32(subject.encode()) + variant * 7919) % (2**31)


def post(path, payload):
    data = json.dumps(payload).encode()
    req = urllib.request.Request(
        f"{SERVER}{path}", data=data, headers={"Content-Type": "application/json"}
    )
    return json.loads(urllib.request.urlopen(req, timeout=60).read())


def get(path):
    return json.loads(urllib.request.urlopen(f"{SERVER}{path}", timeout=60).read())


def run_one(subject, style, ckpt, seed, steps, cfg, size, out_dir, label):
    prompt_id = post("/prompt", {"prompt": workflow(subject, style, ckpt, seed, steps, cfg, size)})[
        "prompt_id"
    ]

    started = time.time()
    while True:
        history = get(f"/history/{prompt_id}")
        if prompt_id in history:
            break
        if time.time() - started > 600:
            raise TimeoutError(f"{subject}: no result after 10 minutes")
        time.sleep(1.5)

    entry = history[prompt_id]
    if entry.get("status", {}).get("status_str") == "error":
        raise RuntimeError(json.dumps(entry["status"])[:600])

    saved = []
    for node in entry["outputs"].values():
        for image in node.get("images", []):
            raw = urllib.request.urlopen(
                f"{SERVER}/view?filename={image['filename']}"
                f"&subfolder={image.get('subfolder', '')}&type={image['type']}",
                timeout=60,
            ).read()
            path = out_dir / f"{label}.png"
            path.write_bytes(raw)
            saved.append(path)

    return saved, time.time() - started


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--subjects", required=True, help="comma-separated")
    ap.add_argument("--styles", default="flat", help="comma-separated; one image per style")
    ap.add_argument("--ckpt", default="dreamshaperXL_turbo.safetensors")
    ap.add_argument("--out", default="scratch/art")
    ap.add_argument("--seed", type=int, default=7, help="base seed; varied per subject")
    ap.add_argument(
        "--variants", type=int, default=3, help="images per subject to choose between"
    )
    ap.add_argument("--steps", type=int, default=8)
    ap.add_argument("--cfg", type=float, default=2.0)
    ap.add_argument("--size", type=int, default=1024)
    args = ap.parse_args()

    out_dir = pathlib.Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    subjects = [s.strip() for s in args.subjects.split(",") if s.strip()]
    styles = [s.strip() for s in args.styles.split(",") if s.strip()]

    for style in styles:
        for subject in subjects:
            for v in range(args.variants):
                seed = subject_seed(subject, args.seed, v)
                label = f"{style}_{subject.replace(' ', '_')[:40]}"
                if args.variants > 1:
                    label += f"_v{v}"
                saved, secs = run_one(
                    subject, style, args.ckpt, seed, args.steps, args.cfg, args.size,
                    out_dir, label,
                )
                print(f"{secs:5.1f}s  seed {seed:<12} {label}")


if __name__ == "__main__":
    main()
