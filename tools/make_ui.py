"""
Generate KIN's style-D UI textures into SQGodotCommon/KinGame/Art/ui/ — the "not flat" layer:
gradient fills, a bevel highlight, a dark inner line and a gold (or red / bone) rim, so a panel
reads as a MATERIAL like the mockup's, not as a Godot colour box.

Drawn at 4x and downsampled (anti-aliasing for free). Deterministic: re-run after changing a
colour here, then `godot-mono --path SQGodotCommon --headless --import`.

    python tools/make_ui.py

Used by `KinUiKit` as 9-slices (StyleBoxTexture); margins there must match the corner sizes here.
"""

import pathlib

from PIL import Image, ImageDraw, ImageFilter

OUT = pathlib.Path(__file__).resolve().parent.parent / "SQGodotCommon" / "KinGame" / "Art" / "ui"
S = 4  # supersampling

GOLD = (227, 178, 60)
GOLD_HI = (255, 226, 140)
GOLD_LO = (150, 104, 30)
RED = (199, 62, 58)
RED_HI = (240, 120, 110)
RED_LO = (120, 30, 28)
BONE = (200, 210, 220)
BONE_HI = (240, 244, 248)
BONE_LO = (110, 120, 132)
DARK = (8, 12, 18)


def vgrad(w, h, top, bottom, alpha=255):
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for y in range(h):
        t = y / max(1, h - 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (alpha,)
        for x in range(w):
            px[x, y] = c
    return img


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


def compose(w, h, shape, fill_top, fill_bottom, rim, rim_w, alpha=240, gloss=True):
    """Rim (bevelled metal) -> dark inner line -> gradient fill -> top gloss, all clipped to `shape`."""
    W, H = w * S, h * S
    outer = shape_mask(W, H, lambda d, w_, h_: shape(d, 0, 0, w_, h_))
    rw = rim_w * S
    inner_line = shape_mask(W, H, lambda d, w_, h_: shape(d, rw, rw, w_ - rw, h_ - rw))
    lw = rw + 2 * S
    inner = shape_mask(W, H, lambda d, w_, h_: shape(d, lw, lw, w_ - lw, h_ - lw))

    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    img.paste(rim_gradient(W, H, *rim), (0, 0), outer)
    img.paste(Image.new("RGBA", (W, H), DARK + (255,)), (0, 0), inner_line)
    img.paste(vgrad(W, H, fill_top, fill_bottom, alpha), (0, 0), inner)
    if gloss:
        # A soft light across the top third of the fill: the "lit from above" that flat boxes lack.
        g = Image.new("L", (W, H), 0)
        ImageDraw.Draw(g).rectangle((0, 0, W, int(H * 0.42)), fill=34)
        g = Image.composite(g, Image.new("L", (W, H), 0), inner).filter(ImageFilter.GaussianBlur(3 * S))
        img = Image.alpha_composite(img, Image.merge("RGBA", (g.point(lambda _: 255),) * 3 + (g,)))
    return img.resize((w, h), Image.LANCZOS)


def rounded(r):
    return lambda d, x0, y0, x1, y1: d.rounded_rectangle((x0, y0, x1 - 1, y1 - 1), radius=max(1, r * S - x0 // 2), fill=255)


def hexagon(end):
    def draw(d, x0, y0, x1, y1):
        e = end * S - x0 * 0.4
        mid = (y0 + y1) / 2
        d.polygon([(x0, mid), (x0 + e, y0), (x1 - e, y0), (x1, mid), (x1 - e, y1), (x0 + e, y1)], fill=255)

    return draw


def circle():
    return lambda d, x0, y0, x1, y1: d.ellipse((x0, y0, x1 - 1, y1 - 1), fill=255)


RIMS = {"gold": (GOLD_HI, GOLD, GOLD_LO), "red": (RED_HI, RED, RED_LO), "bone": (BONE_HI, BONE, BONE_LO)}
SLATE = ((52, 72, 96), (20, 30, 43))


def orb(size=200):
    """The energy orb: a glossy blue sphere in a gold ring with four studs."""
    W = size * S
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = W / 2
    # studs (N/E/S/W), under the ring
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        px, py = c + dx * c * 0.9, c + dy * c * 0.9
        r = W * 0.07
        d.polygon([(px - r * dy, py + r * dx), (px + dx * r * 1.6, py + dy * r * 1.6), (px + r * dy, py - r * dx),
                   (px - dx * r * 0.6, py - dy * r * 0.6)], fill=GOLD + (255,), outline=GOLD_LO + (255,))
    ring = Image.new("L", (W, W), 0)
    ImageDraw.Draw(ring).ellipse((W * 0.08, W * 0.08, W * 0.92, W * 0.92), fill=255)
    img.paste(rim_gradient(W, W, GOLD_HI, GOLD, GOLD_LO), (0, 0), ring)
    dark = Image.new("L", (W, W), 0)
    ImageDraw.Draw(dark).ellipse((W * 0.14, W * 0.14, W * 0.86, W * 0.86), fill=255)
    img.paste(Image.new("RGBA", (W, W), DARK + (255,)), (0, 0), dark)
    # the sphere: radial gradient, light upper-left
    sphere = Image.new("RGBA", (W, W))
    sp = sphere.load()
    lx, ly = W * 0.40, W * 0.36
    for y in range(0, W):
        for x in range(0, W):
            t = min(1.0, ((x - lx) ** 2 + (y - ly) ** 2) ** 0.5 / (W * 0.46))
            a, b = (96, 170, 255), (14, 44, 110)
            sp[x, y] = tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3)) + (255,)
    m = Image.new("L", (W, W), 0)
    ImageDraw.Draw(m).ellipse((W * 0.16, W * 0.16, W * 0.84, W * 0.84), fill=255)
    img.paste(sphere, (0, 0), m)
    # gloss
    g = Image.new("L", (W, W), 0)
    ImageDraw.Draw(g).ellipse((W * 0.28, W * 0.20, W * 0.66, W * 0.42), fill=70)
    g = g.filter(ImageFilter.GaussianBlur(4 * S))
    img = Image.alpha_composite(img, Image.merge("RGBA", (g.point(lambda _: 255),) * 3 + (g,)))
    return img.resize((size, size), Image.LANCZOS)


def scrim(w=256, h=160):
    """The BACKSHADOW behind a creature's words: a dark ellipse blurred until it has no edge."""
    W, H = w * S, h * S
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).ellipse((W * 0.14, H * 0.2, W * 0.86, H * 0.8), fill=185)
    m = m.filter(ImageFilter.GaussianBlur(14 * S))
    img = Image.merge("RGBA", (Image.new("L", (W, H), 4), Image.new("L", (W, H), 7), Image.new("L", (W, H), 12), m))
    return img.resize((w, h), Image.LANCZOS)


