# KIN — visual design requirements

**Status: STYLE D CHOSEN (Shayne, 2026-09-26): fine line art, cel-shaded.** *"More generic, but I
think it's the easiest to replicate and just get stuff done."* The reference image is
`docs/mockups/round1/chatgpt_D_S1.png`. The sprite recipe is proven (see "The sprite pipeline"). C's
true-pixel route is recorded below in case it is ever wanted, but it is not the plan.
Earlier status: DRAFT, 2026-09-25. This doc says what the game's look
must DO; the mockups decide what it looks like. It supersedes the *style* half of `KinUI.md`
("Style — flat vector" and the look parts of "The visual language"). The layout contract and the
readability rules there still stand, unless a section here says otherwise.

**Why now:** Shayne's Relay playtest, 2026-09-25: *"The row-based gameplay feels smoother than the
lane-based gameplay… much more potential fun to be found here. The game still looks terrible
though."* The mechanics are still exploratory, so the look has to survive them changing. See
"What must survive a mechanic change".

## The process

1. **Requirements.** This doc.
2. **Prompts.** `docs/mockups/mockup-prompts.md` has one STYLE block per direction and one SCREEN
   block per screen. A prompt is one style block plus one screen block, so every screen in a
   direction shares one style text word for word.
3. **Mockups from three generators.** Local ComfyUI (SDXL, run by Claude) and ChatGPT and Gemini
   (run by Shayne). Put every result in `docs/mockups/round1/` as
   `<generator>_<direction>_<screen>.png`, e.g. `gemini_B_S1.png`. **Commit them.** The
   2026-09-14 mockups were lost because nobody did.
4. **Analyse.** Score each against the rubric below. Pick ONE direction, or a named mix.
5. **Lock it.** Write the chosen direction into this doc as a style guide: palette hexes, type,
   frame shapes, the sprite recipe, the icon set. Then build, one screen at a time, comparing a
   capture against the mockup.
6. **Make it a skill.** DONE (2026-09-26): the `match-mockup` skill — the loop, which tool makes
   what, `tools/install_art.py`, judging generated art, and the traps.

---

## 1. What the game is (the brief an artist or a model needs)

A **monster-collecting roguelike deckbuilder** in a **generic fantasy** world (`KinSettingSketches.md`:
a substrate that later themes can sit on, so it needs no specific lore). You travel a run of
regions. Each region has a town, wild areas, fights and a gym. You catch the creatures you beat.

**The battle is two LINES of creatures facing each other, side-on.** Your line is on the left
facing right and theirs is on the right facing left. The two FRONTS meet in the middle. Each side
has up to five. You never touch a creature directly. You play cards from a hand onto your
monsters (or onto a foe). Then at END TURN both lines act **in steps from the back, both sides at
once**, and the fronts clash last. Every move is telegraphed a turn ahead, yours and theirs alike.

The fantasy for the player is **"my team, which I caught, standing in a line and fighting as a
relay."** Everything visual should serve three things: the team is yours, the line has an order,
and the two lines are facing off.

**Tone: not decided.** The register an earlier session wanted was "warm, monster-collecting-adjacent,
not cartoonish, not cosy", and it was deferred rather than cancelled. The mockups are where it gets
decided (section 6).

## 2. What is wrong now (captured 2026-09-25, `SQGodotCommon/shots/now/`)

"It looks terrible" gets broken into causes here, because **"looks off" is a bug report**
(`CLAUDE.md`):

| # | Symptom | Cause |
|---|---|---|
| 1 | The battle floats in a void | There is no ground, stage or backdrop. The creatures are round portrait medallions in mid-air, not bodies standing on a field, so nothing says "two lines facing off" |
| 2 | Three art styles on one screen | Painted mobile-game portraits (on white or grey squares) sit next to flat-vector placeholder silhouettes and a flat navy system-font UI |
| 3 | The battle is read as text | Intents are text strings ("CHARGE 9 → FRONT"), the forecast is text ("−18 this turn"), and the order is a small bare digit. It has no icons and no visual order |
| 4 | Everything is the same size | Title, names, moves and hints are all 20–28px caps in one colour, so there is no hierarchy and the eye has nowhere to land |
| 5 | Run screens are text dumps | Town, areas, rewards, gym and find are all a centred column of labels on flat navy. A monster appears as a green button that reads "BRAMBLE 30/30". None of these screens shows a place |
| 6 | Dead space | The top half of the battle screen is empty, and so is the band between the field and the hand |
| 7 | Stock widgets | Godot's default buttons and dropdowns (END TURN, MENU, the scenario picker) |
| 8 | Cards are half-built | Trainer cards show a placeholder silhouette on a flat tint. Monster-deck cards reuse the monster's portrait at a different scale |

## 3. Hard requirements — every direction must meet these

