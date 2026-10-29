---
id: LIM-2
title: Draft Reality Fracture against bots
status: in-progress
area: LIM
created: 2026-10-20
---

## Why

The second limited use case from the first interview (MC-1): draft vs bots. Split from LIM-1 on
2026-10-20, when Shayne chose sealed first.

## Requirements

- [x] Draft 3 packs of 14 with 7 bots, the packs LIM-1's generator makes
- [x] Arena's **Quick Draft**: Shayne and 7 bots; the bots pick the highest-rated card, leaning into
      the colours they have already picked
- [ ] The drafted pool goes to LIM-1's deck builder and is played on the table, as sealed is
- [x] **After the draft, a deck-building screen — the Sealed screen reused** (Shayne, 2026-10-29), its
      pool the 42 picks
- [x] A console `--draft --seed=N` prints all 8 pools and each bot's colours, so the bots can be judged
      before the screen exists
- [x] The echoed-pair block reads as a signal (a pack with two pair cards from different pairs
      means the partner was taken) — the bots' picks must leave that trace honestly

## Decisions

- 2026-10-20, Shayne: after sealed (LIM-1); same Arena packs and collation rules.
- 2026-10-23, Shayne (sprint planning): drafting is **wanted** — FRA first. He plays powered cube
  on MTGA (maybe MTGO later) and wants to practise it here one day, so the draft must not assume
  packs: a cube is a card list. The powered cube's CARDS are deferred — their complexity is known
  to be high (a separate story, planned in depth when its time comes). Draft stats and pick
  tracking are NOT built here: an existing tracker does it (DATA-1).

