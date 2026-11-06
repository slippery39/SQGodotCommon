---
id: AI-19
title: Card-by-card play and rulings — after every card plays
status: backlog
area: AI
created: 2026-11-05
---

## Why

TRK's cards are built to PLAY first (MC-5): each gets a correct engine piece and a simple, sensible AI answer,
and a rule simplified only where nothing reads the gap (`DesignNotes.md`, MagicCore known ceilings). Shayne,
2026-11-05, on Engineered Explosives: "Explosives should be used for a relevant X amount to kill the most
appropriate targets in play. But maybe just note that in the backlog. My main goal here is to get all the
cards implemented first. Then we can optimize usage and rulings per card."

## Requirements

- [ ] Each card listed below plays as a good player would play it, checked in a position (`/ai-inspect`).
- [ ] Each ruling listed below is exact, or its simplification is named and accepted again.

## Decisions

Open questions:
- Which cards first — by how often they are played (the TRK ratings, MC-5.4 / DATA-2), or by how wrong they look at the table?

## Tasks

- [ ] AI-19.1 Engineered Explosives: choose X for the most appropriate kills in play — today X is any the search
      tries, and the counters are the colours the payment can spend (MC-5.3 batch 6a). Also "the most colours"
      never spends a Treasure or Starfleet Academy for a colour — Shayne may want it to.
- [ ] AI-19.2 Deep Space 9: its station progress scores nothing until 9 — the evaluator's station term reads
      `StationComponent`, which DS9 (never a creature) has none of; value the anthem's share (MC-5.3 batch 6b).
- [ ] AI-19.3 Georgiou, Terran Emperor: check the evaluator and rollouts read life at 0 or below sensibly while
      she keeps you in the game — and that losing her there is valued as losing (MC-5.3 batch 6b).
- [ ] AI-19.4 Dr. M'Benga and Geordi: "if YOU would put … counters" — today an opponent's counters on your
      creature (or artifact) are doubled too; `PutCounters` would need who puts them (`DesignNotes.md`).
- [ ] AI-19.5 Lwaxana Troi: "keep her tapped?" in the untap step is the search's yes/no — check it keeps the
      link when the linked creature's triggers matter, and untaps her to block when they don't (MC-5.3 batch 7a).
- [ ] AI-19.6 Brazen Bluff: the OPPONENT's "exile your spell?" is answered by their search, which sees only what
      it knows of our hand — check it weighs the free spell; and Bluff's AI casts it only with a spell to cast free.
- [ ] AI-19.7 The free casts in a resolution (Sisko's five, The Caretaker, discover, Infinite Diversity's copies)
      are one yes/no or choice each — check the search casts a symmetric or harmful one only when it gains.
- [ ] AI-19.8 Worf, Intelligence Contractor and The Wrath of Khan: a creature stolen "until end of turn"
      (`UntilEndOfTurn.ReturnControlTo`) is scored by `Evaluator` as if kept — check a mid-turn score doesn't
      overvalue the steal (a rollout past cleanup sees it go back) (MC-5.3 batch 7a's review).
- [ ] AI-19.9 Genesis Device: which lands to sacrifice — the search branches on at most 32 sets of lands, past
      that the greedy answer sacrifices none, so on a big board it never cashes the Device in (MC-5.3 batch 7b).
- [ ] AI-19.10 Lieutenant Commander Data: the three counters removed — each pick a search branch; check it takes
      them from the least useful creatures, not Data's own growth (MC-5.3 batch 7b).
- [ ] AI-19.11 Explore (Captain Janeway): "put the card into your graveyard?" is the search's yes/no — check it
      keeps a good draw on top and bins a dead one (MC-5.3 batch 7b).
- [ ] AI-19.12 Peace in Our Time: each player's "draw a card and don't attack?" is their search's — check it never
      trades a lethal or winning attack for a card, and that the AI reads twelve peace counters as a clock.
- [ ] AI-19.13 Kobayashi Maru Scenario: Abandon Ship loses the game on its fourth upkeep — check the AI casts it
      only with a way to remove it (or a faster clock), and leaves Abandon Ship for last (MC-5.3 batch 7b).
- [x] AI-19.14 Kai Winn: the name is the opponent's commonest known nonland card; with NOTHING known (a turn-2 Kai)
      it is the list's first — a blind guess (MC-5.3 batch 7c). Shayne, 2026-11-06, for LIMITED ("in a constructed
      deck what you name would be determined completely differently"): "If blind, Kai Winn should name a card that
      has an activated ability on the opponent's side of the board, if none, it should name a card it knows is in
      your hand (via card effects), if that's not available, it should name a common card that it can see in your
      colors." Then (asked): it REPLACES "commonest known", step by step, the common the highest win rate. Built
      2026-11-06 (`GreedyAi.LikeliestName`): until a set has ratings (TRK: MC-5.4) the cheapest common is named.
- [ ] AI-19.15 Q, Stacking the Deck: each activation exiles a hand card to cast a copy, paying — check the AI doesn't
      exile a better card than the copy, and that the exiled card with the gift is valued at all (the evaluator
      sees an exiled card as nothing).
- [ ] AI-19.16 H.M.S. Bounty: the extra turn is seen only inside the rollout's horizon — check it attacks with the
      Bounty when a Whale is out, and fetches the Whale.
- [ ] AI-19.17 The City on the Edge of Forever: chapter I reads as both boards wiped until the Human goes — check the
      AI casts it behind on board, not ahead, and keeps (or sacrifices) the Human on purpose.
- [ ] AI-19.18 Nog, Ambitious Cadet: check the AI pays {1} then {3} with spare mana, and plays lands from its
      graveyard rather than holding them.

## Next

Not started. Add a card here whenever a batch leaves its play or a ruling simple on purpose; start once
MC-5 has every card playing.

## Log

- 2026-11-05: opened with Engineered Explosives (MC-5.3 batch 6a); Deep Space 9 and Georgiou added (batch 6b).
- 2026-11-06: Lwaxana Troi, Brazen Bluff and the free casts added (MC-5.3 batch 7a).
- 2026-11-06: Genesis Device, Data, explore, Peace in Our Time and Kobayashi Maru added (MC-5.3 batch 7b).
