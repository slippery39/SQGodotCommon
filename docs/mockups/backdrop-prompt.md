# Backdrop prompt

For Gemini / any image generator. The constraint that matters most is the **middle must stay empty**
— the board sits on top of it, and a busy centre makes every lane unreadable.

Ask for **16:9, 1920x1080**. Generate one per act if it works; `flood` is the one to try first.

---

## The prompt

```
Flat vector game background, 16:9, 1920x1080. A drowned post-apocalyptic city seen across still
water at dusk.

STYLE — follow exactly:
- Flat vector illustration. Bold geometric shapes, hard edges, solid fills.
- NO gradients, NO texture, NO noise, NO lighting effects, NO glow, NO bloom.
- NO outlines around shapes; forms are separated by value alone.
- Silhouettes only. No visible detail on buildings beyond window rectangles.
- Layered depth: four flat planes, each a single solid colour, lighter and lower-contrast the
  further back it sits. Think screen-printed poster, two or three passes.

PALETTE — use ONLY these six colours, no others, no blends:
- #0F1822 (nearest silhouettes)
- #16212E (ground / sky base)
- #1B2836 (mid buildings)
- #233444 (far buildings)
- #2B4054 (water)
- #3A5068 (water highlight lines)

COMPOSITION — this is critical:
- The CENTRE 60% of the frame, horizontally and vertically, must be nearly EMPTY — flat water and
  flat sky only. A user interface is drawn on top of it and must stay readable.
- Put the city skyline in the LEFT and RIGHT thirds, and only in the lower half.
- Skyline silhouettes: broken towers, collapsed slabs, a leaning crane, half-submerged blocks.
  Flat rectangles and simple angles, no rendering.
- Water fills the bottom third: flat colour with a few long horizontal highlight lines in
  #3A5068. No reflections, no ripples, no foam.
- The sky is one flat colour. No clouds, no sun, no moon, no stars.

DO NOT INCLUDE: people, characters, creatures, vehicles, text, letters, numbers, logos, watermarks,
UI elements, frames, borders, vignettes, drop shadows.

Mood: quiet, cold, abandoned, after the event rather than during it.
```

---

## Per-act variants

Swap the subject line and the two water colours; keep everything else identical so the acts feel
like one game.

| Act | Subject line | Replace `#2B4054` / `#3A5068` with |
|---|---|---|
| The Long Emergency | *"a dead motorway interchange and cooling towers under a flat grey sky"* | `#2E3A44` / `#44525E` |
| The Reckoning | *"a drowned city across still water"* (the prompt above) | unchanged |
| The Rising | *"a city half-buried in drifted ash, shapes softened by it"* | `#3A3444` / `#514A5E` |

## Checking the result before you use it

1. **Squint at it.** If anything in the middle third reads as a shape, reject it — the lanes go there.
2. **Count the colours.** `python -c "from PIL import Image; print(len(Image.open('x.png').convert('RGB').getcolors(1<<24)))"`
   Should be a handful, not thousands. A generator will sneak gradients in; they show up as a
   colour count in the tens of thousands.
3. If the count is high but the image is otherwise right, it can be **palette-locked** to the six
   colours in Pillow rather than regenerated. Ask and I will write that — it is about 20 lines.
4. Drop the result in `SQGodotCommon/DoomGame/Art/` and run
   `godot-mono --path SQGodotCommon --headless --import`, or the game will not see it.