- 2026-10-28, Shayne (LIM-2.1, sprint 3 planning): **Quick Draft** (him and 7 bots), not Premier.
  **Bots pick by rating plus a colour lean** — FRA's rating is the 17lands GIH WR the sealed builder
  already uses; a cube, which has no 17lands data, uses the engine's card values (DATA-2 later). Not
  a trained picker (MtgSimulator's `DraftTrainer`) and not rating alone. Built this sprint, after
  AI-15.

- 2026-10-29, Shayne (sprint 3 planning, Claude's recommendations accepted): **the packs** are 14 cards,
  the land slot included, as LIM-1 opens them. **The colour lean** is MtgSimulator's measured one
  (`DraftPickers.DefaultLaneWeight`): +2 points of rating for a card inside the bot's two colours, −2
  outside, ramped over the first 10 picks — +4 measured worse there (it passes the bombs). **A bot's
  colours** are its top two by picks so far; a gold card is inside only if all its colours are, and
  colourless cards and lands are never penalised. **An unrated card** (under 200 games, Special Guests
  included) scores at the set's median, not 0. **Bots are deterministic**: the top score, seeded, so a
  draft replays. **A console `--draft`** to judge the bots first. **On the table** (LIM-2.3): a run
  against the 7 bots' decks — each built by the sealed builder from its pool — until 7 wins or 3 losses,
  as Arena's Quick Draft; the draft screen shows the pack, your picks by colour and curve, and the pick
  number, no timer; **then the Sealed screen's deck builder for the drafted pool** (his addition).

- 2026-10-29, Claude (LIM-2.2, open to change): **a bot's two colours are the two it has invested the
  most VALUE in** (points above the median, below-median cards counting nothing) — MtgSimulator's
  measured `CommittedColors`, the version the +2 lean was measured with, rather than a plain count —
  and while it has fewer than two, a card may bring in the missing colour without the penalty.
  **A basic land comes last** whatever its rating: 17lands rates a Forest 56.5, and the bots took basics
  as early as pack 2 pick 6 (deck building gives basics free).

Open questions:
- **The colour skew** (LIM-2.2, `docs/findings/limited.md`): rating bots sit in Blue 55 of 80 seats and
  Red 15, because FRA's Blue cards rate highest (58.4% against Green's 54.1%). Leave it (it is what
  rating plus a lean does, and Blue is cut — a real signal), or spread the bots (rate each card against
  its colour's average)? Shayne's call. (Blue 58 of 80 after the review's open second colour.)
- **A run's game left unfinished** (LIM-2.3's review): Escape from the table before the game ends counts
  nothing, so a losing game can be walked away from — Arena counts a concede as a loss. And a draw is
  counted as a loss today; Undo and the sandbox still work in a run's game. Count a left game as a loss?

## Tasks

- [x] LIM-2.1 Interview the open questions — Quick Draft; rating plus colour lean (Decisions)
- [x] LIM-2.2 The draft: 3 FRA packs of 14 from LIM-1's generator, Shayne and 7 bots, passing left
      then right then left; a bot picks by rating with a colour lean (Decisions, 2026-10-29); the
      cards as a list, so a cube drafts the same way later; `python tools/import_17lands.py fra` re-run
      first; `MagicConsole --draft --seed=N`. A test that the bots' picks leave the echoed pairs' trace —
      built (2026-10-29): `Limited/Draft.cs` (copied from MtgSimulator's `Draft`: packs dealt up front,
      picks applied all at once, passed left/right/left; `Fra(seed)` deals Arena packs, `Create` any
      list's); `Draft.BotPick` (rating in points above the median, the lean, basics last); `Draft.Next`
      (seat 0 is Shayne's pick, or a bot); `DraftTests` (9: shape and conservation, the seed replays, the
      passing, the echoed-pair trace, the lean and its control, one colour leaves the second open, a bomb
      outside the lane, basics last, an unrated card at the median — the lean and basics revert-checked).
      The review: `Create` refuses unequal packs; the median is `CardRatings.Median`, the basics
      `SealedBuilder.Basics`; colours read FRA's data (a cube's lookup is MC-4's — the ponytail). The 17lands re-run found the
      same public files as 10-19, so the ratings are unchanged (not committed). Ten drafts measured:
      basics now always the last pick; colours skewed to Blue and Black (open question)
- [ ] LIM-2.3 On the table: a draft screen (the pack, your picks by colour and curve, the pick
      number), then the deck-building screen — the Sealed screen reused for the drafted pool — and a
      run against the 7 bots' decks until 7 wins or 3 losses — **built (2026-10-29), awaiting Shayne's
      playtest**: `DraftSession` (plain C#: the picks, `Builder` — your picks as a `SealedSession`, made
      once so the deck survives each game — the run: `NextBot` each bot in turn, `BotDeck` the sealed
      builder's two-colour build of its picks, `Record`), `DraftScene` (the pack as full cards, the picks
      by colour in curve order, the curve), `SealedScene.OpenDraft` (the run in its title, Start plays
      the next bot, New draft; sealed's seed, paste and sim command hidden), Draft on the deck select;
      `TableSetup.Finished` — the table reports a run's game once and offers only Back to deck building.
      `DraftSessionTests` (4). Captured with real input (`--autopick`, `--tap`): five picks grouped and
      the curve; the builder's suggestion → Start → a game autoplayed to its end → Back → "0–1 · next:
      seat 3", the deck kept. The review: the pack dims an unscripted card as the builder does, the
      picks group by `SealedScene.Order`, the debug args read on the first visit only. Open: a run's game
      left unfinished (Decisions)

## Next

**LIM-2.3 is built; Shayne's playtest ticks it** — Deck select → Draft: 42 picks, the deck builder, a
game of the run (it also exercises TBL-3's hand fan, whose tick waits on the same playtest). Then the
colour-skew question and a run's game left unfinished (both in Decisions).
Run it: `./Run-Godot.ps1 MagicTable/DeckSelectScene.tscn` (Commands.md). MtgSimulator already drafts (`MtgSimulator/Draft/`,
rules in `.claude/rules/sim-draft.md`) and MtgGame has a draft screen (`SQGodotCommon/MtgGame/
Draft/`) — grep them first and copy-adapt.

## Log

- 2026-10-20: split from LIM-1.
- 2026-10-23: wanted, FRA first, a cube later; provisionally next sprint (interview first).
- 2026-10-28: LIM-2.1 interviewed (Quick Draft, rating plus colour lean); status → ready; sprint 3.
- 2026-10-29: sprint 3 planning — the picker's details, the console view and the table's run decided;
  the after-draft deck builder (the Sealed screen) added as a requirement (Shayne).
- 2026-10-29: LIM-2.2 built — the draft, bots and `--draft`; basics made the last pick; the colour skew
  measured (`docs/findings/limited.md`). Status → in-progress.
- 2026-10-29: LIM-2.3 built — the draft screen, the Sealed screen for the pool, the run; captured.
