---
id: LIM-3
title: Sealed builds beyond two colours — streamlined, splash or greedy, offered as suggestions
status: backlog
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

- [ ] The builder can suggest builds of different greed from one pool, each labelled by its
      approach — e.g. the streamlined two-colour build (today's), two colours plus a splash, a
      greedy build — not necessarily all three: only those the pool supports
- [ ] A splash or greedy build is suggested only **when its mana works**: a castability bar,
      measured, using the pool's fixing (duals, and whatever else fixes); a build under the bar
      isn't offered
- [ ] Shown as suggestions on the Sealed screen, never as "best" or "optimal"; Shayne takes one or
      adjusts it
- [ ] The suggestions can be compared by LIM-1.8's sims (`--builds`) — with its caveat: it says how
      well the AI pilots each, not which is right

## Decisions

- 2026-10-24, Shayne: create the story. Suggestions, not the optimal deck; not necessarily three
  builds — the point is judging whether to get greedy. This revisits LIM-1's "two colours, no
  splash" (2026-10-20) for the suggested decks.

Open questions:
- What earns a splash: a bomb or removal by rating (17lands' GIH WR — how far above the pair's
  cards?), how many splash cards, single-pip only? (A double pip is effectively a commitment to that
  colour.)
- The mana bar: how many sources a splash needs, and how fixing counts (duals, Treasures, mana
  creatures, a land that taps for any colour). **Measure it in this engine** — the root CLAUDE.md's
  colour table is MtgCore's, not MagicCore's, which plays real Magic mana; borrow paper's numbers
  (Karsten's) only once this engine is shown to agree.
- "Greedy" — the most bombs whose mana still clears the bar? A three-colour build?
- AI opponents: do they splash too (more varied practice opponents), or stay two-colour?
- The ratings: 17lands' GIH WR for a splash card comes mostly from decks that could cast it — a
  discount for greed, or does the mana bar cover that?
- How the suggestions show on the Sealed screen.

## Tasks

- [ ] LIM-3.1 Interview the open questions

## Next

Interview first. Read `MagicCore/Limited/SealedBuilder.cs` (the pair choice, `Castable`, `DualColors`,
basics split by pips), `SealedScene` and `SealedSession` (MagicTable: the suggested deck), LIM-1's
decisions (ratings, two colours), `CardRatings.cs`, and LIM-1.8 (`SealedBuildMatch`) to compare the
suggestions.

## Log

- 2026-10-24: created from Shayne's note that the builder only builds strict two-colour decks.
