---
name: add-icon
description: Add a UI symbol (icon) to KIN from game-icons.net — find a name that exists, strip its background, recolour it, import it, credit it, and give it a meaning players can hover. Use when a status, keyword, stat or move needs a symbol; when asked to "add an icon", "use a symbol instead of text", or "iconify" something; or when a chip, badge or card shows a word where a symbol should be. KIN only (SQGodotCommon/KinGame).
---

# Adding a symbol to KIN

**The default (Shayne, 2026-10-01): when unsure, start with a SYMBOL and a number, words in a hover**
— not a law, but text must earn its place (`KinUI.md`, the declutter pass). Our symbols come from
**game-icons.net** (CC BY 3.0), as bone silhouettes tinted in code.

## 1. Find a name that EXISTS

Names are guessed, and most guesses 404 (`lorc/flame`, `lorc/lightning-bolt`, `delapouite/card-draw`
all do). Probe a batch at once, raw from the GitHub mirror:

```bash
for n in lorc/small-fire carl-olsen/flame lorc/thorny-vine delapouite/biceps; do
  echo "$(curl -s -o /dev/null -w '%{http_code}' https://raw.githubusercontent.com/game-icons/icons/master/$n.svg) $n"
done
```

Authors used so far: `lorc`, `delapouite`. Browse https://game-icons.net to find candidates by eye.

## 2. Fetch, strip, recolour — into `SQGodotCommon/KinGame/Art/icons/`

Every file has a **black 512×512 square behind the glyph** (`<path d="M0 0h512v512H0z"/>`) and a
white glyph. Remove the square; make the glyph bone `#E8EEF2` (tinted in code with `Modulate`, or
`[img color=…]` in rich text — a light glyph takes any tint):

```python
import re, urllib.request
svg = urllib.request.urlopen(f"https://raw.githubusercontent.com/game-icons/icons/master/{src}.svg").read().decode()
svg = svg.replace('<path d="M0 0h512v512H0z"/>', "")
svg = re.sub(r'fill="#fff"', 'fill="#E8EEF2"', svg)
assert 'M0 0h512v512H0z' not in svg
open(f".../KinGame/Art/icons/{name}.svg", "w", encoding="utf-8").write(svg)
```

## 3. Import — or it does not exist

`godot-mono --path SQGodotCommon --headless --import` (from the repo root). Until then
`ResourceLoader.Exists` is false and **nothing errors** — the icon is simply blank. Check the
`.svg.import` file appeared.

## 4. Credit it — the licence is per icon

Add a row to the icon table in `CREDITS.md` (what it is used as, the original name, the author), and
the author to the attribution line if new. "Some icons from game-icons.net" is not attribution.

## 5. Wire it — an icon nobody can read is noise

- An accessor in `KinArt` (`public static Texture2D XIcon => Drawing("icons/x");`).
- **Its meaning, once, in `KinSymbols`** (name, icon, tint, a one-line meaning) — the ? legend, the
  creature tips, the tile tooltips and the card hover panel all read it. Add it to `Legend` if it is
  common.
- Where it shows: a `Chip` (with its `Tip`) on a creature, a `KinMoveText` badge symbol, a
  run-screen chip row (`MouseFilter.Pass` + `TooltipText`).
- **Tint for its size**: a small symbol needs a brighter tint than the palette colour (Ember's
  `#C2621F` read as mud at chip size; chips use `#FF9A3C`).

## 6. Look at it — and hover it

Build the Godot project, then capture (`Commands.md`): `--mouse=x,y` (canvas pixels, a 1600×900
capture's pixel × 1.2) to see a tip, `--hover-card=N` for a card's panel, `--howto` for the legend.
The Godot traps that bite here (`TextureRect` sizing order, the shared card's `Ready`) are in
`.claude/rules/kin-frontend.md`.
