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

**Why every frame looked the same (found 2026-10-06, first diagnosis WRONG):** the first reading
was "a light tint multiplied onto a dark brown texture stays brown". The real cause: the card
shader (`new_holo.gdshader`) built its output from the texture and never multiplied by `COLOR`,
so every SelfModulate tint on a card part was dropped — frame-by-colour had never rendered at all.
Fixed with one line in the shader; MTG also supplies a near-white frame so the tint is the colour.

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

**Lands: there is no land row.** A played land becomes permanent mana and the card goes to exile
(`PlayLandAction`), so lands are never on the battlefield — the mockups' land chips came from our
own prompt. The MANA display is the land representation; the exile pile is mostly lands.

**Hero:** round brass-framed portrait, life in an oxblood seal bottom-right, emblems as small badges.
Zone piles (library, graveyard, exile) beside it, each a small stack with a brass count plate,
clickable.

**Icons:** game-icons.net through the `add-icon` skill, recoloured to the gem/gold palette — the
mana symbols (sun, drop, skull, flame, tree) and one per keyword: Flying, First/Double Strike,
Deathtouch, Lifelink, Trample, Reach, Taunt, Cover, Indestructible.

**Mana display — designed here, neither mockup got it right:** `ManaBar`, beside the portrait on
both sides: the GENERIC track as amber crystals (lit = available, dark = spent, at most 10) plus a
"4/6" numeral for phone, then one gem per colour the player's lands make with what is left this
turn; a colour with none left goes dark. Lands never stay in play, so this bar is the only place a
player's lands are seen.

**Backdrop:** `MtgGame/Assets/UI/backdrop.jpg` — tooled leather mat with a gold medallion on the
centre line between the two sides (chosen 2026-10-06). Prompt + seed in
`docs/mockups/mtg/backdrop_prompt.json` (`mat`, variant 2, seed 355171413, DreamShaper XL Turbo).
Every screen gets it through `MtgUiStyles.AddBackdrop`.

## 7. Build log

**Step 1 - fonts + kit (2026-10-06).** Cinzel + EB Garamond (OFL) in `MtgGame/Assets/Fonts/`;
Garamond 600 is the project theme's default font. `tools/make_mtg_ui.py` draws plate / plate_hover /
plate_disabled / banner / button(+hover, disabled); `MtgUiStyles` serves them as 9-slices under the
old helper names, so every panel and button restyled without touching its call site. END TURN uses
`StylePrimary`. Clear colour = desk wood until the backdrop.
Captured board, draft, menu: fonts and plates land everywhere. Seen and deferred: the battlefield
plates stretch their gloss into a smear (they go in step 4 - the board is open table); Garamond
rules text on the OLD card face is cramped at draft size (step 2); the menu is the generic
"Game Title" scene (step 7).
**Art gap closed:** 121 of 396 CSC cards and four basics had no art; the scraper had simply not been
re-run for CSC. Re-run (and its Retry-After: 0 backoff fixed): every CSC card has art except the
Clue and Human tokens.

**Step 2 - card face (2026-10-06).** `MtgCardMapper.Face` is the one factory (five screens had their
own copy). It passes MTG's textures (`card_frame`, `card_name`, `card_rules`, `card_cost`) and a
`Details.Decorate` hook (new, game-agnostic) that `MtgCardSkin.Apply` uses on the instance: cost
disc top-right in Cinzel with a gem per pip beneath (generic and colour tracks both visible), name
left-aligned and stopping short of the disc, dark ink on cream with outline and shadow zeroed, a
leather P/T plate, foil off. `FitNameToBox` now fits the label's real width, which fixed names
clipped under the cost disc ("night of the White Orchid").
Found on the way: the shader bug above; the tribe-tinted name plate (never seen before the fix)
read as a second colour claim and was dropped; the hand card scene ships with foil ON, which
washed the cream face in rainbow noise.

**Step 3 - backdrop (2026-10-06).** 13 ComfyUI candidates. Top-down "desk with props" prompts put
a book or clutter in the centre every time; an angled desk gave the most mood but drew an open
BOOK whose seam ran down through both battle rows. A "single seamless mat" prompt (negatives:
book, spine, seam) removed the seam and the candles with it. Three were captured IN the board:
the medallion mat won — the medallion marks the confrontation line, the dark border survives a
phone's crop, the bright-bordered plain mat put gold exactly under the side panels and END TURN.
The battlefield panels are now empty styleboxes: the backdrop is the table.
Seen and deferred: the turn label sits unreadable on the gold border (step 4: banner + outline);
the candlelight mood could return as a warm vignette, no new art needed.

