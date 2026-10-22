---
id: AI-8
title: Plays that looked wrong at the table, rebuilt and fixed
status: ready
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

## Next

Every table game is now recorded (`game_records/`, TBL-5): a NEW report from a played game needs no
rebuilding — `MagicConsole --record=FILE --keep=NAME --at=N` makes it a test case (`Commands.md`).
AI-8.1 and 8.2 predate the records, so they are still rebuilt by hand.

Rebuild AI-8.1 first: a position with the AI's 2/2, the opponent's planeswalker that draws on its
plus, the opponent at a high life total — `Decide` for `GreedyAi` and `beam`. Then read `Evaluator`
(the planeswalker term) and `Rollout.Answer` (attack options: the player and each walker). For AI-8.2
the surveil choice is `ChooseFromLibraryTopChoice` (`MagicCore/Actions/LibraryChoices.cs`).

## Log

- 2026-10-20: created from Shayne's first playtest session (TBL-2) — two reports, no seed saved
  (TBL-5 would make the next one replayable).
