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


def circle():
    return lambda d, x0, y0, x1, y1: d.ellipse((x0, y0, x1 - 1, y1 - 1), fill=255)


def gem(size=32):
    """A WHITE glossy gem, tinted per mana colour in the game (Modulate)."""
    W = size * S
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ring = shape_mask(W, W, lambda d, w, h: d.ellipse((0, 0, w - 1, h - 1), fill=255))
    img.paste(Image.new("RGBA", (W, W), (40, 36, 32, 255)), (0, 0), ring)
    sphere = Image.new("RGBA", (W, W))
    sp = sphere.load()
    lx, ly = W * 0.40, W * 0.36
    for y in range(W):
        for x in range(W):
            t = min(1.0, ((x - lx) ** 2 + (y - ly) ** 2) ** 0.5 / (W * 0.55))
            v = int(255 - 120 * t)
            sp[x, y] = (v, v, v, 255)
    inner = shape_mask(W, W, lambda d, w, h: d.ellipse((w * 0.1, h * 0.1, w * 0.9, h * 0.9), fill=255))
    img.paste(sphere, (0, 0), inner)
    return img.resize((size, size), Image.LANCZOS)


# Card parts — the SAME pixel sizes as Common/Cards/2D/Card2D/Images/Parts, so the shared scene's
# geometry holds. Frame and name plate are near-WHITE: the game multiplies a colour onto them
# (MtgCardTheme), and a dark base turned every colour brown (MtgVisualDesign.md section 2).
CARD_WHITE = ((246, 242, 236), (214, 208, 200))
CREAM = ((250, 244, 230), (226, 216, 196))
RULES = ((238, 228, 206), (216, 202, 174))
EDGE = ((70, 64, 58), (34, 30, 28), (10, 9, 8))


def arch(d, x0, y0, x1, y1):
    """A board token's silhouette: a round-topped arch over a square-ish base."""
    w, h = x1 - x0, y1 - y0
    d.ellipse((x0, y0, x1 - 1, y0 + w * 0.9), fill=255)
    d.rounded_rectangle((x0, y0 + w * 0.45, x1 - 1, y1 - 1), radius=w * 0.08, fill=255, corners=(False, False, True, True))


TOKEN = (150, 190)
TOKEN_RIM = 7


def token_parts():
    """Mask (clips the art), rim (light bevel, tinted by card colour in the game), glow (states)."""
    w, h = TOKEN
    W, H, rw = w * S, h * S, TOKEN_RIM * S
    outer = shape_mask(W, H, lambda d, w_, h_: arch(d, 0, 0, w_, h_))
    inner = shape_mask(W, H, lambda d, w_, h_: arch(d, rw, rw, w_ - rw, h_ - rw))
    line = shape_mask(W, H, lambda d, w_, h_: arch(d, rw - 2 * S, rw - 2 * S, w_ - rw + 2 * S, h_ - rw + 2 * S))

    mask = Image.merge("RGBA", (Image.new("L", (W, H), 255),) * 3 + (inner,))

    rim = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    rim.paste(rim_gradient(W, H, (255, 255, 255), (205, 200, 192), (120, 114, 106)), (0, 0), ImageChops.subtract(outer, line))
    rim.paste(Image.new("RGBA", (W, H), DARK + (255,)), (0, 0), ImageChops.subtract(line, inner))

    pad = 24 * S
    g = Image.new("L", (W + 2 * pad, H + 2 * pad), 0)
    g.paste(outer, (pad, pad))
    g = g.filter(ImageFilter.GaussianBlur(9 * S)).point(lambda v: min(255, v * 2))
    glow = Image.merge("RGBA", (Image.new("L", g.size, 255),) * 3 + (g,))

    shrink = lambda im, size: im.resize(size, Image.LANCZOS)
    return shrink(mask, TOKEN), shrink(rim, TOKEN), shrink(glow, (w + 48, h + 48))


