---
id: MC-10
title: The cube in limited - sealed and draft with LSVPower, on the table
status: in-progress
area: MC
created: 2026-11-23
---

## Why

Every card of the cube plays (MC-4, 528 of 528) and MC-9 is fixing how the AI uses them. Shayne, 2026-11-23: "I
actually want to test out either drafting or sealed with the cube. I think we have gotten far enough where I should play
it … if anything is needed to get it working properly first (i.e. art?) then make sure that is properly implemented."

Found missing: the cube had no packs (`LSV.boosters.json`), no ratings (the AI's sealed decks, the draft bots and the
deck builder's suggestions all rank by one), was not on the deck select's Set button, printed no basic lands (a deck
built from it could not be set up), and the card-value sandbox crashed on Pest Infestation. The art was already there:
all 528 cards' crops and full cards (`fetch_set_art.py lsv`); a cube's tokens fall back to the placeholder (a ceiling).

## Requirements

- [x] Sealed and draft open the cube from the deck select (Set: LSVPower cube).
- [x] Packs of 15 from the cube's whole list; **singleton by default** — no card twice in a pool or a draft — with
      independent packs as an option ("I'd like an option for both just for fun. But singleton is usually the default").
- [x] A deck built from the cube is set up with basic lands; a game plays through.
- [ ] Ratings Shayne trusts for the cube — the source still being discussed (Decisions).
- [ ] Sealed first (Shayne), then draft — each checked on the table end to end.

## Decisions

- 2026-11-23, Shayne: sealed first; singleton by default with an option for independent packs.
- 2026-11-23, Claude: packs are DATA, the booster file every set has (`tools/build_cube_boosters.py lsv`: one sheet,
  every card once, 15 a pack, `"singleton": true`); one deal draws every pack from what it hasn't dealt
  (`Boosters.Packs`) — a sealed pool's six, a draft's 24 (`Draft.New`). FRA and TRK deal exactly as before (a test).
- 2026-11-23, Claude: a set that prints no basic land gets its first printing (`CardRegistry.Get`) — one place, not
  the nine callers that build decks from a set.
- 2026-11-23, **ratings — open.** Three sources, discussed with Shayne:
  - engine-measured (`--interim-ratings=lsv`: rarity median ± the card-value sandbox's rank) — run as a PLACEHOLDER so
    the screens open: it scores a card by the turn it is cast, so the fatties top it (Emrakul, the Aeons Torn 1st;
    Worldspine Wurm, Archon of Cruelty, Griselbrand top 10) and the Power sits low (Black Lotus ~220th, Ancestral Recall
    452nd, Time Walk 503rd of 528); the builder takes the best 23 with no curve, so the AI's decks suffer;
  - self-play (DATA-2, `--sim-ratings`) — on FRA it agreed with 17lands far less (ρ 0.10–0.20) than the interim table
    (0.41–0.56): it measures the AI's skill with a card as much as the card (docs/findings/limited.md); in a cube the
    AI's gaps are bigger. Claude: an experiment on the AI, not the table;
  - CubeCobra's human pick data — Shayne exporting the cube's analysis from the browser (CubeCobra refuses automated
    fetches: 403). Claude's lean: it, with engine values for any card it lacks.
- 2026-11-23, Claude: the AI's search prices a HELD card from the values table, which has FRA and TRK only
  (`CardValues.TableSets`) — LSV cards in hand are priced flat. Adding LSV changes how the AI plays the cube (and its
  golden replays): a task of its own, not slipped in here.

## Tasks

- [x] MC-10.1 (done 2026-11-23) The cube's packs, singleton by default (`Boosters.Packs`, `Config.Singleton`,
      `LSV.boosters.json` from `tools/build_cube_boosters.py`); the Set button's LSVPower cube; "Singleton packs" on
      the deck select (only for a singleton set); basic lands from their first printing; Pest Infestation's "up to X"
      answer with nothing to target (`GreedyAi`, it was null — the sandbox crashed). A placeholder `LSV.ratings.json`.
- [ ] MC-10.2 The ratings: Shayne's CubeCobra export imported (a script), engine values for what it lacks — or the
      source he picks.
- [ ] MC-10.3 Shayne plays cube sealed on the table; his notes become tasks here or in MC-9.
- [ ] MC-10.4 Draft on the table (the screens take the cube already; checked end to end).
- [ ] MC-10.5 LSV in the values table the search prices held cards by (`CardValues.TableSets`) — measured, as an
      evaluator change: the suite, the golden replays, a match on cube decks.
- [ ] MC-10.6 The cube's tokens' art (a cube has no token set: `fetch_set_art.py` fetches none).
- [ ] MC-10.7 The engine's card values discounted by mana cost (Shayne, 2026-11-23: "they should be properly
      discounted by mana cost in the future") — today a 15-mana Emrakul is the cube's best card and Time Walk 503rd of
      528. Options and their cost in DesignNotes ("The card-value sandbox is NOT discounted by mana cost"); a change to
      `CardValues` moves FRA's held-card prices too, so it is measured as an evaluator change.

## Next

MC-10.2 — the ratings, once Shayne's CubeCobra export is in. To play now:
`./Run-Godot.ps1 MagicTable/DeckSelectScene.tscn -GameArgs '--set=LSV'`, then Sealed (the ratings are the placeholder).

## Log

- 2026-11-23: opened (Shayne) — the cube in limited. MC-10.1 done: packs, the Set button, singleton option, basics,
  Pest Infestation's answer; a cube sealed game played through by test; ratings open.
