---
id: LIM-1
title: Sealed for Reality Fracture — authentic Arena packs, an AI build, played on the table
status: in-progress
area: LIM
created: 2026-10-07
---

## Why

Two of the use cases from the first interview (MC-1): **a sealed pool to builds** and **draft vs
bots** — the reason for the whole engine is building better limited decks before an Arena direct.
The FRA Arena Direct is **Bo1 sealed**, so sealed comes first; draft is LIM-2. It also unlocks the
last FRA card, **Extrapolate the Impossible**, whose "from outside the game" needs a sideboard.

## Requirements

- [ ] A sealed pool is 6 FRA packs, each an **Arena** play booster: the paper play booster with
      the foil slot as a second non-foil wildcard (same rarity odds)
- [ ] Packs follow FRA's real collation, from MTGJSON's play-booster data (232 pack variants, exact
      weights): 6 commons, 1 uncommon, 1 common/uncommon, the echoed-pair block (a whole pair of
      one rarity + one card from a DIFFERENT pair), 1 rare/mythic, the wildcard, 1 land
- [ ] Special Guests replace a common in 1 pack in 55 — once all 10 are scripted (MC-3)
- [ ] Commons follow every stated rule and nothing invented: at least 4 of the 5 colours among a
      pack's commons (Wizards' Play Booster Q&A), and no card twice from one sheet (MTGJSON: none
      of FRA's play sheets sets `allowDuplicates`; across sheets a repeat can happen — the two
      wildcards, and the common/uncommon slot, can match a card the pack already holds)
- [ ] The rates are checked by a test over many packs, against the published odds
- [ ] A deck builder on MagicTable: the pool, 40 cards, basics free, Arena-style
- [ ] A suggested deck — what the AI builds from the pool; the same builder makes the AI
      opponent's deck from ITS OWN pool
- [ ] Play the built deck against that AI opponent on the table, Bo1
- [ ] Extrapolate the Impossible plays, with the sideboard (the pool's unplayed cards) as
      "outside the game"

## Decisions

- 2026-10-07, Shayne: Bo1 only for now; Bo3 and sideboarding maybe later.
- 2026-10-09, Shayne: after the UI, his choice between this and a search AI (AI-2).
- 2026-10-20, Shayne: **sealed first**, draft afterwards (LIM-2).
- 2026-10-20, Shayne: **Arena packs** — what an Arena Direct opens. Wizards: "in MTG Arena there
  will be two non-foil wildcard slots" where paper has a foil one, mechanically identical
  contents. Arena's land-slot and Special Guest handling are unpublished, so they mirror paper.
- 2026-10-20, Shayne: **collation = the official rules only.** Paper packs use sequential
  collation (a pack's commons are consecutive cards of fixed print runs — Lethe's Collation
  Project reverse-engineered it through MKM), but FRA's runs are unpublished; synthesized runs
  would invent the neighbours. Revisit if opened-pack data for FRA ever appears.
- 2026-10-20, Shayne: deck building = **build your own + a suggested (AI) build**; AI opponents
  build their own sealed decks with it. Several AI builds ranked by sims: out of scope here.
- 2026-10-20, Shayne: **Special Guests: script all 10** — story MC-3, before pools contain them.
- 2026-10-20, Shayne: **MagicTable first** (the builder is a UI); generator and AI builder are
  plain C# under it, tested.
- 2026-10-20, Shayne: the builder ranks by **17lands' GIH WR** for FRA — the engine's measured
  values put every planeswalker last and climb with cost. Sealed's rate, else Premier Draft's
  (each needing 200 games in hand), computed from the PUBLIC game data, the only 17lands data
  permitted outside the site (DATA-1). The ratings are any table, because custom and unreleased
  sets need the engine's own — story DATA-2.
- 2026-10-20, Shayne: **two colours, no splash** to start; a splash only if builds show the need.

## Tasks

- [x] LIM-1.1 Interview the open questions, then split into stories (2026-10-20: LIM-2, MC-3)
- [x] LIM-1.2 Import FRA's play-booster config from MTGJSON (`tools/`) into an embedded
      `MagicCore/Sets/FRA.boosters.json` — sheets by card NAME, treatments folded onto the FRA card
      (1ca628d)
- [x] LIM-1.3 The pack generator (seeded): Arena packs, colour and duplicate rules, rate test
      (`Limited/Boosters.cs`, `BoosterTests`; 1ca628d)
- [x] LIM-1.4 The sealed AI builder: pool → 40, ranked by 17lands' GIH WR (`Limited/SealedBuilder.cs`,
      `CardRatings.cs`, `tools/import_17lands.py`, `SealedBuilderTests`; 45c3a57)
