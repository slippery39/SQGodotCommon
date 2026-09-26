# Mockup prompts — round 1

**Read `KinVisualDesign.md` first.** It holds the requirements, the screen inventory (S1–S18,
C1–C2), the three directions and the rubric.

**A prompt = one STYLE block + one SCREEN block, pasted together.** Paste the style block word for
word every time. That text is the only thing that keeps six screens looking like one game.

Save results to `docs/mockups/round1/<generator>_<direction>_<screen>.png`, e.g.
`chatgpt_A_S1.png`, `gemini_B_C1.png`, `comfy_C_S10.png`. **Commit them.**

---

## How to drive each generator

### ChatGPT (GPT image) and Gemini (Nano Banana / Imagen)
Both can render UI layouts and short text. That makes them the right tools for FULL-SCREEN mockups.

1. **One chat per direction.** Start with **S1 (battle)**, because it is the hardest screen and it
   sets the look.
2. For every later screen in that chat, put this line first:
   > *Same game, same art style, same UI kit, same palette and same creature designs as the previous
   > image. Now show a different screen:*

   …then the screen block. Keeping one chat per direction is what holds the screens consistent. A
   new chat drifts.
3. If a later image drifts anyway, **upload the S1 image** as a reference and say "match this".
4. Ask for **landscape 16:9** (ChatGPT: 1536x1024 is the nearest it offers; that is fine).
5. Text will be partly garbled. That is acceptable. Judge layout, hierarchy and materials, not
   spelling.
6. If a model draws a phone game, add: *"PC game on a 1920x1080 monitor, mouse-driven, not mobile."*

### ComfyUI (local SDXL — `tools/gen_art.py`)
SDXL cannot lay out a UI or write words. **Use it for what our pipeline will actually produce:**
creature sprites (C1), backdrops, and a "screenshot mood" test. Its S1 result tests whether a
direction can be generated locally at all, not what the screen will be. Run:

```
python tools/gen_art.py --prompt-file docs/mockups/comfy-round1.json --out <scratch dir>
```

(`comfy-round1.json` holds the shortened SDXL versions of these prompts. SDXL wants about 60 words
of keywords, not paragraphs.)

---

## STYLE blocks

### Style A — FIELD JOURNAL
```
STYLE: A naturalist's field journal come to life. Creatures are drawn like plates in an antique
field guide: confident brown-black ink linework with loose watercolour washes, visible paper grain,
soft unfilled highlights. The UI is made of aged cream parchment panels with torn or deckled edges,
thin ink rules, pinned paper tags, wax-seal and rubber-stamp motifs, and handwritten-style labels
next to a clean serif typeface for numbers. The palette is warm cream, sepia, muted moss green,
slate blue and faded ochre. Gold ink marks the player's side and things they can act on. Vermilion
ink marks enemies and damage. Backgrounds are soft watercolour landscapes that fade to paper at the
edges. Tone: curious, warm, a little melancholy; an explorer's notebook, not a children's book.
Painterly but tidy; every element legible.
```

### Style B — BOLD STORYBOOK
```
STYLE: A bold, warm fantasy creature game. Creatures are chunky and appealing, with thick dark
outlines, flat colour with one step of cel shading, simple readable shapes and big expressive eyes.
They are stylised but not babyish. The UI kit is rounded panels with a thick dark outline and a
chunky drop shadow, a deep blue-slate base, bright bone-white text in a heavy rounded sans-serif,
and big pill-shaped buttons. Backgrounds are layered flat-colour landscapes with strong silhouettes
and atmospheric depth; the middle stays calm. Gold means the player's side and anything they can
act on. Red means enemies and damage. Everything reads from across the room: high contrast, clear
icons, generous spacing. Tone: adventurous and warm, in the spirit of a modern indie monster-taming
game.
```

### Style C — HD PIXEL
```
STYLE: High-definition pixel art in the style of modern indie monster-taming RPGs. Creatures are
crisp pixel-art sprites about 96 pixels tall, shown at 3x, with a dark 1-pixel outline, limited
palettes and clean dithering. The UI uses pixel-art 9-slice frames with a beveled dark-blue border,
a pixel bitmap font for labels, and chunky pixel icons. Backgrounds are pixel-art parallax
landscapes in 3–4 depth layers. A fixed 32-colour palette with rich greens, dusk blues and warm
highlights. Gold marks the player's side and legal actions. Red marks enemies and damage. Sharp
pixels everywhere: no blur, no smooth gradients, no anti-aliased painting.
```

