# Limited — measured results

Numbers runs produced for sealed and draft (stories `LIM-*`). The rules they support live in the
stories and `MagicCore/CLAUDE.md`.

## Splash and greedy suggestions: how often the rules fire (2026-10-24, LIM-3.2, LIM-3.5)

`SealedBuilder.Suggestions` on seeded FRA pools, with Shayne's rules of 2026-10-24 (a splash for the
set's top 10% by 17lands GIH WR — ≥ 61.7%; a single pip of the splash colour; 3 sources, one more
per extra card; 7 per main colour; 17 lands):

| | Pools |
|---|---|
| a top-10% card off the streamlined pair's colours | 226 of 300 (75%) |
| two such cards in ONE off colour | 9 of 300 (3%) |
| a splash suggested | 22 of 60 (37%) — 15 before the review's fix (below) |
| a greedy splash suggested (LIM-3.2: one colour, lands only) | 0 of 60 |
| **a greedy build suggested (LIM-3.5: up to four colours, every fixer counted)** | **56 of 60** — 25 three colours, 31 four |
| — four colours with no rule against them | 53 of the 59 |

- **Most off-colour bombs can't be splashed by the rules:** gold cards of two other colours, or two
  pips of the splash colour.
- **LIM-3.2's greedy was rare by construction:** two top-10% cards in ONE off colour (9 of 300
  pools), counting only lands as sources. Shayne (round 3): count every fixer — landcyclers and
  land-searchers for every colour, Heartwood makers, mana creatures — and let greedy add colours.
- **Greedy is scored by its cards' ratings alone**, so another colour always scores higher when the
  mana clears the bar: without a rule, 53 of 59 greedy builds were four colours. Shayne (round 4):
  four only for a top-10% card the best three can't play — 31 four, 25 three. Nothing yet prices
  inconsistency; LIM-3.3's simulated draws should.
- **A fetcher needs a basic to fetch** (Shayne, on seed 3's greedy build splashing Lich's Relic with
  NO Swamp — "landcyclers cannot fetch a black source otherwise"): a landcycler or land-searcher is a
  source of every colour the deck has at least one basic of, and of no other — two landcyclers and a
  Swamp are 3 black sources, two and no Swamp are 0. The mix of
  suggestions didn't change (the fix adds the basic far more often than it rules a build out).
- **Basics are ALLOCATED to the rule, not split by pips and then checked** (the /pre-commit review):
  each main colour first gets the basics it needs for 7 sources, the rest by pips. Split by pips, a
  lopsided pair (9/5 of 14) failed the check and lost a legal splash — 7 of the 22 pools.
  `OneBomb_OnBasicsAlone_…` pins it (revert-checked).
- **Two real bombs pull the pair onto their colour:** the builder picks the pair by its best 23, so a
  pool given two top-10% cards of one colour chose that colour in every pool tried (seeds 1–20) —
  they became the deck, not a splash.

## How often each suggestion's spells are castable on time (2026-10-25, LIM-3.3)

`Castability.OnTime` (simulated draws, 1 000 games a deck, on the play and the draw; a main-colour
card on curve, a splash card by turn 6 — Shayne) over the suggestions of 60 seeded FRA pools:

| Suggestion | Pools | Castable on time: mean (range) |
|---|---|---|
| Two colours | 60 | **82.2%** (74.2–88.7) |
| Splash | 22 | **67.2%** (54.2–75.6) |
| Greedy, three colours | 25 | **60.6%** (48.5–72.4) |
| Greedy, four colours | 31 | **50.8%** (29.5–60.9) |

- **The rule of thumb passes builds that are half on time.** Every suggestion meets its source
  counts, yet a four-colour greedy build casts its spells on time about half as often as it would
  in two colours. Shayne's decision stands: shown, not gated — the screen puts each beside the
  two-colour build's number.
- **A splash costs ~15 points, mostly in the MAIN colours:** the splash's basics come out of the
  pair's (7 each instead of 8–9), so their cards are late more often — the splash card itself is
  judged by turn 6.
- **What it measures:** having the mana, as Karsten's tables do — no spell but fixers, mana
  sources and surveil is cast, so the curve itself never spends mana. A land that enters tapped is a
  source from the next turn (the first version counted it the turn it entered; the tapped-land test
  caught it). After the /pre-commit review: {2/X} paid by its colour or two generic, a Treasure
  spent once (not a lasting source), a surveil only as the card is cast or enters — the means moved
  by 0.2 points at most.
