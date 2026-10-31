---
id: MC-5
title: Star Trek (TRK) plays in MagicCore — sealed and draft before Arena's day 1
status: ready
area: MC
created: 2026-10-30
---

## Why

Shayne, 2026-10-30: a Star Trek set comes out on Arena on **November 10, 2026** (prerelease November 6–12,
tabletop November 13) — "it might be worth implementing up front to get ahead of everything". Getting
ahead means: **"I can practice sealed and draft before Day 1 on arena, get a feel for all the cards and
stuff."**

Scryfall already lists the set (2026-10-30): code `trk`, 311 distinct cards (96 common, 110 uncommon,
80 rare, 25 mythic). New mechanics: Station (17 cards), Strange new worlds (13), Face a dilemma (8);
also Typecycling, Investigate and Kicker, which MagicCore may not have yet (grep MtgCore first). Most
keywords exist already (surveil, landcycling, cycling, treasure, mill, equip).

## Requirements

- [ ] TRK sealed and draft play on MagicTable before November 10 — the cards Shayne will open at the
      prerelease, scripted, as FRA's are (`/card-batch`)
- [ ] Every TRK card plays by Nov 10 — rares and mythics too (Shayne, 2026-10-31)
- [ ] A card's functionality is decoupled from its printings: a reprint is scripted once, its art,
      rarity and collector number are per set (Shayne, 2026-10-31 — sets and cubes alike)
- [ ] The draft bots pick by our own ratings: card win rates pre-simulated against bots (DATA-2), plus
      colour lean — there is no 17lands data before release (Shayne, 2026-10-31)

## Decisions

- 2026-10-30, Shayne: TRK goes into the CURRENT sprint (sprint 3), ahead of the cube (MC-4); "get ahead"
  is practising sealed and draft before Arena's day 1. Not the Commander product (TRC) unless asked.
- 2026-10-30, Shayne: not started in the session that planned it — that session finishes the AI fixes
  (AI-17.6); the survey is the first task of a fresh session.

- 2026-10-31, Shayne: survey first — Scryfall data, the new mechanics, how many cards today's pieces
  build — and the plan reviewed before anything is built. **Reprints are not re-implemented**: a card in
  two sets is one card (one script, one rules text); only its printing — art, rarity, collector number —
  differs by set.