def tick(img, colour=(244, 226, 155)):
    """A gold tick over `img` (the palette's gold text), drawn at S x and downsampled like the rest."""
    w, h = img.size
    layer = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    points = [(x * w * S, y * h * S) for x, y in ((0.26, 0.52), (0.43, 0.70), (0.76, 0.30))]
    ImageDraw.Draw(layer).line(points, fill=colour + (255,), width=int(0.14 * w * S), joint="curve")
    return Image.alpha_composite(img, layer.resize((w, h), Image.LANCZOS))


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

    save(compose(312, 445, rounded(14), CARD_WHITE, EDGE, 4, grain_strength=0.3, gloss=False), "card_frame")
    save(compose(279, 53, rounded(8), CREAM, BRASS, 2, grain_strength=0.2), "card_name")
    save(compose(279, 158, rounded(6), RULES, EDGE, 2, grain_strength=0.25, gloss=False), "card_rules")
    save(compose(64, 64, circle(), LEATHER, BRASS, 4), "card_cost")
    save(compose(96, 56, rounded(14), LEATHER, BRASS, 3), "card_pt")
    save(gem(), "gem")

    # Hero portrait: a circular mask (clips the painting), a thick brass ring, the oxblood life seal.
    P = 256 * S
    save(Image.merge("RGBA", (Image.new("L", (P, P), 255),) * 3
                     + (shape_mask(P, P, lambda d, w, h: d.ellipse((8 * S, 8 * S, w - 8 * S, h - 8 * S), fill=255)),)
                     ).resize((256, 256), Image.LANCZOS), "portrait_mask")
    ring = ImageChops.subtract(shape_mask(P, P, lambda d, w, h: d.ellipse((0, 0, w - 1, h - 1), fill=255)),
                               shape_mask(P, P, lambda d, w, h: d.ellipse((14 * S, 14 * S, w - 14 * S, h - 14 * S), fill=255)))
    portrait = Image.new("RGBA", (P, P), (0, 0, 0, 0))
    portrait.paste(rim_gradient(P, P, *BRASS), (0, 0), ring)
    save(portrait.resize((256, 256), Image.LANCZOS), "portrait_ring")
    save(compose(72, 72, circle(), OXBLOOD, BRASS, 5), "seal")

    # Card back (library, opponent's hand): leather in brass, a gold lozenge in the middle.
    back = compose(120, 168, rounded(10), LEATHER, BRASS, 4)
    big = back.resize((120 * S, 168 * S), Image.LANCZOS)
    d = ImageDraw.Draw(big)
    cx, cy, r = 60 * S, 84 * S, 26 * S
    d.polygon([(cx, cy - r * 1.4), (cx + r, cy), (cx, cy + r * 1.4), (cx - r, cy)], outline=BRASS[0] + (255,), width=3 * S)
    d.polygon([(cx, cy - r * 0.7), (cx + r * 0.5, cy), (cx, cy + r * 0.7), (cx - r * 0.5, cy)], fill=BRASS[1] + (255,))
    save(big.resize((120, 168), Image.LANCZOS), "card_back")

    # Generic mana crystal: a WHITE faceted lozenge, tinted amber (available) or dark (spent).
    W, H = 28 * S, 40 * S
    crystal = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(crystal)
    top, mid, bot, l, r, cx = (0, H * 0.42, H - 1, 0, W - 1, W / 2)
    d.polygon([(cx, top), (r, mid), (cx, bot), (l, mid)], fill=(40, 36, 32, 255))
    inset = 2.5 * S
    d.polygon([(cx, top + inset), (cx, bot - inset), (l + inset, mid)], fill=(255, 255, 255, 255))
    d.polygon([(cx, top + inset), (r - inset, mid), (cx, bot - inset)], fill=(196, 196, 196, 255))
    d.polygon([(cx, top + inset), (cx + W * 0.18, mid * 0.8), (cx, mid), (cx - W * 0.18, mid * 0.8)], fill=(255, 255, 255, 255))
    save(crystal.resize((28, 40), Image.LANCZOS), "crystal")

    mask, rim, glow = token_parts()
    save(mask, "token_mask")
    save(rim, "token_rim")
    save(glow, "token_glow")

    # Checkbox icons, in the project theme (TBL-2.4): a small leather plate, and the same with a
    # gold tick. Godot's defaults were a faint outline that vanished on the dark backdrop.
    check = compose(28, 28, rounded(5), LEATHER, BRASS, 2, grain_strength=0.2, gloss=False)
    save(check, "check_off")
    save(tick(check), "check_on")

    print("made", ", ".join(made), "->", OUT)


if __name__ == "__main__":
    main()
