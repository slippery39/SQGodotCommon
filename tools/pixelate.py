"""Usage: python tools/pixelate.py in.png out.png [height=96] [palette-reference.png]

Turn a generated sprite on a light plain background into real pixel art:
matte the background, downscale to a fixed height, quantize to a small palette, add a 1px dark
outline, upscale with nearest. The model draws; the grid comes from here.

Why: NO generator makes real pixel art. ChatGPT's "pixel" mockup, zoomed, has mixed pixel sizes
off any grid; SDXL ignores the ask. A pixel style is only consistent if this pass makes the grid.
ponytail: the matte keeps pale regions ENCLOSED by the body (a gap between a spear and an arm);
cull or prompt "solid white background, no gaps" until it matters."""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

def matte(im, edge=0.06, pale=True, grass=False):
    """Background = SMOOTH pixels connected to the border (a flat or gradient ground of any
    colour), or pale ones (a white ground). The creature's dark outline is a hard edge, so the
    flood stops there. Holes inside the body are kept (filled); only the largest blob survives.
     also drops green ground connected to the bottom — ONLY for a creature whose legs
    are not green (measured on Bramble, 2026-09-26). A full-width-row trim was tried and cut a wide
    tortoise's own body: do not bring it back."""
    a = np.asarray(im.convert("RGB")).astype(float) / 255
    lum = a.mean(2)
    lum_s = ndimage.gaussian_filter(lum, 1.2)
    g = np.hypot(ndimage.sobel(lum_s, 0), ndimage.sobel(lum_s, 1))
    smooth = g < edge
    cand = smooth
    if pale:
        mx, mn = a.max(2), a.min(2)
        cand |= ((mx - mn) / np.maximum(mx, 1e-6) < 0.16) & (mx > 0.8)
    lab, _ = ndimage.label(cand)
    edges = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    bg = np.isin(lab, list(edges))
    fg = ndimage.binary_fill_holes(~bg)
    fg = ndimage.binary_opening(fg, iterations=2)
    # keep the largest blob only: stray specks of ground are not the creature
    lab2, n = ndimage.label(fg)
    if n > 1:
        sizes = ndimage.sum(fg, lab2, range(1, n + 1))
        fg = lab2 == (1 + int(np.argmax(sizes)))
    fg = ndimage.binary_erosion(fg, iterations=1)  # eat the halo of the old ground
    if grass:
        # Ground grass under a creature with NON-green legs: green pixels in the lowest quarter,
        # connected to the bottom of the cut-out, are ground, not creature.
        rows = np.where(fg.any(1))[0]
        top = rows[0] + int((rows[-1] - rows[0]) * 0.72)
        r, gg, bb = a[..., 0], a[..., 1], a[..., 2]
        green = fg & (gg > r * 1.08) & (gg > bb * 1.08)
        green[:top] = False
        lab3, _ = ndimage.label(ndimage.binary_dilation(green, iterations=2) & fg)
        base = set(np.unique(lab3[rows[-1] - 3 : rows[-1] + 1])) - {0}
        fg &= ~np.isin(lab3, list(base))
        fg = ndimage.binary_opening(fg, iterations=2)
        lab2, n = ndimage.label(fg)
        if n > 1:
            sizes = ndimage.sum(fg, lab2, range(1, n + 1))
            fg = lab2 == (1 + int(np.argmax(sizes)))
    mask = Image.fromarray((fg * 255).astype("uint8"))
    out = im.convert("RGBA")
    out.putalpha(mask)
    return out.crop(mask.getbbox())


def palette_of(ref, colours=32):
    """ONE palette for the whole game, taken from a reference image (the chosen mockup). Every
    sprite and backdrop quantized to it shares its colours, which is what makes a set cohere."""
    return ref.convert("RGB").quantize(colours, method=Image.MEDIANCUT, dither=Image.NONE)


def pixelate(im, height=96, colours=24, scale=3, palette=None):
    w = max(1, round(im.width * height / im.height))
    small = im.resize((w, height), Image.LANCZOS)
    alpha = small.getchannel("A").point(lambda a: 255 if a > 128 else 0)
    rgb = small.convert("RGB")
    rgb = (rgb.quantize(palette=palette, dither=Image.NONE) if palette
           else rgb.quantize(colours, method=Image.MEDIANCUT, dither=Image.NONE)).convert("RGB")
    out = Image.new("RGBA", (w + 2, height + 2), (0,0,0,0))
    body = rgb.convert("RGBA"); body.putalpha(alpha)
    # 1px dark outline: the alpha grown by one pixel, filled dark, under the body
    grown = Image.new("L", out.size, 0); grown.paste(alpha, (1,1))
    grown = grown.filter(ImageFilter.MaxFilter(3))
    outline = Image.new("RGBA", out.size, (20,16,28,255)); outline.putalpha(grown)
    out = Image.alpha_composite(out, outline)
    out.alpha_composite(body, (1,1))
    return out.resize((out.width*scale, out.height*scale), Image.NEAREST)

if __name__ == "__main__":
    # python tools/pixelate.py in.png out.png [height] [palette-reference.png]
    src, dst = sys.argv[1], sys.argv[2]
    h = int(sys.argv[3]) if len(sys.argv) > 3 else 96
    pal = palette_of(Image.open(sys.argv[4])) if len(sys.argv) > 4 else None
    pixelate(matte(Image.open(src)), height=h, palette=pal).save(dst)