**Step 4a - board tokens (2026-10-06).** `BoardToken` (built in code): art clipped to an arch
(`token_mask` + ClipChildren), a light rim tinted by `MtgCardTheme.FrameColor`, a parchment name
plate, power and toughness in two discs (toughness shows what is LEFT, red when damaged; a
planeswalker shows loyalty in the right disc), keywords as a text strip from
`MtgCardMapper.KeywordNames` (the rules text's own list; icons in step 6). States: exhausted =
grey + tilted 12 degrees, summoning sick = dimmed, glows for target / selected / cost / flashback
and a soft green "can attack". `BattlefieldZone.AsTokens` switches the two battlefield zones to
tokens; the graveyard viewer keeps full cards. The opponent's row hugs the centre line.
`-GameArgs '--autoplay=N'` (new) makes the board capturable mid-game.
Found: lands never reach the battlefield (above) — 4b, the land row, was dropped and its code
deleted. A ScrollContainer only stretches its child vertically if the child asks (ExpandFill).
Deferred to 4d: an attached Equipment/Aura peeks out behind its host token and is nearly hidden;
the Clue and Human tokens have no art (a dark fill stands in).

**Step 4c/4d - layout, portraits, piles (2026-10-06).** Done together: building the HUD inside the
old left rail and then moving it would have been the work twice. `BoardUI.tscn` is now
[opponent strip][opponent tokens][your tokens][your strip], hand below, END TURN + parchment turn
banner bottom-right (thumb reach on a phone). `PlayerHud` replaces `PlayerPanel` and the text
graveyard buttons: round portrait (ComfyUI, knight v2 / sorceress v1, prompts in
`docs/mockups/mtg/portrait_prompts.json`) in a brass ring that lights on its owner's turn and turns
gold as a target, life on an oxblood seal, the opponent's hand as card backs, and `ZonePile`s for
library / graveyard / exile (top card's art, brass count, click to view). Exile is NEW and
read-only; it leaves out lands, which are exiled as they become mana. The prompt toast moved to the
centre line — at the top it covered the opponent's portrait, the thing it most often asks you to
target. Full rows shrink their tokens to fit (down to 55%).
Found: an HBox child fills the strip's height unless ShrinkCenter (slivers, bars); `FitRow` first
ran before layout at width 0 and shrank every token to the minimum — it now runs on resize.
Deferred: an attached Equipment/Aura is still nearly hidden behind its host; the mana pips are
the old row until step 5.

**Step 5 - mana (2026-10-06).** `ManaBar` as above, in both HUD strips; `crystal.png` is a white
faceted lozenge tinted per state, colour gems reuse `gem.png` with `MtgCardSkin.GemColor`. The
hand's "playable" glow now asks the engine (`CostEngine.ValidateManaPayment`, both tracks) instead
of comparing generic mana in the UI — it lit a blue card for a player with only White.

**Step 6 - icons (2026-10-06).** 19 game-icons.net symbols in `MtgGame/Assets/Icons/` (credited in
the new root `CREDITS.md`): five mana colours and every keyword `KeywordNames` emits, plus Cover.
`MtgSymbols` is the one registry — icon, tint, and a one-line meaning written for THIS engine
(no blocking: Flying = "only Flying or Reach can attack it"). Tokens show keywords as symbol discs
along the top with the meaning on hover (MouseFilter Pass); card pips and mana-bar gems carry the
colour's symbol in near-black ink. Found: a spent gem's dark symbol on a dark gem hid WHICH colour
was spent — a spent gem keeps a dim, readable symbol. A capture cannot hover, so the tooltips are
wired but unseen until played.

**Step 7 - the other screens (2026-10-06).** Kit helpers `MtgUiStyles.Title` (Cinzel, outlined)
and `StyleChoice` (leather plate, PARCHMENT with ink when selected — one selected state everywhere)
plus `AddBackdrop` on every screen.
- **Title** (`Project/MainMenu`): "Arcane Duel", plates for Start Game / Draft / Quit; Options
  removed (it did nothing). Mouse, touch and keyboard drive one selection.
- **Deck select**: two-column grids per side. Found: one column of 14 decks pushed START BATTLE
  below the bottom of a 720p window — the screen could not start a game. Start is now the oxblood
  button pinned bottom-right; the selected deck's tagline shows under its grid at 22px (it was
  11px, under three of the fourteen).
- **Draft**: Cinzel header, pool panel with a colour count row (`ManaBar.ColourCount`, shared
  with the mana bar). The pool panel is 270 wide on purpose: at 320 the pack wrapped to five a row
  and its third row fell off the screen.
- **Tournament**: backdrop, title, oxblood action button, readable status/hint (built from code;
  it needs a live tournament, so not captured).
Tiny player-facing text raised: choice-panel hint 13->17, deck-list hint 11->15.
The mockup's PICK button was NOT added — clicking a card picks it, one click fewer.

**Phone playtest (2026-10-06), confirmed fixed on the device.** (1) A draft tap picked the card
before it could be read — a touch press is hover and click at once. A tap now SELECTS (gold
outline, big preview, PICK lit); a second tap or PICK picks. Verified with the new `--tap` flag.
(2) Hand cards lifted to ~half the screen, unreadable on a phone; HoverScale could not just grow
(it also scales a DRAGGED card), so on mobile the hovered hand card also shows in a ~90%-height
preview, and every preview uses `MtgUiStyles.PreviewScale`. (3) The three CMB decks shipped 17
Plains (their builder predated colour and skipped `ManaBase`); fixed and pinned by
`ManaBaseTests.EveryRegisteredDeck_ProducesTheColoursItsSpellsNeed`.

**Attachments (2026-10-06).** An Aura or Equipment on a token is a 44px round art chip down the
host's right edge (portrait mask + ring, no new art), its own hover/click target with the
attachment's id; gold ring when it is a valid target. A Myr Enforcer read 13/6 with nothing on
screen saying why — the Plating hid behind it. `--decks=A,B` added to make such boards repeatable.

**Checkboxes (2026-10-16, TBL-2.4).** Godot's default CheckBox icons were a faint dark outline that
vanished on the leather, and a ~10 px tick beside 24 px text. `check_off` / `check_on` (a small leather
plate with a brass rim; the same with a gold `#f4e29b` tick) come from `tools/make_mtg_ui.py` and sit in
`mtg_theme.tres`, so every CheckBox in the project gets them. Shayne chose box icons over plate toggles.
