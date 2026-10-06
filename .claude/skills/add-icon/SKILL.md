---
name: add-icon
description: Add a UI symbol (icon) to a Godot card game in this repo from game-icons.net — find a name that exists, strip its background, recolour it, import it, credit it, and give it a meaning players can hover. Use when a status, keyword, stat, mana colour or action needs a symbol; when asked to "add an icon", "use a symbol instead of text", or "iconify" something; or when a badge or chip shows a word where a symbol would read faster. Game-agnostic; MTG paths below.
---

# Adding a symbol from game-icons.net

Symbols come from **game-icons.net** (CC BY 3.0, ~4000 icons), as light silhouettes tinted in
code. Proven on the KIN branch (2026-10-01), where a declutter pass replaced on-screen sentences
with a symbol, a number, and the words in a hover.

| Game | Icons folder | Where a symbol's meaning lives |
|---|---|---|
| MTG | `SQGodotCommon/MtgGame/Assets/Icons/` | not built yet — see step 5 |
| KIN (`kin-pivot` branch) | `SQGodotCommon/KinGame/Art/icons/` | `KinSymbols` |

## 1. Find a name that EXISTS

Names are guessed, and most guesses 404 (`lorc/flame`, `lorc/lightning-bolt`, `delapouite/card-draw`
all do). Probe a batch at once, raw from the GitHub mirror:

```bash
for n in lorc/small-fire carl-olsen/flame lorc/thorny-vine delapouite/biceps; do
  echo "$(curl -s -o /dev/null -w '%{http_code}' https://raw.githubusercontent.com/game-icons/icons/master/$n.svg) $n"
done
```

Most icons are by `lorc` or `delapouite`. Browse https://game-icons.net to find candidates by eye.

## 2. Fetch, strip, recolour

Every file has a **black 512×512 square behind the glyph** (`<path d="M0 0h512v512H0z"/>`) and a
white glyph. Remove the square; make the glyph a light neutral (`#E8EEF2`) so code can tint it to
anything with `Modulate`, or `[img color=…]` in a `RichTextLabel`:

```python
import re, urllib.request
svg = urllib.request.urlopen(f"https://raw.githubusercontent.com/game-icons/icons/master/{src}.svg").read().decode()
svg = svg.replace('<path d="M0 0h512v512H0z"/>', "")
svg = re.sub(r'fill="#fff"', 'fill="#E8EEF2"', svg)
assert 'M0 0h512v512H0z' not in svg
open(f"{icons_folder}/{name}.svg", "w", encoding="utf-8").write(svg)
```

## 3. Import — or it does not exist

`godot-mono --path SQGodotCommon --headless --import` from the repo root. Until then
`ResourceLoader.Exists` is false and **nothing errors** — the icon is simply blank. Check the
`.svg.import` file appeared, and commit it with the `.svg`.

## 4. Credit it — the licence is per icon

A row in the root `CREDITS.md` icon table: what it is used as, the original name, the author. Add
the author to the attribution line if new. **Create the file with that table if it does not exist
yet** — it must ship with the game. "Some icons from game-icons.net" is not attribution.

## 5. Give it a meaning — an icon nobody can read is noise

- **Write its meaning ONCE, in one registry** (name, icon, tint, a one-line meaning) that every
  tooltip, legend and hover panel reads. Two copies of a meaning drift. MTG has no registry yet: the
  first icon is the moment to add one beside `MtgCardTheme`, not to hardcode a tooltip.
- **Every symbol says what it means on hover.** A Control showing it uses `MouseFilter.Pass` +
  `TooltipText` (Pass shows the tip AND lets the click through; Ignore shows nothing, Stop eats the
  click). Over cards, hover is physics picking and a Control can block it —
  `.claude/rules/godot-frontend.md`.
- **Inline in text needs a `RichTextLabel`** — a `Label` cannot hold an image.
- **Tint for its size.** A small symbol needs a brighter tint than the palette colour: KIN's ember
  orange `#C2621F` read as mud at chip size; `#FF9A3C` read.

## 6. Look at it

Build, then capture (`Commands.md`, "Run a Godot scene"):
`./Run-Godot.ps1 <scene> -Capture shots/icon -Seconds 3`, and read a late frame. A capture cannot
hover, so a tooltip is only seen by playing — or by adding a debug flag that sends a real mouse
event through the viewport.
