---
id: LIM-3
title: Sealed builds beyond two colours — streamlined, splash or greedy, offered as suggestions
status: in-progress
area: LIM
created: 2026-10-24
---

## Why

Shayne (2026-10-24): the AI deck builder builds strict two-colour decks only (`SealedBuilder`:
"Two colours, no splash yet" — LIM-1's decision of 2026-10-20, "a splash only if builds show the
need"). Much of limited deckbuilding, sealed more so, is judging **whether to get greedy** with the
pool: a streamlined two-colour build, two colours plus a splash, or as many bombs as the mana still
supports. "Limited deckbuilding is an art, not a science" — so these are suggestions, never touted
as the optimal deck.

## Requirements

- [x] The builder can suggest builds of different greed from one pool, each labelled by its
      approach — e.g. the streamlined two-colour build (today's), two colours plus a splash, a
      greedy build — not necessarily all three: only those the pool supports
- [x] A splash or greedy build is suggested only **when its mana works** — by the rule of thumb
      (sources, every fixer counted); its castability is MEASURED by simulated draws and shown beside
      the two-colour build's, not used to reject it (Shayne, 2026-10-25)
- [x] Shown as suggestions on the Sealed screen, never as "best" or "optimal"; Shayne takes one or
      adjusts it
- [ ] The suggestions can be compared by LIM-1.8's sims (`--builds`) — with its caveat: it says how
      well the AI pilots each, not which is right

## Decisions

- 2026-10-24, Shayne: create the story. Suggestions, not the optimal deck; not necessarily three
  builds — the point is judging whether to get greedy. This revisits LIM-1's "two colours, no
  splash" (2026-10-20) for the suggested decks.
- 2026-10-24, Shayne (interview, round 1):
  - **a splash is for bombs and premium removal** — cards clearly above the pair's own
  - **no fixed splash size: the mana bar decides** how many
  - **the mana bar: a rule of thumb first, then measured by simulated draws** as a follow-up
  - **the AI opponents use the same builder**, splashes included — more varied practice decks
  - Facts that shaped it (FRA): 10 common duals, one per pair, tapped unless you control a
    planeswalker; the land slot is fixing 55% of the time, so a pool averages ~3–4 duals over the
    10 pairs; Room of Refuge and Murmuring Volume are common fixers; MagicCore mulligans are
    London, 7 cards, so the measured bar is honest here
- 2026-10-24, Shayne (interview, round 2):
  - **worth splashing = the set's top 10% by rating** (FRA: 17lands GIH WR ≥ 61.7%, ~28 cards,
    mostly mythics and rares) AND better than the build's weakest spell it replaces — for another
    set, the top decile of its ratings table
  - **the rule of thumb:** a splash colour needs 3+ sources (its basics, duals, Room of Refuge,
    Murmuring Volume — anything that makes the colour); each main colour keeps 7+; 17 lands; one
    splash card per source beyond 2 (3 sources → 1 card, 4 → 2, …), so the bar sets the size
  - **"greedy" = the biggest splash the bar allows** in the same pair, against the "splash" build's
    single best card — shown only when it differs
  - **an AI opponent plays one of its pool's suggestions at random** — you meet both kinds
- 2026-10-24, Shayne (round 3, after LIM-3.2's first build — "you are only counting lands as
  sources"): in FRA, a slow format with lots of surveil, **3–4 colour decks are common** given duals,
  landcyclers and bombs or premium removal worth it. Revises round 2's greedy:
  - **every fixer counts 1 source**: landcyclers and land-searchers (Hexhaven Battalion, Undulating
    Witness, Apex Witchstalker, Awaken the Inferno, Vinelasher Adept; Simulacrum Shaper, Fblthp),
    Heartwood token makers (Hungering Puppetbeast, Aerid Konstrari, Tenured Tethermage, Vraska),
    mana creatures and rocks — any permanent that taps for coloured mana
  - **greedy = up to 4 colours, if sources allow** — not only a bigger splash of one colour (226 of
    300 pools had an off-colour bomb, but most were gold cards of two other colours)
  - **the card bar follows the colour's sources**: a SPLASH colour (few sources) plays only the top
    10%; a REAL colour (enough sources) plays any card better than what it replaces
  - **surveil and scry: left to the measured bar** (LIM-3.3) — the rule of thumb ignores them
  - **the bars: 7 for each main colour, 6 for a third or fourth REAL colour, 3 for a splash** (one
    more per extra splash card); a landcycler or land-searcher **counts 1 for every colour** (it
    fetches whichever basic is missing, like Room of Refuge). Build it now (LIM-3.5).
- 2026-10-24, Shayne (round 4, on seeing greedy come out FOUR colours in 20 of its 21 pools — it
  scores by card ratings alone, so a fourth colour always wins when the mana clears the bar):
  **prefer three colours; four only if clearly better** — a four-colour build is suggested only when
  it plays a top-10% card the best three-colour build doesn't. Fetchers still count 1 for every
  colour (LIM-3.3's simulation will show how generous that is).
- 2026-10-24, Shayne (on seed 3's greedy splashing Black with no Swamp): **a landcycler can't fetch a
  colour the deck has no basic of** — revises round 3's "counts 1 for every colour": with at least one
  basic of a colour, every fetcher is a source of it; with none, no fetcher is. "Two landcyclers and
  one Swamp is 3 sources, not two. Two landcyclers and 0 Swamps is 0 sources."

- 2026-10-24, Claude (LIM-3.2, open to change): on the Sealed screen, "Suggested deck" became a
  button per suggestion — **Two colours, Splash, Greedy** — in a row that wraps; each button's
  tooltip and a line under the row name what it splashes ("Suggested: Splash Black: Lich's Relic").
- 2026-10-24, Claude (LIM-3.2): a land that chooses its colour as it enters (Room of Refuge) counts
  as a source of EVERY colour, main and splash alike — generous, since one copy makes one colour;
  the measured bar (LIM-3.3) prices it honestly.

- 2026-10-25, Shayne (LIM-3.3, pulled into sprint 2 as item 12): **a lightweight simulator**, not
  the engine goldfished — shuffles, London mulligan, a land a turn, tapped lands, landcyclers,
  mana creatures and Heartwood, surveil from the scripts; **shown, not gated** — the rule of thumb
  still chooses the builds, and each suggestion shows how often its spells are castable on time
  beside the two-colour build's (the buttons' tooltips, the "Started from" line); **on time = main
  colours on curve, a splash card by turn 6**.

## Tasks

- [x] LIM-3.1 Interview the open questions (2026-10-24, two rounds — Decisions)
- [x] LIM-3.2 The suggestions by the rule of thumb: `SealedBuilder.Suggestions` (streamlined, a
      splash of the best card, a greedy splash of as many as the mana allows), `Opponent(seed)` one
      at random; `SealedSession.Suggestions`/`Suggest(i)`, a button each on the Sealed screen;
      `SealedSuggestionTests` (60 pools against the rules; greedy on a pool built for it),
      revert-checked four ways (the main-colour check, double pips, a source short, no rating bar);
      `SealedSessionTests` (each suggestion taken in turn). Captured on seed 3 (`--seed=3`, a real
      click on Splash): 23 + 17, Lich's Relic splashed on a Swamp, Meticulous Commons and Room of
      Refuge. Review fixes: basics allocated to the 7-source rule (15 → 22 of 60 pools splash),
      fixers taken only as needed, a hybrid only the splash pays counts as its pip, only a LAND that
      chooses its colour counts as a source; `SealedDeck.Kind` names the buttons
- [x] LIM-3.5 Greedy up to four colours, every fixer counted (Shayne, rounds 3–4): `SealedBuilder.Fixes`
      (from scripts: mana abilities, tokens made for you, land searches, Treasure; checked against
      the printed text — `Fixes_MatchesWhatEachCardSays`), `Multicolour` (any two mains and one or
      two more colours, each real or a splash; the mains hold the most cards; lands cover each
      colour's shortfall, fixing lands first), four colours only for a bomb the best three can't
      play (`AFourthColour_…`). Revert-checked: landcyclers not fixers, a real colour on 5, mains
      not the biggest, basics by pips, the opponent's Treasure counted, the four-colour rule, a
      fetcher counted with no basic of its colour. Review fixes: lands can't be the four-colour
      build's bomb; a hybrid of two extra colours needs both; `FetchesABasic` (basic searches only —
      Enroot's "a land card" isn't one); the reflection walk reads instance properties; card data
      built once per pool. Captured on seed 3: Greedy White/Blue + Black splash (Lich's Relic) + Red,
      1 Swamp
- [x] LIM-3.3 The measured mana bar, shown not gated: `Castability.OnTime` (simulated draws: London
      mulligan, a land a turn, tapped lands, a land that chooses its colour, landcyclers and
      land-searchers only with a basic to find, mana creatures and Heartwood, surveil from the
      scripts); `SealedSession.OnTime`/`OnTimeText`, on each button's tooltip and the "Started from"
      line. `CastabilityTests`: more sources, tapped lands, a landcycler with and without a basic,
      a splash card by turn 6, {2/X} either way, a Treasure spent once, an ability's surveil not
      the card's — revert-checked (a landcycler making any colour, splash cards on curve, {2/X} by
      colour only; the tapped-land test caught a real bug first). Captured on seed 3: "castable on time
      59% (two colours 84%)"
- [ ] LIM-3.4 Compare a pool's suggestions by sims without exporting each from Arena — LIM-1.8's
      `--builds` reads Arena exports; a pool (or a seed) in, its suggestions out

## Next

LIM-3.2, 3.3 and 3.5 are built: the Sealed screen offers **Two colours / Splash / Greedy**, each
with how often its spells are castable on time beside the two-colour build's. **Measured
(`docs/findings/limited.md`): two colours 82%, splash 67%, greedy 61% in three colours and 51% in
four** — the rule of thumb passes builds that are on time half as often. Shayne decided: shown, not
gated. If those numbers make the greedy suggestions unhelpful, the levers are: gate greedy on a
floor (e.g. within N points of two colours), or raise the source bars. Next: LIM-3.4 (compare the
suggestions by AI-vs-AI sims). Read `SealedBuilder.Suggestions`/`Multicolour` and `Castability`
first. LIM-1.8's opponents include greedy decks.

## Log

- 2026-10-24: created from Shayne's note that the builder only builds strict two-colour decks.
- 2026-10-24: interviewed (two rounds); LIM-3.2 built — 22 of 60 pools get a splash, greedy is rare
  by the rules (`docs/findings/limited.md`). Status → in-progress.
- 2026-10-24: rounds 3–4 — every fixer counts, greedy up to four colours (only for a bomb three
  can't play); LIM-3.5 built: greedy on 56 of 60 pools, 31 of them four colours (findings).
- 2026-10-25: LIM-3.3 — castability measured and shown: two colours 82%, splash 67%, greedy 61% /
  51% (three / four colours) over 60 pools (findings).
