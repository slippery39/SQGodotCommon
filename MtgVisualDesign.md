# MTG visual design — requirements, directions, locked style

The process is the `match-mockup` skill. This file is its record for the MTG front end: what the
look must DO, the mockup prompts as sent, how they scored, and (once chosen) the locked style.

## 1. Requirements — what the look must do

**Canvas.** Landscape 16:9, 1280x720 window override (`project.godot`). **PC and phone equally** —
the APK is how the game gets playtested on the go, so every number, button and token must read and
be tappable on a landscape phone. Hearthstone is the sizing reference: it was built for phones.

**The engine decides the layout, so the reference is Hearthstone, not MTGO** (decided 2026-10-06):
a turn is one phase, there is no blocking and no priority window, payment is automatic. A phase
strip, stack window or "respond?" prompt would show nothing. Attacking picks a target — creature
or player — which is Hearthstone's model. From MTGA: the painted stage, hero portraits,
hover-to-enlarge. From MTGO: zone counts always visible.

**The board answers, with no hover:**
1. Whose turn it is, and whether I can still act (anything playable glows).
2. Life totals — who is closer to losing.
3. What the opponent's creatures threaten: power, toughness, keywords (Flying, Taunt, Cover…).
4. My mana: generic AND each colour, available vs spent this turn.
5. Which of my creatures can attack now (not summoning sick, not exhausted).
6. Hand / library / graveyard / exile counts on both sides.

**Board creatures are compact tokens, not full cards** (decided): art crop, name plate, big P/T,
keyword icons, colour pips. Full card on hover (PC) / long-press (phone).

**Mana is gems that deplete** (decided): generic as a crystal row (lit / spent), each colour as
small coloured gems beside it, spent ones dark. Colour is the game's main constraint, so it must
be glanceable. Card costs show the number AND coloured pips.

**Must survive a mechanic change:** keyword icons come from one table, not per-card art; token
anatomy has room for one more badge (counters, loyalty).

**Producibility:** card art is real MTG art (Scryfall, painterly realism) plus ComfyUI for gaps —
the style must sit next to that. One backdrop for every game for now (decided). Chrome is drawn
(9-slice), not generated.

## 2. Functional inventory — nothing may be lost in the revamp

| Element | Today | Revamp |
|---|---|---|
| Life, hand count, library count | `PlayerPanel` text | hero portrait + counts |
| Graveyard viewer (both sides) | text button → `GraveyardPopup` | clickable pile with count |
| Exile | **not shown** | clickable pile with count |
| Emblems | **not shown** | badge on the hero portrait |
| Mana available / spent | **not shown** | gem row |
| Cost colour pips on cards | **not shown** (number only) | pips under the cost |
| Turn + whose turn | `TurnLabel` | banner + END TURN state |
| End turn | small button | large button, phone-tappable |
| Attack targeting | click attacker → target | arrow from attacker to target |
| Spell / ability targets | panel modulate yellow | glow on valid targets |
| Choices (costs, modes) | `ChoicePanel` | restyle via kit |
| Card preview | `CardPreviewPopup` | hover / long-press |
| Event log | collapsible overlay + unread count | keep, restyle |
| AI inspector (F6), save scenario (F7), pause (Space) | debug overlays | keep, unstyled is fine |

**Why every frame looks the same (found 2026-10-06):** `MainFrame.PNG` is a dark saturated brown
(mean RGB 0.39/0.29/0.16) and the tint is MULTIPLIED onto it — a multiply only removes light, so
white/blue/black/red/green land on `#634823 #423c26 #46321e #613519 #47411c`, five browns. The art
and the untinted gold rules box also cover most of the frame. The wiring is correct; the texture
cannot carry a colour. Fix with the card restyle: a light, low-saturation frame texture (on MTG's
own instance — never edit the shared scene) and a tinted rules box, as both mockups draw.

## 3. Screen inventory