### Style D — FINE LINE (clean line art, cel-shaded) — added 2026-09-25
Shayne: "not pixel art but more fine, line-based art; still looks gamey." The usual names for
this are *clean line art*, *cel-shaded 2D*, and *hand-drawn 2D / ink and flat colour*.
```
STYLE: Crisp hand-drawn 2D game art with clean, fine, confident line art. Creatures have thin,
even dark ink outlines (thinner inside the shape than on the silhouette), flat base colours with
two tones of hard-edged cel shading and a small rim highlight. There is no painterly texture, no
pixels and no blur. The shapes are appealing and readable with a little detail in fur, feathers and
leaves. The UI kit is sleek dark-slate panels with a thin bright inner border, small ornamental
corners, crisp vector icons, and a clean bold sans-serif with outlined numbers. Backgrounds are
drawn in the same line art but softer and lower-contrast than the creatures, so the creatures pop.
Gold marks the player's side and legal actions. Red marks enemies and damage. It should look like
a polished modern 2D indie card game at 1920x1080: sharp at full resolution, detailed but clean.
```

---

## SCREEN blocks

### S1 — Battle, your turn
```
SCREEN: The main battle screen of a PC card game, 16:9 landscape, seen side-on like a stage.
- A wide ground strip across the middle of the screen: a forest clearing with moss and roots. Soft
  scenery above and behind it, calm in the centre.
- LEFT HALF: the player's line of three creatures standing on the ground, facing RIGHT toward the
  enemy, spaced evenly. From the centre outward: BRAMBLE (a squat mossy tortoise-golem with thorny
  leaves on its shell, green), PIKE (a slim blue-and-cream stoat holding a thin spear, blue), and
  GALE (a purple bird of prey with wide wings, purple) at the back.
- RIGHT HALF: the enemy line of three facing LEFT, mirrored. From the centre outward: BOAR (a brown
  tusked wild boar), STONEBEAK (a grey eagle with a heavy stone beak), and WISP (a small teal
  floating spirit flame with two eyes) at the back.
- The two front creatures, Bramble and Boar, face each other across a small gap in the exact centre.
- Above each creature, a small dark intent badge: an icon plus a number and a tiny target arrow.
  Gale: wind icon "3 → all". Pike: spear icon "5 → front". Bramble: fist icon "6 → front".
  Boar: horn icon "9 → front". Stonebeak: talon icon "7 → front two". Wisp: lightning "4 → weakest".
- Next to each intent badge, ONE small round STEP badge. Creatures at the same depth act
  TOGETHER, so they share a number: the two BACK creatures (Gale and Wisp) both show "1", the
  middle pair (Pike and Stonebeak) both show "2", and the two FRONT creatures (Bramble and Boar)
  both show "3". Gold badges on the player's side, red on the enemy's. A faint dotted arc links
  each pair across the field. Only one step badge per creature, with no second set of numbers.
- Under each creature: its name and a chunky HP bar. The player's bars are in each creature's own
  colour: Gale purple "22/22", Pike blue "18/18", Bramble green "30/30". Enemy bars are red:
  Boar "22/22", Stonebeak "16/16", Wisp "12/12". Under each enemy bar, a small red number for the
  damage it will take this turn: Boar "−18", Stonebeak "−5", Wisp "−3".
- TOP-LEFT: a region banner "THE GREENWOOD · TURN 1". TOP-RIGHT: a small menu icon.
- BOTTOM: a hand of five portrait playing cards fanned in an arc, each with a cost gem at the
  top-left, a name, a small illustration of an ACTION (not a creature portrait), and one line of
  rules text:
  CHARGE (1) — a stoat lunging with a spear — "Send it to the front. +2 Power this turn."
  GUST (1) — a swirl of wind and leaves — "Their front two swap."
  RALLY (1) — a raised banner — "+3 Power this turn."
  GUARD (1) — a raised shield — "Gain 6 Block."
  STAGGER (1) — a foe stumbling — "A foe loses its next move."
  CHARGE has a blue frame and a tiny Pike portrait at its foot, and GUST has a purple frame and a
  tiny Gale portrait, showing they belong to those creatures. The other three have neutral frames.
- BOTTOM-LEFT: an energy orb reading "3/3" and a small snare icon "×2". BOTTOM-RIGHT: a large
  "END TURN" button.
- PC game on a 1920x1080 monitor, mouse-driven, not mobile.
```

