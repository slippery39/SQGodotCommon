---
id: AI-8
title: Plays that looked wrong at the table, rebuilt and fixed
status: done
area: AI
created: 2026-10-20
---

## Why

Shayne's first game against the search (TBL-2, 2026-10-20: White-Black against the AI's Red-Green;
no seed saved) showed two AI plays that looked wrong. TBL-2's rule: an implausible AI play is an AI
story's — rebuilt as a position in `MagicCore.Tests/Ai/AiPositionTests.cs` (as AI-1 did the Rank Rat),
the cause found, then fixed or explained.

## Requirements

- [x] Each report rebuilt as a position where the AI makes the play, before anything changes
- [x] The cause found and named (evaluator, rollout model, choice answer, engine) before the fix
- [x] Each fix measured as AI-6 measures (the suite, then `--until-clear` against `beam`) and kept by
      AI-5's rule: better, or no difference AND the rebuilt position fixed, with its test
- [x] Findings recorded in `docs/findings/magic-ai.md`

## Decisions

- 2026-10-20, Shayne: noted now, fixed in a later session.

- 2026-10-23, Shayne: AI-8.5 (a lethal board at the horizon scored as a loss) **deferred** to
  `DesignNotes.md` ("MagicCore search: a lethal board at the horizon") — AI-8.4 fixed the report it
  came from; it waits for a recorded blunder that needs it.

Open questions:
- Planeswalkers: is the fix in the evaluator (what a walker will produce each turn) or in the rollout
  (attacks on walkers judged like attacks on the player)? AI-5 deferred "abilities through play".

## Tasks

- [x] AI-8.1 **It didn't attack a planeswalker that draws a card each turn.** Shayne's Jace token at 3
      loyalty, the AI's 2/2 able to attack it, Shayne at a high life total: the AI attacked the player
      (or held), and Jace drew another card. Not always wrong — "but at a high life total, very likely
      a mistake" (Shayne). Lead: the evaluator prices a planeswalker at 2 + 0.35 × loyalty — nothing
      for what its abilities produce every turn — so 2 loyalty off a walker scores ~0.7 against ~2 for
      2 life off the player; the threat term (AI-5.2) counts only the player's life
      **Rebuilt and traced (2026-10-22):** a 2/2 against a Jace token at 3 (which can −3 to draw), no
      blockers — GreedyAi, the search and the oracle all attack the player, at 20 life and at 8. The
      settled scores: face +2.11, Jace +0.70 (2 loyalty × 0.35). The rollouts never show Jace drawing:
      the default simulated opponent (BoardOnly) never uses loyalty — and even the casting one won't,
      because by the evaluator a −3 that draws and kills the token LOSES its owner value (the walker's
      2 + 1.05 against one card). So the cause is the evaluator's walker term, not the rollout: a
      walker is worth nothing for what its abilities will still produce, and the AI-8 open question
      ("evaluator or rollout?") is answered — evaluator. A second lever: life is valued at 1.0 a point
      up to 20, so 2 damage at 20 life outweighs a card (1.2 flat)
      **Fix chosen (Shayne, 2026-10-22):** not the evaluator — valuing a walker's unspent abilities
      would also stop the AI using its OWN Jace (the 0.8 trap again; no numbers satisfy both). Instead
      the rollout: "assume the opponent will use its best planeswalker ability" — the simulated
      opponent, whatever its model, uses the loyalty ability with the best EFFECT every turn it can
      (`Rollout.TheirLoyalty`; the walker's own worth added back, else a Jace at 3 surveiled rather
      than drew — caught by checking which ability fired). Our own walkers are unchanged: an
      asymmetry Shayne chose. Positions: `AiPositionTests.AJaceThatWouldDraw_IsAttacked_…` — a 2/2
      attacks a Jace at 3 (control: `+idlewalkers` goes face), not one at 1, nor a 5/5 one at 2.
      Suite: 14 of 288 changed (4.9%) — removal aimed at Jace, attacks on Jace. A first match, on a
      version whose Jace surveiled, was stopped unread. **Match: 288 games, 145–143, 50.3% ± 1.6pp —
      no difference of 5pp either way; kept, the positions fixed** (+2.2% steps). Suite accepted,
      oracle re-read (80.6% against 77.8%). Shayne's wider point — resources valued by count, not by
      what they provide — is in DesignNotes (8af6bca, 2026-10-22)
