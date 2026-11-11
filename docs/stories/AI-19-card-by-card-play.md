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
- [ ] AI-19.19 Phyrexian Tower: its "{T}, Sacrifice a creature: Add {B}{B}" is a move, one per creature, its mana
      floating (MC-5.6, Shayne). Check the search takes it only to cast with the mana that turn — and in response to
      removal on the creature, which a one-ply score won't see.
- [ ] AI-19.20 Drivnod, Carnage Dominus: the three creature cards it exiles are picked one at a time by a one-ply
      score (`GreedyAi.Answer`), which doesn't price a graveyard card — so it exiles the first, not the least
      useful (a reanimation target kept). And `{B/P}` paid with life or {B} is the search's: check it pays life only
      when the mana buys something else that turn (MC-5.6 batch 3a).
- [ ] AI-19.21 General Chang, Cold Warrior: the AI sacrificed a 4/4 Cha'DIch Investigator and a token to
      make him bigger at sorcery speed, nothing was dying (Shayne, 2026-11-08). Records:
      `game_records/2026-11-08_025607_seed478788.json` #305, #309 — and #458 it sacrificed Chang HIMSELF.
      Rebuild as a position; the sacrifice should be priced against what the creature is worth.
      **Rebuilt and diagnosed (2026-11-08):** `ChangSac305`, `309`, `458` (kept) - the controls held.
      **Not the evaluator:** it charges the creatures (the pair costs ~15 points at once). The simulated HUMAN
      is what rewards it. He chooses his attack with `WholeAttacks.Best`, judged against `Lookahead.Settle`'s
      defender (AI to 1 life, a Jockey lost), but the rollout then plays the real blocks (AI to 5, nothing
      lost): he attacks into blocks he didn't see, and at turn 15 throws four creatures away - only when the AI
      has sacrificed down to few bodies. That one imagined attack was worth +25 to the sacrifice line, the
      "bait" Shayne's "I wouldn't attack" names. A second trap: `AutoPass` stops in the declare-attackers step
      whenever the defender has an instant-speed ability (Chang's own), so a naive probe never reached the blocks.
      **Fix:** `Evaluator.Terms.TrueDefender` (`+nodefender`) - a simulated attacker is judged against the blocks
      `Rollout.OpponentStep` will choose. All three now flip: #305 plays the Island (+8.87, the Investigator
      sacrifice -10.54), #309 and #458 pass; `ASacrifice_ThatOnlyBaitsTheSimulatedAttack_IsNotMade`, control
      included. **Not yet safe as a default:** the quick check changes 17 of 288 decisions (5.9%), oracle
      agreement 88.2% -> 84.7% (the oracle shares the old model), cost per position +59% (5 892 -> 9 342 steps);
      two earlier controls (`FacingASwingToOneLife`, the Deathbringer's Beast) stopped provoking - the new term
      makes those plays unaided - and were given `TrueDefender` off in their control cases. **Match run: 288 games,
      50.7% ± 1.7pp, no 5pp difference, but 2.3x the cost and a 173 s slowest decision - so the true defender
      re-judges a SHORTLIST (best 3 + no attack, `WholeAttacks.Shortlist`), 3.1x on the biggest board (was 6.8x).
      Shortlist match: 192 games, 52.1% ± 1.9pp (no loss), 1.8x cost, slowest decision 33 s (was 173 s) - kept as
      the default (2026-11-08)**

- [ ] AI-19.22 Borg Cube (Shayne, 2026-11-10: "please look into how its playing Borg Cube, might give us some AI lessons").
      Looked at: `--card-games="Borg Cube"` (800 seeds' decks, 45 hold it), 4 games drawn, **never cast in any** — two
      fair (stuck on 4 lands; never in hand with the mana), one defensible (won-36: 7 mana spent on He's Dead, Jim, won),
      one a MISS: `ai_reports/card-games-2026-11-10_202700-BorgCube-lost-91.json` #343, castable for 5 (affinity), the
      holder at 20 against 9 — **Pass 49.98 over Cast Borg Cube 25.88**, and that seat lost. `--explain=343`: in the
      Cube line the rollout (1) exiles their best attacker, then (2) stations the Cube by TAPPING AN ATTACKER (DOT-7)
      and doesn't attack, (3) casts one spell a turn so the 7-drop pushes D'Ghor and Bat'leth back a turn — the
      opponent ends at 7, not 2; the clock term falls 20 → 4.3. Lessons, each general: **(a)** the rollout's one spell
      a turn (`Rollout.Settings.ActionsPerTurn`, never measured alone — AI-9's Next) charges an expensive spell a whole
      turn of the rest of the curve; **(b)** the rollout stations with a creature that could attack — station with
      what wouldn't attack (summoning sick, or an attack that does nothing); **(c)** a card exiled "with" a permanent
      that will return it under YOUR control (assimilate on attack) is priced only as their loss, not your future
      gain; **(d)** a station threshold's ability (12+: flying for artifact creatures) isn't priced until reached
      (the `Other` price's own ponytail). Shayne's read stands: "maybe Borg Cube is just a 7 mana removal spell most of
      the time" — to the AI it is less.
## Next

Not started. Add a card here whenever a batch leaves its play or a ruling simple on purpose; start once
MC-5 has every card playing.

## Log

- 2026-11-05: opened with Engineered Explosives (MC-5.3 batch 6a); Deep Space 9 and Georgiou added (batch 6b).
- 2026-11-06: Lwaxana Troi, Brazen Bluff and the free casts added (MC-5.3 batch 7a).
- 2026-11-06: Genesis Device, Data, explore, Peace in Our Time and Kobayashi Maru added (MC-5.3 batch 7b).
- 2026-11-06: Phyrexian Tower added (MC-5.6 batch 1).