### S3 — Battle, the relay resolving
```
SCREEN: The same battle as the previous image, mid-animation, while the turn resolves. The hand of
cards is lowered out of view. A banner at the top centre reads "STEP 2 OF 3". The two creatures at
the BACK of each line, GALE on the left and WISP on the right, are acting right now. Gale is lunging
forward with a wide gust of wind sweeping across the enemy line. Wisp shoots a small teal bolt at
Pike. Bold damage numbers float up from the targets: "−3" over each enemy and "−4" over Pike. The
creatures that have not acted yet are slightly dimmed. Their order badges glow softly, showing
who goes next. Motion lines, a flash on the hit creatures, and a small impact burst.
```

### S8 — Title screen
```
SCREEN: The title screen. A large painted hero scene fills the screen: a small team of three
creatures (a mossy tortoise-golem, a blue stoat with a spear, a purple bird of prey) seen from
behind on a hilltop, looking out over a wide fantasy valley with forests, a marsh, rocky ridges and
a distant smoking volcano. The game title "ENDLING" is lettered large at the top left in the style's
title treatment. Below it is a vertical menu of four buttons: NEW RUN, CONTINUE, PRACTICE,
SETTINGS. Quiet, inviting, a sense of a long journey ahead.
```

### S9 — Choose your starter
```
SCREEN: Choose your starting creature. Title at the top: "CHOOSE YOUR STARTER". Three large
side-by-side presentation panels, each holding one full-body creature standing on a small patch of
its own ground, facing right:
1. BRAMBLE — a squat mossy tortoise-golem with thorns (green panel accent). Tagline "Wants to be
   HIT". A passive chip "THORNS 2". Three small move icons with numbers. "HP 30 · POW 2".
2. PIKE — a slim blue-and-cream stoat with a thin spear (blue accent). "Wants never to be where the
   hit lands". Chip "FINISHER +2". "HP 18 · POW 3".
3. GALE — a purple bird of prey with wide wings (purple accent). "Wants the FOES where it chooses".
   Chip "OFF-BALANCE +2". "HP 22 · POW 2".
The middle panel is hovered: lifted, with a gold outline. A subtitle under the title: "Catch the
rest: weaken a foe, then throw a Snare."
```

### S10 — Region map, choose an area
```
SCREEN: The region map. A top banner reads "THE GREENWOOD — REGION 1 OF 10", with a row of ten
small region pips, the first one lit. The main area is an illustrated map of one region as a
painted overhead map: a small town at the bottom, and two paths branching up into two areas. On the
left, MOSSY HOLLOW (a damp green forest). On the right, STONY RIDGE (bare grey crags and wind).
Each area has a label card showing the little creature icons that live there (boar, wisp,
mushroom, snail-shell / eagle, newt, stone guardian, antlered stag) and a sparkling "RARE" slot
with a silhouette. Along each path, a trail of node icons: crossed swords, crossed swords, a
treasure/find icon, a darker "deeper path" skull icon. Both paths meet at the top at a big GYM
building with a boar-skull emblem, labelled "THE OLD TUSKER". The player's team marker stands at
the town. Gold and snare counters in a corner: "GOLD 60 · SNARES 3".
```

### S12 — Victory and reward
```
SCREEN: The screen after winning a battle. A banner at the top: "VICTORY", with "+20 GOLD" under
it. Centre left, a celebration moment: "CAUGHT!" over the small teal glowing WISP creature, which
sits inside a glowing snare ring with sparkles, and a note "joins your team". Along the top-right,
the team as a row of small standing creatures with HP bars (Bramble 30/30, Pike 12/18, Gale 22/22,
Wisp 4/12). The lower half: "CHOOSE A CARD" with three large portrait cards side by side: CALL
SPARKS (cost 1, two little sparks being summoned), STRIKE (cost 1, a quick slash), FRENZY (cost 1,
a creature roaring with red energy). Each has one line of rules text. A small "SKIP" button below.
```

### C1 — Creature sheet
```
SCREEN: A character model sheet on a plain neutral background. Six fantasy creatures in two rows,
each full body, side view, facing RIGHT, standing, the same scale and the same drawing style, with
generous space between them and a name label under each:
BRAMBLE — a squat mossy tortoise-golem, thorny leaves on its shell.
PIKE — a slim blue-and-cream stoat holding a thin spear.
GALE — a purple bird of prey, wings half open.
BOAR — a brown tusked wild boar.
WISP — a small teal glowing spirit flame with two eyes.
STONEBEAK — a grey eagle with a heavy stone-coloured beak.
They must read as one consistent set by one artist. Each must have a distinct silhouette.
```

---

## After a round: what to record

In `KinVisualDesign.md`, under a new "Round 1 results" section: which generator did which
direction best, the rubric scores, what each image got RIGHT that should be kept (e.g. "Gemini B
S1: the intent badges above the heads"), and the decision.
