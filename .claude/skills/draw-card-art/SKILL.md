---
name: draw-card-art
description: Draw the art for a DOOMJAM/ENDLING card, enemy or opponent as a flat SVG, import it, and LOOK at it before believing it. Use when a new card has no art, when art "falls back to the generated figure", when a subject reads wrong or unrecognisably, or when adding any file to SQGodotCommon/KinGame/Art/. Covers the filename rule that silently resolves a card to the wrong drawing, and the render-and-look loop that is the only way to find out what a shape actually looks like.
---

# Drawing card art

Art is **authored flat SVG**, 256x256, on the five-colour palette. The style rules are `KinUI.md`
under "Art" — read them; this skill is the order of operations and the checks, not a second copy.

**The central fact: you cannot tell what an SVG looks like by reading it.** A cleaver drawn with a
curved blade rendered as a frying pan, and nothing about the markup said so. Every piece gets
rendered and looked at, twice — at 256 and at 40.

## 1. Get the filename right, or the card silently has no art

`KinArt.FileName` lowercases, replaces spaces with `_`, strips apostrophes, **and cuts the name at
the first comma, em dash or HYPHEN**:

```csharp
subject.Split(',')[0].Split('—')[0].Split('-')[0].Trim().ToLowerInvariant()
    .Replace(" ", "_").Replace("'", "")
```

| Card name | Looks for |
|---|---|
| `Pyre Keeper` | `pyre_keeper.svg` |
| `Butcher's Bill` | `butchers_bill.svg` |
| `Twice-Buried` | **`twice.svg`** — everything after the hyphen is gone |

A miss is not an error. `Drawing()` returns null and the card falls back to a generated silhouette,
which looks deliberate. **Prefer renaming the card to avoid a hyphen** over naming a file `twice.svg`
— the file should be recognisable in the folder.

## 2. Draw it

Start from a neighbour rather than from nothing — `gravedigger.svg` for a figure, `blood_price.svg`
for a rite, `feral_pack.svg` for anything built of repeated parts. The conventions that matter most:

- **Three tones, no more:** `#0C131B` silhouette, `#2B4054` or `#233444` interior form, one accent —
  `#C73E3A` red, or `#E3B23C` gold for something that pays the player.
- **Draw order is composition.** Anything the figure HOLDS comes after the figure in the file.
- **Organic subjects are built from named parts and `<use>`d.** One hand-written polygon renders as
  a blob. Mechanical subjects survive a single path.
- **First line of the file is a comment naming the card and what it does.** Every existing piece has
  one and it is how you find the right file later.

## 3. Import, or the file does not exist

```
godot-mono --headless --path SQGodotCommon --import
```

New files are invisible to the game until this has run, and it must run again after every edit to
an SVG before the next render.

## 4. Render it and LOOK at it

`SQGodotCommon/art_check.gd` renders subjects to PNG on the board's navy, at 256 and at 40:

```
ART_OUT=<a scratch dir> ART_NAMES=pyre_keeper,gallows_feast \
  godot-mono --headless --path SQGodotCommon --script art_check.gd
```

Then open both PNGs. **Render a piece you know is good alongside the new one** (`gravedigger`) —
it calibrates the eye and catches a whole-batch mistake.

- **256 answers "is it the right thing".** The frying-pan cleaver failed here.
- **40 answers "does it survive the lane".** That is the lane-figure size. A shape that survives 40
  always survives 256; the reverse is not true, so never skip it.

Fix, re-import, re-render. Two rounds is normal.

## 5. Credit and cleanliness

- Original work needs no credit row. **An icon taken from game-icons.net needs a row in the ROOT
  `CREDITS.md`** — the CC BY licence is per-icon. `KinGame/Art/CREDITS.md` is only a pointer.
- Commit the `.svg` AND its generated `.svg.import`, the way every existing piece is committed.
- Delete nothing else from `Art/`; `KinArt` resolves by name and a rename is a silent downgrade to
  the generated figure.

## Done when

The card's own name resolves to the file (check the exact `FileName` transform), both PNGs have been
looked at, and the 40px version is still recognisable as the thing it is meant to be.