- 2026-10-31, Shayne (MC-5.1's questions): **decouple a card's functionality from its printings, up
  front** — future sets and cubes face it too. **Every card by Nov 10, rares and mythics included** — the
  budget fits now that each batch is a fresh session (this set is the test of that). **Bot ratings are
  our own: card win rates pre-simulated against bots** (DATA-2's method), accepted as not fully accurate.

- 2026-10-31, Shayne: **Stardates (SDS) last, but still aimed at Nov 10** — 30 reprints (mythic,
  borderless, ~1 pack in 25 in place of a common), TRK's Special Guests. Until they play, that slot opens
  a common.

- 2026-10-31, Shayne (MC-5.5's questions): **each card in a deck carries its set** — "this matches how
  MTGA and MTGO work, where you can actually choose which art to use in your deck. In draft and sealed
  this would obviously just mean that it would be the art of whatever set you are drafting." **Rarity
  and collector number split out of `CardData`** (a `CardPrinting` per set). The registry is named
  **`CardRegistry`**.

- 2026-10-31, Shayne (MC-5.3 batch 1's questions): **V'Ger's "look at target opponent's hand" goes to the
  log for now**; the table showing known cards face up in the opponent's hand is its own story (TBL-6).
  **A modal activated ability is a real "choose one"** (Tea, Earl Grey, Hot), never two abilities with one
  cost: dilemma cards count mode choices.
  Shayne, the same day: the table showing a modal activation's modes as separate plays is fine, as long as
  dilemma counts it — so DILEMMA (MC-5.2) is raised where modes are chosen: the cast, a trigger's
  `ChooseModesChoice`, AND an activation (`ActivateAbilityAction.Modes`).

- 2026-10-31, Shayne (MC-5.2 Station's questions): **Zefram Cochrane waits** for the tier-3 one-offs (look
  at the top any time, cast an artifact from the top, Spacecraft enter with two more counters). **The AI
  values station progress by a simple formula, to iterate on** — "you want to put counters on your
  spacecraft, but you do need to account for opponent counter attacks if you tap your creatures." **The
  tapped creature's last-known power is a snapshot taken as it is tapped**: "shortcutting LKI … not worth
  a full engine overhaul for what is probably a very rare situation overall. If this situation comes up
  more in playing then we can reconsider doing full LKI" (noted in `DesignNotes.md`).

Open questions:
- Booster collation: MTGJSON has TRK's cards but NO booster data yet (checked 2026-10-31); a provisional
  collation from Wizards' published slot odds is proposed (Collecting Star Trek, magic.wizards.com).

## Tasks

- [x] MC-5.1 The survey (a fresh session): import TRK from Scryfall (`tools/import_scryfall_set.py`), tier
      every card by what the engine needs, the new mechanics' rules read in full, a batch plan sized to
      reach sealed and draft by November 10
- [ ] MC-5.2 The new mechanics (Station, Strange new worlds, Face a dilemma, and whatever the survey finds)
      — Station done 2026-10-31 (batch 2): `TrkCards.Station`, a creature at its threshold read live by
      `Card.Is`, the tapped creature a real choice (`ActivateAbilityAction.TapId`), the AI's station term
- [ ] MC-5.3 Commons and uncommons in large batches, then rares and mythics — batch 1 done 2026-10-31:
      the 88 tier 0–1 C/U not waiting on STATION or ANY-COUNTER (re-tiered: 4 tier 3, 6 tier 2, list in
      `TrkCoverage.md`); new pieces: modal activated abilities, `PutSelfOnBottomCost`, `AddManaOfAnyOneColor`,
      `LookAtHand`, `Count.Plus`; FraCards' shared helpers moved to `Sets/Scripting.cs`. Batch 2 done
      2026-10-31: the 12 STATION C/U waiting on nothing else (Stray from History re-tiered to 2); new
      options: `Is.Tapped`, `RevealUntilOntoBattlefield.HowMany/Tapped`, a spell's cost change seeing its
      targets. Waiting: Hemmer (TAPPED), Rutherford (LOOK-TAKE), Zefram Cochrane (tier-3 one-offs)
- [ ] MC-5.4 TRK boosters, sealed and draft on the table, bots without 17lands
- [x] MC-5.5 FIRST, before any card: a card's functionality decoupled from its printings — one registry
      by name across sets (one script per card; a test fails on a name scripted twice or a reprint whose
      rules text differs), printing data per set (art, rarity, collector number); `FraCards.Get`'s ~50
      call sites moved to it. Done 2026-10-31: `CardRegistry`, `CardPrinting`, `Card.Set`; boosters,
      ratings and drafts by set code; art in `Art/<set>/` first; `TrkCards` with the TRK tokens and the
      10 Planet duals; `TrkCardSheet.md`
- [ ] MC-5.6 LAST, aimed at Nov 10: the 30 Stardates (SDS) — imported as TRK's guests
      (`GUESTS` in `tools/import_scryfall_set.py`, as FRA's SPG), scripted once each, into the collation

## Next

**MC-5.3 batch 2 is done (2026-10-31): Station plays, 116/311** — batch 1's 88 plus the 12 STATION commons
and uncommons waiting on nothing else. Station is `TrkCards.Station(n, keywords)` (Cookbook; the rule in
`magic-mechanics.md`); the rare Spacecraft with a second threshold use `new AtLeast(Count.ChargeCountersOn(
Ref.Source), n)` on that ability. The AI stations (checked with the search on a simple board) by a first
formula Shayne wants iterated (`DesignNotes.md`), and the snapshot-at-tap LKI is a noted shortcut.
**Next: the rest of the Nov 4–6 row — the small pieces (beam me up, strange new worlds, dilemma, Federation,
power 8, becomes tapped — Hemmer, Sulu —, look-take — Rutherford —, counters), Sagas, Kicker, in a NEW
session.** Every batch starts in a NEW session (`/card-batch` step 0) and ends with its usage row
(`tools/usage_report.py --log`, `docs/findings/card-batch-usage.md`) — this set tests that workflow.

Read: this story, `MagicCore/TrkCoverage.md` (every card's tier, what it waits on, what to reuse —
the survey), `/card-batch`, `MagicCore/AbilityCatalog.md`, `MagicCore/Cookbook.md`. Card text:
`python tools/magic_set.py text --set TRK "Name"`. Rules for the new mechanics: Wizards' "Star Trek
Release Notes" (magic.wizards.com/en/news/feature/star-trek-release-notes) — Station's are Edge of
Eternities'. Booster odds: "Collecting Star Trek" on the same site. MTGJSON has no TRK boosters yet
(2026-10-31) — re-check before building the provisional collation.

The plan, one or more fresh sessions per row, every card by Nov 10:

| Dates | Work |
|---|---|
| Nov 1 | **MC-5.5** the card registry (functionality decoupled from printings); TrkCards; tokens; the 10 Planet duals |
| Nov 2–3 | Tier 0–1 commons, then uncommons (~118) |
| Nov 4–6 | Station; the small mechanics (beam me up, strange new worlds, dilemma, Federation, power 8, becomes tapped, counters); Sagas, Kicker → ~185 of 206 C/U |
| Nov 6–7 | Provisional boosters, TRK on Sealed and Draft (MC-5.4); DATA-2's rating method checked on FRA against 17lands |
| Nov 7–9 | Rares and mythics: ~49 tier 1–2, then ~56 tier-3 one-offs in batches of ~14 |
| Nov 9–10 | The TRK rating run (ask first: long), bots on it, a draft to check; the Stardates (MC-5.6) last |

Traps: scripts go in `Sets/Trk/TrkCards.cs`; a reprint FRA already scripts is never scripted again
(only the basics overlap). A FraCards helper TRK needs (`When`, `Spell`, `Target`, `Equip` …) moves to
a shared file then — never copied (Cookbook). TRK art isn't fetched yet: `python tools/fetch_set_art.py
trk` (reprints land in `Art/trk/`), then a Godot import. `SealedSession.Set` is a const "FRA" until
MC-5.4. MagicCore's `CLAUDE.md` is at its 200-line budget: move content down rather than add.

## Log

- 2026-10-30: created — Shayne moved TRK into sprint 3, ahead of the cube; release dates and Scryfall's
  card count checked the same day.
- 2026-10-31: MC-5.1 survey — TRK imported (311), every card tiered, release-note rules read, MTGJSON
  checked (no TRK boosters yet), Stardates (SDS) found; plan put to Shayne. Counts are in Next.
- 2026-10-31: requirements changed (Shayne) — every card by Nov 10, not commons and uncommons first with
  rares if time; printings decoupled from functionality; bots on pre-simulated ratings, not colour lean
  alone.
- 2026-10-31: usage per batch is now logged — `docs/findings/card-batch-usage.md` (the survey: 1.9M).
- 2026-10-31: MC-5.5 — the card registry across sets, Shayne's three answers in Decisions; 21 new tests,
  13/13 revert-checks caught; TRK 16/311 playable.
- 2026-10-31: MC-5.3 batch 1 — 88 cards (48 C, 40 U), TRK 104/311; 11 tests for the tier 2–3 cards,
  11/11 revert-checks caught (one only after a test was strengthened); probe: 150 games, no crash, every
  tier 2–3 card used. TBL-6 created (known hand cards on the table).
- 2026-10-31: MC-5.2 Station + MC-5.3 batch 2 — Shayne's three answers in Decisions; 12 cards (7 C, 5 U),
  TRK 116/311; 15 tests, 16/16 revert-checks caught (two only after a fix: a redundant clamp deleted, a
  test made to ask WHO chooses); probe: 150 games, no crash, every card used; the search AI stations on
  a simple board; bench +2.1% (noise); golden replays regenerated (only games dealing a new card moved).