def contact(w=160, h=40):
    """A creature's CONTACT shadow: a WHITE soft ellipse, tinted in the game (black, or gold when lit)."""
    W, H = w * S, h * S
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).ellipse((W * 0.1, H * 0.22, W * 0.9, H * 0.78), fill=255)
    m = m.filter(ImageFilter.GaussianBlur(5 * S))
    img = Image.merge("RGBA", (Image.new("L", (W, H), 255),) * 3 + (m,))
    return img.resize((w, h), Image.LANCZOS)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    made = []

    def save(img, name):
        img.save(OUT / f"{name}.png")
        made.append(name)

    # Plates (9-slice, corners 16): panels, pills, small buttons.
    for rim in ("gold", "red", "bone"):
        save(compose(64, 64, rounded(12), *SLATE, RIMS[rim], 3), f"plate_{rim}")
    save(compose(64, 64, rounded(12), (70, 94, 122), (30, 42, 58), RIMS["gold"], 3), "plate_gold_hover")

    # The big hex button (END TURN): ends 44 wide, stretched in the middle only.
    for name, top, bottom in (("button_hex", (46, 72, 108), (16, 28, 52)), ("button_hex_hover", (70, 102, 146), (26, 42, 74))):
        save(compose(160, 96, hexagon(40), top, bottom, RIMS["gold"], 4), name)

    # HP bar: a dark trough and a pale fill that the game tints per creature (StyleBoxTexture.ModulateColor).
    save(compose(64, 28, hexagon(14), (10, 16, 24), (26, 36, 50), ((30, 36, 44), (16, 20, 26), (6, 8, 10)), 2, gloss=False), "bar_trough")
    save(compose(64, 28, hexagon(14), (255, 255, 255), (170, 170, 170), ((255, 255, 255), (230, 230, 230), (190, 190, 190)), 1), "bar_fill")

    # Step discs.
    for rim in ("gold", "red", "bone"):
        save(compose(40, 40, circle(), *SLATE, RIMS[rim], 4), f"disc_{rim}")
    # Route places: the same discs, big enough to hold a creature (KinRouteMap).
    for rim in ("gold", "red", "bone"):
        save(compose(128, 128, circle(), *SLATE, RIMS[rim], 8), f"node_{rim}")

    save(orb(), "orb")
    save(scrim(), "scrim")
    save(contact(), "contact")
    print("made", ", ".join(made), "->", OUT)


if __name__ == "__main__":
    main()
