"""
Generate the MTG front end's Dark Arcane UI textures into SQGodotCommon/MtgGame/Assets/UI/ — the
"not flat" layer: a bevelled brass rim, a dark inner line, and a grained gradient fill (leather,
oxblood, parchment), so a panel reads as a MATERIAL like the mockups, not as a Godot colour box.
Palette from MtgVisualDesign.md section 6. Ported from KIN's make_ui.py (kin-pivot branch).

Drawn at 4x and downsampled (anti-aliasing for free). Deterministic: re-run after changing a
colour here, then `godot-mono --path SQGodotCommon --headless --import`.

    python tools/make_mtg_ui.py

Used by `MtgUiStyles` as 9-slices (StyleBoxTexture); its margins must match CORNER here.
"""

import pathlib
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

OUT = pathlib.Path(__file__).resolve().parent.parent / "SQGodotCommon" / "MtgGame" / "Assets" / "UI"
S = 4  # supersampling
CORNER = 16  # 9-slice margin in MtgUiStyles

BRASS = ((236, 200, 130), (148, 101, 54), (70, 44, 20))
BRASS_HOT = ((255, 232, 170), (196, 146, 80), (96, 62, 28))
IRON = ((120, 112, 104), (64, 58, 52), (28, 25, 22))
DARK = (14, 8, 4)

LEATHER = ((62, 42, 28), (36, 24, 16))
LEATHER_HOT = ((84, 58, 38), (50, 34, 22))
OXBLOOD = ((128, 30, 22), (66, 12, 9))
OXBLOOD_HOT = ((160, 44, 30), (90, 18, 12))
ASH = ((58, 52, 46), (34, 30, 26))
PARCHMENT = ((214, 186, 140), (156, 124, 85))


def vgrad(w, h, top, bottom, alpha=255):
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for y in range(h):
        t = y / max(1, h - 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (alpha,)
        for x in range(w):
            px[x, y] = c
    return img


def grain(img, strength):
    """Mottle + fine grain: what makes a fill read as leather or paper instead of a gradient."""
    w, h = img.size
    random.seed(7)  # deterministic
    fine = Image.effect_noise((w, h), 40)
    mottle = Image.effect_noise((w // 8 + 1, h // 8 + 1), 60).resize((w, h), Image.BICUBIC).filter(ImageFilter.GaussianBlur(3 * S))
    noise = ImageChops.blend(fine, mottle, 0.6)
    # Overlay around mid-grey: >128 lightens, <128 darkens.
    rgb = img.convert("RGB")
    light = ImageChops.screen(rgb, Image.merge("RGB", (noise.point(lambda v: max(0, v - 128) * 2),) * 3))
    dark = ImageChops.multiply(light, Image.merge("RGB", (noise.point(lambda v: 255 - max(0, 128 - v) * 2),) * 3))
    out = ImageChops.blend(rgb, dark, strength).convert("RGBA")
    out.putalpha(img.getchannel("A"))
    return out


def rim_gradient(w, h, hi, mid, lo):
    """A metal rim: light at the top, dark at the bottom — the bevel."""
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for y in range(h):
        t = y / max(1, h - 1)
        a, b, u = (hi, mid, t * 2) if t < 0.5 else (mid, lo, (t - 0.5) * 2)
        c = tuple(int(a[i] + (b[i] - a[i]) * u) for i in range(3)) + (255,)
        for x in range(w):
            px[x, y] = c
    return img


def shape_mask(w, h, draw_fn):
    m = Image.new("L", (w, h), 0)
    draw_fn(ImageDraw.Draw(m), w, h)
    return m


def compose(w, h, shape, fill, rim, rim_w, grain_strength=0.35, gloss=True):
    """Rim (bevelled metal) -> dark inner line -> grained gradient fill -> top gloss, clipped to `shape`."""
    W, H = w * S, h * S
    outer = shape_mask(W, H, lambda d, w_, h_: shape(d, 0, 0, w_, h_))
    rw = rim_w * S
    inner_line = shape_mask(W, H, lambda d, w_, h_: shape(d, rw, rw, w_ - rw, h_ - rw))
    lw = rw + 2 * S
    inner = shape_mask(W, H, lambda d, w_, h_: shape(d, lw, lw, w_ - lw, h_ - lw))

    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    img.paste(rim_gradient(W, H, *rim), (0, 0), outer)
    img.paste(Image.new("RGBA", (W, H), DARK + (255,)), (0, 0), inner_line)
    img.paste(grain(vgrad(W, H, *fill), grain_strength), (0, 0), inner)
    if gloss:
        g = Image.new("L", (W, H), 0)
        ImageDraw.Draw(g).rectangle((0, 0, W, int(H * 0.42)), fill=26)
        g = Image.composite(g, Image.new("L", (W, H), 0), inner).filter(ImageFilter.GaussianBlur(3 * S))
        img = Image.alpha_composite(img, Image.merge("RGBA", (g.point(lambda _: 255),) * 3 + (g,)))
    return img.resize((w, h), Image.LANCZOS)


def rounded(r):
    return lambda d, x0, y0, x1, y1: d.rounded_rectangle((x0, y0, x1 - 1, y1 - 1), radius=max(1, r * S - x0 // 2), fill=255)


def pointed(end):
    """The END TURN / PICK shape: a bar whose ends come to a point."""

    def draw(d, x0, y0, x1, y1):
        e = end * S - x0 * 0.4
        mid = (y0 + y1) / 2
        d.polygon([(x0, mid), (x0 + e, y0), (x1 - e, y0), (x1, mid), (x1 - e, y1), (x0 + e, y1)], fill=255)

    return draw


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    made = []

    def save(img, name):
        img.save(OUT / f"{name}.png")
        made.append(name)

    # Plates (9-slice, corners CORNER): panels, small buttons, chips.
    save(compose(64, 64, rounded(10), LEATHER, BRASS, 3), "plate")
    save(compose(64, 64, rounded(10), LEATHER_HOT, BRASS_HOT, 3), "plate_hover")
    save(compose(64, 64, rounded(10), ASH, IRON, 3), "plate_disabled")
    save(compose(64, 64, rounded(8), PARCHMENT, BRASS, 3, grain_strength=0.25), "banner")

    # The primary button (END TURN, PICK): pointed ends, stretched in the middle only.
    for name, fill, rim in (
        ("button", OXBLOOD, BRASS),
        ("button_hover", OXBLOOD_HOT, BRASS_HOT),
        ("button_disabled", ASH, IRON),
    ):
        save(compose(160, 64, pointed(24), fill, rim, 4), name)

    print("made", ", ".join(made), "->", OUT)


if __name__ == "__main__":
    main()
