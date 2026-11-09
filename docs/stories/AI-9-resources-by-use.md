---
id: AI-9
title: Resources priced by what they provide, not by how many there are
status: in-progress
area: AI
created: 2026-10-23
---

## Why

Shayne's principle (2026-10-22, `DesignNotes.md`: "MagicCore's evaluator values resources by count,
not by what they provide"): three lands put into a hand with ten in play are worth almost nothing,
yet score as three cards; loyalty only matters for what it buys when spent. AI-8 fixed the
instances its reports met (trample in the threat term, the opponent's walkers used in the look-ahead,
a walker priced by its best ability). This story is the rest.

## Requirements

- [ ] Each resource below priced by what it provides, each its own switch, measured as AI-6
      measures (the suite, then `--until-clear`) and kept by AI-5's rule
- [ ] Findings in `docs/findings/magic-ai.md`

## Decisions

- 2026-10-23, Shayne: a position built **by hand** may serve as the bad play AI-5's keep rule asks
  for (as the Rank Rat did) — but Shayne agrees the AI's current play there is wrong BEFORE the
  change, so a position can't be built to flatter it.
- 2026-10-28, Shayne (AI-9.2, from AI-13.3 — tried and dropped the same day, see AI-9.2): **life tapers from 10** — a point each up to 10, half a
  point from 10 to 20, a quarter above; the threat term (the clock) carries the danger. The positions:
  at 20 life with Unsummon, **hold** against a plain 3/3 and a vanilla 5-mana 3/4 attacking ("I'm at 5
  life, I can still take a hit"); still **bounce** a token, an Aura'd creature, a countered one; **hold**
  against Fateshaper Aspirant. Today it bounces the plain ones — for the damage at a point a life.
- 2026-10-27, Shayne (from AI-10.3): **a known card in the opponent's hand must not be worth the same
  as an unknown one** — a flaw in this evaluator AND MtgCore's (`MtgSimulator/WeightedStateEvaluator`
  counts each side's non-land hand cards at a flat 1.4, known or not). Try it first: AI-9.5.
- 2026-11-07, Shayne (AI-9.3, from MC-5.4's self-play ratings — FRA's walkers read far below 17lands): **"Planeswalkers
  are value over time engines, and need to be evaluated not just on their current impact on the board (although of
  course, some planeswalkers can have abilities that can immediately provide huge value), but on their value created
  over their lifetime (which could be anywhere from 1-X turns depending on if the opponent can pressure it or not)."**
  "I do not believe that our AI players are better than the typical 17 lands player" — so a walker reading below its
  17lands rate is the AI's fault: not cast (not valued), or the wrong abilities used. Not casting it because the game
  is already won is fine. Today's price (`Evaluator.Walker`): half of ONE best activation (tokens and draws only;
  every other effect 0, the flat body of 2) + 0.35 a loyalty — no lifetime, no pressure. The evidence to start from:
  `MagicConsole --card-games=planeswalkers` (games each walker was drawn in, lost and won) and
  `--record=FILE --card=NAME` (each turn it sat in hand: castable or not, what was done instead).
- 2026-11-07, Shayne (AI-9.3's first position — The Theorist, Jace Beleren discarded to Rank Rat on turn 4, two lands in
  play): **"keeping the island is better, keeping protege's awakening, hapatra the desert frost over jace is clearly not
  right. That Jace literally has the potential to win the game on its own."** The search's scores: discard The Theorist
  −6.32 (chosen), Island −6.79, Protege's Awakening −6.87, Infinite Coursework −7.69, Hapatra −10.17, Last Gasp −10.97 —
  the walker the cheapest spell to lose, its held price 3.3 (FRA's walkers 2.7–3.75 in hand, the bottom quarter; median
  8.0). Kept: `AiPositionTests.TheoristDiscardedOverCommons_KeepsTheWalker` (ignored, the known defect) and its control.