These come from the game itself, not from taste. A mockup that breaks one gets marked down,
however good it looks.

### Canvas and readability
- **1920x1080, 16:9.** Played at 1600x900, supported down to 1280x720.
- The readability floors in `KinUI.md` still hold: **16px** real for the smallest text, **22px** for a
  number that changes the play, **44px+** for titles. The mockup must look readable at 1600x900.
- **Nothing that carries a number is dropped** at any size.

### Yours vs theirs
- Colour must never be the ONLY thing carrying a fact. **Side (left or right) and facing** are the
  primary carriers. Colour is the second.
- There must be one colour for **yours / a legal drop / precious** (it is gold today) and one for
  **the enemy / damage / HP lost** (red today). Each direction may choose its own hexes, but it
  must keep the two roles and keep them reserved.
- **Owner colour per monster** (Bramble green, Pike blue, Gale purple) is real information. A
  monster-deck card leaves the deck when its monster faints, so the card must show its owner.

### The battle must answer these at a glance, with no hover
1. **Who acts, and in what order.** The relay order (back first) is the whole mechanic, and it must
   READ as a sequence, not as a digit.
2. **What each creature will do next, and where it lands.** Attack / block / shove / summon, its
   amount, and its target (the front, the front two, everyone, the weakest, the one ahead).
3. **HP, block and status** for every creature (OFF-BALANCE, TOKEN FADES IN 1, STAGGERED, CATCHABLE).
4. **What ending the turn now would cost.** This is the forecast, per creature.
5. **Energy, the hand, Snares left, and END TURN.**
6. **When a card is held, where it can go.** Legal drops light up on either line.

### Creatures (the art that matters most)
- **Full-body, side-on, standing on the ground**, facing right when yours. A foe is the same sprite
  mirrored. They are not portraits, busts or medallions.
- **Transparent background**, so a sprite can stand on any region's ground.
- **Reads at ~150px tall.** Each creature needs a distinct silhouette. Two creatures must not be
  told apart by colour alone.
- **The same creature is the same sprite on every screen**: battle, starter pick, team, catch
  and reward. The wild and caught versions are the same drawing.
- There are 25 or more creatures now, and the pool grows faster than the art does. **The style must
  be producible by one person plus an image model, CONSISTENTLY.** A style that is gorgeous in one
  image and different in the next fails this.
