---
id: AI-19
title: Card-by-card play and rulings — after every card plays
status: in-progress
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

- 2026-11-11, Shayne (the scan's VALUE list, AI-19.23 — positions in `docs/findings/magic-ai.md`): (2) Cerritos stationed
  by a 2/2 flier, opponent at 16 with no creatures — **fine**: "opponent is still at a high life, no risk of being
  attacked back … getting the 4/4 flyer online soon is a fine goal". (3) Cold-Blooded Crew held, no green source, five
  lands, nothing done at 6 life — **a clear misplay**: "getting the rest of his hand online is more important than
  holding the cold blooded crew in case he draws 4 forests … he probably should have land cycled earlier". (5) Mikaeus
  tapped for his counter in his own upkeep — **wait for the end step**. (6) Suus Mahna tapping itself in its own upkeep —
  "literally a waste of resources". (7) Cybernetic Specialist putting a Swamp on its library's bottom in its upkeep —
  "almost pure negative", and the blocker lost is a cost: "pure waste of a play". (8) Re-equipping the same creature
  (or back and forth) and other free-looking activations — **a waste, "one of the more innocent behaviors", but tapping
  out has a cost: "now your opponent knows you have no instants to play or you can't activate abilities"**. The NOISE
  list (landcycling in his upkeep, a landcycle fetching nothing, the Analyst's upkeep ping, Guidance Failure in its
  upkeep) — **fixed in AI-20**. (1) Sarcophagus — a clear misplay "especially if sarcophagus would not be animated by
  stationing it"; then found: it WAS (two stations, 3 → 6 → 10) and attacked as a 4/4 flier for the win — back to
  Shayne. (4) Rogue Artificial Intelligence — "this one isn't clear" (its lands asked) — back to Shayne.
- 2026-11-11, Shayne (the two sent back): (4) Rogue, one land short with its land drop unused and {2} idle — **"perfectly
  fine now to cycle … Honestly its kind of a judgement call"** (cycling makes Automatons and a held Flare certain; holding
  is defensible) — not a misplay to fix. (1) Sarcophagus — **still a misplay, a smaller and more advanced one: not a
  missed on-board kill but "how to play around removal the best"**: "When swinging for the win, you have to account for,
  can I win through them removing one of my creatures? If they do … are we going to be losing creatures to better
  blocks … is it best to go for the slower less immediate win, but risk the opponent drawing something later". Noted,
  **not to fix this session**; look for a more obvious station case. **Looked (Claude):** of the 80 stations flagged, 60
  animated the ship that turn (station, then swing — the Sarcophagus question); of the other 20, 17 had a blocker that made
  the attack poor or a trade, 2 were free attacks at high life (item 2's "fine"), and 1 was pure waste — Enterprise-D
  stationed by a -2-power Tactical Officer, 0 counters (S6 caught it: the "changes nothing" family). No station fix now;
  S3c to skip a station that animates the ship that turn.

- 2026-11-11, Shayne (FRA's list, `docs/findings/magic-ai.md`): (F1) instants in your own upkeep — "you aren't usually
  casting instants in your own upkeep, unless there is something you need to do in response to beginning of upkeep
  triggers, or before you draw … it definitely should not be the default". (F2) pumps, flying and untaps in the
  opponent's upkeep — "should all be delayed until the effect actually matters (ie. opponent attacks, then you untap your
  creature with hapatra then block)". (F3) re-equipping — "there are use cases for equipping different creatures in a turn,
  but we need a way to stop this when it doesn't provide any extra value". (F4) Arni's loot — not at its own end step:
  "either loot during your main phase to find stuff to play, or loot at the opponents end step to keep it up as a
  potential blocker" (whether to loot at all with a good hand: out of scope). (F5) a free graveyard exile — "it should
  exile a card at every opportunity" (Timetwister-like edge cases aside). **His idea for F5 and the like: a POTENTIAL
  score** — "exiling cards is something you should do if its free, but its not something you always want to prioritize …
  think of the main board score as one score, then potential scores add onto it without adjusting the main score so the AI
  can choose these better effects without it screwing up their main decision making"; and **tapping a creature is spending
  a resource** (a potential blocker) — "always a cost benefit analysis" — potential or main, to discuss.
- 2026-11-12, Shayne (AI-19.22, `Terms.DamagePotential`): marked damage in the potential, the main score still charging
  it — "I guess thats better yeah, it at least makes more sense". The block that moved when damage left the main score
  (Keeper of the Quiet Hour 3/2 at 13 life eating a 1/1 to 5 instead of trading with Koth 3/2 to 7) — **"defensible …
  due to the low life … I can see go either way, so its fine. Reasonable plays is what we are looking for, not perfect
  plays"**; at higher life he'd eat the 1/1 and keep the Keeper.
- 2026-11-12, Shayne (AI-19.22, #343): **the Pass is fair** — attack, then D'Ghor's −1/−1 sweeps: "Its a reasonable
  play to try to make." The pinned test (`BorgCube_ExilesTheCerritos_OverASecondBloodletter`) is removed. The lessons
  are general — **Spacecraft, not the Cube**: stationing priced against the attack or block given up is AI-23.

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
      the time" — to the AI it is less. **Shayne at the table (2026-11-11, Arena): "Borg Cube has been nuts
      too, way more than just a 7 mana removal spell, its pretty much a game winning card. I believe the reason why it had
      such a low win rate is because our AI did not know how to station it properly"** — so its low sim rate is the AI's
      play (lessons (b) and (d): stationing with attackers, the 12+ ability unpriced; and (a), (c) behind it never being
      cast), not the card. **Counted in AI-19.23's TRK scan (300 games, 600 AI sealed decks):** in only 15 decks, drawn in
      8 games, castable on 4 turns in all (seed 130 #347, seed 36 #407, seed 91 #343 — the same game `--card-games` found,
      the decks are seeded — and seed 57 #663), cast once (seed 57). Two problems: the AI's sealed builder rarely plays
      it (its rating comes from the self-play that misplays it — a loop), and castable it passes 3 times in 4. **Shayne
      (2026-11-11): use Borg Cube as a test.** The four castable turns read: seeds 36 and 130 WON that turn
      another way (He's Dead, Jim on Dahar Master; an attack) — passing the Cube was right; seed 57 cast it; **seed 91 #343
      is the one miss** (kept: `Records/BorgCubeNotCast91`), now 30 of 30 guesses "Cast D'Ghor Bloodletter". So in this
      scan the Cube's low rate is mostly the builder leaving it out and the turns it arrives late, plus that one pass —
      the station lessons (b), (d) need a game where it is cast. **Shayne (2026-11-11), #343: Borg Cube exiling U.S.S. Cerritos** —
      "exiling the Cerritos with the Cube actually gives more options. If we want to race we can then attack with the 3/3
      flier, if we want to play defensive we can hold it back. Then we can actually use the 3/3 fliers untap ability over
      the next few turns to either race and untap it, or station and untap it. Given the low life totals and high station
      cost, in this case it might be just to race (3 hits kills the opponent anyways), but if the opponent plays something
      that stops our attacks … we can switch to the station plan … Using the D'Ghor Bloodletter is something we can do on
      the following turn if necessary." Pinned, ignored as the known defect: `BorgCube_ExilesTheCerritos_OverASecondBloodletter`.
      **After AI-13.11/13.12 (2026-11-11):** the kept position still passes it, Pass 30/30, the same scores (49.98 against
      25.88) and the same Cube line (stations with DOT-7, no attack; D'Ghor alone on turn 17). The rerun's own seed 91 went
      another way from turn 13 and cast the Cube on turn 15: it exiled the Cerritos and won that turn.
      **Shayne (2026-11-11): "the thing that really needs to be fixed is, why did Player 1 block the way he did?"** The
      Pass line's Borg 1/1 is blocked by the Romulan, not the untapped Cerritos 4/4, and D'Ghor's −1/−1 finishes it — the
      real game blocked the same way (#352-#353). Cause: Cerritos block / don't block tie exactly (1.71), since marked damage
      costs 1 a point on any creature (`Evaluator.Body`), and the tie goes to "Don't block" (findings). **Shayne: "damage
      dealt to creatures could also be a 'potential' score, rather than a concrete score, because you still need something
      to finish it off afterwards" — pin it, make the change.** Built: `Terms.DamagePotential` — the damage still marked
      counts in the potential (the search's, and the rollout's judged-alone combat tie-break); pinned
      `TheCerritos_BlocksTheBorg_NotTheRomulanAMinusOneFinishes` (30/30). Out of the main score altogether it moved 17 tests
      and a rollout block got worse, so the main score still charges it (findings). #343 still passes, 30/30 — a new Pass
      line, attack then D'Ghor's −1/−1 after the Cerritos blocks DOT-7: **for Shayne to read**. Suite: 1 decision of 288
      changed. Match not run.
- [ ] AI-19.23 **The misplay scan** (Shayne, 2026-11-11: find the cards the AI plays wrong in TRK, then FRA — per-card
      win rates are too noisy at ~100 games, and a misplayed card just reads "bad"). BlunderLint extended, not a
      sibling: `--lint --set=trk --games=N` plays that set's AI sealed decks (DATA-2's), the search on both sides, a
      game stopped past `SimulatedRatings.GameBudget`; every game kept as a record (`ai_reports/lint-<time>/seed-N.json`)
      and READ afterwards (`BlunderLint.Check`; `--lint --records=DIR` re-reads in seconds). The checks, each rule
      agreed with Shayne before it was built (2026-11-11):
      **S1** a sorcery-speed card (no land, instant or flash) still castable as its holder passes its second main
      phase, stack empty — left in hand with the mana for it unspent; "never cast": castable on 2+ turns, never cast;
      ranked by the share of castable turns left against the median at its mana value (10+ games castable).
      **S2** instants and flash: when cast (own/their upkeep, main 1, combat, main 2, end step; in response) and turns
      held through their end step castable — reported, never flagged. **S3** activated abilities of permanents in play,
      by cycle (its controller's untap to the next): (a) a creature's {T} ability used with the stack empty anywhere but
      the opponent's end step, flagged only if it is still tapped as its controller's next turn begins (an untap
      coming clears it — Tal Shiar Analyst); (b) usable in 3+ cycles, never used; (c) station in its own main 1 or
      beginning of combat tapping a creature that could attack; (d) its second main phase passed with no land played
      and a landcycling ability usable. **S4** turned around without news: an instant-speed move left at the holder's
      last own-turn stop, made in the opponent's upkeep or draw step, the opponent having done nothing but pass and no
      new card in the holder's hand (Shayne's landcycle in his upkeep, AI-20). **S5** a "you may" (yes/no, or a pick
      of none or one) declined, by its source on the stack, "pay" counted apart. **S6** our spell or ability, alone on
      the stack, resolved and changed nothing but its cost (life, mana, every zone, each permanent's state).
      **Then each suspect decided again under 30 guesses** (AI-20.2's flip rate): NOISE (flips between guesses → AI-20)
      or VALUE (wrong in most → the fixes). **Built (2026-11-11);** test `TheScan_ReadsAKeptGame_AndFindsItsMisplays`
      (`Records/LintTrkSeed1`, the 4-game trial's seed 1). The trial already shows: Cybernetic Specialist and Suus Mahna
      Practitioner tapped in their own upkeep/combat (S3a, Tal Shiar's pattern), U.S.S. Cerritos stationed with a 2/2
      that could attack (S3c), and a landcycle's search answered "none" (#48: the cost paid, no land fetched).
      **Run (2026-11-11, 300 TRK games, 40.6 min) and every top suspect decided under 30 guesses** — the ranked list is in
      `docs/findings/magic-ai.md` ("AI-19.23: the misplay scan"). VALUE (the same wrong play in nearly every guess):
      (1) paid activations that change nothing — Bat'leth / Neuroflux Heirloom re-equipped onto the same creature, DOT-7
      untapping an untapped creature, 30/30 each; (2) Suus Mahna Practitioner tapping ITSELF, 24/30; (3) Cybernetic
      Specialist moving a Swamp to its library's bottom in its upkeep, 30/30; (4) Mikaeus's counter in his own upkeep,
      29/30; (5) Spacecraft stationed with an attacker, 30/30 (the real AI, not only the rollout); (6) a landcycler held
      with mana open and no land played, 30/30 and 23/30. NOISE (AI-20): the landcycle in the opponent's upkeep (S4), a
      landcycle's search answered "none", Tal Shiar Analyst's upkeep ping and Guidance Failure in its own upkeep (8/30
      each). Most instant and flash casts are in their caster's own upkeep (S2) — more positions before calling it.
      Checks to tighten (false positives): S3a counts sorcery-speed tap abilities, S6 a surveil that kept its card, S5 a
      surveil's keep. **Reviewed with Shayne (2026-11-11, Decisions).** Checks tightened the same day: S3a skips a sorcery-speed
      ability (it can't wait), S3c a station that animates the ship that turn, S5 and S6 a surveil's (or explore's) keep —
      TRK re-read: S3a 157 → 139, S3c 80 → 20, S5 439 → 213, S6 92 → 70. FRA's 300-game run next.
      **Rerun after AI-13.11 and AI-13.12 (2026-11-11, same seeds, 54.1 min, `ai_reports/lint-2026-11-11_215939`;
      `docs/findings/magic-ai.md`, "the scan again"):** S3a 139 → 60, S6 70 → 24; 40 games stopped against 21 (AI-13.12's
      cost). Gone under 30 guesses: Cybernetic Specialist (7), Suus Mahna tapping itself (6), most re-equips (8) — the
      30/30 left are inside won lines (a turn-end-board tie-break, an exact tie). Remain: Cold-Blooded Crew held (3), 30/30
      in three new positions; Mikaeus tapping in his own turn (5), 24/30 in a new one, not a tie; stations with an
      attacker, 20 → 20. S1 rose (105 → 132), spread thin, not traced to the potential score.

## Next

**AI-19.23, the misplay scan** (2026-11-11): built, run (300 TRK games; 219 FRA — the run crashed in the .NET runtime
at 219, not reproduced), reviewed with Shayne item by item (Decisions). Fixed from it the same day: AI-13.11 (the potential
score) and AI-13.12 (the depth fix); the scan rerun on the same seeds (AI-19.23's task: S3a and S6 down by half or
more, the Crew and Mikaeus remain). Next: Borg Cube (AI-19.22) — Shayne reads #343's new Pass line (attack, then the −1/−1), then the match for
`Terms.DamagePotential` (ask first); the four lessons proposed as value changes, waiting for his go-ahead; then the Crew (3), Mikaeus (5) and the won-line ties (8); the smaller value items (Suus Mahna's bait — AI-19.21's
family; the free graveyard exile as a potential term, F5; Arni's end-step loot). Tools: `--lint --records=DIR` re-reads in
~20 s; `--record=FILE --guesses=30 --at=N`; `--explain` shows each line's potential. The scratch tools used for
boards and counts were in the session scratchpad and are gone.

## Log

- 2026-11-11: AI-19.23 — the scan rerun after AI-13.11/13.12 on the same 300 seeds; the VALUE items re-decided under 30
  guesses (findings, "the scan again").
- 2026-11-11: AI-19.23 — the misplay scan built (BlunderLint on TRK sealed, records kept and read, S1–S6) and run on 300
  TRK games; six VALUE misplays and four NOISE ones (`docs/findings/magic-ai.md`).
- 2026-11-05: opened with Engineered Explosives (MC-5.3 batch 6a); Deep Space 9 and Georgiou added (batch 6b).
- 2026-11-06: Lwaxana Troi, Brazen Bluff and the free casts added (MC-5.3 batch 7a).
- 2026-11-06: Genesis Device, Data, explore, Peace in Our Time and Kobayashi Maru added (MC-5.3 batch 7b).
- 2026-11-06: Phyrexian Tower added (MC-5.6 batch 1).
- 2026-11-12: AI-19.22's pre-commit fixes — the rollout's damage tie-break off under `+passbias` (no potential), the potential tie compared within rounding.
- 2026-11-12: AI-19.22 — #343 settled (Shayne: the Pass is reasonable); the station lessons moved to AI-23.
- 2026-11-12: AI-19.22's match — `beam` vs `beam+nodamagepotential` 50.0% over 96 (48–48), no difference, cost −1.5%; `Terms.DamagePotential` kept (`docs/findings/magic-ai.md`).