- [x] AI-8.2 **It surveiled Way of the Wildspeaker into its graveyard** while it had the lands to cast
      it. Leads, unconfirmed: the card's measured value is 6.45 (held at half: ~3.2, well above a flat
      card), so the held-value pricing says keep it — look at who answered the surveil (the search
      branches a one-card surveil; GreedyAi answers a multi-card one by taking the minimum), whether
      its colour could be paid, and what the honest library mode makes of a known top card against a
      shuffled unknown one
      **Rebuilt and traced (2026-10-23):** five Forests, its Jace at 2 uses −1, Way on top (unknown
      until surveiled). Alone, every AI keeps it (search 11.59 against 8.64) — and with Jace at 1, one
      Forest, or an opponent pressing. **With a hand of four spells it bins it** (keep 49.18, bin
      50.08). Cause: the rollout's own policy casts ONE spell a turn (`Rollout.Settings.ActionsPerTurn`
      = 1, MtgSimulator's starting point, never measured alone here), so a fifth spell is never cast
      inside the look-ahead — a known Way is worth only its held price, about a random card's. Not
      the plan replay (a sampled library moves ids, so a planned pick can't replay), not the colour,
      not the held value. With 2 plays a turn: keep 62.80 against 50.08; 3, the same; GreedyAi
      rollouts the same. **The real driver, measured (Shayne: "it's a 4/4 that draws a card"):** the
      four spells were stand-ins (no record of the game); casting Way scores only +1.23 — its +7
      loyalty at 0.35 a counter, less the card leaving hand — against Tam's Resistance +1.45, so the
      one-play look-ahead never casts it; the 4/4 it enables (Jace −4) scores +13.40 but only as the
      NEXT play. Loyalty valued by count, not by what it buys — Shayne's principle exactly. Two plays a
      turn hides it here; valuing a walker's loyalty by what its abilities make is the real fix.
      **Built (Shayne: "start the real fix now"):** `Evaluator.Walker` = max(2, ½ × what its best
      affordable ability makes — a creature token, a card drawn) + 0.35 a counter; `+walkercount` the
      old price. The first version was not monotonic (caught in review; its match stopped) — the
      floor of 2 fixes it, pinned by a test. **Match: 192 games, 98–94, 51.0% ± 1.8pp — no
      difference of 5pp either way; kept, the play fixed:** `WayOfTheWildspeaker_IsCastOverAPump`
      (control: the pump). Card values moved for 4 cards (Way of the Wildspeaker 6.45 → 10.85);
      suite accepted, oracle re-read (81.6% against 80.2%) (8732290, 2026-10-23)
- [x] AI-8.3 **It wouldn't trade Divining Duelist (4/3) for a Beast (4/4) at 22 life** — Shayne's
      sealed game (2026-10-22, kept: `Records/DuelistWontTrade.json`, move #332; the default search,
      the table's and the oracle all decline). Cause found (2026-10-22): the look-ahead's "blocks only
      what it survives" (`Lookahead.QuickAnswer`) counted a DOUBLE STRIKER's damage once — under
      Yuriko the Duelist deals 4 twice — so in every look-ahead the opponent's Crusher (7/5) blocked it
      and died, which made keeping the Duelist look decisive (−75 against −8145, a loss on turn 18).
      Fixed in `Lookahead.Survives` (double strike, deathtouch, damage marked): the simulated Shayne now
      chumps with the 1/1, as he did. The AI STILL declines the trade — now for the horizon: the block
      line dies on turn 18, inside the look-ahead (−8145); the no-block line is alive at 3 life on turn
      19, lost a turn later, and scores −17.91. Both lines lose; which is better is a toss-up.
      Suite: 4 of 288 answers change (1.4%). **Measured: `beam` vs `beam+oldblocks`, 288 games,
      155–133, 53.8% ± 1.3pp — better by 5pp or more; kept** (findings). Suite answers accepted, the
      oracle re-read, GreedyAi's recorded games moved by one; the position is
      `AiPositionTests.DuelistWontTrade_AtTwentyTwoLife_StillDeclines` (its control: still declines).
      Rechecked against MtgSimulator (Shayne, 2026-10-22): its horizon fixes were the terminal
      discount and the absolute horizon (both ported), the pre-search lethal check (not ported — OUR
      lethal, not this) and race pressure. None scored "lost one turn past the horizon"; race pressure
      was its only signal, and our port counts blockers with trample ignored ("count them if a
      misjudged race is ever shown") — at this horizon Yuriko "stops" the trampling Crusher, so the
      term reads 0 (from the code, not yet measured) — AI-8.4 below; AI-8.5, deferred (Decisions). Review: a first-striking
      blocker that kills a double striker first takes one hit, not two (moves no suite answer and no
      recorded game) (441fd6e, 2026-10-22)
- [x] AI-8.4 **Trample in the threat term** (`Evaluator.Pressure`): a trampler meets the blocker that
      absorbs most and gets past what it can't absorb by the next attack (lasting toughness, 1
      against deathtouch; end-of-turn trample doesn't count); `+notrample` reproduces the old
      answers. **Match on the final version (rerun after review moved 1 answer): 288 games, 141–147,
      49.0% ± 1.2pp — no difference of 5pp either way; kept, the rebuilt position fixed:** at #332
      the search and the oracle now TRADE the Duelist (findings). Suite answers accepted, the oracle
      re-read (79.5% against 79.2%), GreedyAi's recorded games moved (all with Red-Green). The
      position's test is `DuelistWontTrade_AtTwentyTwoLife_TradesOnceTrampleCounts` — control:
      `+notrample` still declines (0f35401, 2026-10-22)
## Next

**Done.** Every report is rebuilt, its cause named, and fixed and measured. Deferred: AI-8.5 (a
lethal board at the horizon) and the rest of Shayne's principle — resources valued by what they
provide (DesignNotes: "values resources by count"): unpriced walker effects, a Jace cashed for one
card, lands in hand by what they unlock. A new report from a recorded game starts with
`MagicConsole --record=FILE --keep=NAME --at=N` (Commands.md).

## Log

- 2026-10-20: created from Shayne's first playtest session (TBL-2) — two reports, no seed saved
  (TBL-5 would make the next one replayable).
- 2026-10-22: AI-8.3 from Shayne's sealed game (TBL-5's first real record) — the look-ahead's
  double-strike bug, found by tracing the oracle's rollouts; fix uncommitted, awaiting a measurement.
- 2026-10-22: AI-8.3 measured — the safe-block fix is better by 5pp+; kept. Status → in-progress.
- 2026-10-22: AI-8.4 — trample in the threat term; no difference, kept, and it is what fixes AI-8.3's report.
- 2026-10-22: AI-8.1 — the simulated opponent uses its planeswalkers; no difference, kept, the Jace positions fixed.
- 2026-10-23: AI-8.5 deferred to DesignNotes; AI-8.2 started.
- 2026-10-23: AI-8.2 — a planeswalker priced by what its best ability makes; no difference, kept. AI-8 done.
