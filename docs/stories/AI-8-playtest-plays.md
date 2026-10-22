---
id: AI-8
title: Plays that looked wrong at the table, rebuilt and fixed
status: in-progress
area: AI
created: 2026-10-20
---

## Why

Shayne's first game against the search (TBL-2, 2026-10-20: White-Black against the AI's Red-Green;
no seed saved) showed two AI plays that looked wrong. TBL-2's rule: an implausible AI play is an AI
story's — rebuilt as a position in `MagicCore.Tests/Ai/AiPositionTests.cs` (as AI-1 did the Rank Rat),
the cause found, then fixed or explained.

## Requirements

- [ ] Each report rebuilt as a position where the AI makes the play, before anything changes
- [ ] The cause found and named (evaluator, rollout model, choice answer, engine) before the fix
- [ ] Each fix measured as AI-6 measures (the suite, then `--until-clear` against `beam`) and kept by
      AI-5's rule: better, or no difference AND the rebuilt position fixed, with its test
- [ ] Findings recorded in `docs/findings/magic-ai.md`

## Decisions

- 2026-10-20, Shayne: noted now, fixed in a later session.

Open questions:
- Planeswalkers: is the fix in the evaluator (what a walker will produce each turn) or in the rollout
  (attacks on walkers judged like attacks on the player)? AI-5 deferred "abilities through play".

## Tasks

- [ ] AI-8.1 **It didn't attack a planeswalker that draws a card each turn.** Shayne's Jace token at 3
      loyalty, the AI's 2/2 able to attack it, Shayne at a high life total: the AI attacked the player
      (or held), and Jace drew another card. Not always wrong — "but at a high life total, very likely
      a mistake" (Shayne). Lead: the evaluator prices a planeswalker at 2 + 0.35 × loyalty — nothing
      for what its abilities produce every turn — so 2 loyalty off a walker scores ~0.7 against ~2 for
      2 life off the player; the threat term (AI-5.2) counts only the player's life
- [ ] AI-8.2 **It surveiled Way of the Wildspeaker into its graveyard** while it had the lands to cast
      it. Leads, unconfirmed: the card's measured value is 6.45 (held at half: ~3.2, well above a flat
      card), so the held-value pricing says keep it — look at who answered the surveil (the search
      branches a one-card surveil; GreedyAi answers a multi-card one by taking the minimum), whether
      its colour could be paid, and what the honest library mode makes of a known top card against a
      shuffled unknown one
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
      term reads 0 (from the code, not yet measured) — AI-8.4 and AI-8.5 below. Review: a first-striking
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
      `+notrample` still declines (uncommitted, 2026-10-22)
- [ ] AI-8.5 **A lethal board at the horizon is a loss one turn later**: if the opponent's next attack
      kills through our blockers where a rollout stops, score it as a loss at horizon + 1, decayed like
      any other — so "dead on turn 18" (−8145) and "dead on turn 20" (~−7700) are compared honestly,
      not against −17.91. New ground (MtgSimulator never had it). **No recorded blunder behind it
      now** — AI-8.4 fixed the report it came from; wait for one (a kept record) before building it

## Next

**AI-8.1 next** (suggested — AI-8.5 waits for a recorded blunder that needs it; Shayne to confirm):
the AI leaving a card-drawing planeswalker alone. It predates the records, so rebuild it by hand.

Every table game is now recorded (`game_records/`, TBL-5): a NEW report from a played game needs no
rebuilding — `MagicConsole --record=FILE --keep=NAME --at=N` makes it a test case (`Commands.md`).
AI-8.1 and 8.2 predate the records, so they are still rebuilt by hand: AI-8.1 first — a position with
the AI's 2/2, the opponent's planeswalker that draws on its plus, the opponent at a high life total —
`Decide` for `GreedyAi` and `beam`; then `Evaluator` (the planeswalker term) and `Rollout.Answer`.
For AI-8.2 the surveil choice is `ChooseFromLibraryTopChoice` (`MagicCore/Actions/LibraryChoices.cs`).

## Log

- 2026-10-20: created from Shayne's first playtest session (TBL-2) — two reports, no seed saved
  (TBL-5 would make the next one replayable).
- 2026-10-22: AI-8.3 from Shayne's sealed game (TBL-5's first real record) — the look-ahead's
  double-strike bug, found by tracing the oracle's rollouts; fix uncommitted, awaiting a measurement.
- 2026-10-22: AI-8.3 measured — the safe-block fix is better by 5pp+; kept. Status → in-progress.
- 2026-10-22: AI-8.4 — trample in the threat term; no difference, kept, and it is what fixes AI-8.3's report.
