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