Board (mocked, this pass) → menu, deck select, draft, tournament restyled through shared kit
helpers afterwards (KIN's approach), each given a backdrop.

## 4. Directions — ChatGPT, one chat per direction, STYLE + SCREEN pasted together

### Style A — Painted Stage (MTGA)

```
STYLE: A polished digital fantasy card game in the style of MTG Arena. The play table is a single painted landscape illustration — a calm, sunlit ancient stone courtyard overgrown with moss, seen from slightly above — kept soft, slightly desaturated and low-contrast so the cards on top of it read clearly. UI elements are elegant and minimal: thin brushed-gold rims, translucent dark-navy glass plates, faceted gemstones for mana, soft glows on anything playable. Card art is realistic painterly fantasy illustration in the style of Magic: The Gathering card art. Typography: a classical serif for names, a bold sans-serif for numbers. Numbers are large and high-contrast with dark outlines so everything reads on a phone screen. Palette: warm parchment, muted stone, deep navy glass, gold accents; the five mana colours (white, blue, black, red, green) are the only saturated colours.
```

### Style B — Dark Arcane (Shandalar materials)

```
STYLE: A dark, old-school fantasy card game inspired by 1990s Magic: Shandalar, rebuilt with modern polish. The play table is a worn leather-and-oak wizard's desk under warm candlelight: edges fall into shadow, the centre where the cards are is clearly lit. UI is made of physical materials: tooled leather panels, aged parchment plates, riveted brass and iron frames with bevelled edges, engraved brass coins for counters, mana as glowing runestones set in brass. Card art is realistic painterly fantasy illustration in the style of Magic: The Gathering card art. Typography: an ornate serif for names, bold engraved numerals. Numbers are large and high-contrast with dark outlines so everything reads on a phone screen. Palette: deep browns, oxblood, aged gold, parchment cream, candle amber; the five mana colours glow as accents. Moody but never muddy: the play area stays well lit.
```

### Screen — the board (shared by both directions)

```
SCREEN: The main battle screen, mid-game. Landscape 16:9, 1920x1080. The game is played both on a PC monitor with a mouse and on a landscape phone with touch, so every element is large enough to tap. There are no game phases and no blocking, so there is no phase bar.
Layout, top to bottom:
- Top centre: the opponent's hero portrait (a dark sorceress) in a round frame, with a big life total 14. Beside it: their hand as 5 face-down card backs, their library as a small deck stack labelled 31, their graveyard as a small face-up pile labelled 6, and an exile pile labelled 1.
- The opponent's battlefield row: 3 creatures shown as COMPACT TOKENS, not full cards. Each token is an arched art crop of the creature with its name on a small plate, a big attack number bottom-left, a big health number bottom-right, small keyword icons along the top edge, and small coloured pips showing its colour. Tokens: "Vampire of the Dire Moon" 1/1 with deathtouch and lifelink icons (black); "Blood-Cursed Knight" 3/2 (black and white pips); a red goblin warrior 2/1. Behind them a slim row of their lands as small coloured chips: 3 Swamp, 2 Mountain.
- Centre: open table, with a glowing targeting arrow from my "Serra Avenger" token to the opponent's portrait — an attack in progress.
- My battlefield row: 4 compact tokens: "Serra Avenger" 3/3 with a flying icon (white); "Attended Knight" 2/2 with a first-strike icon (white); a "Soldier" token 1/1 (white); "Llanowar Elves" 1/1 (green), greyed and tilted sideways because it is exhausted. Tokens able to attack have a soft green glow. Behind them a slim row of my lands as chips: 3 Plains, 3 Forest.
- Bottom centre: my hero portrait (a young knight) with life total 18. Beside it my mana: a row of 6 crystals, 4 lit and 2 spent, plus small coloured gems: 2 white gems lit, 1 green gem lit, 1 green gem dark (spent). My library 28, graveyard 4 and exile 0 piles next to my portrait.
- Bottom edge: my hand of 5 cards fanned and overlapping, full card faces with the cost top-left (a number plus coloured pips), art, name, type line and rules text. Cards I can afford glow.
- Right side: a large END TURN button, and above it a small banner "Turn 7 — Your turn".
- Left side: a slim collapsible event-log tab and a settings cog.
- Floating at the right: an enlarged full card preview of "Serra Avenger" (cost 2 plus two white pips, 3/3, Flying, "Can't be cast before round 4").
```

Save results as `docs/mockups/mtg/chatgpt_<stage|arcane>_board.png` and **commit them**.

## 5. Scores (rubric in the `match-mockup` skill, 1–5)

Images: `docs/mockups/mtg/chatgpt_stage_board.png`, `chatgpt_arcane_board.png`; the four reference
screenshots beside them as `ref_*.png`.

| | Stage | Arcane | why |
|---|---|---|---|
| Reads at a glance | 4 | 5 | plain dark leather under the tokens beats a mossy courtyard; gold numbers pop |
| Sides confront | 4 | 4 | same layout; Arcane's lit centre focuses it a little more |
| Consistent | – | – | one screen each; test with a second screen in the winning chat |
| Tokens read at size | 3 | 5 | Arcane splits P/T into two big circles (HS); Stage's "3/3" plate is small |
| Producible | 4 | 4 | both one ComfyUI backdrop; Arcane's empty centre is easier to keep quiet |
| Buildable in Godot | 4 | 4 | both are 9-slice plates + circles; brass/leather needs more texture work |
| Tone | 4 | 4 | Stage = Arena-friendly; Arcane = Shandalar nostalgia, continues today's parchment cards |

**Both got right, adopt whichever wins:** lands as a slim chip row (not cards) — the biggest space
saving; arched tokens with keyword icons on top and a colour pip at the bottom; exhausted =
greyed + tilted; zone piles with counts beside each portrait; colour-framed hand cards; END TURN
right; preview card right.

**Both got WRONG — design it ourselves:** neither drew the two-track mana. Each merged generic and
coloured into one gem row, so the generic count is unreadable. A two-row or crystal-with-pips
design has to be drawn, not taken from a mockup.

### Consistency test — draft screen, sent in the Arcane chat

```
Same game, same art style, same UI kit, same palette and same card designs as the previous image. Now show a different screen:

SCREEN: The draft screen, mid-draft. Landscape 16:9, 1920x1080, played on both a PC monitor with a mouse and a landscape phone with touch, so everything is large enough to tap. Same leather-and-oak desk under candlelight as the table.
- Top left: a parchment banner "Pack 2 · Pick 4" and beneath it small text "7 AI drafters".
- Centre-left, most of the screen: the current pack laid out on the desk as 11 full cards in two neat rows (6 and 5), each card with its colour frame (cream for white, blue, near-black for black, red, green, gold for multicolour), cost top-left as a number plus coloured pips, art, name, type line, rules text, and power/toughness bottom-right on creatures. Cards: "Serra Avenger" (2 + two white pips, 3/3, Flying), "Pegasus Courser" (white, 1/3, Flying), "Primeval Titan" (6 + green pip, 6/6, Trample), "Frost Breath" (blue instant), "Ponder" (blue sorcery), "Cower in Fear" (black instant), "Vampire of the Dire Moon" (black, 1/1, Deathtouch, Lifelink), "Blood for Bones" (black sorcery), "Radha, Heart of Keld" (red and green, gold frame, 3/3), "Sylvan Ranger" (green, 1/1), "Distress" (black sorcery). One card is hovered: lifted, glowing, slightly enlarged.
- Right side, a tall tooled-leather panel "Your Pool — 18": a mana curve bar chart at the top (costs 1 to 6+, bars in brass), five colour gems with counts (white 7, green 6, red 3, black 1, blue 1), then a compact scrolling list of picked cards, each a slim row with cost pips and name, coloured by card colour.
- Bottom right: a brass-and-oxblood button "PICK" matching the END TURN button from the previous image.
```

## 6. Locked style

**Direction: Dark Arcane** (chosen 2026-10-06), locked after the draft screen
(`chatgpt_arcane_draft.png`) came back consistent with the board: same desk, candles, brass and
leather plates, oxblood button, card frames. Scored: Consistent 4.

**Palette** — sampled from the two Arcane mockups, not invented:

| Role | Hex | Where sampled |
|---|---|---|
| Desk wood (edges, darkest) | `#160b05` | draft, between cards |
| Desk leather (play area) | `#371a0c` / `#2d170c` | draft bottom / board centre |
| Panel interior (tooled leather) | `#292219` | draft pool panel |
| Brass (rims, bars, frames) | `#946536` | draft curve bars |
| Parchment (banners, plates) | `#9c7c55` | draft "Pack 2 · Pick 4" banner |
| Oxblood (primary button, life seal) | `#420c09` body, brass rim | PICK / END TURN |
| Gold text / numbers | `#f4e29b` | white gem highlight |
| Card frame W / U / B / R / G / multi | `#c8ae7f` `#4998c8` `#3c3735` `#a8402e` `#527669` `#9d8352` | draft card borders (red from the board's Lightning Bolt) |
| Card text box | cream `#d8cbb4`, tinted slightly toward the frame colour | draft rules boxes |
| Mana gems W / U / B / R / G (lit) | `#f4e29b` `#2f7fd0` `#4a4040` `#d0583a` `#3c9a52` | draft pool gems, brightened per "small symbols need brighter tints" |

**Type** (the project ships NO font today — the Godot default is half of the "default Godot" look):
**Cinzel** (OFL) for banners, buttons and big numerals ("END TURN", "PICK", life); **EB Garamond**
(OFL) for card names and rules text. Both from Google Fonts, imported into `MtgGame/Assets/Fonts/`.

**Card face** (hand, preview, draft): a real MTG layout. Name bar top with the cost on its RIGHT
(number + coloured pips); art; type bar; cream text box; P/T plate bottom-right. The whole border is
the card's colour. **The frame texture must be light, low-saturation grain** so the multiply tint
produces the colour (see §2's bug) — and `MtgCardThemeTests`' luminance floor must be re-thought,
since black becomes `#3c3735`.

**Board token:** arched top, art crop, name plate across the bottom of the art, keyword icons on the
top edge, colour pip at the bottom centre, **power and toughness in two separate big circles**
left/right (Hearthstone). Exhausted = greyscale + tilted. Can attack = green glow; valid target =
gold glow.

**Lands:** a slim row of round chips behind each side's tokens, symbol of the colour it makes.

**Hero:** round brass-framed portrait, life in an oxblood seal bottom-right, emblems as small badges.
Zone piles (library, graveyard, exile) beside it, each a small stack with a brass count plate,
clickable.

**Icons:** game-icons.net through the `add-icon` skill, recoloured to the gem/gold palette — the
mana symbols (sun, drop, skull, flame, tree) and one per keyword: Flying, First/Double Strike,
Deathtouch, Lifelink, Trample, Reach, Taunt, Cover, Indestructible.

**Mana display — designed here, neither mockup got it right:** beside the portrait, a brass bar
with the GENERIC track as amber crystals (lit = available, dark = spent) plus a "4/6" numeral for
phone, then one runestone per colour the player has, each with its remaining count; a stone with 0
left goes dark. To be checked in a capture before it is called done.

**Backdrop:** one ComfyUI desk 1344x768, plain lit leather centre, props (candles, books, coins)
only at the edges — the edges are what a phone crops.