- Bosses (gym leaders' creatures, the "Old" named ones) can be drawn **bigger**, and that is fine.

### Cards
- They use the existing fan (`Common/Cards/2D`): portrait cards, drag onto a creature.
- **Card anatomy:** cost (top-left), name, art, rules text at 16px or more, and the owner marker for
  monster-deck cards (colour + portrait + owner name). Rules text is never cut out. A card that
  does not fit gets a keyword instead (`KinUI.md` "Hover and explanation").
- **Card art must be a different kind of picture from creature art.** A card shows an ACTION
  (a guard, a strike, a gust of wind), not a creature standing there. Today Charge shows Pike's
  portrait, and that makes it read as "a Pike card" rather than "a charge".

### Motion (so the design leaves room for it)
- A blow LUNGES the attacker toward the other line, and the target flashes while its number rises.
  A faint fades out and the line slides closed. The step order is shown as it happens. Sprites
  need room to lunge without covering the neighbour's UI.

### Producibility
- **Solo developer, AI-assisted art.** The visual language is mostly **UI chrome built in Godot**
  (stylebox frames, 9-slices, icons), plus **generated sprites and backdrops**. Nothing may depend
  on hand-painting every screen.
- Region backdrops come **one per biome**: the Greenwood / Mossy Hollow (forest), Stony Ridge
  (rock and wind), Misty Marsh (fog and water), and Ember Crags (volcanic). There are 10 regions,
  built from 4 biomes. **The battle's middle must stay calm**, because the lines stand there
  (`docs/mockups/backdrop-prompt.md`'s rule).
- The icon set is game-icons.net (CC BY, already in `Art/icons/`) unless a direction replaces it.

### What must survive a mechanic change
Mechanics are exploratory (`CLAUDE.md`). So the look must be a **kit, not a set of screens**:
a panel frame, a creature standee, a card frame, an intent badge, an HP bar, a button, a title
treatment and a backdrop. If the kit is right, a new mechanic is a new badge, not a redraw.

---

## 4. Screen inventory — everything a mockup round must cover

**Priority 1** screens decide the direction. **2** follow once a direction is picked. **3** are
components, drawn as a sheet.

| # | Screen | P | The player's decision here | Must show |
|---|---|---|---|---|
| S1 | **Battle — your turn** | 1 | which card, on whom | both lines on a ground, move + target per creature, the order, HP/block/status, forecast, hand, energy, Snares, END TURN, region + turn |
| S2 | **Battle — a card held** | 2 | where it goes | S1 with legal drops lit, others dimmed, the dragged card over the field |
| S3 | **Battle — the relay resolving** | 1 | none (watching) | the step being played: the back pair lunging, a damage number rising, the rest waiting, the step counter |
| S4 | **Deploy** | 2 | the order of your line | your line picked up and being reordered, the foes' line already visible, FIGHT |
| S5 | **Catch** | 2 | throw the Snare or not | a weakened foe marked CATCHABLE, the Snare count, the throw |
| S6 | **Choice mid-card** | 3 | which card to discard | a panel of cards over a dimmed battle |
| S7 | **Creature inspector** (hover) | 2 | — (information) | the sprite, HP, Power, the passive and its rule, the move cycle with the next move marked, statuses |
| S8 | **Title / main menu** | 1 | start | the title (ENDLING is a placeholder), the team or a creature as a hero image, NEW RUN / CONTINUE / PRACTICE / SETTINGS |
| S9 | **Choose your starter** | 1 | which of three | Bramble, Pike and Gale as full sprites, what each WANTS in one line, the passive, the moves, HP/Power |
| S10 | **Region map / choose an area** | 1 | which area (= what you can catch) | the region's name and progress (1 of 10), two areas with their biomes, who lives there, the rare, and the trail ahead (fight, fight, find, deeper path, gym) |
| S11 | **Town** | 2 | spend gold | a place (a street, a stall), the team at full HP, the shop (Snares, 3 cards, remove a card), gold, SET OUT |
| S12 | **Victory / reward** | 1 | which card, or skip | gold gained, who was caught (the new creature, celebrated), who revived, the team with HP, 3 card choices |
| S13 | **Team and bench** | 2 | who fights next | up to 5 in the team in line order, the bench, HP, swapping |
| S14 | **Find** | 3 | — | the find (a Snare, a purse, a rest spot), the team |
| S15 | **Deeper path** | 2 | push your luck or turn back | the harder fight's foes including the RARE, the team's HP, two big choices |
| S16 | **Gym preview** | 2 | ready or not | the gym leader and their line, a sense of occasion, FIGHT |
| S17 | **Run over** | 3 | new run | won or lost, the team that got there, the region reached |
| S18 | **Deck view / remove a card** | 3 | which card goes | the whole deck as cards, the price |
| C1 | **Creature sheet** | 1 | — | six creatures in the chosen style, side-on, transparent: Bramble, Pike, Gale, Boar, Wisp, Stonebeak |
| C2 | **Component sheet** | 2 | — | card frame (trainer / monster-deck), intent badges, HP bar, status chips, buttons, panel frame, energy, Snare |

**Round 1 renders the P1 set only (S1, S3, S8, S9, S10, S12, C1) in every direction.** That makes
7 images × 3 directions × 3 generators. It is enough to choose a direction without drawing 18
screens that might be thrown away.

---

## 5. The content to show (so mockups carry real information)

Use these, not invented placeholders. A mockup of the real game is a better test than a pretty
one.

- **Your line (scenario 0), front first:** BRAMBLE (a squat mossy tortoise-golem, thorns and leaves on
  its shell, green; HP 30/30, next: BASH 6 → front, acts 5th), PIKE (a slim blue-and-cream stoat
  holding a thin spear, blue; 18/18, JAB 5 → front, acts 3rd), GALE (a purple bird-of-prey with
  wide wings, purple; 22/22, BUFFET 3 → all, acts 1st).
- **Their line, front first:** BOAR (a brown tusked wild boar; 22/22, CHARGE 9 → front, acts 6th),
  STONEBEAK (a grey eagle on a rock; 16/16, DIVE 7 → front two, acts 4th), WISP (a small teal
  glowing ghost-flame spirit; 12/12, ZAP 4 → weakest, acts 2nd).
- **Hand:** CHARGE (1, Pike's card: send it to the front, +2 Power this turn), GUST (1, Gale's:
  their front two swap), RALLY (1: +3 Power this turn), GUARD (1: gain 6 Block), STAGGER (1: a foe
  loses its next move). ENERGY 3/3, SNARES 2, TURN 1.
- **Region:** The Greenwood (1 of 10), areas Mossy Hollow (damp green forest; boars, wisps,
  mosshells, hushcaps, broodvines; rare Howler) and Stony Ridge (bare rock and wind; stonebeaks,
  cinder newts, a warden, stormbucks; rare Glowmoth). The gym: The Old Tusker.
- **Reward:** CALL SPARKS (1: summon two Sparks in front), STRIKE (1: it attacks their front now,
  3 + Power), FRENZY (1: +5 Power this turn). +20 gold. Caught: Wisp.

---

## 6. Directions to test in round 1

Three candidates, each chosen to test a different bet. **None of them is a recommendation yet.**

| | A — FIELD JOURNAL | B — BOLD STORYBOOK | C — HD PIXEL |
|---|---|---|---|
| The bet | collecting creatures = a naturalist's bestiary | a warm, readable, painterly creature game | the monster-collecting genre's native look |
| Creatures | ink line + watercolour wash, like a field-guide plate | thick-outlined, flat-shaded, chunky shapes, a little stylised | crisp pixel sprites, ~64–96px scaled up 2–3× |
| UI chrome | parchment, ink rules, pressed-paper cards, stamps and tags | rounded chunky panels, bold shadows, bright buttons | pixel 9-slice frames, bitmap-style type |
| Backdrops | painted vignette landscapes, soft edges | flat layered landscapes, strong shapes | parallax pixel layers |
| Yours / theirs | gold ink / vermilion ink | gold / red, saturated | gold / red from a fixed palette |
| **Producibility with AI** | **good**: models do watercolour well, and ink + wash hides seed drift | **good**: close to the current creature PNGs, so they may be salvageable | **weak**: AI "pixel art" has mixed pixel sizes and needs cleanup per sprite |
| Readability risk | low contrast on parchment, and small text in ink colour | low | text size is fixed by the pixel grid |
| Distinctiveness | high | medium | low (crowded genre) |

**D — FINE LINE (added 2026-09-25, Shayne: "more fine, line-based, still gamey, not pixely")**:
clean, thin ink line art with hard two-tone cel shading, sleek dark-slate UI with thin bright
borders, and line-art backdrops drawn softer than the creatures. This is the non-pixel twin of C.

**C is in the round because it is the genre's default. It is also the one most likely to lose on
producibility.** The test is whether its look is worth that cost. A named mix (for example "A's
chrome with B's creatures") is a legitimate outcome.

## 7. The rubric — how mockups are judged

Score each mockup 1–5 on each line, and then compare directions, not single images.

1. **The battle reads at a glance.** Can you point to who acts first, what the Boar will do and to
   whom, and who is closest to dying, in five seconds, at 1600x900?
2. **The two lines face off.** Does it feel like a confrontation on a ground, not a spreadsheet?
3. **Consistency across screens.** Do the P1 screens of one direction look like one game?
4. **Creatures read at 150px.** Are the silhouettes distinct and the style consistent between creatures?
5. **Producibility.** Could OUR pipeline make 25 more creatures in this style? (Test it: C1 via ComfyUI.)
6. **Buildable in Godot.** Can the chrome be made from styleboxes, 9-slices and icons? Or is it a
   painting that only works as one image?
7. **Tone.** Does it feel like a game about a team you caught and care about?

**What a generated mockup is NOT:** a spec for pixel positions or text. Image models invent UI and
garble words. **Read a mockup for mood, hierarchy, materials and composition.** The layout
contract in `KinUI.md` stays the authority on what goes where.

## 8. Open questions for Shayne

1. **Tone:** warm and friendly (Pokémon-ish), earthy and a bit melancholic (the original "Endling"
   title points there), or neutral? This single answer steers most of section 6.
2. **The existing creature PNGs** (13 painted portraits): keep the look, or treat them as throwaway?
3. **Owner colours** (green, blue, purple for the starters): does every monster keep a signature
   colour, or only the starters?
4. **Is the hand fan staying at the bottom?** It currently takes about 35% of the screen height.

---

## Round 1 results — ComfyUI (local SDXL, DreamShaper XL Turbo), 2026-09-25

36 images: the three directions × (S1 battle, S8 title, a forest backdrop, and Bramble, Pike and
Gale as sprites) × 2 seeds (images deleted 2026-09-26 once style D was chosen). The prompts are in
`docs/mockups/comfy-round1.json`. Each image takes about 16s. **The ChatGPT and Gemini rounds are
still to come**, so nothing below is a decision.

| | A — Field Journal | B — Bold Storybook | C — HD Pixel |
|---|---|---|---|
| Sprites | **the most consistent of the three**: all six read as plates by one artist. But they are realistic animals (Bramble is just a tortoise), so they lose the "monster" feel | clean, appealing, and on a **white ground (easy to matte)**. Poses are 3/4 front, not side-on. Closest to the current 13 PNGs | **not pixel art at all**: the checkpoint ignores the ask and draws smooth semi-painted creatures |
| S1 battle | the mood is right (parchment frame, cards as inked panels), but there is no line-up | **the only one that shows a side-on stage** with creatures on a field and a card bar. The best mood test | the pixel look arrives in scenes (not sprites); creatures are scattered top-down |
| Title / backdrop | watercolour valley, good. The backdrop keeps a calm middle | bright, layered, good | good pixel-ish landscapes |

**What this says about the pipeline, whichever direction wins:**
- **SDXL cannot mock up a screen.** None of the six S1 images forms two lines, and all the text is
  garbled. Full-screen mockups are ChatGPT's and Gemini's job. Locally we test **sprites and
  backdrops**, which is what the pipeline will actually produce.
- **"Facing right" is obeyed about half the time.** Mirror sprites in Godot (foes are already
  flipped) rather than regenerating.
- **Side-on is a hard ask**, and B slides to 3/4 front. Decide whether side-on is really required.
  A 3/4 pose still reads as "facing" if both lines are mirrored.
- **C is not locally producible** without a pixel-art LoRA or a different checkpoint. If C wins on
  looks, that is a setup task (and a cleanup pass per sprite) before any art.
- **A's sprites stand on parchment with a painted ground patch.** They would need a matting pass
  for transparency. Or the parchment could BECOME the frame (a sprite on a card-like plate), which
  answers the requirement differently.

## Round 1 — ChatGPT, and what it changed (2026-09-25)

**`chatgpt_C_S1.png` (Style C, the battle screen) is the first image that looks like the game.**
Shayne: "looks amazing". C is his current lean.

What it gets RIGHT, and should be kept whatever the style:
- **A stage.** A forest ground strip with the lines standing on it, and the fronts meeting in the
  middle. Requirement 1 (in section 2) is solved by composition alone.
- **Intent badges ABOVE the heads** (icon + amount + "→ target") in a dark pill. This is far more
  readable than today's text lines.
- **Order badges joined by a dotted line**, gold on your side and red on theirs.
- **HP bars in owner colours** for your side, red for theirs, and a red forecast number under
  each foe.
- **Cards:** a cost gem, an action illustration (not a creature portrait), a frame in the owner's
  colour, and the owner's face in a small medallion at the foot. That meets every card requirement.
- **The chrome:** an energy orb bottom-left, the Snare count under it, a big framed END TURN, and a
  region banner top-left.
- The sprites are about 150px on a 1672-wide image, which is the size the requirements asked for.

What it gets WRONG, to fix in the prompt (not the style):
- **The order is wrong, and it is shown twice.** It numbered your line 1–3 and theirs 4–6. It
  also added a second, reversed set of digits under the badges. The Relay acts in STEPS from the
  back with BOTH SIDES AT ONCE, so **the badge should be the STEP: Gale and Wisp both "1", Pike
  and Stonebeak "2", Bramble and Boar "3"**, with the pair linked across the field. That shows the
  rule itself rather than a queue. **Today's board has the same flaw**: `ActingSteps` groups by
  POSITION (deepest first, both sides together), but `KinPartyBoard` badges the flattened
  `ActingOrder` as 1–6, which makes simultaneous blows look sequential. A creature with nobody at
  its depth on the other side acts alone in its step.
- It gave every foe the same "−18" and every creature 30/30, and invented its own rules text.
  Ignore those, as expected.
- The backdrop is busy in the centre (cliffs and a waterfall). It reads anyway, because the
  sprites have hard dark outlines. The calm-centre rule may be weaker than we thought when
  sprites are outlined.
- **It is not real pixel art.** Zoomed in, the pixels are of mixed sizes, off any grid, and
  blurred. That leads to the next finding.

### Pixel art is a PASS, not a model — `tools/pixelate.py`

No generator here makes real pixel art: SDXL ignores the ask, and ChatGPT fakes it. **So C does not
need a pixel model.** Generate a clean sprite with any model, then `tools/pixelate.py` mattes the
background, downscales to a fixed height, quantizes to a small palette, adds a 1px outline, and
scales up with nearest-neighbour. The grid and palette then come from the script, so they are
identical on every creature, which a model could never promise.
A test image (since deleted) put B's and C's local sprites through
it and stood on a backdrop. They read as one pixel set. Known gaps: pale background ENCLOSED by
the body survives the matte (Pike's spear gap), and the height and scale must be set to about 64–72px
at 2× for a 150px sprite. A pixel-art LoRA for SDXL is an option on top of this, not a
prerequisite.

### Style D locally — the best ComfyUI result of the four

Six creatures × 2 seeds (`D_C1_*`). **Consistent line weight and shading across all twelve**,
mostly side-on, mostly on a pale ground (so they matte easily), and they read as creatures, not as
the plain animals A draws. Misses: Wisp came out as a small imp (a noun problem: say "a floating
flame"), and one Bramble seed drew the forest behind it. The battle screenshot has the same SDXL
limit as before: no lines of creatures.

**Where round 1 stands:** C (via ChatGPT for mockups, `pixelate.py` for sprites) and D (producible
locally as it is) are the two live candidates. Next: run S1 in style D through ChatGPT, then run
S8/S9/S10/S12 in whichever wins S1, in the SAME chat as that S1.

### `chatgpt_D_S1.png` — Style D, with the corrected S1 prompt (2026-09-25)

Shayne: "also looks pretty good". **Every fact on it is right**: the six HP values, the six intents,
the forecasts on the foes only, the step badges paired 1/2/3 by depth, and all five cards with their
real text, owner frames and owner medallions. The prompt fix worked. The facts in C's image were
wrong because of the prompt, not the style. **So C vs D is not yet a fair comparison: re-run C with
the new S1 screen block before deciding on looks.**

- **D over C:** text and numbers are crisp at every size, which matters most at 1280x720. The creatures
  are larger and more expressive, and they face the right way.
- **C over D:** more genre charm and a stronger identity. D reads as "a polished indie game", which is
  also the crowded look.
- The dotted arcs link each line's sequence (1→2→3) rather than the pairs across the field. Minor.
- The backdrop's centre (a river valley) is busy but low-contrast behind outlined sprites, and it
  reads. **Relax the calm-centre rule to "low contrast behind the lines".**

**Can the local pipeline make this?** A side-by-side (since deleted): ChatGPT's D above,
and ComfyUI's D sprites matted with `pixelate.matte` on a ComfyUI D backdrop below. **The same
family, but not the same artist.** The local creatures are more naturalistic (a bald eagle, a
realistic boar) and less saturated. The local backdrop's trees are too dense and too contrasty, and
it has no ground strip. The matte also keeps the soft grey shadow under the feet. That should be
cut, and the engine should draw a shadow ellipse instead.

**The pipeline this suggests (not decided):** make the creature sprites in ChatGPT, in the same chat
as the S1 image ("draw BOAR alone, full body, side view, on flat white, same style"), then matte them
locally. That gives one artist for the whole set. ComfyUI stays for backdrops and bulk. An
IP-Adapter in ComfyUI (a style reference image) could close the gap locally. That means custom nodes
plus about 2GB of models: ask first.

### The sprite pipeline — PROVEN on one creature (2026-09-25)

Shayne: *"the local ComfyUI can't seem to create good stylised sprites, it's kind of all over the
place… ChatGPT generation is much superior."* **ComfyUI is dropped for creature sprites.**

The test: one message in the same chat as `chatgpt_D_S1.png` ("same style and creature design as
that battle image; draw BOAR alone, side view facing right, on flat pure white"). The result is
`chatgpt_D_boar_raw.png`, 1536x1024, the same boar as in the scene. `pixelate.matte` cut it out
with **no white fringe** (0 whitish pixels on the rim), giving `round1/sprites/boar.png`.
`sprites/boar_check.png` pastes it into the scene at 150px, mirrored as a foe, beside the boar
ChatGPT drew there, and the two cannot be told apart.

**The recipe (provisional until the style is locked):**
1. Anchor every sprite chat to the S1 image. Generate in the same chat, or upload `chatgpt_<X>_S1.png`
   plus an accepted sprite as references when starting a new chat.
2. "Draw <NAME> alone: <one-line description>, full body, side view facing right, standing,
   centred, on a flat pure white background. No ground, no shadow, no UI, no text."
3. `python tools/pixelate.py` → use `matte()` only for D. For C, the full pass makes the grid.
4. Check it at 150px beside an accepted sprite. The engine mirrors foes and draws the ground
   shadow.

**Still to prove:** consistency across 25+ creatures and several chats. Do the other five S1
creatures next and look at all six in one row.

### `chatgpt_C_S1_v2.png` — C with the corrected prompt, and what the two mockups settle

Every fact is right again. The paired steps are right, though one dotted line runs through all six
badges. The Snare icon became a spiked trap. The pixels are fake again (see `pixelate.py`).
**C and D now differ only in style**, so the comparison is fair and the choice is Shayne's.

**The LAYOUT is settled by both** (`round1/kit_C_vs_D.png` crops the same elements from each):
a banner top-left and a menu top-right; one badge row over the heads (a move pill with a step disc
beside it); sprites about 150px with feet on one ground line; name, a hex-ended HP bar and a red
forecast under each creature; a card fan in the centre bottom (cost gem, art window, text box,
owner medallion); an energy orb with the Snare count bottom-left; a framed END TURN bottom-right.
This is style-independent, so it can be BUILT now while the style waits.

**ChatGPT's quota is the bottleneck** (a daily limit that Shayne hit on day one). So spend it only
on what nothing else can do (screens, and one sprite per creature). Gemini has its own quota and
takes a reference image. Everything below runs locally with no quota:
- **Inpainting the mockup** (SDXL; the script was deleted once rejected): paint the UI and creatures out, and
  the model takes its style from the pixels that remain. The result (since deleted) was a
  plausible D forest backdrop. Known seams: a band at the top where the banner mask ended, and a
  sliver of the old waterfall between two masks. Masking 85% of the frame just repaints a new scene.
  **Shayne: "clear seams, which make it basically unusable" — REJECTED.** Text-to-image backdrops
  replaced it.
- **Crops of UI elements are REFERENCE, not assets.** They have baked-in text, they are 1672px, and
  they are soft. The chrome gets built in Godot (styleboxes and 9-slices) to match them.
- **Sprites cannot be cut from the mockup** (150px, on a busy background). Ask for each one alone
  on white.

### TRUE pixel art — the route, if C wins (2026-09-26)

Shayne likes C, and asked whether it can be made real pixel art. **Yes:** `tools/pixelate.py` with a
**shared palette**, so every asset shares one grid and one set of colours:
- **One pixel scale for everything: a 640x360 art canvas at 3×** on 1920x1080. A creature is about
  50px tall (150 on screen). The backdrops are painted or downscaled to 640x360. The UI 9-slices
  and the font are drawn at the same 3×. Mixing scales ("mixels") is the thing that makes pixel
  art look fake, and it is exactly what ChatGPT's mockups do.
- **One palette for the game:** `palette_of(reference)` then `pixelate(..., palette=pal)`.
  `sprites/boar_pixel_on_pixel_backdrop.png` is the ChatGPT D boar at 50px, with a ComfyUI backdrop
  at 640x360, both on a 32-colour palette taken from `chatgpt_C_S1_v2.png`. It is real pixel art
  on one grid.
  Weak spot: a palette taken from one mockup lacks some colours (the boar lost its red eye and went
  a bit purple). **Build it from the mockup PLUS the accepted sprites, or adopt a curated palette**
  (Endesga 32, Resurrect 64).
- **In Godot:** nearest texture filtering, integer scale only, a pixel font at 3×. Positions snap
  to the 3× grid. A lunge moves in whole art pixels.
- **The source sprite can be ANY clean drawing**: the D boar pixelated well. So ChatGPT sprites can
  be requested in D (cleaner edges) and pixelated. That keeps the C/D decision open until late.

## Build iteration 1 — the battle screen toward `chatgpt_D_S1.png` (2026-09-26)

Shayne: *"iterate until the in-game matches the mockup as best as you can; use ComfyUI sprites if
need be, and iterate on their prompts."* Captures are in `SQGodotCommon/shots/i*/`. What was learned:

### Local sprites that match a mockup: img2img from the MOCKUP'S OWN CROPS
Text prompts drift however they are worded. Four rounds on the five intro creatures (`KinGame/Art/sprites/`):
- **Round 1 (text to image, two styles):** colour bleeds between words ("yellow beak" gave yellow
  wings); "side view" is ignored for upright creatures; nouns pull in stereotypes ("eagle" gives a
  bald eagle, "spirit" gives an imp with limbs, "tortoise" gives a plain turtle).
- **Round 2 (refined wording, `(side view:1.4)` weights):** better, but still drifted.
- **img2img from the creature's crop of the mockup, at denoise 0.7 (`tools/img2img.py`) keeps
  ChatGPT's exact design** (pose, colours, props) in clean line art, and mostly drops the scene.
  At 0.55 the forest stays; at 0.8 the design starts to drift. **This is the local route:** one
  ChatGPT mockup or sprite per creature, then local img2img for clean variants.
- **The cut-out needs an EDGE matte**, because img2img backgrounds are flat grey or olive, not white.
  `tools/pixelate.py`'s `matte()` now floods SMOOTH regions from the border (Sobel on a blurred
  luminance), which the dark outline stops, on any colour of ground.
- **Bramble** kept a strip of grass from its forest crop on every seed. `grass=True` drops green
  connected to the bottom, which is safe only because this Bramble's legs are not green. A
  "full-width row" trim was tried and cut the tortoise's own wide body.

### Card art: text to image works for ACTIONS
35 cards, `Art/cards/<name>.png`. Objects and effects (a gust of leaves, a meteor, a shield, a
banner) come out well from text alone. Creatures in card art drift, as they do for sprites.

### The backdrop: text to image, then RAISE it
`backdrops/greenwood.png` (prompt `b1`, seed v1) matches the mockup's composition: trees framing the
sides, a river valley with mountains, a meadow in front. The first capture had the lines standing
in the sky. **The stage is lifted 240px (`StageLift`) so the meadow is under their feet**, and the
sky is what gets cropped.

### The layout
- **Places widen to fit the longest line** (290px at three a side, as in the mockup; 176px at
  five). The view scales with them up to 1.3×, through an inner node: `KinAnimator.Pop` tweens the
  root's scale back to 1. Safe because no card targets an empty place beyond a line's end.
- **Hex ends for free:** `StyleBoxFlat` with a half-height radius and `CornerDetail = 1` draws a
  bevel. Used on the HP bars and END TURN.
- **The card** (`KinCardKit`): a dark body edged in the owner's colour (steel for trainer cards), a
  darker name plate, a parchment text box with dark ink, a blue diamond gem, and an owner medallion
  at the foot. **The shared rules label has a size-10 black SHADOW as well as its outline**, which
  smeared dark ink into a blot; both are zeroed for the card.
- The "YOUR LINE ▶ FRONT" caption is gone, because facing sprites say it. The combat log moved above
  END TURN.
- **Every sprite file must FACE RIGHT; the game mirrors foes.** A crop taken from the mockup of a
  FOE is already facing left (Stonebeak), and a seed can simply draw the creature backwards
  (Bramble). Both stood facing away from the fight until the source files were flipped. Check the
  facing of every new sprite in a capture, where it is obvious. On a contact sheet it is not.
- The owner medallion is cut from the sprite's head: a square around the opaque mass of its upper
  55%, weighted to the front. A top-right corner crop found Pike's spear tip and Gale's wing. Bramble's
  head is low, so its medallion is mostly shell. Known and minor.

**Result:** `round1/build2_vs_mockup_D.png` (the mockup above, the game below). Still different from
the mockup: the region plate names the practice scenario and not the region; every attack uses the
sword icon (the mockup varies them per move); the hint line sits over the stage; Strike's art is a
random beast. Creatures other than the intro six still show their old portrait medallions until
they get sprites.

## Build iteration 2 — Shayne's three notes (2026-09-26)

1. **"The monsters look like they're hovering."** There were two causes. The backdrop's ground under
   the feet was the FAR meadow, so `StageLift` went from 240 to 380 and the feet now stand on the
   near grass. The contact shadow was a hard-edged, faint bar, and is now `Art/ui/contact.png`: a
   soft blurred ellipse sized to each sprite (85% of its width), black at 0.6 alpha, gold when the
   creature is a legal drop.
2. **"The creature text is hard to read — the mockup uses a backshadow."** Every creature's words
   now sit on `Art/ui/scrim.png`, a dark ellipse blurred until it has no edge. A `StyleBoxFlat`
   panel was tried first and read as a dark card. The labels also got a drop shadow.
3. **"The UI looks flat, default Godot."** Every frame is now a TEXTURE:
   `tools/make_ui.py` draws the kit into `Art/ui/` (gradient fill, top gloss, a dark inner line,
   and a bevelled metal rim in gold, red or bone), and `KinUiKit` hands the pieces out as 9-slices.
   The energy orb is a glossy sphere in a studded gold ring. END TURN is a hex button. The HP bars
   are a textured trough plus a pale fill tinted per creature. The step discs and move pills use
   gold or red rims. Card frames got a lit-from-above body and a bevelled edge. **Why a script and
   not ComfyUI:** UI pieces must 9-slice, match each other exactly, and be re-tinted, and
   generated images do none of that. Re-run the script after changing a colour, then `--import`.

## Build iteration 3 — the run screens, with no mockups (2026-09-26)

Shayne: *"While we don't have mockups for the other screens, can we continue using Comfy instead?"*
**Yes, because the battle mockup already fixed the LOOK.** Its kit (bevelled plates, hex button,
outlined type, sprites, action card art) carries to every screen. What a screen still lacks is a
PLACE, and a ComfyUI backdrop is good at that. Every run screen is drawn by `KinPartyRunScreens`'
shared helpers (`Begin`, `Tile`, `Button`, `Label`, `Monster`), so restyling those restyled all of
them at once:
- **A scene behind each screen**, dimmed: `backdrops/title.png` (a sunrise valley with the smoking
  volcano) for the starter pick, `town.png` (a lantern-lit market street) for the town, `map.png`
  (an overhead view of the region) for area choice and the gym, and the battle's `greenwood.png`
  elsewhere. `Begin(title, subtitle, scene)`.
- Tiles are kit plates, tinted toward a starter's colour. Buttons are kit buttons. Titles are gold
  and outlined. Team buttons carry the creature's medallion.
- Tiles show standing SPRITES (starters, area residents, gym and deep foes) and ACTION art (reward
  and shop cards, including a generated Snare).
- **Portraits → sprites by img2img (denoise 0.72)** worked for five of six old portraits (Old
  Tusker, Bog Toad, Briar Viper, Cinder Newt, Mosshell). A portrait that is a whole SCENE (Old Mire
  in its swamp) cannot be matted: its cut-out is the scene. Text-to-image replaced it. Cinder Newt
  came out facing left and was flipped.
- The 16 creatures with no art at all (round one, gym creatures, tokens) were generated by
  text-to-image with the sprite style block.
