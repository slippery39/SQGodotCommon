"""
Put one generated image into the game, in the shape the game expects (style D, KinVisualDesign.md).

    python tools/install_art.py sprite  gen.png  "Old Mire"  [--flip] [--grass]
    python tools/install_art.py card    gen.png  "Battle Cry"
    python tools/install_art.py backdrop gen.png town
    python tools/install_art.py building gen.png hospital

- sprite:   cut out (tools/pixelate.py `matte`), 400px tall, into Art/sprites/<name>.png.
            **Every sprite FILE faces RIGHT** - the game mirrors foes. `--flip` if the model drew it
            facing left (roughly one in three do). `--grass` drops green ground at the feet, only
            for a creature whose legs are not green.
- card:     672x416 (2x the art window), into Art/cards/<name>.png.
- backdrop: as-is, into Art/backdrops/<name>.png.
- building: cut out like a sprite, 400px tall, into Art/buildings/<kind>.png (hospital, shop, pen,
            hall, gate — `BuildingKind`, lowercased), for the town map.

The name is the content name; the file name is derived the way `KinArt` looks it up
("Old Mire" -> old_mire). Then import: `godot-mono --path SQGodotCommon --headless --import`.
"""

import argparse
import pathlib
import sys

from PIL import Image

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from pixelate import matte  # noqa: E402

ART = pathlib.Path(__file__).resolve().parent.parent / "SQGodotCommon" / "KinGame" / "Art"


def file_name(name):
    """Mirror of KinArt.FileName: lowercase, spaces to underscores, apostrophes dropped."""
    return name.strip().lower().replace(" ", "_").replace("'", "")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("kind", choices=["sprite", "card", "backdrop", "building"])
    ap.add_argument("src")
    ap.add_argument("name")
    ap.add_argument("--flip", action="store_true")
    ap.add_argument("--grass", action="store_true")
    ap.add_argument("--art", default=str(ART), help="Art folder (for testing)")
    a = ap.parse_args()

    im = Image.open(a.src)
    if a.kind in ("sprite", "building"):
        im = matte(im, grass=a.grass)
        if a.flip:
            im = im.transpose(Image.FLIP_LEFT_RIGHT)
        im = im.resize((round(im.width * 400 / im.height), 400), Image.LANCZOS)
        folder = "sprites" if a.kind == "sprite" else "buildings"
    elif a.kind == "card":
        im = im.convert("RGB").resize((672, 416), Image.LANCZOS)
        folder = "cards"
    else:
        im = im.convert("RGB")
        folder = "backdrops"

    out = pathlib.Path(a.art) / folder / f"{file_name(a.name)}.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    im.save(out)
    print(f"{a.kind} -> {out}  {im.size}")


if __name__ == "__main__":
    main()