- 2026-11-07, Shayne (AI-9.3's scope): **value over time is not a planeswalker concept — it applies to every card that
  stays in play** (Dark Confidant, an artifact that draws, Inferno Titan, Sun Titan). "The AI seems to only value
  immediate impact right now … this causes it to undervalue the impact of a planeswalker" in discards, surveil and
  scry. AI-9.3 does walkers only, to keep the scope low; build the lifetime so other permanents can plug in later.
- 2026-11-07, Shayne, on the proposed price (what it makes a turn × the turns it lives; two plans, tick or cash;
  turns lived = loyalty against the power that gets past our blockers; 5 turns at 0.7 each): **"a fine thing to
  test to start."** **Every effect is priced** — mana, tap/stun, surveil/scry and ultimates/emblems included ("all of
  those do provide value in some way"). **A walker soaks damage:** "if an opponent attacks our walker instead of us,
  it is effectively gaining us life. This matters more at higher life totals than lower (since at lower the
  opponent might just kill us instead)." **The card-value table needs several boards**, not one: parity with
  nothing in play, parity in a board stall, ahead, behind — the price used the one closest to the current state.
- 2026-11-07, Shayne, the effect prices: **surveil/scry 0.33 of a card each** ("every time you surveil a useless card
  to the graveyard … it has effectively drawn you a card"); **"exile the top card, you may cast it" 0.8 of a card**
  (close to a draw, "technically still worse"); **mana a turn priced exactly as that many extra lands while it is in
  play**; stopping a creature's damage at its power × the turns × the life price; ultimates as proposed. **The several
  boards are Quadrant Theory** — how good a card is behind, at parity, ahead; good behind AND at parity is really good,
  good only ahead is "win-more". More boards over time as needed (enchantment or artifact removal). Built after AI-9.3,
  as its own task (AI-9.7), so a moved position can be traced to one change.
- 2026-11-07, Shayne, on a Jace token passing where it drew: **"Usually we will always want to draw a card here.
  Loyalty = Life should not be a 1-1 thing"** — "in 99.9% of cases, you are drawing a card of a jace that allows you
  to (assuming you can't use the loyalty for something else, or if you have surveil matters cards)". Soak taken at
  half a life point (`SoakShare` 0.5) — but the soak wasn't the cause: the second −3 kept for two turns later
  outweighed the card drawn now; each turn now 0.6 of the one before, not 0.7 (kept:
  `AJaceToken_DrawsWithItsMinusThree_RatherThanBankingTheLoyalty`).

Open questions:
- Which first — lands has a precedent to copy (below); life moves every attack decision.
- Is it wrong today in a way that shows? Search the 288 suite positions for decisions that turn on
  the price (discards, surveil, mulligans) before building.

## Tasks

- [ ] AI-9.1 Lands in hand by what they unlock — every land in our hand was a flat 0.3.
      **STARTED 2026-11-09 (Shayne's rule, from the surveil report AI-17.20):** keep a land only if it unlocks a card we hold
      or lets us do MORE next turn; otherwise it is surplus. Built: `LandUnlock.Unlocks` (pure; next turn's mana = sources +
      lands in hand, one drop this turn if still to come and one next, then the candidate; colours matched pip by pip -
      ponytail ceilings in its summary) with 5 position tests; `Evaluator.Terms.LandUnlock` (`+nolandunlock`) prices an OWN
      land in hand at 0.3 if it unlocks, else `LandSurplus` 0.05. Suite: 21 of 288 changed against 19 with it off, oracle
      agreement 84.7% -> 85.4%, cost unchanged; one change is Pass -> Play Plains. `BasicsTests.Landcycling_..`: its control
      (one ply, blind to the colour) now needs the switch OFF - one ply picks the Swamp that unlocks the Rat. The card-value
      sandbox measures with the term off (the tracked table is unchanged); the greedy golden replay moved in 6 games (after the review fix: land drops until we next cast = 2 only on our own turn with a drop to come, else 1; the candidate competes with the held lands for the drops - suite and match unchanged in reading, match run on the version before the fix)
      (regenerated). MATCH (2026-11-09, 9.9 min): `beam` vs `beam+nolandunlock`, 96 games, beam 51.0% +-1.0pp (49-47), early stop: NO difference of 5pp either way, so kept by AI-5's rule; cost per decision unchanged (3148 vs 3174 steps), 7.7 vs 7.6 lands played a game. NOT done:
      the library-top price for surveil (AI-17.15 / 17.20), the opponent's lands (blind by design)
      MtgSimulator's precedent: "a land is worth 0 on its own; its value is entirely the difference
      it makes to everything else" (`.claude/rules/sim-ai.md`)
- [ ] AI-9.2 Life by what it buys — **tried 2026-10-28 and dropped:** Shayne's taper from 10 (a point a
      life to 10, half to 20, a quarter above) measured no difference over 576 games (+2.8% cost),
      fixed none of his positions (the option value did, AI-13.4), and failed 16 tests by moving every
      evaluation (GreedyAi's golden games, the card-value table) — `docs/findings/magic-ai.md` keeps the
      formula. Still open: a life price that MEASURES better. Original task: — 1.0 a point up to 20, so 2 damage at 20 life outweighs a card
      (AI-8.1's second lever); the threat term already has the clock
- [x] AI-9.3 **A known defect since AI-17.3 (Shayne, 2026-10-29):** Chandra, Torch of Defiance is 3.40 at 4
      loyalty and 2.70 at 2 — body and loyalty only — so a whole attack chips her with a 2/2 and sends
      the 4/4 face; the ignored `RolloutTests.AWholeAttack_SendsTheFourPowerAtTheWalker_TheRestAtTheFace`
      is this task's test. Walker effects still unpriced (damage, mana, life, pumps, exile), and a 3-loyalty Jace
      cashed for a card still scores as a loss (AI-8.2 kept the price monotonic first); our own
      walkers' use
      **Built, not kept yet (2026-11-07):** `Evaluator.Terms.WalkerLifetime` (`+walkernow` is off),
      `MagicCore/Ai/Evaluator.Lifetime.cs` — the best sequence of abilities, one a turn, over 5 turns at 0.6
      each (0.7 banked a Jace token's loyalty — Decisions), the turns lived by the power past our blockers × the
      share sent at it (1 − 1 ÷ their turns to kill us), the damage it soaks at half the life price; every effect
      priced from its type (Decisions). Monotonic by a small search over (turn, loyalty left) — the first version
      ("use it as soon as affordable") was not, caught by the new pin `EveryWalkersLifetime_RisesWithEachCounter`.
      **Two positions flip**, each revert-checked: the Theorist (kept; the control re-measures the hand without
      the term and discards him again) and the Jace token's −3 (Shayne's; at 0.7 it passes). The Chandra attack
      test was found PASSING at HEAD with the old price — fixed by an earlier change while ignored; un-ignored as
      a plain test, no evidence for either price. Table (regenerated): every FRA walker rose but Ajani Resolute
      (his life-gain → loyalty loop unpriced) and Jace, Reality Sculptor (the Jace token's draws at the
      sandbox's flat 1.2; his −3 blunts attacks, worth 0 on the stall board — AI-9.7); Jace-token spells fell
      (Protege's Awakening, the Ways) — loyalty is no longer 0.35 a counter. Rank agreement of the whole table
      with 17lands' GIH WR 0.227 → 0.283 (before the review's fixes). Suite (`--suite=beam`): 16 of 288 changed; oracle agreement 86.8%
      against 88.5% for `beam+walkernow` (an oracle read with the old price), score given up 0.88 against
      1.00, wins missed 1 against 3, losses walked into 0 against 1 — the one "Jace [−3] → Pass" left (seed 3,
      move 43) is a won game, the pass winning sooner. Two controls drift with the table:
      `Unsummon_IsHeld_AgainstAPlainAttacker("plain")` (the blind card price rose with the walkers) and
      `AYouMay_…DeathbringersBeast` (without "you may", Way of the Deathbringer isn't cast at #257); GreedyAi's
      8 golden replays moved. **The two controls dropped (Shayne, 2026-11-07: "no point")** — their main assertions
      stay; the Unsummon control kept for the vanilla 3/4, where it still holds. **The match**
      (`--match=beam,beam+walkernow --until-clear`): 384 games, beam 49.2% ± 1.4pp (189–195) — no difference of
      5pp either way (the three decks' only walkers are White-Blue Jace's, 23–105 for both). **Kept (Shayne,
      2026-11-07)**. /pre-commit's review fixed (Shayne: "make the fixes"): life priced by the life term itself
      (`LifeValue` — a full point at 20, not ½; the low-life surcharge), an activation trigger only on a turn it
      activates, a new Jace token at the walker's price, the edict's filter, Body and the land price called not
      copied, surveil's rider from its effects — 6 more suite decisions moved (the match ran before them; rerun on
      2c5232ac: 288 games, beam 48.6% ± 1.3pp, 140–148, no 5pp difference either way). Suite
      accepted, the oracle re-read: beam 88.2% agreement, 0.85 given up, 1 win missed, 0 losses walked into,
      against `beam+walkernow` 85.8%, 1.09, 2, 1; table vs 17lands premier 0.257, sealed 0.310; GreedyAi's
      replays regenerated (10 of 12 moved); findings ("A planeswalker priced over its lifetime")
- [ ] AI-9.7 **Quadrant Theory boards for the card-value table** (Shayne, 2026-11-07): measure each card on four
      boards — parity empty, parity stall (today's ladders), ahead, behind — and blend the nearest boards' prices for
      the position in hand (a pick would jump). Fixes the symmetric-sweeper limit too. Its own switch, after AI-9.3
- [ ] AI-9.8 **The card-value table charged for mana cost** (Shayne, 2026-11-07: "discounting a cards power by its
      mana cost is probably the way to go"): the table measures a card once castable, each with its own lands, so a
      10-drop reads as free — against 17lands, our rank minus theirs by mana value runs −21 percentiles at 1 to +20
      at 5 (Emrakul, the Exigent Doom +81, Yargle, Goliath of Otaria +87). Beside it, card draw at the sandbox's
      flat 1.2 off a library of basics (findings, "AI-9.3"). Its own switch; after AI-9.7 or before — Shayne's call
- [ ] AI-9.4 The opponent's unseen hand at a flat 2.71 a card — by what it could cast with the mana
      and colours shown; and the RISK it carries (AI-13.4, 2026-10-28): Shayne holds Ferocity of the
      Hunt against an unseen Last Gasp "until the opponent plays their removal spell first", and the
      option value can't see a later answer to the enchanted creature. Its position:
      `AiPositionTests.FerocityAgainstLastGasp(known: false)` — today the AI casts it
      **A second case (Shayne's playtest, 2026-11-09; `2026-11-09_172959_seed550615157.json` #269, T10):** the AI at 5,
      Shayne at 13 with three small creatures; its Planetary Patrol 4/3 attacked (33.56 against -11.64 held), and his
      unseen Khaaaaaaaaaaaannn! (X=1) killed the other Patrol, so his three attackers killed it on T11. The look-ahead's
      opponent plays only cards the AI knows (`Rollout.Settings.KnownOnly`), so racing at 5 life risked nothing; held, two
      blockers would have left it alive. AI-3.4 measured an opponent casting its sampled unseen hand (`+unseen`): no 5pp
      difference then. Not kept as a record yet
- [ ] AI-9.6 A creature's activated abilities priced by what they make, as AI-8.2 priced a walker's
      (AI-14's first reading, Shayne: Yoshimaru, Scrappy Stray, with a {6} +1/+1 ability, traded into a
      spent Rank Rat — "the AI did not properly value our 1/1 compared to theirs"); card-agnostic.
      Position: game 1 seed 1 of `MagicConsole --decisions`, turn 6
      — **gone since AI-15** (2026-10-29: turn 6 no longer attacks), so out of sprint 3 until a new
      position shows it
- [x] AI-9.5 A KNOWN card in the opponent's hand priced as what it is (Shayne, 2026-10-27): today
      `Evaluator.Side` prices every card in their hand blind (`HeldValues.Unknown`), so a creature
      just bounced is "a card", and Unsummon reads as a kill spell (AI-10.3, #547). `Card.KnownTo`
      already says who knows a hidden card (a card returned from play is known to everyone) — price
      one the searcher knows at its value (`HeldValues.Of`), the rest blind as now. Its own switch
      (an `Evaluator.Terms`); the case: `GalliaWontAttack` before #547, where Shayne held Unsummon
      and the board-only search casts it — with the switch it should pass, without it (the control)
      cast. Then the suite and `--until-clear`. MtgCore has the same flaw; port only if asked.
      **Built, not kept yet (2026-10-27):** `Evaluator.Terms.KnownHand` (`+blindhand` is off). It
      fires (after the bounce, Fateshaper Aspirant is known to Everyone; priced 6.50 held against 2.70
      unknown), but #547 doesn't flip: Unsummon on it −24.1 → −27.9, Pass −38.5. With an honest
      casting opponent (`casts+unseen`) the term changes nothing (the creature is recast inside the
      look-ahead) and the search still bounces Gallia (−40.0 against −48.4). So #547 is not this
      term's blunder: Shayne (2026-10-27) held for patience, not price — it is AI-4.2's holding
      position and AI-13's lesson 4. By AI-5's keep rule the term needs a match that measures better.
      Suite: 2 decisions change (one bounce now takes a Cadet TOKEN over Heartstring Puller — a bounced
      token is gone, Shayne's value case), oracle agreement 83.3 → 83.0% against an oracle without the
      term. The match (`--match=beam,beam+blindhand --until-clear`): beam 49.0% ± 1.0pp over 96 games
      (47–49), no 5pp difference either way. **Kept (Shayne, 2026-10-27), on the token bounce** at
      seed 4, move 53 (`AiPositionTests.ABounce_TakesTheToken_…`, its control without the term). Suite
      accepted, the oracle re-read: beam 82.6% agreement (given up 0.87) against beam+blindhand 83.0%
      (0.78) — slightly worse on the positions, recorded in the findings

## Next

**AI-9.3 done (2026-11-07):** walkers priced over their lifetime, kept on two positions (the match: no difference).
Next here, both Shayne's from it: **AI-9.7** (Quadrant Theory boards for the card-value table) and **AI-9.8** (the table
charged for mana cost) — which first is his call. DATA-2's self-play ratings and TRK's real ratings were waiting on
AI-9.3: they need a rerun with the new AI. The table's other outliers (card draw at 1.2, 0-valued instants, empty
graveyards) are in the findings.

AI-9.5 done (2026-10-27): a known card in their hand is priced at its held value; kept on the token
bounce, slightly worse on the suite's oracle, no difference in the match (findings). Next here:
AI-10.4 (an honest casting opponent) is AI-10's. The rest is not this sprint (`docs/stories/README.md`). Read the DesignNotes entry above, `Evaluator.cs`
(`Walker`, `Pressure`, held values) and AI-8's story. A related candidate, not yet a story: the
rollout plays ONE spell a turn (`Rollout.Settings.ActionsPerTurn`, never measured alone — AI-8.2
found it hiding Way of the Wildspeaker); one switch and one match.

## Log

- 2026-10-23: created at sprint planning from the DesignNotes entry and AI-8's leftovers.
- 2026-10-27: AI-9.5 added (Shayne: a known card in their hand is not an unknown one).
- 2026-10-27: AI-9.5 built behind a switch; it fires but doesn't flip #547 (numbers in the task).
- 2026-10-27: AI-9.5 kept (Shayne) on the token bounce; match no difference (findings).
- 2026-11-07: AI-9.3 built (`WalkerLifetime`), the Theorist and a Jace token's −3 flip; match no difference; kept.
- 2026-11-07: AI-9.7 and AI-9.8 added (Shayne).
- 2026-11-07: the match rerun on the committed version (2c5232ac): no difference.