- [ ] LIM-1.5 MagicTable: open a pool, build or take the suggested deck, play the AI's build —
      built (`SealedSession`, `SealedScene`; Sealed on the deck select), captured, NOT yet played
      by hand: Shayne's playtest ticks it
- [x] LIM-1.6 Extrapolate the Impossible, the sideboard as outside the game (`ZoneType.Sideboard`,
      `MagicRules.WithSideboard`, `RevealTwoFromOutsideTheGame`; the table passes both pools'
      sideboards; `FraOutsideTheGameTests`, 6/6 revert-checks; uncommitted, 2026-10-21)

## Next

**Playtest LIM-1.5** (`./Run-Godot.ps1`, MagicTable → Sealed): build by hand, take the suggested
deck, Start, play — and cast Extrapolate the Impossible if the pool has it (your sideboard is the
pool's unplayed cards; the AI chooses which you get). Every "looks off" becomes a task; once it plays
right, LIM-1 is done. The session lives in `SealedScene._session` (static), and Escape from a sealed
game returns to it (`TableSetup.Back`).
Traps: the collation data is by NAME (treatments folded); `GuestsPlayable` stays false until
MC-3; the colour rule redraws the common sheet (~30% of raw draws break it — measured 2026-10-20).
Re-run `python tools/import_17lands.py fra` as 17lands' public files update (weekly or so).
Not yet followed: the AI's guess at a hidden hand (`Hidden.BoosterWeighted`, AI-3.5) still weights
by old draft boosters (10 C / 3 U / 1 R, 1-in-8 mythic) — point it at these sheets once a sealed
opponent's pool is what it guesses from.

## Log

- 2026-10-16: created from the interview's use cases and the old handoff's "Later" list.
- 2026-10-20: researched FRA collation (Wizards' Collecting article, MTG Scribe fact sheet,
  MTGJSON, Lethe); interviewed; split — draft to LIM-2, Special Guests to MC-3. Status → ready.
- 2026-10-20: LIM-1.2 + 1.3 built — 172 pack variants (232 in MTGJSON; borderless pairs folded),
  totals and sheet sizes match the official counts (71 C, 43 U, 50+20 R/M, 270 in the wildcard).
- 2026-10-20: LIM-1.4 — the first ratings came from 17lands' old `card_ratings/data` endpoint and
  were wrong (a frozen snapshot: 139 cards unrated); Shayne caught it against the site. Now
  computed from the public game data: 295 cards, 11 under 200 games in both formats (4 of them
  Special Guests). Two built pools looked like real decks (23 spells, 17 lands, duals and hybrids
  right).
- 2026-10-21: LIM-1.6 — every FRA card plays (285/285). The sideboard zone exists only when given
  one, so no existing game, golden replay or recorded position moved for it; the random-play
  goldens moved only where Extrapolate joined a deck (30 every-card games, ~80 card replays).
- 2026-10-20: LIM-1.5 built. Your pool from the seed, the AI's from seed + 1; captured at 1280x720:
  the pool by colour as card images, a suggested deck as 23 + 17 = 40 (11 Island, 5 Mountain, a
  U/R dual). Clicks not verified by the capture — that is the playtest.
